using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCrawl_Win32_App
{
    public class FileViewModel : INotifyPropertyChanged
    {
        public int    Id        { get; set; }
        public string FullPath  { get; set; } = "";
        public string FileName  { get; set; } = "";
        public string Extension { get; set; } = "";
        public long   FileSize  { get; set; }
        public DateTime CreatedAt { get; set; }
        public int    Priority  { get; set; }

        private ImageSource? _thumbnail;
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(nameof(Thumbnail)); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public static FileViewModel FromModel(FileModel m) => new FileViewModel
        {
            Id        = m.Id,
            FullPath  = m.FullPath,
            FileName  = m.FileName,
            Extension = m.Extension,
            FileSize  = m.FileSize,
            CreatedAt = m.CreatedAt,
            Priority  = m.Priority
        };
    }

    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long b)
                return b switch
                {
                    >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
                    >= 1_048_576     => $"{b / 1_048_576.0:F1} MB",
                    >= 1_024         => $"{b / 1_024.0:F0} KB",
                    _                => $"{b} B"
                };
            return "?";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    internal static class ShellThumbnail
    {
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage([In] SIZE size, [In] SIIGBF flags, [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [Flags]
        private enum SIIGBF { ResizeToFit = 0, BiggerSizeOk = 1, MemoryOnly = 4, IconOnly = 8, ThumbnailOnly = 16 }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);

        private static readonly Guid IID_IShellItem = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

        public static BitmapSource? GetThumbnail(string path, int width, int height)
        {
            try
            {
                var iid = IID_IShellItem;
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out object shellItem);
                var factory = (IShellItemImageFactory)shellItem;
                factory.GetImage(new SIZE { cx = width, cy = height }, SIIGBF.ResizeToFit, out IntPtr hBitmap);
                if (hBitmap == IntPtr.Zero) return null;
                try
                {
                    var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally { DeleteObject(hBitmap); }
            }
            catch { return null; }
        }
    }

    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<FileViewModel> _results = new();
        private readonly DatabaseService _db     = new();
        private readonly SearchEngine    _search = new();
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _thumbCts;
        private int _thumbnailsLoaded;
        private int _totalThumbnailsToLoad;
        private bool _isOperationInProgress;
        
        // Store original search results before filtering
        private List<FileViewModel> _searchResults = new();

        // Thumbnail size matching Windows 11 "Large Icons"
        private const int ThumbW = 160;
        private const int ThumbH = 108;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public MainWindow()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = _results;
            ColSize.Binding = new Binding("FileSize") { Converter = new FileSizeConverter() };
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isOperationInProgress) StartSearch();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isOperationInProgress) StartSearch();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            CancelAllOperations();
        }

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is FileViewModel file)
                OpenFileWithDefaultProgram(file.FullPath);
        }

        private void CancelAllOperations()
        {
            _thumbCts?.Cancel();
            _cts?.Cancel();
            _isOperationInProgress = false;
            ShowStopButton(false);
            SetBusy(false);
            StatusLabel.Text = "Operation cancelled.";
        }

        private void TxtFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyFilter();
                e.Handled = true;
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string filterText = TxtFilter.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(filterText))
            {
                // Restore all original results if filter is empty
                _results.Clear();
                foreach (var item in _searchResults)
                {
                    _results.Add(item);
                }
            }
            else
            {
                // Filter results by filename, extension, or full path (case-insensitive)
                var filtered = _searchResults
                    .Where(f => f.FileName.ToLowerInvariant().Contains(filterText)
                             || f.Extension.ToLowerInvariant().Contains(filterText)
                             || f.FullPath.ToLowerInvariant().Contains(filterText))
                    .ToList();

                _results.Clear();
                foreach (var item in filtered)
                {
                    _results.Add(item);
                }
            }

            // Update result count
            int count = _results.Count;
            ResultCount.Text = count + (count != 1 ? " results" : " result");
            TotalSizeLabel.Text = "Total size: " + FormatTotalSize(_results.ToList());

            if (count == 0)
            {
                EmptyLabel.Text = "No results match the filter: " + TxtFilter.Text;
                EmptyState.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
            }
        }

        private async void StartSearch()
        {
            string query    = TxtSearch.Text.Trim();
            bool dupsOnly   = ChkDuplicates.IsChecked == true;
            bool showThumbs = ChkThumbnails.IsChecked == true;

            // Cancel previous search and thumbnail loads
            _thumbCts?.Cancel();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _isOperationInProgress = true;
            ShowStopButton(true);

            _results.Clear();
            _searchResults.Clear();
            TxtFilter.Clear();
            TxtFilter.IsEnabled = false;
            SetBusy(true);
            EmptyState.Visibility = Visibility.Collapsed;
            StatusLabel.Text = dupsOnly ? "Searching for duplicates..." : "Searching...";
            ResultCount.Text = "0 Results";

            ColThumbnail.Visibility = showThumbs ? Visibility.Visible : Visibility.Collapsed;
            ResultsGrid.RowHeight = showThumbs ? ThumbH + 8 : 38;

            try
            {
                List<WatchedFolders> folders = await Task.Run(() => _db.GetFolders(), token);

                if (folders.Count == 0)
                {
                    EmptyLabel.Text       = "No folders configured in the database.";
                    EmptyState.Visibility = Visibility.Visible;
                    StatusLabel.Text      = "No folders configured.";
                    return;
                }

                var sw = Stopwatch.StartNew();

                List<FileModel> found = await Task.Run(() => _search.Search(folders, query, token), token);

                token.ThrowIfCancellationRequested();

                if (dupsOnly)
                    found = await Task.Run(() => FilterDuplicates(found), token);

                token.ThrowIfCancellationRequested();

                // Sort by file size (largest first) on background thread
                found = await Task.Run(() => found.OrderByDescending(f => f.FileSize).ToList(), token);

                sw.Stop();

                // Project to FileViewModel on background thread
                var viewModels = await Task.Run(
                    () => found.Select(FileViewModel.FromModel).ToList(),
                    token
                );

                token.ThrowIfCancellationRequested();

                // Auto-select all but the lowest-priority file in each duplicate group
                if (dupsOnly)
                {
                    var groups = viewModels
                        .GroupBy(f => new { f.FileName, f.FileSize });
                    foreach (var group in groups)
                    {
                        // Find the file with the highest Priority value (lowest-priority drive)
                        var toKeep = group.OrderByDescending(f => f.Priority).FirstOrDefault();
                        foreach (var file in group)
                        {
                            file.IsSelected = (toKeep != null && file != toKeep);
                        }
                    }
                }

                // Add results to UI in batches to avoid blocking
                // Batch size: add 100 items at a time, then yield to UI
                const int batchSize = 100;
                for (int i = 0; i < viewModels.Count; i += batchSize)
                {
                    token.ThrowIfCancellationRequested();
                    var batch = viewModels.Skip(i).Take(batchSize).ToList();
                    foreach (var vm in batch)
                    {
                        _results.Add(vm);
                    }
                    await Task.Delay(1, token);
                }

                // Store the full search results for filtering
                _searchResults = viewModels.ToList();
                TotalSizeLabel.Text = "Total size: " + FormatTotalSize(viewModels);

                if (showThumbs && viewModels.Count > 0)
                    await LoadThumbnailsAsync(viewModels, token); // Load thumbnails lazily AFTER results are shown
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "Search cancelled.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Error: " + ex.Message;
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isOperationInProgress = false;
                ShowStopButton(false);
                SetBusy(false);
                TxtFilter.IsEnabled = _searchResults.Count > 0;
            }
        }

        // Loads thumbnails one by one in background, sets them on the ViewModel.
        // Loads from largest to smallest files (already sorted in viewModels).
        // INotifyPropertyChanged makes the grid update each cell as it arrives.
        private async Task LoadThumbnailsAsync(List<FileViewModel> items, CancellationToken searchToken)
        {
            _thumbCts?.Cancel();
            _thumbCts?.Dispose();
            _thumbCts = new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(searchToken, _thumbCts.Token);
            var token = linkedCts.Token;

            _thumbnailsLoaded = 0;
            _totalThumbnailsToLoad = items.Count;
            ThumbnailCounter.Visibility = Visibility.Visible;
            UpdateThumbnailCounter();

            // This is the key: it manages the 4-task limit natively without flooding
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = token
            };

            try
            {
                // Parallel.ForEachAsync throttles task creation natively. 
                // It won't flood the UI thread when the token is cancelled.
                await Parallel.ForEachAsync(items, options, async (vm, ct) =>
                {
                    var thumb = await Task.Run(() => ShellThumbnail.GetThumbnail(vm.FullPath, ThumbW, ThumbH), ct);

                    if (thumb != null)
                    {
                        // Ensure PropertyChanged updates safely on the UI thread
                        var dispatcher = Application.Current?.Dispatcher;
                        if (dispatcher != null)
                        {
                            await dispatcher.InvokeAsync(() => vm.Thumbnail = thumb);
                        }
                    }

                    Interlocked.Increment(ref _thumbnailsLoaded);
                    var counterDispatcher = Application.Current?.Dispatcher;
                    if (counterDispatcher != null)
                    {
                        await counterDispatcher.InvokeAsync(UpdateThumbnailCounter);
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Silent catch is correct here; it's a controlled stop.
            }
            finally
            {
                ThumbnailCounter.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateThumbnailCounter()
        {
            ThumbnailCounter.Text = $"Loading Thumbnails: {_thumbnailsLoaded}/{_totalThumbnailsToLoad}";
        }

        private static List<FileModel> FilterDuplicates(List<FileModel> all)
        {
            // Group by filename AND size using anonymous type
            var duplicateGroups = all
                .GroupBy(f => new { f.FileName, f.FileSize })
                .Where(g => g.Count() > 1)
                .ToList();

            var duplicateNamesAndSizes = duplicateGroups
                .Select(g => g.Key)
                .ToHashSet();

            return all
                .Where(f => duplicateNamesAndSizes.Contains(new { f.FileName, f.FileSize }))
                .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.FileSize)
                .ToList();
        }

        private string FormatTotalSize(List<FileViewModel> items)
        {
            long totalBytes = items.Sum(f => f.FileSize);
            var converter = new FileSizeConverter();
            return converter.Convert(totalBytes, typeof(string), null, CultureInfo.CurrentCulture).ToString() ?? "0 B";
        }

        private void SetBusy(bool busy)
        {
            SearchProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowStopButton(bool show)
        {
            BtnSearch.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            BtnStop.Visibility   = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OpenFileWithDefaultProgram(string fullPath)
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
                    FileName        = fullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening file: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FileViewModel file)
            {
                OpenContainingFolder(file.FullPath);
            }
        }

        // Attempts to focus the file in an existing Explorer window. 
        // If it's not open, it launches a new Explorer instance.
        private static void OpenContainingFolder(string fullPath)
        {
            try
            {
                string? folder = Path.GetDirectoryName(fullPath);
                if (folder == null || !Directory.Exists(folder))
                {
                    MessageBox.Show("Folder not found.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!FocusFileInExistingExplorer(fullPath)) // Try to be efficient: check if the folder is already open
                {
                    
                    Process.Start(new ProcessStartInfo // Fallback: open a brand new Explorer window and select the file
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{fullPath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening Explorer: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Uses Windows Shell COM automation to find open Explorer windows
        private static bool FocusFileInExistingExplorer(string filePath)
        {
            try
            {
                string? targetFolder = Path.GetDirectoryName(filePath);
                string? fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(targetFolder) || string.IsNullOrEmpty(fileName)) return false;

                string normalizedTarget = Path.GetFullPath(targetFolder).TrimEnd('\\').ToLowerInvariant();

                Type? shellAppType = Type.GetTypeFromProgID("Shell.Application");
                if (shellAppType == null) return false;

                dynamic shell = Activator.CreateInstance(shellAppType)!;
                var windows = shell.Windows();

                foreach (var win in windows)
                {
                    try
                    {
                        // We must handle cases where LocationURL is a URI (file:///...)
                        string? url = win.LocationURL as string;
                        if (string.IsNullOrEmpty(url)) continue;

                        string winPath = Path.GetFullPath(new Uri(url).LocalPath).TrimEnd('\\').ToLowerInvariant();

                        if (winPath == normalizedTarget)
                        {
                            IntPtr hwnd = (IntPtr)win.HWND;

                            // Handle minimized states and force foreground
                            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                            SetForegroundWindow(hwnd);

                            var folderView = win.Document;
                            var folderItem = folderView.Folder.ParseName(fileName);

                            if (folderItem != null)
                            {
                                // Now we can select it using the View
                                // Flags: 1=Deselect others, 8=Ensure visible, 16=Focus
                                folderView.SelectItem(folderItem, 1 | 8 | 16);
                            }

                            return true;
                        }
                    }
                    catch { /* Skip windows that are non-responsive */ }
                }
            }
            catch { /* Fallback to new instance */ }

            return false;
        }

        private void SelectCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateDeleteButtonState();
        }
        private void SelectCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateDeleteButtonState();
        }
        private void UpdateDeleteButtonState()
        {
            BtnDeleteSelected.IsEnabled = _results.Any(f => f.IsSelected);
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _results.Where(f => f.IsSelected).ToList();
            if (selected.Count == 0) return;
            var result = MessageBox.Show($"Are you sure you want to delete {selected.Count} file(s)?",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            int deleted = 0;
            foreach (var file in selected)
            {
                try
                {
                    if (File.Exists(file.FullPath))
                    {
                        File.Delete(file.FullPath);
                        deleted++;
                    }
                    _results.Remove(file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete: {file.FullPath}\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            MessageBox.Show($"Deleted {deleted} file(s).", "Delete Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateDeleteButtonState();
        }
    }
}
