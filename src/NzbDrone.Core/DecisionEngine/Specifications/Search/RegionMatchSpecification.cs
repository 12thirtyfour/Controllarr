using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class RegionMatchSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public RegionMatchSpecification(Logger logger)
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

            var singleEpisodeSpec = information.SearchCriteria as SeasonSearchCriteria;

            if (singleEpisodeSpec == null)
            {
                return DownloadSpecDecision.Accept();
            }

            if (singleEpisodeSpec.IsPlatformCollectionSearch || singleEpisodeSpec.SeasonNumber <= 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var seasonNumbers = remoteEpisode.ParsedEpisodeInfo.SeasonNumbers;

            var matches = seasonNumbers.Count > 0
                ? seasonNumbers.Contains(singleEpisodeSpec.SeasonNumber)
                : singleEpisodeSpec.SeasonNumber == remoteEpisode.MappedSeasonNumber;

            if (!matches)
            {
                _logger.Debug("Region does not match searched region, skipping.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeason, "Wrong region");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
