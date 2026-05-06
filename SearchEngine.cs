using System;
using System.IO;
using System.Collections.Generic;
using DriveCrawl_Win32_App.Models;

namespace DriveCrawl_Win32_App.Services
{
    public class SearchEngine
    {
        public List<FileModel> Search(List<WatchedFolders> folders, string query)
        {
            var results = new List<FileModel>();

            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder.Path)) continue;

                try
                {
                    // EnumerateFiles es ideal para no saturar la RAM si hay miles de archivos
                    var files = Directory.EnumerateFiles(folder.Path, $"*{query}*", SearchOption.AllDirectories);

                    foreach (var filePath in files)
                    {
                        var info = new FileInfo(filePath);
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
    }
}