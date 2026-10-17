using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MediaFiles.LibraryImport
{
    public interface IUnmanagedDiskScanner
    {
        List<ManualImportItem> Scan(string rootFolder);
    }

    public class UnmanagedDiskScanner : IUnmanagedDiskScanner
    {
        private readonly IGamePlatformRepository _gamePlatformRepository;
        private readonly IGameTitleRepository _gameTitleRepository;
        private readonly Logger _logger;

        private static readonly string[] ArchiveExtensions = { ".zip" };
        private static readonly string[] RomExtensions = { ".smc", ".sfc", ".nes", ".md", ".bin", ".cue", ".iso", ".chd" };

        public UnmanagedDiskScanner(
            IGamePlatformRepository gamePlatformRepository,
            IGameTitleRepository gameTitleRepository,
            Logger logger)
        {
            _gamePlatformRepository = gamePlatformRepository;
            _gameTitleRepository = gameTitleRepository;
            _logger = logger;
        }

        public List<ManualImportItem> Scan(string rootFolder)
        {
            var results = new List<ManualImportItem>();

            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                _logger.Warn("Root folder does not exist: {0}", rootFolder);
                return results;
            }

            var files = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (RomExtensions.Contains(extension) || ArchiveExtensions.Contains(extension))
                {
                    var item = ProcessFile(file);
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        private ManualImportItem ProcessFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var parsedInfo = RomParser.ParseRomName(fileName);

            if (parsedInfo == null || string.IsNullOrWhiteSpace(parsedInfo.GameTitle))
            {
                return null;
            }

            var hashes = ComputeHashes(filePath);

            // Match against catalog
            GamePlatform matchedPlatform = null;
            GameTitle matchedTitle = null;
            var isHighConfidence = false;

            var allPlatforms = _gamePlatformRepository.All();
            var allTitles = _gameTitleRepository.All();

            matchedTitle = allTitles.FirstOrDefault(t => t.Title.Equals(parsedInfo.GameTitle, StringComparison.InvariantCultureIgnoreCase) ||
                                                         t.CleanTitle.Equals(parsedInfo.GameTitle, StringComparison.InvariantCultureIgnoreCase));

            if (matchedTitle != null)
            {
                matchedPlatform = allPlatforms.FirstOrDefault(p => p.Id == matchedTitle.PlatformId);
                isHighConfidence = true; // High confidence if exact title match
            }

            return new ManualImportItem
            {
                Path = filePath,
                Name = fileName,
                Size = new FileInfo(filePath).Length,
                GamePlatform = matchedPlatform,
                GameTitle = matchedTitle,
                Hashes = hashes,
                IsHighConfidenceMatch = isHighConfidence
            };
        }

        private Dictionary<string, string> ComputeHashes(string filePath)
        {
            var hashes = new Dictionary<string, string>();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (ArchiveExtensions.Contains(extension))
            {
                // Inspect zip central directory for hashes without extracting
                try
                {
                    using (var archive = ZipFile.OpenRead(filePath))
                    {
                        var romEntry = archive.Entries.FirstOrDefault(e => RomExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()));
                        if (romEntry != null)
                        {
                            using (var entryStream = romEntry.Open())
                            using (var sha1 = SHA1.Create())
                            using (var md5 = MD5.Create())
                            {
                                var buffer = new byte[8192];
                                int bytesRead;
                                while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    sha1.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                                }

                                sha1.TransformFinalBlock(new byte[0], 0, 0);
                                md5.TransformFinalBlock(new byte[0], 0, 0);

                                hashes["SHA-1"] = BitConverter.ToString(sha1.Hash).Replace("-", "").ToLowerInvariant();
                                hashes["MD5"] = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                            }

                            // CRC32 directly from central directory
                            var crc32Property = romEntry.GetType().GetProperty("Crc32", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                            if (crc32Property != null)
                            {
                                var crc32Value = (uint)crc32Property.GetValue(romEntry);
                                hashes["CRC32"] = crc32Value.ToString("x8").ToLowerInvariant();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to inspect archive hashes for {filePath}");
                }
            }
            else
            {
                // Compute hashes for regular file
                try
                {
                    using (var stream = File.OpenRead(filePath))
                    using (var sha1 = SHA1.Create())
                    using (var md5 = MD5.Create())
                    {
                        var buffer = new byte[8192];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sha1.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                            md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                        }

                        sha1.TransformFinalBlock(new byte[0], 0, 0);
                        md5.TransformFinalBlock(new byte[0], 0, 0);

                        hashes["SHA-1"] = BitConverter.ToString(sha1.Hash).Replace("-", "").ToLowerInvariant();
                        hashes["MD5"] = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to compute hashes for {filePath}");
                }
            }

            return hashes;
        }
    }
}
