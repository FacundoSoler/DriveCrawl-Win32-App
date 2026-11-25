using Dapper;
using DriveCrawl_Win32_App.Models;
using Microsoft.Data.Sqlite;

namespace DriveCrawl_Win32_App.Data
{
    public record SearchLocationChanges(
        IReadOnlyList<WatchedFolders> Added,
        IReadOnlyList<WatchedFolders> Updated,
        IReadOnlyList<int> RemovedIds)
    {
        public bool IsEmpty => Added.Count == 0 && Updated.Count == 0 && RemovedIds.Count == 0;
    }

    public interface ISearchLocationStore
    {
        IReadOnlyList<WatchedFolders> GetAll();

        void SaveChanges(SearchLocationChanges changes);
    }

    /// <summary>
    /// Reads and writes the WatchedFolders table, which the UI calls "search locations".
    /// </summary>
    public class SearchLocationRepository : ISearchLocationStore
    {
        private readonly string _connectionString;

        public SearchLocationRepository(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

            _connectionString = connectionString;
        }

        public IReadOnlyList<WatchedFolders> GetAll()
        {
            using var connection = new SqliteConnection(_connectionString);
            // COALESCE because the original hand-made schema allowed NULL in both columns.
            return connection.Query<WatchedFolders>(
                "SELECT Id, Path, COALESCE(IsExternal, 0) AS IsExternal, COALESCE(Priority, 1) AS Priority " +
                "FROM WatchedFolders ORDER BY Priority, Path COLLATE NOCASE").ToList();
        }

        public void SaveChanges(SearchLocationChanges changes)
        {
            ArgumentNullException.ThrowIfNull(changes);
            if (changes.IsEmpty) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            // Removals go first so that a path freed by one row can be reused by an added or edited row.
            connection.Execute("DELETE FROM WatchedFolders WHERE Id = @Id",
                changes.RemovedIds.Select(id => new { Id = id }), transaction);
            connection.Execute("UPDATE WatchedFolders SET Path = @Path, IsExternal = @IsExternal, Priority = @Priority WHERE Id = @Id",
                changes.Updated, transaction);
            connection.Execute("INSERT INTO WatchedFolders (Path, IsExternal, Priority) VALUES (@Path, @IsExternal, @Priority)",
                changes.Added, transaction);

            transaction.Commit();
        }
    }
}
