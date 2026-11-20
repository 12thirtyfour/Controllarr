using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Disc;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace NzbDrone.Core.MediaFiles.Archive
{
    public interface IArchiveRepackService
    {
        void RepackArchive(string archivePath);
        void SyncSingleRomEntryName(string archivePath);
        byte[] ModernizeINesHeader(byte[] romBytes);
    }

    public class ArchiveRepackService : IArchiveRepackService
    {
        private readonly Logger _logger;

        public ArchiveRepackService(Logger logger)
        {
            _logger = logger;
        }

        public void RepackArchive(string archivePath)
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
            {
                var entries = archive.Entries.ToList();
                foreach (var entry in entries)
                {
                    if (!entry.Name.EndsWith(".smc", StringComparison.InvariantCultureIgnoreCase) ||
                        entry.Length % 1024 != 512)
                    {
                        continue;
                    }

                    _logger.Debug("Stripping 512-byte copier header from {0}", entry.Name);
                    var newEntryName = Path.GetFileNameWithoutExtension(entry.Name) + ".sfc";
                    var newEntry = archive.CreateEntry(newEntryName, CompressionLevel.Optimal);

                    using (var sourceStream = entry.Open())
                    using (var destStream = newEntry.Open())
                    {
                        var header = new byte[512];
                        var skipped = 0;
                        while (skipped < 512)
                        {
                            var read = sourceStream.Read(header, skipped, 512 - skipped);
                            if (read == 0)
                            {
                                break;
                            }

                            skipped += read;
                        }

                        sourceStream.CopyTo(destStream, 81920);
                    }

                    entry.Delete();
                }
            }
        }

        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".rar"
        };

        public void SyncSingleRomEntryName(string archivePath)
        {
            SanitizeSingleRomZip(archivePath);
        }

        public static bool IsArchiveExtension(string extension)
        {
            return !string.IsNullOrWhiteSpace(extension) && ArchiveExtensions.Contains(extension);
        }

        public static bool HasValidRomEntries(string archivePath, IBiosService biosService = null)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return false;
            }

            try
            {
                var extension = Path.GetExtension(archivePath);
                if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var zipArchive = ZipFile.OpenRead(archivePath);
                    foreach (var entry in zipArchive.Entries)
                    {
                        if (IsJunkEntry(entry.FullName, entry.Name))
                        {
                            continue;
                        }

                        if ((biosService != null && biosService.IsKnownBiosFileName(entry.Name)) ||
                            RegionHelper.IsBios(entry.Name, null, null, entry.FullName))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(entry.Name);
                        if (MediaFileExtensions.RomExtensions.Contains(ext) || IsArchiveExtension(ext))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (IsArchiveExtension(extension))
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath);
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key))
                        {
                            continue;
                        }

                        var fullName = entry.Key.Replace('\\', '/');
                        var name = Path.GetFileName(fullName);
                        if (IsJunkEntry(fullName, name))
                        {
                            continue;
                        }

                        if ((biosService != null && biosService.IsKnownBiosFileName(name)) ||
                            RegionHelper.IsBios(name, null, null, fullName))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(name);
                        if (MediaFileExtensions.RomExtensions.Contains(ext) || IsArchiveExtension(ext))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            catch
            {
            }

            return false;
        }

        public static string ResolvePrimaryRomFile(IEnumerable<string> romFiles)
        {
            if (romFiles == null)
            {
                return null;
            }

            var list = romFiles
                .Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (list.Count == 0)
            {
                return null;
            }

            var sheets = list
                .Where(f => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(f)))
                .ToList();

            var preferredSheet = SelectPreferredSingleSheet(sheets);
            if (!string.IsNullOrWhiteSpace(preferredSheet))
            {
                return preferredSheet;
            }

            if (sheets.Count > 1)
            {
                return null;
            }

            if (list.Count == 1)
            {
                return list[0];
            }

            return null;
        }

        private static string SelectPreferredSingleSheet(List<string> sheets)
        {
            if (sheets == null || sheets.Count == 0)
            {
                return null;
            }

            if (sheets.Count == 1)
            {
                return sheets[0];
            }

            var distinctDirs = sheets
                .Select(s => Path.GetDirectoryName(s) ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var distinctBaseNames = sheets
                .Select(s => Path.GetFileNameWithoutExtension(s) ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctDirs != 1 || distinctBaseNames != 1)
            {
                return null;
            }

            return sheets
                .OrderBy(s =>
                {
                    var ext = Path.GetExtension(s)?.ToLowerInvariant();
                    return ext == ".cue" ? 0 : (ext == ".gdi" ? 1 : 2);
                })
                .First();
        }

        public static bool IsMultiGameArchive(string archivePath, IBiosService biosService = null)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return false;
            }

            try
            {
                var extension = Path.GetExtension(archivePath);
                var entryFullNames = new List<string>();

                if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var zipArchive = ZipFile.OpenRead(archivePath);
                    foreach (var entry in zipArchive.Entries)
                    {
                        if (!entry.FullName.EndsWith("/") && !entry.FullName.EndsWith("\\"))
                        {
                            entryFullNames.Add(entry.FullName);
                        }
                    }
                }
                else if (IsArchiveExtension(extension))
                {
                    using var archive = ArchiveFactory.OpenArchive(archivePath);
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory && !string.IsNullOrWhiteSpace(entry.Key))
                        {
                            entryFullNames.Add(entry.Key);
                        }
                    }
                }
                else
                {
                    return false;
                }

                var gameEntries = new List<string>();
                foreach (var fullName in entryFullNames)
                {
                    var name = Path.GetFileName(fullName.Replace('\\', '/'));
                    if (IsJunkEntry(fullName, name))
                    {
                        continue;
                    }

                    if ((biosService != null && biosService.IsKnownBiosFileName(name)) ||
                        RegionHelper.IsBios(name, null, null, fullName))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(name);
                    if (IsArchiveExtension(ext))
                    {
                        return true;
                    }

                    if (MediaFileExtensions.RomExtensions.Contains(ext))
                    {
                        gameEntries.Add(fullName);
                    }
                }

                if (gameEntries.Count <= 1)
                {
                    return false;
                }

                var cueCount = 0;
                string singleCueDir = null;
                var allSameDir = true;
                string firstDir = null;

                foreach (var fullName in gameEntries)
                {
                    var ext = Path.GetExtension(fullName);
                    if (ext.Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".gdi", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".ccd", StringComparison.OrdinalIgnoreCase))
                    {
                        cueCount++;
                    }

                    var dir = Path.GetDirectoryName(fullName) ?? string.Empty;
                    if (firstDir == null)
                    {
                        firstDir = dir;
                    }
                    else if (!string.Equals(firstDir, dir, StringComparison.OrdinalIgnoreCase))
                    {
                        allSameDir = false;
                    }

                    if (cueCount == 1 && singleCueDir == null)
                    {
                        singleCueDir = dir;
                    }
                }

                var isSingleMultiTrack = cueCount == 1 && allSameDir;
                return !isSingleMultiTrack;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsJunkEntry(string fullName, string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName) || string.IsNullOrWhiteSpace(fullName))
            {
                return true;
            }

            if (fullName.EndsWith("/") || fullName.EndsWith("\\"))
            {
                return true;
            }

            return fullName.IndexOf("__MACOSX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entryName.StartsWith(".", StringComparison.Ordinal) ||
                   entryName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                   entryName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
        }

        public static bool SanitizeSingleRomZip(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return false;
            }

            var expectedBaseName = Path.GetFileNameWithoutExtension(archivePath);
            if (string.IsNullOrWhiteSpace(expectedBaseName))
            {
                return false;
            }

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                var nonDirEntries = archive.Entries
                    .Where(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\"))
                    .ToList();

                if (nonDirEntries.Count == 1)
                {
                    var onlyEntry = nonDirEntries[0];
                    var onlyExt = Path.GetExtension(onlyEntry.Name);
                    if (!IsJunkEntry(onlyEntry.FullName, onlyEntry.Name) &&
                        !IsArchiveExtension(onlyExt) &&
                        MediaFileExtensions.RomExtensions.Contains(onlyExt) &&
                        string.Equals(onlyEntry.FullName, expectedBaseName + onlyExt, StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_repack_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                if (!TryUnwrapSingleRomFromArchive(archivePath, tempDir, out var romPath))
                {
                    return false;
                }

                var romExt = Path.GetExtension(romPath);
                var expectedEntryName = expectedBaseName + romExt;
                var tempZipPath = Path.Combine(tempDir, "repacked.zip");

                using (var newArchive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
                {
                    newArchive.CreateEntryFromFile(romPath, expectedEntryName, CompressionLevel.Optimal);
                }

                if (File.Exists(tempZipPath) && new FileInfo(tempZipPath).Length > 0)
                {
                    File.Copy(tempZipPath, archivePath, overwrite: true);
                    return true;
                }

                return false;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public static bool TryUnwrapSingleRomFromArchive(string archivePath, string workingDirectory, out string extractedRomPath, int maxDepth = 5)
        {
            extractedRomPath = null;
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath) || string.IsNullOrWhiteSpace(workingDirectory))
            {
                return false;
            }

            var romFiles = new List<string>();
            ExtractArchiveRecursive(archivePath, workingDirectory, 0, maxDepth, romFiles, preserveRelativePaths: false, includeCompanionTracks: false);

            if (romFiles.Count != 1)
            {
                return false;
            }

            extractedRomPath = romFiles[0];
            return true;
        }

        public static bool TryUnwrapGameFromArchive(
            string archivePath,
            string workingDirectory,
            out string primaryRomPath,
            out List<string> companionTrackPaths,
            int maxDepth = 5)
        {
            primaryRomPath = null;
            companionTrackPaths = new List<string>();

            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath) || string.IsNullOrWhiteSpace(workingDirectory))
            {
                return false;
            }

            var extractedFiles = new List<string>();
            ExtractArchiveRecursive(archivePath, workingDirectory, 0, maxDepth, extractedFiles, preserveRelativePaths: true, includeCompanionTracks: true);

            if (extractedFiles.Count == 0)
            {
                return false;
            }

            var sheets = extractedFiles
                .Where(f => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(f)))
                .ToList();

            var candidateSheet = SelectPreferredSingleSheet(sheets);
            if (!string.IsNullOrWhiteSpace(candidateSheet))
            {
                var sheetDir = Path.GetDirectoryName(candidateSheet) ?? string.Empty;
                var resolvedTracks = DiscSheetHelper.ResolveTrackFiles(candidateSheet);
                var resolvedTrackSet = new HashSet<string>(resolvedTracks.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);

                var hasForeignFiles = extractedFiles.Any(f =>
                    !string.Equals(f, candidateSheet, StringComparison.OrdinalIgnoreCase) &&
                    !resolvedTrackSet.Contains(Path.GetFullPath(f)) &&
                    !string.Equals(Path.GetDirectoryName(f) ?? string.Empty, sheetDir, StringComparison.OrdinalIgnoreCase));

                if (!hasForeignFiles)
                {
                    primaryRomPath = candidateSheet;
                    var trackSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var track in resolvedTracks)
                    {
                        if (!string.Equals(track, primaryRomPath, StringComparison.OrdinalIgnoreCase) && trackSet.Add(track))
                        {
                            companionTrackPaths.Add(track);
                        }
                    }

                    foreach (var file in extractedFiles)
                    {
                        if (!string.Equals(file, primaryRomPath, StringComparison.OrdinalIgnoreCase) &&
                            DiscSheetHelper.IsCompanionTrackExtension(Path.GetExtension(file)) &&
                            trackSet.Add(file))
                        {
                            companionTrackPaths.Add(file);
                        }
                    }

                    return true;
                }

                return false;
            }

            if (sheets.Count > 1)
            {
                return false;
            }

            var romCandidates = extractedFiles
                .Where(f => MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            if (romCandidates.Count == 1)
            {
                primaryRomPath = romCandidates[0];
                return true;
            }

            return false;
        }

        public static bool TryCompressFileToZipInPlace(string sourceFilePath, out string zipFilePath, Logger logger = null)
        {
            zipFilePath = sourceFilePath;
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return false;
            }

            var targetZipPath = Path.ChangeExtension(sourceFilePath, ".zip");
            if (string.Equals(sourceFilePath, targetZipPath, StringComparison.OrdinalIgnoreCase))
            {
                zipFilePath = sourceFilePath;
                return true;
            }

            try
            {
                if (File.Exists(targetZipPath))
                {
                    File.Delete(targetZipPath);
                }

                using (var zipArchive = ZipFile.Open(targetZipPath, ZipArchiveMode.Create))
                {
                    zipArchive.CreateEntryFromFile(sourceFilePath, Path.GetFileName(sourceFilePath), CompressionLevel.Optimal);
                }

                if (File.Exists(targetZipPath))
                {
                    File.Delete(sourceFilePath);
                    zipFilePath = targetZipPath;
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger?.Debug(ex, "Unable to compress ROM '{0}' to zip", sourceFilePath);

                try
                {
                    if (File.Exists(sourceFilePath) && File.Exists(targetZipPath))
                    {
                        File.Delete(targetZipPath);
                    }
                }
                catch
                {
                }
            }

            zipFilePath = sourceFilePath;
            return false;
        }

        private static void ExtractArchiveRecursive(
            string archivePath,
            string targetDir,
            int depth,
            int maxDepth,
            List<string> collectedRomFiles,
            bool preserveRelativePaths = false,
            bool includeCompanionTracks = false)
        {
            if (depth > maxDepth || !File.Exists(archivePath))
            {
                return;
            }

            Directory.CreateDirectory(targetDir);
            var targetDirFull = Path.GetFullPath(targetDir);
            if (!targetDirFull.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                targetDirFull += Path.DirectorySeparatorChar;
            }

            var ext = Path.GetExtension(archivePath);
            var extractedEntries = new List<string>();

            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(archivePath);
                foreach (var entry in archive.Entries)
                {
                    if (IsJunkEntry(entry.FullName, entry.Name) ||
                        (includeCompanionTracks && RegionHelper.IsBios(entry.Name, null, null, entry.FullName)))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entry.Name);
                    var isSupportedContent = IsArchiveExtension(entryExt) ||
                                             MediaFileExtensions.RomExtensions.Contains(entryExt) ||
                                             (includeCompanionTracks && DiscSheetHelper.IsCompanionTrackExtension(entryExt));
                    if (!isSupportedContent)
                    {
                        continue;
                    }

                    var relativeOrName = preserveRelativePaths
                        ? entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar)
                        : entry.Name;

                    var destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativeOrName));
                    if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (File.Exists(destPath))
                    {
                        var destParent = Path.GetDirectoryName(destPath) ?? targetDirFull;
                        destPath = Path.Combine(destParent, Guid.NewGuid().ToString("N") + "_" + entry.Name);
                    }

                    var parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrWhiteSpace(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    entry.ExtractToFile(destPath, overwrite: true);
                    extractedEntries.Add(destPath);
                }
            }
            else if (IsArchiveExtension(ext))
            {
                using var archive = ArchiveFactory.OpenArchive(archivePath);
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    var entryKey = entry.Key.Replace('\\', '/');
                    var entryName = Path.GetFileName(entryKey);
                    if (IsJunkEntry(entryKey, entryName) ||
                        (includeCompanionTracks && RegionHelper.IsBios(entryName, null, null, entryKey)))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entryName);
                    var isSupportedContent = IsArchiveExtension(entryExt) ||
                                             MediaFileExtensions.RomExtensions.Contains(entryExt) ||
                                             (includeCompanionTracks && DiscSheetHelper.IsCompanionTrackExtension(entryExt));
                    if (!isSupportedContent)
                    {
                        continue;
                    }

                    var relativeOrName = preserveRelativePaths
                        ? entryKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar)
                        : entryName;

                    var destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativeOrName));
                    if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (File.Exists(destPath))
                    {
                        var destParent = Path.GetDirectoryName(destPath) ?? targetDirFull;
                        destPath = Path.Combine(destParent, Guid.NewGuid().ToString("N") + "_" + entryName);
                    }

                    var parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrWhiteSpace(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                    extractedEntries.Add(destPath);
                }
            }

            foreach (var extractedPath in extractedEntries)
            {
                var extractedExt = Path.GetExtension(extractedPath);
                if (IsArchiveExtension(extractedExt))
                {
                    if (depth < maxDepth)
                    {
                        var nestedDir = Path.Combine(targetDir, "nested_" + Guid.NewGuid().ToString("N"));
                        ExtractArchiveRecursive(extractedPath, nestedDir, depth + 1, maxDepth, collectedRomFiles, preserveRelativePaths, includeCompanionTracks);
                    }
                }
                else if (MediaFileExtensions.RomExtensions.Contains(extractedExt) ||
                         (includeCompanionTracks && DiscSheetHelper.IsCompanionTrackExtension(extractedExt)))
                {
                    collectedRomFiles.Add(extractedPath);
                }
            }
        }

        public byte[] ModernizeINesHeader(byte[] romBytes)
        {
            if (romBytes.Length > 4 &&
                romBytes[0] == 'N' &&
                romBytes[1] == 'E' &&
                romBytes[2] == 'S' &&
                romBytes[3] == 0x1A)
            {
                // Has iNES header, basic detection successful
                _logger.Debug("Detected iNES header, modernization required.");

                // For demonstration, we simply return it unmodified since actual NES 2.0 modernization requires PRG/CHR ROM chunk parsing.
                return romBytes;
            }

            return romBytes;
        }
    }
}
