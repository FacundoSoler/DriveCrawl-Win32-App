using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.DataLocations;

public sealed class DataLocationServiceTests : IDisposable
{
    private readonly DataLocationTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new DataLocationService(null!, _context.SettingsStore, _ => true));
        Should.Throw<ArgumentNullException>(() => new DataLocationService(_context.Paths, null!, _ => true));
        Should.Throw<ArgumentNullException>(() => new DataLocationService(_context.Paths, _context.SettingsStore, null!));
    }

    [Fact]
    public void FirstRun_WithNoSettingsAndNoLibrary_RequiresTheUserToChoose()
    {
        var service = _context.CreateService();

        service.TryResolveWithoutAsking().ShouldBeNull();
        service.CurrentLocation.ShouldBeNull();
        service.CurrentDatabasePath.ShouldBeNull();
    }

    [Theory]
    [InlineData(DataLocation.AppFolder)]
    [InlineData(DataLocation.UserProfile)]
    public void SavedChoice_IsUsedWithoutAsking(DataLocation saved)
    {
        _context.SettingsStore.Save(new AppSettings(saved));
        var service = _context.CreateService();

        service.TryResolveWithoutAsking().ShouldBe(saved);
        service.CurrentDatabasePath.ShouldBe(_context.Paths.GetDatabasePath(saved));
    }

    [Fact]
    public void ExistingLibraryNextToExe_IsAdoptedSilently_AndRemembered()
    {
        _context.CreateLibraryAt(DataLocation.AppFolder);
        var service = _context.CreateService();

        service.TryResolveWithoutAsking().ShouldBe(DataLocation.AppFolder);
        _context.SettingsStore.Load().ShouldBe(new AppSettings(DataLocation.AppFolder));
    }

    [Fact]
    public void SavedChoice_WinsOverALibraryNextToExe()
    {
        _context.CreateLibraryAt(DataLocation.AppFolder);
        _context.SettingsStore.Save(new AppSettings(DataLocation.UserProfile));

        _context.CreateService().TryResolveWithoutAsking().ShouldBe(DataLocation.UserProfile);
    }

    [Fact]
    public void SaveChoice_PersistsAndBecomesCurrent()
    {
        var service = _context.CreateService();

        service.SaveChoice(DataLocation.UserProfile);

        service.CurrentLocation.ShouldBe(DataLocation.UserProfile);
        _context.SettingsStore.Load().ShouldBe(new AppSettings(DataLocation.UserProfile));
    }

    [Fact]
    public void IsAvailable_ReflectsWhetherTheFolderIsWritable()
    {
        _context.ReadOnlyLocations.Add(DataLocation.AppFolder);
        var service = _context.CreateService();

        service.IsAvailable(DataLocation.AppFolder).ShouldBeFalse();
        service.IsAvailable(DataLocation.UserProfile).ShouldBeTrue();
    }

    [Fact]
    public void HasLibraryAt_ChecksForTheDatabaseFile()
    {
        var service = _context.CreateService();
        _context.CreateLibraryAt(DataLocation.UserProfile);

        service.HasLibraryAt(DataLocation.UserProfile).ShouldBeTrue();
        service.HasLibraryAt(DataLocation.AppFolder).ShouldBeFalse();
    }

    [Fact]
    public void ChangeLocation_BeforeResolving_Throws()
    {
        Should.Throw<InvalidOperationException>(() => _context.CreateService().ChangeLocation(DataLocation.UserProfile, true));
    }

    [Fact]
    public void ChangeLocation_ToTheSameLocation_IsUnchanged()
    {
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);

        service.ChangeLocation(DataLocation.AppFolder, true).ShouldBe(LocationChangeResult.Unchanged);
    }

    [Fact]
    public void ChangeLocation_WithCopy_CopiesTheLibrary_KeepsTheOriginal_AndSavesTheChoice()
    {
        string original = _context.CreateLibraryAt(DataLocation.AppFolder, "my folders");
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);

        var result = service.ChangeLocation(DataLocation.UserProfile, copyCurrentLibrary: true);

        result.ShouldBe(LocationChangeResult.Copied);
        File.ReadAllText(_context.Paths.GetDatabasePath(DataLocation.UserProfile)).ShouldBe("my folders");
        File.Exists(original).ShouldBeTrue();
        service.CurrentLocation.ShouldBe(DataLocation.UserProfile);
        _context.SettingsStore.Load().ShouldBe(new AppSettings(DataLocation.UserProfile));
    }

    [Fact]
    public void ChangeLocation_WithoutCopy_SwitchesWithoutCreatingAFile()
    {
        _context.CreateLibraryAt(DataLocation.AppFolder);
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);

        var result = service.ChangeLocation(DataLocation.UserProfile, copyCurrentLibrary: false);

        result.ShouldBe(LocationChangeResult.SwitchedWithoutCopy);
        service.HasLibraryAt(DataLocation.UserProfile).ShouldBeFalse();
        service.CurrentLocation.ShouldBe(DataLocation.UserProfile);
    }

    [Fact]
    public void ChangeLocation_WhenCurrentHasNoLibrary_SwitchesWithoutCopy()
    {
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);

        service.ChangeLocation(DataLocation.UserProfile, copyCurrentLibrary: true)
            .ShouldBe(LocationChangeResult.SwitchedWithoutCopy);
    }

    [Fact]
    public void ChangeLocation_NeverOverwritesALibraryAlreadyAtTheTarget()
    {
        _context.CreateLibraryAt(DataLocation.AppFolder, "current");
        string target = _context.CreateLibraryAt(DataLocation.UserProfile, "already there");
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);

        var result = service.ChangeLocation(DataLocation.UserProfile, copyCurrentLibrary: true);

        result.ShouldBe(LocationChangeResult.TargetAlreadyHadLibrary);
        File.ReadAllText(target).ShouldBe("already there");
        service.CurrentLocation.ShouldBe(DataLocation.UserProfile);
    }
}
