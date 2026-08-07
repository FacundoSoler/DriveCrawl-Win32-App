using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Manages file operations (Delete, Move) with confirmation dialogs and error handling.
    /// </summary>
    public class FileActionsManager
    {
        private readonly ObservableCollection<FileViewModel> _resultsCollection;

        public event Action? OperationCompleted;

        public FileActionsManager(ObservableCollection<FileViewModel> resultsCollection)
        {
            _resultsCollection = resultsCollection ?? throw new ArgumentNullException(nameof(resultsCollection));
        }

        public async Task DeleteSelectedFilesAsync(IEnumerable<FileViewModel> selectedFiles)
        {
            var filesToDelete = selectedFiles.ToList();
            if (filesToDelete.Count == 0) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete {filesToDelete.Count} file(s)?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            int successCount = 0;
            var failedFiles = new List<(string Path, string Error)>();

            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file.FullPath))
                    {
                        File.Delete(file.FullPath);
                        successCount++;
                    }
                    _resultsCollection.Remove(file);
                }
                catch (Exception ex)
                {
                    failedFiles.Add((file.FullPath, ex.Message));
                }
            }

            ShowSummaryDialog("Delete", successCount, failedFiles);
            OperationCompleted?.Invoke();
        }

        public async Task MoveSelectedFilesAsync(IEnumerable<FileViewModel> selectedFiles, string destinationFolder)
        {
            var filesToMove = selectedFiles.ToList();
            if (filesToMove.Count == 0 || string.IsNullOrWhiteSpace(destinationFolder)) return;

            var result = MessageBox.Show(
                $"Are you sure you want to move {filesToMove.Count} file(s) to:\n{destinationFolder}?",
                "Confirm Move",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            int successCount = 0;
            var failedFiles = new List<(string Path, string Error)>();

            foreach (var file in filesToMove)
            {
                try
                {
                    if (!File.Exists(file.FullPath)) continue;

                    string newPath = Path.Combine(destinationFolder, file.FileName);
                    if (File.Exists(newPath))
                    {
                        failedFiles.Add((file.FullPath, "File already exists at destination."));
                        continue;
                    }

                    File.Move(file.FullPath, newPath);

                    file.FullPath = newPath;
                    file.IsSelected = false;
                    successCount++;
                }
                catch (Exception ex)
                {
                    failedFiles.Add((file.FullPath, ex.Message));
                }
            }

            ShowSummaryDialog("Move", successCount, failedFiles);
            OperationCompleted?.Invoke();
        }

        private void ShowSummaryDialog(string operationName, int successCount, List<(string Path, string Error)> failedFiles)
        {
            if (failedFiles.Count > 0)
            {
                string failureMessage = string.Join("\n",
                    failedFiles.Take(3).Select(f => $"{Path.GetFileName(f.Path)}: {f.Error}"));

                string moreIndicator = failedFiles.Count > 3 ? $"\n...and {failedFiles.Count - 3} more errors." : "";

                MessageBox.Show(
                    $"Successfully {operationName.ToLower()}d {successCount} file(s).\n\nFailed:\n{failureMessage}{moreIndicator}",
                    $"{operationName} Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    $"Successfully {operationName.ToLower()}d {successCount} file(s).",
                    $"{operationName} Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}