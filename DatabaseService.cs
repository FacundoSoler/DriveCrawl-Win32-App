using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Dapper;
using DriveCrawl_Win32_App.Models;
using System.IO;

namespace DriveCrawl_Win32_App.Data
{
    public class DatabaseService
    {
        // La base de datos vive en la carpeta de ejecución de la app
        private readonly string _connectionString;

        public DatabaseService()
            : this(BuildDefaultConnectionString())
        {
        }

        public DatabaseService(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

            _connectionString = connectionString;
        }

        public List<WatchedFolders> GetFolders()
        {
            using var connection = new SqliteConnection(_connectionString);
            // Dapper mapea automáticamente el resultado a la clase WatchedFolder
            return connection.Query<WatchedFolders>(
                "SELECT * FROM WatchedFolders ORDER BY Priority ASC"
            ).ToList();
        }

        private static string BuildDefaultConnectionString()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "Database", "MediaLibrary.db");
            return $"Data Source={dbPath}";
        }
    }
}