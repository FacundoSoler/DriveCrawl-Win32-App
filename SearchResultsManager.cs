using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using static DriveCrawl_Win32_App.MainWindow;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Manages search result filtering, UI updates, and statistics.
    /// Separates filtering logic from the main window to promote SOLID principles.
    /// </summary>
    public class SearchResultsManager
    {
        private readonly ObservableCollection<FileViewModel> _resultsCollection;
        private readonly TextBlock _statusLabel;
        private readonly TextBlock _resultCountLabel;
        private readonly TextBlock _totalSizeLabel;
        private readonly TextBlock _filterPlaceholderLabel;
        private readonly Border _emptyState;
        private List<FileViewModel> _originalSearchResults = new();

        public SearchResultsManager(
            ObservableCollection<FileViewModel> resultsCollection,
            TextBlock statusLabel,
            TextBlock resultCountLabel,
            TextBlock totalSizeLabel,
            TextBlock filterPlaceholderLabel,
            Border emptyState)
        {
            _resultsCollection = resultsCollection ?? throw new ArgumentNullException(nameof(resultsCollection));
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
            _resultCountLabel = resultCountLabel ?? throw new ArgumentNullException(nameof(resultCountLabel));
            _totalSizeLabel = totalSizeLabel ?? throw new ArgumentNullException(nameof(totalSizeLabel));
            _filterPlaceholderLabel = filterPlaceholderLabel ?? throw new ArgumentNullException(nameof(filterPlaceholderLabel));
            _emptyState = emptyState ?? throw new ArgumentNullException(nameof(emptyState));
        }

        public void SetSearchResults(IEnumerable<FileViewModel> results)
        {
            var resultList = results.ToList();
            _originalSearchResults = resultList;
            RefreshResultsDisplay(resultList);
        }

        public void ClearResults()
        {
            _resultsCollection.Clear();
            _originalSearchResults.Clear();
            UpdateResultCountAndSize(0, 0);
        }

        public void ApplyFilter(string filterText, string bitrateText, Operator bitrateOperator, string videoHeight, Operator videoHeightOperator)
        {
            string normalizedFilter = filterText?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedBitrate = bitrateText?.Trim() ?? string.Empty;
            int videoHeightParsedValue;
            int.TryParse(videoHeight, out videoHeightParsedValue);

            bool isBitrateFilterActive = int.TryParse(normalizedBitrate, out int targetBitrate);
            bool isVideoResolutionFilterActive = int.TryParse(videoHeight, out int targetVideoHeight);

            if (string.IsNullOrEmpty(normalizedFilter) && !isBitrateFilterActive && !isVideoResolutionFilterActive)
            {
                RefreshResultsDisplay(_originalSearchResults);
                _emptyState.Visibility = Visibility.Collapsed;
                return;
            }

            var filtered = _originalSearchResults.Where(f =>
            {
                bool matchesName = string.IsNullOrEmpty(normalizedFilter) ||
                                   (f.FileName != null && f.FileName.ToLowerInvariant().Contains(normalizedFilter));

                bool matchesBitrate = true;
                if (isBitrateFilterActive)
                {
                    if (!f.BitrateKbps.HasValue)
                    {
                        matchesBitrate = false;
                    }
                    else
                    {
                        int fileBitrate = f.BitrateKbps.Value;
                        matchesBitrate = bitrateOperator switch
                        {
                            Operator.GreaterThan => fileBitrate > targetBitrate,
                            Operator.LessThan => fileBitrate < targetBitrate,
                            Operator.Equals => fileBitrate == targetBitrate,
                            _ => true
                        };
                    }
                }

                bool matchesResolution = true;
                if (isVideoResolutionFilterActive && videoHeightParsedValue > 0) {
                    if (f.VideoHeight == 0)
                    {
                        matchesResolution = false;
                    }
                    else {
                        int fileVideoHeightValue = f.VideoHeight;
                        matchesResolution = videoHeightOperator switch
                        {
                            Operator.GreaterThan => fileVideoHeightValue >= videoHeightParsedValue,
                            Operator.LessThan => fileVideoHeightValue <= videoHeightParsedValue,
                            Operator.Equals => fileVideoHeightValue == videoHeightParsedValue,
                            _ => true
                        };
                    }
                }

                return matchesName && matchesBitrate && matchesResolution;
            }).ToList();

            RefreshResultsDisplay(filtered);

            if (filtered.Count == 0)
            {
                _filterPlaceholderLabel.Text = "No results match the current filter criteria.";
                _emptyState.Visibility = Visibility.Visible;
            }
            else
            {
                _emptyState.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshResultsDisplay(List<FileViewModel> items)
        {
            _resultsCollection.Clear();
            foreach (var item in items)
            {
                _resultsCollection.Add(item);
            }

            UpdateResultCountAndSize(items.Count, CalculateTotalSize(items));

            if (items.Count == 0)
            {
                _emptyState.Visibility = Visibility.Visible;
            }
            else
            {
                _emptyState.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateResultCountAndSize(int count, long totalBytes)
        {
            _resultCountLabel.Text = count + (count != 1 ? " results" : " result");
            _totalSizeLabel.Text = "Total size: " + FormatTotalSize(totalBytes);
        }

        public int GetCurrentResultCount() => _resultsCollection.Count;

        public List<FileViewModel> GetSelectedResults() =>
            _resultsCollection.Where(f => f.IsSelected).ToList();

        private long CalculateTotalSize(List<FileViewModel> items) =>
            items.Sum(f => f.FileSize);

        private string FormatTotalSize(long totalBytes)
        {
            var converter = new FileSizeConverter();
            return converter.Convert(totalBytes, typeof(string), null, CultureInfo.CurrentCulture).ToString() ?? "0 B";
        }
    }
}
