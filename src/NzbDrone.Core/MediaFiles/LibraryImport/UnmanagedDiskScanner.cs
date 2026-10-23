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
        private readonly IGamePlatformRepository _gamePlatformRepository;
        private readonly IGameTitleRepository _gameTitleRepository;
        private readonly Logger _logger;

        private static readonly string[] ArchiveExtensions = { ".zip" };
        private static readonly string[] RomExtensions = { ".smc", ".sfc", ".nes", ".md", ".bin", ".cue", ".iso", ".chd" };

        private readonly IPlatformCatalogService _platformCatalogService;
        private readonly IDatCacheService _datCacheService;

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
                allPlatforms = new System.Collections.Generic.List<NzbDrone.Core.Games.PlatformDefinition>();
            }

            var validExtensions = new System.Collections.Generic.List<string>();
            foreach (var platform in allPlatforms)
            {
                if (platform != null && platform.RomFileExtensions != null)
                {
                    validExtensions.AddRange(platform.RomFileExtensions);
                }
            }

            if (validExtensions.Count == 0)
            {
                validExtensions.AddRange(RomExtensions);
            }

            if (validExtensions.Count == 0)
            {
                validExtensions.AddRange(RomExtensions);
            }

            validExtensions = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Distinct(validExtensions));

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

        private ManualImportItem ProcessFile(string filePath, System.Collections.Generic.List<string> validExtensions)
        {
            var fileName = Path.GetFileName(filePath);
            var parsedInfo = RomParser.ParseRomName(fileName);

            if (parsedInfo == null || string.IsNullOrWhiteSpace(parsedInfo.GameTitle))
            {
                return null;
            }

            var hashes = ComputeHashes(filePath, validExtensions);

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

            if (_datCacheService != null)
            {
                if (hashes.TryGetValue("CRC32", out var crc) && _datCacheService.FindByCrc32(crc) != null)
                {
                    isHighConfidence = true;
                }
                else if (hashes.TryGetValue("MD5", out var md5) && _datCacheService.FindByMd5(md5) != null)
                {
                    isHighConfidence = true;
                }
                else if (hashes.TryGetValue("SHA-1", out var sha1) && _datCacheService.FindBySha1(sha1) != null)
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

        private Dictionary<string, string> ComputeHashes(string filePath, System.Collections.Generic.List<string> validExtensions)
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
                        var romEntry = archive.Entries.FirstOrDefault(e => validExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()));
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
