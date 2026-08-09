namespace DriveCrawl_Win32_App.Models
{
    public class WatchedFolders
    {
        public int Id { get; set; }
        public required string Path { get; set; }
        public int IsExternal { get; set; } // 0 for interal drives, 1 for external drives (plugged in to computer)
        public int Priority { get; set; }
    }
}