using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.SearchLocations;

public class SearchLocationRulesTests
{
    [Theory]
    [InlineData(@"D:\Media", @"D:\Media")]
    [InlineData(@"D:\Media\", @"D:\Media")]
    [InlineData(@"D:\Media\\", @"D:\Media")]
    [InlineData(@"  D:\Media  ", @"D:\Media")]
    [InlineData("\"D:\\Media\"", @"D:\Media")]
    [InlineData(@"d:\media", @"D:\media")]
    [InlineData(@"D:/Media/Movies", @"D:\Media\Movies")]
    [InlineData(@"D:\Media\..\Videos", @"D:\Videos")]
    [InlineData(@"E:\", @"E:\")]
    [InlineData(@"\\nas\share\videos\", @"\\nas\share\videos")]
    public void Normalize_ProducesACanonicalFullPath(string raw, string expected)
    {
        var result = SearchLocationRules.Normalize(raw);

        result.IsValid.ShouldBeTrue();
        result.Path.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, "Choose a folder.")]
    [InlineData("", "Choose a folder.")]
    [InlineData("   ", "Choose a folder.")]
    [InlineData(@"Videos\Movies", @"Use a full path, such as D:\Videos.")]
    [InlineData("D:", @"Use a full path, such as D:\Videos.")]
    [InlineData(@"D:Videos", @"Use a full path, such as D:\Videos.")]
    [InlineData("D:\\Vid\0eos", "The path contains characters Windows doesn't allow.")]
    public void Normalize_RejectsUnusablePaths(string? raw, string expectedError)
    {
        var result = SearchLocationRules.Normalize(raw);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldBe(expectedError);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(0, false)]
    [InlineData(5, false)]
    public void IsValidPriority_AcceptsOneToFour(int priority, bool expected)
    {
        SearchLocationRules.IsValidPriority(priority).ShouldBe(expected);
    }

    [Theory]
    [InlineData(@"D:\Media")]
    [InlineData(@"d:\MEDIA")]
    [InlineData(@"D:\Media\")]
    public void FindConflict_SameFolderIgnoringCaseAndSlashes_IsADuplicate(string existing)
    {
        SearchLocationRules.FindConflict(@"D:\Media", new[] { existing })
            .ShouldBe("This folder is already a search location.");
    }

    [Fact]
    public void FindConflict_FolderInsideAnExistingLocation_IsRejected()
    {
        SearchLocationRules.FindConflict(@"D:\Media\Movies", new[] { @"D:\Media" })
            .ShouldBe(@"This folder is inside D:\Media, which is already a search location.");
    }

    [Fact]
    public void FindConflict_FolderContainingAnExistingLocation_IsRejected()
    {
        SearchLocationRules.FindConflict(@"D:\Media", new[] { @"D:\Media\Movies" })
            .ShouldBe(@"This folder contains D:\Media\Movies, which is already a search location.");
    }

    [Fact]
    public void FindConflict_DriveRoot_ContainsEverythingOnThatDrive()
    {
        SearchLocationRules.FindConflict(@"E:\Backup", new[] { @"E:\" })
            .ShouldBe(@"This folder is inside E:\, which is already a search location.");
    }

    [Theory]
    [InlineData(@"D:\Media2")]
    [InlineData(@"D:\Med")]
    [InlineData(@"E:\Media")]
    [InlineData(@"D:\Videos\Media")]
    public void FindConflict_SiblingsAndLookalikePrefixes_AreAllowed(string candidate)
    {
        SearchLocationRules.FindConflict(candidate, new[] { @"D:\Media" }).ShouldBeNull();
    }

    [Fact]
    public void FindConflict_NoOtherLocations_IsAllowed()
    {
        SearchLocationRules.FindConflict(@"D:\Media", Array.Empty<string>()).ShouldBeNull();
    }

    [Theory]
    [InlineData(@"D:\Media", @"D:\Media", true)]
    [InlineData(@"D:\Media", @"d:\media\Movies", true)]
    [InlineData(@"D:\Media\Movies", @"D:\Media", true)]
    [InlineData(@"D:\Media", @"D:\Media2", false)]
    public void IsSameOrNested_WorksInBothDirections(string first, string second, bool expected)
    {
        SearchLocationRules.IsSameOrNested(first, second).ShouldBe(expected);
    }
}
