namespace DriveCrawl_Win32_App.Models
{
    public class WatchedFolders
    {
        public int Id { get; set; }
        public required string Path { get; set; }
        public int IsExternal { get; set; } // 0 para interno, 1 para externo
        public int Priority { get; set; }
    }
}