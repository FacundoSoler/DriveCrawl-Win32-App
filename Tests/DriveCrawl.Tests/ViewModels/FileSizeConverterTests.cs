using System.Globalization;
using DriveCrawl.Tests.TestSupport;
using DriveCrawl_Win32_App;

namespace DriveCrawl.Tests.ViewModels;

public class FileSizeConverterTests
{
    private readonly FileSizeConverter _converter = new();

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(1_023L, "1023 B")]
    [InlineData(1_024L, "1 KB")]
    [InlineData(1_536L, "2 KB")]
    [InlineData(1_048_575L, "1024 KB")]
    [InlineData(1_048_576L, "1.0 MB")]
    [InlineData(1_572_864L, "1.5 MB")]
    [InlineData(1_073_741_823L, "1024.0 MB")]
    [InlineData(1_073_741_824L, "1.0 GB")]
    [InlineData(5_905_580_032L, "5.5 GB")]
    public void Convert_LongBytes_FormatsWithLargestFittingUnit(long bytes, string expected)
    {
        using var _ = CultureScope.Invariant();

        var result = _converter.Convert(bytes, typeof(string), null!, CultureInfo.InvariantCulture);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Convert_NegativeBytes_FallsThroughToBytes()
    {
        using var _ = CultureScope.Invariant();

        _converter.Convert(-5L, typeof(string), null!, CultureInfo.InvariantCulture).ShouldBe("-5 B");
    }

    [Theory]
    [InlineData(1024)]
    [InlineData("1024")]
    [InlineData(1024.0)]
    [InlineData(null)]
    public void Convert_NonLongValue_ReturnsQuestionMark(object? value)
    {
        _converter.Convert(value!, typeof(string), null!, CultureInfo.InvariantCulture).ShouldBe("?");
    }

    [Fact]
    public void Convert_UsesCurrentThreadCulture_NotTheCultureArgument()
    {
        // Why: WPF passes the binding culture, but the converter formats with CurrentCulture. Users on es-ES see "1,5 MB".
        using var _ = new CultureScope("es-ES");

        var result = _converter.Convert(1_572_864L, typeof(string), null!, CultureInfo.InvariantCulture);

        result.ShouldBe("1,5 MB");
    }

    [Fact]
    public void ConvertBack_Always_ThrowsNotImplemented()
    {
        Should.Throw<NotImplementedException>(
            () => _converter.ConvertBack("1 KB", typeof(long), null!, CultureInfo.InvariantCulture));
    }
}
