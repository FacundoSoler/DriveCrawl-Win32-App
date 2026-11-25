using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.DataLocations;

/// <summary>
/// Fake "exe folder" and "%LocalAppData%" inside a temp directory, so location tests never touch the real profile.
/// </summary>
public sealed class DataLocationTestContext : IDisposable
{
    private readonly TempDirectory _temp = new();

    public string AppBaseDirectory { get; }
    public string LocalAppDataDirectory { get; }
    public DataLocationPaths Paths { get; }
    public AppSettingsStore SettingsStore { get; }
    public HashSet<DataLocation> ReadOnlyLocations { get; } = new();

    public DataLocationTestContext()
    {
        AppBaseDirectory = _temp.CreateSubdirectory("app");
        LocalAppDataDirectory = _temp.CreateSubdirectory("localappdata");
        Paths = new DataLocationPaths(AppBaseDirectory, LocalAppDataDirectory);
        SettingsStore = new AppSettingsStore(Paths.SettingsFilePath);
    }

    public DataLocationService CreateService() => new(Paths, SettingsStore, CanWriteTo);

    public string CreateLibraryAt(DataLocation location, string content = "library")
    {
        string path = Paths.GetDatabasePath(location);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => _temp.Dispose();

    private bool CanWriteTo(string directory) =>
        !ReadOnlyLocations.Any(l => Path.GetDirectoryName(Paths.GetDatabasePath(l)) == directory);
}
