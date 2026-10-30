using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

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
            ".mkv", ".mp4", ".avi", ".wmv", ".mov", ".flv", ".webm", ".ts", ".m2ts", ".srt", ".sub", ".idx"
        };

        private static readonly uint[] Crc32Table = InitializeCrc32Table();

        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IPlatformCatalogService _platformCatalogService;
        private readonly IDatCacheService _datCacheService;
        private readonly Logger _logger;

        public UnmanagedDiskScanner(
            ISeriesService seriesService,
            IEpisodeService episodeService,
            IPlatformCatalogService platformCatalogService,
            IDatCacheService datCacheService,
            Logger logger)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
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

            var validExtensions = GetValidRomExtensions();
            var files = Directory.GetFiles(rootFolder, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (ExcludedNonGameExtensions.Contains(extension))
                {
                    continue;
                }

                if (ArchiveExtensions.Contains(extension))
                {
                    ProcessZipArchive(file, rootFolder, validExtensions, results);
                }
                else if (validExtensions.Count == 0 || validExtensions.Contains(extension))
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
                            var crc32 = entry.Crc32.ToString("x8").ToLowerInvariant();
                            item.Hashes ??= new Dictionary<string, string>();
                            item.Hashes["CRC32"] = crc32;

                            var (series, episode, highConfidence) = ResolvePlatformAndGame(crc32, entry.Name, zipPath, preferredSeries: item.Series);
                            item.Series ??= series;
                            if (episode != null)
                            {
                                item.SeasonNumber ??= episode.SeasonNumber;
                                if (item.Episodes == null || item.Episodes.Count == 0)
                                {
                                    item.Episodes = new List<Episode> { episode };
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
                    item.SeasonNumber ??= processed.SeasonNumber;
                    if ((item.Episodes == null || item.Episodes.Count == 0) && processed.Episodes != null)
                    {
                        item.Episodes = processed.Episodes;
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
                            var crc32 = entry.Crc32.ToString("x8").ToLowerInvariant();
                            localEpisode.RomHashes = new Dictionary<string, string> { ["CRC32"] = crc32 };

                            var (series, episode, highConfidence) = ResolvePlatformAndGame(crc32, entry.Name, zipPath, preferredSeries: localEpisode.Series);
                            localEpisode.Series ??= series;
                            if (episode != null && (localEpisode.Episodes == null || localEpisode.Episodes.Count == 0))
                            {
                                localEpisode.Episodes = new List<Episode> { episode };
                            }

                            if (localEpisode.FileEpisodeInfo != null && episode != null)
                            {
                                localEpisode.FileEpisodeInfo.SeasonNumber ??= episode.SeasonNumber;
                                if (localEpisode.FileEpisodeInfo.EpisodeNumbers == null || localEpisode.FileEpisodeInfo.EpisodeNumbers.Length == 0)
                                {
                                    localEpisode.FileEpisodeInfo.EpisodeNumbers = new[] { episode.EpisodeNumber };
                                }
                            }

                            localEpisode.RomHashVerified = highConfidence;
                        }
                    }

                    return;
                }

                if (!File.Exists(localEpisode.Path))
                {
                    return;
                }

                var processed = ProcessSingleFile(localEpisode.Path, Path.GetDirectoryName(localEpisode.Path), validExtensions, localEpisode.Series);
                if (processed != null)
                {
                    localEpisode.RomHashes = processed.Hashes ?? new Dictionary<string, string>();
                    localEpisode.RomHashVerified = processed.IsHighConfidenceMatch;
                    localEpisode.Series ??= processed.Series;
                    if ((localEpisode.Episodes == null || localEpisode.Episodes.Count == 0) && processed.Episodes != null && processed.Episodes.Count > 0)
                    {
                        localEpisode.Episodes = processed.Episodes;
                    }

                    if (localEpisode.FileEpisodeInfo != null && processed.Episodes != null && processed.Episodes.Count > 0)
                    {
                        var firstEp = processed.Episodes[0];
                        localEpisode.FileEpisodeInfo.SeasonNumber ??= firstEp.SeasonNumber;
                        if (localEpisode.FileEpisodeInfo.EpisodeNumbers == null || localEpisode.FileEpisodeInfo.EpisodeNumbers.Length == 0)
                        {
                            localEpisode.FileEpisodeInfo.EpisodeNumbers = new[] { firstEp.EpisodeNumber };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to verify ROM hash for local episode {0}", localEpisode.Path);
            }
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
                var gameEntries = archive.Entries.Where(e =>
                {
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\"))
                    {
                        return false;
                    }

                    if (e.FullName.Contains("__MACOSX") || e.Name.StartsWith(".") || e.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var ext = Path.GetExtension(e.Name);
                    return !ExcludedNonGameExtensions.Contains(ext) && (validExtensions.Contains(ext) || MediaFileExtensions.RomExtensions.Contains(ext));
                }).ToList();

                if (gameEntries.Count == 0)
                {
                    return;
                }

                var isMultiTrackDisc = gameEntries.Any(e => Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                                                            Path.GetExtension(e.Name).Equals(".gdi", StringComparison.OrdinalIgnoreCase));

                if (gameEntries.Count == 1 || isMultiTrackDisc)
                {
                    var primaryEntry = gameEntries.FirstOrDefault(e => Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                                                                       Path.GetExtension(e.Name).Equals(".gdi", StringComparison.OrdinalIgnoreCase)) ?? gameEntries[0];

                    var crc32 = primaryEntry.Crc32.ToString("x8").ToLowerInvariant();
                    var hashes = new Dictionary<string, string> { ["CRC32"] = crc32 };

                    try
                    {
                        using var entryStream = primaryEntry.Open();
                        ComputeStreamHashes(entryStream, hashes, computeCrc32: false);
                    }
                    catch
                    {
                    }

                    var (series, episode, highConfidence) = ResolvePlatformAndGame(crc32, primaryEntry.Name, filePath, hashes);

                    results.Add(new ManualImportItem
                    {
                        Path = filePath,
                        RelativePath = rootFolder.GetRelativePath(filePath),
                        Name = Path.GetFileNameWithoutExtension(primaryEntry.Name),
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
                    foreach (var entry in gameEntries)
                    {
                        var crc32 = entry.Crc32.ToString("x8").ToLowerInvariant();
                        var hashes = new Dictionary<string, string> { ["CRC32"] = crc32 };

                        var (series, episode, highConfidence) = ResolvePlatformAndGame(crc32, entry.Name, filePath, hashes);

                        results.Add(new ManualImportItem
                        {
                            Path = $"{filePath}#{entry.FullName}",
                            RelativePath = $"{rootFolder.GetRelativePath(filePath)}#{entry.FullName}",
                            Name = Path.GetFileNameWithoutExtension(entry.Name),
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

        private ManualImportItem ProcessSingleFile(string filePath, string rootFolder, List<string> validExtensions, Series preferredSeries = null)
        {
            var fileName = Path.GetFileName(filePath);
            var parsedInfo = RomParser.ParseRomName(fileName);

            var hashes = ComputeHashes(filePath, validExtensions);

            var crc32 = hashes.TryGetValue("CRC32", out var c) ? c : null;
            var (matchedSeries, matchedEpisode, isHighConfidence) = ResolvePlatformAndGame(crc32, fileName, filePath, hashes, preferredSeries);

            return new ManualImportItem
            {
                Path = filePath,
                RelativePath = rootFolder.GetRelativePath(filePath),
                Name = parsedInfo?.GameTitle ?? Path.GetFileNameWithoutExtension(fileName),
                Size = new FileInfo(filePath).Length,
                Series = matchedSeries,
                SeasonNumber = matchedEpisode?.SeasonNumber,
                Episodes = matchedEpisode != null ? new List<Episode> { matchedEpisode } : new List<Episode>(),
                Hashes = hashes,
                IsHighConfidenceMatch = isHighConfidence
            };
        }

        private (Series Series, Episode Episode, bool IsHighConfidence) ResolvePlatformAndGame(
            string crc32,
            string romName,
            string filePath,
            Dictionary<string, string> hashes = null,
            Series preferredSeries = null)
        {
            DatEntry matchedDatEntry = null;
            string platformSlug = null;

            if (_datCacheService != null)
            {
                var sha256 = hashes != null && hashes.TryGetValue("SHA-256", out var s256) ? s256 : null;
                var sha1 = hashes != null && hashes.TryGetValue("SHA-1", out var s1) ? s1 : null;
                var md5 = hashes != null && hashes.TryGetValue("MD5", out var m5) ? m5 : null;

                var datResult = _datCacheService.FindPlatformByHash(crc32, sha1, md5, sha256);
                if (datResult != null)
                {
                    matchedDatEntry = datResult.DatEntry;
                    platformSlug = datResult.PlatformSlug;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(sha256))
                    {
                        matchedDatEntry = _datCacheService.FindBySha256(sha256);
                    }

                    if (matchedDatEntry == null && !string.IsNullOrWhiteSpace(sha1))
                    {
                        matchedDatEntry = _datCacheService.FindBySha1(sha1);
                    }

                    if (matchedDatEntry == null && !string.IsNullOrWhiteSpace(md5))
                    {
                        matchedDatEntry = _datCacheService.FindByMd5(md5);
                    }

                    if (matchedDatEntry == null && !string.IsNullOrWhiteSpace(crc32))
                    {
                        matchedDatEntry = _datCacheService.FindByCrc32(crc32);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(platformSlug))
            {
                platformSlug = PlatformCompatibilityValidator.GetPlatformByExtension(Path.GetExtension(romName));
            }

            if (string.IsNullOrWhiteSpace(platformSlug))
            {
                platformSlug = DetectPlatformFromPathClues(filePath);
            }

            var matchedSeries = preferredSeries ?? FindPlatformBySlugOrTitle(platformSlug, filePath);
            var matchedEpisode = FindMatchingEpisode(matchedSeries, romName, matchedDatEntry);

            if (matchedSeries == null && matchedEpisode != null)
            {
                matchedSeries = _seriesService?.GetAllSeries().FirstOrDefault(s => s.Id == matchedEpisode.SeriesId);
            }

            var isHighConfidence = (matchedSeries != null && matchedEpisode != null) || matchedDatEntry != null;
            return (matchedSeries, matchedEpisode, isHighConfidence);
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
                var matchedByPath = allPlatforms.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.Path) &&
                    (contextualPath.StartsWith(p.Path, StringComparison.OrdinalIgnoreCase) || p.Path.IsParentPath(contextualPath)));

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

        private Episode FindMatchingEpisode(Series series, string romName, DatEntry matchedDatEntry)
        {
            var candidateTitles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            var fileNameNoExt = Path.GetFileNameWithoutExtension(romName);
            if (!string.IsNullOrWhiteSpace(fileNameNoExt))
            {
                candidateTitles.Add(fileNameNoExt);
            }

            var parsedRom = RomParser.ParseRomName(Path.GetFileName(romName));
            if (!string.IsNullOrWhiteSpace(parsedRom?.GameTitle))
            {
                candidateTitles.Add(parsedRom.GameTitle);
            }

            if (!string.IsNullOrWhiteSpace(matchedDatEntry?.GameName))
            {
                candidateTitles.Add(matchedDatEntry.GameName);
                var parsedDatTitle = RomParser.ParseRomName(matchedDatEntry.GameName)?.GameTitle;
                if (!string.IsNullOrWhiteSpace(parsedDatTitle))
                {
                    candidateTitles.Add(parsedDatTitle);
                }
            }

            if (!string.IsNullOrWhiteSpace(matchedDatEntry?.RomName))
            {
                var datRomNoExt = Path.GetFileNameWithoutExtension(matchedDatEntry.RomName);
                if (!string.IsNullOrWhiteSpace(datRomNoExt))
                {
                    candidateTitles.Add(datRomNoExt);
                }

                var parsedDatRom = RomParser.ParseRomName(matchedDatEntry.RomName)?.GameTitle;
                if (!string.IsNullOrWhiteSpace(parsedDatRom))
                {
                    candidateTitles.Add(parsedDatRom);
                }
            }

            var cleanCandidates = candidateTitles
                .Select(c => c.CleanSeriesTitle())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            if (series != null)
            {
                var games = _episodeService?.GetEpisodeBySeries(series.Id) ?? new List<Episode>();
                var matched = games.FirstOrDefault(g => g.Title != null && (
                    candidateTitles.Contains(g.Title) ||
                    cleanCandidates.Contains(g.Title.CleanSeriesTitle())));

                if (matched != null)
                {
                    return matched;
                }

                if (_episodeService != null)
                {
                    foreach (var cand in candidateTitles)
                    {
                        var ep = _episodeService.FindEpisodeByTitle(series.Id, 1, cand);
                        if (ep != null)
                        {
                            return ep;
                        }
                    }

                    var bestTitle = !string.IsNullOrWhiteSpace(parsedRom?.GameTitle)
                        ? parsedRom.GameTitle
                        : (!string.IsNullOrWhiteSpace(matchedDatEntry?.GameName)
                            ? matchedDatEntry.GameName
                            : fileNameNoExt);

                    var seasonNum = parsedRom?.RegionSeasonNumber ?? 1;
                    var nextEpNum = games
                        .Where(e => e.SeasonNumber == seasonNum)
                        .Select(e => e.EpisodeNumber)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    var newEpisode = new Episode
                    {
                        SeriesId = series.Id,
                        SeasonNumber = seasonNum,
                        EpisodeNumber = nextEpNum,
                        AbsoluteEpisodeNumber = nextEpNum,
                        Title = bestTitle,
                        Monitored = true,
                        AirDateUtc = DateTime.UtcNow
                    };

                    _episodeService.InsertMany(new List<Episode> { newEpisode });
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
            var hashes = new Dictionary<string, string>();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (ArchiveExtensions.Contains(extension))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(filePath);
                    var romEntry = archive.Entries.FirstOrDefault(e =>
                        !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                        (validExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()) ||
                         MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()))) ??
                        archive.Entries.FirstOrDefault(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\"));
                    if (romEntry != null)
                    {
                        using (var entryStream = romEntry.Open())
                        {
                            ComputeStreamHashes(entryStream, hashes, computeCrc32: false);
                        }

                        hashes["CRC32"] = romEntry.Crc32.ToString("x8").ToLowerInvariant();
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
                    using var stream = File.OpenRead(filePath);
                    ComputeStreamHashes(stream, hashes, computeCrc32: true);
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
            using var sha256 = SHA256.Create();
            using var sha1 = SHA1.Create();
            using var md5 = MD5.Create();

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
