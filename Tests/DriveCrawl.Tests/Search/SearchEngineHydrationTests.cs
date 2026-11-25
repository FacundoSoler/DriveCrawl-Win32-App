using System.Text;
using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl.Tests.Search;

public sealed class SearchEngineHydrationTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly SearchEngine _engine = new();

    public SearchEngineHydrationTests()
    {
        Assert.SkipWhen(Environment.ProcessorCount < 2, "Hydration throws on single-core machines (MaxDegreeOfParallelism = 0).");
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task MediaWithDuration_GetsDurationAndBitrateFromFileSize()
    {
        string path = Path.Combine(_temp.Path, "tone.wav");
        WriteSilentWav(path, sampleRate: 8_000, seconds: 2);
        var vm = TestData.ViewModel("tone.wav", fileSize: 250_000, fullPath: path);

        await _engine.HydrateVideoMetadataAsync(new[] { vm }, TestContext.Current.CancellationToken);

        vm.Duration!.Value.TotalSeconds.ShouldBe(2, tolerance: 0.01);
        vm.BitrateKbps.ShouldBe(1_000);
        vm.VideoHeight.ShouldBe(0);
    }

    [Fact]
    public async Task UnreadableOrMissingFiles_AreIgnored()
    {
        var corrupt = TestData.ViewModel("broken.mp4", fullPath: _temp.CreateFile("broken.mp4", 64));
        var missing = TestData.ViewModel("gone.mp4", fullPath: Path.Combine(_temp.Path, "gone.mp4"));

        await _engine.HydrateVideoMetadataAsync(new[] { corrupt, missing }, TestContext.Current.CancellationToken);

        corrupt.Duration.ShouldBeNull();
        corrupt.BitrateKbps.ShouldBeNull();
        missing.Duration.ShouldBeNull();
    }

    [Fact]
    public async Task EmptyInput_Completes()
    {
        await _engine.HydrateVideoMetadataAsync(Array.Empty<FileViewModel>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PreCancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _engine.HydrateVideoMetadataAsync(new[] { TestData.ViewModel("a.mp4") }, cts.Token));
    }

    private static void WriteSilentWav(string path, int sampleRate, int seconds)
    {
        const short channels = 1;
        const short bitsPerSample = 8;
        int dataSize = sampleRate * seconds;

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(Enumerable.Repeat((byte)128, dataSize).ToArray());
    }
}
