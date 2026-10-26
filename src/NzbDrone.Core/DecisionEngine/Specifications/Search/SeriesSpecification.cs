using NLog;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class SeriesSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public SeriesSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode remoteEpisode, ReleaseDecisionInformation information)
        {
            var searchCriteria = information.SearchCriteria;

            if (searchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Checking if platform matches searched platform");

            if (remoteEpisode.Series.Id != searchCriteria.Series.Id)
            {
                _logger.Debug("Platform {0} does not match {1}", remoteEpisode.Series, searchCriteria.Series);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, "Wrong platform");
            }

            var platformSlug = searchCriteria.Series?.TitleSlug;
            var platformName = searchCriteria.Series?.Title;
            var candidatePath = remoteEpisode.Release?.Origin ?? remoteEpisode.Release?.DownloadUrl ?? remoteEpisode.Release?.Title;

            if (!string.IsNullOrWhiteSpace(candidatePath) && !string.IsNullOrWhiteSpace(platformSlug))
            {
                var format = remoteEpisode.Release?.Container ?? remoteEpisode.ParsedRomInfo?.Format;
                if (!PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName, format))
                {
                    _logger.Debug("Release '{0}' is not compatible with platform '{1}'", candidatePath, platformName);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, $"File format or platform tag does not match {platformName}");
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
