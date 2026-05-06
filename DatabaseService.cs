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

        public DatabaseService() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dbPath = Path.Combine(baseDir, "Database", "MediaLibrary.db");
            _connectionString = $"Data Source={dbPath}";
        }

        public List<WatchedFolders> GetFolders()
        {
            using var connection = new SqliteConnection(_connectionString);
            // Dapper mapea automáticamente el resultado a la clase WatchedFolder
            return connection.Query<WatchedFolders>(
                "SELECT * FROM WatchedFolders ORDER BY Priority ASC"
            ).ToList();
        }
    }
}