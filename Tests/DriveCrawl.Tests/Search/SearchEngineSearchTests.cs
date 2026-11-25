using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.Search;

public sealed class SearchEngineSearchTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SearchEngine _engine = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FindsMatchingFiles_InNestedSubfolders()
    {
        _temp.CreateFile(@"a\b\c\holiday.mp4");
        _temp.CreateFile(@"other.mp4");

        var results = Search("holiday");

        results.ShouldHaveSingleItem().FileName.ShouldBe("holiday.mp4");
    }

    [Fact]
    public void Matching_IsCaseInsensitive()
    {
        _temp.CreateFile("Holiday.mp4");

        Search("HOLIDAY").ShouldHaveSingleItem();
    }

    [Fact]
    public void Query_AlsoMatchesTheExtension()
    {
        _temp.CreateFile("one.mp4");
        _temp.CreateFile("two.mp4");
        _temp.CreateFile("three.mkv");

        Search("mp4").Count.ShouldBe(2);
    }

    [Fact]
    public void EmptyQuery_ReturnsEveryFile()
    {
        _temp.CreateFile("a.txt");
        _temp.CreateFile(@"sub\b.jpg");

        Search("").Count.ShouldBe(2);
    }

    [Fact]
    public void QuestionMarkInQuery_ActsAsSingleCharacterWildcard()
    {
        _temp.CreateFile("xabcx.txt");
        _temp.CreateFile("xacx.txt");

        Search("a?c").ShouldHaveSingleItem().FileName.ShouldBe("xabcx.txt");
    }

    [Fact]
    public void MapsFileInfoAndFolderPriorityOntoTheModel()
    {
        string path = _temp.CreateFile("clip.mp4", 42);

        var file = _engine.Search(new() { TestData.Folder(_temp.Path, priority: 3) }, "clip", cancellationToken: TestContext.Current.CancellationToken)
            .ShouldHaveSingleItem();

        file.FileName.ShouldBe("clip.mp4");
        file.FullPath.ShouldBe(path);
        file.Extension.ShouldBe(".mp4");
        file.FileSize.ShouldBe(42);
        file.Priority.ShouldBe(3);
        file.CreatedAt.ShouldBe(new FileInfo(path).CreationTime);
        file.Id.ShouldBe(0);
        file.Duration.ShouldBeNull();
        file.BitrateKbps.ShouldBeNull();
    }

    [Theory]
    [InlineData(FileTypeFilter.AllFiles, new[] { "a.mp4", "b.jpg", "c.txt" })]
    [InlineData(FileTypeFilter.Videos, new[] { "a.mp4" })]
    [InlineData(FileTypeFilter.Photos, new[] { "b.jpg" })]
    [InlineData(FileTypeFilter.Other, new[] { "c.txt" })]
    public void AppliesTheFileTypeFilter(FileTypeFilter filter, string[] expected)
    {
        _temp.CreateFile("a.mp4");
        _temp.CreateFile("b.jpg");
        _temp.CreateFile("c.txt");

        var results = _engine.Search(new() { TestData.Folder(_temp.Path) }, "", filter, TestContext.Current.CancellationToken);

        results.Select(f => f.FileName).Order().ShouldBe(expected);
    }

    [Fact]
    public void MissingFolder_IsSkipped_AndOtherFoldersAreStillSearched()
    {
        _temp.CreateFile("found.txt");
        var folders = new List<WatchedFolders>
        {
            TestData.Folder(Path.Combine(_temp.Path, "does-not-exist")),
            TestData.Folder(_temp.Path),
        };

        _engine.Search(folders, "found", cancellationToken: TestContext.Current.CancellationToken).ShouldHaveSingleItem();
    }

    [Fact]
    public void MultipleFolders_AreSearchedInListOrder_WithTheirOwnPriority()
    {
        string first = _temp.CreateSubdirectory("first");
        string second = _temp.CreateSubdirectory("second");
        _temp.CreateFile(@"second\b.txt");
        _temp.CreateFile(@"first\a.txt");
        var folders = new List<WatchedFolders> { TestData.Folder(second, 2), TestData.Folder(first, 1) };

        var results = _engine.Search(folders, "", cancellationToken: TestContext.Current.CancellationToken);

        results.Select(f => (f.FileName, f.Priority)).ShouldBe(new[] { ("b.txt", 2), ("a.txt", 1) });
    }

    [Fact]
    public void NoFolders_ReturnsEmpty()
    {
        _engine.Search(new(), "x", cancellationToken: TestContext.Current.CancellationToken).ShouldBeEmpty();
    }

    [Fact]
    public void PreCancelledToken_ThrowsBeforeSearching()
    {
        _temp.CreateFile("a.txt");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Should.Throw<OperationCanceledException>(() => _engine.Search(new() { TestData.Folder(_temp.Path) }, "", cancellationToken: cts.Token));
    }

    private List<FileModel> Search(string query) =>
        _engine.Search(new() { TestData.Folder(_temp.Path) }, query, cancellationToken: TestContext.Current.CancellationToken);
}
