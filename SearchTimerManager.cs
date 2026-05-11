using System.Diagnostics;
using System.Windows.Controls;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Manages the search timer and elapsed time tracking.
    /// Updates status labels with real-time elapsed time during search.
    /// </summary>
    public class SearchTimerManager
    {
        private readonly TextBlock _statusLabel;
        private System.Windows.Threading.DispatcherTimer? _searchTimer;
        private Stopwatch? _searchStopwatch;

        public SearchTimerManager(TextBlock statusLabel)
        {
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
        }

        public void StartTimer(string initialMessage)
        {
            StopTimer();

            _searchStopwatch = Stopwatch.StartNew();
            _searchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _searchTimer.Tick += (s, e) => UpdateStatusWithElapsedTime(initialMessage);
            _searchTimer.Start();
        }

        public void StopTimer()
        {
            try
            {
                _searchTimer?.Stop();
            }
            finally
            {
                _searchTimer = null;
                _searchStopwatch = null;
            }
        }

        public void SetCompletionStatus(string message)
        {
            StopTimer();
            if (_searchStopwatch != null)
            {
                double elapsedSeconds = _searchStopwatch.Elapsed.TotalSeconds;
                _statusLabel.Text = $"{message} {elapsedSeconds:F2}s";
            }
            else
            {
                _statusLabel.Text = message;
            }
        }

        private void UpdateStatusWithElapsedTime(string baseMessage)
        {
            if (_searchStopwatch != null)
            {
                double elapsed = _searchStopwatch.Elapsed.TotalSeconds;
                _statusLabel.Text = $"{baseMessage} ({elapsed:F1}s)";
            }
        }

        public double GetElapsedSeconds()
        {
            return _searchStopwatch?.Elapsed.TotalSeconds ?? 0;
        }
    }
}
