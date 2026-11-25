using System.Windows.Controls;
using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;

namespace DriveCrawl.Tests.Search;

public class SearchTimerManagerTests
{
    [WpfFact]
    public void Constructor_NullLabel_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new SearchTimerManager(null!));
    }

    [WpfFact]
    public void GetElapsedSeconds_IsZero_BeforeStart()
    {
        new SearchTimerManager(new TextBlock()).GetElapsedSeconds().ShouldBe(0);
    }

    [WpfFact]
    public void GetElapsedSeconds_Advances_WhileRunning()
    {
        var timer = new SearchTimerManager(new TextBlock());

        timer.StartTimer("Searching...");
        Thread.Sleep(20);

        timer.GetElapsedSeconds().ShouldBeGreaterThan(0);
        timer.StopTimer();
    }

    [WpfFact]
    public void StopTimer_ResetsElapsedTime_AndIsSafeToRepeat()
    {
        var timer = new SearchTimerManager(new TextBlock());
        timer.StartTimer("Searching...");

        timer.StopTimer();
        timer.StopTimer();

        timer.GetElapsedSeconds().ShouldBe(0);
    }

    [WpfFact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void SetCompletionStatus_ShowsMessageWithoutElapsedTime()
    {
        // Why pinned: StopTimer() clears the stopwatch before it is read, so the elapsed-seconds branch never runs.
        var label = new TextBlock();
        var timer = new SearchTimerManager(label);
        timer.StartTimer("Searching...");
        Thread.Sleep(20);

        timer.SetCompletionStatus("Done.");

        label.Text.ShouldBe("Done.");
    }

    [WpfFact]
    [Trait(TestTraits.Behaviour, TestTraits.SuspectedBug)]
    public void GetElapsedSeconds_IsZero_AfterCompletionStatus()
    {
        // Why pinned: MainWindow reads elapsed time after SetCompletionStatus and reports "0.00 seconds".
        var timer = new SearchTimerManager(new TextBlock());
        timer.StartTimer("Searching...");
        Thread.Sleep(20);

        timer.SetCompletionStatus("Done.");

        timer.GetElapsedSeconds().ShouldBe(0);
    }
}
