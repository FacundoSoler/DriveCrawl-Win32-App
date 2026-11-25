using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Data;
using Microsoft.Data.Sqlite;

namespace DriveCrawl.Tests.Data;

public sealed class SchemaMigratorTests : IDisposable
{
    // The hand-made schema that pre-migration MediaLibrary.db files have, at user_version 0.
    private const string LegacySchema = """
        CREATE TABLE "Files" ("Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "FullPath" TEXT NOT NULL UNIQUE,
            "FileName" TEXT NOT NULL, "Extension" TEXT, "FileSize" INTEGER, "CreatedAt" DATETIME,
            "LastAccessed" DATETIME, "ThumbnailPath" TEXT, "DriveLabel" TEXT);
        CREATE TABLE "Tags" ("Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, "TagName" TEXT NOT NULL UNIQUE);
        CREATE TABLE "FileTags" ("FileId" INTEGER NOT NULL, "TagId" INTEGER NOT NULL,
            FOREIGN KEY("FileId") REFERENCES "Files"("Id") ON DELETE CASCADE,
            FOREIGN KEY("TagId") REFERENCES "Tags"("Id") ON DELETE CASCADE, PRIMARY KEY("FileId","TagId"));
        CREATE TABLE "WatchedFolders" ("Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Path" TEXT NOT NULL UNIQUE, "IsExternal" INTEGER DEFAULT 0, Priority INTEGER DEFAULT 1);
        CREATE INDEX "idx_file_name" ON "Files" ("FileName");
        CREATE INDEX "idx_full_path" ON "Files" ("FullPath");
        """;

    private readonly TempDirectory _temp = new();
    private readonly string _databasePath;
    private readonly string _connectionString;

