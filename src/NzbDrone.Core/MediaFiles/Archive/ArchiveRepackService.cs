using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Games;
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
            ExtractArchiveRecursive(archivePath, workingDirectory, 0, maxDepth, romFiles);

            if (romFiles.Count != 1)
            {
                return false;
            }

            extractedRomPath = romFiles[0];
            return true;
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

        private static void ExtractArchiveRecursive(string archivePath, string targetDir, int depth, int maxDepth, List<string> collectedRomFiles)
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
                    if (IsJunkEntry(entry.FullName, entry.Name))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entry.Name);
                    if (!IsArchiveExtension(entryExt) && !MediaFileExtensions.RomExtensions.Contains(entryExt))
                    {
                        continue;
                    }

                    var destPath = Path.GetFullPath(Path.Combine(targetDirFull, entry.Name));
                    if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (File.Exists(destPath))
                    {
                        destPath = Path.Combine(targetDirFull, Guid.NewGuid().ToString("N") + "_" + entry.Name);
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
                    if (IsJunkEntry(entryKey, entryName))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entryName);
                    if (!IsArchiveExtension(entryExt) && !MediaFileExtensions.RomExtensions.Contains(entryExt))
                    {
                        continue;
                    }

                    var destPath = Path.GetFullPath(Path.Combine(targetDirFull, entryName));
                    if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (File.Exists(destPath))
                    {
                        destPath = Path.Combine(targetDirFull, Guid.NewGuid().ToString("N") + "_" + entryName);
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
                        ExtractArchiveRecursive(extractedPath, nestedDir, depth + 1, maxDepth, collectedRomFiles);
                    }
                }
                else if (MediaFileExtensions.RomExtensions.Contains(extractedExt))
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
