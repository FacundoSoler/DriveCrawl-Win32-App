using System.Windows;
using System.Windows.Controls;
using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;

namespace DriveCrawl.Tests.Thumbnails;

public class ThumbnailManagerTests
{
    [WpfFact]
    public void Constructor_NullLabel_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ThumbnailManager(null!));
    }

    [WpfFact]
    public async Task LoadThumbnails_EmptyList_ReportsZeroProgressAndHidesCounter()
    {
        var label = new TextBlock();
        var manager = new ThumbnailManager(label);
        var progress = new List<(int Loaded, int Total)>();
        manager.ThumbnailProgressChanged += (loaded, total) => progress.Add((loaded, total));

        await manager.LoadThumbnailsAsync(new List<FileViewModel>(), TestContext.Current.CancellationToken);

        progress.ShouldBe(new[] { (0, 0) });
        label.Text.ShouldBe("Loading Thumbnails: 0/0");
        label.Visibility.ShouldBe(Visibility.Collapsed);
    }

    [WpfFact]
    public async Task LoadThumbnails_CancelledSearchToken_CompletesWithoutThrowing()
    {
        var label = new TextBlock();
        var manager = new ThumbnailManager(label);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await manager.LoadThumbnailsAsync(new List<FileViewModel> { TestData.ViewModel("a.mp4") }, cts.Token);

        label.Visibility.ShouldBe(Visibility.Collapsed);
    }

    [WpfFact]
    public void CancelLoading_BeforeAnyLoad_DoesNotThrow()
    {
        Should.NotThrow(() => new ThumbnailManager(new TextBlock()).CancelLoading());
    }
}
