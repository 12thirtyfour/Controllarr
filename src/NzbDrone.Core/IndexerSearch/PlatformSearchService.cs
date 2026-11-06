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
    public class PlatformSearchService : IExecute<PlatformSearchCommand>
    {
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public PlatformSearchService(ISeriesService seriesService,
                                   IEpisodeService episodeService,
                                   ISearchForReleases releaseSearchService,
                                   IProcessDownloadDecisions processDownloadDecisions,
                                   Logger logger)
        {
            _seriesService = seriesService;
            _episodeService = episodeService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(PlatformSearchCommand message)
        {
            var series = _seriesService.GetSeries(message.SeriesId);
            var downloadedCount = 0;
            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;

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
                try
                {
                    var decisions = _releaseSearchService.EpisodeSearch(episode, userInvokedSearch, false).GetAwaiter().GetResult();
                    var processDecisions = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
                    downloadedCount += processDecisions.Grabbed.Count;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for game: [{0}]", episode.Title);
                }
            }

            _logger.ProgressInfo("Platform search completed. {0} reports downloaded.", downloadedCount);
        }
    }
}
