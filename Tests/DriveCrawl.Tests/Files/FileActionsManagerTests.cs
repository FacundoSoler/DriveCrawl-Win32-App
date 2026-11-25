using System.Collections.ObjectModel;
using System.Windows;
using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;
using DriveCrawl_Win32_App.Services;
using NSubstitute;

namespace DriveCrawl.Tests.Files;

public sealed class FileActionsManagerTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly IUserDialogs _dialogs = Substitute.For<IUserDialogs>();
    private readonly ObservableCollection<FileViewModel> _results = new();
    private readonly FileActionsManager _manager;
    private int _operationCompletedCount;

    public FileActionsManagerTests()
    {
        _manager = new FileActionsManager(_results, _dialogs);
        _manager.OperationCompleted += () => _operationCompletedCount++;
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new FileActionsManager(null!, _dialogs));
        Should.Throw<ArgumentNullException>(() => new FileActionsManager(_results, null!));
    }

    [Fact]
    public async Task Delete_EmptySelection_DoesNothing()
    {
        await _manager.DeleteSelectedFilesAsync(Array.Empty<FileViewModel>());

        _dialogs.ReceivedCalls().ShouldBeEmpty();
        _operationCompletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Delete_AsksForConfirmationWithFileCount()
    {
        var files = new[] { AddExistingFile("a.txt"), AddExistingFile("b.txt") };

        await _manager.DeleteSelectedFilesAsync(files);

        _dialogs.Received(1).Confirm("Are you sure you want to delete 2 file(s)?", "Confirm Delete", MessageBoxImage.Warning);
    }

    [Fact]
    public async Task Delete_Declined_KeepsFilesAndRows()
    {
        var file = AddExistingFile("a.txt");
        ConfirmReturns(false);

        await _manager.DeleteSelectedFilesAsync(new[] { file });

        File.Exists(file.FullPath).ShouldBeTrue();
        _results.ShouldContain(file);
        _dialogs.DidNotReceiveWithAnyArgs().Notify(default!, default!, default);
        _operationCompletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesFileAndRow_AndReportsSuccess()
    {
        var file = AddExistingFile("a.txt");
        ConfirmReturns(true);

        await _manager.DeleteSelectedFilesAsync(new[] { file });

        File.Exists(file.FullPath).ShouldBeFalse();
        _results.ShouldBeEmpty();
        _dialogs.Received(1).Notify("Successfully deleted 1 file(s).", "Delete Complete", MessageBoxImage.Information);
        _operationCompletedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Delete_FileAlreadyGone_RemovesRowButDoesNotCountIt()
    {
        var ghost = TestData.ViewModel("ghost.txt", fullPath: Path.Combine(_temp.Path, "ghost.txt"));
        _results.Add(ghost);
        ConfirmReturns(true);

        await _manager.DeleteSelectedFilesAsync(new[] { ghost });

        _results.ShouldBeEmpty();
        _dialogs.Received(1).Notify("Successfully deleted 0 file(s).", "Delete Complete", MessageBoxImage.Information);
    }

    [Fact]
    public async Task Delete_LockedFile_KeepsRow_AndReportsFailure()
    {
        var locked = AddExistingFile("locked.txt");
        ConfirmReturns(true);
        using (new FileStream(locked.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await _manager.DeleteSelectedFilesAsync(new[] { locked });
        }

        _results.ShouldContain(locked);
        _dialogs.Received(1).Notify(
            Arg.Is<string>(m => m.StartsWith("Successfully deleted 0 file(s).\n\nFailed:\nlocked.txt: ")),
            "Delete Summary",
            MessageBoxImage.Warning);
    }

    [Fact]
    public async Task Delete_MoreThanThreeFailures_ListsThreeAndCountsTheRest()
    {
        var files = Enumerable.Range(1, 4).Select(i => AddExistingFile($"locked{i}.txt")).ToList();
        ConfirmReturns(true);
        var locks = files.Select(f => new FileStream(f.FullPath, FileMode.Open, FileAccess.Read, FileShare.None)).ToList();
        try
        {
            await _manager.DeleteSelectedFilesAsync(files);
        }
        finally
        {
            locks.ForEach(l => l.Dispose());
        }

        _dialogs.Received(1).Notify(
            Arg.Is<string>(m => m.EndsWith("\n...and 1 more errors.") && !m.Contains("locked4.txt")),
            "Delete Summary",
            MessageBoxImage.Warning);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Move_BlankDestination_DoesNothing(string destination)
    {
        await _manager.MoveSelectedFilesAsync(new[] { AddExistingFile("a.txt") }, destination);

        _dialogs.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Move_EmptySelection_DoesNothing()
    {
        await _manager.MoveSelectedFilesAsync(Array.Empty<FileViewModel>(), _temp.CreateSubdirectory("dest"));

        _dialogs.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Move_AsksForConfirmationWithDestination()
    {
        string destination = _temp.CreateSubdirectory("dest");

        await _manager.MoveSelectedFilesAsync(new[] { AddExistingFile("a.txt") }, destination);

        _dialogs.Received(1).Confirm($"Are you sure you want to move 1 file(s) to:\n{destination}?", "Confirm Move", MessageBoxImage.Question);
    }

    [Fact]
    public async Task Move_Declined_LeavesFileInPlace()
    {
        var file = AddExistingFile("a.txt");
        string originalPath = file.FullPath;
        ConfirmReturns(false);

        await _manager.MoveSelectedFilesAsync(new[] { file }, _temp.CreateSubdirectory("dest"));

        File.Exists(originalPath).ShouldBeTrue();
        _operationCompletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Move_Confirmed_MovesFile_UpdatesPathAndDeselects()
    {
        var file = AddExistingFile("a.txt");
        string originalPath = file.FullPath;
        file.IsSelected = true;
        string destination = _temp.CreateSubdirectory("dest");
        ConfirmReturns(true);

        await _manager.MoveSelectedFilesAsync(new[] { file }, destination);

        string expectedPath = Path.Combine(destination, "a.txt");
        File.Exists(originalPath).ShouldBeFalse();
        File.Exists(expectedPath).ShouldBeTrue();
        file.FullPath.ShouldBe(expectedPath);
        file.IsSelected.ShouldBeFalse();
        _results.ShouldContain(file);
        _dialogs.Received(1).Notify("Successfully moved 1 file(s).", "Move Complete", MessageBoxImage.Information);
        _operationCompletedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Move_DestinationAlreadyHasFile_ReportsFailureAndNeverOverwrites()
    {
        var file = AddExistingFile("a.txt");
        string originalPath = file.FullPath;
        string destination = _temp.CreateSubdirectory("dest");
        File.WriteAllText(Path.Combine(destination, "a.txt"), "existing");
        ConfirmReturns(true);

        await _manager.MoveSelectedFilesAsync(new[] { file }, destination);

        File.Exists(originalPath).ShouldBeTrue();
        File.ReadAllText(Path.Combine(destination, "a.txt")).ShouldBe("existing");
        file.FullPath.ShouldBe(originalPath);
        _dialogs.Received(1).Notify(
            "Successfully moved 0 file(s).\n\nFailed:\na.txt: File already exists at destination.",
            "Move Summary",
            MessageBoxImage.Warning);
    }

    [Fact]
    public async Task Move_MissingSource_IsSkippedSilently()
    {
        var ghost = TestData.ViewModel("ghost.txt", fullPath: Path.Combine(_temp.Path, "ghost.txt"));
        ConfirmReturns(true);

        await _manager.MoveSelectedFilesAsync(new[] { ghost }, _temp.CreateSubdirectory("dest"));

        _dialogs.Received(1).Notify("Successfully moved 0 file(s).", "Move Complete", MessageBoxImage.Information);
    }

    private FileViewModel AddExistingFile(string name)
    {
        string path = _temp.CreateFile(name, 8);
        var vm = TestData.ViewModel(name, fileSize: 8, fullPath: path);
        _results.Add(vm);
        return vm;
    }

    private void ConfirmReturns(bool answer)
    {
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<MessageBoxImage>()).Returns(answer);
    }
}
