using System.Windows;

namespace DriveCrawl_Win32_App.Services
{
    /// <summary>
    /// Abstracts modal user dialogs so that logic classes can be exercised without blocking on a MessageBox.
    /// </summary>
    public interface IUserDialogs
    {
        bool Confirm(string message, string title, MessageBoxImage icon);

        void Notify(string message, string title, MessageBoxImage icon);
    }
}
