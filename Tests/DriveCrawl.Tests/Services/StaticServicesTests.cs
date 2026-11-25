using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.Services;

/// <summary>
/// The static Shell/Win32 services launch processes and show dialogs, so only their guard paths are unit tested.
/// </summary>
public class StaticServicesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenFileWithDefaultProgram_BlankPath_ThrowsArgumentException(string? path)
    {
        Should.Throw<ArgumentException>(() => FileSystemOperations.OpenFileWithDefaultProgram(path!))
            .ParamName.ShouldBe("fullPath");
    }

    [Fact]
    public void GetThumbnail_NonexistentPath_ReturnsNull()
    {
        using var temp = new TempDirectory();

        ShellThumbnailService.GetThumbnail(Path.Combine(temp.Path, "missing.mp4")).ShouldBeNull();
    }
}
