using System.Windows;
using DriveCrawl_Win32_App.ViewModels;

namespace DriveCrawl_Win32_App.Views
{
    public partial class DataLocationWindow : Window
    {
        public DataLocationWindow(DataLocationViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ModernWpf.ThemeManager.SetRequestedTheme(this, ModernWpf.ElementTheme.Dark);
            InitializeComponent();
            DataContext = viewModel;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
