using System.Windows;
using DriveCrawl_Win32_App.Data;

namespace DriveCrawl_Win32_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // MainWindow queries the database in its constructor, so it is only created once the schema is ready.
            if (!TryInitializeDatabase())
            {
                Shutdown(1);
                return;
            }

            new MainWindow().Show();
        }

        private static bool TryInitializeDatabase()
        {
            string databasePath = DatabaseLocation.DefaultDatabasePath;
            try
            {
                DatabaseInitializer.Initialize(databasePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"DriveCrawl could not prepare its database at:\n{databasePath}\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
    }
}
