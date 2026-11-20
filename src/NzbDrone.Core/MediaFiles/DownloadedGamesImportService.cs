using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Download;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.MediaFiles.Patching;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace NzbDrone.Core.MediaFiles
{
    public interface IDownloadedGamesImportService
    {
        List<ImportResult> ProcessRootFolder(DirectoryInfo directoryInfo);
        List<ImportResult> ProcessPath(string path, ImportMode importMode = ImportMode.Auto, Series series = null, DownloadClientItem downloadClientItem = null);
        bool ShouldDeleteFolder(DirectoryInfo directoryInfo, Series series);
    }

    public class DownloadedGamesImportService : IDownloadedGamesImportService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IDiskScanService _diskScanService;
        private readonly ISeriesService _seriesService;
        private readonly IParsingService _parsingService;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IImportApprovedGames _importApprovedEpisodes;
        private readonly IDetectSample _detectSample;
        private readonly IRuntimeInfo _runtimeInfo;
        private readonly IConfigService _configService;
        private readonly IRejectedFileExtensionService _rejectedFileExtensionService;
        private readonly IRomPatchingService _romPatchingService;
        private readonly IBiosService _biosService;
        private readonly IImportActivityService _importActivityService;
        private readonly IDatCacheService _datCacheService;
        private readonly IEpisodeService _episodeService;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public DownloadedGamesImportService(IDiskProvider diskProvider,
                                               IDiskScanService diskScanService,
                                               ISeriesService seriesService,
                                               IParsingService parsingService,
                                               IMakeImportDecision importDecisionMaker,
                                               IImportApprovedGames importApprovedEpisodes,
                                               IDetectSample detectSample,
                                               IRuntimeInfo runtimeInfo,
                                               IConfigService configService,
                                               IRejectedFileExtensionService rejectedFileExtensionService,
                                               Logger logger,
                                               IRomPatchingService romPatchingService = null,
                                               IBiosService biosService = null,
                                               IImportActivityService importActivityService = null,
                                               IDatCacheService datCacheService = null,
                                               IEpisodeService episodeService = null,
                                               IMediaFileService mediaFileService = null)
        {
            _diskProvider = diskProvider;
            _diskScanService = diskScanService;
            _seriesService = seriesService;
            _parsingService = parsingService;
            _importDecisionMaker = importDecisionMaker;
            _importApprovedEpisodes = importApprovedEpisodes;
            _detectSample = detectSample;
            _runtimeInfo = runtimeInfo;
            _configService = configService;
            _rejectedFileExtensionService = rejectedFileExtensionService;
            _romPatchingService = romPatchingService;
            _biosService = biosService;
            _importActivityService = importActivityService;
            _datCacheService = datCacheService;
            _episodeService = episodeService;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public List<ImportResult> ProcessRootFolder(DirectoryInfo directoryInfo)
        {
            var results = new List<ImportResult>();

            foreach (var subFolder in _diskProvider.GetDirectories(directoryInfo.FullName))
            {
                var folderResults = ProcessFolder(new DirectoryInfo(subFolder), ImportMode.Auto, null);
                results.AddRange(folderResults);
            }

            foreach (var videoFile in _diskScanService.GetVideoFiles(directoryInfo.FullName, false))
            {
                var fileResults = ProcessFile(new FileInfo(videoFile), ImportMode.Auto, null);
                results.AddRange(fileResults);
            }

            return results;
        }

        public List<ImportResult> ProcessPath(string path, ImportMode importMode = ImportMode.Auto, Series series = null, DownloadClientItem downloadClientItem = null)
        {
            _logger.Debug("Processing path: {0}", path);

            if (_diskProvider.FolderExists(path))
            {
                var directoryInfo = new DirectoryInfo(path);

                if (series == null)
                {
                    return ProcessFolder(directoryInfo, importMode, downloadClientItem);
                }

                return ProcessFolder(directoryInfo, importMode, series, downloadClientItem);
            }

            if (_diskProvider.FileExists(path))
            {
                var fileInfo = new FileInfo(path);

                if (series == null)
                {
                    return ProcessFile(fileInfo, importMode, downloadClientItem);
                }

                return ProcessFile(fileInfo, importMode, series, downloadClientItem);
            }

            LogInaccessiblePathError(path);
            return new List<ImportResult>();
        }

        public bool ShouldDeleteFolder(DirectoryInfo directoryInfo, Series series)
        {
            try
            {
                var videoFiles = _diskScanService.GetVideoFiles(directoryInfo.FullName);
                var rarFiles = _diskProvider.GetFiles(directoryInfo.FullName, true).Where(f =>
                    Path.GetExtension(f).Equals(".rar",
                        StringComparison.OrdinalIgnoreCase));

                foreach (var videoFile in videoFiles)
                {
                    var episodeParseResult = Parser.Parser.ParseTitle(Path.GetFileName(videoFile));

                    if (episodeParseResult == null)
                    {
                        var ext = Path.GetExtension(videoFile);
                        var romParseResult = !ext.IsNullOrWhiteSpace() && MediaFileExtensions.Extensions.Contains(ext)
                            ? RomParser.ParseRomName(videoFile)
                            : null;

                        if (romParseResult != null && !string.IsNullOrWhiteSpace(romParseResult.GameTitle))
                        {
                            _logger.Debug("Unimported ROM file detected: [{0}]", videoFile);
                            return false;
                        }

                        _logger.Warn("Unable to parse file on import: [{0}]", videoFile);
                        return false;
                    }

                    if (_detectSample.IsSample(series, videoFile, episodeParseResult.IsPossibleSpecialEpisode) !=
                        DetectSampleResult.Sample)
                    {
                        _logger.Warn("Non-sample file detected: [{0}]", videoFile);
                        return false;
                    }
                }

                if (rarFiles.Any(f => _diskProvider.GetFileSize(f) > 10.Megabytes()))
                {
                    _logger.Warn("RAR file detected, will require manual cleanup");
                    return false;
                }

                return true;
            }
            catch (DirectoryNotFoundException e)
            {
                _logger.Debug(e, "Folder {0} has already been removed", directoryInfo.FullName);
                return false;
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Unable to determine whether folder {0} should be removed", directoryInfo.FullName);
                return false;
            }
        }

        private List<ImportResult> ProcessFolder(DirectoryInfo directoryInfo, ImportMode importMode, DownloadClientItem downloadClientItem)
        {
            var cleanedUpName = GetCleanedUpFolderName(directoryInfo.Name);
            var series = _parsingService.GetSeries(cleanedUpName);

            if (series == null)
            {
                _logger.Debug("Unknown Platform {0}", cleanedUpName);

                return new List<ImportResult>
                       {
                           UnknownSeriesResult("Unknown Platform")
                       };
            }

            return ProcessFolder(directoryInfo, importMode, series, downloadClientItem);
        }

        private List<ImportResult> ProcessFolder(DirectoryInfo directoryInfo, ImportMode importMode, Series series, DownloadClientItem downloadClientItem)
        {
            if (_seriesService.SeriesPathExists(directoryInfo.FullName))
            {
                _logger.Warn("Unable to process folder that is mapped to an existing platform");
                return new List<ImportResult>
                {
                    RejectionResult(ImportRejectionReason.SeriesFolder, "Import path is mapped to a platform folder")
                };
            }

            var folderInfo = Parser.Parser.ParseTitle(directoryInfo.Name);
            var videoFiles = _diskScanService.FilterPaths(directoryInfo.FullName, _diskScanService.GetVideoFiles(directoryInfo.FullName));
            var downloadClientItemInfo = downloadClientItem == null ? null : Parser.Parser.ParseTitle(downloadClientItem.Title);

            if (downloadClientItem == null)
            {
                foreach (var videoFile in videoFiles)
                {
                    if (_diskProvider.IsFileLocked(videoFile))
                    {
                        return new List<ImportResult>
                               {
                                   FileIsLockedResult(videoFile)
                               };
                    }
                }
            }

            if (downloadClientItemInfo is { IsMultiSeason: true })
            {
                _logger.Debug("Download client item is marked as multi-region, not processing automatically to avoid importing incorrect files");

                return new List<ImportResult>
                {
                    RejectionResult(ImportRejectionReason.MultiSeason, "Multi-region download, unable to import automatically")
                };
            }

            var patchResults = new List<ImportResult>();

            if (_biosService != null && series != null)
            {
                try
                {
                    var allCandidateFiles = _diskProvider.GetFiles(directoryInfo.FullName, true);
                    foreach (var candidateFile in allCandidateFiles)
                    {
                        var fileName = Path.GetFileName(candidateFile);
                        if (_biosService.IsKnownBiosFileName(fileName) ||
                            candidateFile.Contains($"{Path.DirectorySeparatorChar}BIOS{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                            candidateFile.Contains($"{Path.DirectorySeparatorChar}bios{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                        {
                            var processed = _biosService.ProcessCandidateBiosFile(candidateFile, series, move: importMode == ImportMode.Move);
                            if (processed)
                            {
                                _logger.Info("Imported and verified BIOS file {0} from download for {1}", fileName, series.Title);
                                patchResults.Add(new ImportResult(new ImportDecision(new LocalEpisode
                                {
                                    Path = Path.Combine(series.Path, "BIOS", fileName),
                                    Series = series
                                })));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Error processing candidate BIOS files in download folder {0}", directoryInfo.FullName);
                }
            }

            var remainingVideoFiles = new List<string>();

            foreach (var candidateFile in videoFiles)
            {
                var ext = Path.GetExtension(candidateFile);
                if (IsSupportedMultiGameArchiveExtension(ext) &&
                    IsMultiGameZipArchive(candidateFile))
                {
                    patchResults.AddRange(ProcessMultiGameZipArchive(new FileInfo(candidateFile), importMode, series, downloadClientItem));
                    continue;
                }

                if (_romPatchingService != null &&
                    !string.IsNullOrWhiteSpace(ext) &&
                    MediaFileExtensions.PatchExtensions.Contains(ext))
                {
                    var patchOutcome = _romPatchingService.TryApplyPatchFileForSeries(candidateFile, series);
                    if (patchOutcome != null && patchOutcome.Success)
                    {
                        patchResults.Add(new ImportResult(new ImportDecision(new LocalEpisode
                        {
                            Path = patchOutcome.OutputRomPath ?? candidateFile,
                            Series = series
                        })));
                        continue;
                    }
                }

                remainingVideoFiles.Add(candidateFile);
            }

            _importActivityService?.SetCurrentActivity("Verifying...");
            var decisions = _importDecisionMaker.GetImportDecisions(remainingVideoFiles, series, downloadClientItem, downloadClientItemInfo, folderInfo, true);
            var importResults = ImportDecisionsWithArchiveExtraction(decisions, importMode, series, downloadClientItem);
            importResults.AddRange(patchResults);

            if (importMode == ImportMode.Auto)
            {
                importMode = (downloadClientItem == null || downloadClientItem.CanMoveFiles) ? ImportMode.Move : ImportMode.Copy;
            }

            if (importMode == ImportMode.Move &&
                importResults.Any(i => i.Result == ImportResultType.Imported))
            {
                DeleteEmptySubdirectories(directoryInfo.FullName);

                var isFolderEmpty = _diskProvider.FolderExists(directoryInfo.FullName) &&
                                    !_diskScanService.GetVideoFiles(directoryInfo.FullName).Any() &&
                                    !_diskProvider.GetFiles(directoryInfo.FullName, true).Any();

                if (isFolderEmpty || ShouldDeleteFolder(directoryInfo, series))
                {
                    _logger.Debug("Deleting folder after importing valid files");

                    try
                    {
                        _diskProvider.DeleteFolder(directoryInfo.FullName, true);
                    }
                    catch (IOException e)
                    {
                        _logger.Debug(e, "Unable to delete folder after importing: {0}", e.Message);
                    }
                }
            }
            else if (importResults.Empty())
            {
                importResults.AddIfNotNull(CheckEmptyResultForIssue(directoryInfo.FullName));
            }

            return importResults;
        }

        private List<ImportResult> ProcessFile(FileInfo fileInfo, ImportMode importMode, DownloadClientItem downloadClientItem)
        {
            var series = _parsingService.GetSeries(Path.GetFileNameWithoutExtension(fileInfo.Name));

            if (series == null)
            {
                _logger.Debug("Unknown Platform for file: {0}", fileInfo.Name);

                return new List<ImportResult>
                       {
                           UnknownSeriesResult(string.Format("Unknown Platform for file: {0}", fileInfo.Name), fileInfo.FullName)
                       };
            }

            return ProcessFile(fileInfo, importMode, series, downloadClientItem);
        }

        private List<ImportResult> ProcessFile(FileInfo fileInfo, ImportMode importMode, Series series, DownloadClientItem downloadClientItem)
        {
            if (Path.GetFileNameWithoutExtension(fileInfo.Name).StartsWith("._"))
            {
                _logger.Debug("[{0}] starts with '._', skipping", fileInfo.FullName);

                return new List<ImportResult>
                       {
                           new ImportResult(new ImportDecision(new LocalEpisode { Path = fileInfo.FullName }, new ImportRejection(ImportRejectionReason.InvalidFilePath, "Invalid ROM file, filename starts with '._'")), "Invalid ROM file, filename starts with '._'")
                       };
            }

            var extension = Path.GetExtension(fileInfo.Name);

            if (FileExtensions.DangerousExtensions.Contains(extension))
            {
                return new List<ImportResult>
                {
                    new ImportResult(new ImportDecision(new LocalEpisode { Path = fileInfo.FullName },
                            new ImportRejection(ImportRejectionReason.DangerousFile, $"Caution: Found potentially dangerous file with extension: {extension}")),
                        $"Caution: Found potentially dangerous file with extension: {extension}")
                };
            }

            if (FileExtensions.ExecutableExtensions.Contains(extension))
            {
                return new List<ImportResult>
                {
                    new ImportResult(new ImportDecision(new LocalEpisode { Path = fileInfo.FullName },
                            new ImportRejection(ImportRejectionReason.ExecutableFile, $"Caution: Found executable file with extension: '{extension}'")),
                        $"Caution: Found executable file with extension: '{extension}'")
                };
            }

            if (FileExtensions.ParseUserRejectedExtensions(_configService.UserRejectedExtensions).Contains(extension))
            {
                return new List<ImportResult>
                {
                    new ImportResult(new ImportDecision(new LocalEpisode
                            {
                                Path = fileInfo.FullName
                            },
                            new ImportRejection(ImportRejectionReason.UserRejectedExtension, $"Caution: Found file with user defined rejected extension: '{extension}'")),
                        $"Caution: Found executable file with user defined rejected extension: '{extension}'")
                };
            }

            if (_biosService != null && series != null && (_biosService.IsKnownBiosFileName(fileInfo.Name) || RegionHelper.IsBios(fileInfo.Name)))
            {
                var processed = _biosService.ProcessCandidateBiosFile(fileInfo.FullName, series, move: importMode == ImportMode.Move);
                if (processed)
                {
                    _logger.Info("Imported and verified downloaded BIOS file {0} for {1}", fileInfo.Name, series.Title);
                    return new List<ImportResult>
                    {
                        new ImportResult(new ImportDecision(new LocalEpisode
                        {
                            Path = Path.Combine(series.Path, "BIOS", fileInfo.Name),
                            Series = series
                        }))
                    };
                }
            }

            if (extension.IsNullOrWhiteSpace() || !MediaFileExtensions.Extensions.Contains(extension))
            {
                var crossMatch = TryHandleNoWastedDownload(fileInfo, series, importMode, downloadClientItem, null);
                if (crossMatch != null)
                {
                    return crossMatch;
                }

                _logger.Debug("[{0}] has an unsupported extension: '{1}'", fileInfo.FullName, extension);

                return new List<ImportResult>
                       {
                           new ImportResult(new ImportDecision(new LocalEpisode { Path = fileInfo.FullName },
                               new ImportRejection(ImportRejectionReason.UnsupportedExtension, $"Invalid ROM file, unsupported extension: '{extension}'")),
                               $"Invalid ROM file, unsupported extension: '{extension}'")
                       };
            }

            if (downloadClientItem == null)
            {
                if (_diskProvider.IsFileLocked(fileInfo.FullName))
                {
                    return new List<ImportResult>
                           {
                               FileIsLockedResult(fileInfo.FullName)
                           };
                }
            }

            if (_romPatchingService != null && MediaFileExtensions.PatchExtensions.Contains(extension))
            {
                var patchOutcome = _romPatchingService.TryApplyPatchFileForSeries(fileInfo.FullName, series);
                if (patchOutcome != null && patchOutcome.Success)
                {
                    var patchedEpisode = new LocalEpisode
                    {
                        Path = patchOutcome.OutputRomPath ?? fileInfo.FullName,
                        Series = series
                    };

                    return new List<ImportResult>
                    {
                        new ImportResult(new ImportDecision(patchedEpisode))
                    };
                }
            }

            if (IsSupportedMultiGameArchiveExtension(extension) && IsMultiGameZipArchive(fileInfo.FullName))
            {
                return ProcessMultiGameZipArchive(fileInfo, importMode, series, downloadClientItem);
            }

            var downloadClientItemInfo = downloadClientItem == null ? null : Parser.Parser.ParseTitle(downloadClientItem.Title);
            var decisions = _importDecisionMaker.GetImportDecisions(new List<string>() { fileInfo.FullName }, series, downloadClientItem, downloadClientItemInfo, null, true);

            if (decisions != null && decisions.Any() && decisions.All(d => !d.Approved))
            {
                var firstDecision = decisions.First();
                var crossMatch = TryHandleNoWastedDownload(fileInfo, series, importMode, downloadClientItem, firstDecision.LocalEpisode);
                if (crossMatch != null)
                {
                    return crossMatch;
                }
            }

            return ImportDecisionsWithArchiveExtraction(decisions, importMode, series, downloadClientItem);
        }

        private List<ImportResult> ImportDecisionsWithArchiveExtraction(
            List<ImportDecision> decisions,
            ImportMode importMode,
            Series series,
            DownloadClientItem downloadClientItem)
        {
            if (decisions == null || decisions.Count == 0)
            {
                return _importApprovedEpisodes.Import(decisions, true, downloadClientItem, importMode);
            }

            var archiveDecisionsToExtract = new List<ImportDecision>();
            var directDecisions = new List<ImportDecision>();

            foreach (var decision in decisions)
            {
                if (decision != null &&
                    decision.Approved &&
                    decision.LocalEpisode != null &&
                    ShouldExtractSingleGameArchiveForImport(decision.LocalEpisode.Path, decision.LocalEpisode.Series ?? series))
                {
                    archiveDecisionsToExtract.Add(decision);
                }
                else
                {
                    directDecisions.Add(decision);
                }
            }

            if (archiveDecisionsToExtract.Count == 0)
            {
                return _importApprovedEpisodes.Import(decisions, true, downloadClientItem, importMode);
            }

            var allResults = new List<ImportResult>();

            if (directDecisions.Count > 0)
            {
                allResults.AddRange(_importApprovedEpisodes.Import(directDecisions, true, downloadClientItem, importMode));
            }

            var effectiveImportMode = importMode == ImportMode.Auto
                ? ((downloadClientItem == null || downloadClientItem.CanMoveFiles) ? ImportMode.Move : ImportMode.Copy)
                : importMode;

            foreach (var archiveDecision in archiveDecisionsToExtract)
            {
                allResults.AddRange(ImportExtractedSingleGameArchive(
                    archiveDecision,
                    effectiveImportMode,
                    archiveDecision.LocalEpisode.Series ?? series,
                    downloadClientItem));
            }

            return allResults;
        }

        private static bool ShouldExtractSingleGameArchiveForImport(string archivePath, Series series)
        {
            if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            {
                return false;
            }

            var ext = Path.GetExtension(archivePath)?.ToLowerInvariant() ?? string.Empty;
            if (!ArchiveRepackService.IsArchiveExtension(ext))
            {
                return false;
            }

            var shouldCompress = RomFileMovingService.IsPlatformCompressionEnabled(series);
            return RomFileMovingService.ShouldUnwrapArchiveBeforeMove(archivePath, ext, shouldCompress, series);
        }

        private List<ImportResult> ImportExtractedSingleGameArchive(
            ImportDecision archiveDecision,
            ImportMode effectiveImportMode,
            Series targetSeries,
            DownloadClientItem downloadClientItem)
        {
            var archivePath = archiveDecision.LocalEpisode.Path;
            var baseDir = Path.GetDirectoryName(archivePath);
            var folderPrefix = Path.GetFileNameWithoutExtension(archivePath) + "_extract_" + Guid.NewGuid().ToString("N")[..8];
            var extractDir = !string.IsNullOrWhiteSpace(baseDir)
                ? Path.Combine(baseDir, folderPrefix)
                : Path.Combine(Path.GetTempPath(), folderPrefix);

            try
            {
                _importActivityService?.SetCurrentActivity("Extracting...");
                _logger.Info("Extracting verified single-game archive {0} to {1}", archivePath, extractDir);

                try
                {
                    Directory.CreateDirectory(extractDir);
                }
                catch
                {
                    extractDir = Path.Combine(Path.GetTempPath(), folderPrefix);
                    Directory.CreateDirectory(extractDir);
                }

                var rejectedExtensions = FileExtensions.ParseUserRejectedExtensions(_configService?.UserRejectedExtensions ?? string.Empty);
                var extractedFiles = new List<string>();
                ExtractArchiveEntriesForSingleGame(archivePath, extractDir, 0, 5, rejectedExtensions, extractedFiles);

                var biosFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var extractedFile in extractedFiles)
                {
                    var fileName = Path.GetFileName(extractedFile);
                    var isBios = (targetSeries != null && _biosService != null && _biosService.IsKnownBiosFileName(fileName)) ||
                                 RegionHelper.IsBios(fileName, null, null, extractedFile);
                    if (isBios)
                    {
                        biosFiles.Add(extractedFile);
                        if (_biosService != null && targetSeries != null)
                        {
                            _biosService.ProcessCandidateBiosFile(extractedFile, targetSeries, move: effectiveImportMode == ImportMode.Move);
                        }
                    }
                }

                var romCandidates = extractedFiles
                    .Where(f => !biosFiles.Contains(f))
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f);
                        return MediaFileExtensions.RomExtensions.Contains(ext) ||
                               DiscSheetHelper.IsCompanionTrackExtension(ext);
                    })
                    .ToList();

                var primaryRomPath = ArchiveRepackService.ResolvePrimaryRomFile(romCandidates);
                if (string.IsNullOrWhiteSpace(primaryRomPath) || !File.Exists(primaryRomPath))
                {
                    return new List<ImportResult>
                    {
                        new ImportResult(
                            new ImportDecision(
                                archiveDecision.LocalEpisode,
                                new ImportRejection(ImportRejectionReason.ArchiveFile, "Extracted archive did not contain a valid single-game ROM set")),
                            "Extracted archive did not contain a valid single-game ROM set")
                    };
                }

                if (DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(primaryRomPath)))
                {
                    DiscSheetHelper.RepairSheetInPlaceIfNeeded(primaryRomPath);
                }

                var primaryDir = Path.GetDirectoryName(primaryRomPath);
                if (_configService != null &&
                    _configService.ImportExtraFiles &&
                    !string.IsNullOrWhiteSpace(primaryDir))
                {
                    foreach (var extractedFile in extractedFiles)
                    {
                        if (biosFiles.Contains(extractedFile))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(extractedFile);
                        if (MediaFileExtensions.RomExtensions.Contains(ext) ||
                            DiscSheetHelper.IsCompanionTrackExtension(ext) ||
                            !File.Exists(extractedFile))
                        {
                            continue;
                        }

                        var fileDir = Path.GetDirectoryName(extractedFile);
                        if (!string.Equals(fileDir, primaryDir, StringComparison.OrdinalIgnoreCase))
                        {
                            var adjacentPath = Path.Combine(primaryDir, Path.GetFileName(extractedFile));
                            if (!File.Exists(adjacentPath))
                            {
                                File.Copy(extractedFile, adjacentPath, overwrite: false);
                            }
                        }
                    }
                }

                var orig = archiveDecision.LocalEpisode;
                var extractedLocalEpisode = new LocalEpisode
                {
                    Path = primaryRomPath,
                    Size = DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(primaryRomPath))
                        ? DiscSheetHelper.GetEffectiveFileSize(primaryRomPath)
                        : new FileInfo(primaryRomPath).Length,
                    FileEpisodeInfo = orig.FileEpisodeInfo,
                    DownloadClientEpisodeInfo = orig.DownloadClientEpisodeInfo,
                    DownloadItem = orig.DownloadItem,
                    FolderEpisodeInfo = orig.FolderEpisodeInfo ?? new ParsedEpisodeInfo { ReleaseTitle = Path.GetFileName(extractDir) },
                    Series = targetSeries,
                    Episodes = orig.Episodes,
                    Quality = orig.Quality,
                    Languages = orig.Languages,
                    IndexerFlags = orig.IndexerFlags,
                    ReleaseType = orig.ReleaseType,
                    MediaInfo = orig.MediaInfo,
                    ExistingFile = false,
                    SceneSource = orig.SceneSource,
                    ReleaseGroup = orig.ReleaseGroup,
                    ReleaseHash = orig.ReleaseHash,
                    SceneName = !string.IsNullOrWhiteSpace(orig.SceneName) ? orig.SceneName : Path.GetFileNameWithoutExtension(primaryRomPath),
                    OtherVideoFiles = false,
                    CustomFormats = orig.CustomFormats,
                    CustomFormatScore = orig.CustomFormatScore,
                    OriginalFileNameCustomFormats = orig.OriginalFileNameCustomFormats,
                    OriginalFileNameCustomFormatScore = orig.OriginalFileNameCustomFormatScore,
                    Release = orig.Release,
                    ShouldImportExtras = orig.ShouldImportExtras,
                    PossibleExtraFiles = orig.PossibleExtraFiles,
                    SubtitleInfo = orig.SubtitleInfo,
                    RomHashVerified = orig.RomHashVerified,
                    RomHashes = orig.RomHashes
                };

                var extractedResults = _importApprovedEpisodes.Import(
                    new List<ImportDecision> { new ImportDecision(extractedLocalEpisode) },
                    true,
                    downloadClientItem,
                    effectiveImportMode);

                if (effectiveImportMode == ImportMode.Move &&
                    extractedResults.Any(r => r.Result == ImportResultType.Imported) &&
                    !extractedResults.Any(r => r.Result == ImportResultType.Rejected))
                {
                    if (_diskProvider.FileExists(archivePath))
                    {
                        _diskProvider.DeleteFile(archivePath);
                    }
                    else if (File.Exists(archivePath))
                    {
                        File.Delete(archivePath);
                    }
                }

                return extractedResults;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to extract verified single-game archive {0}", archivePath);
                return new List<ImportResult>
                {
                    new ImportResult(
                        new ImportDecision(
                            archiveDecision.LocalEpisode,
                            new ImportRejection(ImportRejectionReason.ArchiveFile, $"Failed to extract archive: {ex.Message}")),
                        $"Failed to extract archive: {ex.Message}")
                };
            }
            finally
            {
                if (Directory.Exists(extractDir))
                {
                    try
                    {
                        Directory.Delete(extractDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Unable to clean up temporary extraction folder {0}", extractDir);
                    }
                }
            }
        }

        private static void ExtractArchiveEntriesForSingleGame(
            string archivePath,
            string targetDir,
            int depth,
            int maxDepth,
            HashSet<string> rejectedExtensions,
            List<string> collectedFiles)
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
            var newlyExtracted = new List<string>();

            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(archivePath);
                foreach (var entry in archive.Entries)
                {
                    if (ArchiveRepackService.IsJunkEntry(entry.FullName, entry.Name))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entry.Name);
                    if (ShouldSkipExtractedExtension(entryExt, rejectedExtensions))
                    {
                        continue;
                    }

                    var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                    var destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativePath));
                    if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var destParent = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrWhiteSpace(destParent) && !Directory.Exists(destParent))
                    {
                        Directory.CreateDirectory(destParent);
                    }

                    entry.ExtractToFile(destPath, overwrite: true);
                    newlyExtracted.Add(destPath);
                }
            }
            else if (ArchiveRepackService.IsArchiveExtension(ext))
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
                    if (ArchiveRepackService.IsJunkEntry(entryKey, entryName))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entryName);
                    if (ShouldSkipExtractedExtension(entryExt, rejectedExtensions))
                    {
                        continue;
                    }

                    var relativePath = entryKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                    var destPath = Path.GetFullPath(Path.Combine(targetDirFull, relativePath));
                    if (!destPath.StartsWith(targetDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var destParent = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrWhiteSpace(destParent) && !Directory.Exists(destParent))
                    {
                        Directory.CreateDirectory(destParent);
                    }

                    entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                    newlyExtracted.Add(destPath);
                }
            }

            foreach (var extractedPath in newlyExtracted)
            {
                var extractedExt = Path.GetExtension(extractedPath);
                if (ArchiveRepackService.IsArchiveExtension(extractedExt))
                {
                    if (depth < maxDepth)
                    {
                        var nestedDir = Path.Combine(Path.GetDirectoryName(extractedPath) ?? targetDir, "nested_" + Guid.NewGuid().ToString("N")[..8]);
                        ExtractArchiveEntriesForSingleGame(extractedPath, nestedDir, depth + 1, maxDepth, rejectedExtensions, collectedFiles);
                    }

                    try
                    {
                        if (File.Exists(extractedPath))
                        {
                            File.Delete(extractedPath);
                        }
                    }
                    catch
                    {
                    }
                }
                else
                {
                    collectedFiles.Add(extractedPath);
                }
            }
        }

        private static bool ShouldSkipExtractedExtension(string ext, HashSet<string> rejectedExtensions)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return false;
            }

            return FileExtensions.DangerousExtensions.Contains(ext) ||
                   FileExtensions.ExecutableExtensions.Contains(ext) ||
                   (rejectedExtensions != null && rejectedExtensions.Contains(ext));
        }

        private List<ImportResult> TryHandleNoWastedDownload(FileInfo fileInfo, Series targetSeries, ImportMode importMode, DownloadClientItem downloadClientItem, LocalEpisode existingLocalEpisode)
        {
            if ((_datCacheService == null && _mediaFileService == null) || fileInfo == null || !fileInfo.Exists || downloadClientItem == null)
            {
                return null;
            }

            try
            {
                var hashes = existingLocalEpisode?.RomHashes != null && existingLocalEpisode.RomHashes.Count > 0
                    ? existingLocalEpisode.RomHashes
                    : UnmanagedDiskScanner.ComputeFileOrArchiveHashes(fileInfo.FullName, MediaFileExtensions.RomExtensions, _datCacheService, _logger);

                hashes.TryGetValue("CRC32", out var crc32);
                hashes.TryGetValue("SHA-1", out var sha1);
                hashes.TryGetValue("MD5", out var md5);
                hashes.TryGetValue("SHA-256", out var sha256);

                var duplicateLibraryFile = TryFindMatchingLibraryFile(fileInfo, hashes);
                if (duplicateLibraryFile != null)
                {
                    var duplicateMessage = $"File '{fileInfo.Name}' failed verification and its hash matches already-imported library file '{duplicateLibraryFile.RelativePath ?? duplicateLibraryFile.Path}'. Failing download without blocklisting.";
                    _logger.Info(duplicateMessage);

                    return new List<ImportResult>
                    {
                        new ImportResult(
                            new ImportDecision(
                                new LocalEpisode { Path = fileInfo.FullName, RomHashes = hashes },
                                new ImportRejection(ImportRejectionReason.DuplicateLibraryHash, duplicateMessage)),
                            duplicateMessage)
                    };
                }

                if (_datCacheService == null)
                {
                    return null;
                }

                var matchedWithPlatform = _datCacheService.FindPlatformByHash(crc32, sha1, md5, sha256);
                var matchedEntry = matchedWithPlatform?.DatEntry;
                if (matchedEntry == null)
                {
                    return null;
                }

                var rawPlatformSlug = matchedWithPlatform.PlatformSlug ?? matchedWithPlatform.DatFile?.Name;
                var matchedPlatformSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(rawPlatformSlug, matchedWithPlatform.DatFile?.Name);
                var allPlatforms = _seriesService.GetAllSeries();
                var matchedPlatform = allPlatforms.FirstOrDefault(s =>
                    string.Equals(PlatformCompatibilityValidator.NormalizePlatformSlug(s.TitleSlug, s.Title), matchedPlatformSlug, StringComparison.OrdinalIgnoreCase));

                Episode matchedEpisode = null;
                if (matchedPlatform != null && _episodeService != null)
                {
                    var games = _episodeService.GetEpisodeBySeries(matchedPlatform.Id);
                    var normalizedTargetName = CleanTitleForMatch(matchedEntry.GameName ?? matchedEntry.RomName);
                    matchedEpisode = games.FirstOrDefault(g =>
                        string.Equals(CleanTitleForMatch(g.Title), normalizedTargetName, StringComparison.OrdinalIgnoreCase));
                }

                if (matchedPlatform != null && matchedEpisode != null && matchedPlatform.Monitored && matchedEpisode.Monitored)
                {
                    _logger.Info(
                        "No Wasted Downloads: File '{0}' matched monitored ROM '{1}' on platform '{2}'. Importing for matched ROM and failing original request.",
                        fileInfo.Name,
                        matchedEpisode.Title,
                        matchedPlatform.Title);

                    var matchedLocalEpisode = new LocalEpisode
                    {
                        Path = fileInfo.FullName,
                        Size = fileInfo.Length,
                        Series = matchedPlatform,
                        Episodes = new List<Episode> { matchedEpisode },
                        RomHashes = hashes,
                        RomHashVerified = true
                    };

                    var importResults = ImportDecisionsWithArchiveExtraction(
                        new List<ImportDecision> { new ImportDecision(matchedLocalEpisode) },
                        importMode,
                        matchedPlatform,
                        downloadClientItem);
                    if (importResults.Any(r => r.Result == ImportResultType.Imported))
                    {
                        var crossMessage = $"Imported as '{matchedEpisode.Title}' ({matchedPlatform.Title}); failing original request so search continues.";
                        return new List<ImportResult>
                        {
                            new ImportResult(
                                new ImportDecision(
                                    new LocalEpisode { Path = fileInfo.FullName },
                                    new ImportRejection(ImportRejectionReason.CrossPlatformMatchImported, crossMessage)),
                                crossMessage)
                        };
                    }
                }

                var matchedLabel = matchedEpisode?.Title ?? matchedEntry.GameName ?? matchedEntry.RomName ?? fileInfo.Name;
                var platformLabel = matchedPlatform?.Title ?? matchedWithPlatform.DatFile?.Name ?? matchedWithPlatform.PlatformSlug ?? "Unknown Platform";
                var isSamePlatform = targetSeries != null &&
                    ((matchedPlatform != null && matchedPlatform.Id == targetSeries.Id) ||
                     string.Equals(
                         PlatformCompatibilityValidator.NormalizePlatformSlug(targetSeries.TitleSlug, targetSeries.Title),
                         matchedPlatformSlug,
                         StringComparison.OrdinalIgnoreCase));

                string holdMessage;
                if (isSamePlatform)
                {
                    holdMessage = matchedEpisode != null && !matchedEpisode.Monitored
                        ? $"Matches unmonitored ROM '{matchedLabel}' on '{targetSeries.Title}'. Held for manual verification."
                        : $"Matched DAT entry '{matchedLabel}' on '{targetSeries.Title}', but could not match an active monitored ROM in your library. Held for manual verification.";
                }
                else
                {
                    holdMessage = $"Does not match target platform '{targetSeries?.Title}', but matches unmonitored ROM '{matchedLabel}' ({platformLabel}). Held for manual verification.";
                }

                _logger.Warn("No Wasted Downloads: {0}", holdMessage);

                return new List<ImportResult>
                {
                    new ImportResult(
                        new ImportDecision(
                            new LocalEpisode { Path = fileInfo.FullName },
                            new ImportRejection(ImportRejectionReason.UnmonitoredDatMatch, holdMessage)),
                        holdMessage)
                };
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to cross-match rejected file '{0}' in DAT cache.", fileInfo.FullName);
                return null;
            }
        }

        private EpisodeFile TryFindMatchingLibraryFile(FileInfo fileInfo, Dictionary<string, string> hashes)
        {
            if (_mediaFileService == null || hashes == null || hashes.Count == 0)
            {
                return null;
            }

            var allPlatforms = _seriesService?.GetAllSeries();
            if (allPlatforms == null || allPlatforms.Count == 0)
            {
                return null;
            }

            var platformIds = allPlatforms.Select(s => s.Id).ToList();
            var libraryFiles = _mediaFileService.GetFilesBySeriesIds(platformIds);
            if (libraryFiles == null || libraryFiles.Count == 0)
            {
                return null;
            }

            var platformById = allPlatforms.ToDictionary(s => s.Id);
            hashes.TryGetValue("CRC32", out var crc32);
            hashes.TryGetValue("SHA-1", out var sha1);
            hashes.TryGetValue("MD5", out var md5);
            hashes.TryGetValue("SHA-256", out var sha256);

            foreach (var libraryFile in libraryFiles)
            {
                if (!string.IsNullOrWhiteSpace(libraryFile.ReleaseHash) &&
                    (string.Equals(libraryFile.ReleaseHash, crc32, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(libraryFile.ReleaseHash, sha1, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(libraryFile.ReleaseHash, md5, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(libraryFile.ReleaseHash, sha256, StringComparison.OrdinalIgnoreCase)))
                {
                    return libraryFile;
                }

                if (libraryFile.Size != fileInfo.Length)
                {
                    continue;
                }

                platformById.TryGetValue(libraryFile.SeriesId, out var platform);
                var candidatePath = !string.IsNullOrWhiteSpace(libraryFile.Path) && Path.IsPathRooted(libraryFile.Path)
                    ? libraryFile.Path
                    : (platform != null && !string.IsNullOrWhiteSpace(platform.Path) && !string.IsNullOrWhiteSpace(libraryFile.RelativePath)
                        ? Path.Combine(platform.Path, libraryFile.RelativePath)
                        : null);

                if (string.IsNullOrWhiteSpace(candidatePath) || !_diskProvider.FileExists(candidatePath))
                {
                    continue;
                }

                var candidateHashes = UnmanagedDiskScanner.ComputeFileOrArchiveHashes(candidatePath, MediaFileExtensions.RomExtensions, _datCacheService, _logger);
                if (HashesMatch(hashes, candidateHashes))
                {
                    return libraryFile;
                }
            }

            return null;
        }

        private static bool HashesMatch(Dictionary<string, string> left, Dictionary<string, string> right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            foreach (var key in new[] { "SHA-256", "SHA-1", "MD5", "CRC32" })
            {
                if (left.TryGetValue(key, out var leftVal) &&
                    right.TryGetValue(key, out var rightVal) &&
                    !string.IsNullOrWhiteSpace(leftVal) &&
                    string.Equals(leftVal, rightVal, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string CleanTitleForMatch(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var noExt = Path.GetFileNameWithoutExtension(title);
            return System.Text.RegularExpressions.Regex.Replace(noExt, @"\s*[(\[][^)\]]*[)\]]", string.Empty).Trim();
        }

        private static bool IsSupportedMultiGameArchiveExtension(string extension)
        {
            return ArchiveRepackService.IsArchiveExtension(extension);
        }

        private bool IsMultiGameZipArchive(string archivePath)
        {
            return ArchiveRepackService.IsMultiGameArchive(archivePath, _biosService);
        }

        private List<ImportResult> ProcessMultiGameZipArchive(FileInfo fileInfo, ImportMode importMode, Series series, DownloadClientItem downloadClientItem)
        {
            var extractDir = Path.Combine(fileInfo.DirectoryName ?? Path.GetTempPath(), Path.GetFileNameWithoutExtension(fileInfo.Name) + "_extracted");

            try
            {
                _importActivityService?.SetCurrentActivity("Extracting...");
                _logger.Info("Extracting multi-game archive {0} to {1} for collection import", fileInfo.FullName, extractDir);
                _diskProvider.EnsureFolder(extractDir);

                var fullExtractRoot = Path.GetFullPath(extractDir);
                if (!fullExtractRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    fullExtractRoot += Path.DirectorySeparatorChar;
                }

                if (fileInfo.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    using (var archive = ZipFile.OpenRead(fileInfo.FullName))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                            {
                                continue;
                            }

                            if (ArchiveRepackService.IsJunkEntry(entry.FullName, entry.Name))
                            {
                                continue;
                            }

                            var destPath = Path.GetFullPath(Path.Combine(extractDir, entry.FullName));
                            if (!destPath.StartsWith(fullExtractRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var destParent = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrWhiteSpace(destParent))
                            {
                                _diskProvider.EnsureFolder(destParent);
                            }

                            entry.ExtractToFile(destPath, true);
                        }
                    }
                }
                else
                {
                    using (var archive = ArchiveFactory.OpenArchive(fileInfo.FullName))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key))
                            {
                                continue;
                            }

                            var entryKey = entry.Key.Replace('\\', '/');
                            var entryName = Path.GetFileName(entryKey);
                            if (ArchiveRepackService.IsJunkEntry(entryKey, entryName))
                            {
                                continue;
                            }

                            var destPath = Path.GetFullPath(Path.Combine(extractDir, entry.Key));
                            if (!destPath.StartsWith(fullExtractRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var destParent = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrWhiteSpace(destParent))
                            {
                                _diskProvider.EnsureFolder(destParent);
                            }

                            entry.WriteToFile(destPath, new ExtractionOptions { ExtractFullPath = false, Overwrite = true });
                        }
                    }
                }

                var folderResults = ProcessFolder(new DirectoryInfo(extractDir), importMode, series, downloadClientItem);

                var effectiveImportMode = importMode == ImportMode.Auto
                    ? ((downloadClientItem == null || downloadClientItem.CanMoveFiles) ? ImportMode.Move : ImportMode.Copy)
                    : importMode;

                if (_diskProvider.FolderExists(extractDir))
                {
                    DeleteEmptySubdirectories(extractDir);
                }

                var isExtractDirEmpty = _diskProvider.FolderExists(extractDir) &&
                                        !_diskProvider.GetFiles(extractDir, true).Any();

                var hasRemainingUnimportedFiles = folderResults.Any(i => i.Result == ImportResultType.Rejected) ||
                                                  (_diskProvider.FolderExists(extractDir) && !isExtractDirEmpty && !ShouldDeleteFolder(new DirectoryInfo(extractDir), series));

                if (effectiveImportMode == ImportMode.Move &&
                    ((folderResults.Any(i => i.Result == ImportResultType.Imported) && !hasRemainingUnimportedFiles) || isExtractDirEmpty))
                {
                    if (downloadClientItem == null && _diskProvider.FileExists(fileInfo.FullName))
                    {
                        _diskProvider.DeleteFile(fileInfo.FullName);
                    }

                    if (_diskProvider.FolderExists(extractDir))
                    {
                        _diskProvider.DeleteFolder(extractDir, true);
                    }
                }
                else if (effectiveImportMode == ImportMode.Copy && _diskProvider.FolderExists(extractDir))
                {
                    _diskProvider.DeleteFolder(extractDir, true);
                }

                return folderResults;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to extract multi-game archive {0}", fileInfo.FullName);
                return new List<ImportResult>
                {
                    RejectionResult(ImportRejectionReason.ArchiveFile, $"Failed to extract multi-game archive: {ex.Message}")
                };
            }
        }

        private void DeleteEmptySubdirectories(string rootPath, HashSet<string> visited = null, int depth = 0)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || depth > 32 || !_diskProvider.FolderExists(rootPath))
            {
                return;
            }

            visited ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!visited.Add(rootPath))
            {
                return;
            }

            try
            {
                foreach (var subDir in _diskProvider.GetDirectories(rootPath))
                {
                    if (string.IsNullOrWhiteSpace(subDir) || visited.Contains(subDir))
                    {
                        continue;
                    }

                    DeleteEmptySubdirectories(subDir, visited, depth + 1);

                    if (!_diskProvider.GetFiles(subDir, false).Any() &&
                        !_diskProvider.GetDirectories(subDir).Any())
                    {
                        _logger.Debug("Deleting empty folder after import: {0}", subDir);
                        _diskProvider.DeleteFolder(subDir, false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to clean up empty subdirectories in {0}", rootPath);
            }
        }

        private string GetCleanedUpFolderName(string folder)
        {
            folder = folder.Replace("_UNPACK_", "")
                           .Replace("_FAILED_", "");

            return folder;
        }

        private ImportResult FileIsLockedResult(string videoFile)
        {
            _logger.Debug("[{0}] is currently locked by another process, skipping", videoFile);
            return new ImportResult(new ImportDecision(new LocalEpisode { Path = videoFile }, new ImportRejection(ImportRejectionReason.FileLocked, "Locked file, try again later")), "Locked file, try again later");
        }

        private ImportResult UnknownSeriesResult(string message, string videoFile = null)
        {
            var localEpisode = videoFile == null ? null : new LocalEpisode { Path = videoFile };

            return new ImportResult(new ImportDecision(localEpisode, new ImportRejection(ImportRejectionReason.UnknownSeries, "Unknown Platform")), message);
        }

        private ImportResult RejectionResult(ImportRejectionReason reason, string message)
        {
            return new ImportResult(new ImportDecision(null, new ImportRejection(reason, message)), message);
        }

        private ImportResult CheckEmptyResultForIssue(string folder)
        {
            var files = _diskProvider.GetFiles(folder, true);
            var rejection = _rejectedFileExtensionService.GetRejection(files);

            if (rejection != null)
            {
                return RejectionResult(rejection.Reason, rejection.Message);
            }

            if (files.Any(file => FileExtensions.ArchiveExtensions.Contains(Path.GetExtension(file))))
            {
                return RejectionResult(ImportRejectionReason.ArchiveFile, "Found archive file, might need to be extracted");
            }

            return null;
        }

        private void LogInaccessiblePathError(string path)
        {
            if (_runtimeInfo.IsWindowsService)
            {
                var mounts = _diskProvider.GetMounts();
                var mount = mounts.FirstOrDefault(m => m.RootDirectory == Path.GetPathRoot(path));

                if (mount == null)
                {
                    _logger.Error("Import failed, path does not exist or is not accessible by Controllarr: {0}. Unable to find a volume mounted for the path. If you're using a mapped network drive see the FAQ for more info", path);
                    return;
                }

                if (mount.DriveType == DriveType.Network)
                {
                    _logger.Error("Import failed, path does not exist or is not accessible by Controllarr: {0}. It's recommended to avoid mapped network drives when running as a Windows service. See the FAQ for more info", path);
                    return;
                }
            }

            if (OsInfo.IsWindows)
            {
                if (path.StartsWith(@"\\"))
                {
                    _logger.Error("Import failed, path does not exist or is not accessible by Controllarr: {0}. Ensure the user running Controllarr has access to the network share", path);
                    return;
                }
            }

            _logger.Error("Import failed, path does not exist or is not accessible by Controllarr: {0}. Ensure the path exists and the user running Controllarr has the correct permissions to access this file/folder", path);
        }
    }
}
