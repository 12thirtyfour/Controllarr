using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles
{
    public static class RenameFolderCleanupHelper
    {
        private static readonly HashSet<string> OsJunkFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Thumbs.db",
            "desktop.ini",
            ".DS_Store"
        };

        private static readonly HashSet<string> OsJunkDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "@eaDir",
            ".@__thumb"
        };

        public static bool IsEmptyOrOsJunkOnly(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directoryPath))
                {
                    if (!IsOsJunkFileName(Path.GetFileName(file)))
                    {
                        return false;
                    }
                }

                foreach (var subDir in Directory.EnumerateDirectories(directoryPath))
                {
                    var subDirName = Path.GetFileName(Path.TrimEndingDirectorySeparator(subDir));
                    if (OsJunkDirectoryNames.Contains(subDirName))
                    {
                        continue;
                    }

                    if (!IsEmptyOrOsJunkOnly(subDir))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void CleanupVacatedDirectories(
            IEnumerable<string> vacatedDirectories,
            string boundaryDirectory,
            IEnumerable<string> protectedPaths,
            Logger logger)
        {
            if (vacatedDirectories == null || string.IsNullOrWhiteSpace(boundaryDirectory))
            {
                return;
            }

            var boundary = Path.TrimEndingDirectorySeparator(boundaryDirectory.Trim());
            var protectedList = (protectedPaths ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.TrimEndingDirectorySeparator(p.Trim()))
                .ToList();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawStartDir in vacatedDirectories)
            {
                if (string.IsNullOrWhiteSpace(rawStartDir))
                {
                    continue;
                }

                var startDir = Path.TrimEndingDirectorySeparator(rawStartDir.Trim());
                if (!seen.Add(startDir))
                {
                    continue;
                }

                var dir = startDir;
                while (!string.IsNullOrWhiteSpace(dir) &&
                       boundary.IsParentPath(dir) &&
                       !dir.PathEquals(boundary) &&
                       !protectedList.Any(p => dir.PathEquals(p) || dir.IsParentPath(p)))
                {
                    var parent = Directory.GetParent(Path.TrimEndingDirectorySeparator(dir))?.FullName;
                    if (parent != null)
                    {
                        parent = Path.TrimEndingDirectorySeparator(parent);
                    }

                    if (!Directory.Exists(dir))
                    {
                        if (string.IsNullOrWhiteSpace(parent) || parent.PathEquals(dir))
                        {
                            break;
                        }

                        dir = parent;
                        continue;
                    }

                    if (!IsEmptyOrOsJunkOnly(dir))
                    {
                        break;
                    }

                    try
                    {
                        Directory.Delete(dir, true);
                        logger?.Debug("Removed vacated folder after rename: {0}", dir);
                    }
                    catch (Exception ex)
                    {
                        logger?.Warn(ex, "Failed to remove vacated folder {0} after rename", dir);
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(parent) || parent.PathEquals(dir))
                    {
                        break;
                    }

                    dir = parent;
                }
            }
        }

        private static bool IsOsJunkFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            return OsJunkFileNames.Contains(fileName) ||
                   fileName.StartsWith("._", StringComparison.Ordinal);
        }
    }
}
