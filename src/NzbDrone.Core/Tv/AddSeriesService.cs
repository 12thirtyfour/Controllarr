using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentValidation;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Games;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Tv
{
    public interface IAddSeriesService
    {
        Series AddSeries(Series newSeries);
        List<Series> AddSeries(List<Series> newSeries, bool ignoreErrors = false);
    }

    public class AddSeriesService : IAddSeriesService
    {
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IProvideSeriesInfo _seriesInfo;
        private readonly IGameMetadataProxy _gameMetadataProxy;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly ISmartAddService _smartAddService;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IAddSeriesValidator _addSeriesValidator;
        private readonly Logger _logger;

        public AddSeriesService(ISeriesService seriesService,
                                IEpisodeService episodeService,
                                IProvideSeriesInfo seriesInfo,
                                IGameMetadataProxy gameMetadataProxy,
                                IGameCatalogService gameCatalogService,
                                ISmartAddService smartAddService,
                                IBuildFileNames fileNameBuilder,
                                IAddSeriesValidator addSeriesValidator,
                                Logger logger)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _seriesInfo = seriesInfo;
            _gameMetadataProxy = gameMetadataProxy;
            _gameCatalogService = gameCatalogService;
            _smartAddService = smartAddService;
            _fileNameBuilder = fileNameBuilder;
            _addSeriesValidator = addSeriesValidator;
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

            newSeries = AddSkyhookData(newSeries);
            newSeries = SetPropertiesAndValidate(newSeries);

            _logger.Info("Adding Platform {0} Path: [{1}]", newSeries, newSeries.Path);
            _seriesService.AddSeries(newSeries);

            return newSeries;
        }

        public List<Series> AddSeries(List<Series> newSeries, bool ignoreErrors = false)
        {
            var added = DateTime.UtcNow;
            var seriesToAdd = new List<Series>();
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

                    var series = AddSkyhookData(s);
                    series = SetPropertiesAndValidate(series);
                    series.Added = added;
                    if (existingSeriesTvdbIds.ContainsValue(series.TvdbId))
                    {
                        _logger.Debug("Platform ID {0} was not added due to validation failure: Platform {1} already exists in database", s.TvdbId, s);
                        continue;
                    }

                    if (seriesToAdd.Any(f => f.TvdbId == series.TvdbId))
                    {
                        _logger.Trace("Platform ID {0} was already added from another import list, not adding platform {1} again", s.TvdbId, s);
                        continue;
                    }

                    var duplicateSlug = seriesToAdd.FirstOrDefault(f => f.TitleSlug == series.TitleSlug);
                    if (duplicateSlug != null)
                    {
                        _logger.Debug("Platform ID {0} was not added due to validation failure: Duplicate Slug {1} used by platform {2}", s.TvdbId, s.TitleSlug, duplicateSlug.TvdbId);
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

            return _seriesService.AddSeries(seriesToAdd);
        }

        private Series AddGameOrParentPlatform(Series newSeries, GameCatalogEntry gameEntry)
        {
            _gameCatalogService.RegisterGameSelection(gameEntry.TvdbId);

            try
            {
                _smartAddService?.SmartAddGame(new SmartAddRequest
                {
                    Title = gameEntry.Title,
                    PlatformSlug = gameEntry.PlatformSlug,
                    RootFolderPath = newSeries.RootFolderPath
                });
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Skipping SmartAdd table sync for {0}", gameEntry.Title);
            }

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
                        .Where(e => e.SeasonNumber == gameEntry.RegionSeasonNumber)
                        .Select(e => e.EpisodeNumber)
                        .DefaultIfEmpty(0)
                        .Max() + 1;

                    gameEntry.EpisodeNumber = nextEpisodeNum;
                    var newEpisode = _gameCatalogService.CreateEpisodeForGame(gameEntry, existingPlatform.Id, true);
                    _episodeService.InsertMany(new List<Episode> { newEpisode });
                }

                var regionSeason = existingPlatform.Seasons?.FirstOrDefault(s => s.SeasonNumber == gameEntry.RegionSeasonNumber);
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

            return newSeries;
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
