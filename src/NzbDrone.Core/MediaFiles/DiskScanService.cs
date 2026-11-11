using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public interface IDiskScanService
    {
        void Scan(Series series);
        string[] GetVideoFiles(string path, bool allDirectories = true);
        string[] GetNonVideoFiles(string path, bool allDirectories = true);
        List<string> FilterPaths(string basePath, IEnumerable<string> files, bool filterExtras = true);
    }

    public class DiskScanService :
        IDiskScanService,
        IExecute<RescanPlatformCommand>
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IImportApprovedGames _importApprovedEpisodes;
        private readonly IConfigService _configService;
        private readonly ISeriesService _seriesService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IMediaFileTableCleanupService _mediaFileTableCleanupService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IUpdateMediaInfo _updateMediaInfoService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;
        private readonly IBiosService _biosService;

        public DiskScanService(IDiskProvider diskProvider,
                               IMakeImportDecision importDecisionMaker,
                               IImportApprovedGames importApprovedEpisodes,
                               IConfigService configService,
                               ISeriesService seriesService,
                               IMediaFileService mediaFileService,
                               IMediaFileTableCleanupService mediaFileTableCleanupService,
                               IRootFolderService rootFolderService,
                               IUpdateMediaInfo updateMediaInfoService,
                               IEventAggregator eventAggregator,
                               Logger logger,
                               IBiosService biosService = null)
        {
            _diskProvider = diskProvider;
            _importDecisionMaker = importDecisionMaker;
            _importApprovedEpisodes = importApprovedEpisodes;
            _configService = configService;
            _seriesService = seriesService;
            _mediaFileService = mediaFileService;
            _mediaFileTableCleanupService = mediaFileTableCleanupService;
            _rootFolderService = rootFolderService;
            _updateMediaInfoService = updateMediaInfoService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _biosService = biosService;
        }

        private static readonly Regex ExcludedExtrasSubFolderRegex = new Regex(@"(?:\\|\/|^)(?:extras|extrafanart|behind the scenes|deleted scenes|featurettes|interviews|other|scenes|samples|shorts|trailers|theme[-_. ]music|backdrops)(?:\\|\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedSubFoldersRegex = new Regex(@"(?:\\|\/|^)(?:@eadir|\.@__thumb|plex versions|\.[^\\/]+)(?:\\|\/)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedExtraFilesRegex = new Regex(@"(-(trailer|other|behindthescenes|deleted|featurette|interview|scene|short)\.[^.]+$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ExcludedFilesRegex = new Regex(@"^\.(_|unmanic|DS_Store$)|^Thumbs\.db$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public void Scan(Series series)
        {
            if (series != null && series.Id > 0 && _seriesService.IsSeriesDeletedWithFiles(series.Id))
            {
                if (!string.IsNullOrWhiteSpace(series.Path) && _diskProvider.FolderExists(series.Path))
                {
                    _diskProvider.DeleteFolder(series.Path, true);
                }

                return;
            }

            var rootFolder = _rootFolderService.GetBestRootFolderPath(series.Path);

            var seriesFolderExists = _diskProvider.FolderExists(series.Path);

            if (!seriesFolderExists)
            {
                var trimmedPath = series.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var candidateZips = new List<string>
                {
                    trimmedPath + ".zip",
                    trimmedPath + ".7z"
                };

                if (!string.IsNullOrWhiteSpace(rootFolder) && _diskProvider.FolderExists(rootFolder))
                {
                    candidateZips.Add(Path.Combine(rootFolder, series.CleanTitle + ".zip"));
                    candidateZips.Add(Path.Combine(rootFolder, series.CleanTitle + ".7z"));
                    candidateZips.Add(Path.Combine(rootFolder, series.Title + ".zip"));
                    candidateZips.Add(Path.Combine(rootFolder, series.Title + ".7z"));
                    if (!string.IsNullOrWhiteSpace(series.TitleSlug))
                    {
                        candidateZips.Add(Path.Combine(rootFolder, series.TitleSlug + ".zip"));
                        candidateZips.Add(Path.Combine(rootFolder, series.TitleSlug + ".7z"));
                    }

                    var platformAliases = PlatformCompatibilityValidator.GetAliasesForPlatform(series.TitleSlug ?? series.CleanTitle);
                    foreach (var alias in platformAliases)
                    {
                        candidateZips.Add(Path.Combine(rootFolder, alias + ".zip"));
                        candidateZips.Add(Path.Combine(rootFolder, alias + ".7z"));
                    }
                }

                if (candidateZips.Any(z => _diskProvider.FileExists(z)))
                {
                    _diskProvider.CreateFolder(series.Path);
                    SetPermissions(series.Path);
                    seriesFolderExists = true;
                }
            }

            if (!seriesFolderExists)
            {
                if (!_diskProvider.FolderExists(rootFolder))
                {
                    _logger.Warn("Platform's root folder ({0}) doesn't exist.", rootFolder);
                    _eventAggregator.PublishEvent(new PlatformScanSkippedEvent(series, SeriesScanSkippedReason.RootFolderDoesNotExist));
                    return;
                }

                if (_diskProvider.FolderEmpty(rootFolder))
                {
                    _logger.Warn("Platform's root folder ({0}) is empty.", rootFolder);
                    _eventAggregator.PublishEvent(new PlatformScanSkippedEvent(series, SeriesScanSkippedReason.RootFolderIsEmpty));
                    return;
                }
            }

            _logger.ProgressInfo("Scanning {0}", series.Title);

            if (!seriesFolderExists)
            {
                if (_configService.CreateEmptySeriesFolders)
                {
                    if (_configService.DeleteEmptyFolders)
                    {
                        _logger.Debug("Not creating missing platform folder: {0} because delete empty platform folders is enabled", series.Path);
                    }
                    else
                    {
                        _logger.Debug("Creating missing platform folder: {0}", series.Path);

                        _diskProvider.CreateFolder(series.Path);
                        SetPermissions(series.Path);
                    }
                }
                else
                {
                    _logger.Debug("Platform folder doesn't exist: {0}", series.Path);
                }

                CleanMediaFiles(series, new List<string>());
                CompletedScanning(series, new List<string>());

                return;
            }

            var videoFilesStopwatch = Stopwatch.StartNew();
            MigrateLegacySeasonFolders(series);
            ExtractCollectionArchives(series);

            if (_biosService != null)
            {
                try
                {
                    _biosService.ScanAndVerifyPlatformBios(series);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Failed to scan and verify platform BIOS for {0}", series);
                }
            }

            var mediaFileList = FilterPaths(series.Path, GetVideoFiles(series.Path)).ToList();
            videoFilesStopwatch.Stop();
            _logger.Trace("Finished getting ROM files for: {0} [{1}]", series, videoFilesStopwatch.Elapsed);

            CleanMediaFiles(series, mediaFileList);

            var seriesFiles = _mediaFileService.GetFilesBySeries(series.Id);
            var unmappedFiles = MediaFileService.FilterExistingFiles(mediaFileList, seriesFiles, series);

            var decisionsStopwatch = Stopwatch.StartNew();
            var decisions = _importDecisionMaker.GetImportDecisions(unmappedFiles, series, false);
            decisionsStopwatch.Stop();
            _logger.Trace("Import decisions complete for: {0} [{1}]", series, decisionsStopwatch.Elapsed);
            _importApprovedEpisodes.Import(decisions, false);

            // Refresh existing files after import in case any files were moved or upgraded
            seriesFiles = _mediaFileService.GetFilesBySeries(series.Id);

            // Update existing files that have a different file size

            var fileInfoStopwatch = Stopwatch.StartNew();
            var filesToUpdate = new List<EpisodeFile>();

            foreach (var file in seriesFiles)
            {
                var path = Path.Combine(series.Path, file.RelativePath);
                long fileSize;

                try
                {
                    fileSize = _diskProvider.GetFileSize(path);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }

                if (file.Size == fileSize)
                {
                    continue;
                }

                file.Size = fileSize;

                if (!_updateMediaInfoService.Update(file, series))
                {
                    filesToUpdate.Add(file);
                }
            }

            // Update any files that had a file size change, but didn't get media info updated.
            if (filesToUpdate.Any())
            {
                _mediaFileService.Update(filesToUpdate);
            }

            fileInfoStopwatch.Stop();
            _logger.Trace("Reprocessing existing files complete for: {0} [{1}]", series, decisionsStopwatch.Elapsed);

            if (series.Id > 0 && _seriesService.IsSeriesDeletedWithFiles(series.Id))
            {
                if (!string.IsNullOrWhiteSpace(series.Path) && _diskProvider.FolderExists(series.Path))
                {
                    _diskProvider.DeleteFolder(series.Path, true);
                }

                return;
            }

            RemoveEmptySeriesFolder(series.Path);

            var possibleExtraFiles = new List<string>();

            if (_diskProvider.FolderExists(series.Path))
            {
                var extraFiles = GetNonVideoFiles(series.Path);
                possibleExtraFiles = FilterPaths(series.Path, extraFiles);
            }

            CompletedScanning(series, possibleExtraFiles);
        }

        private void CleanMediaFiles(Series series, List<string> mediaFileList)
        {
            _logger.Debug("{0} Cleaning up media files in DB", series);
            _mediaFileTableCleanupService.Clean(series, mediaFileList);
        }

        private void CompletedScanning(Series series, List<string> possibleExtraFiles)
        {
            _logger.Info("Completed scanning disk for {0}", series.Title);
            _eventAggregator.PublishEvent(new PlatformScannedEvent(series, possibleExtraFiles));
        }

        public string[] GetVideoFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for video files", path);

            var filesOnDisk = _diskProvider.GetFiles(path, allDirectories).ToList();

            var mediaFileList = filesOnDisk.Where(file =>
            {
                var ext = Path.GetExtension(file);
                if (!MediaFileExtensions.Extensions.Contains(ext))
                {
                    return false;
                }

                if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    return !IsMultiGameCollectionArchive(file);
                }

                return true;
            }).ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} video files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        private bool IsMultiGameCollectionArchive(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                if (archive.Entries.Count <= 1)
                {
                    return false;
                }

                var validExtensions = MediaFileExtensions.RomExtensions;
                var gameEntries = archive.Entries.Where(e =>
                {
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\") ||
                        e.FullName.Contains("__MACOSX") || e.Name.StartsWith(".") ||
                        e.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var ext = Path.GetExtension(e.Name);
                    return validExtensions.Contains(ext);
                }).ToList();

                if (gameEntries.Count <= 1)
                {
                    return false;
                }

                var cueEntries = gameEntries.Where(e =>
                    Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(e.Name).Equals(".gdi", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetExtension(e.Name).Equals(".ccd", StringComparison.OrdinalIgnoreCase)).ToList();

                if (cueEntries.Count <= 1)
                {
                    var hasNestedArchives = gameEntries.Any(e =>
                        Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                    var distinctDirectories = gameEntries
                        .Select(e => Path.GetDirectoryName(e.FullName) ?? string.Empty)
                        .Distinct()
                        .ToList();

                    if (!hasNestedArchives && distinctDirectories.Count <= 1)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string[] GetNonVideoFiles(string path, bool allDirectories = true)
        {
            _logger.Debug("Scanning '{0}' for non-video files", path);

            var filesOnDisk = _diskProvider.GetFiles(path, allDirectories).ToList();

            var mediaFileList = filesOnDisk.Where(file => !MediaFileExtensions.Extensions.Contains(Path.GetExtension(file)))
                                           .ToList();

            _logger.Trace("{0} files were found in {1}", filesOnDisk.Count, path);
            _logger.Debug("{0} non-video files were found in {1}", mediaFileList.Count, path);

            return mediaFileList.ToArray();
        }

        public List<string> FilterPaths(string basePath, IEnumerable<string> paths, bool filterExtras = true)
        {
            var filteredPaths = paths.Where(path => !ExcludedSubFoldersRegex.IsMatch(basePath.GetRelativePath(path)))
                        .Where(path => !ExcludedFilesRegex.IsMatch(Path.GetFileName(path)))
                        .ToList();

            if (filterExtras)
            {
                filteredPaths = filteredPaths.Where(path => !ExcludedExtrasSubFolderRegex.IsMatch(basePath.GetRelativePath(path)))
                                             .Where(path => !ExcludedExtraFilesRegex.IsMatch(Path.GetFileName(path)))
                                             .ToList();
            }

            return filteredPaths;
        }

        private void ExtractCollectionArchives(Series series)
        {
            var archiveFiles = new List<string>();

            if (_diskProvider.FolderExists(series.Path))
            {
                archiveFiles.AddRange(_diskProvider.GetFiles(series.Path, true)
                    .Where(f => Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetExtension(f).Equals(".7z", StringComparison.OrdinalIgnoreCase)));
            }

            var trimmedPath = series.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folderName = Path.GetFileName(trimmedPath);
            var candidateArchives = new List<string>
            {
                trimmedPath + ".zip",
                trimmedPath + ".7z"
            };

            var rootFolder = _rootFolderService.GetBestRootFolderPath(series.Path);
            if (!string.IsNullOrWhiteSpace(rootFolder) && _diskProvider.FolderExists(rootFolder))
            {
                candidateArchives.Add(Path.Combine(rootFolder, folderName + ".zip"));
                candidateArchives.Add(Path.Combine(rootFolder, folderName + ".7z"));
                candidateArchives.Add(Path.Combine(rootFolder, series.CleanTitle + ".zip"));
                candidateArchives.Add(Path.Combine(rootFolder, series.CleanTitle + ".7z"));
                candidateArchives.Add(Path.Combine(rootFolder, series.Title + ".zip"));
                candidateArchives.Add(Path.Combine(rootFolder, series.Title + ".7z"));

                if (!string.IsNullOrWhiteSpace(series.TitleSlug))
                {
                    candidateArchives.Add(Path.Combine(rootFolder, series.TitleSlug + ".zip"));
                    candidateArchives.Add(Path.Combine(rootFolder, series.TitleSlug + ".7z"));
                }

                var platformAliases = PlatformCompatibilityValidator.GetAliasesForPlatform(series.TitleSlug ?? series.CleanTitle);
                foreach (var alias in platformAliases)
                {
                    candidateArchives.Add(Path.Combine(rootFolder, alias + ".zip"));
                    candidateArchives.Add(Path.Combine(rootFolder, alias + ".7z"));
                }
            }

            foreach (var cand in candidateArchives)
            {
                if (_diskProvider.FileExists(cand) && !archiveFiles.Contains(cand, PathEqualityComparer.Instance))
                {
                    archiveFiles.Add(cand);
                }
            }

            var newlyExtractedArchives = new List<string>();

            foreach (var archiveFile in archiveFiles)
            {
                if (!Path.GetExtension(archiveFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var deleteArchiveAfterExtract = false;

                try
                {
                    using (var archive = ZipFile.OpenRead(archiveFile))
                    {
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

                            var segments = e.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            return segments.Any(s => string.Equals(s, "BIOS", StringComparison.OrdinalIgnoreCase));
                        }).ToList();

                        if (biosEntries.Count > 0)
                        {
                            var targetBiosDirectory = Path.Combine(series.Path, "BIOS");
                            if (!_diskProvider.FolderExists(targetBiosDirectory))
                            {
                                _diskProvider.CreateFolder(targetBiosDirectory);
                            }

                            foreach (var bEntry in biosEntries)
                            {
                                var destBiosPath = Path.Combine(targetBiosDirectory, bEntry.Name);
                                try
                                {
                                    bEntry.ExtractToFile(destBiosPath, true);
                                    if (_biosService != null)
                                    {
                                        _biosService.ProcessCandidateBiosFile(destBiosPath, series, move: false);
                                    }

                                    _logger.Info("Extracted and verified collection BIOS {0} for {1}", bEntry.Name, series.Title);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Debug(ex, "Failed to extract collection BIOS entry {0}", bEntry.FullName);
                                }
                            }
                        }

                        var validExtensions = MediaFileExtensions.RomExtensions;
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
                            return validExtensions.Contains(ext);
                        }).ToList();

                        var cueEntries = gameEntries.Where(e =>
                            Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetExtension(e.Name).Equals(".gdi", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetExtension(e.Name).Equals(".ccd", StringComparison.OrdinalIgnoreCase)).ToList();

                        var hasNestedArchives = gameEntries.Any(e =>
                            Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                            Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                        var isSingleMultiTrack = cueEntries.Count == 1 && !hasNestedArchives &&
                            gameEntries.Select(e => Path.GetDirectoryName(e.FullName) ?? string.Empty).Distinct().Count() <= 1;

                        var isMultiGame = gameEntries.Count > 1 && !isSingleMultiTrack;

                        var shouldExtract = isMultiGame || isSingleMultiTrack || gameEntries.Count > 0 || biosEntries.Count > 0;

                        if (shouldExtract)
                        {
                            _logger.ProgressInfo("Extracting {0} ROMs from collection archive: {1}", gameEntries.Count, Path.GetFileName(archiveFile));

                            var targetDirectory = Path.GetFullPath(series.Path);
                            if (!targetDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                            {
                                targetDirectory += Path.DirectorySeparatorChar;
                            }

                            if (!_diskProvider.FolderExists(targetDirectory))
                            {
                                _diskProvider.CreateFolder(targetDirectory);
                            }

                            var shouldCompress = RomFileMovingService.IsPlatformCompressionEnabled(series);

                            foreach (var entry in gameEntries)
                            {
                                var relativeTarget = entry.FullName.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

                                var segments = relativeTarget.Split(Path.DirectorySeparatorChar);
                                var archiveBaseName = Path.GetFileNameWithoutExtension(archiveFile);
                                if (segments.Length > 1 &&
                                    (string.Equals(segments[0], archiveBaseName, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(segments[0], series.Title, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(segments[0], series.CleanTitle, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(segments[0], series.TitleSlug, StringComparison.OrdinalIgnoreCase)))
                                {
                                    relativeTarget = string.Join(Path.DirectorySeparatorChar, segments.Skip(1));
                                }

                                if (isSingleMultiTrack && !relativeTarget.Contains(Path.DirectorySeparatorChar))
                                {
                                    relativeTarget = Path.Combine(archiveBaseName, relativeTarget);
                                }
                                else if (isMultiGame && cueEntries.Count == 0)
                                {
                                    var entryFileName = Path.GetFileName(relativeTarget);
                                    var regionFolder = MetadataSource.Game.GameCatalogProxy.ResolveCanonicalRegionFolderForRom(entry.FullName, entryFileName);
                                    relativeTarget = Path.Combine(regionFolder, entryFileName);
                                }

                                var destinationPath = Path.GetFullPath(Path.Combine(targetDirectory, relativeTarget));

                                if (!destinationPath.StartsWith(targetDirectory, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger.Warn("Zip entry '{0}' attempts path traversal outside target directory. Skipping.", entry.FullName);
                                    continue;
                                }

                                var destDir = Path.GetDirectoryName(destinationPath);

                                if (!_diskProvider.FolderExists(destDir))
                                {
                                    _diskProvider.CreateFolder(destDir);
                                }

                                if (!_diskProvider.FileExists(destinationPath))
                                {
                                    using (var entryStream = entry.Open())
                                    {
                                        _diskProvider.SaveStream(entryStream, destinationPath);
                                    }

                                    var destExt = Path.GetExtension(destinationPath);
                                    if (destExt.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                                    {
                                        newlyExtractedArchives.Add(destinationPath);
                                    }
                                    else if (shouldCompress &&
                                             RomFileMovingService.IsZipCompressibleRom(destExt, series, destinationPath) &&
                                             File.Exists(destinationPath))
                                    {
                                        try
                                        {
                                            var zipDest = Path.ChangeExtension(destinationPath, ".zip");
                                            if (File.Exists(zipDest))
                                            {
                                                File.Delete(zipDest);
                                            }

                                            using (var zipArchive = ZipFile.Open(zipDest, ZipArchiveMode.Create))
                                            {
                                                zipArchive.CreateEntryFromFile(destinationPath, Path.GetFileName(destinationPath), CompressionLevel.Optimal);
                                            }

                                            if (File.Exists(zipDest))
                                            {
                                                File.Delete(destinationPath);
                                            }
                                        }
                                        catch (Exception zipEx)
                                        {
                                            _logger.Debug(zipEx, "Unable to compress extracted ROM '{0}' to zip", destinationPath);
                                        }
                                    }
                                }
                            }

                            _logger.Info("Extracted {0} ROMs from collection archive '{1}'", gameEntries.Count, Path.GetFileName(archiveFile));

                            if ((isSingleMultiTrack && archiveFile.StartsWith(targetDirectory, StringComparison.OrdinalIgnoreCase)) ||
                                (_configService == null || _configService.DeleteOriginalAfterConversion))
                            {
                                deleteArchiveAfterExtract = true;
                            }
                        }
                    }

                    if (deleteArchiveAfterExtract && _diskProvider.FileExists(archiveFile))
                    {
                        _diskProvider.DeleteFile(archiveFile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Failed to extract collection archive '{0}' during disk scan for {1}", archiveFile, series.Title);
                }
            }

            foreach (var innerZip in newlyExtractedArchives)
            {
                if (!_diskProvider.FileExists(innerZip))
                {
                    continue;
                }

                try
                {
                    using var archive = ZipFile.OpenRead(innerZip);
                    var validEntries = archive.Entries.Where(e =>
                    {
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\"))
                        {
                            return false;
                        }

                        if (e.FullName.Contains("__MACOSX") || e.Name.StartsWith(".") ||
                            e.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }

                        var ext = Path.GetExtension(e.Name);
                        return MediaFileExtensions.RomExtensions.Contains(ext);
                    }).ToList();

                    if (validEntries.Count == 0)
                    {
                        continue;
                    }

                    var cueEntries = validEntries.Where(e =>
                        Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(e.Name).Equals(".gdi", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetExtension(e.Name).Equals(".ccd", StringComparison.OrdinalIgnoreCase)).ToList();

                    var zipDir = Path.GetDirectoryName(innerZip);
                    var zipBaseName = Path.GetFileNameWithoutExtension(innerZip);

                    if (cueEntries.Count > 0 || validEntries.Count > 1)
                    {
                        var extractTargetDir = Path.Combine(zipDir, zipBaseName);
                        if (!_diskProvider.FolderExists(extractTargetDir))
                        {
                            _diskProvider.CreateFolder(extractTargetDir);
                        }

                        foreach (var entry in validEntries)
                        {
                            var targetFileName = Path.GetFileName(entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                            var dest = Path.Combine(extractTargetDir, targetFileName);
                            if (!_diskProvider.FileExists(dest))
                            {
                                using var stream = entry.Open();
                                _diskProvider.SaveStream(stream, dest);
                            }
                        }

                        _logger.Info("Extracted {0} files from disc game archive '{1}' into '{2}'", validEntries.Count, Path.GetFileName(innerZip), extractTargetDir);
                        _diskProvider.DeleteFile(innerZip);
                    }
                    else if (validEntries.Count == 1 && !RomFileMovingService.IsPlatformCompressionEnabled(series))
                    {
                        var singleEntry = validEntries[0];
                        var dest = Path.Combine(zipDir, singleEntry.Name);
                        if (!_diskProvider.FileExists(dest))
                        {
                            using var stream = singleEntry.Open();
                            _diskProvider.SaveStream(stream, dest);
                        }

                        _logger.Info("Extracted single ROM '{0}' from archive '{1}'", singleEntry.Name, Path.GetFileName(innerZip));
                        _diskProvider.DeleteFile(innerZip);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Failed to inspect nested zip archive '{0}'", innerZip);
                }
            }
        }

        public static List<string> FilteredSubFolderMatches(string subfolder)
        {
            var matches = new List<string>();

            foreach (var match in ExcludedSubFoldersRegex.Matches(subfolder))
            {
                matches.Add(match.ToString());
            }

            foreach (var match in ExcludedExtrasSubFolderRegex.Matches(subfolder))
            {
                matches.Add(match.ToString());
            }

            foreach (var match in ExcludedExtraFilesRegex.Matches(subfolder))
            {
                matches.Add(match.ToString());
            }

            return matches;
        }

        private void SetPermissions(string path)
        {
            if (!_configService.SetPermissionsLinux)
            {
                return;
            }

            try
            {
                _diskProvider.SetPermissions(path, _configService.ChmodFolder, _configService.ChownGroup);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to apply permissions to: " + path);
                _logger.Debug(ex, ex.Message);
            }
        }

        private void RemoveEmptySeriesFolder(string path)
        {
            if (_configService.DeleteEmptyFolders)
            {
                _diskProvider.RemoveEmptySubfolders(path);

                if (_diskProvider.FolderEmpty(path))
                {
                    _diskProvider.DeleteFolder(path, true);
                }
            }
        }

        private void MigrateLegacySeasonFolders(Series series)
        {
            if (series == null || string.IsNullOrWhiteSpace(series.Path) || !_diskProvider.FolderExists(series.Path))
            {
                return;
            }

            var legacyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Specials", "Unlicensed" },
                { "Season 0", "Unlicensed" },
                { "Season 00", "Unlicensed" },
                { "Season 1", "USA" },
                { "Season 01", "USA" },
                { "Season 2", "Europe" },
                { "Season 02", "Europe" },
                { "Season 3", "Japan" },
                { "Season 03", "Japan" },
                { "Season 4", "World" },
                { "Season 04", "World" },
                { "Season 5", "Homebrew" },
                { "Season 05", "Homebrew" },
                { "Season 6", "ROM Hacks" },
                { "Season 06", "ROM Hacks" }
            };

            var hadLegacyFolder = false;

            foreach (var pair in legacyMappings)
            {
                var legacyPath = Path.Combine(series.Path, pair.Key);
                if (!_diskProvider.FolderExists(legacyPath))
                {
                    continue;
                }

                hadLegacyFolder = true;
                var targetPath = Path.Combine(series.Path, pair.Value);

                try
                {
                    if (_diskProvider.FolderEmpty(legacyPath))
                    {
                        _diskProvider.DeleteFolder(legacyPath, true);
                    }
                    else if (!_diskProvider.FolderExists(targetPath))
                    {
                        _diskProvider.MoveFolder(legacyPath, targetPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to migrate legacy region folder {0} to {1}", legacyPath, targetPath);
                }
            }

            if (hadLegacyFolder && series.SeasonFolder && series.Seasons != null)
            {
                foreach (var season in series.Seasons.Where(s => s.Monitored))
                {
                    var regionName = season.SeasonNumber switch
                    {
                        0 => "Unlicensed",
                        1 => "USA",
                        2 => "Europe",
                        3 => "Japan",
                        4 => "World",
                        5 => "Homebrew",
                        6 => "ROM Hacks",
                        _ => null
                    };

                    if (!string.IsNullOrWhiteSpace(regionName))
                    {
                        var regionPath = Path.Combine(series.Path, regionName);
                        if (!_diskProvider.FolderExists(regionPath))
                        {
                            _diskProvider.CreateFolder(regionPath);
                        }
                    }
                }
            }
        }

        public void Execute(RescanPlatformCommand message)
        {
            if (message.SeriesId.HasValue)
            {
                var series = _seriesService.GetSeries(message.SeriesId.Value);
                Scan(series);
            }
            else
            {
                var allSeries = _seriesService.GetAllSeries();

                foreach (var series in allSeries)
                {
                    Scan(series);
                }
            }
        }
    }
}
