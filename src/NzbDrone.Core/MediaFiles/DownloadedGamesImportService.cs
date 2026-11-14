using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Download;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.Patching;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;

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
                                               IEpisodeService episodeService = null)
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
            var importResults = _importApprovedEpisodes.Import(decisions, true, downloadClientItem, importMode);
            importResults.AddRange(patchResults);

            if (importMode == ImportMode.Auto)
            {
                importMode = (downloadClientItem == null || downloadClientItem.CanMoveFiles) ? ImportMode.Move : ImportMode.Copy;
            }

            if (importMode == ImportMode.Move &&
                importResults.Any(i => i.Result == ImportResultType.Imported) &&
                ShouldDeleteFolder(directoryInfo, series))
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

            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) && IsMultiGameZipArchive(fileInfo.FullName))
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

            return _importApprovedEpisodes.Import(decisions, true, downloadClientItem, importMode);
        }

        private List<ImportResult> TryHandleNoWastedDownload(FileInfo fileInfo, Series targetSeries, ImportMode importMode, DownloadClientItem downloadClientItem, LocalEpisode existingLocalEpisode)
        {
            if (_datCacheService == null || fileInfo == null || !fileInfo.Exists || downloadClientItem == null)
            {
                return null;
            }

            try
            {
                var hashes = existingLocalEpisode?.RomHashes != null && existingLocalEpisode.RomHashes.Count > 0
                    ? existingLocalEpisode.RomHashes
                    : ComputeFileHashes(fileInfo.FullName);

                hashes.TryGetValue("CRC32", out var crc32);
                hashes.TryGetValue("SHA-1", out var sha1);
                hashes.TryGetValue("MD5", out var md5);
                hashes.TryGetValue("SHA-256", out var sha256);

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

                    var importResults = _importApprovedEpisodes.Import(new List<ImportDecision> { new ImportDecision(matchedLocalEpisode) }, true, downloadClientItem, importMode);
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
                var holdMessage = $"Does not match target platform '{targetSeries?.Title}', but matches unmonitored ROM '{matchedLabel}' ({platformLabel}). Held for manual verification.";
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

        private static string CleanTitleForMatch(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var noExt = Path.GetFileNameWithoutExtension(title);
            return System.Text.RegularExpressions.Regex.Replace(noExt, @"\s*[(\[][^)\]]*[)\]]", string.Empty).Trim();
        }

        private static Dictionary<string, string> ComputeFileHashes(string filePath)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using var stream = File.OpenRead(filePath);
            using var md5 = MD5.Create();
            using var sha1 = SHA1.Create();
            using var sha256 = SHA256.Create();

            var buffer = new byte[81920];
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            }

            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            hashes["MD5"] = Convert.ToHexString(md5.Hash).ToLowerInvariant();
            hashes["SHA-1"] = Convert.ToHexString(sha1.Hash).ToLowerInvariant();
            hashes["SHA-256"] = Convert.ToHexString(sha256.Hash).ToLowerInvariant();
            return hashes;
        }

        private bool IsMultiGameZipArchive(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
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

                    return MediaFileExtensions.RomExtensions.Contains(Path.GetExtension(e.Name));
                }).ToList();

                if (gameEntries.Count <= 1)
                {
                    return false;
                }

                var cueEntries = gameEntries.Where(e =>
                    Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(e.Name).Equals(".gdi", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(e.Name).Equals(".ccd", StringComparison.OrdinalIgnoreCase)).ToList();

                var hasNestedArchives = gameEntries.Any(e =>
                    Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                var isSingleMultiTrack = cueEntries.Count == 1 && !hasNestedArchives &&
                    gameEntries.Select(e => Path.GetDirectoryName(e.FullName) ?? string.Empty).Distinct().Count() <= 1;

                return !isSingleMultiTrack;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to inspect zip archive {0}", zipPath);
                return false;
            }
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

                using (var archive = ZipFile.OpenRead(fileInfo.FullName))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            continue;
                        }

                        if (entry.FullName.Contains("__MACOSX") || entry.Name.StartsWith(".") || entry.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
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

                var folderResults = ProcessFolder(new DirectoryInfo(extractDir), importMode, series, downloadClientItem);

                var effectiveImportMode = importMode == ImportMode.Auto
                    ? ((downloadClientItem == null || downloadClientItem.CanMoveFiles) ? ImportMode.Move : ImportMode.Copy)
                    : importMode;

                if (effectiveImportMode == ImportMode.Move && folderResults.Any(i => i.Result == ImportResultType.Imported))
                {
                    if (_diskProvider.FileExists(fileInfo.FullName))
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
