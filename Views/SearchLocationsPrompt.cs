using System.Windows;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.ViewModels;

namespace DriveCrawl_Win32_App.Views
{
    /// <summary>
    /// Opens the Search Locations dialog with its dependencies wired up.
    /// </summary>
    public class SearchLocationsPrompt
    {
        private readonly ISearchLocationStore _store;
        private readonly IUserDialogs _dialogs;
        private readonly IFolderPicker _folderPicker;
        private readonly IPathProbe _pathProbe;

        public SearchLocationsPrompt(ISearchLocationStore store, IUserDialogs dialogs, IFolderPicker folderPicker, IPathProbe pathProbe)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _pathProbe = pathProbe ?? throw new ArgumentNullException(nameof(pathProbe));
        }

        /// <returns>True when the user saved changes, so the caller should reload its list.</returns>
        public bool TryManage(Window owner)
        {
            var viewModel = new SearchLocationsViewModel(_store, _dialogs, _folderPicker, _pathProbe);
            viewModel.Load();

            var window = new SearchLocationsWindow(viewModel) { Owner = owner };
            return window.ShowDialog() == true;
        }
    }
}
