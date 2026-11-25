using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.DataLocations;

public sealed class DataLocationPathsAndSettingsTests : IDisposable
{
    private readonly DataLocationTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public void AppFolder_IsADatabaseSubfolderNextToTheExe()
    {
        _context.Paths.GetDatabasePath(DataLocation.AppFolder)
            .ShouldBe(Path.Combine(_context.AppBaseDirectory, "Database", "MediaLibrary.db"));
    }

    [Fact]
    public void UserProfile_IsADriveCrawlFolderUnderLocalAppData()
    {
        _context.Paths.GetDatabasePath(DataLocation.UserProfile)
            .ShouldBe(Path.Combine(_context.LocalAppDataDirectory, "DriveCrawl", "MediaLibrary.db"));
    }

    [Fact]
    public void SettingsFile_AlwaysLivesUnderLocalAppData()
    {
        _context.Paths.SettingsFilePath
            .ShouldBe(Path.Combine(_context.LocalAppDataDirectory, "DriveCrawl", "settings.json"));
    }

    [Fact]
    public void UnknownLocation_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _context.Paths.GetDatabasePath((DataLocation)42));
    }

    [Fact]
    public void SettingsStore_MissingFile_LoadsNull()
    {
        _context.SettingsStore.Load().ShouldBeNull();
    }

    [Theory]
    [InlineData(DataLocation.AppFolder)]
    [InlineData(DataLocation.UserProfile)]
    public void SettingsStore_RoundTripsTheChoice(DataLocation location)
    {
        _context.SettingsStore.Save(new AppSettings(location));

        _context.SettingsStore.Load().ShouldBe(new AppSettings(location));
    }

    [Fact]
    public void SettingsStore_WritesReadableEnumNames()
    {
        _context.SettingsStore.Save(new AppSettings(DataLocation.UserProfile));

        File.ReadAllText(_context.Paths.SettingsFilePath).ShouldContain("\"UserProfile\"");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"DataLocation\": \"Moon\" }")]
    [InlineData("{ \"DataLocation\": 7 }")]
    [InlineData("null")]
    public void SettingsStore_CorruptOrUnknownContent_LoadsNull(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_context.Paths.SettingsFilePath)!);
        File.WriteAllText(_context.Paths.SettingsFilePath, content);

        _context.SettingsStore.Load().ShouldBeNull();
    }

    [Fact]
    public void WriteProbe_WritableDirectory_ReturnsTrue_AndLeavesNoFilesBehind()
    {
        using var temp = new TempDirectory();

        DirectoryWriteProbe.CanWriteTo(temp.Path).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(temp.Path).ShouldBeEmpty();
    }

    [Fact]
    public void WriteProbe_MissingDirectory_ProbesItsParent_WithoutCreatingIt()
    {
        using var temp = new TempDirectory();
        string missing = Path.Combine(temp.Path, "not", "yet");

        DirectoryWriteProbe.CanWriteTo(missing).ShouldBeTrue();
        Directory.Exists(missing).ShouldBeFalse();
    }
}
