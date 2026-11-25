using DriveCrawl_Win32_App;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl.Tests.TestSupport;

public static class TestData
{
    public static FileModel FileModel(string fileName, long fileSize = 100, int priority = 1, string? directory = null)
    {
        string folder = directory ?? @"C:\media";
        return new FileModel
        {
            FileName = fileName,
            FullPath = System.IO.Path.Combine(folder, fileName),
            Extension = System.IO.Path.GetExtension(fileName),
            FileSize = fileSize,
            Priority = priority
        };
    }

    public static FileViewModel ViewModel(
        string fileName,
        long fileSize = 100,
        int priority = 1,
        int? bitrateKbps = null,
        int videoHeight = 0,
        string? fullPath = null)
    {
        return new FileViewModel
        {
            FileName = fileName,
            FullPath = fullPath ?? System.IO.Path.Combine(@"C:\media", fileName),
            Extension = System.IO.Path.GetExtension(fileName),
            FileSize = fileSize,
            Priority = priority,
            BitrateKbps = bitrateKbps,
            VideoHeight = videoHeight
        };
    }

    public static WatchedFolders Folder(string path, int priority = 1) => new() { Path = path, Priority = priority };
}
