using System.ComponentModel;
using System.Windows;
using DriveCrawl_Win32_App.ViewModels;

namespace DriveCrawl_Win32_App.Views
{
    public partial class SearchLocationsWindow : Window
    {
        private readonly SearchLocationsViewModel _viewModel;

        public SearchLocationsWindow(SearchLocationsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            _viewModel = viewModel;

            ModernWpf.ThemeManager.SetRequestedTheme(this, ModernWpf.ElementTheme.Dark);
            InitializeComponent();
            DataContext = viewModel;
        }

        private static SearchLocationItemViewModel ItemOf(object sender) =>
            (SearchLocationItemViewModel)((FrameworkElement)sender).DataContext;

        private void AddLocationButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BeginAdd();
            if (_viewModel.IsEditorOpen)
                EditorPathBox.Focus();
        }

        private void EditLocationButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BeginEdit(ItemOf(sender));
            EditorPathBox.Focus();
        }

        private void RemoveLocationButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RequestRemove(ItemOf(sender));
        }

        private void UndoRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.UndoRemove(ItemOf(sender));
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BrowseForEditorPath();
        }

        private void SaveEditButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyEdit();
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CancelEdit();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Save())
                DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            // Saved changes close silently; any other way out (Cancel, Esc, the X) asks before discarding edits.
            if (DialogResult != true && !_viewModel.ConfirmDiscardChanges())
                e.Cancel = true;
        }
    }
}
