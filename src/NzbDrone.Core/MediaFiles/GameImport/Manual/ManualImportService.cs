using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Bios;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.GameImport.Aggregation;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.GameImport.Manual
{
    public interface IManualImportService
    {
        List<ManualImportItem> GetMediaFiles(int seriesId, int? seasonNumber);
        List<ManualImportItem> GetMediaFiles(string path, string downloadId, int? seriesId, bool filterExistingFiles);
        ManualImportItem ReprocessItem(string path, string downloadId, int seriesId, int? seasonNumber, List<int> episodeIds, string releaseGroup, QualityModel quality, List<Language> languages, int indexerFlags, ReleaseType releaseType);
    }

    public class ManualImportService : IExecute<ManualImportCommand>, IManualImportService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IParsingService _parsingService;
        private readonly IDiskScanService _diskScanService;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IImportApprovedGames _importApprovedGames;
        private readonly IAggregationService _aggregationService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IDownloadedGamesImportService _downloadedGamesImportService;
        private readonly IMediaFileService _mediaFileService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly ILocalGameCustomFormatCalculationService _localGameFormatCalculator;
        private readonly IEventAggregator _eventAggregator;
        private readonly IUnmanagedDiskScanner _unmanagedDiskScanner;
        private readonly IBiosService _biosService;
        private readonly Logger _logger;

        public ManualImportService(IDiskProvider diskProvider,
                                   IParsingService parsingService,
                                   IDiskScanService diskScanService,
                                   IMakeImportDecision importDecisionMaker,
                                   ISeriesService seriesService,
                                   IEpisodeService episodeService,
                                   IAggregationService aggregationService,
                                   IImportApprovedGames importApprovedGames,
                                   ITrackedDownloadService trackedDownloadService,
                                   IDownloadedGamesImportService downloadedGamesImportService,
                                   IMediaFileService mediaFileService,
                                   ILocalGameCustomFormatCalculationService localGameFormatCalculator,
                                   ICustomFormatCalculationService formatCalculator,
                                   IEventAggregator eventAggregator,
                                   Logger logger,
                                   IUnmanagedDiskScanner unmanagedDiskScanner,
                                   IBiosService biosService = null)
        {
            _diskProvider = diskProvider;
            _parsingService = parsingService;
            _diskScanService = diskScanService;
            _importDecisionMaker = importDecisionMaker;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _aggregationService = aggregationService;
            _importApprovedGames = importApprovedGames;
            _trackedDownloadService = trackedDownloadService;
            _downloadedGamesImportService = downloadedGamesImportService;
            _mediaFileService = mediaFileService;
            _localGameFormatCalculator = localGameFormatCalculator;
            _formatCalculator = formatCalculator;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _unmanagedDiskScanner = unmanagedDiskScanner;
            _biosService = biosService;
        }

        public List<ManualImportItem> GetMediaFiles(int seriesId, int? seasonNumber)
        {
            var platform = _seriesService.GetSeries(seriesId);
            var directoryInfo = new DirectoryInfo(platform.Path);
            var platformFiles = seasonNumber.HasValue ? _mediaFileService.GetFilesBySeason(seriesId, seasonNumber.Value) : _mediaFileService.GetFilesBySeries(seriesId);
            var games = _episodeService.GetEpisodeBySeries(platform.Id);

            var items = platformFiles.Select(romFile => MapItem(romFile, platform, directoryInfo.Name, games)).ToList();

            if (!seasonNumber.HasValue && directoryInfo.Exists)
            {
                var mediaFiles = _diskScanService.FilterPaths(platform.Path, _diskScanService.GetVideoFiles(platform.Path)).ToList();
                var unmappedFiles = MediaFileService.FilterExistingFiles(mediaFiles, platformFiles, platform);

                items.AddRange(unmappedFiles.Select(file =>
                {
                    var item = new ManualImportItem
                    {
                        Path = Path.Combine(platform.Path, file),
                        FolderName = directoryInfo.Name,
                        RelativePath = platform.Path.GetRelativePath(file),
                        Name = Path.GetFileNameWithoutExtension(file),
                        Series = platform,
                        SeasonNumber = null,
                        Episodes = new List<Episode>(),
                        ReleaseGroup = string.Empty,
                        Quality = new QualityModel(Quality.Unknown),
                        Languages = new List<Language> { Language.Unknown },
                        Size = _diskProvider.GetFileSize(file),
                        Rejections = Enumerable.Empty<ImportRejection>()
                    };
                    _unmanagedDiskScanner?.EnrichItem(item);
                    return item;
                }));
            }

            return items;
        }

        public List<ManualImportItem> GetMediaFiles(string path, string downloadId, int? seriesId, bool filterExistingFiles)
        {
            if (downloadId.IsNotNullOrWhiteSpace())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);

                if (trackedDownload?.ImportItem == null)
                {
                    return new List<ManualImportItem>();
                }

                path = trackedDownload.ImportItem.OutputPath.FullPath;
            }

            if (!_diskProvider.FolderExists(path))
            {
                if (!_diskProvider.FileExists(path))
                {
                    return new List<ManualImportItem>();
                }

                var ext = Path.GetExtension(path);
                if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    if (_unmanagedDiskScanner != null)
                    {
                        var scanned = _unmanagedDiskScanner.Scan(path);
                        if (scanned != null && scanned.Count > 0)
                        {
                            if (seriesId.HasValue)
                            {
                                var targetPlatform = _seriesService.GetSeries(seriesId.Value);
                                foreach (var item in scanned)
                                {
                                    if (item.Series == null)
                                    {
                                        item.Series = targetPlatform;
                                        _unmanagedDiskScanner.EnrichItem(item);
                                    }
                                }

                                scanned = scanned.Where(i => i.Series?.Id == seriesId.Value).ToList();
                            }

                            if (downloadId.IsNotNullOrWhiteSpace())
                            {
                                scanned.ForEach(i => i.DownloadId = downloadId);
                            }

                            return scanned;
                        }
                    }
                }

                var rootFolder = Path.GetDirectoryName(path);
                return new List<ManualImportItem> { ProcessFile(rootFolder, rootFolder, path, downloadId) };
            }

            if (_unmanagedDiskScanner != null)
            {
                var scannedItems = _unmanagedDiskScanner.Scan(path);
                if (scannedItems != null && scannedItems.Count > 0)
                {
                    if (seriesId.HasValue)
                    {
                        var targetPlatform = _seriesService.GetSeries(seriesId.Value);
                        foreach (var item in scannedItems)
                        {
                            if (item.Series == null)
                            {
                                item.Series = targetPlatform;
                                _unmanagedDiskScanner.EnrichItem(item);
                            }
                        }

                        scannedItems = scannedItems.Where(i => i.Series?.Id == seriesId.Value).ToList();
                    }

                    if (downloadId.IsNotNullOrWhiteSpace())
                    {
                        scannedItems.ForEach(i => i.DownloadId = downloadId);
                    }

                    if (filterExistingFiles)
                    {
                        scannedItems = scannedItems.Where(item =>
                        {
                            if (item.Series == null || item.Episodes == null || item.Episodes.Count == 0)
                            {
                                return true;
                            }

                            var existingFiles = _mediaFileService.GetFilesBySeries(item.Series.Id);
                            return !existingFiles.Any(f => item.Episodes.Any(e => e.EpisodeFileId == f.Id));
                        }).ToList();
                    }

                    return scannedItems;
                }
            }

            return ProcessFolder(path, path, downloadId, seriesId, filterExistingFiles);
        }

        public ManualImportItem ReprocessItem(string path, string downloadId, int seriesId, int? seasonNumber, List<int> episodeIds, string releaseGroup, QualityModel quality, List<Language> languages, int indexerFlags, ReleaseType releaseType)
        {
            var rootFolder = Path.GetDirectoryName(path);
            var platform = _seriesService.GetSeries(seriesId);

            var languageParse = LanguageParser.ParseLanguages(path);

            if (languageParse.Count <= 1 && languageParse.First() == Language.Unknown && platform != null)
            {
                languageParse = new List<Language> { platform.OriginalLanguage };
                _logger.Debug("Language couldn't be parsed from release, falling back to platform original language: {0}", platform.OriginalLanguage.Name);
            }

            if (episodeIds.Any())
            {
                var downloadClientItem = GetTrackedDownload(downloadId)?.DownloadItem;
                var games = _episodeService.GetEpisodes(episodeIds);
                var finalReleaseGroup = releaseGroup.IsNullOrWhiteSpace()
                    ? Parser.ReleaseGroupParser.ParseReleaseGroup(path)
                    : releaseGroup;
                var finalQuality = quality.Quality == Quality.Unknown ? QualityParser.ParseQuality(path) : quality;
                var finalLanguages =
                    languages?.Count <= 1 && (languages?.SingleOrDefault() ?? Language.Unknown) == Language.Unknown
                        ? languageParse
                        : languages;

                var localGame = new LocalEpisode();
                localGame.Series = platform;
                localGame.Episodes = games;
                localGame.FileEpisodeInfo = Parser.Parser.ParsePath(path);
                localGame.DownloadClientEpisodeInfo = downloadClientItem == null ? null : Parser.Parser.ParseTitle(downloadClientItem.Title);
                localGame.DownloadItem = downloadClientItem;
                localGame.Path = path;
                localGame.SceneSource = SceneSource(platform, rootFolder);
                localGame.ExistingFile = platform.Path.IsParentPath(path);
                localGame.Size = _diskProvider.GetFileSize(path);
                localGame.ReleaseGroup = finalReleaseGroup;
                localGame.Languages = finalLanguages;
                localGame.Quality = finalQuality;
                localGame.IndexerFlags = (IndexerFlags)indexerFlags;
                localGame.ReleaseType = releaseType;

                _localGameFormatCalculator.UpdateEpisodeCustomFormats(localGame);

                // Augment game file so imported files have all additional information an automatic import would
                localGame = _aggregationService.Augment(localGame, downloadClientItem);

                // Reapply the user-chosen values.
                localGame.Series = platform;
                localGame.Episodes = games;
                localGame.ReleaseGroup = finalReleaseGroup;
                localGame.Quality = finalQuality;
                localGame.Languages = finalLanguages;
                localGame.IndexerFlags = (IndexerFlags)indexerFlags;
                localGame.ReleaseType = releaseType;

                return MapItem(_importDecisionMaker.GetDecision(localGame, downloadClientItem), rootFolder, downloadId, null);
            }

            if (seasonNumber.HasValue)
            {
                var downloadClientItem = GetTrackedDownload(downloadId)?.DownloadItem;

                var localGame = new LocalEpisode
                {
                    Series = platform,
                    Episodes = new List<Episode>(),
                    FileEpisodeInfo = Parser.Parser.ParsePath(path),
                    DownloadClientEpisodeInfo = downloadClientItem == null
                        ? null
                        : Parser.Parser.ParseTitle(downloadClientItem.Title),
                    DownloadItem = downloadClientItem,
                    Path = path,
                    SceneSource = SceneSource(platform, rootFolder),
                    ExistingFile = platform.Path.IsParentPath(path),
                    Size = _diskProvider.GetFileSize(path),
                    ReleaseGroup = releaseGroup.IsNullOrWhiteSpace() ? Parser.ReleaseGroupParser.ParseReleaseGroup(path) : releaseGroup,
                    Languages = languages?.Count <= 1 && (languages?.SingleOrDefault() ?? Language.Unknown) == Language.Unknown ? LanguageParser.ParseLanguages(path) : languages,
                    Quality = quality.Quality == Quality.Unknown ? QualityParser.ParseQuality(path) : quality,
                    IndexerFlags = (IndexerFlags)indexerFlags,
                    ReleaseType = releaseType
                };

                return MapItem(new ImportDecision(localGame, new ImportRejection(ImportRejectionReason.NoEpisodes, "Games not selected")), rootFolder, downloadId, null);
            }

            return ProcessFile(rootFolder, rootFolder, path, downloadId, platform);
        }

        private List<ManualImportItem> ProcessFolder(string rootFolder, string baseFolder, string downloadId, int? seriesId, bool filterExistingFiles)
        {
            DownloadClientItem downloadClientItem = null;
            Series platform = null;

            var directoryInfo = new DirectoryInfo(baseFolder);

            if (seriesId.HasValue)
            {
                platform = _seriesService.GetSeries(seriesId.Value);
            }
            else
            {
                try
                {
                    platform = _parsingService.GetSeries(directoryInfo.Name);
                }
                catch (MultipleSeriesFoundException e)
                {
                    _logger.Warn(e, "Unable to find platform from title");
                }
            }

            if (downloadId.IsNotNullOrWhiteSpace())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);
                downloadClientItem = trackedDownload.DownloadItem;

                if (platform == null)
                {
                    platform = trackedDownload.RemoteEpisode?.Series;
                }
            }

            if (platform == null)
            {
                var files = _diskScanService.FilterPaths(rootFolder, _diskScanService.GetVideoFiles(baseFolder, false));

                if (files.Count > 100)
                {
                    _logger.Warn("Unable to determine platform from folder name and found more than 100 files. Skipping parsing");
                    return ProcessDownloadDirectory(rootFolder, files);
                }

                var subfolders = _diskScanService.FilterPaths(rootFolder, _diskProvider.GetDirectories(baseFolder));

                var processedFiles = files.Select(file => ProcessFile(rootFolder, baseFolder, file, downloadId));
                var processedFolders = subfolders.SelectMany(subfolder => ProcessFolder(rootFolder, subfolder, downloadId, null, filterExistingFiles));

                return processedFiles.Concat(processedFolders).Where(i => i != null).ToList();
            }

            var downloadClientItemInfo = downloadClientItem == null ? null : Parser.Parser.ParseTitle(downloadClientItem.Title);
            var folderInfo = Parser.Parser.ParseTitle(directoryInfo.Name);
            var platformFiles = _diskScanService.FilterPaths(rootFolder, _diskScanService.GetVideoFiles(baseFolder).ToList());
            var decisions = _importDecisionMaker.GetImportDecisions(platformFiles, platform, downloadClientItem, downloadClientItemInfo, folderInfo, SceneSource(platform, baseFolder), filterExistingFiles);

            return decisions.Select(decision => MapItem(decision, rootFolder, downloadId, directoryInfo.Name)).ToList();
        }

        private ManualImportItem ProcessFile(string rootFolder, string baseFolder, string file, string downloadId, Series platform = null)
        {
            try
            {
                var trackedDownload = GetTrackedDownload(downloadId);
                var relativeFile = baseFolder.GetRelativePath(file);

                if (platform == null)
                {
                    platform = _parsingService.GetSeries(relativeFile.Split('\\', '/')[0]);
                }

                if (platform == null)
                {
                    platform = _parsingService.GetSeries(relativeFile);
                }

                if (trackedDownload != null && platform == null)
                {
                    platform = trackedDownload?.RemoteEpisode?.Series;
                }

                if (platform == null)
                {
                    var relativeParseInfo = Parser.Parser.ParsePath(relativeFile);

                    if (relativeParseInfo != null)
                    {
                        platform = _seriesService.FindByTitle(relativeParseInfo.SeriesTitle);
                    }
                }

                if (platform == null)
                {
                    var ext = Path.GetExtension(file);
                    var platformSlug = PlatformCompatibilityValidator.GetPlatformByExtension(ext);
                    if (!string.IsNullOrWhiteSpace(platformSlug))
                    {
                        platform = _seriesService.GetAllSeries().FirstOrDefault(s =>
                            string.Equals(PlatformCompatibilityValidator.NormalizePlatformSlug(s.CleanTitle, s.Title), platformSlug, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (platform == null)
                {
                    var localGame = new LocalEpisode();
                    localGame.Path = file;
                    localGame.ReleaseGroup = Parser.ReleaseGroupParser.ParseReleaseGroup(file);
                    localGame.Quality = QualityParser.ParseQuality(file);
                    localGame.Languages = LanguageParser.ParseLanguages(file);
                    localGame.Size = _diskProvider.GetFileSize(file);

                    return MapItem(new ImportDecision(localGame,
                        new ImportRejection(ImportRejectionReason.UnknownSeries, "Unknown Platform")),
                        rootFolder,
                        downloadId,
                        null);
                }

                var downloadClientItemInfo = trackedDownload?.DownloadItem == null ? null : Parser.Parser.ParseTitle(trackedDownload.DownloadItem.Title);

                var importDecisions = _importDecisionMaker.GetImportDecisions(new List<string> { file },
                    platform,
                    trackedDownload?.DownloadItem,
                    downloadClientItemInfo,
                    null,
                    SceneSource(platform, baseFolder));

                if (importDecisions.Any())
                {
                    return MapItem(importDecisions.First(), rootFolder, downloadId, null);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to process file: {0}", file);
            }

            var fallbackItem = new ManualImportItem
            {
                DownloadId = downloadId,
                Path = file,
                RelativePath = rootFolder.GetRelativePath(file),
                Name = Path.GetFileNameWithoutExtension(file),
                Size = _diskProvider.GetFileSize(file),
                Rejections = new List<ImportRejection>()
            };
            _unmanagedDiskScanner?.EnrichItem(fallbackItem);
            return fallbackItem;
        }

        private List<ManualImportItem> ProcessDownloadDirectory(string rootFolder, List<string> romFiles)
        {
            var items = new List<ManualImportItem>();

            foreach (var file in romFiles)
            {
                var localGame = new LocalEpisode();
                localGame.Path = file;
                localGame.Quality = new QualityModel(Quality.Unknown);
                localGame.Languages = new List<Language> { Language.Unknown };
                localGame.Size = _diskProvider.GetFileSize(file);

                items.Add(MapItem(new ImportDecision(localGame), rootFolder, null, null));
            }

            return items;
        }

        private bool SceneSource(Series platform, string folder)
        {
            return !(platform.Path.PathEquals(folder) || platform.Path.IsParentPath(folder));
        }

        private TrackedDownload GetTrackedDownload(string downloadId)
        {
            if (downloadId.IsNotNullOrWhiteSpace())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);

                return trackedDownload;
            }

            return null;
        }

        private ManualImportItem MapItem(ImportDecision decision, string rootFolder, string downloadId, string folderName)
        {
            var item = new ManualImportItem();

            item.Path = decision.LocalEpisode.Path;
            item.FolderName = folderName;
            item.RelativePath = rootFolder.GetRelativePath(decision.LocalEpisode.Path);
            item.Name = Path.GetFileNameWithoutExtension(decision.LocalEpisode.Path);
            item.DownloadId = downloadId;

            if (decision.LocalEpisode.Episodes.Any() && decision.LocalEpisode.Episodes.Select(c => c.SeasonNumber).Distinct().Count() == 1)
            {
                var regions = decision.LocalEpisode.Episodes.Select(c => c.SeasonNumber).Distinct().ToList();

                if (regions.Empty())
                {
                    _logger.Warn("Expected one region, but found none for: {0}", decision.LocalEpisode.Path);
                }
                else if (regions.Count > 1)
                {
                    _logger.Warn("Expected one region, but found {0} ({1}) for: {2}", regions.Count, string.Join(", ", regions), decision.LocalEpisode.Path);
                }
                else
                {
                    item.SeasonNumber = decision.LocalEpisode.SeasonNumber;
                    item.Episodes = decision.LocalEpisode.Episodes;
                }
            }

            item.ReleaseGroup = decision.LocalEpisode.ReleaseGroup;
            item.Quality = decision.LocalEpisode.Quality;
            item.Languages = decision.LocalEpisode.Languages;
            item.Size = _diskProvider.GetFileSize(decision.LocalEpisode.Path);
            item.Rejections = decision.Rejections;
            item.IndexerFlags = (int)decision.LocalEpisode.IndexerFlags;
            item.ReleaseType = decision.LocalEpisode.ReleaseType;

            if (decision.LocalEpisode.Series != null)
            {
                item.Series = decision.LocalEpisode.Series;

                if (item.SeasonNumber.HasValue)
                {
                    item.CustomFormats = _localGameFormatCalculator.ParseEpisodeCustomFormats(decision.LocalEpisode);
                    item.CustomFormatScore = item.Series.QualityProfile?.Value.CalculateCustomFormatScore(item.CustomFormats) ?? 0;
                }
            }

            _unmanagedDiskScanner?.EnrichItem(item);

            return item;
        }

        private ManualImportItem MapItem(EpisodeFile romFile, Series platform, string folderName, List<Episode> games)
        {
            var item = new ManualImportItem();

            item.Path = Path.Combine(platform.Path, romFile.RelativePath);
            item.FolderName = folderName;
            item.RelativePath = romFile.RelativePath;
            item.Name = Path.GetFileNameWithoutExtension(romFile.Path);
            item.Series = platform;
            item.SeasonNumber = romFile.SeasonNumber;
            item.Episodes = games.Where(e => e.EpisodeFileId == romFile.Id).ToList();
            item.ReleaseGroup = romFile.ReleaseGroup;
            item.Quality = romFile.Quality;
            item.Languages = romFile.Languages;
            item.IndexerFlags = (int)romFile.IndexerFlags;
            item.ReleaseType = romFile.ReleaseType;
            item.Size = _diskProvider.GetFileSize(item.Path);
            item.Rejections = Enumerable.Empty<ImportRejection>();
            item.EpisodeFileId = romFile.Id;
            item.CustomFormats = _formatCalculator.ParseCustomFormat(romFile, platform);

            _unmanagedDiskScanner?.EnrichItem(item);

            return item;
        }

        public void Execute(ManualImportCommand message)
        {
            _logger.ProgressTrace("Manually importing {0} files using mode {1}", message.Files.Count, message.ImportMode);

            var imported = new List<ImportResult>();
            var importedTrackedDownload = new List<ManuallyImportedFile>();
            var importedUntrackedDownload = new List<ImportResult>();

            for (var i = 0; i < message.Files.Count; i++)
            {
                _logger.ProgressTrace("Processing file {0} of {1}", i + 1, message.Files.Count);

                var file = message.Files[i];
                var platform = _seriesService.GetSeries(file.SeriesId);
                if (platform != null)
                {
                    platform.SeasonFolder = true;
                }

                var games = _episodeService.GetEpisodes(file.GameIds);
                var fileGameInfo = Parser.Parser.ParsePath(file.Path) ?? new ParsedEpisodeInfo();
                var existingFile = platform != null && platform.Path.IsParentPath(file.Path);
                if (existingFile && platform != null)
                {
                    var relativePath = platform.Path.GetRelativePath(file.Path);
                    var relativeDir = Path.GetDirectoryName(relativePath);
                    var needsRegionFolder = string.IsNullOrWhiteSpace(relativeDir);
                    var needsZipCompression = RomFileMovingService.IsPlatformCompressionEnabled(platform) &&
                                              RomFileMovingService.IsZipCompressibleRom(Path.GetExtension(file.Path), platform, file.Path);

                    if (needsRegionFolder || needsZipCompression)
                    {
                        existingFile = false;
                    }
                }

                var actualPath = file.Path;
                string tempExtractDir = null;

                if (file.Path.Contains(".zip#"))
                {
                    var parts = file.Path.Split(new[] { ".zip#" }, StringSplitOptions.None);
                    var zipPath = parts[0] + ".zip";
                    var entryName = parts[1];

                    if (File.Exists(zipPath))
                    {
                        tempExtractDir = Path.Combine(Path.GetTempPath(), "ControllarrImport", Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tempExtractDir);
                        actualPath = Path.Combine(tempExtractDir, Path.GetFileName(entryName));

                        using var archive = ZipFile.OpenRead(zipPath);
                        var entry = archive.GetEntry(entryName) ?? archive.Entries.FirstOrDefault(e => e.FullName == entryName || e.Name == Path.GetFileName(entryName));
                        if (entry != null)
                        {
                            entry.ExtractToFile(actualPath, true);

                            if (Path.GetExtension(actualPath).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                                !RomFileMovingService.IsPlatformCompressionEnabled(platform))
                            {
                                try
                                {
                                    using var innerArchive = ZipFile.OpenRead(actualPath);
                                    var innerEntry = innerArchive.Entries.FirstOrDefault(e =>
                                        !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                                        MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name)) &&
                                        !Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                                        !Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                                    if (innerEntry != null)
                                    {
                                        var innerDest = Path.Combine(tempExtractDir, innerEntry.Name);
                                        innerEntry.ExtractToFile(innerDest, true);
                                        actualPath = innerDest;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.Debug(ex, "Failed to extract nested inner ROM archive '{0}'", actualPath);
                                }
                            }
                        }
                    }

                    existingFile = false;
                }

                if (_biosService != null && platform != null &&
                    _biosService.IsKnownBiosFileName(Path.GetFileName(actualPath)))
                {
                    var isBios = _biosService.ProcessCandidateBiosFile(actualPath, platform, move: message.ImportMode == ImportMode.Move);
                    if (isBios)
                    {
                        var biosDest = Path.Combine(platform.Path, "BIOS", Path.GetFileName(actualPath));
                        var biosResult = new ImportResult(new ImportDecision(new LocalEpisode
                        {
                            Path = biosDest,
                            Series = platform
                        }));
                        imported.Add(biosResult);
                        importedUntrackedDownload.Add(biosResult);

                        if (tempExtractDir != null && Directory.Exists(tempExtractDir))
                        {
                            try
                            {
                                Directory.Delete(tempExtractDir, true);
                            }
                            catch
                            {
                            }
                        }

                        continue;
                    }
                }

                TrackedDownload trackedDownload = null;

                var localGame = new LocalEpisode
                {
                    ExistingFile = existingFile,
                    Episodes = games,
                    FileEpisodeInfo = fileGameInfo,
                    Path = actualPath,
                    ReleaseGroup = file.ReleaseGroup,
                    Quality = file.Quality,
                    Languages = file.Languages,
                    IndexerFlags = (IndexerFlags)file.IndexerFlags,
                    ReleaseType = file.ReleaseType,
                    Series = platform,
                    Size = 0
                };

                if (file.DownloadId.IsNotNullOrWhiteSpace())
                {
                    trackedDownload = _trackedDownloadService.Find(file.DownloadId);
                    localGame.DownloadClientEpisodeInfo = trackedDownload?.RemoteEpisode?.ParsedEpisodeInfo;
                    localGame.DownloadItem = trackedDownload?.DownloadItem;
                }

                if (file.FolderName.IsNotNullOrWhiteSpace())
                {
                    localGame.FolderEpisodeInfo = Parser.Parser.ParseTitle(file.FolderName);
                    localGame.SceneSource = !existingFile;
                }

                // Augment game file so imported files have all additional information an automatic import would
                localGame = _aggregationService.Augment(localGame, trackedDownload?.DownloadItem);

                // Apply the user-chosen values.
                localGame.Series = platform;
                localGame.Episodes = games;
                localGame.ReleaseGroup = file.ReleaseGroup;
                localGame.Quality = file.Quality;
                localGame.IndexerFlags = (IndexerFlags)file.IndexerFlags;
                localGame.ReleaseType = file.ReleaseType;

                if (file.Languages is { Count: > 0 } && file.Languages.All(l => l is not null && l.IsValid()))
                {
                    localGame.Languages = file.Languages;
                }

                _localGameFormatCalculator.UpdateEpisodeCustomFormats(localGame);

                // TODO: Cleanup non-tracked downloads

                var importDecision = new ImportDecision(localGame);

                if (trackedDownload == null)
                {
                    var importResult = _importApprovedGames.Import(new List<ImportDecision> { importDecision }, !existingFile, null, message.ImportMode);

                    imported.AddRange(importResult);
                    importedUntrackedDownload.AddRange(importResult);
                }
                else
                {
                    var importResult = _importApprovedGames.Import(new List<ImportDecision> { importDecision }, true, trackedDownload.DownloadItem, message.ImportMode).First();

                    imported.Add(importResult);

                    importedTrackedDownload.Add(new ManuallyImportedFile
                    {
                        TrackedDownload = trackedDownload,
                        ImportResult = importResult
                    });
                }

                if (tempExtractDir != null && Directory.Exists(tempExtractDir))
                {
                    try
                    {
                        Directory.Delete(tempExtractDir, true);
                    }
                    catch
                    {
                    }
                }
            }

            if (imported.Any())
            {
                _logger.ProgressTrace("Manually imported {0} files", imported.Count);
            }

            var untrackedImports = importedUntrackedDownload.Where(i => i.Result == ImportResultType.Imported).ToList();

            if (untrackedImports.Any())
            {
                foreach (var groupedUntrackedImport in untrackedImports.GroupBy(i => new { i.EpisodeFile.SeriesId, i.EpisodeFile.SeasonNumber }))
                {
                    var localGames = groupedUntrackedImport.Select(u => u.ImportDecision.LocalEpisode).ToList();
                    var romFiles = groupedUntrackedImport.Select(u => u.EpisodeFile).ToList();
                    var localGame = localGames.First();
                    var platform = localGame.Series;
                    var sourcePath = localGames.Select(l => l.Path).ToList().GetLongestCommonPath();
                    var games = localGames.SelectMany(l => l.Episodes).ToList();
                    var parsedGameInfo = localGame.FolderEpisodeInfo ?? localGame.FileEpisodeInfo;

                    _eventAggregator.PublishEvent(new UntrackedDownloadCompletedEvent(platform, games, romFiles, parsedGameInfo, sourcePath));
                }
            }

            foreach (var groupedTrackedDownload in importedTrackedDownload.GroupBy(i => i.TrackedDownload.DownloadItem.DownloadId).ToList())
            {
                var trackedDownload = groupedTrackedDownload.First().TrackedDownload;
                var importedPlatform = imported.First().ImportDecision.LocalEpisode.Series;
                var outputPath = trackedDownload.ImportItem.OutputPath.FullPath;

                if (_diskProvider.FolderExists(outputPath))
                {
                    if (_downloadedGamesImportService.ShouldDeleteFolder(
                            new DirectoryInfo(outputPath), importedPlatform) &&
                        trackedDownload.DownloadItem.CanMoveFiles)
                    {
                        _diskProvider.DeleteFolder(outputPath, true);
                    }
                }

                var importedResults = groupedTrackedDownload.Select(c => c.ImportResult)
                    .Where(c => c.Result == ImportResultType.Imported)
                    .ToList();

                var allGamesImported = importedResults
                                                                .SelectMany(c => c.ImportDecision.LocalEpisode.Episodes).Count() >=
                                                                    Math.Max(1, trackedDownload.RemoteEpisode?.Episodes?.Count ?? 1);

                if (allGamesImported)
                {
                    var romFiles = importedResults.Select(i => i.EpisodeFile).ToList();

                    trackedDownload.State = TrackedDownloadState.Imported;
                    _eventAggregator.PublishEvent(new DownloadCompletedEvent(trackedDownload, importedPlatform.Id, romFiles, importedResults.First().ImportDecision.LocalEpisode.Release));
                }
            }
        }
    }
}
