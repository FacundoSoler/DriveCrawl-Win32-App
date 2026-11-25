using System.IO;

namespace DriveCrawl_Win32_App.Services
{
    public static class DirectoryWriteProbe
    {
        /// <summary>
        /// Checks whether the directory (or, if it doesn't exist yet, its nearest existing parent) accepts new files.
        /// </summary>
        public static bool CanWriteTo(string directory)
        {
            // Probe an existing ancestor so that merely showing the options never creates folders the user didn't pick.
            string? probeDirectory = FindNearestExistingDirectory(directory);
            if (probeDirectory is null)
                return false;

            string probeFile = Path.Combine(probeDirectory, $".drivecrawl-write-test-{Guid.NewGuid():N}");
            try
            {
                File.WriteAllText(probeFile, string.Empty);
                File.Delete(probeFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static string? FindNearestExistingDirectory(string directory)
        {
            string? current = directory;
            while (!string.IsNullOrEmpty(current) && !Directory.Exists(current))
            {
                current = Path.GetDirectoryName(current);
            }
            return string.IsNullOrEmpty(current) ? null : current;
        }
    }
}
