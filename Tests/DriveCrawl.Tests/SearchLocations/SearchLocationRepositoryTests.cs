using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using Microsoft.Data.Sqlite;

namespace DriveCrawl.Tests.SearchLocations;

public sealed class SearchLocationRepositoryTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _connectionString;
    private readonly SearchLocationRepository _repository;

    public SearchLocationRepositoryTests()
    {
        // Pooling off so the temp file is released and can be deleted when the test ends.
        _connectionString = DatabaseLocation.BuildConnectionString(Path.Combine(_temp.Path, "library.db")) + ";Pooling=False";
        new SchemaMigrator(_connectionString, EmbeddedSchemaMigrations.Load()).MigrateToLatest();
        _repository = new SearchLocationRepository(_connectionString);
    }

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_BlankConnectionString_Throws(string? connectionString)
    {
        Should.Throw<ArgumentException>(() => new SearchLocationRepository(connectionString!));
    }

    [Fact]
    public void GetAll_EmptyTable_ReturnsEmpty()
    {
        _repository.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void SaveChanges_Added_ArePersistedWithGeneratedIds()
    {
        _repository.SaveChanges(Changes(added: new[] { Folder(@"D:\Media", 2, external: true) }));

        var saved = _repository.GetAll().ShouldHaveSingleItem();
        saved.Id.ShouldBeGreaterThan(0);
        saved.Path.ShouldBe(@"D:\Media");
        saved.Priority.ShouldBe(2);
        saved.IsExternal.ShouldBe(1);
    }

    [Fact]
    public void GetAll_OrdersByPriorityThenPathIgnoringCase()
    {
        _repository.SaveChanges(Changes(added: new[]
        {
            Folder(@"D:\b", 2), Folder(@"D:\C", 1), Folder(@"D:\a", 2),
        }));

        _repository.GetAll().Select(f => f.Path).ShouldBe(new[] { @"D:\C", @"D:\a", @"D:\b" });
    }

    [Fact]
    public void SaveChanges_Updated_ChangePathPriorityAndExternalFlag()
    {
        _repository.SaveChanges(Changes(added: new[] { Folder(@"D:\Old", 1) }));
        var existing = _repository.GetAll().Single();

        _repository.SaveChanges(Changes(updated: new[] { new WatchedFolders { Id = existing.Id, Path = @"E:\New", Priority = 4, IsExternal = 1 } }));

        var updated = _repository.GetAll().ShouldHaveSingleItem();
        (updated.Id, updated.Path, updated.Priority, updated.IsExternal).ShouldBe((existing.Id, @"E:\New", 4, 1));
    }

    [Fact]
    public void SaveChanges_Removed_DeletesOnlyThoseRows()
    {
        _repository.SaveChanges(Changes(added: new[] { Folder(@"D:\Keep", 1), Folder(@"D:\Drop", 2) }));
        int dropId = _repository.GetAll().Single(f => f.Path == @"D:\Drop").Id;

        _repository.SaveChanges(Changes(removed: new[] { dropId }));

        _repository.GetAll().ShouldHaveSingleItem().Path.ShouldBe(@"D:\Keep");
    }

    [Fact]
    public void SaveChanges_RemovesBeforeAdding_SoAPathCanBeReplacedInOneSave()
    {
        _repository.SaveChanges(Changes(added: new[] { Folder(@"D:\Media", 1) }));
        int oldId = _repository.GetAll().Single().Id;

        _repository.SaveChanges(Changes(added: new[] { Folder(@"D:\Media", 3) }, removed: new[] { oldId }));

        var replaced = _repository.GetAll().ShouldHaveSingleItem();
        replaced.Priority.ShouldBe(3);
        replaced.Id.ShouldNotBe(oldId);
    }

    [Fact]
    public void SaveChanges_IsAtomic_WhenAnyStatementFails()
    {
        _repository.SaveChanges(Changes(added: new[] { Folder(@"D:\Existing", 1) }));
        int existingId = _repository.GetAll().Single().Id;

        Should.Throw<SqliteException>(() => _repository.SaveChanges(Changes(
            added: new[] { Folder(@"D:\Dup", 2), Folder(@"D:\Dup", 3) },
            updated: new[] { new WatchedFolders { Id = existingId, Path = @"D:\Renamed", Priority = 1 } })));

        // The UPDATE ran before the second INSERT broke the UNIQUE constraint, but the whole batch rolled back.
        _repository.GetAll().ShouldHaveSingleItem().Path.ShouldBe(@"D:\Existing");
    }

    [Fact]
    public void GetAll_TreatsLegacyNullColumnsAsDefaults()
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"INSERT INTO WatchedFolders (Path, IsExternal, Priority) VALUES ('C:\Legacy', NULL, NULL);";
            command.ExecuteNonQuery();
        }

        var legacy = _repository.GetAll().ShouldHaveSingleItem();
        legacy.IsExternal.ShouldBe(0);
        legacy.Priority.ShouldBe(1);
    }

    [Fact]
    public void SaveChanges_EmptyChanges_DoesNothing()
    {
        Should.NotThrow(() => _repository.SaveChanges(Changes()));
    }

    private static WatchedFolders Folder(string path, int priority, bool external = false) =>
        new() { Path = path, Priority = priority, IsExternal = external ? 1 : 0 };

    private static SearchLocationChanges Changes(
        IReadOnlyList<WatchedFolders>? added = null,
        IReadOnlyList<WatchedFolders>? updated = null,
        IReadOnlyList<int>? removed = null) =>
        new(added ?? Array.Empty<WatchedFolders>(), updated ?? Array.Empty<WatchedFolders>(), removed ?? Array.Empty<int>());
}
