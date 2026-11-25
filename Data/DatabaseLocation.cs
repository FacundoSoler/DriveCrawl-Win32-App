using System.IO;
using Microsoft.Data.Sqlite;

namespace DriveCrawl_Win32_App.Data
{
    /// <summary>
    /// How connections to MediaLibrary.db are opened. Where the file lives is decided by DataLocationService.
    /// </summary>
    public static class DatabaseLocation
    {
        public const string DatabaseFileName = "MediaLibrary.db";

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
