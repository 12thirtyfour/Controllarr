using System.Linq;
using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class GameRequestedSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public GameRequestedSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            var criteriaEpisodes = information.SearchCriteria.Episodes.Select(v => v.Id).ToHashSet();

            if (!remoteEpisode.Episodes.Any(e => criteriaEpisodes.Contains(e.Id)))
            {
                _logger.Debug("Release rejected since the game wasn't requested: {0}", remoteEpisode.ParsedEpisodeInfo);

                var firstMismatch = remoteEpisode.Episodes
                    .OrderBy(v => v.SeasonNumber)
                    .ThenBy(v => v.EpisodeNumber)
                    .FirstOrDefault();

                if (firstMismatch != null)
                {
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongEpisode, $"Game wasn't requested: {firstMismatch.Title}");
                }

                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongEpisode, "Game wasn't requested");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