    public SchemaMigratorTests()
    {
        _databasePath = Path.Combine(_temp.Path, "library.db");
        // Pooling off so the temp file is released and can be deleted when the test ends.
        _connectionString = DatabaseLocation.BuildConnectionString(_databasePath) + ";Pooling=False";
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void EmbeddedMigrations_AreSequentialFromOne_AndNotEmpty()
    {
        var migrations = EmbeddedSchemaMigrations.Load();

        migrations.Select(m => m.Version).ShouldBe(Enumerable.Range(1, migrations.Count));
        migrations.ShouldAllBe(m => !string.IsNullOrWhiteSpace(m.Sql) && !string.IsNullOrWhiteSpace(m.Name));
        migrations[0].Name.ShouldBe("initial_schema");
    }

    [Fact]
    public void FreshDatabase_IsCreatedAtLatestVersion_WithAllTables()
    {
        var migrator = CreateMigrator();

        int applied = migrator.MigrateToLatest();

        applied.ShouldBe(migrator.LatestVersion);
        migrator.GetCurrentVersion().ShouldBe(migrator.LatestVersion);
        Tables().ShouldBe(new[] { "FileTags", "Files", "Tags", "WatchedFolders" });
    }

    [Fact]
    public void FreshDatabase_ContainsNoRows()
    {
        CreateMigrator().MigrateToLatest();

        foreach (var table in new[] { "WatchedFolders", "Files", "Tags", "FileTags" })
        {
            Scalar<long>($"SELECT COUNT(*) FROM {table};").ShouldBe(0, table);
        }
    }

    [Fact]
    public void FreshDatabase_HasFileNameIndex_ButNotTheRedundantFullPathIndex()
    {
        CreateMigrator().MigrateToLatest();

        var indexes = Indexes();
        indexes.ShouldContain("idx_file_name");
        indexes.ShouldNotContain("idx_full_path");
    }

    [Fact]
    public void FreshDatabase_DeclaresCascadingForeignKeysOnFileTags()
    {
        CreateMigrator().MigrateToLatest();

        var foreignKeys = Query("SELECT \"table\", on_delete FROM pragma_foreign_key_list('FileTags') ORDER BY \"table\";",
            r => (Table: r.GetString(0), OnDelete: r.GetString(1)));

        foreignKeys.ShouldBe(new[] { ("Files", "CASCADE"), ("Tags", "CASCADE") });
    }

    [Fact]
    public void MigrateToLatest_IsIdempotent()
    {
        var migrator = CreateMigrator();
        migrator.MigrateToLatest();

        migrator.MigrateToLatest().ShouldBe(0);
        migrator.GetCurrentVersion().ShouldBe(migrator.LatestVersion);
    }

    [Fact]
    public void LegacyDatabase_IsAdoptedWithoutLosingRows()
    {
        Execute(LegacySchema);
        Execute("""INSERT INTO WatchedFolders (Path, IsExternal, Priority) VALUES ('X:\one', 0, 2), ('Y:\two', 1, 1);""");
        var migrator = CreateMigrator();

        migrator.MigrateToLatest();

        migrator.GetCurrentVersion().ShouldBe(migrator.LatestVersion);
        Scalar<long>("SELECT COUNT(*) FROM WatchedFolders;").ShouldBe(2);
        Indexes().ShouldNotContain("idx_full_path");
    }

    [Fact]
    public void LegacyDatabase_IsStillReadableByDatabaseService()
    {
        Execute(LegacySchema);
        Execute("""INSERT INTO WatchedFolders (Path, Priority) VALUES ('X:\one', 2), ('Y:\two', 1);""");

        CreateMigrator().MigrateToLatest();

        new DatabaseService(_connectionString).GetFolders().Select(f => f.Path).ShouldBe(new[] { @"Y:\two", @"X:\one" });
    }

    [Fact]
    public void ForeignKeys_AreEnforced_ThroughTheAppConnectionString()
    {
        CreateMigrator().MigrateToLatest();

        Should.Throw<SqliteException>(() => Execute("INSERT INTO FileTags (FileId, TagId) VALUES (999, 999);"))
            .SqliteErrorCode.ShouldBe(19);
    }

    [Fact]
    public void DeletingAFile_CascadesToItsTags()
    {
        CreateMigrator().MigrateToLatest();
        Execute("""
            INSERT INTO Files (Id, FullPath, FileName) VALUES (1, 'C:\a.mp4', 'a.mp4');
            INSERT INTO Tags (Id, TagName) VALUES (1, 'holiday');
            INSERT INTO FileTags (FileId, TagId) VALUES (1, 1);
            """);

        Execute("DELETE FROM Files WHERE Id = 1;");

        Scalar<long>("SELECT COUNT(*) FROM FileTags;").ShouldBe(0);
        Scalar<long>("SELECT COUNT(*) FROM Tags;").ShouldBe(1);
    }

    [Fact]
    public void WatchedFolders_AppliesDefaults_AndRejectsDuplicatePaths()
    {
        CreateMigrator().MigrateToLatest();
        Execute("""INSERT INTO WatchedFolders (Path) VALUES ('C:\media');""");

        Scalar<long>("SELECT IsExternal FROM WatchedFolders;").ShouldBe(0);
        Scalar<long>("SELECT Priority FROM WatchedFolders;").ShouldBe(1);
        Should.Throw<SqliteException>(() => Execute("""INSERT INTO WatchedFolders (Path) VALUES ('C:\media');"""));
    }

    [Fact]
    public void FailingMigration_IsRolledBack_AndEarlierMigrationsStay()
    {
        var migrator = new SchemaMigrator(_connectionString, new[]
        {
            new SchemaMigration(1, "good", "CREATE TABLE First (Id INTEGER);"),
            new SchemaMigration(2, "bad", "CREATE TABLE Second (Id INTEGER); THIS IS NOT SQL;"),
        });

        var ex = Should.Throw<InvalidOperationException>(() => migrator.MigrateToLatest());

        ex.Message.ShouldStartWith("Schema migration 0002 (bad) failed");
        migrator.GetCurrentVersion().ShouldBe(1);
        Tables().ShouldBe(new[] { "First" });
    }

    [Fact]
    public void DatabaseNewerThanTheApp_IsRefused()
    {
        Execute("PRAGMA user_version = 99;");
        var migrator = CreateMigrator();

        Should.Throw<InvalidOperationException>(() => migrator.MigrateToLatest())
            .Message.ShouldContain("version 99");
    }

    [Theory]
    [InlineData(new[] { 2 })]
    [InlineData(new[] { 1, 1 })]
    [InlineData(new[] { 1, 3 })]
    public void Constructor_RejectsNonSequentialVersions(int[] versions)
    {
        var migrations = versions.Select(v => new SchemaMigration(v, $"m{v}", "SELECT 1;")).ToList();

        Should.Throw<ArgumentException>(() => new SchemaMigrator(_connectionString, migrations));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_RejectsBlankConnectionString(string? connectionString)
    {
        Should.Throw<ArgumentException>(() => new SchemaMigrator(connectionString!, Array.Empty<SchemaMigration>()));
    }

    [Fact]
    public void DatabaseInitializer_CreatesMissingDirectoryAndDatabase()
    {
        string nestedPath = Path.Combine(_temp.Path, "new", "folder", DatabaseLocation.DatabaseFileName);

        DatabaseInitializer.Initialize(nestedPath);
        SqliteConnection.ClearAllPools();

        File.Exists(nestedPath).ShouldBeTrue();
    }

    [Fact]
    public void BuildConnectionString_EnablesForeignKeys()
    {
        new SqliteConnectionStringBuilder(DatabaseLocation.BuildConnectionString(@"C:\x\y.db")).ForeignKeys.ShouldBe(true);
    }

    private SchemaMigrator CreateMigrator() => new(_connectionString, EmbeddedSchemaMigrations.Load());

    private List<string> Tables() =>
        Query("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;", r => r.GetString(0));

    private List<string> Indexes() =>
        Query("SELECT name FROM sqlite_master WHERE type = 'index';", r => r.GetString(0));

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private T Scalar<T>(string sql)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)command.ExecuteScalar()!;
    }

    private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
            rows.Add(map(reader));
        return rows;
    }
}
