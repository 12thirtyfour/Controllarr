using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.GameImport.Manual;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using SharpCompress.Archives;

namespace NzbDrone.Core.MediaFiles.LibraryImport
{
    public interface IUnmanagedDiskScanner
    {
        List<ManualImportItem> Scan(string rootFolder);
        void EnrichItem(ManualImportItem item);
        void VerifyAndEnrichLocalEpisode(LocalEpisode localEpisode);
    }

    public class UnmanagedDiskScanner : IUnmanagedDiskScanner
    {
        private static readonly string[] ArchiveExtensions = { ".zip" };
        private static readonly HashSet<string> ExcludedNonGameExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".nfo", ".url", ".xml", ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".db", ".ini",
            ".mkv", ".mp4", ".avi", ".wmv", ".mov", ".flv", ".webm", ".ts", ".m2ts", ".srt", ".sub", ".idx",
            ".exe", ".bat", ".cmd", ".sh", ".html", ".htm", ".pdf", ".doc", ".docx", ".torrent", ".md", ".rtf", ".log"
        };

        private static readonly uint[][] Crc32Tables = InitializeCrc32SlicingBy8Tables();

        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IPlatformCatalogService _platformCatalogService;
        private readonly IDatCacheService _datCacheService;
        private readonly Logger _logger;
        private readonly IBiosService _biosService;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly IDiscCompressionService _discCompressionService;

