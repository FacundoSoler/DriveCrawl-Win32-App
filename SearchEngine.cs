using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using DriveCrawl_Win32_App.Models;

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
                    // EnumerateFiles es ideal para no saturar la RAM si hay miles de archivos
                    var files = Directory.EnumerateFiles(folder.Path, $"*{query}*", SearchOption.AllDirectories);

                    foreach (var filePath in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var info = new FileInfo(filePath);

                        if (!MatchesFileTypeFilter(info.Extension, fileTypeFilter))
                            continue;

                        results.Add(new FileModel
                        {
                            FileName = info.Name,
                            FullPath = info.FullName,
                            Extension = info.Extension,
                            FileSize = info.Length,
                            CreatedAt = info.CreationTime,
                            Priority = folder.Priority
                        });
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

        private static bool MatchesFileTypeFilter(string extension, FileTypeFilter filter)
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