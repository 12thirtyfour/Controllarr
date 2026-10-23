using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NzbDrone.Core.Dat;
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
        private static readonly string[] ArchiveExtensions = { ".zip" };
        private static readonly string[] RomExtensions = { ".smc", ".sfc", ".nes", ".md", ".bin", ".cue", ".iso", ".chd" };
        private static readonly uint[] Crc32Table = InitializeCrc32Table();

        private readonly IGamePlatformRepository _gamePlatformRepository;
        private readonly IGameTitleRepository _gameTitleRepository;
        private readonly IPlatformCatalogService _platformCatalogService;
        private readonly IDatCacheService _datCacheService;
        private readonly Logger _logger;

        public UnmanagedDiskScanner(
            IGamePlatformRepository gamePlatformRepository,
            IGameTitleRepository gameTitleRepository,
            IPlatformCatalogService platformCatalogService,
            IDatCacheService datCacheService,
            Logger logger)
        {
            _gamePlatformRepository = gamePlatformRepository;
            _gameTitleRepository = gameTitleRepository;
            _platformCatalogService = platformCatalogService;
            _datCacheService = datCacheService;
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

            var allPlatforms = _platformCatalogService != null ? _platformCatalogService.GetAll() : null;
            if (allPlatforms == null)
            {
                allPlatforms = new List<PlatformDefinition>();
            }

            var validExtensions = new List<string>();
            foreach (var platform in allPlatforms)
            {
                if (platform?.RomFileExtensions != null)
                {
                    validExtensions.AddRange(platform.RomFileExtensions);
                }
            }

            if (validExtensions.Count == 0)
            {
                validExtensions.AddRange(RomExtensions);
            }

            validExtensions = validExtensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var files = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (validExtensions.Count == 0 || validExtensions.Contains(extension) || ArchiveExtensions.Contains(extension))
                {
                    var item = ProcessFile(file, validExtensions);
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        private ManualImportItem ProcessFile(string filePath, List<string> validExtensions)
        {
            var fileName = Path.GetFileName(filePath);
            var parsedInfo = RomParser.ParseRomName(fileName);

            if (parsedInfo == null || string.IsNullOrWhiteSpace(parsedInfo.GameTitle))
            {
                return null;
            }

            var hashes = ComputeHashes(filePath, validExtensions);

            GamePlatform matchedPlatform = null;
            GameTitle matchedTitle = null;
            var isHighConfidence = false;

            var allPlatforms = _gamePlatformRepository.All();
            var allTitles = _gameTitleRepository.All();

            matchedTitle = allTitles.FirstOrDefault(t =>
                t.Title.Equals(parsedInfo.GameTitle, StringComparison.InvariantCultureIgnoreCase) ||
                (t.CleanTitle != null && t.CleanTitle.Equals(parsedInfo.GameTitle, StringComparison.InvariantCultureIgnoreCase)));

            if (matchedTitle != null)
            {
                matchedPlatform = allPlatforms.FirstOrDefault(p => p.Id == matchedTitle.PlatformId);
                isHighConfidence = true;
            }

            if (_datCacheService != null)
            {
                if (hashes.TryGetValue("SHA-256", out var sha256) && _datCacheService.FindBySha256(sha256) != null)
                {
                    isHighConfidence = true;
                }
                else if (hashes.TryGetValue("SHA-1", out var sha1) && _datCacheService.FindBySha1(sha1) != null)
                {
                    isHighConfidence = true;
                }
                else if (hashes.TryGetValue("MD5", out var md5) && _datCacheService.FindByMd5(md5) != null)
                {
                    isHighConfidence = true;
                }
                else if (hashes.TryGetValue("CRC32", out var crc) && _datCacheService.FindByCrc32(crc) != null)
                {
                    isHighConfidence = true;
                }
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

        private Dictionary<string, string> ComputeHashes(string filePath, List<string> validExtensions)
        {
            var hashes = new Dictionary<string, string>();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (ArchiveExtensions.Contains(extension))
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(filePath))
                    {
                        var romEntry = archive.Entries.FirstOrDefault(e => validExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()));
                        if (romEntry != null)
                        {
                            using (var entryStream = romEntry.Open())
                            {
                                ComputeStreamHashes(entryStream, hashes, computeCrc32: false);
                            }

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
                try
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        ComputeStreamHashes(stream, hashes, computeCrc32: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to compute hashes for {filePath}");
                }
            }

            return hashes;
        }

        private static void ComputeStreamHashes(Stream stream, Dictionary<string, string> hashes, bool computeCrc32)
        {
            using (var sha256 = SHA256.Create())
            using (var sha1 = SHA1.Create())
            using (var md5 = MD5.Create())
            {
                var buffer = new byte[8192];
                var crc = 0xFFFFFFFFu;
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    sha1.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);

                    if (computeCrc32)
                    {
                        for (var i = 0; i < bytesRead; i++)
                        {
                            crc = (crc >> 8) ^ Crc32Table[(crc ^ buffer[i]) & 0xFF];
                        }
                    }
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                hashes["SHA-256"] = BitConverter.ToString(sha256.Hash).Replace("-", "").ToLowerInvariant();
                hashes["SHA-1"] = BitConverter.ToString(sha1.Hash).Replace("-", "").ToLowerInvariant();
                hashes["MD5"] = BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();

                if (computeCrc32)
                {
                    hashes["CRC32"] = (~crc).ToString("x8").ToLowerInvariant();
                }
            }
        }

        private static uint[] InitializeCrc32Table()
        {
            var table = new uint[256];
            for (var i = 0u; i < 256; i++)
            {
                var entry = i;
                for (var j = 0; j < 8; j++)
                {
                    entry = (entry & 1) == 1 ? (entry >> 1) ^ 0xEDB88320u : entry >> 1;
                }

                table[i] = entry;
            }

            return table;
        }
    }
}
