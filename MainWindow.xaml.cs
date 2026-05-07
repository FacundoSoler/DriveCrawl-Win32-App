using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

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

        // Thumbnail size matching Windows 11 "Large Icons"
        private const int ThumbW = 160;
        private const int ThumbH = 108;

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
            StatusLabel.Text = "Operación cancelada.";
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
            SetBusy(true);
            EmptyState.Visibility = Visibility.Collapsed;
            StatusLabel.Text = dupsOnly ? "Buscando duplicados..." : "Buscando...";
            ResultCount.Text = "0 resultados";

            // Show/hide thumbnail column
            ColThumbnail.Visibility = showThumbs ? Visibility.Visible : Visibility.Collapsed;
            // Expand row height when thumbnails are on
            ResultsGrid.RowHeight = showThumbs ? ThumbH + 8 : 38;

            try
            {
                List<WatchedFolders> folders = await Task.Run(() => _db.GetFolders(), token);

                if (folders.Count == 0)
                {
                    EmptyLabel.Text       = "No hay carpetas configuradas en la base de datos.";
                    EmptyState.Visibility = Visibility.Visible;
                    StatusLabel.Text      = "Sin carpetas configuradas.";
                    return;
                }

                var sw = Stopwatch.StartNew();

                // Run search off UI thread with cancellation support
                List<FileModel> found = await Task.Run(() => _search.Search(folders, query, token), token);

                token.ThrowIfCancellationRequested();

                // Duplicates filter runs on background thread too
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
                    
                    // Yield to UI thread to keep responsive
                    await Task.Delay(1, token);
                }

                int count = _results.Count;
                ResultCount.Text = count + (count != 1 ? " resultados" : " resultado");
                StatusLabel.Text = "Listo en " + sw.Elapsed.TotalSeconds.ToString("F2") + "s"
                                   + (dupsOnly ? " (solo duplicados)" : "");

                if (count == 0)
                {
                    EmptyLabel.Text = dupsOnly
                        ? "No se encontraron duplicados para: " + (string.IsNullOrWhiteSpace(query) ? "(todos los archivos)" : query)
                        : "Sin resultados para: " + query;
                    EmptyState.Visibility = Visibility.Visible;
                }

                // Display total size
                TotalSizeLabel.Text = "Tamaño total: " + FormatTotalSize(viewModels);

                // Load thumbnails lazily AFTER results are shown
                if (showThumbs && count > 0)
                    await LoadThumbnailsAsync(viewModels, token);
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "Busqueda cancelada.";
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
                        await Application.Current.Dispatcher.InvokeAsync(() => vm.Thumbnail = thumb);
                    }

                    Interlocked.Increment(ref _thumbnailsLoaded);
                    await Application.Current.Dispatcher.InvokeAsync(UpdateThumbnailCounter);
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
            ThumbnailCounter.Text = $"Cargando miniaturas: {_thumbnailsLoaded}/{_totalThumbnailsToLoad}";
        }

        private static List<FileModel> FilterDuplicates(List<FileModel> all)
        {
            var duplicateNames = all
                .GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return all
                .Where(f => duplicateNames.Contains(f.FileName))
                .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
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
                    MessageBox.Show("El archivo no existe.", "Aviso",
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
                MessageBox.Show("Error al abrir el archivo: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void OpenContainingFolder(string fullPath)
        {
            try
            {
                string? folder = Path.GetDirectoryName(fullPath);
                if (folder == null || !Directory.Exists(folder))
                {
                    MessageBox.Show("No se encontro la carpeta.", "Aviso",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = "/select,\"" + fullPath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al abrir Explorer: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
