using System.IO;
using Microsoft.Data.Sqlite;

namespace DriveCrawl_Win32_App.Data
{
    /// <summary>
    /// Single source of truth for where MediaLibrary.db lives and how connections to it are opened.
    /// </summary>
    public static class DatabaseLocation
    {
        public const string DatabaseFileName = "MediaLibrary.db";

        public static string DefaultDatabasePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", DatabaseFileName);

        public static string BuildConnectionString(string databasePath)
        {
            // Foreign keys are off by default in SQLite on every connection; without this the ON DELETE CASCADE rules never fire.
            return new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true
            }.ToString();
        }

        public static void EnsureDirectoryExists(string databasePath)
        {
            string? directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
