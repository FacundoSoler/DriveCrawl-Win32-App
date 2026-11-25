using System.Windows;
using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Services;
using DriveCrawl_Win32_App.Views;

namespace DriveCrawl_Win32_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var dataLocations = DataLocationService.ForCurrentUser();
            var locationPrompt = new DataLocationPrompt(dataLocations);

            var location = dataLocations.TryResolveWithoutAsking() ?? locationPrompt.AskOnFirstRun();
            if (location is null)
            {
                Shutdown(0);
                return;
            }

            string databasePath = dataLocations.GetDatabasePath(location.Value);
            if (!TryInitializeDatabase(databasePath))
            {
                Shutdown(1);
                return;
            }

            ShowMainWindow(databasePath, dataLocations, locationPrompt);
        }

        private void ShowMainWindow(string databasePath, DataLocationService dataLocations, DataLocationPrompt locationPrompt)
        {
            string connectionString = DatabaseLocation.BuildConnectionString(databasePath);
            var searchLocationsPrompt = new SearchLocationsPrompt(
                new SearchLocationRepository(connectionString),
                new MessageBoxUserDialogs(),
                new OpenFolderDialogPicker(),
                new SystemPathProbe());
            var mainWindow = new MainWindow(new DatabaseService(connectionString), dataLocations, locationPrompt, searchLocationsPrompt);

            // Startup runs with OnExplicitShutdown so closing the first-run dialog doesn't end the app; from here on the main window decides.
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        private static bool TryInitializeDatabase(string databasePath)
        {
            try
            {
                DatabaseInitializer.Initialize(databasePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"DriveCrawl could not prepare its library at:\n{databasePath}\n\n{ex.Message}",
                    "Library Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}
