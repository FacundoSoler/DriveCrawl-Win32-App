namespace DriveCrawl_Win32_App.Services
{
    public static class DrivePathSelection
    {
        /// <summary>
        /// Decides which locations are ticked after the list reloads: ones the user had unticked stay unticked,
        /// everything else (including newly added locations) is ticked.
        /// </summary>
        public static ISet<int> ResolveSelectedIds(IReadOnlyDictionary<int, bool> previousSelection, IEnumerable<int> reloadedIds)
        {
            return reloadedIds
                .Where(id => !previousSelection.TryGetValue(id, out bool wasSelected) || wasSelected)
                .ToHashSet();
        }
    }
}
