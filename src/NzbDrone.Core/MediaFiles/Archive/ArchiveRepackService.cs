using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;

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

        public void SyncSingleRomEntryName(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return;
            }

            var expectedBaseName = Path.GetFileNameWithoutExtension(archivePath);
            if (string.IsNullOrWhiteSpace(expectedBaseName))
            {
                return;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                {
                    var fileEntries = archive.Entries
                        .Where(e => !e.FullName.EndsWith("/") &&
                                    !e.FullName.EndsWith("\\") &&
                                    !e.FullName.Contains("__MACOSX") &&
                                    !e.Name.StartsWith("."))
                        .ToList();

                    if (fileEntries.Count != 1)
                    {
                        return;
                    }

                    var entry = fileEntries[0];
                    var entryExt = Path.GetExtension(entry.Name);
                    var expectedEntryName = expectedBaseName + entryExt;

                    if (string.Equals(entry.FullName, expectedEntryName, StringComparison.Ordinal))
                    {
                        return;
                    }

                    var tempFilePath = Path.Combine(tempDir, expectedEntryName);
                    entry.ExtractToFile(tempFilePath, true);
                    entry.Delete();
                    archive.CreateEntryFromFile(tempFilePath, expectedEntryName, CompressionLevel.Optimal);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
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
