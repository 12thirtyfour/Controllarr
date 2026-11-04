using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class QualityAllowedByProfileSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public QualityAllowedByProfileSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            _logger.Debug("Checking if report meets quality requirements. {0}", subject.ParsedEpisodeInfo.Quality);

            var profile = subject.Series.QualityProfile.Value;
            var quality = subject.ParsedEpisodeInfo.Quality?.Quality ?? Qualities.Quality.Unknown;
            var qualityIndex = profile.GetIndex(quality);
            var qualityOrGroup = (qualityIndex.Index >= 0 && qualityIndex.Index < profile.Items.Count)
                ? profile.Items[qualityIndex.Index]
                : null;

            if (qualityOrGroup == null || !qualityOrGroup.Allowed)
            {
                var isGamingQuality = quality.Id >= 100 || quality == Qualities.Quality.Unknown;
                if (isGamingQuality && (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Equals("Any", System.StringComparison.OrdinalIgnoreCase)))
                {
                    return DownloadSpecDecision.Accept();
                }

                _logger.Debug("Quality {0} rejected by platform's quality profile", subject.ParsedEpisodeInfo.Quality);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.QualityNotWanted, "{0} is not wanted in profile", subject.ParsedEpisodeInfo.Quality.Quality);
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
