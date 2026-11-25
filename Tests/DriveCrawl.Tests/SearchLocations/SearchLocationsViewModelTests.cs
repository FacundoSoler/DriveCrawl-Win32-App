using System.Windows;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.ViewModels;
using NSubstitute;

namespace DriveCrawl.Tests.SearchLocations;

public class SearchLocationsViewModelTests
{
    private readonly ISearchLocationStore _store = Substitute.For<ISearchLocationStore>();
    private readonly IUserDialogs _dialogs = Substitute.For<IUserDialogs>();
    private readonly IFolderPicker _picker = Substitute.For<IFolderPicker>();
    private readonly IPathProbe _probe = Substitute.For<IPathProbe>();

    public SearchLocationsViewModelTests()
    {
        _probe.DirectoryExists(Arg.Any<string>()).Returns(true);
        _store.GetAll().Returns(Array.Empty<WatchedFolders>());
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new SearchLocationsViewModel(null!, _dialogs, _picker, _probe));
        Should.Throw<ArgumentNullException>(() => new SearchLocationsViewModel(_store, null!, _picker, _probe));
        Should.Throw<ArgumentNullException>(() => new SearchLocationsViewModel(_store, _dialogs, null!, _probe));
        Should.Throw<ArgumentNullException>(() => new SearchLocationsViewModel(_store, _dialogs, _picker, null!));
    }

    [Fact]
    public void Load_NoLocations_ShowsEmptyState()
    {
        var vm = CreateLoaded();

        vm.IsEmpty.ShouldBeTrue();
        vm.HasLocations.ShouldBeFalse();
        vm.ShowEmptyState.ShouldBeTrue();
        vm.HasChanges.ShouldBeFalse();
        vm.CanSave.ShouldBeFalse();
    }

    [Fact]
    public void Load_ShowsEachLocationWithItsStatus()
    {
        _store.GetAll().Returns(new[]
        {
            Saved(1, @"C:\Videos", 1),
            Saved(2, @"E:\Backup", 2, external: true),
            Saved(3, @"J:\Archive", 4, external: true),
            Saved(4, @"D:\Gone", 3),
        });
        _probe.DirectoryExists(@"J:\Archive").Returns(false);
        _probe.DirectoryExists(@"D:\Gone").Returns(false);

        var vm = CreateLoaded();

        vm.Locations.Select(l => (l.Path, l.PriorityLabel, l.StatusText)).ShouldBe(new[]
        {
            (@"C:\Videos", "P1", "Internal"),
            (@"E:\Backup", "P2", "External"),
            (@"J:\Archive", "P4", "Offline · drive not connected"),
            (@"D:\Gone", "P3", "Offline · folder not found"),
        });
        vm.ShowEmptyState.ShouldBeFalse();
    }

    [Fact]
    public void Load_FlagsOverlapsThatAlreadyExistInTheDatabase()
    {
        _store.GetAll().Returns(new[] { Saved(1, @"D:\Media", 1), Saved(2, @"D:\Media\Movies", 2) });

        var vm = CreateLoaded();

        vm.Locations[0].OverlapWarning.ShouldBe(@"This folder contains D:\Media\Movies, which is already a search location.");
        vm.Locations[1].OverlapWarning.ShouldBe(@"This folder is inside D:\Media, which is already a search location.");
    }

    [Fact]
    public void BeginAdd_PickerCancelled_DoesNothing()
    {
        _picker.PickFolder(Arg.Any<string>(), Arg.Any<string?>()).Returns((string?)null);
        var vm = CreateLoaded();

        vm.BeginAdd();

        vm.IsEditorOpen.ShouldBeFalse();
    }

    [Fact]
    public void BeginAdd_OpensEditorWithPickedFolder_PriorityOne_AndExternalFromDriveType()
    {
        _picker.PickFolder(Arg.Any<string>(), Arg.Any<string?>()).Returns(@"F:\Camera");
        _probe.IsRemovableDrive(@"F:\Camera").Returns(true);
        var vm = CreateLoaded();

        vm.BeginAdd();

        vm.IsEditorOpen.ShouldBeTrue();
        vm.ShowEmptyState.ShouldBeFalse();
        vm.EditorTitle.ShouldBe("Add search location");
        vm.EditorPath.ShouldBe(@"F:\Camera");
        vm.EditorPriority.ShouldBe(1);
        vm.PriorityOptions.Single(o => o.IsSelected).Value.ShouldBe(1);
        vm.EditorIsExternal.ShouldBeTrue();
    }

    [Fact]
    public void ApplyEdit_ValidNewLocation_IsAddedNormalised_AndEnablesSave()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media\");

        vm.ApplyEdit().ShouldBeTrue();

        var added = vm.Locations.ShouldHaveSingleItem();
        added.Path.ShouldBe(@"D:\Media");
        added.IsNew.ShouldBeTrue();
        vm.IsEditorOpen.ShouldBeFalse();
        vm.HasChanges.ShouldBeTrue();
        vm.CanSave.ShouldBeTrue();
    }

    [Fact]
    public void ApplyEdit_UsesThePriorityChosenInThePicker()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media");

        vm.PriorityOptions.Single(o => o.Value == 3).IsSelected = true;
        vm.ApplyEdit();

        vm.Locations.Single().Priority.ShouldBe(3);
    }

    [Theory]
    [InlineData(@"D:\Media", "This folder is already a search location.")]
    [InlineData(@"d:\media\", "This folder is already a search location.")]
    [InlineData(@"D:\Media\Movies", @"This folder is inside D:\Media, which is already a search location.")]
    [InlineData(@"D:\", @"This folder contains D:\Media, which is already a search location.")]
    public void ApplyEdit_DuplicateOrOverlappingFolder_IsRejectedWithAMessage(string path, string expectedError)
    {
        _store.GetAll().Returns(new[] { Saved(1, @"D:\Media", 1) });
        var vm = CreateLoaded();
        OpenAddEditor(vm, path);

        vm.ApplyEdit().ShouldBeFalse();

        vm.EditorError.ShouldBe(expectedError);
        vm.HasEditorError.ShouldBeTrue();
        vm.IsEditorOpen.ShouldBeTrue();
        vm.Locations.Count.ShouldBe(1);
        vm.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void ApplyEdit_MissingInternalFolder_IsRejected_ButAnUnpluggedExternalDriveIsAllowed()
    {
        _probe.DirectoryExists(@"J:\Archive").Returns(false);
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"J:\Archive");

        vm.ApplyEdit().ShouldBeFalse();
        vm.EditorError!.ShouldStartWith("This folder doesn't exist.");

        vm.EditorIsExternal = true;
        vm.EditorError.ShouldBeNull();
        vm.ApplyEdit().ShouldBeTrue();
        vm.Locations.Single().StatusText.ShouldBe("Offline · drive not connected");
    }

    [Fact]
    public void ApplyEdit_RelativePath_IsRejected()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, "Videos");

        vm.ApplyEdit().ShouldBeFalse();
        vm.EditorError.ShouldBe(@"Use a full path, such as D:\Videos.");
    }

    [Fact]
    public void TypingInThePath_ClearsThePreviousError()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, "Videos");
        vm.ApplyEdit();

        vm.EditorPath = @"D:\Videos";

        vm.EditorError.ShouldBeNull();
    }

    [Fact]
    public void BeginEdit_PrefillsTheEditor_AndApplyUpdatesTheRow()
    {
        _store.GetAll().Returns(new[] { Saved(5, @"D:\Media", 2) });
        var vm = CreateLoaded();
        var item = vm.Locations.Single();

        vm.BeginEdit(item);
        vm.EditorTitle.ShouldBe("Edit search location");
        vm.EditorPath.ShouldBe(@"D:\Media");
        vm.EditorPriority.ShouldBe(2);

        vm.EditorPriority = 4;
        vm.EditorIsExternal = true;
        vm.ApplyEdit().ShouldBeTrue();

        item.Priority.ShouldBe(4);
        item.IsExternal.ShouldBeTrue();
        item.IsModified.ShouldBeTrue();
        vm.HasChanges.ShouldBeTrue();
    }

    [Fact]
    public void BeginEdit_DoesNotConflictWithItself()
    {
        _store.GetAll().Returns(new[] { Saved(5, @"D:\Media", 2) });
        var vm = CreateLoaded();

        vm.BeginEdit(vm.Locations.Single());

        vm.ApplyEdit().ShouldBeTrue();
        vm.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void BeginEdit_OutOfRangePriority_DefaultsTheEditorToOne()
    {
        _store.GetAll().Returns(new[] { Saved(5, @"D:\Media", 9) });
        var vm = CreateLoaded();

        vm.BeginEdit(vm.Locations.Single());

        vm.EditorPriority.ShouldBe(1);
    }

    [Fact]
    public void BrowseForEditorPath_ReplacesThePathWhenAFolderIsPicked()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Old");
        _picker.PickFolder("Choose a folder", @"D:\Old").Returns(@"D:\New");

        vm.BrowseForEditorPath();

        vm.EditorPath.ShouldBe(@"D:\New");
    }

    [Fact]
    public void CancelEdit_ClosesTheEditorWithoutChanges()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media");

        vm.CancelEdit();

        vm.IsEditorOpen.ShouldBeFalse();
        vm.Locations.ShouldBeEmpty();
    }

    [Fact]
    public void CanSave_IsFalseWhileTheEditorIsOpen()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media");
        vm.ApplyEdit();
        OpenAddEditor(vm, @"E:\Other");

        vm.HasChanges.ShouldBeTrue();
        vm.CanSave.ShouldBeFalse();
    }

    [Fact]
    public void RequestRemove_SavedLocation_AsksFirst_AndDeclineKeepsIt()
    {
        _store.GetAll().Returns(new[] { Saved(1, @"D:\Media", 1) });
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MessageBoxImage>()).Returns(false);
        var vm = CreateLoaded();

        vm.RequestRemove(vm.Locations.Single());

        _dialogs.Received(1).Confirm(
            "Remove D:\\Media from search locations?\n\nFiles on disk are not touched.",
            "Remove Search Location",
            MessageBoxImage.Question);
        vm.Locations.Single().IsMarkedForRemoval.ShouldBeFalse();
    }

    [Fact]
    public void RequestRemove_Confirmed_MarksForRemoval_AndUndoRestores()
    {
        _store.GetAll().Returns(new[] { Saved(1, @"D:\Media", 1) });
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MessageBoxImage>()).Returns(true);
        var vm = CreateLoaded();
        var item = vm.Locations.Single();

        vm.RequestRemove(item);
        item.IsMarkedForRemoval.ShouldBeTrue();
        vm.IsEmpty.ShouldBeTrue();
        vm.HasLocations.ShouldBeTrue();
        vm.HasChanges.ShouldBeTrue();

        vm.UndoRemove(item);
        item.IsMarkedForRemoval.ShouldBeFalse();
        vm.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void RequestRemove_UnsavedLocation_DisappearsWithoutAsking()
    {
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media");
        vm.ApplyEdit();

        vm.RequestRemove(vm.Locations.Single());

        vm.Locations.ShouldBeEmpty();
        vm.HasChanges.ShouldBeFalse();
        _dialogs.DidNotReceiveWithAnyArgs().Confirm(default!, default!, default);
    }

    [Fact]
    public void RemovingAParent_LetsYouAddAFolderInsideIt()
    {
        _store.GetAll().Returns(new[] { Saved(1, @"D:\Media", 1) });
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MessageBoxImage>()).Returns(true);
        var vm = CreateLoaded();
        vm.RequestRemove(vm.Locations.Single());

        OpenAddEditor(vm, @"D:\Media\Movies");

        vm.ApplyEdit().ShouldBeTrue();
    }

    [Fact]
    public void UndoingARemovalThatNowOverlaps_BlocksSaving()
    {
        _store.GetAll().Returns(new[] { Saved(1, @"D:\Media", 1) });
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MessageBoxImage>()).Returns(true);
        var vm = CreateLoaded();
        var parent = vm.Locations.Single();
        vm.RequestRemove(parent);
        OpenAddEditor(vm, @"D:\Media\Movies");
        vm.ApplyEdit();

        vm.UndoRemove(parent);

        vm.Locations.Single(l => l.IsNew).HasOverlapWarning.ShouldBeTrue();
        vm.CanSave.ShouldBeFalse();
    }

    [Fact]
    public void Save_SendsAddedUpdatedAndRemovedLocationsInOneBatch()
    {
        _store.GetAll().Returns(new[] { Saved(1, @"C:\Keep", 1), Saved(2, @"D:\Edit", 1), Saved(3, @"E:\Drop", 1) });
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MessageBoxImage>()).Returns(true);
        var vm = CreateLoaded();

        vm.BeginEdit(vm.Locations.Single(l => l.Id == 2));
        vm.EditorPriority = 3;
        vm.ApplyEdit();
        vm.RequestRemove(vm.Locations.Single(l => l.Id == 3));
        OpenAddEditor(vm, @"F:\New");
        vm.ApplyEdit();

        vm.Save().ShouldBeTrue();

        _store.Received(1).SaveChanges(Arg.Is<SearchLocationChanges>(c =>
            c.Added.Count == 1 && c.Added[0].Path == @"F:\New" && c.Added[0].Id == 0 &&
            c.Updated.Count == 1 && c.Updated[0].Id == 2 && c.Updated[0].Priority == 3 &&
            c.RemovedIds.Count == 1 && c.RemovedIds[0] == 3));
    }

    [Fact]
    public void Save_WithoutChanges_DoesNothing()
    {
        var vm = CreateLoaded();

        vm.Save().ShouldBeFalse();

        _store.DidNotReceiveWithAnyArgs().SaveChanges(default!);
    }

    [Fact]
    public void Save_StoreFailure_ShowsAnErrorAndStaysOpen()
    {
        _store.When(s => s.SaveChanges(Arg.Any<SearchLocationChanges>()))
            .Do(_ => throw new Microsoft.Data.Sqlite.SqliteException("disk I/O error", 10));
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media");
        vm.ApplyEdit();

        vm.Save().ShouldBeFalse();

        _dialogs.Received(1).Notify(Arg.Is<string>(m => m.Contains("disk I/O error")), "Search Locations", MessageBoxImage.Error);
    }

    [Fact]
    public void ConfirmDiscardChanges_NoChanges_ClosesWithoutAsking()
    {
        var vm = CreateLoaded();

        vm.ConfirmDiscardChanges().ShouldBeTrue();
        _dialogs.DidNotReceiveWithAnyArgs().Confirm(default!, default!, default);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfirmDiscardChanges_WithChanges_FollowsTheUsersAnswer(bool answer)
    {
        _dialogs.Confirm(Arg.Any<string>(), "Discard Changes", MessageBoxImage.Warning).Returns(answer);
        var vm = CreateLoaded();
        OpenAddEditor(vm, @"D:\Media");
        vm.ApplyEdit();

        vm.ConfirmDiscardChanges().ShouldBe(answer);
    }

    private SearchLocationsViewModel CreateLoaded()
    {
        var vm = new SearchLocationsViewModel(_store, _dialogs, _picker, _probe);
        vm.Load();
        return vm;
    }

    private void OpenAddEditor(SearchLocationsViewModel vm, string path)
    {
        _picker.PickFolder("Add a search location", null).Returns(path);
        vm.BeginAdd();
    }

    private static WatchedFolders Saved(int id, string path, int priority, bool external = false) =>
        new() { Id = id, Path = path, Priority = priority, IsExternal = external ? 1 : 0 };
}
