using System.IO;

namespace DriveCrawl_Win32_App.Services
{
    public record PathNormalizationResult(string? Path, string? Error)
    {
        public bool IsValid => Error is null;
    }

    /// <summary>
    /// Rules every search location must satisfy, independent of any UI or storage.
    /// </summary>
    public static class SearchLocationRules
    {
        public const int MinPriority = 1;
        public const int MaxPriority = 4;

        public static bool IsValidPriority(int priority) => priority is >= MinPriority and <= MaxPriority;

        public static PathNormalizationResult Normalize(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return Invalid("Choose a folder.");

            string trimmed = rawPath.Trim().Trim('"');

            if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return Invalid("The path contains characters Windows doesn't allow.");

            // Checked before GetFullPath, which would otherwise silently resolve a relative path against the app's folder.
            if (!Path.IsPathFullyQualified(trimmed))
                return Invalid(@"Use a full path, such as D:\Videos.");

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(trimmed);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Invalid("That isn't a valid folder path.");
            }

            return new PathNormalizationResult(TrimTrailingSeparators(UppercaseDriveLetter(fullPath)), null);
        }

        /// <summary>
        /// Returns why <paramref name="candidatePath"/> can't sit alongside <paramref name="otherPaths"/>, or null if it can.
        /// </summary>
        public static string? FindConflict(string candidatePath, IEnumerable<string> otherPaths)
        {
            foreach (string otherRaw in otherPaths)
            {
                string other = Normalize(otherRaw).Path ?? otherRaw;

                if (string.Equals(candidatePath, other, StringComparison.OrdinalIgnoreCase))
                    return "This folder is already a search location.";

                // Overlaps make one physical file appear twice, so duplicate mode would mark the only copy for deletion.
                if (IsInside(candidatePath, other))
                    return $"This folder is inside {other}, which is already a search location.";

                if (IsInside(other, candidatePath))
                    return $"This folder contains {other}, which is already a search location.";
            }

            return null;
        }

        public static bool IsSameOrNested(string firstPath, string secondPath) =>
            string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase) ||
            IsInside(firstPath, secondPath) ||
            IsInside(secondPath, firstPath);

        private static bool IsInside(string childPath, string parentPath)
        {
            string parentPrefix = parentPath.EndsWith('\\') ? parentPath : parentPath + '\\';
            return childPath.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string UppercaseDriveLetter(string path) =>
            path.Length >= 2 && path[1] == ':' ? char.ToUpperInvariant(path[0]) + path[1..] : path;

        private static string TrimTrailingSeparators(string path)
        {
            string root = Path.GetPathRoot(path) ?? string.Empty;
            return path.Length > root.Length ? path.TrimEnd('\\', '/') : path;
        }

        private static PathNormalizationResult Invalid(string error) => new(null, error);
    }
}
