using Microsoft.Win32;
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

        private const string VlcAppPathKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\vlc.exe";
        private const string VlcArgumentsFormat = "--no-one-instance \"{0}\"";

        public static void OpenFileWithDefaultProgram(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                throw new ArgumentException("Path cannot be null or empty.", nameof(fullPath));
            }

            if (!File.Exists(fullPath))
            {
                MessageBox.Show("File doesn't exist.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (TryFindVlcPath(out string vlcPath))
                {
                    LaunchVlcNewInstance(vlcPath, fullPath);
                }
                else
                {
                    LaunchFallbackDefaultProgram(fullPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool TryFindVlcPath(out string vlcPath)
        {
            // Check 64-bit registry, 32-bit registry, user registry, then physical fallback folders
            vlcPath = GetRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry64)
                   ?? GetRegistryValue(RegistryHive.LocalMachine, RegistryView.Registry32)
                   ?? GetRegistryValue(RegistryHive.CurrentUser, RegistryView.Default)
                   ?? GetDefaultInstallationPath();

            return !string.IsNullOrEmpty(vlcPath) && File.Exists(vlcPath);
        }

        private static string GetRegistryValue(RegistryHive hive, RegistryView view)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var subKey = baseKey.OpenSubKey(VlcAppPathKey);
                return subKey?.GetValue(null)?.ToString(); // Default value contains full path to exe
            }
            catch
            {
                return null;
            }
        }

        private static string GetDefaultInstallationPath()
        {
            var standardPaths = new[]
            {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC", "vlc.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC", "vlc.exe")
        };

            foreach (var path in standardPaths)
            {
                if (File.Exists(path)) return path;
            }

            return null;
        }

        private static void LaunchVlcNewInstance(string vlcPath, string filePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = vlcPath,
                Arguments = string.Format(VlcArgumentsFormat, filePath),
                UseShellExecute = false
            });
        }

        private static void LaunchFallbackDefaultProgram(string filePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
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
