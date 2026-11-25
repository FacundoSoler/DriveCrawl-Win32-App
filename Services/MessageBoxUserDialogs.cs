using System.Windows;

namespace DriveCrawl_Win32_App.Services
{
    public class MessageBoxUserDialogs : IUserDialogs
    {
        public bool Confirm(string message, string title, MessageBoxImage icon)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
        }

        public void Notify(string message, string title, MessageBoxImage icon)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }
}
