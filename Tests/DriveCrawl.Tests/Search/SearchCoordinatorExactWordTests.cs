using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;

namespace DriveCrawl.Tests.Search;

public class SearchCoordinatorExactWordTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankQuery_ReturnsTheSameListUnchanged(string query)
    {
        var input = new List<FileViewModel> { TestData.ViewModel("anything.mp4") };

        SearchCoordinator.FilterByExactWord(input, query).ShouldBeSameAs(input);
    }

    [Theory]
    [InlineData("cat", "my cat.mp4", true)]
    [InlineData("cat", "Cat-video.mkv", true)]
    [InlineData("CAT", "a.cat.video.avi", true)]
    [InlineData("cat", "category.mp4", false)]
    [InlineData("cat", "my_cat_video.mp4", false)]
    [InlineData("cat", "cat2.mp4", false)]
    [InlineData("my cat", "my cat video.mp4", true)]
    [InlineData("my cat", "my  cat video.mp4", false)]
    public void MatchesWholeWordsOnly_IgnoringCase(string query, string fileName, bool expected)
    {
        Matches(query, fileName).ShouldBe(expected);
    }

    [Fact]
    public void ExtensionIsStripped_SoTheQueryCannotMatchIt()
    {
        Matches("mp4", "video.mp4").ShouldBeFalse();
    }

    [Theory]
    [InlineData("a.b", "x a.b y.txt", true)]
    [InlineData("a.b", "x axb y.txt", false)]
    [InlineData("(1)", "clip (1).mp4", false)]
    public void RegexMetacharacters_AreMatchedLiterally(string query, string fileName, bool expected)
    {
        // "(1)" fails because \b cannot sit between a space and "(": both are non-word characters.
        Matches(query, fileName).ShouldBe(expected);
    }

    [Theory]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    [InlineData("learn c++.pdf", false)]
    [InlineData("c++x.pdf", true)]
    public void QueryEndingInNonWordCharacter_NeedsAWordCharacterAfterIt(string fileName, bool expected)
    {
        Matches("c++", fileName).ShouldBe(expected);
    }

    [Fact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void ExtensionStripping_IsCaseSensitive()
    {
        // Why pinned: FileName "clip.MP4" with Extension ".mp4" keeps its extension, so "MP4" matches.
        var vm = TestData.ViewModel("clip.MP4");
        vm.Extension = ".mp4";

        SearchCoordinator.FilterByExactWord(new() { vm }, "MP4").ShouldHaveSingleItem();
    }

    [Fact]
    public void PreservesInputOrder()
    {
        var input = new List<FileViewModel>
        {
            TestData.ViewModel("z cat.mp4"),
            TestData.ViewModel("nothing.mp4"),
            TestData.ViewModel("a cat.mp4"),
        };

        var result = SearchCoordinator.FilterByExactWord(input, "cat");

        result.Select(f => f.FileName).ShouldBe(new[] { "z cat.mp4", "a cat.mp4" });
    }

    private static bool Matches(string query, string fileName)
    {
        var result = SearchCoordinator.FilterByExactWord(new() { TestData.ViewModel(fileName) }, query);
        return result.Count == 1;
    }
}
