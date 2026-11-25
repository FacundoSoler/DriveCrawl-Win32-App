namespace DriveCrawl_Win32_App.Data
{
    /// <summary>
    /// Creates the database file if needed and upgrades it to the latest embedded schema.
    /// Runs once at startup, before any window queries the database.
    /// </summary>
    public static class DatabaseInitializer
    {
        public static void Initialize(string databasePath)
        {
            DatabaseLocation.EnsureDirectoryExists(databasePath);

            var migrator = new SchemaMigrator(
                DatabaseLocation.BuildConnectionString(databasePath),
                EmbeddedSchemaMigrations.Load());

            migrator.MigrateToLatest();
        }
    }
}
