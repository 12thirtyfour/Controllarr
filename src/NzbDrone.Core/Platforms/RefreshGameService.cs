using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Games;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public interface IRefreshGameService
    {
        void RefreshEpisodeInfo(Series series, IEnumerable<Episode> remoteEpisodes);
    }

    public class RefreshGameService : IRefreshGameService
    {
        private readonly IEpisodeService _episodeService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public RefreshGameService(IEpisodeService episodeService, IEventAggregator eventAggregator, Logger logger)
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

            var filteredEpisodes = FilterEpisodesForCollectionStrategy(series, dupeFreeRemoteEpisodes, existingEpisodes);
            var orderedEpisodes = OrderEpisodes(series, filteredEpisodes.ToList()).ToList();
            var episodesPerSeason = orderedEpisodes.GroupBy(s => s.SeasonNumber).ToDictionary(g => g.Key, g => g.Count());
            var latestSeason = seasons.MaxBy(s => s.SeasonNumber);

            var validExisting = existingEpisodes
                .Where(e => (string.IsNullOrWhiteSpace(e.Title) || GameCatalogProxy.IsValidGameTitle(e.Title)) &&
                            (!RegionHelper.IsBios(e.Title) || e.SeasonNumber == GameCatalogProxy.RegionBios))
                .ToList();

            var claimedEpisodes = new HashSet<Episode>(ReferenceEqualityComparer.Instance);
            var byTvdbId = new Dictionary<int, Queue<Episode>>();
            var bySeasonTitleFinale = new Dictionary<(int SeasonNumber, string Title, string FinaleType), Queue<Episode>>();
            var bySeasonTitle = new Dictionary<(int SeasonNumber, string Title), Queue<Episode>>();
            var byTitle = new Dictionary<string, Queue<Episode>>(StringComparer.OrdinalIgnoreCase);
            var bySeasonEpisodeNumber = new Dictionary<(int SeasonNumber, int EpisodeNumber), Queue<Episode>>();

            static void AddToBucket<TKey>(Dictionary<TKey, Queue<Episode>> dict, TKey key, Episode ep)
            {
                if (!dict.TryGetValue(key, out var queue))
                {
                    queue = new Queue<Episode>();
                    dict[key] = queue;
                }

                queue.Enqueue(ep);
            }

            Episode PopUnclaimed<TKey>(Dictionary<TKey, Queue<Episode>> dict, TKey key)
            {
                if (!dict.TryGetValue(key, out var queue))
                {
                    return null;
                }

                while (queue.Count > 0)
                {
                    var candidate = queue.Dequeue();
                    if (!claimedEpisodes.Contains(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            foreach (var existing in validExisting)
            {
                if (existing.TvdbId > 0)
                {
                    AddToBucket(byTvdbId, existing.TvdbId, existing);
                }

                if (existing.Title != null)
                {
                    var titleUpper = existing.Title.ToUpperInvariant();
                    var finaleUpper = (existing.FinaleType ?? string.Empty).ToUpperInvariant();
                    AddToBucket(bySeasonTitleFinale, (existing.SeasonNumber, titleUpper, finaleUpper), existing);
                    AddToBucket(bySeasonTitle, (existing.SeasonNumber, titleUpper), existing);
                    AddToBucket(byTitle, existing.Title, existing);
                }

                AddToBucket(bySeasonEpisodeNumber, (existing.SeasonNumber, existing.EpisodeNumber), existing);
            }

            foreach (var episode in orderedEpisodes)
            {
                try
                {
                    Episode episodeToUpdate = null;

                    if (episode.TvdbId > 0)
                    {
                        episodeToUpdate = PopUnclaimed(byTvdbId, episode.TvdbId);
                    }

                    if (episodeToUpdate == null && episode.Title != null)
                    {
                        var titleUpper = episode.Title.ToUpperInvariant();
                        var finaleUpper = (episode.FinaleType ?? string.Empty).ToUpperInvariant();
                        episodeToUpdate = PopUnclaimed(bySeasonTitleFinale, (episode.SeasonNumber, titleUpper, finaleUpper))
                            ?? PopUnclaimed(bySeasonTitle, (episode.SeasonNumber, titleUpper))
                            ?? PopUnclaimed(byTitle, episode.Title);
                    }

                    if (episodeToUpdate == null)
                    {
                        episodeToUpdate = PopUnclaimed(bySeasonEpisodeNumber, (episode.SeasonNumber, episode.EpisodeNumber));
                    }

                    if (episodeToUpdate != null)
                    {
                        claimedEpisodes.Add(episodeToUpdate);
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

            var unreferencedExisting = existingEpisodes
                .Where(e => !claimedEpisodes.Contains(e))
                .ToList();

            var episodesToDelete = unreferencedExisting
                .Where(e => e.SeasonNumber != GameCatalogProxy.RegionBios)
                .ToList();

            _episodeService.DeleteMany(episodesToDelete);
            _episodeService.UpdateMany(updateList);
            _episodeService.InsertMany(newList);

            _eventAggregator.PublishEvent(new GameInfoRefreshedEvent(series, newList, updateList, unreferencedExisting));

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

        private IEnumerable<Episode> FilterEpisodesForCollectionStrategy(Series series, IEnumerable<Episode> episodes, List<Episode> existingEpisodes)
        {
            if (series.CollectionStrategy == CollectionStrategy.Preservationist)
            {
                return episodes;
            }

            var preferInitial = series.RevisionPreference == RevisionPreference.InitialRelease;
            var encryptionPref = series.EncryptionPreference;

            var groups = episodes.GroupBy(e => $"{e.SeasonNumber}:{GameCatalogProxy.ExtractBaseTitle(e.Title, e.FinaleType).ToLowerInvariant()}");
            var filtered = new List<Episode>();

            foreach (var group in groups)
            {
                var groupList = group.ToList();

                var retailList = groupList
                    .Where(e => !GameCatalogProxy.IsPreReleaseVersion(e.FinaleType, e.Title))
                    .ToList();

                var candidateList = retailList.Count > 0 ? retailList : groupList;

                var baseGamePool = candidateList.Where(e => !GameCatalogProxy.IsUpdateVersion(e.FinaleType, e.Title)).ToList();
                var patchUpdatePool = candidateList.Where(e => GameCatalogProxy.IsPatchUpdateVersion(e.FinaleType, e.Title)).ToList();
                var dlcPool = candidateList.Where(e => GameCatalogProxy.IsUpdateVersion(e.FinaleType, e.Title) && !GameCatalogProxy.IsPatchUpdateVersion(e.FinaleType, e.Title)).ToList();

                void AddPreferredVersionEpisodes(List<Episode> candidates)
                {
                    if (candidates.Count == 0)
                    {
                        return;
                    }

                    var versionGroups = candidates
                        .GroupBy(e => GameCatalogProxy.ExtractVersionOnly(e.FinaleType, e.Title))
                        .OrderBy(vg => GameCatalogProxy.GetRevisionSortOrder(vg.Key))
                        .ToList();

                    if (versionGroups.Count == 0)
                    {
                        return;
                    }

                    var chosenVersionGroup = preferInitial ? versionGroups.First() : versionGroups.Last();
                    filtered.AddRange(chosenVersionGroup);
                }

                void ProcessPool(List<Episode> pool)
                {
                    if (pool.Count == 0)
                    {
                        return;
                    }

                    var unencryptedRetail = pool
                        .Where(e => !GameCatalogProxy.IsEncryptedVersion(e.FinaleType, e.Title))
                        .ToList();
                    var encryptedRetail = pool
                        .Where(e => GameCatalogProxy.IsEncryptedVersion(e.FinaleType, e.Title))
                        .ToList();

                    if (encryptionPref == Games.EncryptionPreference.NotEncrypted ||
                        encryptionPref == Games.EncryptionPreference.Both)
                    {
                        if (unencryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(unencryptedRetail);
                        }
                        else if (encryptionPref == Games.EncryptionPreference.NotEncrypted && encryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(encryptedRetail);
                        }
                    }

                    if (encryptionPref == Games.EncryptionPreference.Encrypted ||
                        encryptionPref == Games.EncryptionPreference.Both)
                    {
                        if (encryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(encryptedRetail);
                        }
                        else if (encryptionPref == Games.EncryptionPreference.Encrypted && unencryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(unencryptedRetail);
                        }
                    }
                }

                if (baseGamePool.Count > 0)
                {
                    ProcessPool(baseGamePool);
                }
                else if (patchUpdatePool.Count == 0 && dlcPool.Count == 0)
                {
                    ProcessPool(candidateList);
                }

                if (patchUpdatePool.Count > 0)
                {
                    ProcessPool(patchUpdatePool);
                }

                if (dlcPool.Count > 0)
                {
                    ProcessPool(dlcPool);
                }
            }

            if (existingEpisodes != null)
            {
                var filteredKeys = filtered
                    .Select(f => (f.SeasonNumber, f.EpisodeNumber))
                    .ToHashSet();

                foreach (var epWithFile in existingEpisodes)
                {
                    if (epWithFile.EpisodeFileId > 0 &&
                        filteredKeys.Add((epWithFile.SeasonNumber, epWithFile.EpisodeNumber)))
                    {
                        filtered.Add(epWithFile);
                    }
                }
            }

            return filtered;
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
