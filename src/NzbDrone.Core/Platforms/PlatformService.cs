using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Games;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public interface IPlatformService
    {
    }

    public interface ISeriesService : IPlatformService
    {
        Series GetSeries(int seriesId);
        List<Series> GetSeries(IEnumerable<int> seriesIds);
        Series AddSeries(Series newSeries);
        List<Series> AddSeries(List<Series> newSeries);
        Series FindByTvdbId(int tvdbId);
        Series FindByTvRageId(int tvRageId);
        Series FindByImdbId(string imdbId);
        Series FindByTitle(string title);
        Series FindByTitle(string title, int year);
        Series FindByTitleInexact(string title);
        Series FindByPath(string path);
        void DeleteSeries(List<int> seriesIds, bool deleteFiles, bool addImportListExclusion);
        bool IsSeriesDeletedWithFiles(int seriesId);
        List<Series> GetAllSeries();
        Dictionary<int, int> AllSeriesTvdbIds();
        Dictionary<int, string> GetAllSeriesPaths();
        Dictionary<int, List<int>> GetAllSeriesTags();
        List<Series> AllForTag(int tagId);
        Dictionary<int, int> GetAllSeriesQualityProfiles();
        Series UpdateSeries(Series series, bool updateEpisodesToMatchSeason = true, bool publishUpdatedEvent = true);
        List<Series> UpdateSeries(List<Series> series, bool moveFiles);
        Series UpdateSeries(Series series, string sourcePath, string destinationPath, bool moveFiles);
        bool SeriesPathExists(string folder);
        void RemoveAddOptions(Series series);
        bool UpdateAutoTaggingTags(Series series);
        void UpdateTags(List<Series> series);
    }

    public class PlatformService : IPlatformService, ISeriesService, IHandle<ApplicationStartedEvent>
    {
        private readonly ISeriesRepository _seriesRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly IEpisodeService _episodeService;
        private readonly IBuildSeriesPaths _seriesPathBuilder;
        private readonly IAutoTaggingService _autoTaggingService;
        private static readonly ConcurrentDictionary<int, bool> DeletedSeriesWithFiles = new();

        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public PlatformService(ISeriesRepository seriesRepository,
                             IEventAggregator eventAggregator,
                             IEpisodeService episodeService,
                             IBuildSeriesPaths seriesPathBuilder,
                             IAutoTaggingService autoTaggingService,
                             IManageCommandQueue commandQueueManager,
                             Logger logger)
        {
            _seriesRepository = seriesRepository;
            _eventAggregator = eventAggregator;
            _episodeService = episodeService;
            _seriesPathBuilder = seriesPathBuilder;
            _autoTaggingService = autoTaggingService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public Series GetSeries(int seriesId)
        {
            return _seriesRepository.Get(seriesId);
        }

        public List<Series> GetSeries(IEnumerable<int> seriesIds)
        {
            return _seriesRepository.Get(seriesIds).ToList();
        }

        public Series AddSeries(Series newSeries)
        {
            _seriesRepository.Insert(newSeries);
            DeletedSeriesWithFiles.TryRemove(newSeries.Id, out _);
            _eventAggregator.PublishEvent(new PlatformAddedEvent(GetSeries(newSeries.Id)));

            return newSeries;
        }

        public List<Series> AddSeries(List<Series> newSeries)
        {
            _seriesRepository.InsertMany(newSeries);
            foreach (var s in newSeries)
            {
                DeletedSeriesWithFiles.TryRemove(s.Id, out _);
            }

            _eventAggregator.PublishEvent(new PlatformImportedEvent(newSeries.Select(s => s.Id).ToList()));

            return newSeries;
        }

        public Series FindByTvdbId(int tvdbId)
        {
            return _seriesRepository.FindByTvdbId(tvdbId);
        }

        public Series FindByTvRageId(int tvRageId)
        {
            return _seriesRepository.FindByTvRageId(tvRageId);
        }

        public Series FindByImdbId(string imdbId)
        {
            return _seriesRepository.FindByImdbId(imdbId);
        }

        public Series FindByTitle(string title)
        {
            return _seriesRepository.FindByTitle(title.CleanSeriesTitle());
        }

        public Series FindByTitleInexact(string title)
        {
            // find any series clean title within the provided release title
            var cleanTitle = title.CleanSeriesTitle();
            var list = _seriesRepository.FindByTitleInexact(cleanTitle);
            if (!list.Any())
            {
                // no series matched
                return null;
            }

            if (list.Count == 1)
            {
                // return the first series if there is only one
                return list.Single();
            }

            // build ordered list of series by position in the search string
            var query =
                list.Select(series => new
                {
                    position = cleanTitle.IndexOf(series.CleanTitle),
                    length = series.CleanTitle.Length,
                    series = series
                })
                    .Where(s => (s.position >= 0))
                    .ToList()
                    .OrderBy(s => s.position)
                    .ThenByDescending(s => s.length)
                    .ToList();

            // get the leftmost series that is the longest
            // series are usually the first thing in release title, so we select the leftmost and longest match
            var match = query.First().series;

            _logger.Debug("Multiple platforms matched {0} from title {1}", match.Title, title);
            foreach (var entry in list)
            {
                _logger.Debug("Multiple platform match candidate: {0} cleantitle: {1}", entry.Title, entry.CleanTitle);
            }

            return match;
        }

        public Series FindByPath(string path)
        {
            return _seriesRepository.FindByPath(path);
        }

        public Series FindByTitle(string title, int year)
        {
            return _seriesRepository.FindByTitle(title.CleanSeriesTitle(), year);
        }

        public void DeleteSeries(List<int> seriesIds, bool deleteFiles, bool addImportListExclusion)
        {
            if (deleteFiles && seriesIds != null)
            {
                foreach (var id in seriesIds)
                {
                    DeletedSeriesWithFiles[id] = true;
                }
            }

            var series = _seriesRepository.Get(seriesIds).ToList();
            _seriesRepository.DeleteMany(seriesIds);
            _eventAggregator.PublishEvent(new PlatformDeletedEvent(series, deleteFiles, addImportListExclusion));
        }

        public bool IsSeriesDeletedWithFiles(int seriesId)
        {
            return DeletedSeriesWithFiles.TryGetValue(seriesId, out var deleted) && deleted;
        }

        public List<Series> GetAllSeries()
        {
            return _seriesRepository.All().ToList();
        }

        public Dictionary<int, int> AllSeriesTvdbIds()
        {
            return _seriesRepository.AllSeriesTvdbIds();
        }

        public Dictionary<int, string> GetAllSeriesPaths()
        {
            return _seriesRepository.AllSeriesPaths();
        }

        public Dictionary<int, List<int>> GetAllSeriesTags()
        {
            return _seriesRepository.AllSeriesTags();
        }

        public Dictionary<int, int> GetAllSeriesQualityProfiles()
        {
            return _seriesRepository.AllSeriesQualityProfiles();
        }

        public List<Series> AllForTag(int tagId)
        {
            return GetAllSeries().Where(s => s.Tags.Contains(tagId))
                                 .ToList();
        }

        // updateEpisodesToMatchSeason is an override for GameMonitoredService to use so a change via Season pass doesn't get nuked by the seasons loop.
        // TODO: Remove when seasons are split from series (or we come up with a better way to address this)
        public Series UpdateSeries(Series series, bool updateEpisodesToMatchSeason = true, bool publishUpdatedEvent = true)
        {
            var storedSeries = GetSeries(series.Id);

            var episodeMonitoredChanged = false;

            if (updateEpisodesToMatchSeason)
            {
                if (storedSeries != null && series.Monitored != storedSeries.Monitored)
                {
                    if (series.Monitored)
                    {
                        if (series.Seasons != null)
                        {
                            foreach (var season in series.Seasons)
                            {
                                season.Monitored = true;
                                _episodeService.SetEpisodeMonitoredBySeason(series.Id, season.SeasonNumber, true);
                                episodeMonitoredChanged = true;
                            }
                        }

                        ApplyOneGameOneRomMonitoringIfNeeded(series, storedSeries, ref episodeMonitoredChanged);
                    }
                    else
                    {
                        if (series.Seasons != null)
                        {
                            foreach (var season in series.Seasons)
                            {
                                season.Monitored = false;
                            }
                        }

                        var allEpisodes = _episodeService.GetEpisodeBySeries(series.Id) ?? new List<Episode>();
                        foreach (var ep in allEpisodes)
                        {
                            ep.Monitored = false;
                        }

                        if (allEpisodes.Any())
                        {
                            _episodeService.UpdateEpisodes(allEpisodes);
                        }

                        episodeMonitoredChanged = true;
                    }
                }
                else
                {
                    if (series.Seasons != null)
                    {
                        foreach (var season in series.Seasons)
                        {
                            var storedSeason = storedSeries?.Seasons?.FirstOrDefault(s => s.SeasonNumber == season.SeasonNumber);

                            if (storedSeason != null && season.Monitored != storedSeason.Monitored)
                            {
                                _episodeService.SetEpisodeMonitoredBySeason(series.Id, season.SeasonNumber, season.Monitored);
                                episodeMonitoredChanged = true;
                            }
                        }
                    }

                    ApplyOneGameOneRomMonitoringIfNeeded(series, storedSeries, ref episodeMonitoredChanged);
                }
            }

            // Never update AddOptions when updating a series, keep it the same as the existing stored series.
            series.AddOptions = storedSeries.AddOptions;
            UpdateAutoTaggingTags(series);

            var updatedSeries = _seriesRepository.Update(series);
            if (publishUpdatedEvent)
            {
                _eventAggregator.PublishEvent(new PlatformEditedEvent(updatedSeries, storedSeries, episodeMonitoredChanged));
            }

            return updatedSeries;
        }

        private void ApplyOneGameOneRomMonitoringIfNeeded(Series series, Series storedSeries, ref bool episodeMonitoredChanged)
        {
            var strategyChanged = storedSeries == null ||
                                  series.CollectionStrategy != storedSeries.CollectionStrategy ||
                                  series.RevisionPreference != storedSeries.RevisionPreference ||
                                  series.EncryptionPreference != storedSeries.EncryptionPreference;

            if (strategyChanged && storedSeries != null)
            {
                _commandQueueManager.Push(new RefreshPlatformCommand(new List<int> { series.Id }, true));
            }

            if (!episodeMonitoredChanged && !strategyChanged)
            {
                return;
            }

            var allEpisodes = _episodeService.GetEpisodeBySeries(series.Id) ?? new List<Episode>();
            if (!allEpisodes.Any())
            {
                return;
            }

            var monitoredSeasonSet = series.Seasons != null
                ? series.Seasons.Where(s => s.Monitored).Select(s => s.SeasonNumber).ToHashSet()
                : new HashSet<int>();
            var preferInitial = series.RevisionPreference == RevisionPreference.InitialRelease;
            var encryptionPref = series.EncryptionPreference;

            if (series.CollectionStrategy == CollectionStrategy.Preservationist)
            {
                foreach (var item in allEpisodes)
                {
                    item.Monitored = monitoredSeasonSet.Contains(item.SeasonNumber);
                }

                _episodeService.UpdateEpisodes(allEpisodes);
                episodeMonitoredChanged = true;
                return;
            }

            foreach (var ep in allEpisodes)
            {
                if (!monitoredSeasonSet.Contains(ep.SeasonNumber))
                {
                    ep.Monitored = false;
                }
            }

            var groups = series.CollectionStrategy == CollectionStrategy.OneGameOneRom
                ? allEpisodes
                    .Where(e => monitoredSeasonSet.Contains(e.SeasonNumber))
                    .GroupBy(e => ExtractBaseTitle(e.Title, e.FinaleType).ToLowerInvariant())
                : allEpisodes
                    .Where(e => monitoredSeasonSet.Contains(e.SeasonNumber))
                    .GroupBy(e => $"{e.SeasonNumber}:{ExtractBaseTitle(e.Title, e.FinaleType).ToLowerInvariant()}");

            foreach (var group in groups)
            {
                var groupList = group.ToList();

                var retailList = groupList
                    .Where(e => !GameCatalogProxy.IsPreReleaseVersion(e.FinaleType, e.Title))
                    .ToList();

                var candidateList = retailList.Count > 0 ? retailList : groupList;

                var preferredEpisodes = new HashSet<Episode>();

                void AddPreferredVersionEpisodes(List<Episode> candidates)
                {
                    if (candidates.Count == 0)
                    {
                        return;
                    }

                    var regionCandidates = candidates;
                    if (series.CollectionStrategy == CollectionStrategy.OneGameOneRom)
                    {
                        var bestRegion = candidates
                            .Select(c => c.SeasonNumber)
                            .OrderBy(GameCatalogProxy.GetRegionPriority)
                            .FirstOrDefault();
                        regionCandidates = candidates.Where(c => c.SeasonNumber == bestRegion).ToList();
                    }

                    var versionGroups = regionCandidates
                        .GroupBy(e => GameCatalogProxy.ExtractVersionOnly(e.FinaleType, e.Title))
                        .OrderBy(vg => GameCatalogProxy.GetRevisionSortOrder(vg.Key))
                        .ToList();

                    if (versionGroups.Count == 0)
                    {
                        return;
                    }

                    var chosenVersionGroup = preferInitial ? versionGroups.First() : versionGroups.Last();
                    foreach (var ep in chosenVersionGroup)
                    {
                        preferredEpisodes.Add(ep);
                    }
                }

                void ProcessCandidatePool(List<Episode> pool)
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

                    if (encryptionPref == EncryptionPreference.NotEncrypted ||
                        encryptionPref == EncryptionPreference.Both)
                    {
                        if (unencryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(unencryptedRetail);
                        }
                        else if (encryptionPref == EncryptionPreference.NotEncrypted && encryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(encryptedRetail);
                        }
                    }

                    if (encryptionPref == EncryptionPreference.Encrypted ||
                        encryptionPref == EncryptionPreference.Both)
                    {
                        if (encryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(encryptedRetail);
                        }
                        else if (encryptionPref == EncryptionPreference.Encrypted && unencryptedRetail.Count > 0)
                        {
                            AddPreferredVersionEpisodes(unencryptedRetail);
                        }
                    }
                }

                var baseGamePool = candidateList.Where(e => !GameCatalogProxy.IsUpdateVersion(e.FinaleType, e.Title)).ToList();
                var updatePool = candidateList.Where(e => GameCatalogProxy.IsUpdateVersion(e.FinaleType, e.Title)).ToList();

                if (baseGamePool.Count > 0)
                {
                    ProcessCandidatePool(baseGamePool);
                }
                else if (updatePool.Count == 0)
                {
                    ProcessCandidatePool(candidateList);
                }

                if (updatePool.Count > 0)
                {
                    ProcessCandidatePool(updatePool);
                }

                foreach (var item in groupList)
                {
                    item.Monitored = preferredEpisodes.Contains(item);
                }
            }

            _episodeService.UpdateEpisodes(allEpisodes);
            episodeMonitoredChanged = true;
        }

        private static string ExtractBaseTitle(string title, string finaleType = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var clean = title;
            if (!string.IsNullOrWhiteSpace(finaleType) &&
                !string.Equals(finaleType, "Retail (v1.0)", StringComparison.OrdinalIgnoreCase) &&
                clean.EndsWith($"({finaleType})", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - finaleType.Length - 2).Trim();
            }

            var parenIdx = clean.IndexOf(" (", StringComparison.Ordinal);
            var bracketIdx = clean.IndexOf(" [", StringComparison.Ordinal);
            var cutIdx = -1;
            if (parenIdx > 0 && bracketIdx > 0)
            {
                cutIdx = Math.Min(parenIdx, bracketIdx);
            }
            else if (parenIdx > 0)
            {
                cutIdx = parenIdx;
            }
            else if (bracketIdx > 0)
            {
                cutIdx = bracketIdx;
            }

            if (cutIdx > 0)
            {
                clean = clean.Substring(0, cutIdx).Trim();
            }

            clean = GameCatalogProxy.DiscTagRegex.Replace(clean, string.Empty).Trim(' ', '-', ':');
            return clean;
        }

        public List<Series> UpdateSeries(List<Series> series, bool moveFiles)
        {
            _logger.Debug("Updating {0} platforms", series.Count);

            var pendingMoveSeriesIds = GetSeriesIdsWithPendingMove();
            var moveQueue = new List<BulkMovePlatform>();
            string destinationRootFolder = null;

            foreach (var s in series)
            {
                _logger.Trace("Updating: {0}", s.Title);

                if (!s.RootFolderPath.IsNullOrWhiteSpace())
                {
                    if (pendingMoveSeriesIds.Contains(s.Id))
                    {
                        _logger.Warn("Platform '{0}' is already being moved from {1} to {2}, skipping path update.", s.Title, s.Path, s.PendingPath);
                    }
                    else
                    {
                        var updatedPath = _seriesPathBuilder.BuildPath(s, !moveFiles);

                        if (moveFiles)
                        {
                            s.PendingPath = updatedPath;
                            moveQueue.Add(new BulkMovePlatform { SeriesId = s.Id, SourcePath = s.Path });
                            destinationRootFolder = s.RootFolderPath;

                            _logger.Trace("Path for '{0}' will be updated from {1} to {2} after files are moved successfully", s.Title, s.Path, updatedPath);
                        }
                        else
                        {
                            s.Path = updatedPath;
                            s.PendingPath = null;

                            _logger.Trace("Changing path for {0} to {1}", s.Title, s.Path);
                        }
                    }
                }
                else
                {
                    _logger.Trace("Not changing path for: {0}", s.Title);
                }

                UpdateAutoTaggingTags(s);
            }

            _seriesRepository.UpdateMany(series);
            _logger.Debug("{0} platforms updated", series.Count);
            _eventAggregator.PublishEvent(new PlatformBulkEditedEvent(series));

            if (moveQueue.Any())
            {
                _commandQueueManager.Push(new BulkMovePlatformCommand
                {
                    DestinationRootFolder = destinationRootFolder,
                    Series = moveQueue
                });
            }

            return series;
        }

        public Series UpdateSeries(Series series, string sourcePath, string destinationPath, bool moveFiles)
        {
            var queueMove = false;

            if (GetSeriesIdsWithPendingMove().Contains(series.Id))
            {
                series.Path = sourcePath;

                _logger.Warn("Platform '{0}' is already being moved from {1} to {2}, skipping path update.", series.Title, series.Path, series.PendingPath);
            }
            else if (moveFiles)
            {
                series.Path = sourcePath;
                series.PendingPath = destinationPath;
                queueMove = true;

                _logger.Trace("Path for '{0}' will be updated from {1} to {2} after files are moved successfully", series.Title, series.Path, destinationPath);
            }
            else
            {
                series.Path = destinationPath;
                series.PendingPath = null;

                _logger.Trace("Changing path for {0} to {1}", series.Title, series.Path);
            }

            var updatedSeries = UpdateSeries(series, true, true);

            if (queueMove)
            {
                _commandQueueManager.Push(new MovePlatformCommand
                {
                    SeriesId = series.Id,
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath
                },
                    trigger: CommandTrigger.Manual);
            }

            return updatedSeries;
        }

        private HashSet<int> GetSeriesIdsWithPendingMove()
        {
            var seriesIds = new HashSet<int>();

            foreach (var command in _commandQueueManager.All())
            {
                if (command.Status is not (CommandStatus.Queued or CommandStatus.Started))
                {
                    continue;
                }

                if (command.Body is MovePlatformCommand moveCommand)
                {
                    seriesIds.Add(moveCommand.SeriesId);
                }
                else if (command.Body is BulkMovePlatformCommand bulkCommand)
                {
                    foreach (var m in bulkCommand.Series)
                    {
                        seriesIds.Add(m.SeriesId);
                    }
                }
            }

            return seriesIds;
        }

        [EventHandleOrder(EventHandleOrder.Last)]
        public void Handle(ApplicationStartedEvent message)
        {
            ClearStalePendingPaths();
        }

        private void ClearStalePendingPaths()
        {
            var seriesWithPendingPath = _seriesRepository.All()
                .Where(s => s.PendingPath.IsNotNullOrWhiteSpace())
                .ToList();

            if (seriesWithPendingPath.Empty())
            {
                return;
            }

            var pendingMoveSeriesIds = GetSeriesIdsWithPendingMove();
            var staleSeries = seriesWithPendingPath.Where(s => !pendingMoveSeriesIds.Contains(s.Id)).ToList();

            if (staleSeries.Empty())
            {
                return;
            }

            foreach (var series in staleSeries)
            {
                _logger.Warn("Platform '{0}' has a stale PendingPath ({1}) left over from an interrupted move, clearing it.", series.Title, series.PendingPath);
                series.PendingPath = null;
            }

            _seriesRepository.SetFields(staleSeries, s => s.PendingPath);
        }

        public bool SeriesPathExists(string folder)
        {
            return _seriesRepository.SeriesPathExists(folder);
        }

        public void RemoveAddOptions(Series series)
        {
            _seriesRepository.SetFields(series, s => s.AddOptions);
        }

        public bool UpdateAutoTaggingTags(Series series)
        {
            _logger.Trace("Updating tags for {0}", series);

            var tagsAdded = new HashSet<int>();
            var tagsRemoved = new HashSet<int>();
            var changes = _autoTaggingService.GetTagChanges(series);

            foreach (var tag in changes.TagsToRemove)
            {
                if (series.Tags.Contains(tag))
                {
                    series.Tags.Remove(tag);
                    tagsRemoved.Add(tag);
                }
            }

            foreach (var tag in changes.TagsToAdd)
            {
                if (!series.Tags.Contains(tag))
                {
                    series.Tags.Add(tag);
                    tagsAdded.Add(tag);
                }
            }

            if (tagsAdded.Any() || tagsRemoved.Any())
            {
                _logger.Debug("Updated tags for '{0}'. Added: {1}, Removed: {2}", series.Title, tagsAdded.Count, tagsRemoved.Count);

                return true;
            }

            _logger.Debug("Tags not updated for '{0}'", series.Title);

            return false;
        }

        public void UpdateTags(List<Series> series)
        {
            if (series.Count == 0)
            {
                return;
            }

            _seriesRepository.SetFields(series, s => s.Tags);
            _eventAggregator.PublishEvent(new PlatformBulkEditedEvent(series));
        }
    }

    public class SeriesService : PlatformService
    {
        public SeriesService(ISeriesRepository seriesRepository,
                             IEventAggregator eventAggregator,
                             IEpisodeService episodeService,
                             IBuildSeriesPaths seriesPathBuilder,
                             IAutoTaggingService autoTaggingService,
                             IManageCommandQueue commandQueueManager,
                             Logger logger)
            : base(seriesRepository, eventAggregator, episodeService, seriesPathBuilder, autoTaggingService, commandQueueManager, logger)
        {
        }
    }
}
