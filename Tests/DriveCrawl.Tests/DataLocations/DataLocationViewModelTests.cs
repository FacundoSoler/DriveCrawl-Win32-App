using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.ViewModels;

namespace DriveCrawl.Tests.DataLocations;

public sealed class DataLocationViewModelTests : IDisposable
{
    private readonly DataLocationTestContext _context = new();

    public void Dispose() => _context.Dispose();

    [Fact]
    public void FirstRun_OffersBothLocations_WithTheirFullPaths()
    {
        var vm = new DataLocationViewModel(_context.CreateService(), DataLocationPromptMode.FirstRun);

        vm.Options.Select(o => (o.Location, o.DatabasePath)).ShouldBe(new[]
        {
            (DataLocation.AppFolder, _context.Paths.GetDatabasePath(DataLocation.AppFolder)),
            (DataLocation.UserProfile, _context.Paths.GetDatabasePath(DataLocation.UserProfile)),
        });
    }

    [Fact]
    public void FirstRun_PreselectsTheAppFolder_MarkedAsDefault()
    {
        var vm = new DataLocationViewModel(_context.CreateService(), DataLocationPromptMode.FirstRun);

        vm.SelectedLocation.ShouldBe(DataLocation.AppFolder);
        vm.Options[0].Badge.ShouldBe("Default");
        vm.Options[1].HasBadge.ShouldBeFalse();
        vm.CanConfirm.ShouldBeTrue();
    }

    [Fact]
    public void FirstRun_UsesFirstRunWordingAndNoCopyOption()
    {
        var vm = new DataLocationViewModel(_context.CreateService(), DataLocationPromptMode.FirstRun);

        vm.Heading.ShouldBe("Where should DriveCrawl keep your library?");
        vm.ConfirmText.ShouldBe("Continue");
        vm.CancelText.ShouldBe("Quit");
        vm.ShowCopyOption.ShouldBeFalse();
    }

    [Fact]
    public void ReadOnlyAppFolder_IsDisabledWithAReason_AndUserProfileIsPreselected()
    {
        _context.ReadOnlyLocations.Add(DataLocation.AppFolder);

        var vm = new DataLocationViewModel(_context.CreateService(), DataLocationPromptMode.FirstRun);

        var appFolder = vm.Options.Single(o => o.Location == DataLocation.AppFolder);
        appFolder.IsAvailable.ShouldBeFalse();
        appFolder.IsUnavailable.ShouldBeTrue();
        appFolder.UnavailableReason.ShouldNotBeEmpty();
        vm.SelectedLocation.ShouldBe(DataLocation.UserProfile);
    }

    [Fact]
    public void NoWritableLocation_CannotConfirm()
    {
        _context.ReadOnlyLocations.Add(DataLocation.AppFolder);
        _context.ReadOnlyLocations.Add(DataLocation.UserProfile);

        var vm = new DataLocationViewModel(_context.CreateService(), DataLocationPromptMode.FirstRun);

        vm.SelectedLocation.ShouldBeNull();
        vm.CanConfirm.ShouldBeFalse();
    }

    [Fact]
    public void SelectingAnotherOption_UpdatesSelectionAndRaisesChangeNotifications()
    {
        var vm = new DataLocationViewModel(_context.CreateService(), DataLocationPromptMode.FirstRun);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Options[0].IsSelected = false;
        vm.Options[1].IsSelected = true;

        vm.SelectedLocation.ShouldBe(DataLocation.UserProfile);
        raised.ShouldContain(nameof(DataLocationViewModel.CanConfirm));
        raised.ShouldContain(nameof(DataLocationViewModel.SelectedLocation));
    }

    [Fact]
    public void Change_PreselectsCurrentLocation_MarksItCurrent_AndCannotConfirmUntilAnotherIsPicked()
    {
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.UserProfile);

        var vm = new DataLocationViewModel(service, DataLocationPromptMode.Change);

        vm.SelectedLocation.ShouldBe(DataLocation.UserProfile);
        vm.Options.Single(o => o.Location == DataLocation.UserProfile).Badge.ShouldBe("Current");
        vm.CanConfirm.ShouldBeFalse();

        vm.Options.Single(o => o.Location == DataLocation.UserProfile).IsSelected = false;
        vm.Options.Single(o => o.Location == DataLocation.AppFolder).IsSelected = true;

        vm.CanConfirm.ShouldBeTrue();
        vm.ConfirmText.ShouldBe("Change and restart");
        vm.CancelText.ShouldBe("Cancel");
    }

    [Fact]
    public void Change_ShowsCopyOption_OnlyWhenTheCurrentLocationHasALibrary()
    {
        var service = _context.CreateService();
        service.SaveChoice(DataLocation.AppFolder);

        new DataLocationViewModel(service, DataLocationPromptMode.Change).ShowCopyOption.ShouldBeFalse();

        _context.CreateLibraryAt(DataLocation.AppFolder);
        var vm = new DataLocationViewModel(service, DataLocationPromptMode.Change);

        vm.ShowCopyOption.ShouldBeTrue();
        vm.CopyCurrentLibrary.ShouldBeTrue();
    }
}
