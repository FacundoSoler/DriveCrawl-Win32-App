using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;

namespace DriveCrawl.Tests.Search;

public class SearchCoordinatorDuplicateTests
{
    [Fact]
    public void FilterDuplicates_KeepsOnlyFilesSharingNameAndSize()
    {
        var files = new List<DriveCrawl_Win32_App.Models.FileModel>
        {
            TestData.FileModel("a.mp4", 100, directory: @"C:\one"),
            TestData.FileModel("a.mp4", 100, directory: @"D:\two"),
            TestData.FileModel("unique.mp4", 100),
        };

        var result = SearchCoordinator.FilterDuplicates(files);

        result.Select(f => f.FullPath).ShouldBe(new[] { @"C:\one\a.mp4", @"D:\two\a.mp4" });
    }

    [Fact]
    public void FilterDuplicates_SameNameDifferentSize_IsNotADuplicate()
    {
        var files = new List<DriveCrawl_Win32_App.Models.FileModel>
        {
            TestData.FileModel("a.mp4", 100),
            TestData.FileModel("a.mp4", 101),
        };

        SearchCoordinator.FilterDuplicates(files).ShouldBeEmpty();
    }

    [Fact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void FilterDuplicates_ComparesNamesCaseSensitively()
    {
        // Why pinned: NTFS is case-insensitive, so "A.mp4" and "a.mp4" are likely the same file copied twice.
        var files = new List<DriveCrawl_Win32_App.Models.FileModel>
        {
            TestData.FileModel("A.mp4", 100),
            TestData.FileModel("a.mp4", 100),
        };

        SearchCoordinator.FilterDuplicates(files).ShouldBeEmpty();
    }

    [Fact]
    public void FilterDuplicates_OrdersByNameIgnoringCaseThenBySize()
    {
        var files = new List<DriveCrawl_Win32_App.Models.FileModel>
        {
            TestData.FileModel("b.mp4", 5, directory: @"C:\1"),
            TestData.FileModel("B.mp4", 1, directory: @"C:\1"),
            TestData.FileModel("a.mp4", 9, directory: @"C:\1"),
            TestData.FileModel("b.mp4", 5, directory: @"C:\2"),
            TestData.FileModel("B.mp4", 1, directory: @"C:\2"),
            TestData.FileModel("a.mp4", 9, directory: @"C:\2"),
        };

        var result = SearchCoordinator.FilterDuplicates(files);

        result.Select(f => (f.FileName, f.FileSize)).ShouldBe(new[]
        {
            ("a.mp4", 9L), ("a.mp4", 9L),
            ("B.mp4", 1L), ("B.mp4", 1L),
            ("b.mp4", 5L), ("b.mp4", 5L),
        });
    }

    [Fact]
    public void FilterDuplicates_EmptyInput_ReturnsEmpty()
    {
        SearchCoordinator.FilterDuplicates(new()).ShouldBeEmpty();
    }

    [Fact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void AutoSelect_KeepsTheCopyWithTheHighestPriorityNumber_AndSelectsTheRest()
    {
        // Why pinned: the UI shows Priority 1 as the most important (green) folder, so keeping the highest number may be inverted.
        var p1 = TestData.ViewModel("a.mp4", 100, priority: 1, fullPath: @"C:\p1\a.mp4");
        var p3 = TestData.ViewModel("a.mp4", 100, priority: 3, fullPath: @"C:\p3\a.mp4");
        var p2 = TestData.ViewModel("a.mp4", 100, priority: 2, fullPath: @"C:\p2\a.mp4");

        SearchCoordinator.AutoSelectDuplicatesForDeletion(new() { p1, p3, p2 });

        p3.IsSelected.ShouldBeFalse();
        p1.IsSelected.ShouldBeTrue();
        p2.IsSelected.ShouldBeTrue();
    }

    [Fact]
    public void AutoSelect_PriorityTie_KeepsTheFirstFileInOrder()
    {
        var first = TestData.ViewModel("a.mp4", 100, priority: 4, fullPath: @"C:\x\a.mp4");
        var second = TestData.ViewModel("a.mp4", 100, priority: 4, fullPath: @"C:\y\a.mp4");

        SearchCoordinator.AutoSelectDuplicatesForDeletion(new() { first, second });

        first.IsSelected.ShouldBeFalse();
        second.IsSelected.ShouldBeTrue();
    }

    [Fact]
    public void AutoSelect_SingleFileGroup_IsLeftUnselected()
    {
        var lonely = TestData.ViewModel("solo.mp4");
        lonely.IsSelected = true;

        SearchCoordinator.AutoSelectDuplicatesForDeletion(new() { lonely });

        lonely.IsSelected.ShouldBeFalse();
    }

    [Fact]
    public void AutoSelect_GroupsAreEvaluatedIndependently()
    {
        var a1 = TestData.ViewModel("a.mp4", 100, priority: 1);
        var a2 = TestData.ViewModel("a.mp4", 100, priority: 2);
        var b1 = TestData.ViewModel("b.mp4", 200, priority: 5);
        var b2 = TestData.ViewModel("b.mp4", 200, priority: 1);

        SearchCoordinator.AutoSelectDuplicatesForDeletion(new() { a1, a2, b1, b2 });

        new[] { a1.IsSelected, a2.IsSelected, b1.IsSelected, b2.IsSelected }
            .ShouldBe(new[] { true, false, false, true });
    }
}
