using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.IndexerSearch
{
    public class RegionSearchService : IExecute<RegionSearchCommand>
    {
        private readonly IEpisodeService _episodeService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public RegionSearchService(IEpisodeService episodeService,
                                   ISearchForReleases releaseSearchService,
                                   IProcessDownloadDecisions processDownloadDecisions,
                                   Logger logger)
        {
            _episodeService = episodeService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(RegionSearchCommand message)
        {
            var downloadedCount = 0;
            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;

            var episodes = _episodeService.GetEpisodesBySeason(message.SeriesId, message.SeasonNumber)
                .Where(e => e.Monitored &&
                            !e.HasFile &&
                            (!e.AirDateUtc.HasValue || e.AirDateUtc.Value.Before(DateTime.UtcNow)))
                .OrderBy(e => e.EpisodeNumber)
                .ToList();

            _logger.ProgressInfo("Starting search for {0} monitored games in region {1}...", episodes.Count, message.SeasonNumber);

            foreach (var episode in episodes)
            {
                try
                {
                    var decisions = _releaseSearchService.EpisodeSearch(episode, userInvokedSearch, false).GetAwaiter().GetResult();
                    var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processed.Grabbed.Count;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for game: [{0}]", episode.Title);
                }
            }

            _logger.ProgressInfo("Region search completed. {0} releases downloaded.", downloadedCount);
        }
    }
}
