using DriveCrawl_Win32_App.Data;
using DriveCrawl_Win32_App.Models;
using DriveCrawl_Win32_App.Services;
using System.Text.RegularExpressions;

namespace DriveCrawl_Win32_App
{
    /// <summary>
    /// Orchestrates the search workflow including folder retrieval, duplicate filtering,
    /// sorting, and view model creation. Handles background task coordination.
    /// </summary>
    public class SearchCoordinator
    {
        private readonly DatabaseService _database;
        private readonly SearchEngine _searchEngine;

        public SearchCoordinator(DatabaseService database, SearchEngine searchEngine)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        }

        public async Task<List<FileViewModel>> ExecuteSearchAsync(string query, bool isExactPhrase, bool includeDuplicatesOnly, FileTypeFilter fileTypeFilter = FileTypeFilter.AllFiles, CancellationToken token = default)
        {
            var folders = await Task.Run(() => _database.GetFolders(), token);
            if (folders.Count == 0)
                throw new InvalidOperationException("No folders configured in the database.");

            var foundFiles = await RunSearchAsync(folders, query, fileTypeFilter, token);
            token.ThrowIfCancellationRequested();

            if (includeDuplicatesOnly)
            {
                foundFiles = await Task.Run(() => FilterDuplicates(foundFiles), token);
            }

            token.ThrowIfCancellationRequested();

            foundFiles = await Task.Run(() => foundFiles.OrderByDescending(f => f.FileSize).ToList(), token);

            var viewModels = await Task.Run(() => foundFiles.Select(FileViewModel.FromModel).ToList(), token);
            token.ThrowIfCancellationRequested();

            if (includeDuplicatesOnly)
            {
                AutoSelectDuplicatesForDeletion(viewModels);
            }

            if (isExactPhrase)
            {
                viewModels = await Task.Run(() => FilterByExactWord(viewModels, query), token);
            }

            return viewModels;
        }

        private async Task<List<FileModel>> RunSearchAsync(List<WatchedFolders> folders, string query, FileTypeFilter fileTypeFilter, CancellationToken token)
        {
            return await Task.Run(() => _searchEngine.Search(folders, query, fileTypeFilter, token), token);
        }

        private static List<FileModel> FilterDuplicates(List<FileModel> all)
        {
            var duplicateGroups = all
                .GroupBy(f => new { f.FileName, f.FileSize })
                .Where(g => g.Count() > 1)
                .ToList();

            var duplicateNamesAndSizes = duplicateGroups
                .Select(g => g.Key)
                .ToHashSet();

            return all
                .Where(f => duplicateNamesAndSizes.Contains(new { f.FileName, f.FileSize }))
                .OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.FileSize)
                .ToList();
        }

        private static void AutoSelectDuplicatesForDeletion(List<FileViewModel> viewModels)
        {
            var groups = viewModels.GroupBy(f => new { f.FileName, f.FileSize });
            foreach (var group in groups)
            {
                var lowestPriorityFile = group.OrderByDescending(f => f.Priority).FirstOrDefault();
                foreach (var file in group)
                {
                    file.IsSelected = (lowestPriorityFile != null && file != lowestPriorityFile);
                }
            }
        }

        private static List<FileViewModel> FilterByExactWord(List<FileViewModel> viewModels, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return viewModels;

            // Construct and compile the Regex for exact word matching
            string pattern = $@"\b{Regex.Escape(query)}\b";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            return viewModels.Where(f =>
            {
                string nameOnly = f.FileName;

                // Rigorous check: Strip the extension strictly in-memory using the ViewModel's properties
                if (!string.IsNullOrEmpty(f.Extension) && f.FileName.EndsWith(f.Extension))
                {
                    nameOnly = f.FileName.Substring(0, f.FileName.Length - f.Extension.Length);
                }

                return regex.IsMatch(nameOnly);
            }).ToList();
        }
    }
}
