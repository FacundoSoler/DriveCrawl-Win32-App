using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Manages file deletion operations with confirmation dialogs and error handling.
    /// Maintains separation of concerns for deletion logic.
    /// </summary>
    public class FileDeleteManager
    {
        private readonly ObservableCollection<FileViewModel> _resultsCollection;

        public event Action? DeletionStatusChanged;

        public FileDeleteManager(ObservableCollection<FileViewModel> resultsCollection)
        {
            _resultsCollection = resultsCollection ?? throw new ArgumentNullException(nameof(resultsCollection));
        }

        public async Task DeleteSelectedFilesAsync(IEnumerable<FileViewModel> selectedFiles)
        {
            var filesToDelete = selectedFiles.ToList();
            if (filesToDelete.Count == 0)
                return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete {filesToDelete.Count} file(s)?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            int deletedCount = 0;
            var failedDeletions = new List<(string Path, string Error)>();

            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file.FullPath))
                    {
                        File.Delete(file.FullPath);
                        deletedCount++;
                    }
                    _resultsCollection.Remove(file);
                }
                catch (Exception ex)
                {
                    failedDeletions.Add((file.FullPath, ex.Message));
                }
            }

            ShowDeletionSummary(deletedCount, failedDeletions);
            DeletionStatusChanged?.Invoke();
        }

        private void ShowDeletionSummary(int deletedCount, List<(string Path, string Error)> failedDeletions)
        {
            if (failedDeletions.Count > 0)
            {
                string failureMessage = string.Join("\n", 
                    failedDeletions.Take(3).Select(f => $"{Path.GetFileName(f.Path)}: {f.Error}"));

                MessageBox.Show(
                    $"Deleted {deletedCount} file(s).\n\nFailed:\n{failureMessage}",
                    "Delete Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    $"Deleted {deletedCount} file(s).",
                    "Delete Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
