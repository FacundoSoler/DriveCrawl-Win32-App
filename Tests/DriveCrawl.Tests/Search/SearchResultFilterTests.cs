using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;
using static DriveCrawl_Win32_App.MainWindow;

namespace DriveCrawl.Tests.Search;

public class SearchResultFilterTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("   ", "  ", "ALL")]
    [InlineData("", "abc", "1080p")]
    public void IsActive_IsFalse_WhenNoCriterionParses(string? name, string? bitrate, string? height)
    {
        var filter = new SearchResultFilter(name, bitrate, Operator.Equals, height, Operator.Equals);

        filter.IsActive.ShouldBeFalse();
    }

    [Theory]
    [InlineData("cat", "", "ALL")]
    [InlineData("", "5000", "ALL")]
    [InlineData("", "", "720")]
    public void IsActive_IsTrue_WhenAnyCriterionIsSet(string name, string bitrate, string height)
    {
        var filter = new SearchResultFilter(name, bitrate, Operator.Equals, height, Operator.Equals);

        filter.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("HOLIDAY", "my holiday.mp4", true)]
    [InlineData("  day  ", "Holiday.mp4", true)]
    [InlineData(".mp4", "clip.mp4", true)]
    [InlineData("beach", "Holiday.mp4", false)]
    public void Name_IsTrimmedCaseInsensitiveSubstringIncludingExtension(string name, string fileName, bool expected)
    {
        var filter = new SearchResultFilter(name, "", Operator.Equals, "ALL", Operator.Equals);

        filter.Matches(TestData.ViewModel(fileName)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(Operator.GreaterThan, 5000, false)]
    [InlineData(Operator.GreaterThan, 5001, true)]
    [InlineData(Operator.LessThan, 5000, false)]
    [InlineData(Operator.LessThan, 4999, true)]
    [InlineData(Operator.Equals, 5000, true)]
    [InlineData(Operator.Equals, 5001, false)]
    public void Bitrate_ComparesStrictly(Operator op, int fileBitrate, bool expected)
    {
        var filter = new SearchResultFilter("", "5000", op, "ALL", Operator.Equals);

        filter.Matches(TestData.ViewModel("a.mp4", bitrateKbps: fileBitrate)).ShouldBe(expected);
    }

    [Fact]
    public void Bitrate_TextIsTrimmed()
    {
        var filter = new SearchResultFilter("", "  5000  ", Operator.Equals, "ALL", Operator.Equals);

        filter.Matches(TestData.ViewModel("a.mp4", bitrateKbps: 5000)).ShouldBeTrue();
    }

    [Fact]
    public void Bitrate_ExcludesFilesWithoutBitrate_WhenActive()
    {
        var filter = new SearchResultFilter("", "1", Operator.GreaterThan, "ALL", Operator.Equals);

        filter.Matches(TestData.ViewModel("notes.txt", bitrateKbps: null)).ShouldBeFalse();
    }

    [Fact]
    public void Bitrate_IgnoresFilesWithoutBitrate_WhenInactive()
    {
        var filter = new SearchResultFilter("notes", "", Operator.GreaterThan, "ALL", Operator.Equals);

        filter.Matches(TestData.ViewModel("notes.txt", bitrateKbps: null)).ShouldBeTrue();
    }

    [Theory]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    [InlineData(Operator.GreaterThan, 1080, true)]
    [InlineData(Operator.GreaterThan, 1079, false)]
    [InlineData(Operator.LessThan, 1080, true)]
    [InlineData(Operator.LessThan, 1081, false)]
    [InlineData(Operator.Equals, 1080, true)]
    [InlineData(Operator.Equals, 720, false)]
    public void VideoHeight_GreaterAndLessThan_AreInclusive_UnlikeBitrate(Operator op, int fileHeight, bool expected)
    {
        // Why pinned: ">" and "<" behave as ">=" and "<=" for resolution, but strictly for bitrate.
        var filter = new SearchResultFilter("", "", Operator.Equals, "1080", op);

        filter.Matches(TestData.ViewModel("a.mp4", videoHeight: fileHeight)).ShouldBe(expected);
    }

    [Fact]
    public void VideoHeight_ExcludesFilesWithUnknownHeight_WhenActive()
    {
        var filter = new SearchResultFilter("", "", Operator.Equals, "720", Operator.LessThan);

        filter.Matches(TestData.ViewModel("a.mp4", videoHeight: 0)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void VideoHeight_NonPositive_IsActiveButConstrainsNothing(string height)
    {
        var filter = new SearchResultFilter("", "", Operator.Equals, height, Operator.Equals);

        filter.IsActive.ShouldBeTrue();
        filter.Matches(TestData.ViewModel("a.mp4", videoHeight: 0)).ShouldBeTrue();
    }

    [Fact]
    public void AllActiveCriteria_MustMatch()
    {
        var filter = new SearchResultFilter("clip", "1000", Operator.GreaterThan, "720", Operator.Equals);

        filter.Matches(TestData.ViewModel("clip.mp4", bitrateKbps: 2000, videoHeight: 720)).ShouldBeTrue();
        filter.Matches(TestData.ViewModel("other.mp4", bitrateKbps: 2000, videoHeight: 720)).ShouldBeFalse();
        filter.Matches(TestData.ViewModel("clip.mp4", bitrateKbps: 500, videoHeight: 720)).ShouldBeFalse();
        filter.Matches(TestData.ViewModel("clip.mp4", bitrateKbps: 2000, videoHeight: 1080)).ShouldBeFalse();
    }
}
