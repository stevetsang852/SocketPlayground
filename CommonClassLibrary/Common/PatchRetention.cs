using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CommonClassLibrary
{
    /// <summary>
    /// Selects old upgrade patch directories for deletion while keeping the newest N
    /// and never touching a protected (active) directory.
    /// </summary>
    public static class PatchRetention
    {
        public static bool IsPatchDirectoryName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            // Matches legacy "...patch" and current "..._patch" HandlePath naming.
            return name.EndsWith("patch", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns full paths that should be deleted under <paramref name="upgradeRoot"/>,
        /// keeping the newest <paramref name="keepCount"/> patch dirs (by LastWriteTimeUtc).
        /// <paramref name="protectFullPath"/> is always retained even if that exceeds keepCount.
        /// </summary>
        public static IReadOnlyList<string> SelectPatchDirsToDelete(
            string upgradeRoot,
            string? protectFullPath,
            int keepCount,
            IEnumerable<DirectoryInfo>? directories = null)
        {
            if (string.IsNullOrWhiteSpace(upgradeRoot) || !Directory.Exists(upgradeRoot))
            {
                return Array.Empty<string>();
            }

            if (keepCount < 0)
            {
                keepCount = 0;
            }

            var protect = string.IsNullOrWhiteSpace(protectFullPath)
                ? null
                : Normalize(protectFullPath);

            var rootFull = Normalize(upgradeRoot);

            var allPatchDirs = (directories ?? new DirectoryInfo(upgradeRoot).EnumerateDirectories())
                .Where(d => IsPatchDirectoryName(d.Name))
                .Select(d => new DirectoryInfo(d.FullName))
                .Where(d => IsUnderRoot(Normalize(d.FullName), rootFull))
                .OrderByDescending(d => d.LastWriteTimeUtc)
                .ThenByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var keepers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in allPatchDirs.Take(keepCount))
            {
                keepers.Add(Normalize(d.FullName));
            }

            if (protect is not null)
            {
                keepers.Add(protect);
            }

            return allPatchDirs
                .Select(d => Normalize(d.FullName))
                .Where(full => !keepers.Contains(full))
                .ToList();
        }

        /// <summary>
        /// Deletes directories returned by <see cref="SelectPatchDirsToDelete"/>; returns deleted paths.
        /// </summary>
        public static IReadOnlyList<string> Apply(string upgradeRoot, string? protectFullPath, int keepCount)
        {
            var toDelete = SelectPatchDirsToDelete(upgradeRoot, protectFullPath, keepCount);
            var deleted = new List<string>();
            foreach (var path in toDelete)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                        deleted.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PatchRetention] failed to delete '{path}': {ex.Message}");
                }
            }

            return deleted;
        }

        private static string Normalize(string path) =>
            Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static bool IsUnderRoot(string full, string rootFull) =>
            string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
