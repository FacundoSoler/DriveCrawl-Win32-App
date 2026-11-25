using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl.Tests.ViewModels;

public class FileViewModelTests
{
    [Fact]
    public void FromModel_CopiesEveryModelField()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5);
        var model = new FileModel
        {
            Id = 7,
            FullPath = @"D:\Videos\clip.mp4",
            FileName = "clip.mp4",
            Extension = ".mp4",
            FileSize = 12_345,
            CreatedAt = created,
            Priority = 3,
            Duration = TimeSpan.FromSeconds(90),
            BitrateKbps = 4_500
        };

        var vm = FileViewModel.FromModel(model);

        vm.Id.ShouldBe(7);
        vm.FullPath.ShouldBe(@"D:\Videos\clip.mp4");
        vm.FileName.ShouldBe("clip.mp4");
        vm.Extension.ShouldBe(".mp4");
        vm.FileSize.ShouldBe(12_345);
        vm.CreatedAt.ShouldBe(created);
        vm.Priority.ShouldBe(3);
        vm.Duration.ShouldBe(TimeSpan.FromSeconds(90));
        vm.BitrateKbps.ShouldBe(4_500);
    }

    [Fact]
    public void FromModel_LeavesUiOnlyStateAtDefaults()
    {
        var vm = FileViewModel.FromModel(TestData.FileModel("clip.mp4"));

        vm.VideoHeight.ShouldBe(0);
        vm.VideoWidth.ShouldBe(0);
        vm.VideoResolution.ShouldBe(string.Empty);
        vm.IsSelected.ShouldBeFalse();
        vm.Thumbnail.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(1920, 0, "")]
    [InlineData(1920, 1080, "1920x1080")]
    [InlineData(0, 720, "0x720")]
    public void VideoResolution_IsEmptyOnlyWhenHeightIsZero(int width, int height, string expected)
    {
        var vm = new FileViewModel { VideoWidth = width, VideoHeight = height };

        vm.VideoResolution.ShouldBe(expected);
    }

    [Fact]
    public void FullPath_RaisesPropertyChanged_OnlyWhenValueChanges()
    {
        var vm = new FileViewModel { FullPath = "a" };
        var raised = RecordPropertyChanges(vm);

        vm.FullPath = "a";
        vm.FullPath = "b";

        raised.ShouldBe(new[] { nameof(FileViewModel.FullPath) });
    }

    [Fact]
    public void VideoDimensions_RaisePropertyChanged_OnlyWhenValueChanges()
    {
        var vm = new FileViewModel();
        var raised = RecordPropertyChanges(vm);

        vm.VideoHeight = 0;
        vm.VideoWidth = 0;
        vm.VideoHeight = 1080;
        vm.VideoWidth = 1920;

        raised.ShouldBe(new[] { nameof(FileViewModel.VideoHeight), nameof(FileViewModel.VideoWidth) });
    }

    [Fact]
    public void IsSelectedAndThumbnail_RaisePropertyChanged_EvenWhenValueIsUnchanged()
    {
        var vm = new FileViewModel();
        var raised = RecordPropertyChanges(vm);

        vm.IsSelected = false;
        vm.Thumbnail = null;

        raised.ShouldBe(new[] { nameof(FileViewModel.IsSelected), nameof(FileViewModel.Thumbnail) });
    }

    [Fact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void VideoResolution_IsNotNotified_WhenDimensionsChange()
    {
        // Why pinned: the grid's Resolution column binds to VideoResolution, so it won't refresh after metadata hydration.
        var vm = new FileViewModel();
        var raised = RecordPropertyChanges(vm);

        vm.VideoWidth = 1280;
        vm.VideoHeight = 720;

        raised.ShouldNotContain(nameof(FileViewModel.VideoResolution));
    }

    [Fact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void DurationAndBitrate_AreNotNotified_WhenSet()
    {
        // Why pinned: hydration sets these from worker threads after the rows are already bound.
        var vm = new FileViewModel();
        var raised = RecordPropertyChanges(vm);

        vm.Duration = TimeSpan.FromMinutes(1);
        vm.BitrateKbps = 1_000;

        raised.ShouldBeEmpty();
    }

    private static List<string?> RecordPropertyChanges(FileViewModel vm)
    {
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }
}
