using System.Globalization;

namespace DriveCrawl.Tests.TestSupport;

/// <summary>
/// Temporarily switches the current thread's culture, because size formatting depends on it.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _originalCulture;
    private readonly CultureInfo _originalUiCulture;

    public CultureScope(string cultureName)
        : this(CultureInfo.GetCultureInfo(cultureName))
    {
    }

    public CultureScope(CultureInfo culture)
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static CultureScope Invariant() => new(CultureInfo.InvariantCulture);

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }
}
