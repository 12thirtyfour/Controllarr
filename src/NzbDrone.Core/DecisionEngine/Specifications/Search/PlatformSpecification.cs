using NLog;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class PlatformSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public PlatformSpecification(Logger logger)
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

            var platformSlug = searchCriteria.Series?.TitleSlug ?? remoteEpisode.Series?.TitleSlug;
            var platformName = searchCriteria.Series?.Title ?? remoteEpisode.Series?.Title;
            var title = remoteEpisode.Release?.Title;
            var candidatePath = remoteEpisode.Release?.Origin ?? remoteEpisode.Release?.DownloadUrl;
            var format = remoteEpisode.Release?.Container ?? remoteEpisode.ParsedRomInfo?.Format;

            if (!string.IsNullOrWhiteSpace(platformSlug))
            {
                if (!string.IsNullOrWhiteSpace(title) &&
                    !PlatformCompatibilityValidator.IsCompatible(title, platformSlug, platformName, format))
                {
                    _logger.Debug("Release title '{0}' is not compatible with platform '{1}'", title, platformName);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, $"File format or platform tag does not match {platformName}");
                }

                if (!string.IsNullOrWhiteSpace(candidatePath) &&
                    !string.Equals(candidatePath, title, System.StringComparison.OrdinalIgnoreCase) &&
                    !PlatformCompatibilityValidator.IsCompatible(candidatePath, platformSlug, platformName, format))
                {
                    _logger.Debug("Release path '{0}' is not compatible with platform '{1}'", candidatePath, platformName);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongSeries, $"File format or platform tag does not match {platformName}");
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
