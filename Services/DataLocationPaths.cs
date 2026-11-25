using System.IO;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl_Win32_App.Services
{
    /// <summary>
    /// Maps each <see cref="DataLocation"/> to a concrete file path.
    /// </summary>
    public class DataLocationPaths
    {
        private const string ProductFolderName = "DriveCrawl";
        private const string AppFolderDatabaseDirectory = "Database";
        private const string SettingsFileName = "settings.json";

        private readonly string _appBaseDirectory;
        private readonly string _localAppDataDirectory;

        // The settings file always lives in the user profile because the app folder may be read-only (e.g. Program Files).
        public string SettingsFilePath => Path.Combine(_localAppDataDirectory, ProductFolderName, SettingsFileName);

        public DataLocationPaths(string appBaseDirectory, string localAppDataDirectory)
        {
            _appBaseDirectory = appBaseDirectory;
            _localAppDataDirectory = localAppDataDirectory;
        }

        public static DataLocationPaths ForCurrentUser() => new(
            AppDomain.CurrentDomain.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        public string GetDatabasePath(DataLocation location) => location switch
        {
            DataLocation.AppFolder => Path.Combine(_appBaseDirectory, AppFolderDatabaseDirectory, DatabaseLocation.DatabaseFileName),
            DataLocation.UserProfile => Path.Combine(_localAppDataDirectory, ProductFolderName, DatabaseLocation.DatabaseFileName),
            _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Unknown data location.")
        };
    }
}
