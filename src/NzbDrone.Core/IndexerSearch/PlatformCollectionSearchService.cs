using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.IndexerSearch
{
    public class PlatformCollectionSearchService : IExecute<PlatformCollectionSearchCommand>
    {
        private readonly ISeriesService _seriesService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public PlatformCollectionSearchService(
            ISeriesService seriesService,
            ISearchForReleases releaseSearchService,
            IProcessDownloadDecisions processDownloadDecisions,
            Logger logger)
        {
            _seriesService = seriesService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(PlatformCollectionSearchCommand message)
        {
            var series = _seriesService.GetSeries(message.SeriesId);
            _logger.ProgressInfo("Starting collection search for {0}...", series.Title);

            var userInvokedSearch = message.Trigger == CommandTrigger.Manual;

            var decisions = _releaseSearchService.PlatformCollectionSearch(
                series.Id,
                userInvokedSearch,
                false).GetAwaiter().GetResult();

            var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();
            var downloadedCount = processed.Grabbed.Count;

            _logger.ProgressInfo("Collection search for {0} completed. {1} reports downloaded.", series.Title, downloadedCount);
        }
    }
}
