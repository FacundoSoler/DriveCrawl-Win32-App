using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl_Win32_App
{
    // ---------------------------------------------------------------
    // FileModel must implement INotifyPropertyChanged so Thumbnail
    // can be set after the row is already visible in the grid.
    // Add this partial class extension to or add these members directly
    // to your existing FileModel.cs if you prefer.
    // ---------------------------------------------------------------
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

    // ---------------------------------------------------------------
    // FileSizeConverter
    // ---------------------------------------------------------------
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

    // ---------------------------------------------------------------
    // Windows Shell thumbnail extractor (no NuGet required)
    // Uses IShellItemImageFactory to same source Explorer uses
    // ---------------------------------------------------------------
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

    // ---------------------------------------------------------------
    // MainWindow
    // ---------------------------------------------------------------
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<FileViewModel> _results = new();
        private readonly DatabaseService _db     = new();
        private readonly SearchEngine    _search = new();
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _thumbCts;
        private int _thumbnailsLoaded;
        private int _totalThumbnailsToLoad;

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
            if (e.Key == Key.Enter) StartSearch();
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
            => StartSearch();

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is FileViewModel file)
                OpenContainingFolder(file.FullPath);
        }

        private async void StartSearch()
        {
            string query    = TxtSearch.Text.Trim();
            bool dupsOnly   = ChkDuplicates.IsChecked == true;
            bool showThumbs = ChkThumbnails.IsChecked == true;

            // Empty query with checkbox unchecked: scan everything (original behavior)
            // Empty query is always allowed to SearchEngine uses "*" pattern when empty

            // Cancel previous search and thumbnail loads
            _thumbCts?.Cancel();
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

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

                // Run search off UI thread to empty query becomes "*" inside SearchEngine
                List<FileModel> found = await Task.Run(() => _search.Search(folders, query), token);

                // Duplicates filter to runs on background thread too
                if (dupsOnly)
                    found = await Task.Run(() => FilterDuplicates(found), token);

                sw.Stop();

                // Project to FileViewModel (supports lazy Thumbnail property)
                var viewModels = found.Select(FileViewModel.FromModel).ToList();

                foreach (var vm in viewModels)
                {
                    token.ThrowIfCancellationRequested();
                    _results.Add(vm);
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
                    _ = LoadThumbnailsAsync(viewModels);
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
                SetBusy(false);
            }
        }

        // Loads thumbnails one by one in background, sets them on the ViewModel.
        // INotifyPropertyChanged makes the grid update each cell as it arrives.
        private async Task LoadThumbnailsAsync(List<FileViewModel> items)
        {
            _thumbCts?.Cancel();
            _thumbCts?.Dispose();
            _thumbCts = new CancellationTokenSource();
            var token = _thumbCts.Token;

            _thumbnailsLoaded = 0;
            _totalThumbnailsToLoad = items.Count;
            ThumbnailCounter.Visibility = Visibility.Visible;
            UpdateThumbnailCounter();

            // Semaphore limits concurrent shell calls (avoids hammering slow drives)
            var sem = new SemaphoreSlim(4);

            var tasks = items.Select(async vm =>
            {
                await sem.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    // Shell call must run on STA thread to use Task.Run with manual STA
                    var thumb = await Task.Run(() => ShellThumbnail.GetThumbnail(vm.FullPath, ThumbW, ThumbH), token);
                    if (thumb != null)
                        vm.Thumbnail = thumb; // INotifyPropertyChanged fires, grid updates
                    
                    Interlocked.Increment(ref _thumbnailsLoaded);
                    Dispatcher.Invoke(UpdateThumbnailCounter);
                }
                catch (OperationCanceledException) { }
                catch { /* file might be locked or deleted */ }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks);
            ThumbnailCounter.Visibility = Visibility.Collapsed;
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
            BtnSearch.IsEnabled       = !busy;
            SearchProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
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
