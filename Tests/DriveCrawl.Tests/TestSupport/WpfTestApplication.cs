using System.Windows;

namespace DriveCrawl.Tests.TestSupport;

public static class WpfTestApplication
{
    /// <summary>
    /// At runtime App.xaml provides ModernWpf's theme resources; windows that call ThemeManager fail without them.
    /// </summary>
    public static void EnsureCreated()
    {
        if (Application.Current is not null)
            return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ModernWpf.ThemeResources());
        app.Resources.MergedDictionaries.Add(new ModernWpf.Controls.XamlControlsResources());
    }
}
