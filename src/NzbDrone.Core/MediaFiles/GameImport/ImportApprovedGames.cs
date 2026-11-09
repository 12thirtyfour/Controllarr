using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.GameImport
{
    public interface IImportApprovedGames
    {
        List<ImportResult> Import(List<ImportDecision> decisions, bool newDownload, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto);
    }

    public class ImportApprovedGames : IImportApprovedGames
    {
        private readonly IUpgradeMediaFiles _romFileUpgrader;
        private readonly IMediaFileService _mediaFileService;
        private readonly IExtraService _extraService;
        private readonly IExistingExtraFiles _existingExtraFiles;
        private readonly IDiskProvider _diskProvider;
        private readonly IHistoryService _historyService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly Logger _logger;

        public ImportApprovedGames(IUpgradeMediaFiles romFileUpgrader,
                                      IMediaFileService mediaFileService,
                                      IExtraService extraService,
                                      IExistingExtraFiles existingExtraFiles,
                                      IDiskProvider diskProvider,
                                      IHistoryService historyService,
                                      IEventAggregator eventAggregator,
                                      IManageCommandQueue commandQueueManager,
                                      Logger logger,
                                      IBuildFileNames fileNameBuilder = null)
        {
            _romFileUpgrader = romFileUpgrader;
            _mediaFileService = mediaFileService;
            _extraService = extraService;
            _existingExtraFiles = existingExtraFiles;
            _diskProvider = diskProvider;
            _historyService = historyService;
            _eventAggregator = eventAggregator;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
            _fileNameBuilder = fileNameBuilder;
        }

        public List<ImportResult> Import(List<ImportDecision> decisions, bool newDownload, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto)
        {
            var qualifiedImports = decisions
                .Where(decision => decision.Approved)
                .GroupBy(decision => decision.LocalEpisode.Series.Id)
                .SelectMany(group => group
                    .OrderByDescending(decision => decision.LocalEpisode.Quality, new QualityModelComparer(group.First().LocalEpisode.Series.QualityProfile))
                    .ThenByDescending(decision => decision.LocalEpisode.Size))
                .ToList();

            var importResults = new List<ImportResult>();

            foreach (var importDecision in qualifiedImports.OrderBy(e => e.LocalEpisode.Episodes.Select(game => game.EpisodeNumber).MinOrDefault())
                                                           .ThenByDescending(e => e.LocalEpisode.Size))
            {
                var localGame = importDecision.LocalEpisode;
                var oldFiles = new List<DeletedRomFile>();

                try
                {
                    // check if already imported
                    if (importResults.SelectMany(r => r.ImportDecision.LocalEpisode.Episodes)
                                         .Select(e => e.Id)
                                         .Intersect(localGame.Episodes.Select(e => e.Id))
                                         .Any())
                    {
                        importResults.Add(new ImportResult(importDecision, "Game has already been imported"));
                        continue;
                    }

                    var romFile = localGame.ToEpisodeFile();
                    romFile.Size = _diskProvider.GetFileSize(localGame.Path);

                    if (downloadClientItem?.DownloadId.IsNotNullOrWhiteSpace() == true)
                    {
                        var grabHistory = _historyService.FindByDownloadId(downloadClientItem.DownloadId)
                            .OrderByDescending(h => h.Date)
                            .FirstOrDefault(h => h.EventType == GameHistoryEventType.Grabbed);

                        if (Enum.TryParse(grabHistory?.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                        {
                            romFile.IndexerFlags = flags;
                        }

                        // Prefer the release type from the grabbed history
                        if (Enum.TryParse(grabHistory?.Data.GetValueOrDefault("releaseType"), true, out ReleaseType releaseType) &&
                            releaseType != ReleaseType.Unknown)
                        {
                            romFile.ReleaseType = releaseType;
                        }
                    }

                    bool copyOnly;
                    switch (importMode)
                    {
                        default:
                        case ImportMode.Auto:
                            copyOnly = downloadClientItem is { CanMoveFiles: false };
                            break;
                        case ImportMode.Move:
                            copyOnly = false;
                            break;
                        case ImportMode.Copy:
                            copyOnly = true;
                            break;
                    }

                    var requiresInPlaceOrganization = !newDownload && ShouldOrganizeExistingRomFile(localGame, romFile);

                    if (newDownload || requiresInPlaceOrganization)
                    {
                        if (requiresInPlaceOrganization && localGame.Series != null)
                        {
                            localGame.Series.SeasonFolder = true;
                        }

                        if (downloadClientItem is { OutputPath.IsEmpty: false })
                        {
                            var outputDirectory = downloadClientItem.OutputPath.Directory.ToString();

                            if (outputDirectory.IsParentPath(localGame.Path))
                            {
                                romFile.OriginalFilePath = outputDirectory.GetRelativePath(localGame.Path);
                            }
                        }

                        oldFiles = _romFileUpgrader.UpgradeEpisodeFile(romFile, localGame, copyOnly).OldFiles;
                    }
                    else
                    {
                        // Delete existing files from the DB mapped to this path
                        var previousFiles = _mediaFileService.GetFilesWithRelativePath(localGame.Series.Id, romFile.RelativePath);

                        foreach (var previousFile in previousFiles)
                        {
                            _mediaFileService.Delete(previousFile, DeleteMediaFileReason.ManualOverride);
                        }
                    }

                    romFile = _mediaFileService.Add(romFile);
                    importResults.Add(new ImportResult(importDecision, romFile));

                    if (newDownload)
                    {
                        if (localGame.ScriptImported)
                        {
                            _existingExtraFiles.ImportExtraFiles(localGame.Series, localGame.PossibleExtraFiles, localGame.FileNameBeforeRename);

                            if (localGame.FileNameBeforeRename != romFile.RelativePath)
                            {
                                _extraService.MoveFilesAfterRename(localGame.Series, romFile);
                            }
                        }

                        if (!localGame.ScriptImported || localGame.ShouldImportExtras)
                        {
                            _extraService.ImportEpisode(localGame, romFile, copyOnly);
                        }
                    }

                    _eventAggregator.PublishEvent(new GameImportedEvent(localGame, romFile, oldFiles, newDownload, downloadClientItem));
                }
                catch (RootFolderNotFoundException e)
                {
                    _logger.Warn(e, "Couldn't import game " + localGame);
                    _eventAggregator.PublishEvent(new GameImportFailedEvent(e, localGame, newDownload, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import game, Root folder missing."));
                }
                catch (DestinationAlreadyExistsException e)
                {
                    _logger.Warn(e, "Couldn't import game " + localGame);
                    importResults.Add(new ImportResult(importDecision, "Failed to import game, Destination already exists."));

                    _commandQueueManager.Push(new RescanPlatformCommand(localGame.Series.Id));
                }
                catch (RecycleBinException e)
                {
                    _logger.Warn(e, "Couldn't import game " + localGame);
                    _eventAggregator.PublishEvent(new GameImportFailedEvent(e, localGame, newDownload, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import game, unable to move existing file to the Recycle Bin."));
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "Couldn't import game " + localGame);
                    importResults.Add(new ImportResult(importDecision, "Failed to import game"));
                }
            }

            // Adding all the rejected decisions
            importResults.AddRange(decisions.Where(c => !c.Approved)
                                            .Select(d => new ImportResult(d, d.Rejections.Select(r => r.Message).ToArray())));

            return importResults;
        }

        private bool ShouldOrganizeExistingRomFile(LocalEpisode localGame, EpisodeFile romFile)
        {
            if (localGame?.Series == null ||
                string.IsNullOrWhiteSpace(localGame.Path) ||
                string.IsNullOrWhiteSpace(romFile?.RelativePath))
            {
                return false;
            }

            var ext = Path.GetExtension(localGame.Path);
            if (string.IsNullOrWhiteSpace(ext) || !MediaFileExtensions.RomExtensions.Contains(ext))
            {
                return false;
            }

            if (!_diskProvider.FileExists(localGame.Path))
            {
                return false;
            }

            var relativeDir = Path.GetDirectoryName(romFile.RelativePath);
            if (string.IsNullOrWhiteSpace(relativeDir))
            {
                return true;
            }

            var shouldCompress = RomFileMovingService.IsPlatformCompressionEnabled(localGame.Series);
            if (shouldCompress && RomFileMovingService.IsZipCompressibleRom(ext, localGame.Series, localGame.Path))
            {
                return true;
            }

            if (!shouldCompress && ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (_fileNameBuilder != null && localGame.Episodes != null && localGame.Episodes.Any())
            {
                try
                {
                    localGame.Series.SeasonFolder = true;
                    var expectedFilePath = _fileNameBuilder.BuildFilePath(
                        localGame.Episodes,
                        localGame.Series,
                        romFile,
                        ext,
                        null,
                        localGame.CustomFormats);

                    if (!string.IsNullOrWhiteSpace(expectedFilePath) && !localGame.Path.PathEquals(expectedFilePath))
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }
}
