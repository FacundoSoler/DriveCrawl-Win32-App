using System.IO;
using System.Windows;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.ViewModels;
using Microsoft.Data.Sqlite;

namespace DriveCrawl_Win32_App.Views
{
    /// <summary>
    /// UI flow around <see cref="DataLocationService"/>: shows the location dialog and reports the outcome to the user.
    /// </summary>
    public class DataLocationPrompt
    {
        private readonly DataLocationService _service;

        public DataLocationPrompt(DataLocationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public DataLocation? AskOnFirstRun()
        {
            var viewModel = new DataLocationViewModel(_service, DataLocationPromptMode.FirstRun);
            if (new DataLocationWindow(viewModel).ShowDialog() != true || viewModel.SelectedLocation is not { } chosen)
                return null;

            _service.SaveChoice(chosen);
            return chosen;
        }

        /// <returns>True when the location changed and the app must restart to use it.</returns>
        public bool TryChangeLocation(Window owner)
        {
            var viewModel = new DataLocationViewModel(_service, DataLocationPromptMode.Change);
            var window = new DataLocationWindow(viewModel) { Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (window.ShowDialog() != true || viewModel.SelectedLocation is not { } target)
                return false;

            try
            {
                // Pooled connections keep the current database file open; release them before copying it.
                SqliteConnection.ClearAllPools();
                var result = _service.ChangeLocation(target, viewModel.CopyCurrentLibrary);
                ReportExistingTargetLibrary(result, target);
                return result != LocationChangeResult.Unchanged;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(owner, $"The library could not be copied to the new location:\n\n{ex.Message}",
                    "Change Library Location", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ReportExistingTargetLibrary(LocationChangeResult result, DataLocation target)
        {
            if (result != LocationChangeResult.TargetAlreadyHadLibrary) return;

            MessageBox.Show(
                $"A library already exists at:\n{_service.GetDatabasePath(target)}\n\n" +
                "DriveCrawl will use that library. Nothing was overwritten, and your previous library was left untouched.",
                "Change Library Location", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
