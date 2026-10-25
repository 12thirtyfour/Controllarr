using System.Linq;
using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.RssSync
{
    public class MonitoredEpisodeSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MonitoredEpisodeSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is { MonitoredEpisodesOnly: false })
            {
                _logger.Debug("Skipping monitored check during search");
                return DownloadSpecDecision.Accept();
            }

            if (!subject.Series.Monitored)
            {
                _logger.Debug("{0} is present in the DB but not tracked. Rejecting", subject.Series);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.SeriesNotMonitored, "Platform is not monitored");
            }

            var monitoredCount = subject.Episodes.Count(episode => episode.Monitored);
            if (monitoredCount == subject.Episodes.Count)
            {
                return DownloadSpecDecision.Accept();
            }

            if (subject.Episodes.Count == 1)
            {
                _logger.Debug("Game is not monitored. Rejecting", monitoredCount, subject.Episodes.Count);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.EpisodeNotMonitored, "Game is not monitored");
            }

            if (monitoredCount == 0)
            {
                _logger.Debug("No games in the release are monitored. Rejecting", monitoredCount, subject.Episodes.Count);
            }
            else
            {
                _logger.Debug("Only {0}/{1} games in the release are monitored. Rejecting", monitoredCount, subject.Episodes.Count);
            }

            return DownloadSpecDecision.Reject(DownloadRejectionReason.EpisodeNotMonitored, "One or more games is not monitored");
        }
    }
}
