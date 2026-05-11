using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace DriveCrawl_Win32_App.Services
{
    /// <summary>
    /// Provides file system operations including opening files and explorer windows.
    /// Handles both direct file opening and intelligent explorer window focusing.
    /// </summary>
    public static class FileSystemOperations
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public static void OpenFileWithDefaultProgram(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    MessageBox.Show("File doesn't exist.", "Warning",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void OpenContainingFolder(string fullPath)
        {
            try
            {
                string? folder = Path.GetDirectoryName(fullPath);
                if (folder == null || !Directory.Exists(folder))
                {
                    MessageBox.Show("Folder not found.", "Warning", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!FocusFileInExistingExplorer(fullPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{fullPath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Explorer: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool FocusFileInExistingExplorer(string filePath)
        {
            try
            {
                string? targetFolder = Path.GetDirectoryName(filePath);
                string? fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(targetFolder) || string.IsNullOrEmpty(fileName))
                    return false;

                string normalizedTarget = Path.GetFullPath(targetFolder).TrimEnd('\\').ToLowerInvariant();

                Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
                if (shellAppType == null)
                    return false;

                dynamic shell = Activator.CreateInstance(shellAppType)!;
                var windows = shell.Windows();

                foreach (var win in windows)
                {
                    try
                    {
                        string? url = win.LocationURL as string;
                        if (string.IsNullOrEmpty(url))
                            continue;

                        string winPath = Path.GetFullPath(new Uri(url).LocalPath).TrimEnd('\\').ToLowerInvariant();

                        if (winPath == normalizedTarget)
                        {
                            IntPtr hwnd = (IntPtr)win.HWND;

                            if (IsIconic(hwnd))
                                ShowWindow(hwnd, SW_RESTORE);
                            SetForegroundWindow(hwnd);

                            var folderView = win.Document;
                            var folderItem = folderView.Folder.ParseName(fileName);

                            if (folderItem != null)
                            {
                                folderView.SelectItem(folderItem, 1 | 8 | 16);
                            }

                            return true;
                        }
                    }
                    catch
                    {
                        // Skip unresponsive explorer windows
                    }
                }
            }
            catch
            {
                // Fall back to new instance if Shell.Application fails
            }

            return false;
        }
    }
}
