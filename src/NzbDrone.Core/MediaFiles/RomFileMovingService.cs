using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.Synchronization;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public interface IMoveRomFiles
    {
        EpisodeFile MoveEpisodeFile(EpisodeFile romFile, Series platform);
        EpisodeFile MoveEpisodeFile(EpisodeFile romFile, LocalEpisode localGame);
        EpisodeFile CopyEpisodeFile(EpisodeFile romFile, LocalEpisode localGame);
    }

    public class RomFileMovingService : IMoveRomFiles
    {
        private static readonly Regex DiscSuffixRegex = new Regex(
            @"\s*\(\s*(?:Disc|Disk|CD|Side|GD-ROM)\s*(?:\d+(?:\s*of\s*\d+)?|[A-Z])\s*\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IEpisodeService _gameService;
        private readonly IUpdateRomFileService _updateRomFileService;
        private readonly IBuildFileNames _buildFileNames;
        private readonly IDiskTransferService _diskTransferService;
        private readonly IDiskProvider _diskProvider;
        private readonly IMediaFileAttributeService _mediaFileAttributeService;
        private readonly IImportScript _scriptImportDecider;
        private readonly IRootFolderService _rootFolderService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IConfigService _configService;
        private readonly ISidecarSaveService _sidecarSaveService;
        private readonly IDiscCompressionService _discCompressionService;
        private readonly IArchiveRepackService _archiveRepackService;
        private readonly IImportActivityService _importActivityService;
        private readonly Logger _logger;

        public RomFileMovingService(IEpisodeService gameService,
                                IUpdateRomFileService updateRomFileService,
                                IBuildFileNames buildFileNames,
                                IDiskTransferService diskTransferService,
                                IDiskProvider diskProvider,
                                IMediaFileAttributeService mediaFileAttributeService,
                                IImportScript scriptImportDecider,
                                IRootFolderService rootFolderService,
                                IEventAggregator eventAggregator,
                                IConfigService configService,
                                Logger logger,
                                ISidecarSaveService sidecarSaveService = null,
                                IDiscCompressionService discCompressionService = null,
                                IArchiveRepackService archiveRepackService = null,
                                IImportActivityService importActivityService = null)
        {
            _gameService = gameService;
            _updateRomFileService = updateRomFileService;
            _buildFileNames = buildFileNames;
            _diskTransferService = diskTransferService;
            _diskProvider = diskProvider;
            _mediaFileAttributeService = mediaFileAttributeService;
            _scriptImportDecider = scriptImportDecider;
            _rootFolderService = rootFolderService;
            _eventAggregator = eventAggregator;
            _configService = configService;
            _logger = logger;
            _sidecarSaveService = sidecarSaveService;
            _discCompressionService = discCompressionService;
            _archiveRepackService = archiveRepackService;
            _importActivityService = importActivityService;
        }

        public EpisodeFile MoveEpisodeFile(EpisodeFile romFile, Series platform)
        {
            var games = _gameService.GetEpisodesByFileId(romFile.Id);
            return MoveRomFile(romFile, platform, games);
        }

        private EpisodeFile MoveRomFile(EpisodeFile romFile, Series platform, List<Episode> games)
        {
            var filePath = _buildFileNames.BuildFilePath(games, platform, romFile, Path.GetExtension(romFile.RelativePath));

            EnsureGameFolder(romFile, platform, games.Select(v => v.SeasonNumber).First(), filePath);

            _logger.Debug("Renaming ROM file: {0} to {1}", romFile, filePath);

            return TransferFile(romFile, platform, games, filePath, TransferMode.Move);
        }

        public EpisodeFile MoveEpisodeFile(EpisodeFile romFile, LocalEpisode localGame)
        {
            var filePath = _buildFileNames.BuildFilePath(localGame.Episodes, localGame.Series, romFile, Path.GetExtension(localGame.Path), null, localGame.CustomFormats);

            EnsureGameFolder(romFile, localGame, filePath);

            _logger.Debug("Moving ROM file: {0} to {1}", romFile.Path, filePath);

            return TransferFile(romFile, localGame.Series, localGame.Episodes, filePath, TransferMode.Move, localGame);
        }

        public EpisodeFile CopyEpisodeFile(EpisodeFile romFile, LocalEpisode localGame)
        {
            var filePath = _buildFileNames.BuildFilePath(localGame.Episodes, localGame.Series, romFile, Path.GetExtension(localGame.Path), null, localGame.CustomFormats);

            EnsureGameFolder(romFile, localGame, filePath);

            if (_configService.CopyUsingHardlinks)
            {
                _logger.Debug("Attempting to hardlink ROM file: {0} to {1}", romFile.Path, filePath);
                return TransferFile(romFile, localGame.Series, localGame.Episodes, filePath, TransferMode.HardLinkOrCopy, localGame);
            }

            _logger.Debug("Copying ROM file: {0} to {1}", romFile.Path, filePath);
            return TransferFile(romFile, localGame.Series, localGame.Episodes, filePath, TransferMode.Copy, localGame);
        }

        private EpisodeFile TransferFile(EpisodeFile romFile, Series platform, List<Episode> games, string destinationFilePath, TransferMode mode, LocalEpisode localGame = null)
        {
            Ensure.That(romFile, () => romFile).IsNotNull();
            Ensure.That(platform, () => platform).IsNotNull();
            Ensure.That(destinationFilePath, () => destinationFilePath).IsValidPath(PathValidationType.CurrentOs);

            var romFilePath = romFile.Path ?? Path.Combine(platform.Path, romFile.RelativePath);

            if (!_diskProvider.FileExists(romFilePath))
            {
                throw new FileNotFoundException("ROM file path does not exist", romFilePath);
            }

            if (romFilePath == destinationFilePath)
            {
                if (File.Exists(destinationFilePath) && ShouldPostProcessInPlace(destinationFilePath, platform))
                {
                    PostProcessRomFile(ref destinationFilePath, romFile, platform);
                    return romFile;
                }

                throw new SameFilenameException("File not moved, source and destination are the same", romFilePath);
            }

            romFile.RelativePath = platform.Path.GetRelativePath(destinationFilePath);

            if (localGame is not null)
            {
                localGame.FileNameBeforeRename = romFile.RelativePath;
            }

            var sidecarMode = (mode == TransferMode.Copy || mode == TransferMode.HardLinkOrCopy)
                ? ImportMode.Copy
                : ImportMode.Move;

            var stagedPreprocessed = false;
            var discCompressionAttempted = false;
            var originalRomFilePath = romFilePath;
            string tempStagingDir = null;

            if (localGame is not null && _scriptImportDecider.TryImport(romFilePath, destinationFilePath, localGame, romFile, mode) is var scriptImportDecision && scriptImportDecision != ScriptImportDecision.DeferMove)
            {
                if (scriptImportDecision == ScriptImportDecision.RenameRequested)
                {
                    try
                    {
                        MoveRomFile(romFile, platform, romFile.Episodes);
                    }
                    catch (SameFilenameException)
                    {
                        _logger.Debug("No rename was required. File already exists at destination.");
                    }
                }
            }
            else
            {
                try
                {
                    var transferSourcePath = romFilePath;
                    var transferMode = mode;

                    if (TryStagePreMoveChanges(romFilePath, ref destinationFilePath, romFile, platform, out var stagedPath, out tempStagingDir, out discCompressionAttempted))
                    {
                        transferSourcePath = stagedPath;
                        transferMode = TransferMode.Move;
                        stagedPreprocessed = true;
                    }

                    _importActivityService?.SetCurrentActivity(mode == TransferMode.Move ? "Moving..." : "Copying...");
                    if (DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(transferSourcePath)) && File.Exists(transferSourcePath))
                    {
                        TransferDiscSheetAndTracks(transferSourcePath, destinationFilePath, transferMode);
                        if (File.Exists(destinationFilePath))
                        {
                            romFile.Size = new FileInfo(destinationFilePath).Length;
                        }
                    }
                    else
                    {
                        _diskTransferService.TransferFile(transferSourcePath, destinationFilePath, transferMode, File.Exists(destinationFilePath));
                    }

                    if (stagedPreprocessed && mode == TransferMode.Move)
                    {
                        DeleteDiscSourceAndTracks(originalRomFilePath);
                    }
                }
                finally
                {
                    if (!string.IsNullOrWhiteSpace(tempStagingDir) && Directory.Exists(tempStagingDir))
                    {
                        try
                        {
                            Directory.Delete(tempStagingDir, true);
                        }
                        catch (Exception ex)
                        {
                            _logger.Debug(ex, "Unable to clean up temporary staging directory {0}", tempStagingDir);
                        }
                    }
                }
            }

            _sidecarSaveService?.RenameAdjacentSaves(originalRomFilePath, destinationFilePath, sidecarMode);

            PostProcessRomFile(ref destinationFilePath, romFile, platform, discCompressionAttempted);

            _updateRomFileService.ChangeFileDateForFile(romFile, platform, games);

            try
            {
                _mediaFileAttributeService.SetFolderLastWriteTime(platform.Path, romFile.DateAdded);

                if (platform.SeasonFolder)
                {
                    var regionFolder = Path.GetDirectoryName(destinationFilePath);

                    _mediaFileAttributeService.SetFolderLastWriteTime(regionFolder, romFile.DateAdded);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to set last write time");
            }

            _mediaFileAttributeService.SetFilePermissions(destinationFilePath);

            return romFile;
        }

        public static bool IsPlatformCompressionEnabled(Series platform)
        {
            var format = platform?.PreferredArchiveFormat?.Trim();
            if (!string.IsNullOrWhiteSpace(format))
            {
                if (format.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("uncompressed", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (format.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("enabled", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("chd", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("rvz", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("zip", StringComparison.OrdinalIgnoreCase) ||
                    format.Equals("7z", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return true;
        }

        public static bool IsZipCompressibleRom(string ext, Series platform = null, string filePath = null)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return false;
            }

            if (MediaFileExtensions.ZipCompressibleRomExtensions.Contains(ext))
            {
                return true;
            }

            if (ext.Equals(".bin", StringComparison.OrdinalIgnoreCase))
            {
                if (PlatformCatalogService.IsDiscBasedPlatform(platform?.TitleSlug, platform?.Title))
                {
                    return false;
                }

                var slug = platform?.TitleSlug ?? string.Empty;
                if (slug.Equals("genesis", StringComparison.OrdinalIgnoreCase) ||
                    slug.Equals("atari2600", StringComparison.OrdinalIgnoreCase) ||
                    slug.Equals("atari7800", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    var dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        var hasCueOrGdi = Directory.GetFiles(dir)
                            .Any(f =>
                            {
                                var e = Path.GetExtension(f);
                                return e.Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                                       e.Equals(".gdi", StringComparison.OrdinalIgnoreCase) ||
                                       e.Equals(".ccd", StringComparison.OrdinalIgnoreCase);
                            });

                        if (!hasCueOrGdi)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryStagePreMoveChanges(
            string sourceFilePath,
            ref string destinationFilePath,
            EpisodeFile romFile,
            Series platform,
            out string stagedPath,
            out string tempStagingDir,
            out bool discCompressionAttempted)
        {
            stagedPath = null;
            tempStagingDir = null;
            discCompressionAttempted = false;

            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return false;
            }

            var ext = Path.GetExtension(sourceFilePath)?.ToLowerInvariant() ?? string.Empty;
            var shouldCompress = IsPlatformCompressionEnabled(platform);

            try
            {
                if (ArchiveRepackService.IsArchiveExtension(ext) &&
                    ShouldUnwrapArchiveBeforeMove(sourceFilePath, ext, shouldCompress, platform))
                {
                    _importActivityService?.SetCurrentActivity("Extracting...");
                    tempStagingDir = Path.Combine(Path.GetTempPath(), "controllarr_stage_" + Guid.NewGuid().ToString("N"));
                    var unwrapDir = Path.Combine(tempStagingDir, "unwrapped");
                    Directory.CreateDirectory(unwrapDir);

                    if (ArchiveRepackService.TryUnwrapGameFromArchive(sourceFilePath, unwrapDir, out var unwrappedRomPath, out var unwrappedCompanionTracks))
                    {
                        var innerExt = Path.GetExtension(unwrappedRomPath)?.ToLowerInvariant() ?? string.Empty;
                        if (DiscSheetHelper.IsDiscSheetExtension(innerExt))
                        {
                            DiscSheetHelper.RepairSheetInPlaceIfNeeded(unwrappedRomPath);
                        }

                        if (shouldCompress &&
                            unwrappedCompanionTracks.Count == 0 &&
                            IsZipCompressibleRom(innerExt, platform, unwrappedRomPath))
                        {
                            _importActivityService?.SetCurrentActivity("Compressing...");
                            var stagedZipPath = Path.Combine(tempStagingDir, Path.ChangeExtension(Path.GetFileName(destinationFilePath), ".zip"));
                            var entryName = Path.ChangeExtension(Path.GetFileName(destinationFilePath), innerExt);

                            CreateSingleRomZip(unwrappedRomPath, stagedZipPath, entryName);

                            if (File.Exists(stagedZipPath) && new FileInfo(stagedZipPath).Length > 0)
                            {
                                stagedPath = stagedZipPath;
                                UpdateRomFileDestination(romFile, platform, ref destinationFilePath, stagedZipPath, ".zip");
                                return true;
                            }
                        }
                        else if (shouldCompress &&
                                 (innerExt == ".cue" || innerExt == ".iso" || innerExt == ".gdi"))
                        {
                            discCompressionAttempted = true;
                            if (TryStageDiscCompression(unwrappedRomPath, romFile, platform, ref tempStagingDir, ref destinationFilePath, out stagedPath))
                            {
                                return true;
                            }
                        }

                        stagedPath = unwrappedRomPath;
                        UpdateRomFileDestination(romFile, platform, ref destinationFilePath, unwrappedRomPath, innerExt);
                        return true;
                    }
                }

                if (!shouldCompress)
                {
                    return false;
                }

                if (IsZipCompressibleRom(ext, platform, sourceFilePath))
                {
                    _importActivityService?.SetCurrentActivity("Compressing...");
                    tempStagingDir = Path.Combine(Path.GetTempPath(), "controllarr_stage_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempStagingDir);

                    var stagedZipPath = Path.Combine(tempStagingDir, Path.ChangeExtension(Path.GetFileName(destinationFilePath), ".zip"));
                    var entryName = Path.ChangeExtension(Path.GetFileName(destinationFilePath), ext);

                    CreateSingleRomZip(sourceFilePath, stagedZipPath, entryName);

                    if (File.Exists(stagedZipPath) && new FileInfo(stagedZipPath).Length > 0)
                    {
                        stagedPath = stagedZipPath;
                        UpdateRomFileDestination(romFile, platform, ref destinationFilePath, stagedZipPath, ".zip");
                        return true;
                    }
                }
                else if (ext == ".cue" || ext == ".iso" || ext == ".gdi")
                {
                    discCompressionAttempted = true;
                    if (TryStageDiscCompression(sourceFilePath, romFile, platform, ref tempStagingDir, ref destinationFilePath, out stagedPath))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Pre-move staging failed for {0}; falling back to transferring original file", sourceFilePath);
                stagedPath = null;
            }

            return false;
        }

        private void TransferDiscSheetAndTracks(string sourceSheetPath, string destinationSheetPath, TransferMode transferMode)
        {
            var tracks = DiscSheetHelper.ResolveTrackFiles(sourceSheetPath);
            var sourceBaseName = Path.GetFileNameWithoutExtension(sourceSheetPath);
            var destBaseName = Path.GetFileNameWithoutExtension(destinationSheetPath);
            var destDir = Path.GetDirectoryName(destinationSheetPath);
            var sheetExt = Path.GetExtension(sourceSheetPath) ?? string.Empty;

            var shouldRenameTracks = !string.Equals(sourceBaseName, destBaseName, StringComparison.Ordinal);
            var needsRewrite = shouldRenameTracks || DiscSheetHelper.NeedsTrackReferenceRewrite(sourceSheetPath);

            var trackNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var plannedTransfers = new List<(string SourcePath, string DestPath)>(tracks.Count);

            for (var i = 0; i < tracks.Count; i++)
            {
                var trackPath = tracks[i];
                var destTrackFileName = shouldRenameTracks
                    ? DiscSheetHelper.BuildRenamedTrackFileName(destBaseName, trackPath, i + 1, tracks.Count, sheetExt)
                    : Path.GetFileName(trackPath);

                var destTrackPath = Path.Combine(destDir, destTrackFileName);
                trackNameMap[trackPath] = destTrackFileName;
                plannedTransfers.Add((trackPath, destTrackPath));
            }

            var willWriteRewrittenSheet = (sheetExt.Equals(".cue", StringComparison.OrdinalIgnoreCase) ||
                                           sheetExt.Equals(".gdi", StringComparison.OrdinalIgnoreCase)) &&
                                          tracks.Count > 0 &&
                                          needsRewrite;

            var rewrittenSheetContent = willWriteRewrittenSheet
                ? DiscSheetHelper.RewriteSheetContent(sourceSheetPath, trackNameMap)
                : null;

            foreach (var (trackPath, destTrackPath) in plannedTransfers)
            {
                if (!trackPath.PathEquals(destTrackPath))
                {
                    _diskTransferService.TransferFile(trackPath, destTrackPath, transferMode, File.Exists(destTrackPath));
                }
            }

            if (willWriteRewrittenSheet)
            {
                if (!string.IsNullOrWhiteSpace(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.WriteAllText(destinationSheetPath, rewrittenSheetContent, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                if (transferMode == TransferMode.Move &&
                    !sourceSheetPath.PathEquals(destinationSheetPath) &&
                    File.Exists(sourceSheetPath))
                {
                    File.Delete(sourceSheetPath);
                }
            }
            else if (!sourceSheetPath.PathEquals(destinationSheetPath))
            {
                _diskTransferService.TransferFile(sourceSheetPath, destinationSheetPath, transferMode, File.Exists(destinationSheetPath));
            }
        }

        private static void UpdateRomFileDestination(EpisodeFile romFile, Series platform, ref string destinationFilePath, string actualFilePath, string newExtension = null)
        {
            destinationFilePath = string.IsNullOrWhiteSpace(newExtension)
                ? actualFilePath
                : Path.ChangeExtension(destinationFilePath, newExtension);

            if (!string.IsNullOrWhiteSpace(platform?.Path))
            {
                romFile.RelativePath = platform.Path.GetRelativePath(destinationFilePath);
            }

            romFile.Size = new FileInfo(actualFilePath).Length;
        }

        private bool TryStageDiscCompression(string sourcePath, EpisodeFile romFile, Series platform, ref string tempStagingDir, ref string destinationFilePath, out string stagedPath)
        {
            stagedPath = null;
            if (_discCompressionService == null)
            {
                return false;
            }

            _importActivityService?.SetCurrentActivity("Compressing...");
            var platformSlug = platform?.TitleSlug ?? platform?.Title ?? string.Empty;
            var (_, _, _, predictedOutput) = _discCompressionService.BuildCompressionCommand(sourcePath, platformSlug);
            var targetExt = Path.GetExtension(predictedOutput);
            if (string.IsNullOrWhiteSpace(targetExt))
            {
                targetExt = ".chd";
            }

            if (string.IsNullOrWhiteSpace(tempStagingDir))
            {
                tempStagingDir = Path.Combine(Path.GetTempPath(), "controllarr_stage_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempStagingDir);
            }

            var stagedDiscPath = Path.Combine(tempStagingDir, Path.ChangeExtension(Path.GetFileName(destinationFilePath), targetExt));
            if (_discCompressionService.CompressDisc(sourcePath, platformSlug, out var actualOutput, stagedDiscPath) &&
                !string.IsNullOrWhiteSpace(actualOutput) &&
                File.Exists(actualOutput) &&
                new FileInfo(actualOutput).Length > 0)
            {
                stagedPath = actualOutput;
                UpdateRomFileDestination(romFile, platform, ref destinationFilePath, actualOutput, Path.GetExtension(actualOutput));
                return true;
            }

            return false;
        }

        public static bool ShouldUnwrapArchiveBeforeMove(string sourceFilePath, string ext, bool shouldCompress, Series platform = null)
        {
            if (ext == ".7z" || ext == ".rar")
            {
                return true;
            }

            if (ext != ".zip")
            {
                return false;
            }

            if (!shouldCompress)
            {
                return true;
            }

            try
            {
                using var archive = ZipFile.OpenRead(sourceFilePath);
                var nonDirEntries = archive.Entries
                    .Where(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\"))
                    .ToList();

                if (nonDirEntries.Count != 1)
                {
                    return true;
                }

                var onlyEntry = nonDirEntries[0];
                var onlyExt = Path.GetExtension(onlyEntry.Name);
                return ArchiveRepackService.IsJunkEntry(onlyEntry.FullName, onlyEntry.Name) ||
                       ArchiveRepackService.IsArchiveExtension(onlyExt) ||
                       onlyEntry.FullName.Contains('/') ||
                       onlyEntry.FullName.Contains('\\') ||
                       !IsZipCompressibleRom(onlyExt, platform);
            }
            catch
            {
                return false;
            }
        }

        private static void CreateSingleRomZip(string sourceRomPath, string stagedZipPath, string entryName)
        {
            using var zipArchive = ZipFile.Open(stagedZipPath, ZipArchiveMode.Create);
            zipArchive.CreateEntryFromFile(sourceRomPath, entryName, CompressionLevel.Optimal);
        }

        private void DeleteDiscSourceAndTracks(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return;
            }

            var ext = Path.GetExtension(sourceFilePath)?.ToLowerInvariant() ?? string.Empty;
            var filesToDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sourceFilePath };

            if (DiscSheetHelper.IsDiscSheetExtension(ext) && File.Exists(sourceFilePath))
            {
                try
                {
                    foreach (var trackPath in DiscSheetHelper.ResolveTrackFiles(sourceFilePath))
                    {
                        filesToDelete.Add(trackPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to parse disc tracks for cleanup from {0}", sourceFilePath);
                }
            }

            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unable to delete original file after compression: {0}", file);
                }
            }
        }

        public static bool ShouldPostProcessInPlace(string filePath, Series platform)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            var shouldCompress = IsPlatformCompressionEnabled(platform);

            if (shouldCompress && IsZipCompressibleRom(ext, platform, filePath))
            {
                return true;
            }

            if (ArchiveRepackService.IsArchiveExtension(ext) &&
                ShouldUnwrapArchiveBeforeMove(filePath, ext, shouldCompress, platform))
            {
                return true;
            }

            if (shouldCompress && (ext == ".cue" || ext == ".iso" || ext == ".gdi"))
            {
                return true;
            }

            return false;
        }

        private void PostProcessRomFile(ref string destinationFilePath, EpisodeFile romFile, Series platform, bool discCompressionAttempted = false)
        {
            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                return;
            }

            var ext = Path.GetExtension(destinationFilePath)?.ToLowerInvariant() ?? string.Empty;
            var shouldCompress = IsPlatformCompressionEnabled(platform);

            try
            {
                if (ArchiveRepackService.IsArchiveExtension(ext) && File.Exists(destinationFilePath))
                {
                    if (ShouldUnwrapArchiveBeforeMove(destinationFilePath, ext, shouldCompress, platform))
                    {
                        var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_post_" + Guid.NewGuid().ToString("N"));
                        var destDir = Path.GetDirectoryName(destinationFilePath);
                        var destBaseName = Path.GetFileNameWithoutExtension(destinationFilePath);
                        var originalArchivePath = destinationFilePath;

                        try
                        {
                            Directory.CreateDirectory(tempDir);
                            if (!string.IsNullOrWhiteSpace(destDir) &&
                                ArchiveRepackService.TryUnwrapGameFromArchive(originalArchivePath, tempDir, out var unwrappedRomPath, out var companionTracks))
                            {
                                var innerExt = Path.GetExtension(unwrappedRomPath)?.ToLowerInvariant() ?? string.Empty;
                                if (DiscSheetHelper.IsDiscSheetExtension(innerExt))
                                {
                                    DiscSheetHelper.RepairSheetInPlaceIfNeeded(unwrappedRomPath);
                                }

                                if (shouldCompress &&
                                    companionTracks.Count == 0 &&
                                    IsZipCompressibleRom(innerExt, platform, unwrappedRomPath))
                                {
                                    var finalZipPath = Path.Combine(destDir, destBaseName + ".zip");
                                    var stagedZipPath = Path.Combine(tempDir, destBaseName + ".zip");
                                    CreateSingleRomZip(unwrappedRomPath, stagedZipPath, destBaseName + innerExt);
                                    File.Copy(stagedZipPath, finalZipPath, overwrite: true);
                                    UpdateRomFileDestination(romFile, platform, ref destinationFilePath, finalZipPath);
                                    ext = ".zip";
                                    if (File.Exists(originalArchivePath) && !originalArchivePath.PathEquals(destinationFilePath))
                                    {
                                        File.Delete(originalArchivePath);
                                    }
                                }
                                else if (shouldCompress &&
                                         (innerExt == ".cue" || innerExt == ".iso" || innerExt == ".gdi") &&
                                         _discCompressionService != null)
                                {
                                    discCompressionAttempted = true;
                                    var tempStaging = tempDir;
                                    var compressedTarget = Path.Combine(destDir, destBaseName + ".chd");
                                    if (TryStageDiscCompression(unwrappedRomPath, romFile, platform, ref tempStaging, ref compressedTarget, out var stagedDiscPath))
                                    {
                                        var finalDiscPath = Path.Combine(destDir, Path.GetFileName(compressedTarget));
                                        _diskTransferService.TransferFile(stagedDiscPath, finalDiscPath, TransferMode.Move, File.Exists(finalDiscPath));
                                        UpdateRomFileDestination(romFile, platform, ref destinationFilePath, finalDiscPath);
                                        ext = Path.GetExtension(destinationFilePath)?.ToLowerInvariant() ?? ext;
                                        if (File.Exists(originalArchivePath) && !originalArchivePath.PathEquals(destinationFilePath))
                                        {
                                            File.Delete(originalArchivePath);
                                        }
                                    }
                                    else
                                    {
                                        var finalUncompressedPath = Path.Combine(destDir, destBaseName + innerExt);
                                        if (DiscSheetHelper.IsDiscSheetExtension(innerExt))
                                        {
                                            TransferDiscSheetAndTracks(unwrappedRomPath, finalUncompressedPath, TransferMode.Move);
                                        }
                                        else
                                        {
                                            _diskTransferService.TransferFile(unwrappedRomPath, finalUncompressedPath, TransferMode.Move, File.Exists(finalUncompressedPath));
                                        }

                                        UpdateRomFileDestination(romFile, platform, ref destinationFilePath, finalUncompressedPath);
                                        ext = innerExt;
                                        if (File.Exists(originalArchivePath) && !originalArchivePath.PathEquals(destinationFilePath))
                                        {
                                            File.Delete(originalArchivePath);
                                        }
                                    }
                                }
                                else
                                {
                                    var finalUncompressedPath = Path.Combine(destDir, destBaseName + innerExt);
                                    if (DiscSheetHelper.IsDiscSheetExtension(innerExt))
                                    {
                                        TransferDiscSheetAndTracks(unwrappedRomPath, finalUncompressedPath, TransferMode.Move);
                                    }
                                    else
                                    {
                                        _diskTransferService.TransferFile(unwrappedRomPath, finalUncompressedPath, TransferMode.Move, File.Exists(finalUncompressedPath));
                                    }

                                    UpdateRomFileDestination(romFile, platform, ref destinationFilePath, finalUncompressedPath);
                                    ext = innerExt;
                                    if (File.Exists(originalArchivePath) && !originalArchivePath.PathEquals(destinationFilePath))
                                    {
                                        File.Delete(originalArchivePath);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            if (Directory.Exists(tempDir))
                            {
                                try
                                {
                                    Directory.Delete(tempDir, true);
                                }
                                catch (Exception ex)
                                {
                                    _logger.Debug(ex, "Unable to clean up temp post-process folder {0}", tempDir);
                                }
                            }
                        }
                    }
                    else if (shouldCompress && ext == ".zip" && _archiveRepackService != null)
                    {
                        _importActivityService?.SetCurrentActivity("Compressing...");
                        _archiveRepackService.SyncSingleRomEntryName(destinationFilePath);
                        _archiveRepackService.RepackArchive(destinationFilePath);
                        romFile.Size = new FileInfo(destinationFilePath).Length;
                    }
                }
                else if (shouldCompress && File.Exists(destinationFilePath) && IsZipCompressibleRom(ext, platform, destinationFilePath))
                {
                    _importActivityService?.SetCurrentActivity("Compressing...");
                    if (ArchiveRepackService.TryCompressFileToZipInPlace(destinationFilePath, out var zipPath, _logger))
                    {
                        UpdateRomFileDestination(romFile, platform, ref destinationFilePath, zipPath);
                        ext = ".zip";
                    }
                }
                else if (shouldCompress && !discCompressionAttempted && _discCompressionService != null && (ext == ".cue" || ext == ".iso" || ext == ".gdi"))
                {
                    _importActivityService?.SetCurrentActivity("Compressing...");
                    var platformSlug = platform?.TitleSlug ?? platform?.Title ?? string.Empty;
                    var originalSourcePath = destinationFilePath;
                    if (_discCompressionService.CompressDisc(originalSourcePath, platformSlug, out var compressedDiscPath) &&
                        !string.IsNullOrWhiteSpace(compressedDiscPath) &&
                        File.Exists(compressedDiscPath) &&
                        new FileInfo(compressedDiscPath).Length > 0)
                    {
                        DeleteDiscSourceAndTracks(originalSourcePath);
                        UpdateRomFileDestination(romFile, platform, ref destinationFilePath, compressedDiscPath);
                        ext = Path.GetExtension(destinationFilePath)?.ToLowerInvariant() ?? ext;
                    }
                }

                if (_discCompressionService != null && (ext == ".chd" || ext == ".rvz" || ext == ".cue" || ext == ".iso"))
                {
                    var targetDir = Path.GetDirectoryName(destinationFilePath);
                    var rawBaseTitle = Path.GetFileNameWithoutExtension(destinationFilePath);

                    if (!string.IsNullOrWhiteSpace(targetDir) &&
                        Directory.Exists(targetDir) &&
                        DiscSuffixRegex.IsMatch(rawBaseTitle))
                    {
                        var strippedBaseTitle = StripDiscSuffix(rawBaseTitle);
                        if (!string.IsNullOrWhiteSpace(strippedBaseTitle))
                        {
                            var adjacentDiscs = Directory.GetFiles(targetDir)
                                .Where(f =>
                                {
                                    var e = Path.GetExtension(f)?.ToLowerInvariant();
                                    if (e != ".chd" && e != ".rvz" && e != ".cue" && e != ".iso")
                                    {
                                        return false;
                                    }

                                    var name = Path.GetFileNameWithoutExtension(f);
                                    return DiscSuffixRegex.IsMatch(name) &&
                                           string.Equals(StripDiscSuffix(name), strippedBaseTitle, StringComparison.OrdinalIgnoreCase);
                                })
                                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                .ToArray();

                            if (adjacentDiscs.Length > 1)
                            {
                                var m3uPath = Path.Combine(targetDir, strippedBaseTitle + ".m3u");
                                _discCompressionService.GenerateM3uPlaylist(adjacentDiscs, m3uPath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to complete ROM post-processing for {0}", destinationFilePath);
            }
        }

        private static string StripDiscSuffix(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return string.Empty;
            }

            var stripped = DiscSuffixRegex.Replace(fileNameWithoutExtension, string.Empty);
            return Regex.Replace(stripped, @"\s{2,}", " ").Trim();
        }

        private void EnsureGameFolder(EpisodeFile romFile, LocalEpisode localGame, string filePath)
        {
            EnsureGameFolder(romFile, localGame.Series, localGame.SeasonNumber, filePath);
        }

        private void EnsureGameFolder(EpisodeFile romFile, Series platform, int regionNumber, string filePath)
        {
            var gameFolder = Path.GetDirectoryName(filePath);
            var regionFolder = _buildFileNames.BuildSeasonPath(platform, regionNumber);
            var platformFolder = platform.Path;
            var rootFolder = _rootFolderService.GetBestRootFolderPath(platformFolder);

            if (rootFolder.IsNullOrWhiteSpace())
            {
                throw new RootFolderNotFoundException($"Root folder was not found, '{platformFolder}' is not a subdirectory of a defined root folder.");
            }

            if (!_diskProvider.FolderExists(rootFolder))
            {
                throw new RootFolderNotFoundException($"Root folder '{rootFolder}' was not found.");
            }

            var changed = false;
            var newEvent = new GameFolderCreatedEvent(platform, romFile);

            if (!_diskProvider.FolderExists(platformFolder))
            {
                CreateFolder(platformFolder);
                newEvent.SeriesFolder = platformFolder;
                changed = true;
            }

            if (platformFolder != regionFolder && !_diskProvider.FolderExists(regionFolder))
            {
                CreateFolder(regionFolder);
                newEvent.SeasonFolder = regionFolder;
                changed = true;
            }

            if (regionFolder != gameFolder && !_diskProvider.FolderExists(gameFolder))
            {
                CreateFolder(gameFolder);
                newEvent.EpisodeFolder = gameFolder;
                changed = true;
            }

            if (changed)
            {
                _eventAggregator.PublishEvent(newEvent);
            }
        }

        private void CreateFolder(string directoryName)
        {
            Ensure.That(directoryName, () => directoryName).IsNotNullOrWhiteSpace();

            var parentFolder = new OsPath(directoryName).Directory.FullPath;
            if (!_diskProvider.FolderExists(parentFolder))
            {
                CreateFolder(parentFolder);
            }

            try
            {
                _diskProvider.CreateFolder(directoryName);
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Unable to create directory: {0}", directoryName);
            }

            _mediaFileAttributeService.SetFolderPermissions(directoryName);
        }
    }
}
