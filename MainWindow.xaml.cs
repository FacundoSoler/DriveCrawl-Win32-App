using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace DriveCrawl_Win32_App
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<FileViewModel> _results = new();
        private readonly DatabaseService _database = new();
        private readonly SearchEngine _searchEngine = new();
        private readonly SearchCoordinator _searchCoordinator;
        private readonly SearchResultsManager _resultsManager;
        private readonly SearchTimerManager _timerManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FileDeleteManager _deleteManager;

        private CancellationTokenSource? _searchCts;
        private bool _isOperationInProgress;

        private const int GridRowHeight = 38;
        private const int GridRowHeightWithThumbnails = 116;

        public MainWindow()
        {
            InitializeComponent();
            ResultsGrid.ItemsSource = _results;
            ColSize.Binding = new Binding("FileSize") { Converter = new FileSizeConverter() };

            _searchCoordinator = new SearchCoordinator(_database, _searchEngine);
            _resultsManager = new SearchResultsManager(
                _results, StatusLabel, ResultCount, TotalSizeLabel, EmptyLabel, EmptyState);
            _timerManager = new SearchTimerManager(StatusLabel);
            _thumbnailManager = new ThumbnailManager(ThumbnailCounter);
            _deleteManager = new FileDeleteManager(_results);
            _deleteManager.DeletionStatusChanged += UpdateDeleteButtonState;
        }

        #region Search Orchestration

        #endregion

        #region Main Search Entry Point

        private async void StartSearch()
        {
            string query = TxtSearch.Text.Trim();
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
                var viewModels = await PerformSearchAsync(query, dupsOnly, fileTypeFilter, token);
                token.ThrowIfCancellationRequested();

                if (viewModels.Count > 0)
                {
                    await AddSearchResultsToUIAsync(viewModels, token);
                    FinalizeSearchResults(viewModels);

                    if (showThumbs)
                        await _thumbnailManager.LoadThumbnailsAsync(viewModels, token);
                }

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

        private async Task<List<FileViewModel>> PerformSearchAsync(string query, bool includeDuplicatesOnly, FileTypeFilter fileTypeFilter, CancellationToken token)
        {
            try
            {
                return await _searchCoordinator.ExecuteSearchAsync(query, includeDuplicatesOnly, fileTypeFilter, token);
            }
            catch (InvalidOperationException ex)
            {
                EmptyLabel.Text = ex.Message;
                EmptyState.Visibility = Visibility.Visible;
                StatusLabel.Text = "Search failed: No folders configured.";
                return new List<FileViewModel>();
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
            _resultsManager.ApplyFilter(TxtFilter.Text);
        }

        #endregion

        #region File Operations

        private void DeleteSelectedFiles()
        {
            var selected = _resultsManager.GetSelectedResults();
            if (selected.Count > 0)
            {
                _ = _deleteManager.DeleteSelectedFilesAsync(selected);
            }
        }

        #endregion

        #region Event Handlers - Search

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isOperationInProgress)
                StartSearch();
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

        #region Event Handlers - Filter

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

        #endregion

        #region Event Handlers - Grid

        private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsGrid.SelectedItem is FileViewModel file)
                FileSystemOperations.OpenFileWithDefaultProgram(file.FullPath);
        }

        #endregion

        #region Event Handlers - File Management

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

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedFiles();
        }

        private void ShowInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FileViewModel file)
                FileSystemOperations.OpenContainingFolder(file.FullPath);
        }

        #endregion
    }
}
