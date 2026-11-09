using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Tv
{
    public interface IAddPlatformService
    {
        Series AddSeries(Series newSeries);
        List<Series> AddSeries(List<Series> newSeries, bool ignoreErrors = false);
    }

    public class AddPlatformService : IAddPlatformService
    {
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IProvidePlatformInfo _seriesInfo;
        private readonly IGameMetadataProxy _gameMetadataProxy;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IAddPlatformValidator _addSeriesValidator;
        private readonly IDiskProvider _diskProvider;
        private readonly IRootFolderService _rootFolderService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public AddPlatformService(ISeriesService seriesService,
                                IEpisodeService episodeService,
                                IProvidePlatformInfo seriesInfo,
                                IGameMetadataProxy gameMetadataProxy,
                                IGameCatalogService gameCatalogService,
                                IBuildFileNames fileNameBuilder,
                                IAddPlatformValidator addSeriesValidator,
                                IDiskProvider diskProvider,
                                IRootFolderService rootFolderService,
                                IManageCommandQueue commandQueueManager,
                                Logger logger)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _seriesInfo = seriesInfo;
            _gameMetadataProxy = gameMetadataProxy;
            _gameCatalogService = gameCatalogService;
            _fileNameBuilder = fileNameBuilder;
            _addSeriesValidator = addSeriesValidator;
            _diskProvider = diskProvider;
            _rootFolderService = rootFolderService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public Series AddSeries(Series newSeries)
        {
            Ensure.That(newSeries, () => newSeries).IsNotNull();

            if (_gameCatalogService != null &&
                _gameCatalogService.TryGetGameEntry(newSeries.TvdbId, out var gameEntry))
            {
                return AddGameOrParentPlatform(newSeries, gameEntry);
            }

            var existingPlatform = _seriesService.FindByTvdbId(newSeries.TvdbId);
            if (existingPlatform == null && !string.IsNullOrWhiteSpace(newSeries.Title))
            {
                existingPlatform = _seriesService.FindByTitle(newSeries.Title);
            }

            if (existingPlatform == null && !string.IsNullOrWhiteSpace(newSeries.Path))
            {
                existingPlatform = _seriesService.FindByPath(newSeries.Path);
            }

            if (existingPlatform == null && !string.IsNullOrWhiteSpace(newSeries.TitleSlug))
            {
                existingPlatform = _seriesService.GetAllSeries()
                    .FirstOrDefault(s => string.Equals(s.TitleSlug, newSeries.TitleSlug, StringComparison.OrdinalIgnoreCase));
            }

            if (existingPlatform != null)
            {
                _logger.Info("Platform {0} already exists in library (ID {1}); importing incoming content into library", existingPlatform.Title, existingPlatform.Id);
                ImportIncomingContentToExistingPlatform(newSeries, existingPlatform);
                return existingPlatform;
            }

            newSeries = AddSkyhookData(newSeries);
            newSeries = SetPropertiesAndValidate(newSeries);

            _logger.Info("Adding Platform {0} Path: [{1}]", newSeries, newSeries.Path);
            _seriesService.AddSeries(newSeries);
            EnsurePlatformFoldersCreated(newSeries);

            return newSeries;
        }

        public List<Series> AddSeries(List<Series> newSeries, bool ignoreErrors = false)
        {
            var added = DateTime.UtcNow;
            var seriesToAdd = new List<Series>();
            var existingSeriesToReturn = new List<Series>();
            var existingSeriesTvdbIds = _seriesService.AllSeriesTvdbIds();

            foreach (var s in newSeries)
            {
                if (s.Path.IsNullOrWhiteSpace())
                {
                    _logger.Info("Adding Platform {0} Root Folder Path: [{1}]", s, s.RootFolderPath);
                }
                else
                {
                    _logger.Info("Adding Platform {0} Path: [{1}]", s, s.Path);
                }

                try
                {
                    if (_gameCatalogService != null &&
                        _gameCatalogService.TryGetGameEntry(s.TvdbId, out var gameEntry))
                    {
                        _gameCatalogService.RegisterGameSelection(gameEntry.TvdbId);
                        s.TvdbId = gameEntry.PlatformTvdbId;
                        s.Title = gameEntry.PlatformName;
                        s.TitleSlug = gameEntry.PlatformSlug;
                        s.Path = null;
                        s.Seasons = null;
                    }

                    var existing = _seriesService.FindByTvdbId(s.TvdbId);
                    if (existing == null && !string.IsNullOrWhiteSpace(s.Title))
                    {
                        existing = _seriesService.FindByTitle(s.Title);
                    }

                    if (existing == null && !string.IsNullOrWhiteSpace(s.Path))
                    {
                        existing = _seriesService.FindByPath(s.Path);
                    }

                    if (existing == null && !string.IsNullOrWhiteSpace(s.TitleSlug))
                    {
                        existing = _seriesService.GetAllSeries()
                            .FirstOrDefault(p => string.Equals(p.TitleSlug, s.TitleSlug, StringComparison.OrdinalIgnoreCase));
                    }

                    if (existing != null)
                    {
                        _logger.Info("Platform ID {0} was already present: Platform {1} exists in database as {2} (ID {3}); importing incoming files into existing library", s.TvdbId, s.Title, existing.Title, existing.Id);
                        ImportIncomingContentToExistingPlatform(s, existing);
                        if (!existingSeriesToReturn.Any(e => e.Id == existing.Id))
                        {
                            existingSeriesToReturn.Add(existing);
                        }

                        continue;
                    }

                    var series = AddSkyhookData(s);
                    series = SetPropertiesAndValidate(series);
                    series.Added = added;

                    if (seriesToAdd.Any(f => f.TvdbId == series.TvdbId ||
                                             string.Equals(f.Title, series.Title, StringComparison.OrdinalIgnoreCase) ||
                                             (!string.IsNullOrWhiteSpace(f.TitleSlug) && string.Equals(f.TitleSlug, series.TitleSlug, StringComparison.OrdinalIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(f.Path) && string.Equals(f.Path, series.Path, StringComparison.OrdinalIgnoreCase))))
                    {
                        _logger.Trace("Platform ID {0} ({1}) was already queued for addition, not adding platform again", s.TvdbId, s.Title);
                        continue;
                    }

                    seriesToAdd.Add(series);
                }
                catch (ValidationException ex)
                {
                    if (!ignoreErrors)
                    {
                        throw;
                    }

                    _logger.Debug("Platform {0} with ID {1} was not added due to validation failures. {2}", s, s.TvdbId, ex.Message);
                }
            }

            var addedSeries = SaveAddedSeries(seriesToAdd);
            addedSeries.AddRange(existingSeriesToReturn);

            return addedSeries;
        }

        private List<Series> SaveAddedSeries(List<Series> seriesToAdd)
        {
            if (seriesToAdd.Empty())
            {
                return new List<Series>();
            }

            var addedSeries = _seriesService.AddSeries(seriesToAdd) ?? new List<Series>();

            foreach (var series in addedSeries)
            {
                EnsurePlatformFoldersCreated(series);
            }

            return addedSeries;
        }

        private Series AddGameOrParentPlatform(Series newSeries, GameCatalogEntry gameEntry)
        {
            _gameCatalogService.RegisterGameSelection(gameEntry.TvdbId);

            var existingPlatform = _seriesService.FindByTvdbId(gameEntry.PlatformTvdbId) ??
                                   _seriesService.FindByTitle(gameEntry.PlatformName);

            if (existingPlatform != null)
            {
                _logger.Info("Platform {0} already exists; monitoring game {1} under {2}", existingPlatform.Title, gameEntry.Title, gameEntry.RegionName);

                var existingEpisodes = _episodeService?.GetEpisodeBySeries(existingPlatform.Id) ?? new List<Episode>();
                var matchingEpisode = existingEpisodes.FirstOrDefault(e =>
                    e.TvdbId == gameEntry.TvdbId ||
                    string.Equals(e.Title, gameEntry.Title, StringComparison.OrdinalIgnoreCase));

                if (matchingEpisode != null)
                {
                    if (!matchingEpisode.Monitored)
                    {
                        _episodeService.SetEpisodeMonitored(matchingEpisode.Id, true);
                    }
                }
                else if (_episodeService != null)
                {
                    var nextEpisodeNum = existingEpisodes
                        .Where(e => e.SeasonNumber == gameEntry.RegionNumber)
                        .Select(e => e.EpisodeNumber)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    gameEntry.GameNumber = nextEpisodeNum;
                    var newEpisode = _gameCatalogService.CreateEpisodeForGame(gameEntry, existingPlatform.Id, true);
                    _episodeService.InsertMany(new List<Episode> { newEpisode });
                }

                var regionSeason = existingPlatform.Seasons?.FirstOrDefault(s => s.SeasonNumber == gameEntry.RegionNumber);
                if (regionSeason != null && !regionSeason.Monitored)
                {
                    regionSeason.Monitored = true;
                }

                if (!existingPlatform.Monitored)
                {
                    existingPlatform.Monitored = true;
                }

                _seriesService.UpdateSeries(existingPlatform, false);
                _gameCatalogService.ClearPendingPlatformSelections(gameEntry.PlatformTvdbId);

                return existingPlatform;
            }

            if (string.IsNullOrWhiteSpace(newSeries.RootFolderPath))
            {
                var defaultRoot = _rootFolderService?.All()?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Path))?.Path;

                if (string.IsNullOrWhiteSpace(defaultRoot))
                {
                    var anySeries = _seriesService.GetAllSeries()?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Path));
                    if (anySeries != null)
                    {
                        defaultRoot = !string.IsNullOrWhiteSpace(anySeries.RootFolderPath)
                            ? anySeries.RootFolderPath
                            : Path.GetDirectoryName(anySeries.Path);
                    }
                }

                if (!string.IsNullOrWhiteSpace(defaultRoot))
                {
                    newSeries.RootFolderPath = defaultRoot;
                }
            }

            newSeries.TvdbId = gameEntry.PlatformTvdbId;
            newSeries.Title = gameEntry.PlatformName;
            newSeries.TitleSlug = gameEntry.PlatformSlug;
            newSeries.Path = null;
            newSeries.Seasons = null;
            newSeries.Monitored = true;

            if (newSeries.AddOptions != null)
            {
                newSeries.AddOptions.Monitor = MonitorTypes.Skip;
            }

            newSeries = AddSkyhookData(newSeries);
            newSeries = SetPropertiesAndValidate(newSeries);

            _logger.Info("Auto-provisioning Platform {0} for Game {1} at Path: [{2}]", newSeries.Title, gameEntry.Title, newSeries.Path);
            _seriesService.AddSeries(newSeries);
            EnsurePlatformFoldersCreated(newSeries);

            return newSeries;
        }

        private void EnsurePlatformFoldersCreated(Series series)
        {
            if (_diskProvider == null || string.IsNullOrWhiteSpace(series?.Path))
            {
                return;
            }

            try
            {
                if (!_diskProvider.FolderExists(series.Path))
                {
                    _diskProvider.CreateFolder(series.Path);
                }

                var candidateArchive = string.Empty;
                if (_diskProvider.FileExists(series.Path) &&
                    (series.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                     series.Path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)))
                {
                    candidateArchive = series.Path;
                }
                else if (_diskProvider.FileExists(series.Path + ".zip"))
                {
                    candidateArchive = series.Path + ".zip";
                }
                else if (_diskProvider.FileExists(series.Path + ".7z"))
                {
                    candidateArchive = series.Path + ".7z";
                }

                if (!string.IsNullOrWhiteSpace(candidateArchive) && _diskProvider.FileExists(candidateArchive))
                {
                    ExtractArchiveToDirectory(candidateArchive, series.Path, series);
                }

                var biosPath = Path.Combine(series.Path, "BIOS");
                if (!_diskProvider.FolderExists(biosPath))
                {
                    _diskProvider.CreateFolder(biosPath);
                }

                if (series.SeasonFolder && series.Seasons != null)
                {
                    MigrateLegacySeasonFolders(series.Path);

                    foreach (var season in series.Seasons.Where(s => s.Monitored))
                    {
                        var regionFolder = _fileNameBuilder.GetSeasonFolder(series, season.SeasonNumber);
                        if (string.IsNullOrWhiteSpace(regionFolder) ||
                            regionFolder.StartsWith("Season ", StringComparison.OrdinalIgnoreCase) ||
                            regionFolder.Equals("Specials", StringComparison.OrdinalIgnoreCase))
                        {
                            regionFolder = ResolveCanonicalRegionFolderName(season.SeasonNumber, season.Title);
                        }

                        if (!string.IsNullOrWhiteSpace(regionFolder))
                        {
                            var fullRegionPath = Path.Combine(series.Path, regionFolder);
                            if (!_diskProvider.FolderExists(fullRegionPath))
                            {
                                _diskProvider.CreateFolder(fullRegionPath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to automatically create platform folder structure for {0} at {1}", series.Title, series.Path);
            }
        }

        private void ImportIncomingContentToExistingPlatform(Series incoming, Series existing)
        {
            try
            {
                if (!_diskProvider.FolderExists(existing.Path))
                {
                    _diskProvider.CreateFolder(existing.Path);
                }

                var candidateArchive = string.Empty;
                if (!string.IsNullOrWhiteSpace(incoming.Path))
                {
                    if (_diskProvider.FileExists(incoming.Path) &&
                        (incoming.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                         incoming.Path.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)))
                    {
                        candidateArchive = incoming.Path;
                    }
                    else if (_diskProvider.FileExists(incoming.Path + ".zip"))
                    {
                        candidateArchive = incoming.Path + ".zip";
                    }
                    else if (_diskProvider.FileExists(incoming.Path + ".7z"))
                    {
                        candidateArchive = incoming.Path + ".7z";
                    }
                }

                if (!string.IsNullOrWhiteSpace(candidateArchive) && _diskProvider.FileExists(candidateArchive))
                {
                    ExtractArchiveToDirectory(candidateArchive, existing.Path, existing);
                }
                else if (!string.IsNullOrWhiteSpace(incoming.Path) &&
                         _diskProvider.FolderExists(incoming.Path) &&
                         !incoming.Path.PathEquals(existing.Path))
                {
                    CopyFolderContent(incoming.Path, existing.Path);
                }

                _commandQueueManager?.Push(new RefreshPlatformCommand(new List<int> { existing.Id }, true));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to import incoming content for existing platform {0}", existing.Title);
            }
        }

        private void ExtractArchiveToDirectory(string archivePath, string destinationDirectory, Series series)
        {
            if (!_diskProvider.FolderExists(destinationDirectory))
            {
                _diskProvider.CreateFolder(destinationDirectory);
            }

            if (!Path.GetExtension(archivePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                using var archive = ZipFile.OpenRead(archivePath);
                var validExtensions = MediaFileExtensions.RomExtensions;
                var newlyExtractedZips = new List<string>();

                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\") ||
                        entry.FullName.Contains("__MACOSX") || entry.Name.StartsWith(".") ||
                        entry.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(entry.Name);
                    if (!validExtensions.Contains(ext))
                    {
                        continue;
                    }

                    var destFile = Path.Combine(destinationDirectory, Path.GetFileName(entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!_diskProvider.FileExists(destFile))
                    {
                        using var stream = entry.Open();
                        _diskProvider.SaveStream(stream, destFile);
                    }

                    if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        newlyExtractedZips.Add(destFile);
                    }
                }

                foreach (var innerZip in newlyExtractedZips)
                {
                    if (!_diskProvider.FileExists(innerZip))
                    {
                        continue;
                    }

                    try
                    {
                        using var innerArchive = ZipFile.OpenRead(innerZip);
                        var innerRom = innerArchive.Entries.FirstOrDefault(e =>
                            !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\") &&
                            validExtensions.Contains(Path.GetExtension(e.Name)) &&
                            !Path.GetExtension(e.Name).Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetExtension(e.Name).Equals(".7z", StringComparison.OrdinalIgnoreCase));

                        if (innerRom != null)
                        {
                            var innerDest = Path.Combine(destinationDirectory, innerRom.Name);
                            if (!_diskProvider.FileExists(innerDest))
                            {
                                using var stream = innerRom.Open();
                                _diskProvider.SaveStream(stream, innerDest);
                            }

                            _diskProvider.DeleteFile(innerZip);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Failed to unpack nested zip '{0}'", innerZip);
                    }
                }

                _logger.Info("Extracted archive '{0}' into platform directory '{1}'", Path.GetFileName(archivePath), destinationDirectory);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to extract archive '{0}' to '{1}'", archivePath, destinationDirectory);
            }
        }

        private void CopyFolderContent(string sourceDirectory, string destinationDirectory)
        {
            try
            {
                if (!_diskProvider.FolderExists(destinationDirectory))
                {
                    _diskProvider.CreateFolder(destinationDirectory);
                }

                var files = _diskProvider.GetFiles(sourceDirectory, true);
                var validExtensions = MediaFileExtensions.RomExtensions;

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    if (!validExtensions.Contains(ext))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(destinationDirectory, fileName);
                    if (!_diskProvider.FileExists(destFile))
                    {
                        _diskProvider.CopyFile(file, destFile);
                    }

                    if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        ExtractArchiveToDirectory(destFile, destinationDirectory, null);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to copy folder contents from '{0}' to '{1}'", sourceDirectory, destinationDirectory);
            }
        }

        private static string ResolveCanonicalRegionFolderName(int seasonNumber, string seasonTitle)
        {
            if (!string.IsNullOrWhiteSpace(seasonTitle) &&
                !seasonTitle.StartsWith("Season ", StringComparison.OrdinalIgnoreCase) &&
                !seasonTitle.Equals("Specials", StringComparison.OrdinalIgnoreCase))
            {
                return seasonTitle;
            }

            return seasonNumber switch
            {
                0 => "Unlicensed",
                1 => "USA",
                2 => "Europe",
                3 => "Japan",
                4 => "World",
                5 => "Homebrew",
                6 => "ROM Hacks",
                _ => "USA"
            };
        }

        private void MigrateLegacySeasonFolders(string platformPath)
        {
            if (string.IsNullOrWhiteSpace(platformPath) || !_diskProvider.FolderExists(platformPath))
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

            foreach (var pair in legacyMappings)
            {
                var legacyPath = Path.Combine(platformPath, pair.Key);
                if (!_diskProvider.FolderExists(legacyPath))
                {
                    continue;
                }

                var targetPath = Path.Combine(platformPath, pair.Value);
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
                    _logger.Debug(ex, "Unable to migrate legacy season folder {0} to {1}", legacyPath, targetPath);
                }
            }
        }

        private Series AddSkyhookData(Series newSeries)
        {
            Tuple<Series, List<Episode>> tuple;

            try
            {
                tuple = _seriesInfo.GetSeriesInfo(newSeries.TvdbId, newSeries.Language, newSeries.SeasonType);
            }
            catch (SeriesNotFoundException)
            {
                _logger.Error("Platform {0} with ID {1} was not found in the metadata catalog. Path: {2}", newSeries, newSeries.TvdbId, newSeries.Path);

                throw new ValidationException(new List<ValidationFailure>
                                              {
                                                  new ValidationFailure("TvdbId", $"A platform or game with this ID was not found. Path: {newSeries.Path}", newSeries.TvdbId)
                                              });
            }

            var series = tuple.Item1;

            // If seasons were passed in on the new series use them, otherwise use the seasons from catalog
            newSeries.Seasons = newSeries.Seasons != null && newSeries.Seasons.Any() ? newSeries.Seasons : series.Seasons;

            series.ApplyChanges(newSeries);

            return series;
        }

        private Series SetPropertiesAndValidate(Series newSeries)
        {
            if (string.IsNullOrWhiteSpace(newSeries.Path))
            {
                var folderName = _fileNameBuilder.GetSeriesFolder(newSeries);
                newSeries.Path = Path.Combine(newSeries.RootFolderPath, folderName);
            }

            newSeries.CleanTitle = newSeries.Title.CleanSeriesTitle();
            newSeries.SortTitle = SeriesTitleNormalizer.Normalize(newSeries.Title, newSeries.TvdbId);
            newSeries.Added = DateTime.UtcNow;

            if (newSeries.QualityProfileId <= 0)
            {
                newSeries.QualityProfileId = 1;
            }

            if (newSeries.AddOptions != null && newSeries.AddOptions.Monitor == MonitorTypes.None)
            {
                newSeries.Monitored = false;

                if (newSeries.Seasons != null)
                {
                    foreach (var season in newSeries.Seasons)
                    {
                        season.Monitored = false;
                    }
                }
            }

            var validationResult = _addSeriesValidator.Validate(newSeries);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            return newSeries;
        }
    }
}
