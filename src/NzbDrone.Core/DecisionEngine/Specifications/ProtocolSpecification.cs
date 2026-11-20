using NLog;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class ProtocolSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IDelayProfileService _delayProfileService;
        private readonly Logger _logger;

        public ProtocolSpecification(IDelayProfileService delayProfileService, Logger logger)
        {
            _delayProfileService = delayProfileService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            _logger.Debug("Checking if release protocol is valid: {0}", subject.Release.DownloadProtocol);

            if (subject.Release.DownloadProtocol == DownloadProtocol.Unknown)
            {
                _logger.Debug("[{0}] Download protocol is unknown", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.ProtocolDisabled, "Download protocol is unknown");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
