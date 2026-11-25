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
    public class RegionSearchService : IExecute<RegionSearchCommand>,
                                       IHandle<ApplicationShutdownRequested>
    {
        private readonly IEpisodeService _episodeService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly ISeriesService _seriesService;
        private readonly IRomHackAutomationService _romHackAutomationService;
        private readonly Logger _logger;
        private volatile bool _shutdownRequested;

        public RegionSearchService(IEpisodeService episodeService,
                                   ISearchForReleases releaseSearchService,
                                   IProcessDownloadDecisions processDownloadDecisions,
                                   Logger logger,
                                   ISeriesService seriesService = null,
                                   IRomHackAutomationService romHackAutomationService = null)
        {
            _episodeService = episodeService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _seriesService = seriesService;
            _romHackAutomationService = romHackAutomationService;
            _logger = logger;
        }

        public void Execute(RegionSearchCommand message)
        {
            var downloadedCount = 0;
            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;
            Series series = null;

            if ((message.SeasonNumber == GameCatalogProxy.RegionRomHacks ||
                 message.SeasonNumber == GameCatalogProxy.RegionTranslations) &&
                _seriesService != null)
            {
                series = _seriesService.GetSeries(message.SeriesId);
            }

            var episodes = _episodeService.GetEpisodesBySeason(message.SeriesId, message.SeasonNumber)
                .Where(e => e.Monitored &&
                            !e.HasFile &&
                            (!e.AirDateUtc.HasValue || e.AirDateUtc.Value.Before(DateTime.UtcNow)))
                .OrderBy(e => e.EpisodeNumber)
                .ToList();

            _logger.ProgressInfo("Starting search for {0} monitored games in region {1}...", episodes.Count, message.SeasonNumber);

            foreach (var episode in episodes)
            {
                if (_shutdownRequested)
                {
                    _logger.Info("Stopping region search due to application shutdown.");
                    break;
                }

                try
                {
                    if (_romHackAutomationService != null &&
                        series != null &&
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
                    var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processed.Grabbed.Count;
                }
                catch (Exception ex)
                {
                    if (_shutdownRequested || SearchShutdownHelper.IsContainerDisposedException(ex))
                    {
                        _logger.Info("Stopping region search because the application container is shutting down.");
                        break;
                    }

                    _logger.Error(ex, "Unable to search for game: [{0}]", episode.Title);
                }
            }

            _logger.ProgressInfo("Region search completed. {0} releases downloaded.", downloadedCount);
        }

        public void Handle(ApplicationShutdownRequested message)
        {
            _shutdownRequested = true;
        }
    }
}
