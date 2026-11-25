using System.IO;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl_Win32_App.Services
{
    public enum LocationChangeResult
    {
        Unchanged,
        Copied,
        SwitchedWithoutCopy,
        TargetAlreadyHadLibrary
    }

    /// <summary>
    /// Decides where the library database lives: remembers the user's choice, adopts pre-existing libraries,
    /// and switches locations (optionally copying the current library across).
    /// </summary>
    public class DataLocationService
    {
        private readonly DataLocationPaths _paths;
        private readonly AppSettingsStore _settingsStore;
        private readonly Func<string, bool> _canWriteToDirectory;

        public DataLocation? CurrentLocation { get; private set; }

        public string? CurrentDatabasePath => CurrentLocation is { } location ? GetDatabasePath(location) : null;

        public DataLocationService(DataLocationPaths paths, AppSettingsStore settingsStore, Func<string, bool> canWriteToDirectory)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _canWriteToDirectory = canWriteToDirectory ?? throw new ArgumentNullException(nameof(canWriteToDirectory));
        }

        public static DataLocationService ForCurrentUser()
        {
            var paths = DataLocationPaths.ForCurrentUser();
            return new DataLocationService(paths, new AppSettingsStore(paths.SettingsFilePath), DirectoryWriteProbe.CanWriteTo);
        }

        public string GetDatabasePath(DataLocation location) => _paths.GetDatabasePath(location);

        public bool IsAvailable(DataLocation location) =>
            _canWriteToDirectory(Path.GetDirectoryName(GetDatabasePath(location))!);

        public bool HasLibraryAt(DataLocation location) => File.Exists(GetDatabasePath(location));

        /// <summary>
        /// Returns the location to use without asking the user, or null when the user must choose (first run).
        /// </summary>
        public DataLocation? TryResolveWithoutAsking()
        {
            var settings = _settingsStore.Load();
            if (settings is not null)
            {
                CurrentLocation = settings.DataLocation;
                return CurrentLocation;
            }

            // Installs that predate the location choice already keep their library next to the exe; adopt it instead of prompting.
            if (HasLibraryAt(DataLocation.AppFolder))
            {
                SaveChoice(DataLocation.AppFolder);
                return CurrentLocation;
            }

            return null;
        }

        public void SaveChoice(DataLocation location)
        {
            _settingsStore.Save(new AppSettings(location));
            CurrentLocation = location;
        }

        public LocationChangeResult ChangeLocation(DataLocation target, bool copyCurrentLibrary)
        {
            if (CurrentLocation is not { } current)
                throw new InvalidOperationException("The current data location has not been resolved yet.");

            if (current == target)
                return LocationChangeResult.Unchanged;

            var result = CopyLibraryIfRequested(current, target, copyCurrentLibrary);
            SaveChoice(target);
            return result;
        }

        private LocationChangeResult CopyLibraryIfRequested(DataLocation current, DataLocation target, bool copyCurrentLibrary)
        {
            if (!copyCurrentLibrary || !HasLibraryAt(current))
                return LocationChangeResult.SwitchedWithoutCopy;

            // Never overwrite: a library already at the target may hold data the user wants to keep.
            if (HasLibraryAt(target))
                return LocationChangeResult.TargetAlreadyHadLibrary;

            string targetPath = GetDatabasePath(target);
            DatabaseLocation.EnsureDirectoryExists(targetPath);
            File.Copy(GetDatabasePath(current), targetPath);
            return LocationChangeResult.Copied;
        }
    }
}
