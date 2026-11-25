using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Data;
using Microsoft.Data.Sqlite;

namespace DriveCrawl.Tests.Data;

public sealed class DatabaseServiceTests : IDisposable
{
    private const string WatchedFoldersSchema = """
        CREATE TABLE WatchedFolders (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            Path TEXT NOT NULL UNIQUE,
            IsExternal INTEGER DEFAULT 0,
            Priority INTEGER DEFAULT 1);
        """;

    private readonly TempDirectory _temp = new();
    private readonly string _connectionString;

    public DatabaseServiceTests()
    {
        // Pooling off so the temp file is released and can be deleted when the test ends.
        _connectionString = $"Data Source={Path.Combine(_temp.Path, "test.db")};Pooling=False";
    }

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankConnectionString_Throws(string? connectionString)
    {
        Should.Throw<ArgumentException>(() => new DatabaseService(connectionString!));
    }

    [Fact]
    public void GetFolders_ReturnsAllFoldersOrderedByPriority()
    {
        Execute(WatchedFoldersSchema);
        Execute("""
            INSERT INTO WatchedFolders (Path, IsExternal, Priority) VALUES
                ('D:\Downloads', 0, 3),
                ('C:\Videos', 0, 1),
                ('J:\Temp', 1, 2);
            """);

        var folders = new DatabaseService(_connectionString).GetFolders();

        folders.Select(f => (f.Path, f.IsExternal, f.Priority)).ShouldBe(new[]
        {
            (@"C:\Videos", 0, 1),
            (@"J:\Temp", 1, 2),
            (@"D:\Downloads", 0, 3),
        });
        folders.ShouldAllBe(f => f.Id > 0);
    }

    [Fact]
    public void GetFolders_AppliesColumnDefaults()
    {
        Execute(WatchedFoldersSchema);
        Execute(@"INSERT INTO WatchedFolders (Path) VALUES ('C:\Only');");

        var folder = new DatabaseService(_connectionString).GetFolders().ShouldHaveSingleItem();

        folder.IsExternal.ShouldBe(0);
        folder.Priority.ShouldBe(1);
    }

    [Fact]
    public void GetFolders_EmptyTable_ReturnsEmptyList()
    {
        Execute(WatchedFoldersSchema);

        new DatabaseService(_connectionString).GetFolders().ShouldBeEmpty();
    }

    [Fact]
    public void GetFolders_MissingTable_ThrowsSqliteException()
    {
        // Mirrors a fresh clone: the DB file is absent, SQLite creates an empty one, and the query fails.
        Should.Throw<SqliteException>(() => new DatabaseService(_connectionString).GetFolders())
            .Message.ShouldContain("no such table: WatchedFolders");
    }

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
