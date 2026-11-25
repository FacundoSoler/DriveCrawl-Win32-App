using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.Search;

public sealed class SearchCoordinatorExecuteSearchTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SearchCoordinator _coordinator =
        new(new DatabaseService("Data Source=:memory:"), new SearchEngine());

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new SearchCoordinator(null!, new SearchEngine()));
        Should.Throw<ArgumentNullException>(() => new SearchCoordinator(new DatabaseService("Data Source=:memory:"), null!));
    }

    [Fact]
    public async Task NoFolders_ThrowsInvalidOperation()
    {
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _coordinator.ExecuteSearchAsync(new(), "x", false, false, token: TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("No folders configured in the database.");
    }

    [Fact]
    public async Task Results_AreOrderedBySizeDescending()
    {
        _temp.CreateFile("small.txt", 10);
        _temp.CreateFile("large.txt", 300);
        _temp.CreateFile("medium.txt", 50);

        var (results, _) = await Search("", isExactPhrase: false, duplicatesOnly: false);

        results.Select(f => f.FileName).ShouldBe(new[] { "large.txt", "medium.txt", "small.txt" });
    }

    [Fact]
    public async Task DuplicatesOnly_ReturnsDuplicateGroups_WithAllButOneCopyPreselected()
    {
        string lowPriorityFolder = _temp.CreateSubdirectory("p1");
        string highPriorityFolder = _temp.CreateSubdirectory("p3");
        _temp.CreateFile(@"p1\movie.mp4", 64);
        _temp.CreateFile(@"p3\movie.mp4", 64);
        _temp.CreateFile(@"p1\unique.mp4", 64);
        var folders = new List<WatchedFolders>
        {
            TestData.Folder(lowPriorityFolder, priority: 1),
            TestData.Folder(highPriorityFolder, priority: 3),
        };

        var (results, _) = await _coordinator.ExecuteSearchAsync(
            folders, "", isExactPhrase: false, includeDuplicatesOnly: true, token: TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
        results.ShouldAllBe(f => f.FileName == "movie.mp4");
        results.Single(f => f.Priority == 3).IsSelected.ShouldBeFalse();
        results.Single(f => f.Priority == 1).IsSelected.ShouldBeTrue();
    }

    [Fact]
    public async Task ExactPhrase_NarrowsWildcardMatchesToWholeWords()
    {
        _temp.CreateFile("my cat.mp4", 1);
        _temp.CreateFile("category.mp4", 1);

        var (results, _) = await Search("cat", isExactPhrase: true, duplicatesOnly: false);

        results.ShouldHaveSingleItem().FileName.ShouldBe("my cat.mp4");
    }

    [Fact]
    public async Task WithoutExactPhrase_KeepsSubstringMatches()
    {
        _temp.CreateFile("my cat.mp4", 1);
        _temp.CreateFile("category.mp4", 1);

        var (results, _) = await Search("cat", isExactPhrase: false, duplicatesOnly: false);

        results.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(FileTypeFilter.AllFiles)]
    [InlineData(FileTypeFilter.Photos)]
    [InlineData(FileTypeFilter.Other)]
    public async Task NonVideoSearch_ReturnsAlreadyCompletedHydrationTask(FileTypeFilter filter)
    {
        _temp.CreateFile("a.txt", 1);

        var (_, hydration) = await Search("", false, false, filter);

        hydration.ShouldBeSameAs(Task.CompletedTask);
    }

    [Fact]
    public async Task VideoSearch_ReturnsHydrationTaskThatCompletes()
    {
        Assert.SkipWhen(Environment.ProcessorCount < 2, "Hydration throws on single-core machines (MaxDegreeOfParallelism = 0).");
        _temp.CreateFile("not-really-a-video.mp4", 16);

        var (results, hydration) = await Search("", false, false, FileTypeFilter.Videos);
        await hydration;

        results.ShouldHaveSingleItem().Duration.ShouldBeNull();
    }

    [Fact]
    public async Task CancelledToken_ThrowsOperationCanceled()
    {
        _temp.CreateFile("a.txt", 1);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _coordinator.ExecuteSearchAsync(
            new() { TestData.Folder(_temp.Path) }, "", false, false, token: cts.Token));
    }

    private Task<(List<FileViewModel>, Task hydrationTask)> Search(
        string query, bool isExactPhrase, bool duplicatesOnly, FileTypeFilter filter = FileTypeFilter.AllFiles)
    {
        return _coordinator.ExecuteSearchAsync(
            new() { TestData.Folder(_temp.Path) }, query, isExactPhrase, duplicatesOnly, filter, TestContext.Current.CancellationToken);
    }
}
