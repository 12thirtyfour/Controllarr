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
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update))
                {
                    var entries = archive.Entries.ToList();
                    foreach (var entry in entries)
                    {
                        if (entry.Name.EndsWith(".smc", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var tempFilePath = Path.Combine(tempDir, entry.Name);
                            entry.ExtractToFile(tempFilePath);

                            var bytes = File.ReadAllBytes(tempFilePath);

                            // Detect 512-byte header
                            if (bytes.Length % 1024 == 512)
                            {
                                _logger.Debug("Stripping 512-byte copier header from {0}", entry.Name);
                                var newBytes = new byte[bytes.Length - 512];
                                Array.Copy(bytes, 512, newBytes, 0, newBytes.Length);

                                var newExtensionPath = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(entry.Name) + ".sfc");
                                File.WriteAllBytes(newExtensionPath, newBytes);

                                entry.Delete();
                                archive.CreateEntryFromFile(newExtensionPath, Path.GetFileName(newExtensionPath), CompressionLevel.Optimal); // Enforces Deflate
                            }
                        }
                    }
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
