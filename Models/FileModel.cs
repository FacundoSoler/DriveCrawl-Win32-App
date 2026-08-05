using System;

namespace DriveCrawl_Win32_App.Models
{
    public class FileModel
    {
        public int Id { get; set; }
        public required string FullPath { get; set; }
        public required string FileName { get; set; }
        public required string Extension { get; set; }
        public long FileSize { get; set; } // En bytes, luego lo pasamos a MB en la UI
        public DateTime CreatedAt { get; set; }
        public int Priority { get; set; } // 1, 2, 3 o 4
        public TimeSpan? Duration { get; set; }
        public int? BitrateKbps { get; set; }
    }
}