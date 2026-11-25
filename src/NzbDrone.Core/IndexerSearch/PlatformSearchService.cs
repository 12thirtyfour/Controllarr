using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles.Patching;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.IndexerSearch
{
    public class PlatformSearchService : IExecute<PlatformSearchCommand>,
                                         IHandle<ApplicationShutdownRequested>
    {
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly IRomHackAutomationService _romHackAutomationService;
        private readonly Logger _logger;
        private volatile bool _shutdownRequested;

        public PlatformSearchService(ISeriesService seriesService,
                                   IEpisodeService episodeService,
                                   ISearchForReleases releaseSearchService,
                                   IProcessDownloadDecisions processDownloadDecisions,
                                   Logger logger,
                                   IRomHackAutomationService romHackAutomationService = null)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _romHackAutomationService = romHackAutomationService;
            _logger = logger;
        }

        public void Execute(PlatformSearchCommand message)
        {
            var series = _seriesService.GetSeries(message.SeriesId);
            var downloadedCount = 0;
            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;

            if (message.SearchCollectionFirst &&
                !Games.PlatformCatalogService.IsDiscBasedPlatform(series.TitleSlug, series.Title) &&
                CollectionSearchHelper.TryGrabCollection(_releaseSearchService, _processDownloadDecisions, _logger, series.Id, userInvokedSearch))
            {
                return;
            }

            var allEpisodes = _episodeService.GetEpisodeBySeries(series.Id);
            var monitoredSeasons = series.Seasons
                .Where(s => s.Monitored)
                .Select(s => s.SeasonNumber)
                .ToHashSet();

            var episodes = allEpisodes.Where(e =>
            {
                if (!e.Monitored || e.HasFile)
                {
                    return false;
                }

                if (monitoredSeasons.Count > 0 && !monitoredSeasons.Contains(e.SeasonNumber))
                {
                    return false;
                }

                if (e.AirDateUtc.HasValue && e.AirDateUtc.Value.After(DateTime.UtcNow))
                {
                    return false;
                }

                return true;
            })
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)
            .ToList();

            _logger.ProgressInfo("Searching for {0} monitored games on {1}...", episodes.Count, series.Title);

            foreach (var episode in episodes)
            {
                if (_shutdownRequested)
                {
                    _logger.Info("Stopping platform search for {0} due to application shutdown.", series.Title);
                    break;
                }

                try
                {
                    if (_romHackAutomationService != null &&
                        (episode.SeasonNumber == GameCatalogProxy.RegionRomHacks ||
                         episode.SeasonNumber == GameCatalogProxy.RegionTranslations))
                    {
                        var patchOutcome = _romHackAutomationService.TryAcquirePatchForGame(series, episode);
                        if (patchOutcome != null && patchOutcome.Success)
                        {
                            downloadedCount++;
                            continue;
                        }
                    }

                    var decisions = _releaseSearchService.EpisodeSearch(episode, userInvokedSearch, false).GetAwaiter().GetResult();
                    var processDecisions = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processDecisions.Grabbed.Count;
                }
                catch (Exception ex)
                {
                    if (_shutdownRequested || SearchShutdownHelper.IsContainerDisposedException(ex))
                    {
                        _logger.Info("Stopping platform search for {0} because the application container is shutting down.", series.Title);
                        break;
                    }

                    _logger.Error(ex, "Unable to search for game: [{0}]", episode.Title);
                }
            }

            _logger.ProgressInfo("Platform search completed. {0} reports downloaded.", downloadedCount);
        }

        public void Handle(ApplicationShutdownRequested message)
        {
            _shutdownRequested = true;
        }
    }
}
