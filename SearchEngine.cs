using DriveCrawl_Win32_App.Models;
using System.Diagnostics;
using System.IO;
using TagLib;

namespace DriveCrawl_Win32_App.Services
{
    public class SearchEngine
    {
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".flv", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp"
        };

        private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".svg", ".webp", ".tiff", ".ico", ".heic"
        };

        public List<FileModel> Search(List<WatchedFolders> folders, string query, FileTypeFilter fileTypeFilter = FileTypeFilter.AllFiles, CancellationToken cancellationToken = default)
        {
            var results = new List<FileModel>();

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(folder.Path)) continue;

                try
                {
                    // EnumerateFiles es ideal para no saturar la RAM
                    var files = Directory.EnumerateFiles(folder.Path, $"*{query}*", SearchOption.AllDirectories);

                    foreach (var filePath in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var info = new FileInfo(filePath);

                        if (!MatchesFileTypeFilter(info.Extension, fileTypeFilter))
                            continue;

                        var fileModel = new FileModel
                        {
                            FileName = info.Name,
                            FullPath = info.FullName,
                            Extension = info.Extension,
                            FileSize = info.Length,
                            CreatedAt = info.CreationTime,
                            Priority = folder.Priority
                        };

                        results.Add(fileModel);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignora carpetas protegidas (como System Volume Information)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en {folder.Path}: {ex.Message}");
                }
            }

            return results;
        }

        public async Task HydrateVideoMetadataAsync(IEnumerable<FileViewModel> files, CancellationToken token)
        {
            // Process in parallel with a sensible degree of concurrency to avoid thrashing HDDs
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2,
                CancellationToken = token
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(files, options, fileModel =>
                {
                    try
                    {
                        using var tfile = TagLib.File.Create(fileModel.FullPath);
                        var duration = tfile.Properties.Duration;
                        if (duration.TotalSeconds > 0)
                        {
                            fileModel.Duration = duration;
                            fileModel.BitrateKbps = (int)((fileModel.FileSize * 8) / (duration.TotalSeconds * 1000));
                            fileModel.VideoHeight = tfile.Properties.VideoHeight;
                            fileModel.VideoWidth = tfile.Properties.VideoWidth;
                        }
                    }
                    catch
                    {
                        // Ignore locked/corrupted
                    }
                });
            });
        }

        internal static bool MatchesFileTypeFilter(string extension, FileTypeFilter filter)
        {
            return filter switch
            {
                FileTypeFilter.AllFiles => true,
                FileTypeFilter.Videos => VideoExtensions.Contains(extension),
                FileTypeFilter.Photos => PhotoExtensions.Contains(extension),
                FileTypeFilter.Other => !VideoExtensions.Contains(extension) && !PhotoExtensions.Contains(extension),
                _ => true
            };
        }
    }
}