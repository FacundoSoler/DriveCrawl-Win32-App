using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.Views;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DriveCrawl_Win32_App
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<FileViewModel> _results = new();
        public ObservableCollection<SelectableFolder> DrivePathsList { get; set; } = new ObservableCollection<SelectableFolder>();

        private readonly DatabaseService _database;
        private readonly DataLocationService _dataLocations;
        private readonly DataLocationPrompt _locationPrompt;
        private readonly SearchEngine _searchEngine = new();
        private readonly SearchCoordinator _searchCoordinator;
        private readonly SearchResultsManager _resultsManager;
        private readonly SearchTimerManager _timerManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FileActionsManager _fileActionsManager;

        private CancellationTokenSource? _searchCts;
        private bool _isOperationInProgress;

        private const int GridRowHeight = 38;
        private const int GridRowHeightWithThumbnails = 116;

        public enum Operator
        {
            Equals,
            GreaterThan,
            LessThan
        }

        public enum FileType {
            All,
            Videos,
            Photos,
            Other
        }

        public class SelectableFolder
        {
            public WatchedFolders UnderlyingFolder { get; set; }
            public object Path { get; set; }
            public bool IsSelected { get; set; } = true; // All options selected by default
        }

        public MainWindow(DatabaseService database, DataLocationService dataLocations, DataLocationPrompt locationPrompt)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _dataLocations = dataLocations ?? throw new ArgumentNullException(nameof(dataLocations));
            _locationPrompt = locationPrompt ?? throw new ArgumentNullException(nameof(locationPrompt));

            ModernWpf.ThemeManager.SetRequestedTheme(this, ModernWpf.ElementTheme.Dark);
            InitializeComponent();

            DataContext = this;

            ResultsGrid.ItemsSource = _results;
            LoadDrivePaths();

            SelectFileTypeByTag(FileType.Videos.ToString());
            ShowHideFilters();

            ColSize.Binding = new Binding("FileSize") { Converter = new FileSizeConverter() };
            LibraryButton.ToolTip = $"Library: {_dataLocations.CurrentDatabasePath}";

            _searchCoordinator = new SearchCoordinator(_database, _searchEngine);
            _resultsManager = new SearchResultsManager(
                _results, StatusLabel, ResultCount, TotalSizeLabel, EmptyLabel, EmptyState);
            _timerManager = new SearchTimerManager(StatusLabel);
            _thumbnailManager = new ThumbnailManager(ThumbnailCounter);
            _fileActionsManager = new FileActionsManager(_results, new MessageBoxUserDialogs());
            _fileActionsManager.OperationCompleted += ShowHideActionButtons;
        }

        #region Main Search Entry Point

        private async void LoadDrivePaths() {
            var watchedFolders = await Task.Run(() => _database.GetFolders());

            var selectableFolders = watchedFolders.Select(f => new SelectableFolder
            {
                UnderlyingFolder = f,
                Path = f.Path,
                IsSelected = true
            }).ToList();

            DrivePathsList.Clear();
            foreach (var folder in selectableFolders) {
                DrivePathsList.Add(folder);
            }
        }

        private async void StartSearch()
        {
            string query = TxtSearch.Text.Trim();
            bool isExactPhrase = ChkExactPhrase.IsChecked == true;
            bool dupsOnly = ChkDuplicates.IsChecked == true;
            bool showThumbs = ChkThumbnails.IsChecked == true;
            FileTypeFilter fileTypeFilter = GetSelectedFileTypeFilter();

            CancelPreviousSearch();
            _isOperationInProgress = true;
            ShowStopButton(true);

            PrepareSearchUI(showThumbs);

            string timerMessage = dupsOnly ? "Searching for duplicates..." : "Searching...";
            _timerManager.StartTimer(timerMessage);

            var token = _searchCts!.Token;

            try
            {
                var selectedWatchedFolders = DrivePathsList
                    .Where(f => f.IsSelected)
                    .Select(f => f.UnderlyingFolder)
                    .ToList();
                var results = await PerformSearchAsync(selectedWatchedFolders, query, isExactPhrase, dupsOnly, fileTypeFilter, token);

                token.ThrowIfCancellationRequested();

                if (results.Item1.Count > 0)
                {
                    await AddSearchResultsToUIAsync(results.Item1, token);
                    FinalizeSearchResults(results.Item1);

                    if (showThumbs)
                        await _thumbnailManager.LoadThumbnailsAsync(results.Item1, token);
                }

                if (!results.hydrationTask.IsCompleted)
                {
                    _timerManager.SetCompletionStatus($"Main Search completed in {_timerManager.GetElapsedSeconds():F2} seconds. Loading video file BitRates... ");
                    await results.hydrationTask;
                }

                token.ThrowIfCancellationRequested();

                _timerManager.SetCompletionStatus($"Ready. Search completed in {_timerManager.GetElapsedSeconds():F2} seconds.");
            }
            catch (OperationCanceledException)
            {
                StatusLabel.Text = "Search cancelled.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CleanupAfterSearch();
            }
        }

        private void CancelPreviousSearch()
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            _thumbnailManager.CancelLoading();
        }

        private void ShowStopButton(bool show)
        {
            BtnSearch.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            BtnStop.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PrepareSearchUI(bool showThumbnails)
        {
            _resultsManager.ClearResults();
            TxtFilter.Clear();
            TxtFilter.IsEnabled = false;
            SetBusy(true);
            EmptyState.Visibility = Visibility.Collapsed;
            ResultCount.Text = "0 Results";

            ColThumbnail.Visibility = showThumbnails ? Visibility.Visible : Visibility.Collapsed;
            ResultsGrid.RowHeight = showThumbnails ? GridRowHeightWithThumbnails : GridRowHeight;
        }

        private async Task<(List<FileViewModel>, Task hydrationTask)> PerformSearchAsync(List<WatchedFolders> selectedWatchedFolders, string query,
            bool isExactPhrase, bool includeDuplicatesOnly, FileTypeFilter fileTypeFilter, CancellationToken token)
        {
            try
            {
                return await _searchCoordinator.ExecuteSearchAsync(selectedWatchedFolders, query, isExactPhrase, includeDuplicatesOnly, fileTypeFilter, token);
            }
            catch (InvalidOperationException ex)
            {
                EmptyLabel.Text = ex.Message;
                EmptyState.Visibility = Visibility.Visible;
                StatusLabel.Text = "Search failed: No folders configured.";
                throw;
            }
        }

        private async Task AddSearchResultsToUIAsync(List<FileViewModel> viewModels, CancellationToken token)
        {
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
        }

        private void FinalizeSearchResults(List<FileViewModel> viewModels)
        {
            _resultsManager.SetSearchResults(viewModels);
            TxtFilter.IsEnabled = viewModels.Count > 0;
        }

        #endregion

        #region Search Utilities

        private void CleanupAfterSearch()
        {
            _timerManager.StopTimer();
            _isOperationInProgress = false;
            ShowStopButton(false);
            SetBusy(false);
            TxtFilter.IsEnabled = _resultsManager.GetCurrentResultCount() > 0;
        }

        private void SetBusy(bool busy)
        {
            SearchProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        private FileTypeFilter GetSelectedFileTypeFilter()
        {
            return CmbFileType.SelectedIndex switch
            {
                0 => FileTypeFilter.AllFiles,
                1 => FileTypeFilter.Videos,
                2 => FileTypeFilter.Photos,
                3 => FileTypeFilter.Other,
                _ => FileTypeFilter.AllFiles
            };
        }

        #endregion

        #region Filtering

        private void ApplyFilter()
        {
            var selectedBitrateOp = Operator.Equals;
            if (CmbBitrateOp.SelectedItem is ComboBoxItem selectedItem)
            {
                selectedBitrateOp = selectedItem.Tag?.ToString() switch
                {
                    "GreaterThan" => Operator.GreaterThan,
                    "LessThan" => Operator.LessThan,
                    _ => Operator.Equals
                };
            }

            var selectedVideoHeightOp = Operator.Equals;
            if (CmbVideoHeightOp.SelectedItem is ComboBoxItem selectedVideoHeightItem)
            {
                selectedVideoHeightOp = selectedVideoHeightItem.Tag?.ToString() switch
                {
                    "GreaterThan" => Operator.GreaterThan,
                    "LessThan" => Operator.LessThan,
                    _ => Operator.Equals
                };
            }

            string videoHeightValue = CmbResolution.Text;
            _resultsManager.ApplyFilter(TxtFilter.Text, TxtBitRate.Text, selectedBitrateOp, videoHeightValue, selectedVideoHeightOp);
        }

        #endregion

        #region File Operations

        #endregion

        #region Event Handlers - Search bar

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isOperationInProgress)
                StartSearch();
        }

        private void CmbFileType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowHideFilters();
        }

        private void SelectFileTypeByTag(string targetTag)
        {
            CmbFileType.SelectedItem = CmbFileType.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase));
        }

        private void ShowHideFilters()
        {
            if (!IsInitialized || PanelVideoFilters == null) return;

            string? selectedTag = (CmbFileType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            bool isVideo = string.Equals(selectedTag, "Videos", StringComparison.OrdinalIgnoreCase);

            PanelVideoFilters.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isOperationInProgress)
                StartSearch();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _searchCts?.Cancel();
            _isOperationInProgress = false;
            ShowStopButton(false);
            SetBusy(false);
            StatusLabel.Text = "Operation cancelled.";
        }

        #endregion

        #region Event Handlers - Filter bar

        private void TxtFilter_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyFilter();
                e.Handled = true;
            }
        }

        private void TxtBitrate_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ApplyFilter();
                e.Handled = true;
            }
        }

        private void TxtBitRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Text = Regex.Replace(tb.Text, "[^0-9]", "");
                tb.CaretIndex = tb.Text.Length; // Keep cursor at the end
            }
        }


        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        #endregion

        #region Event Handlers - Grid

        private void FileNameCell_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is FileViewModel file)
                FileSystemOperations.OpenFileWithDefaultProgram(file.FullPath);
        }

        private void ShowInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FileViewModel file)
                FileSystemOperations.OpenContainingFolder(file.FullPath);
        }

        #endregion

        #region Event Handlers - File Management

        private void SelectFileCheckBox_Click(object sender, RoutedEventArgs e)
        {
            ShowHideActionButtons();
        }

        private void ChkSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chkSelectAll && chkSelectAll.IsChecked.HasValue)
            {
                bool isChecked = chkSelectAll.IsChecked.Value;

                foreach (var file in _results)
                {
                    file.IsSelected = isChecked;
                }
            }
            ShowHideActionButtons();
        }

        private void ShowHideActionButtons()
        {
            var checkedItems = _results.Any(f => f.IsSelected);
            if (checkedItems) {
                BtnDeleteSelected.IsEnabled = true;
                BtnMove.IsEnabled = true;
            }
            else {
                BtnDeleteSelected.IsEnabled = false;
                BtnMove.IsEnabled = false;
            }
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _resultsManager.GetSelectedResults();
            if (selected.Count == 0) return;

            _ = _fileActionsManager.DeleteSelectedFilesAsync(selected);
        }

        private async void BtnMove_Click(object sender, RoutedEventArgs e)
        {
            var selected = _resultsManager.GetSelectedResults();
            if (selected.Count == 0) return;

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Destination Folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string destinationPath = dialog.FolderName;
                await _fileActionsManager.MoveSelectedFilesAsync(selected, destinationPath);
            }
        }

        private void LibraryButton_Click(object sender, RoutedEventArgs e)
        {
            LibraryButton.ContextMenu.PlacementTarget = LibraryButton;
            LibraryButton.ContextMenu.IsOpen = true;
        }

        private void OpenLibraryFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_dataLocations.CurrentDatabasePath is { } path)
                FileSystemOperations.OpenContainingFolder(path);
        }

        private void ChangeLibraryLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_locationPrompt.TryChangeLocation(this))
                AppRestarter.Restart();
        }

        #endregion

    }
}