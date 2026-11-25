using Microsoft.Data.Sqlite;

namespace DriveCrawl_Win32_App.Data
{
    /// <summary>
    /// Brings a SQLite database up to the latest schema by applying, in order, every migration whose
    /// version is above the database's PRAGMA user_version. Each migration runs in its own transaction.
    /// </summary>
    public class SchemaMigrator
    {
        private readonly string _connectionString;
        private readonly IReadOnlyList<SchemaMigration> _migrations;

        public int LatestVersion => _migrations.Count == 0 ? 0 : _migrations[^1].Version;

        public SchemaMigrator(string connectionString, IReadOnlyList<SchemaMigration> migrations)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
            ArgumentNullException.ThrowIfNull(migrations);

            EnsureVersionsAreSequentialFromOne(migrations);
            _connectionString = connectionString;
            _migrations = migrations.OrderBy(m => m.Version).ToList();
        }

        public int GetCurrentVersion()
        {
            using var connection = OpenConnection();
            return ReadUserVersion(connection);
        }

        public int MigrateToLatest()
        {
            using var connection = OpenConnection();
            int currentVersion = ReadUserVersion(connection);
            EnsureDatabaseIsNotNewerThanApp(currentVersion);

            var pending = _migrations.Where(m => m.Version > currentVersion).ToList();
            foreach (var migration in pending)
            {
                ApplyMigration(connection, migration);
            }

            return pending.Count;
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private static int ReadUserVersion(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void ApplyMigration(SqliteConnection connection, SchemaMigration migration)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                ExecuteNonQuery(connection, transaction, migration.Sql);
                // PRAGMA cannot take parameters; Version is an int, so interpolation is safe here.
                ExecuteNonQuery(connection, transaction, $"PRAGMA user_version = {migration.Version};");
                transaction.Commit();
            }
            catch (SqliteException ex)
            {
                transaction.Rollback();
                throw new InvalidOperationException(
                    $"Schema migration {migration.Version:D4} ({migration.Name}) failed: {ex.Message}", ex);
            }
        }

        private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private void EnsureDatabaseIsNotNewerThanApp(int currentVersion)
        {
            if (currentVersion > LatestVersion)
                throw new InvalidOperationException(
                    $"The database schema is version {currentVersion}, but this build only knows up to version {LatestVersion}. " +
                    "It was probably created by a newer version of DriveCrawl.");
        }

        private static void EnsureVersionsAreSequentialFromOne(IReadOnlyList<SchemaMigration> migrations)
        {
            var versions = migrations.Select(m => m.Version).Order().ToList();
            for (int i = 0; i < versions.Count; i++)
            {
                if (versions[i] != i + 1)
                    throw new ArgumentException(
                        $"Migration versions must be unique and sequential starting at 1; found [{string.Join(", ", versions)}].",
                        nameof(migrations));
            }
        }
    }
}
