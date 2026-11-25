using System.IO;
using Microsoft.Win32;

namespace DriveCrawl_Win32_App.Services
{
    public interface IFolderPicker
    {
        string? PickFolder(string title, string? initialFolder);
    }

    public interface IPathProbe
    {
        bool DirectoryExists(string path);

        bool IsRemovableDrive(string path);
    }

    public class OpenFolderDialogPicker : IFolderPicker
    {
        public string? PickFolder(string title, string? initialFolder)
        {
            var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
            if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder))
                dialog.InitialDirectory = initialFolder;

            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }
    }

    public class SystemPathProbe : IPathProbe
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);

        public bool IsRemovableDrive(string path)
        {
            try
            {
                string? root = Path.GetPathRoot(path);
                return !string.IsNullOrEmpty(root) && new DriveInfo(root).DriveType == DriveType.Removable;
            }
            catch (ArgumentException)
            {
                // UNC paths and other non-drive roots can't be classified; treat them as internal.
                return false;
            }
        }
    }
}