        public UnmanagedDiskScanner(
            ISeriesService seriesService,
            IEpisodeService episodeService,
            IPlatformCatalogService platformCatalogService,
            IDatCacheService datCacheService,
            Logger logger,
            IBiosService biosService = null,
            IGameCatalogService gameCatalogService = null,
            IDiscCompressionService discCompressionService = null)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _platformCatalogService = platformCatalogService;
            _datCacheService = datCacheService;
            _logger = logger;
            _biosService = biosService;
            _gameCatalogService = gameCatalogService;
            _discCompressionService = discCompressionService;
        }

        public List<ManualImportItem> Scan(string rootFolder)
        {
            var results = new List<ManualImportItem>();

            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                return results;
            }

            var validExtensions = GetValidRomExtensions();

            if (File.Exists(rootFolder))
            {
                var ext = Path.GetExtension(rootFolder).ToLowerInvariant();
                var baseFolder = Path.GetDirectoryName(rootFolder);
                if (ArchiveExtensions.Contains(ext))
                {
                    ProcessZipArchive(rootFolder, baseFolder, validExtensions, results);
                    return results;
                }

                if (validExtensions.Count == 0 || validExtensions.Contains(ext))
                {
                    var item = ProcessSingleFile(rootFolder, baseFolder, validExtensions);
                    if (item != null)
                    {
                        results.Add(item);
                    }

                    return results;
                }

                return results;
            }

            if (!Directory.Exists(rootFolder))
            {
                _logger.Warn("Root folder does not exist: {0}", rootFolder);
                return results;
            }

            var files = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);
            var companionTracks = DiscSheetHelper.FindCompanionTrackFiles(files);

            foreach (var file in files)
            {
                if (companionTracks.Contains(file) || ArchiveRepackService.IsNonRomArchiveJunkFile(file))
                {
                    continue;
                }

                var fileName = Path.GetFileName(file);
                var isBios = (_biosService != null && _biosService.IsKnownBiosFileName(fileName)) ||
                             RegionHelper.IsBios(fileName, null, null, file);
                if (isBios)
                {
                    var biosItem = ProcessBiosFile(file, rootFolder);
                    if (biosItem != null)
                    {
                        results.Add(biosItem);
                        continue;
                    }
                }

                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (ExcludedNonGameExtensions.Contains(extension))
                {
                    continue;
                }

                if (ArchiveExtensions.Contains(extension))
                {
                    ProcessZipArchive(file, rootFolder, validExtensions, results);
                }
                else if (validExtensions.Count == 0 || validExtensions.Contains(extension) || DiscSheetHelper.IsDiscSheetExtension(extension))
                {
                    var item = ProcessSingleFile(file, rootFolder, validExtensions);
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
            }

            return results;
        }

        public void EnrichItem(ManualImportItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            try
            {
                var validExtensions = GetValidRomExtensions();

                if (item.Path.Contains(".zip#"))
                {
                    var parts = item.Path.Split(new[] { ".zip#" }, StringSplitOptions.None);
                    var zipPath = parts[0] + ".zip";
                    var entryName = parts[1];

                    if (File.Exists(zipPath))
                    {
                        using var archive = ZipFile.OpenRead(zipPath);
                        var entry = archive.GetEntry(entryName) ?? archive.Entries.FirstOrDefault(e => e.FullName == entryName || e.Name == Path.GetFileName(entryName));
                        if (entry != null)
                        {
                            var (resolvedName, crc32) = GetInnerRomDetails(entry);
                            item.Hashes ??= new Dictionary<string, string>();
                            item.Hashes["CRC32"] = crc32;

                            var (series, episode, highConfidence, _) = ResolvePlatformAndGame(crc32, resolvedName, zipPath, item.Hashes, preferredSeries: item.Series);
                            item.Series ??= series;
                            if (episode != null)
                            {
                                if (highConfidence || string.IsNullOrWhiteSpace(item.Name))
                                {
                                    item.Name = episode.Title;
                                }

                                if (highConfidence)
                                {
                                    item.SeasonNumber = episode.SeasonNumber;
                                    item.Episodes = new List<Episode> { episode };
                                }
                                else
                                {
                                    item.SeasonNumber ??= episode.SeasonNumber;
                                    if (item.Episodes == null || item.Episodes.Count == 0)
                                    {
                                        item.Episodes = new List<Episode> { episode };
                                    }
                                }
                            }

                            item.IsHighConfidenceMatch = highConfidence;
                        }
                    }

                    return;
                }

                if (!File.Exists(item.Path))
                {
                    return;
                }

                var processed = ProcessSingleFile(item.Path, Path.GetDirectoryName(item.Path), validExtensions, item.Series);
                if (processed != null)
                {
                    item.Hashes = processed.Hashes ?? new Dictionary<string, string>();
                    item.Series ??= processed.Series;
                    if (processed.IsHighConfidenceMatch)
                    {
                        if (!string.IsNullOrWhiteSpace(processed.Name))
                        {
                            item.Name = processed.Name;
                        }

                        if (processed.SeasonNumber.HasValue)
                        {
                            item.SeasonNumber = processed.SeasonNumber;
                        }

                        if (processed.Episodes != null && processed.Episodes.Count > 0)
                        {
                            item.Episodes = processed.Episodes;
                        }
                    }
                    else
                    {
                        item.SeasonNumber ??= processed.SeasonNumber;
                        if ((item.Episodes == null || item.Episodes.Count == 0) && processed.Episodes != null)
                        {
                            item.Episodes = processed.Episodes;
                        }
                    }

                    item.IsHighConfidenceMatch = processed.IsHighConfidenceMatch;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to enrich manual import item hashes for {0}", item.Path);
            }
        }

        public void VerifyAndEnrichLocalEpisode(LocalEpisode localEpisode)
        {
            if (localEpisode == null || string.IsNullOrWhiteSpace(localEpisode.Path))
            {
                return;
            }

            try
            {
                if (localEpisode.Series != null &&
                    !PlatformCompatibilityValidator.IsLocalFileCompatible(localEpisode.Path, localEpisode.Series.TitleSlug, localEpisode.Series.Title))
                {
                    _logger.Debug("Skipping ROM hash calculation for {0}: format not compatible with platform {1}", localEpisode.Path, localEpisode.Series.Title);
                    return;
                }

                var validExtensions = GetValidRomExtensions();

                if (localEpisode.Path.Contains(".zip#"))
                {
                    var parts = localEpisode.Path.Split(new[] { ".zip#" }, StringSplitOptions.None);
                    var zipPath = parts[0] + ".zip";
                    var entryName = parts[1];

                    if (File.Exists(zipPath))
                    {
                        using var archive = ZipFile.OpenRead(zipPath);
                        var entry = archive.GetEntry(entryName) ?? archive.Entries.FirstOrDefault(e => e.FullName == entryName || e.Name == Path.GetFileName(entryName));
                        if (entry != null)
                        {
                            var (resolvedName, crc32) = GetInnerRomDetails(entry);
                            localEpisode.RomHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CRC32"] = crc32 };
                            if (string.IsNullOrWhiteSpace(localEpisode.ReleaseHash) && !string.IsNullOrWhiteSpace(crc32))
                            {
                                localEpisode.ReleaseHash = crc32;
                            }

                            var (series, episode, highConfidence, matchedDatEntry) = ResolvePlatformAndGame(crc32, resolvedName, zipPath, preferredSeries: localEpisode.Series, allowCreateEpisode: false);
                            var isDatVerified = matchedDatEntry != null;
                            localEpisode.Series ??= series;
                            if (isDatVerified && episode != null)
                            {
                                localEpisode.Episodes = new List<Episode> { episode };
                            }

                            if (localEpisode.FileEpisodeInfo != null && isDatVerified && episode != null)
                            {
                                localEpisode.FileEpisodeInfo.SeasonNumber = episode.SeasonNumber;
                                localEpisode.FileEpisodeInfo.EpisodeNumbers = new[] { episode.EpisodeNumber };
                            }

                            localEpisode.RomHashVerified = isDatVerified;
                            if (localEpisode.RomHashVerified && (localEpisode.Quality == null || localEpisode.Quality.Quality == Quality.Unknown))
                            {
                                localEpisode.Quality = new QualityModel(Quality.VerifiedGoodDump);
                            }
                        }
                    }

                    return;
                }

                if (!File.Exists(localEpisode.Path))
                {
                    return;
                }

                var processed = ProcessSingleFile(localEpisode.Path, Path.GetDirectoryName(localEpisode.Path), validExtensions, localEpisode.Series, allowCreateEpisode: false);
                if (processed != null)
                {
                    localEpisode.RomHashes = processed.Hashes ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (string.IsNullOrWhiteSpace(localEpisode.ReleaseHash))
                    {
                        if (localEpisode.RomHashes.TryGetValue("SHA-1", out var s1) && !string.IsNullOrWhiteSpace(s1))
                        {
                            localEpisode.ReleaseHash = s1;
                        }
                        else if (localEpisode.RomHashes.TryGetValue("CRC32", out var c32) && !string.IsNullOrWhiteSpace(c32))
                        {
                            localEpisode.ReleaseHash = c32;
                        }
                    }

                    var isDatVerified = processed.IsDatHashVerified;
                    localEpisode.RomHashVerified = isDatVerified;
                    if (localEpisode.RomHashVerified && (localEpisode.Quality == null || localEpisode.Quality.Quality == Quality.Unknown))
                    {
                        localEpisode.Quality = new QualityModel(Quality.VerifiedGoodDump);
                    }

                    localEpisode.Series ??= processed.Series;
                    if (isDatVerified && processed.Episodes != null && processed.Episodes.Count > 0)
                    {
                        localEpisode.Episodes = processed.Episodes;
                    }

                    if (localEpisode.FileEpisodeInfo != null && isDatVerified && processed.Episodes != null && processed.Episodes.Count > 0)
                    {
                        var firstEp = processed.Episodes[0];
                        localEpisode.FileEpisodeInfo.SeasonNumber = firstEp.SeasonNumber;
                        localEpisode.FileEpisodeInfo.EpisodeNumbers = new[] { firstEp.EpisodeNumber };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to verify ROM hash for local episode {0}", localEpisode.Path);
            }
        }

        private static bool IsSingleMultiTrackDisc(List<ZipArchiveEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            var cueEntries = entries.Where(e => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(e.Name))).ToList();

            if (cueEntries.Count != 1)
            {
                return false;
            }

            var hasNestedArchives = entries.Any(e =>
                Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

            if (hasNestedArchives)
            {
                return false;
            }

            var distinctDirectories = entries
                .Select(e => Path.GetDirectoryName(e.FullName) ?? string.Empty)
                .Distinct()
                .ToList();

            return distinctDirectories.Count <= 1;
        }

        private static (string Name, string Crc32) GetInnerRomDetails(ZipArchiveEntry entry)
        {
            var crc32 = entry.Crc32.ToString("x8").ToLowerInvariant();
            var name = entry.Name;

            if (Path.GetExtension(entry.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var entryStream = entry.Open();
                    using var innerZip = new ZipArchive(entryStream, ZipArchiveMode.Read);
                    var innerRom = innerZip.Entries.FirstOrDefault(e =>
                        !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                        MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)) &&
                        !Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                        !Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                    if (innerRom != null)
                    {
                        return (innerRom.Name, innerRom.Crc32.ToString("x8").ToLowerInvariant());
                    }
                }
                catch
                {
                }
            }

            return (name, crc32);
        }

        private List<string> GetValidRomExtensions()
        {
            var validExtensions = new List<string>();

            var allPlatforms = _platformCatalogService != null ? _platformCatalogService.GetAll() : null;
            if (allPlatforms != null)
            {
                foreach (var platform in allPlatforms)
                {
                    if (platform?.RomFileExtensions != null)
                    {
                        validExtensions.AddRange(platform.RomFileExtensions);
                    }
                }
            }

            if (validExtensions.Count == 0)
            {
                validExtensions.AddRange(MediaFileExtensions.RomExtensions);
            }

            return validExtensions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void ProcessZipArchive(string filePath, string rootFolder, List<string> validExtensions, List<ManualImportItem> results)
        {
            try
            {
                using var archive = ZipFile.OpenRead(filePath);

                var biosEntries = archive.Entries.Where(e =>
                {
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\"))
                    {
                        return false;
                    }

                    if (e.FullName.Contains("__MACOSX") || e.Name.StartsWith(".") || e.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    if (_biosService != null && _biosService.IsKnownBiosFileName(e.Name))
                    {
                        return true;
                    }

                    if (RegionHelper.IsBios(e.Name, null, null, e.FullName))
                    {
                        return true;
                    }

                    var segments = e.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    return segments.Any(s => string.Equals(s, "BIOS", StringComparison.OrdinalIgnoreCase));
                }).ToList();

                var allSeries = _seriesService?.GetAllSeries() ?? new List<Series>();

                foreach (var bEntry in biosEntries)
                {
                    BiosFile biosFile = null;
                    if (_biosService != null)
                    {
                        _biosService.TryGetBiosFile(bEntry.Name, out biosFile);
                    }

                    var matchedSeries = biosFile != null
                        ? allSeries.FirstOrDefault(s =>
                            string.Equals(s.TitleSlug, biosFile.GamePlatform, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.CleanTitle, biosFile.GamePlatform, StringComparison.OrdinalIgnoreCase))
                        : null;

                    if (matchedSeries == null)
                    {
                        var platformSlug = DetectPlatformFromPathClues(filePath) ?? DetectPlatformFromPathClues(bEntry.FullName);
                        matchedSeries = FindPlatformBySlugOrTitle(platformSlug, filePath);
                    }

                    results.Add(new ManualImportItem
                    {
                        Path = $"{filePath}#{bEntry.FullName}",
                        RelativePath = rootFolder.GetRelativePath(filePath) + "#" + bEntry.FullName,
                        Name = $"[BIOS] {biosFile?.Description ?? bEntry.Name}",
                        Size = bEntry.Length,
                        Series = matchedSeries,
                        SeasonNumber = 99,
                        Episodes = new List<Episode>(),
                        Hashes = new Dictionary<string, string>
                        {
                            { "CRC32", bEntry.Crc32.ToString("x8").ToLowerInvariant() },
                            { "MD5", biosFile?.ExpectedMd5 ?? string.Empty },
                            { "SHA-1", biosFile?.ExpectedSha1 ?? string.Empty }
                        },
                        IsHighConfidenceMatch = biosFile != null
                    });
                }

                var gameEntries = archive.Entries.Where(e =>
                {
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\"))
                    {
                        return false;
                    }

                    if (ArchiveRepackService.IsJunkEntry(e.FullName, e.Name) ||
                        ArchiveRepackService.IsNonRomArchiveJunkFile(e.FullName))
                    {
                        return false;
                    }

                    if (biosEntries.Contains(e) || RegionHelper.IsBios(e.Name, null, null, e.FullName))
                    {
                        return false;
                    }

                    var ext = Path.GetExtension(e.Name);
                    return !ExcludedNonGameExtensions.Contains(ext) &&
                           (validExtensions.Contains(ext) ||
                            MediaFileExtensions.RomExtensions.Contains(ext) ||
                            DiscSheetHelper.IsDiscSheetExtension(ext));
                }).ToList();

                if (gameEntries.Count == 0)
                {
                    return;
                }

                var isSingleMultiTrack = IsSingleMultiTrackDisc(gameEntries);

                if (gameEntries.Count == 1 || isSingleMultiTrack)
                {
                    var primaryEntry = gameEntries.FirstOrDefault(e => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(e.Name))) ?? gameEntries[0];

                    var orderedHashEntries = gameEntries
                        .OrderBy(e => GetCandidateHashPriority(e.Name))
                        .ThenBy(e => GetTrackPriorityBoost(e.Name))
                        .ThenByDescending(e => e.Length)
                        .ToList();

                    var selectedEntry = orderedHashEntries.FirstOrDefault(e =>
                    {
                        var (_, candidateCrc) = GetInnerRomDetails(e);
                        return IsMeaningfulDatMatch(_datCacheService?.FindByCrc32(candidateCrc));
                    }) ?? orderedHashEntries[0];

                    var (resolvedName, crc32) = GetInnerRomDetails(selectedEntry);
                    var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["CRC32"] = crc32 };

                    var datMatch = _datCacheService?.FindByCrc32(crc32);
                    if (datMatch != null && (!string.IsNullOrWhiteSpace(datMatch.Sha1) || !string.IsNullOrWhiteSpace(datMatch.Md5)))
                    {
                        if (!string.IsNullOrWhiteSpace(datMatch.Sha1))
                        {
                            hashes["SHA-1"] = datMatch.Sha1.ToLowerInvariant();
                        }

                        if (!string.IsNullOrWhiteSpace(datMatch.Md5))
                        {
                            hashes["MD5"] = datMatch.Md5.ToLowerInvariant();
                        }

                        if (!string.IsNullOrWhiteSpace(datMatch.Sha256))
                        {
                            hashes["SHA-256"] = datMatch.Sha256.ToLowerInvariant();
                        }
                    }
                    else
                    {
                        try
                        {
                            using var entryStream = selectedEntry.Open();
                            ComputeStreamHashes(entryStream, hashes, computeCrc32: false);
                        }
                        catch
                        {
                        }
                    }

                    var (series, episode, highConfidence, matchedDat) = ResolvePlatformAndGame(crc32, isSingleMultiTrack ? primaryEntry.Name : resolvedName, filePath, hashes);
                    var hasDatIdentity = !string.IsNullOrWhiteSpace(matchedDat?.GameName) || !string.IsNullOrWhiteSpace(matchedDat?.RomName);

                    results.Add(new ManualImportItem
                    {
                        Path = filePath,
                        RelativePath = rootFolder.GetRelativePath(filePath),
                        Name = hasDatIdentity && episode != null ? episode.Title : Path.GetFileNameWithoutExtension(primaryEntry.Name),
                        Size = new FileInfo(filePath).Length,
                        Series = series,
                        SeasonNumber = episode?.SeasonNumber,
                        Episodes = episode != null ? new List<Episode> { episode } : new List<Episode>(),
                        Hashes = hashes,
                        IsHighConfidenceMatch = highConfidence
                    });
                }
                else
                {
                    var cueEntries = gameEntries.Where(e => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(e.Name))).ToList();

                    var entriesToProcess = cueEntries.Count > 1 ? cueEntries : gameEntries;

                    foreach (var entry in entriesToProcess)
                    {
                        var (entryName, crc32) = GetInnerRomDetails(entry);
                        var hashes = new Dictionary<string, string> { ["CRC32"] = crc32 };

                        var (series, episode, highConfidence, matchedDat) = ResolvePlatformAndGame(crc32, entryName, filePath, hashes);
                        var hasDatIdentity = !string.IsNullOrWhiteSpace(matchedDat?.GameName) || !string.IsNullOrWhiteSpace(matchedDat?.RomName);

                        results.Add(new ManualImportItem
                        {
                            Path = $"{filePath}#{entry.FullName}",
                            RelativePath = $"{rootFolder.GetRelativePath(filePath)}#{entry.FullName}",
                            Name = hasDatIdentity && episode != null ? episode.Title : Path.GetFileNameWithoutExtension(entryName),
                            Size = entry.Length,
                            Series = series,
                            SeasonNumber = episode?.SeasonNumber,
                            Episodes = episode != null ? new List<Episode> { episode } : new List<Episode>(),
                            Hashes = hashes,
                            IsHighConfidenceMatch = highConfidence
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to inspect archive {filePath}");
            }
        }

        private ManualImportItem ProcessBiosFile(string filePath, string rootFolder)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var fileName = Path.GetFileName(filePath);
            BiosFile biosFile = null;
            if (_biosService != null)
            {
                _biosService.TryGetBiosFile(fileName, out biosFile);
            }

            var allSeries = _seriesService?.GetAllSeries() ?? new List<Series>();
            var matchedSeries = biosFile != null
                ? allSeries.FirstOrDefault(s =>
                    string.Equals(s.TitleSlug, biosFile.GamePlatform, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.CleanTitle, biosFile.GamePlatform, StringComparison.OrdinalIgnoreCase))
                : null;

            if (matchedSeries == null && biosFile != null && !string.IsNullOrWhiteSpace(biosFile.GamePlatform))
            {
                var normalizedBiosPlatform = PlatformCompatibilityValidator.NormalizePlatformSlug(biosFile.GamePlatform, biosFile.GamePlatform);
                matchedSeries = allSeries.FirstOrDefault(s =>
                {
                    var normalizedSeriesSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(s.TitleSlug, s.Title);
                    return string.Equals(normalizedSeriesSlug, normalizedBiosPlatform, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (matchedSeries == null)
            {
                var platformSlug = DetectPlatformFromPathClues(filePath);
                matchedSeries = FindPlatformBySlugOrTitle(platformSlug, filePath);
            }

            var isValid = biosFile != null && _biosService != null && _biosService.VerifyBios(filePath, biosFile.ExpectedMd5, biosFile.ExpectedSha1);

            return new ManualImportItem
            {
                Path = filePath,
                RelativePath = rootFolder.GetRelativePath(filePath),
                Name = $"[BIOS] {biosFile?.Description ?? fileName}",
                Size = new FileInfo(filePath).Length,
                Series = matchedSeries,
                SeasonNumber = 99,
                Episodes = new List<Episode>(),
                Hashes = new Dictionary<string, string>
                {
                    { "MD5", biosFile?.ExpectedMd5 ?? string.Empty },
                    { "SHA-1", biosFile?.ExpectedSha1 ?? string.Empty }
                },
                IsHighConfidenceMatch = isValid
            };
        }

        private ManualImportItem ProcessSingleFile(string filePath, string rootFolder, List<string> validExtensions, Series preferredSeries = null, bool allowCreateEpisode = true)
        {
            var fileName = Path.GetFileName(filePath);
            if (RegionHelper.IsBios(fileName, null, null, filePath))
            {
                return ProcessBiosFile(filePath, rootFolder);
            }

            var hashes = ComputeHashes(filePath, validExtensions);

            var effectiveRomName = fileName;
            if (hashes.TryGetValue("ROM_NAME", out var innerRomName) && !string.IsNullOrWhiteSpace(innerRomName))
            {
                effectiveRomName = innerRomName;
            }

            var parsedInfo = RomParser.ParseRomName(effectiveRomName) ?? RomParser.ParseRomName(fileName);

            var crc32 = hashes.TryGetValue("CRC32", out var c) ? c : null;
            var (matchedSeries, matchedEpisode, isHighConfidence, matchedDatEntry) = ResolvePlatformAndGame(crc32, effectiveRomName, filePath, hashes, preferredSeries, allowCreateEpisode);
            var effectiveSize = DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(filePath))
                ? DiscSheetHelper.GetEffectiveFileSize(filePath)
                : new FileInfo(filePath).Length;

            return new ManualImportItem
            {
                Path = filePath,
                RelativePath = rootFolder.GetRelativePath(filePath),
                Name = matchedEpisode?.Title ?? parsedInfo?.GameTitle ?? Path.GetFileNameWithoutExtension(effectiveRomName),
                Size = effectiveSize,
                Series = matchedSeries,
                SeasonNumber = matchedEpisode?.SeasonNumber,
                Episodes = matchedEpisode != null ? new List<Episode> { matchedEpisode } : new List<Episode>(),
                Hashes = hashes,
                IsHighConfidenceMatch = isHighConfidence,
                IsDatHashVerified = matchedDatEntry != null
            };
        }

        private (Series Series, Episode Episode, bool IsHighConfidence, DatEntry MatchedDatEntry) ResolvePlatformAndGame(
            string crc32,
            string romName,
            string filePath,
            Dictionary<string, string> hashes = null,
            Series preferredSeries = null,
            bool allowCreateEpisode = true)
        {
            DatEntry matchedDatEntry = null;
            string platformSlug = null;

            if (_datCacheService != null)
            {
                var sha256 = hashes != null && hashes.TryGetValue("SHA-256", out var s256) ? s256 : null;
                var sha1 = hashes != null && hashes.TryGetValue("SHA-1", out var s1) ? s1 : null;
                var md5 = hashes != null && hashes.TryGetValue("MD5", out var m5) ? m5 : null;

                if (preferredSeries != null)
                {
                    var candidate = _datCacheService.FindBestMatch(crc32, md5, sha1, sha256, preferredSeries.TitleSlug, preferredSeries.Title);
                    if (IsMeaningfulDatMatch(candidate))
                    {
                        matchedDatEntry = candidate;
                    }
                }

                if (matchedDatEntry == null)
                {
                    var datResult = _datCacheService.FindPlatformByHash(crc32, sha1, md5, sha256);
                    if (datResult != null && IsMeaningfulDatMatch(datResult.DatEntry))
                    {
                        matchedDatEntry = datResult.DatEntry;
                        platformSlug = datResult.PlatformSlug;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(sha256))
                        {
                            var candidate = _datCacheService.FindBySha256(sha256);
                            if (IsMeaningfulDatMatch(candidate))
                            {
                                matchedDatEntry = candidate;
                            }
                        }

                        if (matchedDatEntry == null && !string.IsNullOrWhiteSpace(sha1))
                        {
                            var candidate = _datCacheService.FindBySha1(sha1);
                            if (IsMeaningfulDatMatch(candidate))
                            {
                                matchedDatEntry = candidate;
                            }
                        }

                        if (matchedDatEntry == null && !string.IsNullOrWhiteSpace(md5))
                        {
                            var candidate = _datCacheService.FindByMd5(md5);
                            if (IsMeaningfulDatMatch(candidate))
                            {
                                matchedDatEntry = candidate;
                            }
                        }

                        if (matchedDatEntry == null && !string.IsNullOrWhiteSpace(crc32))
                        {
                            var candidate = _datCacheService.FindByCrc32(crc32);
                            if (IsMeaningfulDatMatch(candidate))
                            {
                                matchedDatEntry = candidate;
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(platformSlug))
            {
                var ext = Path.GetExtension(romName);
                if ((ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".7z", StringComparison.OrdinalIgnoreCase)) &&
                    !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    try
                    {
                        if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            using var archive = ZipFile.OpenRead(filePath);
                            var innerRom = archive.Entries.FirstOrDefault(e =>
                                !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                                MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)) &&
                                !Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                                !Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                            if (innerRom != null)
                            {
                                platformSlug = PlatformCompatibilityValidator.GetPlatformByExtension(Path.GetExtension(innerRom.Name));
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrWhiteSpace(platformSlug))
                {
                    platformSlug = PlatformCompatibilityValidator.GetPlatformByExtension(ext);
                }
            }

            if (string.IsNullOrWhiteSpace(platformSlug))
            {
                platformSlug = DetectPlatformFromPathClues(filePath);
            }

            var matchedSeries = preferredSeries ?? FindPlatformBySlugOrTitle(platformSlug, filePath);
            if (preferredSeries == null && matchedDatEntry == null && matchedSeries != null &&
                !PlatformCompatibilityValidator.IsLocalFileCompatible(filePath, matchedSeries.TitleSlug, matchedSeries.Title))
            {
                matchedSeries = null;
            }

            var matchedEpisode = matchedDatEntry != null
                ? FindMatchingEpisode(matchedSeries, romName, matchedDatEntry, crc32, hashes, allowCreateEpisode)
                : null;

            if (matchedSeries == null && matchedEpisode != null)
            {
                matchedSeries = _seriesService?.GetAllSeries().FirstOrDefault(s => s.Id == matchedEpisode.SeriesId);
            }

            var isHighConfidence = matchedDatEntry != null;
            return (matchedSeries, matchedEpisode, isHighConfidence, matchedDatEntry);
        }

        private string DetectPlatformFromPathClues(string filePath)
        {
            var normalized = filePath.Replace('\\', '/');
            var segments = normalized.Split('/');

            for (var i = segments.Length - 1; i >= 0; i--)
            {
                var segment = segments[i].Trim();
                if (string.IsNullOrWhiteSpace(segment) || PlatformCompatibilityValidator.IsRegionOrBucketFolder(segment))
                {
                    continue;
                }

                var clean = Path.GetFileNameWithoutExtension(segment);
                var slug = PlatformCompatibilityValidator.NormalizePlatformSlug(null, clean);
                if (!string.IsNullOrWhiteSpace(slug) && PlatformCompatibilityValidator.CanonicalPlatformNames.ContainsKey(slug))
                {
                    return slug;
                }
            }

            return null;
        }

        private Series FindPlatformBySlugOrTitle(string platformSlug, string contextualPath = null)
        {
            var allPlatforms = _seriesService?.GetAllSeries() ?? new List<Series>();
            if (allPlatforms.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(contextualPath))
            {
                var matchedByPath = allPlatforms
                    .Where(p => !string.IsNullOrWhiteSpace(p.Path) &&
                                (p.Path.PathEquals(contextualPath) || p.Path.IsParentPath(contextualPath)))
                    .OrderByDescending(p => p.Path.Length)
                    .FirstOrDefault();

                if (matchedByPath != null)
                {
                    return matchedByPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(platformSlug))
            {
                var matched = allPlatforms.FirstOrDefault(p =>
                {
                    var pSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(p.CleanTitle, p.Title);
                    return string.Equals(pSlug, platformSlug, StringComparison.OrdinalIgnoreCase);
                });

                if (matched != null)
                {
                    return matched;
                }
            }

            if (!string.IsNullOrWhiteSpace(contextualPath))
            {
                var contextualSlug = DetectPlatformFromPathClues(contextualPath);
                if (!string.IsNullOrWhiteSpace(contextualSlug))
                {
                    var matched = allPlatforms.FirstOrDefault(p =>
                    {
                        var pSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(p.CleanTitle, p.Title);
                        return string.Equals(pSlug, contextualSlug, StringComparison.OrdinalIgnoreCase);
                    });

                    if (matched != null)
                    {
                        return matched;
                    }
                }
            }

            return null;
        }

        private Episode FindMatchingEpisode(
            Series series,
            string romName,
            DatEntry matchedDatEntry,
            string crc32 = null,
            Dictionary<string, string> hashes = null,
            bool allowCreateEpisode = true)
        {
            if (matchedDatEntry == null)
            {
                return null;
            }

            var candidateTitles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            var fileNameNoExt = Path.GetFileNameWithoutExtension(romName);
            if (!string.IsNullOrWhiteSpace(fileNameNoExt))
            {
                candidateTitles.Add(fileNameNoExt);
                var spaceNormalized = fileNameNoExt.Replace('_', ' ');
                if (!string.IsNullOrWhiteSpace(spaceNormalized))
                {
                    candidateTitles.Add(spaceNormalized);
                }
            }

            var rawFileName = Path.GetFileName(romName);
            var parsedRom = RomParser.ParseRomName(rawFileName) ?? RomParser.ParseRomName(rawFileName.Replace('_', ' '));
            if (!string.IsNullOrWhiteSpace(parsedRom?.GameTitle))
            {
                candidateTitles.Add(parsedRom.GameTitle);
                var parsedSpaceNormalized = parsedRom.GameTitle.Replace('_', ' ');
                if (!string.IsNullOrWhiteSpace(parsedSpaceNormalized))
                {
                    candidateTitles.Add(parsedSpaceNormalized);
                }

                var normalizedArticle = RomParser.NormalizeTitleArticle(parsedSpaceNormalized);
                if (!string.IsNullOrWhiteSpace(normalizedArticle))
                {
                    candidateTitles.Add(normalizedArticle);
                }
            }

            if (!string.IsNullOrWhiteSpace(matchedDatEntry?.GameName))
            {
                candidateTitles.Add(matchedDatEntry.GameName);
                var normalizedDatArticle = RomParser.NormalizeTitleArticle(matchedDatEntry.GameName);
                if (!string.IsNullOrWhiteSpace(normalizedDatArticle))
                {
                    candidateTitles.Add(normalizedDatArticle);
                }

                var parsedDatTitle = RomParser.ParseRomName(matchedDatEntry.GameName)?.GameTitle;
                if (!string.IsNullOrWhiteSpace(parsedDatTitle))
                {
                    candidateTitles.Add(parsedDatTitle);
                    var normalizedParsedDat = RomParser.NormalizeTitleArticle(parsedDatTitle);
                    if (!string.IsNullOrWhiteSpace(normalizedParsedDat))
                    {
                        candidateTitles.Add(normalizedParsedDat);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(matchedDatEntry?.RomName))
            {
                var datRomNoExt = Path.GetFileNameWithoutExtension(matchedDatEntry.RomName);
                if (!string.IsNullOrWhiteSpace(datRomNoExt))
                {
                    candidateTitles.Add(datRomNoExt);
                    var normalizedRomArticle = RomParser.NormalizeTitleArticle(datRomNoExt);
                    if (!string.IsNullOrWhiteSpace(normalizedRomArticle))
                    {
                        candidateTitles.Add(normalizedRomArticle);
                    }
                }

                var parsedDatRom = RomParser.ParseRomName(matchedDatEntry.RomName)?.GameTitle;
                if (!string.IsNullOrWhiteSpace(parsedDatRom))
                {
                    candidateTitles.Add(parsedDatRom);
                    var normalizedParsedRom = RomParser.NormalizeTitleArticle(parsedDatRom);
                    if (!string.IsNullOrWhiteSpace(normalizedParsedRom))
                    {
                        candidateTitles.Add(normalizedParsedRom);
                    }
                }
            }

            var cleanCandidates = candidateTitles
                .Select(c => c.CleanSeriesTitle())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            if (series != null)
            {
                var games = _episodeService?.GetEpisodeBySeries(series.Id) ?? new List<Episode>();

                if (_gameCatalogService != null)
                {
                    var lookupSha256 = (hashes != null && hashes.TryGetValue("SHA-256", out var h256) ? h256 : null) ?? matchedDatEntry?.Sha256;
                    var lookupSha1 = (hashes != null && hashes.TryGetValue("SHA-1", out var h1) ? h1 : null) ?? matchedDatEntry?.Sha1;
                    var lookupMd5 = (hashes != null && hashes.TryGetValue("MD5", out var h5) ? h5 : null) ?? matchedDatEntry?.Md5;
                    var lookupCrc = !string.IsNullOrWhiteSpace(crc32) ? crc32 : matchedDatEntry?.Crc32;

                    if (_gameCatalogService.TryFindGameByHash(series.TvdbId, lookupSha256, lookupSha1, lookupMd5, lookupCrc, out var catalogMatch) &&
                        catalogMatch != null)
                    {
                        var hashMatchedEpisode = games.FirstOrDefault(g => g.TvdbId > 0 && g.TvdbId == catalogMatch.TvdbId);
                        if (hashMatchedEpisode != null)
                        {
                            return hashMatchedEpisode;
                        }
                    }
                }

                var datRawName = !string.IsNullOrWhiteSpace(matchedDatEntry?.GameName)
                    ? matchedDatEntry.GameName
                    : (!string.IsNullOrWhiteSpace(matchedDatEntry?.RomName)
                        ? Path.GetFileNameWithoutExtension(matchedDatEntry.RomName)
                        : null);
                var parsedDat = !string.IsNullOrWhiteSpace(datRawName)
                    ? RomParser.ParseRomName(datRawName)
                    : null;
                var datTitle = !string.IsNullOrWhiteSpace(parsedDat?.GameTitle)
                    ? RomParser.NormalizeTitleArticle(parsedDat.GameTitle)
                    : (!string.IsNullOrWhiteSpace(datRawName)
                        ? RomParser.NormalizeTitleArticle(datRawName)
                        : null);

                var bestTitle = !string.IsNullOrWhiteSpace(datTitle)
                    ? datTitle
                    : (!string.IsNullOrWhiteSpace(parsedRom?.GameTitle)
                        ? RomParser.NormalizeTitleArticle(parsedRom.GameTitle)
                        : fileNameNoExt);

                var isBios = RegionHelper.IsBios(bestTitle, datRawName ?? romName, parsedDat?.DumpTags ?? parsedRom?.DumpTags);
                int seasonNum;
                bool hasExplicitRegion;
                if (isBios)
                {
                    seasonNum = GameCatalogProxy.RegionBios;
                    hasExplicitRegion = true;
                }
                else if (matchedDatEntry != null && !string.IsNullOrWhiteSpace(datRawName))
                {
                    seasonNum = GameCatalogProxy.ClassifyRegionAndCategory(datRawName, parsedDat);
                    hasExplicitRegion = (parsedDat?.Regions != null && parsedDat.Regions.Count > 0) ||
                                        seasonNum != GameCatalogProxy.RegionUsa;
                }
                else
                {
                    seasonNum = parsedRom?.RegionNumber ?? 1;
                    hasExplicitRegion = parsedRom?.Regions != null && parsedRom.Regions.Count > 0;
                }

                var targetVersion = !string.IsNullOrWhiteSpace(datRawName)
                    ? GameCatalogProxy.ExtractRevisionTag(datRawName, parsedDat, seasonNum)
                    : GameCatalogProxy.ExtractRevisionTag(Path.GetFileName(romName), parsedRom, seasonNum);

                // When a checksum matched a DAT entry, match on authoritative title, region, and version first.
                var regionMatches = games.Where(g => g.Title != null &&
                    g.SeasonNumber == seasonNum &&
                    (candidateTitles.Contains(g.Title) ||
                     cleanCandidates.Contains(g.Title.CleanSeriesTitle()))).ToList();

                var matched = regionMatches.FirstOrDefault(g =>
                    string.Equals(g.FinaleType ?? "Retail (v1.0)", targetVersion, StringComparison.OrdinalIgnoreCase)) ??
                    regionMatches.FirstOrDefault();

                if (matched == null && (!hasExplicitRegion || seasonNum == GameCatalogProxy.RegionWorld))
                {
                    var crossRegionMatches = games.Where(g => g.Title != null && (
                        candidateTitles.Contains(g.Title) ||
                        cleanCandidates.Contains(g.Title.CleanSeriesTitle()))).ToList();

                    matched = crossRegionMatches.FirstOrDefault(g =>
                        string.Equals(g.FinaleType ?? "Retail (v1.0)", targetVersion, StringComparison.OrdinalIgnoreCase)) ??
                        crossRegionMatches.FirstOrDefault();
                }

                if (matched != null)
                {
                    return matched;
                }

                if (_episodeService != null)
                {
                    foreach (var cand in candidateTitles)
                    {
                        var ep = _episodeService.FindEpisodeByTitle(series.Id, seasonNum, cand);
                        if (ep != null)
                        {
                            return ep;
                        }
                    }

                    var fuzzyMatched = games.FirstOrDefault(g => g.Title != null &&
                        g.SeasonNumber == seasonNum &&
                        cleanCandidates.Any(c => !string.IsNullOrWhiteSpace(c) && c.Length >= 4 &&
                            (string.Equals(g.Title.CleanSeriesTitle(), c, StringComparison.OrdinalIgnoreCase) ||
                             (Math.Abs(g.Title.CleanSeriesTitle().Length - c.Length) <= 3 &&
                              (g.Title.CleanSeriesTitle().StartsWith(c) || c.StartsWith(g.Title.CleanSeriesTitle()))))));
                    if (fuzzyMatched != null)
                    {
                        return fuzzyMatched;
                    }

                    if (!allowCreateEpisode)
                    {
                        return null;
                    }

                    var nextEpNum = games
                        .Where(e => e.SeasonNumber == seasonNum)
                        .Select(e => e.EpisodeNumber)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    var deterministicTvdbId = ComputeStablePositiveInt32(series.TvdbId + ":" + seasonNum + ":" + bestTitle + ":" + targetVersion);

                    var newEpisode = new Episode
                    {
                        SeriesId = series.Id,
                        TvdbId = deterministicTvdbId,
                        SeasonNumber = seasonNum,
                        EpisodeNumber = nextEpNum,
                        AbsoluteEpisodeNumber = nextEpNum,
                        Title = bestTitle,
                        FinaleType = targetVersion,
                        Monitored = true,
                        AirDateUtc = DateTime.UtcNow
                    };

                    _episodeService.InsertMany(new List<Episode> { newEpisode });
                    games.Add(newEpisode);
                    return newEpisode;
                }
            }
            else
            {
                var allPlatforms = _seriesService?.GetAllSeries() ?? new List<Series>();
                foreach (var platform in allPlatforms)
                {
                    var games = _episodeService?.GetEpisodeBySeries(platform.Id) ?? new List<Episode>();
                    var matched = games.FirstOrDefault(g => g.Title != null && (
                        candidateTitles.Contains(g.Title) ||
                        cleanCandidates.Contains(g.Title.CleanSeriesTitle())));

                    if (matched != null)
                    {
                        return matched;
                    }
                }
            }

            return null;
        }

        private Dictionary<string, string> ComputeHashes(string filePath, List<string> validExtensions)
        {
            return ComputeFileOrArchiveHashes(filePath, validExtensions, _datCacheService, _logger, _discCompressionService);
        }

        public static Dictionary<string, string> ComputeFileOrArchiveHashes(
            string filePath,
            IEnumerable<string> validExtensions = null,
            IDatCacheService datCacheService = null,
            Logger logger = null,
            IDiscCompressionService discCompressionService = null)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return hashes;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var sfvCrc = TryGetCrcFromSfv(filePath);
            if (!string.IsNullOrWhiteSpace(sfvCrc))
            {
                hashes["SFV_CRC32"] = sfvCrc;
            }

            if (ArchiveRepackService.IsArchiveExtension(extension))
            {
                ComputeArchiveHashes(filePath, extension, validExtensions, datCacheService, logger, hashes);
            }
            else
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 2 * 1024 * 1024, FileOptions.SequentialScan);
                    ComputeStreamHashes(stream, hashes, computeCrc32: true);
                }
                catch (Exception ex)
                {
                    logger?.Error(ex, $"Failed to compute hashes for {filePath}");
                }

                if (DiscSheetHelper.IsDiscSheetExtension(extension))
                {
                    EnrichDiscSheetHashesFromTracks(filePath, datCacheService, logger, hashes);
                }
                else if (extension.Equals(".chd", StringComparison.OrdinalIgnoreCase) &&
                         datCacheService != null &&
                         discCompressionService != null &&
                         !MatchesAnyDatHash(datCacheService, hashes))
                {
                    EnrichChdHashesFromTracks(filePath, datCacheService, discCompressionService, logger, hashes);
                }
            }

            if (!string.IsNullOrWhiteSpace(sfvCrc))
            {
                if (hashes.TryGetValue("CRC32", out var computedCrc))
                {
                    if (string.Equals(computedCrc, sfvCrc, StringComparison.OrdinalIgnoreCase))
                    {
                        logger?.Debug("Checksum matched release SFV for {0}: {1}", filePath, computedCrc);
                    }
                    else
                    {
                        logger?.Warn("Checksum MISMATCH with release SFV for {0}! Computed: {1}, Expected SFV: {2}", filePath, computedCrc, sfvCrc);
                    }
                }
            }

            return hashes;
        }

        private static void ComputeArchiveHashes(
            string filePath,
            string extension,
            IEnumerable<string> validExtensions,
            IDatCacheService datCacheService,
            Logger logger,
            Dictionary<string, string> hashes)
        {
            var validExtensionSet = validExtensions != null
                ? new HashSet<string>(validExtensions, StringComparer.OrdinalIgnoreCase)
                : null;

            try
            {
                if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ComputeZipArchiveHashes(filePath, validExtensionSet, datCacheService, hashes);
                    return;
                }

                ComputeSharpCompressArchiveHashes(filePath, validExtensionSet, datCacheService, hashes);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, $"Failed to inspect archive hashes for {filePath}");
            }
        }

        private static void ComputeZipArchiveHashes(
            string filePath,
            HashSet<string> validExtensionSet,
            IDatCacheService datCacheService,
            Dictionary<string, string> hashes)
        {
            using var archive = ZipFile.OpenRead(filePath);
            var romEntries = archive.Entries
                .Where(e => IsValidArchiveHashCandidate(e.FullName, e.Name, validExtensionSet, allowNestedArchives: false))
                .OrderBy(e => GetCandidateHashPriority(e.Name))
                .ThenBy(e => GetTrackPriorityBoost(e.Name))
                .ThenByDescending(e => e.Length)
                .ToList();

            if (romEntries.Count == 0)
            {
                var nestedZip = archive.Entries.FirstOrDefault(e =>
                    !ArchiveRepackService.IsJunkEntry(e.FullName, e.Name) &&
                    !RegionHelper.IsBios(e.Name, null, null, e.FullName) &&
                    Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase));

                if (nestedZip != null)
                {
                    var (innerName, innerCrc) = GetInnerRomDetails(nestedZip);
                    hashes["CRC32"] = innerCrc;
                    hashes["ROM_NAME"] = innerName;
                    var nestedDat = datCacheService?.FindBestMatch(innerCrc) ?? datCacheService?.FindByCrc32(innerCrc);
                    if (nestedDat != null)
                    {
                        CopyHashesFromDatEntry(nestedDat, hashes);
                    }
                }

                return;
            }

            if (datCacheService != null)
            {
                foreach (var candidate in romEntries)
                {
                    var crc32Hex = candidate.Crc32.ToString("x8").ToLowerInvariant();
                    var datMatch = datCacheService.FindBestMatch(crc32Hex) ?? datCacheService.FindByCrc32(crc32Hex);
                    if (!IsMeaningfulDatMatch(datMatch))
                    {
                        continue;
                    }

                    hashes["CRC32"] = crc32Hex;
                    hashes["ROM_NAME"] = candidate.Name;
                    if (!string.IsNullOrWhiteSpace(datMatch.Sha1) || !string.IsNullOrWhiteSpace(datMatch.Md5) || !string.IsNullOrWhiteSpace(datMatch.Sha256))
                    {
                        CopyHashesFromDatEntry(datMatch, hashes);
                    }
                    else
                    {
                        using var entryStream = candidate.Open();
                        ComputeStreamHashes(entryStream, hashes, computeCrc32: false);
                    }

                    return;
                }
            }

            var primary = romEntries[0];
            hashes["CRC32"] = primary.Crc32.ToString("x8").ToLowerInvariant();
            hashes["ROM_NAME"] = primary.Name;

            var primaryDat = datCacheService?.FindBestMatch(hashes["CRC32"]) ?? datCacheService?.FindByCrc32(hashes["CRC32"]);
            if (primaryDat != null && (!string.IsNullOrWhiteSpace(primaryDat.Sha1) || !string.IsNullOrWhiteSpace(primaryDat.Md5) || !string.IsNullOrWhiteSpace(primaryDat.Sha256)))
            {
                CopyHashesFromDatEntry(primaryDat, hashes);
            }
            else
            {
                using var entryStream = primary.Open();
                ComputeStreamHashes(entryStream, hashes, computeCrc32: false);
            }

            if (datCacheService != null && !MatchesAnyDatHash(datCacheService, hashes) && romEntries.Count > 1)
            {
                var fallbackCandidates = new List<ZipArchiveEntry>();
                fallbackCandidates.AddRange(romEntries.Skip(1).Where(e => !DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(e.Name))).Take(2));
                var sheetFallback = romEntries.Skip(1).FirstOrDefault(e => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(e.Name)));
                if (sheetFallback != null)
                {
                    fallbackCandidates.Add(sheetFallback);
                }

                foreach (var fallback in fallbackCandidates)
                {
                    var candidateHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["CRC32"] = fallback.Crc32.ToString("x8").ToLowerInvariant(),
                        ["ROM_NAME"] = fallback.Name
                    };

                    using (var fallbackStream = fallback.Open())
                    {
                        ComputeStreamHashes(fallbackStream, candidateHashes, computeCrc32: false);
                    }

                    if (MatchesAnyDatHash(datCacheService, candidateHashes))
                    {
                        foreach (var kvp in candidateHashes)
                        {
                            hashes[kvp.Key] = kvp.Value;
                        }

                        return;
                    }
                }
            }
        }

        private static void ComputeSharpCompressArchiveHashes(
            string filePath,
            HashSet<string> validExtensionSet,
            IDatCacheService datCacheService,
            Dictionary<string, string> hashes)
        {
            using var archive = ArchiveFactory.OpenArchive(filePath);
            var romEntries = archive.Entries
                .Where(e => !e.IsDirectory && !string.IsNullOrWhiteSpace(e.Key))
                .Select(e =>
                {
                    var fullKey = e.Key.Replace('\\', '/');
                    return (Entry: e, FullName: fullKey, Name: Path.GetFileName(fullKey));
                })
                .Where(x => IsValidArchiveHashCandidate(x.FullName, x.Name, validExtensionSet, allowNestedArchives: false))
                .OrderBy(x => GetCandidateHashPriority(x.Name))
                .ThenBy(x => GetTrackPriorityBoost(x.Name))
                .ThenByDescending(x => x.Entry.Size)
                .ToList();

            if (romEntries.Count == 0)
            {
                return;
            }

            var primary = romEntries[0];
            hashes["ROM_NAME"] = primary.Name;
            using (var entryStream = primary.Entry.OpenEntryStream())
            {
                ComputeStreamHashes(entryStream, hashes, computeCrc32: true);
            }

            if (datCacheService != null && !MatchesAnyDatHash(datCacheService, hashes) && romEntries.Count > 1)
            {
                var fallbackCandidates = romEntries.Skip(1).Where(x => !DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(x.Name))).Take(2).ToList();
                var sheetFallback = romEntries.Skip(1).FirstOrDefault(x => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(x.Name)));
                if (sheetFallback.Entry != null)
                {
                    fallbackCandidates.Add(sheetFallback);
                }

                foreach (var fallback in fallbackCandidates)
                {
                    var candidateHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ROM_NAME"] = fallback.Name
                    };

                    using (var fallbackStream = fallback.Entry.OpenEntryStream())
                    {
                        ComputeStreamHashes(fallbackStream, candidateHashes, computeCrc32: true);
                    }

                    if (MatchesAnyDatHash(datCacheService, candidateHashes))
                    {
                        foreach (var kvp in candidateHashes)
                        {
                            hashes[kvp.Key] = kvp.Value;
                        }

                        return;
                    }
                }
            }
        }

        private static void EnrichChdHashesFromTracks(
            string chdPath,
            IDatCacheService datCacheService,
            IDiscCompressionService discCompressionService,
            Logger logger,
            Dictionary<string, string> hashes)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr-chd-verify-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (discCompressionService.ExtractChdTracksForVerification(chdPath, tempDir, out var cuePath) &&
                    !string.IsNullOrWhiteSpace(cuePath) &&
                    File.Exists(cuePath))
                {
                    EnrichDiscSheetHashesFromTracks(cuePath, datCacheService, logger, hashes, Path.GetFileName(chdPath));
                }
            }
            catch (Exception ex)
            {
                logger?.Debug(ex, "Unable to verify inner tracks of CHD {0}", chdPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                }
            }
        }

        private static void EnrichDiscSheetHashesFromTracks(
            string sheetPath,
            IDatCacheService datCacheService,
            Logger logger,
            Dictionary<string, string> hashes,
            string originalRomName = null)
        {
            if (datCacheService != null && MatchesAnyDatHash(datCacheService, hashes))
            {
                return;
            }

            var tracks = DiscSheetHelper.ResolveTrackFilesPrioritizingData(sheetPath);
            if (tracks.Count == 0)
            {
                return;
            }

            var maxTracksToCheck = datCacheService == null ? 1 : Math.Min(8, tracks.Count);
            for (var i = 0; i < maxTracksToCheck; i++)
            {
                var trackPath = tracks[i];
                if (!File.Exists(trackPath))
                {
                    continue;
                }

                try
                {
                    var trackHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    using (var stream = new FileStream(trackPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 2 * 1024 * 1024, FileOptions.SequentialScan))
                    {
                        ComputeStreamHashes(stream, trackHashes, computeCrc32: true);
                    }

                    if (datCacheService == null || MatchesAnyDatHash(datCacheService, trackHashes))
                    {
                        foreach (var kvp in trackHashes)
                        {
                            hashes[kvp.Key] = kvp.Value;
                        }

                        hashes["ROM_NAME"] = !string.IsNullOrWhiteSpace(originalRomName)
                            ? originalRomName
                            : Path.GetFileName(sheetPath);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger?.Debug(ex, "Unable to compute track hashes for {0}", trackPath);
                }
            }
        }

        private static bool IsValidArchiveHashCandidate(string fullName, string name, HashSet<string> validExtensionSet, bool allowNestedArchives)
        {
            if (ArchiveRepackService.IsJunkEntry(fullName, name))
            {
                return false;
            }

            var ext = Path.GetExtension(name);
            if (string.IsNullOrWhiteSpace(ext) || ExcludedNonGameExtensions.Contains(ext))
            {
                return false;
            }

            if (!allowNestedArchives && ArchiveRepackService.IsArchiveExtension(ext))
            {
                return false;
            }

            if (RegionHelper.IsBios(name, null, null, fullName))
            {
                return false;
            }

            return (validExtensionSet != null && validExtensionSet.Contains(ext)) ||
                   MediaFileExtensions.RomExtensions.Contains(ext) ||
                   DiscSheetHelper.IsCompanionTrackExtension(ext);
        }

        private static int GetCandidateHashPriority(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            if (DiscSheetHelper.IsDiscSheetExtension(ext))
            {
                return 2;
            }

            if (ArchiveRepackService.IsArchiveExtension(ext))
            {
                return 1;
            }

            return 0;
        }

        private static int GetTrackPriorityBoost(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return 1;
            }

            if (fileName.Contains("(Track 01)", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("(Track 1)", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Track 01", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Track 1", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return 1;
        }

        private static bool IsMeaningfulDatMatch(DatEntry datEntry)
        {
            return datEntry != null &&
                   (!string.IsNullOrWhiteSpace(datEntry.GameName) ||
                    !string.IsNullOrWhiteSpace(datEntry.RomName) ||
                    !string.IsNullOrWhiteSpace(datEntry.Sha1) ||
                    !string.IsNullOrWhiteSpace(datEntry.Md5) ||
                    !string.IsNullOrWhiteSpace(datEntry.Sha256) ||
                    !string.IsNullOrWhiteSpace(datEntry.Crc32));
        }

        private static bool MatchesAnyDatHash(IDatCacheService datCacheService, Dictionary<string, string> hashes)
        {
            if (datCacheService == null || hashes == null || hashes.Count == 0)
            {
                return false;
            }

            hashes.TryGetValue("SHA-256", out var sha256);
            hashes.TryGetValue("SHA-1", out var sha1);
            hashes.TryGetValue("MD5", out var md5);
            hashes.TryGetValue("CRC32", out var crc32);

            return IsMeaningfulDatMatch(datCacheService.FindBestMatch(crc32, md5, sha1, sha256));
        }

        private static void CopyHashesFromDatEntry(DatEntry datMatch, Dictionary<string, string> hashes)
        {
            if (datMatch == null || hashes == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(datMatch.Sha1))
            {
                hashes["SHA-1"] = datMatch.Sha1.ToLowerInvariant();
            }

            if (!string.IsNullOrWhiteSpace(datMatch.Md5))
            {
                hashes["MD5"] = datMatch.Md5.ToLowerInvariant();
            }

            if (!string.IsNullOrWhiteSpace(datMatch.Sha256))
            {
                hashes["SHA-256"] = datMatch.Sha256.ToLowerInvariant();
            }
        }

        private static void ComputeStreamHashes(Stream stream, Dictionary<string, string> hashes, bool computeCrc32)
        {
            using var sha256 = SHA256.Create();
            using var sha1 = SHA1.Create();
            using var md5 = MD5.Create();

            var buffer = ArrayPool<byte>.Shared.Rent(2 * 1024 * 1024);
            try
            {
                var crc = 0xFFFFFFFFu;
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    sha1.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);

                    if (computeCrc32)
                    {
                        var i = 0;
                        while (i + 8 <= bytesRead)
                        {
                            var b0 = buffer[i++] ^ (byte)crc;
                            var b1 = buffer[i++] ^ (byte)(crc >> 8);
                            var b2 = buffer[i++] ^ (byte)(crc >> 16);
                            var b3 = buffer[i++] ^ (byte)(crc >> 24);
                            crc = Crc32Tables[7][b0]
                                ^ Crc32Tables[6][b1]
                                ^ Crc32Tables[5][b2]
                                ^ Crc32Tables[4][b3]
                                ^ Crc32Tables[3][buffer[i++]]
                                ^ Crc32Tables[2][buffer[i++]]
                                ^ Crc32Tables[1][buffer[i++]]
                                ^ Crc32Tables[0][buffer[i++]];
                        }

                        while (i < bytesRead)
                        {
                            crc = (crc >> 8) ^ Crc32Tables[0][(crc ^ buffer[i++]) & 0xFF];
                        }
                    }
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                hashes["SHA-256"] = Convert.ToHexString(sha256.Hash).ToLowerInvariant();
                hashes["SHA-1"] = Convert.ToHexString(sha1.Hash).ToLowerInvariant();
                hashes["MD5"] = Convert.ToHexString(md5.Hash).ToLowerInvariant();

                if (computeCrc32)
                {
                    hashes["CRC32"] = (~crc).ToString("x8").ToLowerInvariant();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static string TryGetCrcFromSfv(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    return null;
                }

                var fileName = Path.GetFileName(filePath);
                var sfvFiles = Directory.GetFiles(directory, "*.sfv");
                foreach (var sfvFile in sfvFiles)
                {
                    var lines = File.ReadAllLines(sfvFile);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        {
                            continue;
                        }

                        var lastSpace = trimmed.LastIndexOfAny(new[] { ' ', '\t' });
                        if (lastSpace > 0)
                        {
                            var filePart = trimmed.Substring(0, lastSpace).Trim();
                            var crcPart = trimmed.Substring(lastSpace + 1).Trim();

                            if (crcPart.Length == 8 &&
                                (string.Equals(filePart, fileName, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(Path.GetFileName(filePart), fileName, StringComparison.OrdinalIgnoreCase)))
                            {
                                return crcPart.ToLowerInvariant();
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static uint[][] InitializeCrc32SlicingBy8Tables()
        {
            var tables = new uint[8][];
            for (var i = 0; i < 8; i++)
            {
                tables[i] = new uint[256];
            }

            for (var i = 0u; i < 256; i++)
            {
                var entry = i;
                for (var j = 0; j < 8; j++)
                {
                    entry = (entry & 1) == 1 ? (entry >> 1) ^ 0xEDB88320u : entry >> 1;
                }

                tables[0][i] = entry;
            }

            for (var i = 0; i < 256; i++)
            {
                for (var j = 1; j < 8; j++)
                {
                    tables[j][i] = (tables[j - 1][i] >> 8) ^ tables[0][tables[j - 1][i] & 0xFF];
                }
            }

            return tables;
        }

        public static int ComputeStablePositiveInt32(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(bytes, hash);

            var value = ((hash[0] & 0x7F) << 24) |
                        (hash[1] << 16) |
                        (hash[2] << 8) |
                        hash[3];

            return value == 0 ? 1 : value;
        }
    }
}
