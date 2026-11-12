using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.Synchronization;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
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
        private readonly IArchiveExtractionService _archiveExtractionService;
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
                                IArchiveExtractionService archiveExtractionService = null)
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
            _archiveExtractionService = archiveExtractionService;
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
                _diskTransferService.TransferFile(romFilePath, destinationFilePath, mode, File.Exists(destinationFilePath));
            }

            var sidecarMode = (mode == TransferMode.Copy || mode == TransferMode.HardLinkOrCopy)
                ? ImportMode.Copy
                : ImportMode.Move;
            _sidecarSaveService?.RenameAdjacentSaves(romFilePath, destinationFilePath, sidecarMode);

            PostProcessRomFile(ref destinationFilePath, romFile, platform);

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

        private static bool ShouldPostProcessInPlace(string filePath, Series platform)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
            var shouldCompress = IsPlatformCompressionEnabled(platform);

            if (shouldCompress && IsZipCompressibleRom(ext, platform, filePath))
            {
                return true;
            }

            if (!shouldCompress && ext == ".zip")
            {
                return true;
            }

            return false;
        }

        private void PostProcessRomFile(ref string destinationFilePath, EpisodeFile romFile, Series platform)
        {
            if (string.IsNullOrWhiteSpace(destinationFilePath))
            {
                return;
            }

            var ext = Path.GetExtension(destinationFilePath)?.ToLowerInvariant() ?? string.Empty;
            var shouldCompress = IsPlatformCompressionEnabled(platform);

            try
            {
                if (ext == ".nes" && _archiveRepackService != null && File.Exists(destinationFilePath))
                {
                    var romBytes = File.ReadAllBytes(destinationFilePath);
                    var modernized = _archiveRepackService.ModernizeINesHeader(romBytes);
                    if (modernized != null && !ReferenceEquals(romBytes, modernized))
                    {
                        File.WriteAllBytes(destinationFilePath, modernized);
                    }
                }

                if (ext == ".zip")
                {
                    if (!shouldCompress && _archiveExtractionService != null && File.Exists(destinationFilePath))
                    {
                        var destDir = Path.GetDirectoryName(destinationFilePath);
                        if (!string.IsNullOrWhiteSpace(destDir))
                        {
                            _archiveExtractionService.ExtractArchive(destinationFilePath, destDir, false);
                        }
                    }
                    else if (shouldCompress && _archiveRepackService != null && File.Exists(destinationFilePath))
                    {
                        _archiveRepackService.RepackArchive(destinationFilePath);
                        _archiveRepackService.SyncSingleRomEntryName(destinationFilePath);
                    }
                }
                else if (shouldCompress && File.Exists(destinationFilePath) && IsZipCompressibleRom(ext, platform, destinationFilePath))
                {
                    var zipPath = Path.ChangeExtension(destinationFilePath, ".zip");
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }

                    using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        zipArchive.CreateEntryFromFile(destinationFilePath, Path.GetFileName(destinationFilePath), CompressionLevel.Optimal);
                    }

                    if (File.Exists(zipPath))
                    {
                        File.Delete(destinationFilePath);
                        destinationFilePath = zipPath;
                        ext = ".zip";
                        if (!string.IsNullOrWhiteSpace(platform?.Path))
                        {
                            romFile.RelativePath = platform.Path.GetRelativePath(destinationFilePath);
                        }

                        romFile.Size = new FileInfo(destinationFilePath).Length;
                    }
                }
                else if (shouldCompress && _discCompressionService != null && (ext == ".cue" || ext == ".iso" || ext == ".gdi"))
                {
                    var platformSlug = platform?.TitleSlug ?? platform?.Title ?? string.Empty;
                    if (_discCompressionService.CompressDisc(destinationFilePath, platformSlug, out var compressedDiscPath) &&
                        !string.IsNullOrWhiteSpace(compressedDiscPath))
                    {
                        destinationFilePath = compressedDiscPath;
                        ext = Path.GetExtension(destinationFilePath)?.ToLowerInvariant() ?? ext;
                        if (!string.IsNullOrWhiteSpace(platform?.Path))
                        {
                            romFile.RelativePath = platform.Path.GetRelativePath(destinationFilePath);
                        }
                    }
                }

                if (_discCompressionService != null && (ext == ".chd" || ext == ".rvz" || ext == ".cue" || ext == ".iso"))
                {
                    var targetDir = Path.GetDirectoryName(destinationFilePath);
                    if (!string.IsNullOrWhiteSpace(targetDir) && Directory.Exists(targetDir))
                    {
                        var adjacentDiscs = Directory.GetFiles(targetDir)
                            .Where(f =>
                            {
                                var e = Path.GetExtension(f)?.ToLowerInvariant();
                                return e == ".chd" || e == ".rvz" || e == ".cue" || e == ".iso";
                            })
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        if (adjacentDiscs.Length > 1)
                        {
                            var baseTitle = Path.GetFileNameWithoutExtension(destinationFilePath);
                            var m3uPath = Path.Combine(targetDir, baseTitle + ".m3u");
                            if (!File.Exists(m3uPath))
                            {
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
