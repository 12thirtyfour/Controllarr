using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Extensions;
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

            var qualityComparer = new QualityModelComparer(qualityProfile);
            var qualityCompare = qualityComparer.Compare(newQuality?.Quality, currentQuality.Quality);
            var downloadPropersAndRepacks = _configService.DownloadPropersAndRepacks;

            if (qualityCompare > 0 && QualityCutoffNotMet(qualityProfile, currentQuality, newQuality))
            {
                _logger.Debug("New item has a better quality. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.None;
            }

            if (qualityCompare < 0)
            {
                _logger.Debug("Existing item has better quality, skipping. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.BetterQuality;
            }

            var qualityRevisionCompare = newQuality?.Revision.CompareTo(currentQuality.Revision) ?? 0;

            // Accept unless the user doesn't want to prefer propers, optionally they can
            // use preferred words to prefer propers/repacks over non-propers/repacks.
            if (downloadPropersAndRepacks != ProperDownloadTypes.DoNotPrefer &&
                qualityRevisionCompare > 0)
            {
                _logger.Debug("New item has a better quality revision, skipping. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.None;
            }

            if (qualityProfile != null && !qualityProfile.UpgradeAllowed)
            {
                _logger.Debug("Quality profile '{0}' does not allow upgrading. Skipping.", qualityProfile.Name);

                return UpgradeableRejectReason.UpgradesNotAllowed;
            }

            // Reject unless the user does not prefer propers/repacks and it's a revision downgrade.
            if (downloadPropersAndRepacks != ProperDownloadTypes.DoNotPrefer &&
                qualityRevisionCompare < 0)
            {
                _logger.Debug("Existing item has a better quality revision, skipping. Existing: {0}. New: {1}", currentQuality, newQuality);
                return UpgradeableRejectReason.BetterRevision;
            }

            if (qualityCompare > 0)
            {
                if (qualityProfile != null && qualityProfile.Items != null && qualityProfile.Items.Count > 0)
                {
                    _logger.Debug("Existing item meets cut-off for quality, skipping. Existing: {0}. Cutoff: {1}",
                        currentQuality,
                        qualityProfile.Items[qualityProfile.GetIndex(qualityProfile.Cutoff).Index]);
                }

                return UpgradeableRejectReason.QualityCutoff;
            }

            var currentFormatScore = qualityProfile?.CalculateCustomFormatScore(currentCustomFormats) ?? 0;
            var newFormatScore = qualityProfile?.CalculateCustomFormatScore(newCustomFormats) ?? 0;

            if (newFormatScore <= currentFormatScore)
            {
                _logger.Debug("New item's custom formats [{0}] ({1}) do not improve on [{2}] ({3}), skipping",
                    newCustomFormats.ConcatToString(),
                    newFormatScore,
                    currentCustomFormats.ConcatToString(),
                    currentFormatScore);
                return UpgradeableRejectReason.CustomFormatScore;
            }

            if (qualityProfile != null && currentFormatScore >= qualityProfile.CutoffFormatScore)
            {
                _logger.Debug("Existing item meets cut-off for custom formats, skipping. Existing: [{0}] ({1}). Cutoff score: {2}",
                    currentCustomFormats.ConcatToString(),
                    currentFormatScore,
                    qualityProfile.CutoffFormatScore);
                return UpgradeableRejectReason.CustomFormatCutoff;
            }

            if (qualityProfile != null && newFormatScore < currentFormatScore + qualityProfile.MinUpgradeFormatScore)
            {
                _logger.Debug("New item's custom formats [{0}] ({1}) do not meet minimum custom format score increment of {2} required for upgrade, skipping. Existing: [{3}] ({4}).",
                              newCustomFormats.ConcatToString(),
                              newFormatScore,
                              qualityProfile.MinUpgradeFormatScore,
                              currentCustomFormats.ConcatToString(),
                              currentFormatScore);
                return UpgradeableRejectReason.MinCustomFormatScore;
            }

            _logger.Debug("New item's custom formats [{0}] ({1}) improve on [{2}] ({3}), accepting",
                newCustomFormats.ConcatToString(),
                newFormatScore,
                currentCustomFormats.ConcatToString(),
                currentFormatScore);
            return UpgradeableRejectReason.None;
        }

        public bool QualityCutoffNotMet(QualityProfile profile, QualityModel currentQuality, QualityModel newQuality = null)
        {
            if (currentQuality == null)
            {
                return true;
            }

            if (profile == null)
            {
                return true;
            }

            var cutoff = profile.UpgradeAllowed ? profile.Cutoff : profile.FirststAllowedQuality().Id;
            var cutoffCompare = new QualityModelComparer(profile).Compare(currentQuality.Quality.Id, cutoff);

            if (cutoffCompare < 0)
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

            _logger.Debug("Existing item meets cut-off, skipping. Existing: {0} [{1}] ({2})",
                currentQuality,
                currentFormats.ConcatToString(),
                profile.CalculateCustomFormatScore(currentFormats));

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
            var isQualityUpgrade = new QualityModelComparer(qualityProfile).Compare(newQuality, currentQuality) > 0;
            var isCustomFormatUpgrade = qualityProfile != null && qualityProfile.CalculateCustomFormatScore(newCustomFormats) > qualityProfile.CalculateCustomFormatScore(currentCustomFormats);

            if (IsRevisionUpgrade(currentQuality, newQuality))
            {
                _logger.Debug("New quality '{0}' is a revision upgrade for '{1}'", newQuality, currentQuality);
                return true;
            }

            if ((isQualityUpgrade || isCustomFormatUpgrade) && (qualityProfile?.UpgradeAllowed ?? false))
            {
                _logger.Debug("Quality profile allows upgrading");
                return true;
            }

            if ((isQualityUpgrade || isCustomFormatUpgrade) && !(qualityProfile?.UpgradeAllowed ?? false))
            {
                _logger.Debug("Quality profile does not allow upgrades, skipping");
                return false;
            }

            return true;
        }
    }
}
