using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.Search;

public class SearchEngineFileTypeFilterTests
{
    public static TheoryData<string> VideoExtensions =>
        new(".mp4", ".mkv", ".avi", ".mov", ".flv", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp");

    public static TheoryData<string> PhotoExtensions =>
        new(".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".tiff", ".ico", ".heic");

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".txt")]
    [InlineData("")]
    public void AllFiles_MatchesAnyExtension(string extension)
    {
        SearchEngine.MatchesFileTypeFilter(extension, FileTypeFilter.AllFiles).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(VideoExtensions))]
    public void Videos_MatchesEveryKnownVideoExtension(string extension)
    {
        SearchEngine.MatchesFileTypeFilter(extension, FileTypeFilter.Videos).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(PhotoExtensions))]
    public void Photos_MatchesEveryKnownPhotoExtension(string extension)
    {
        SearchEngine.MatchesFileTypeFilter(extension, FileTypeFilter.Photos).ShouldBeTrue();
    }

    [Theory]
    [InlineData(".MP4", FileTypeFilter.Videos)]
    [InlineData(".MkV", FileTypeFilter.Videos)]
    [InlineData(".JPG", FileTypeFilter.Photos)]
    [InlineData(".HeIc", FileTypeFilter.Photos)]
    public void Matching_IsCaseInsensitive(string extension, FileTypeFilter filter)
    {
        SearchEngine.MatchesFileTypeFilter(extension, filter).ShouldBeTrue();
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".txt")]
    [InlineData(".ts")]
    [InlineData(".m2ts")]
    [InlineData("mp4")]
    [InlineData("")]
    public void Videos_RejectsNonVideoExtensions(string extension)
    {
        SearchEngine.MatchesFileTypeFilter(extension, FileTypeFilter.Videos).ShouldBeFalse();
    }

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".raw")]
    [InlineData(".txt")]
    [InlineData("")]
    public void Photos_RejectsNonPhotoExtensions(string extension)
    {
        SearchEngine.MatchesFileTypeFilter(extension, FileTypeFilter.Photos).ShouldBeFalse();
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".ts", true)]
    [InlineData("", true)]
    [InlineData(".mp4", false)]
    [InlineData(".JPG", false)]
    public void Other_MatchesOnlyExtensionsThatAreNeitherVideoNorPhoto(string extension, bool expected)
    {
        SearchEngine.MatchesFileTypeFilter(extension, FileTypeFilter.Other).ShouldBe(expected);
    }

    [Fact]
    public void UndefinedFilterValue_MatchesEverything()
    {
        SearchEngine.MatchesFileTypeFilter(".txt", (FileTypeFilter)99).ShouldBeTrue();
    }
}
