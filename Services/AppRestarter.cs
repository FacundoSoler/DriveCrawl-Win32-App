using System.Diagnostics;
using System.Windows;

namespace DriveCrawl_Win32_App.Services
{
    public static class AppRestarter
    {
        public static void Restart()
        {
            string? executablePath = Environment.ProcessPath;
            if (executablePath is not null)
                Process.Start(new ProcessStartInfo(executablePath) { UseShellExecute = true });

            Application.Current.Shutdown();
        }
    }
}
