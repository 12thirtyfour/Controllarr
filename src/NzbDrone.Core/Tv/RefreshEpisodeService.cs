using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public interface IRefreshEpisodeService
    {
        void RefreshEpisodeInfo(Series series, IEnumerable<Episode> remoteEpisodes);
    }

    public class RefreshEpisodeService : IRefreshEpisodeService
    {
        private readonly IEpisodeService _episodeService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public RefreshEpisodeService(IEpisodeService episodeService, IEventAggregator eventAggregator, Logger logger)
        {
            _episodeService = episodeService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void RefreshEpisodeInfo(Series series, IEnumerable<Episode> remoteEpisodes)
        {
            _logger.Info("Starting game info refresh for: {0}", series);
            var successCount = 0;
            var failCount = 0;

            var existingEpisodes = _episodeService.GetEpisodeBySeries(series.Id);
            var seasons = series.Seasons;
            var hasExisting = existingEpisodes.Any();

            var updateList = new List<Episode>();
            var newList = new List<Episode>();
            var dupeFreeRemoteEpisodes = remoteEpisodes.DistinctBy(m => new { m.SeasonNumber, m.EpisodeNumber }).ToList();

            if (series.SeriesType == SeriesTypes.Anime)
            {
                dupeFreeRemoteEpisodes = MapAbsoluteEpisodeNumbers(dupeFreeRemoteEpisodes);
            }

            var orderedEpisodes = OrderEpisodes(series, dupeFreeRemoteEpisodes).ToList();
            var episodesPerSeason = orderedEpisodes.GroupBy(s => s.SeasonNumber).ToDictionary(g => g.Key, g => g.Count());
            var latestSeason = seasons.MaxBy(s => s.SeasonNumber);

            foreach (var episode in orderedEpisodes)
            {
                try
                {
                    var episodeToUpdate = existingEpisodes.FirstOrDefault(e => e.SeasonNumber == episode.SeasonNumber && e.EpisodeNumber == episode.EpisodeNumber);

                    if (episodeToUpdate != null)
                    {
                        existingEpisodes.Remove(episodeToUpdate);
                        updateList.Add(episodeToUpdate);

                        // Anime series with newly added absolute episode number
                        if (series.SeriesType == SeriesTypes.Anime &&
                            !episodeToUpdate.AbsoluteEpisodeNumber.HasValue &&
                            episode.AbsoluteEpisodeNumber.HasValue)
                        {
                            episodeToUpdate.AbsoluteEpisodeNumberAdded = true;
                        }
                    }
                    else
                    {
                        episodeToUpdate = new Episode();
                        episodeToUpdate.Monitored = GetMonitoredStatus(episode, seasons, series);
                        newList.Add(episodeToUpdate);
                    }

                    episodeToUpdate.SeriesId = series.Id;
                    episodeToUpdate.TvdbId = episode.TvdbId;
                    episodeToUpdate.EpisodeNumber = episode.EpisodeNumber;
                    episodeToUpdate.SeasonNumber = episode.SeasonNumber;
                    episodeToUpdate.AbsoluteEpisodeNumber = episode.AbsoluteEpisodeNumber;
                    episodeToUpdate.AiredAfterSeasonNumber = episode.AiredAfterSeasonNumber;
                    episodeToUpdate.AiredBeforeSeasonNumber = episode.AiredBeforeSeasonNumber;
                    episodeToUpdate.AiredBeforeEpisodeNumber = episode.AiredBeforeEpisodeNumber;
                    episodeToUpdate.Title = episode.Title ?? "TBA";
                    episodeToUpdate.Overview = episode.Overview;
                    episodeToUpdate.AirDate = episode.AirDate;
                    episodeToUpdate.AirDateUtc = episode.AirDateUtc;
                    episodeToUpdate.Runtime = episode.Runtime;
                    episodeToUpdate.FinaleType = episode.FinaleType;
                    episodeToUpdate.Ratings = episode.Ratings;
                    episodeToUpdate.Images = episode.Images;

                    successCount++;
                }
                catch (Exception e)
                {
                    _logger.Fatal(e, "An error has occurred while updating game info for platform {0}. {1}", series, episode);
                    failCount++;
                }
            }

            UnmonitorReaddedEpisodes(series, newList, hasExisting);

            var allEpisodes = new List<Episode>();
            allEpisodes.AddRange(newList);
            allEpisodes.AddRange(updateList);

            _episodeService.DeleteMany(existingEpisodes);
            _episodeService.UpdateMany(updateList);
            _episodeService.InsertMany(newList);

            _eventAggregator.PublishEvent(new EpisodeInfoRefreshedEvent(series, newList, updateList, existingEpisodes));

            if (failCount != 0)
            {
                _logger.Info("Finished game refresh for platform: {0}. Successful: {1} - Failed: {2} ",
                    series.Title,
                    successCount,
                    failCount);
            }
            else
            {
                _logger.Info("Finished game refresh for platform: {0}.", series);
            }
        }

        private bool GetMonitoredStatus(Episode episode, IEnumerable<Season> seasons, Series series)
        {
            if (episode.EpisodeNumber == 0 && episode.SeasonNumber != 1)
            {
                return false;
            }

            var season = seasons.SingleOrDefault(c => c.SeasonNumber == episode.SeasonNumber);
            return season == null || season.Monitored;
        }

        private void UnmonitorReaddedEpisodes(Series series, List<Episode> episodes, bool hasExisting)
        {
            if (series.AddOptions != null)
            {
                return;
            }

            var threshold = DateTime.UtcNow.AddDays(-14);

            var oldEpisodes = episodes.Where(e => e.AirDateUtc.HasValue && e.AirDateUtc.Value.Before(threshold)).ToList();

            if (oldEpisodes.Any())
            {
                if (hasExisting)
                {
                    _logger.Warn("Platform {0} ({1}) had {2} old games appear, please check monitored status.", series.TvdbId, series.Title, oldEpisodes.Count);
                }
                else
                {
                    threshold = DateTime.UtcNow.AddDays(-1);

                    foreach (var episode in episodes)
                    {
                        if (episode.AirDateUtc.HasValue && episode.AirDateUtc.Value.Before(threshold))
                        {
                            episode.Monitored = false;
                        }
                    }

                    _logger.Warn("Platform {0} ({1}) had {2} old games appear, unmonitored released games to prevent unexpected downloads.", series.TvdbId, series.Title, oldEpisodes.Count);
                }
            }
        }

        private List<Episode> MapAbsoluteEpisodeNumbers(List<Episode> remoteEpisodes)
        {
            // Return all episodes with no abs number, but distinct for those with abs number
            return remoteEpisodes.Where(e => e.AbsoluteEpisodeNumber.HasValue)
                                 .OrderByDescending(e => e.SeasonNumber)
                                 .DistinctBy(e => e.AbsoluteEpisodeNumber.Value)
                                 .Concat(remoteEpisodes.Where(e => !e.AbsoluteEpisodeNumber.HasValue))
                                 .ToList();
        }

        private IEnumerable<Episode> OrderEpisodes(Series series, List<Episode> episodes)
        {
            if (series.SeriesType == SeriesTypes.Anime)
            {
                var withAbs = episodes.Where(e => e.AbsoluteEpisodeNumber.HasValue)
                                      .OrderBy(e => e.AbsoluteEpisodeNumber);

                var withoutAbs = episodes.Where(e => !e.AbsoluteEpisodeNumber.HasValue)
                                         .OrderBy(e => e.SeasonNumber)
                                         .ThenBy(e => e.EpisodeNumber);

                return withAbs.Concat(withoutAbs);
            }

            return episodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber);
        }
    }
}
