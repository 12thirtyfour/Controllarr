using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public interface IUpgradableSpecification
    {
        UpgradeableRejectReason IsUpgradable(QualityProfile profile, QualityModel currentQuality, List<CustomFormat> currentCustomFormats, QualityModel newQuality, List<CustomFormat> newCustomFormats);
        bool QualityCutoffNotMet(QualityProfile profile, QualityModel currentQuality, QualityModel newQuality = null);
        bool CutoffNotMet(QualityProfile profile, QualityModel currentQuality, List<CustomFormat> currentCustomFormats, QualityModel newQuality = null);
        bool IsRevisionUpgrade(QualityModel currentQuality, QualityModel newQuality);
        bool IsUpgradeAllowed(QualityProfile qualityProfile, QualityModel currentQuality, List<CustomFormat> currentCustomFormats, QualityModel newQuality, List<CustomFormat> newCustomFormats);
    }

    public class UpgradableSpecification : IUpgradableSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public UpgradableSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public UpgradeableRejectReason IsUpgradable(QualityProfile qualityProfile, QualityModel currentQuality, List<CustomFormat> currentCustomFormats, QualityModel newQuality, List<CustomFormat> newCustomFormats)
        {
            if (currentQuality == null)
            {
                return UpgradeableRejectReason.None;
            }

            var downloadPropersAndRepacks = _configService.DownloadPropersAndRepacks;
            var qualityRevisionCompare = newQuality?.Revision.CompareTo(currentQuality.Revision) ?? 0;

            if (downloadPropersAndRepacks != ProperDownloadTypes.DoNotPrefer && qualityRevisionCompare > 0)
            {
                _logger.Debug("New item has a better quality revision. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.None;
            }

            var currentId = currentQuality.Quality?.Id ?? 0;
            var newId = newQuality?.Quality?.Id ?? 0;

            if (newId > currentId)
            {
                _logger.Debug("New item has a better quality. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.None;
            }

            if (newId < currentId)
            {
                _logger.Debug("Existing item has better quality, skipping. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.BetterQuality;
            }

            if (downloadPropersAndRepacks != ProperDownloadTypes.DoNotPrefer && qualityRevisionCompare < 0)
            {
                _logger.Debug("Existing item has a better quality revision, skipping. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.BetterRevision;
            }

            return UpgradeableRejectReason.None;
        }

        public bool QualityCutoffNotMet(QualityProfile profile, QualityModel currentQuality, QualityModel newQuality = null)
        {
            if (currentQuality == null)
            {
                return true;
            }

            var isUnverified = currentQuality.Quality == Quality.Unknown || currentQuality.Quality == Quality.UnverifiedRaw;
            if (isUnverified)
            {
                return true;
            }

            var currentId = currentQuality.Quality?.Id ?? 0;
            var newId = newQuality?.Quality?.Id ?? 0;
            if (newId > currentId)
            {
                return true;
            }

            if (newQuality != null && IsRevisionUpgrade(currentQuality, newQuality))
            {
                return true;
            }

            return false;
        }

        private bool CustomFormatCutoffNotMet(QualityProfile profile, List<CustomFormat> currentFormats)
        {
            if (profile == null)
            {
                return false;
            }

            var score = profile.CalculateCustomFormatScore(currentFormats);
            var cutoff = profile.UpgradeAllowed ? profile.CutoffFormatScore : profile.MinFormatScore;

            return score < cutoff;
        }

        public bool CutoffNotMet(QualityProfile profile, QualityModel currentQuality, List<CustomFormat> currentFormats, QualityModel newQuality = null)
        {
            if (QualityCutoffNotMet(profile, currentQuality, newQuality))
            {
                return true;
            }

            if (CustomFormatCutoffNotMet(profile, currentFormats))
            {
                return true;
            }

            return false;
        }

        public bool IsRevisionUpgrade(QualityModel currentQuality, QualityModel newQuality)
        {
            if (currentQuality == null || newQuality == null)
            {
                return false;
            }

            var compare = newQuality.Revision.CompareTo(currentQuality.Revision);

            if (currentQuality.Quality == newQuality.Quality && compare > 0)
            {
                _logger.Debug("New quality is a better revision for existing quality. Existing: {0}. New: {1}", currentQuality, newQuality);
                return true;
            }

            return false;
        }

        public bool IsUpgradeAllowed(QualityProfile qualityProfile, QualityModel currentQuality, List<CustomFormat> currentCustomFormats, QualityModel newQuality, List<CustomFormat> newCustomFormats)
        {
            return IsUpgradable(qualityProfile, currentQuality, currentCustomFormats, newQuality, newCustomFormats) == UpgradeableRejectReason.None;
        }
    }
}
