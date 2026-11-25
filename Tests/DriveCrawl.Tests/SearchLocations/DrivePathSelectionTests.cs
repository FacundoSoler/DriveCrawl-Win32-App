using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.SearchLocations;

public class DrivePathSelectionTests
{
    [Fact]
    public void FirstLoad_TicksEverything()
    {
        DrivePathSelection.ResolveSelectedIds(new Dictionary<int, bool>(), new[] { 1, 2, 3 })
            .ShouldBe(new[] { 1, 2, 3 }, ignoreOrder: true);
    }

    [Fact]
    public void Reload_KeepsUntickedLocationsUnticked()
    {
        var previous = new Dictionary<int, bool> { [1] = true, [2] = false };

        DrivePathSelection.ResolveSelectedIds(previous, new[] { 1, 2 }).ShouldBe(new[] { 1 });
    }

    [Fact]
    public void Reload_TicksNewlyAddedLocations()
    {
        var previous = new Dictionary<int, bool> { [1] = false };

        DrivePathSelection.ResolveSelectedIds(previous, new[] { 1, 7 }).ShouldBe(new[] { 7 });
    }

    [Fact]
    public void Reload_DropsRemovedLocations()
    {
        var previous = new Dictionary<int, bool> { [1] = true, [2] = true };

        DrivePathSelection.ResolveSelectedIds(previous, new[] { 2 }).ShouldBe(new[] { 2 });
    }
}
