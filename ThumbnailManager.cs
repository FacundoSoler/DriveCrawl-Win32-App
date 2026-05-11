using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using DriveCrawl_Win32_App.Services;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Manages asynchronous thumbnail loading for file view models.
    /// Loads thumbnails in parallel with a configurable concurrency limit.
    /// </summary>
    public class ThumbnailManager
    {
        private const int MaxParallelThumbnailTasks = 4;

        private readonly TextBlock _thumbnailCounterLabel;
        private CancellationTokenSource? _thumbnailCts;
        private int _thumbnailsLoaded;
        private int _totalThumbnailsToLoad;

        public event Action<int, int>? ThumbnailProgressChanged;

        public ThumbnailManager(TextBlock thumbnailCounterLabel)
        {
            _thumbnailCounterLabel = thumbnailCounterLabel ?? throw new ArgumentNullException(nameof(thumbnailCounterLabel));
        }

        public async Task LoadThumbnailsAsync(List<FileViewModel> items, CancellationToken searchToken)
        {
            _thumbnailCts?.Cancel();
            _thumbnailCts?.Dispose();
            _thumbnailCts = new CancellationTokenSource();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(searchToken, _thumbnailCts.Token);
            var token = linkedCts.Token;

            _thumbnailsLoaded = 0;
            _totalThumbnailsToLoad = items.Count;
            _thumbnailCounterLabel.Visibility = Visibility.Visible;
            UpdateThumbnailCounter();

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelThumbnailTasks,
                CancellationToken = token
            };

            try
            {
                await Parallel.ForEachAsync(items, options, async (vm, ct) =>
                {
                    await LoadAndAssignThumbnailAsync(vm, ct);
                    IncrementThumbnailCounter();
                });
            }
            catch (OperationCanceledException)
            {
                // Controlled cancellation is expected
            }
            finally
            {
                _thumbnailCounterLabel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadAndAssignThumbnailAsync(FileViewModel vm, CancellationToken ct)
        {
            var thumb = await Task.Run(() => ShellThumbnailService.GetThumbnail(vm.FullPath), ct);

            if (thumb != null)
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => vm.Thumbnail = thumb);
                }
            }
        }

        private void IncrementThumbnailCounter()
        {
            Interlocked.Increment(ref _thumbnailsLoaded);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.InvokeAsync(UpdateThumbnailCounter);
            }
        }

        private void UpdateThumbnailCounter()
        {
            _thumbnailCounterLabel.Text = $"Loading Thumbnails: {_thumbnailsLoaded}/{_totalThumbnailsToLoad}";
            ThumbnailProgressChanged?.Invoke(_thumbnailsLoaded, _totalThumbnailsToLoad);
        }

        public void CancelLoading()
        {
            _thumbnailCts?.Cancel();
        }
    }
}
