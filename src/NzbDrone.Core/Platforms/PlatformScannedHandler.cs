using NLog;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public class PlatformScannedHandler : IHandle<PlatformScannedEvent>,
                                        IHandle<PlatformScanSkippedEvent>
    {
        private readonly IGameMonitoredService _episodeMonitoredService;
        private readonly ISeriesService _seriesService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IGameRefreshedService _episodeRefreshedService;
        private readonly IEventAggregator _eventAggregator;

        private readonly Logger _logger;

        public PlatformScannedHandler(IGameMonitoredService episodeMonitoredService,
                                    ISeriesService seriesService,
                                    IManageCommandQueue commandQueueManager,
                                    IGameRefreshedService episodeRefreshedService,
                                    IEventAggregator eventAggregator,
                                    Logger logger)
        {
            _episodeMonitoredService = episodeMonitoredService;
            _seriesService = seriesService;
            _commandQueueManager = commandQueueManager;
            _episodeRefreshedService = episodeRefreshedService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        private void HandleScanEvents(Series series)
        {
            var addOptions = series.AddOptions;

            if (addOptions == null)
            {
                _episodeRefreshedService.Search(series);
                return;
            }

            _logger.Info("[{0}] was recently added, performing post-add actions", series.Title);
            _episodeMonitoredService.SetEpisodeMonitoredStatus(series, addOptions);

            // If both options are enabled search for the whole series, which will only include monitored episodes.
            // This way multiple searches for the same season are skipped, though a season that can't be upgraded may be
            // searched, but the logs will be more explicit.

            if (addOptions.Monitor == MonitorTypes.None)
            {
                _logger.Info("[{0}] was added with monitoring set to None; skipping automatic search", series.Title);
            }
            else if (addOptions.SearchForMissingEpisodes && addOptions.SearchForCutoffUnmetEpisodes)
            {
                _commandQueueManager.Push(new PlatformSearchCommand(series.Id) { SearchCollectionFirst = true });
            }
            else
            {
                if (addOptions.SearchForMissingEpisodes)
                {
                    _commandQueueManager.Push(new MissingGameSearchCommand(series.Id) { SearchCollectionFirst = true });
                }

                if (addOptions.SearchForCutoffUnmetEpisodes)
                {
                    _commandQueueManager.Push(new CutoffUnmetGameSearchCommand(series.Id));
                }
            }

            series.AddOptions = null;
            _seriesService.RemoveAddOptions(series);

            _eventAggregator.PublishEvent(new PlatformAddCompletedEvent(series));
        }

        public void Handle(PlatformScannedEvent message)
        {
            HandleScanEvents(message.Series);
        }

        public void Handle(PlatformScanSkippedEvent message)
        {
            HandleScanEvents(message.Series);
        }
    }
}
