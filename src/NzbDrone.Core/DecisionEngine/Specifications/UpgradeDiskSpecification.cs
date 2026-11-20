using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeDiskSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IConfigService _configService;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeDiskSpecification(UpgradableSpecification upgradableSpecification,
                                        IConfigService configService,
                                        ICustomFormatCalculationService formatService,
                                        Logger logger)
        {
            _configService = configService;
            _upgradableSpecification = upgradableSpecification;
            _formatService = formatService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            var qualityProfile = subject.Series.QualityProfile.Value;

            if (subject.ParsedEpisodeInfo.FullSeason)
            {
                var totalEpisodesInPack = subject.Episodes.Count;

                if (totalEpisodesInPack == 0)
                {
                    // Should not happen, but good to guard against it.
                    return DownloadSpecDecision.Accept();
                }

                // Count missing episodes as upgradable
                var missingEpisodesCount = subject.Episodes.Count(c => c.EpisodeFileId == 0);
                var upgradedCount = missingEpisodesCount;
                _logger.Debug("{0} games are missing from disk and are considered upgradable.", upgradedCount);

                // If all episodes in the pack are missing, accept it immediately without loading EpisodeFile records.
                if (missingEpisodesCount == totalEpisodesInPack)
                {
                    _logger.Debug("All games in region pack are missing, accepting.");
                    return DownloadSpecDecision.Accept();
                }

                var seasonPackUpgrade = _configService.SeasonPackUpgrade;
                var seasonPackUpgradeThreshold = _configService.SeasonPackUpgradeThreshold;
                if (seasonPackUpgrade == PlatformPackUpgradeType.Any && upgradedCount > 0)
                {
                    return DownloadSpecDecision.Accept();
                }

                var threshold = seasonPackUpgrade == PlatformPackUpgradeType.All
                    ? 100.0
                    : seasonPackUpgradeThreshold;

                if (seasonPackUpgrade != PlatformPackUpgradeType.Any && (double)upgradedCount / totalEpisodesInPack * 100 >= threshold)
                {
                    return DownloadSpecDecision.Accept();
                }

                // Check existing files lazily and stop early once the upgrade criteria are met.
                foreach (var episode in subject.Episodes)
                {
                    if (episode.EpisodeFileId == 0)
                    {
                        continue;
                    }

                    var file = episode.EpisodeFile.Value;
                    if (file == null)
                    {
                        continue;
                    }

                    _logger.Debug("Comparing file quality with report. Existing file is {0}.", file.Quality);

                    var customFormats = _formatService.ParseCustomFormat(file);

                    if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                            file.Quality,
                            customFormats,
                            subject.ParsedEpisodeInfo.Quality))
                    {
                        _logger.Debug("Cutoff already met for existing file, not an upgrade.");
                        continue;
                    }

                    var upgradeableRejectReason = _upgradableSpecification.IsUpgradable(qualityProfile,
                        file.Quality,
                        customFormats,
                        subject.ParsedEpisodeInfo.Quality,
                        subject.CustomFormats);

                    if (upgradeableRejectReason == UpgradeableRejectReason.None)
                    {
                        _logger.Debug("Existing game is upgradable.");
                        upgradedCount++;

                        if (seasonPackUpgrade == PlatformPackUpgradeType.Any ||
                            (double)upgradedCount / totalEpisodesInPack * 100 >= threshold)
                        {
                            return DownloadSpecDecision.Accept();
                        }
                    }
                }

                _logger.Debug("Total upgradable games: {0} out of {1}. Region import setting: {2}, Threshold: {3}%", upgradedCount, totalEpisodesInPack, seasonPackUpgrade, seasonPackUpgradeThreshold);
                var upgradablePercentage = (double)upgradedCount / totalEpisodesInPack * 100;

                return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskNotUpgrade, $"Region pack does not meet the upgrade criteria. Upgradable: {upgradedCount}/{totalEpisodesInPack} ({upgradablePercentage:0.##}%), Mode: {seasonPackUpgrade}, Threshold: {seasonPackUpgradeThreshold}%");
            }

            foreach (var file in subject.Episodes.Where(c => c.EpisodeFileId != 0).Select(c => c.EpisodeFile.Value))
            {
                var decision = CheckUpgradeSpecification(file, qualityProfile, subject);
                if (decision != null)
                {
                    return decision;
                }
            }

            return DownloadSpecDecision.Accept();
        }

        private DownloadSpecDecision CheckUpgradeSpecification(NzbDrone.Core.MediaFiles.EpisodeFile file, NzbDrone.Core.Profiles.Qualities.QualityProfile qualityProfile, RemoteEpisode subject)
        {
            if (file == null)
            {
                _logger.Debug("File is no longer available, skipping this file.");
                return null;
            }

            _logger.Debug("Comparing file quality with report. Existing file is {0}.", file.Quality);

            var customFormats = _formatService.ParseCustomFormat(file);

            if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                    file.Quality,
                    customFormats,
                    subject.ParsedEpisodeInfo.Quality))
            {
                _logger.Debug("Cutoff already met, rejecting.");
                var cutoff = qualityProfile.UpgradeAllowed ? qualityProfile.Cutoff : qualityProfile.FirststAllowedQuality().Id;
                var qualityCutoff = qualityProfile.Items[qualityProfile.GetIndex(cutoff).Index];
                return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCutoffMet, "Existing file meets cutoff: {0}", qualityCutoff);
            }

            var upgradeableRejectReason = _upgradableSpecification.IsUpgradable(qualityProfile,
                file.Quality,
                customFormats,
                subject.ParsedEpisodeInfo.Quality,
                subject.CustomFormats);

            switch (upgradeableRejectReason)
            {
                case UpgradeableRejectReason.None:
                    return null;

                case UpgradeableRejectReason.BetterQuality:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherPreference, "Existing file on disk is of equal or higher preference: {0}", file.Quality);

                case UpgradeableRejectReason.BetterRevision:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherRevision, "Existing file on disk is of equal or higher revision: {0}", file.Quality.Revision);

                case UpgradeableRejectReason.QualityCutoff:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCutoffMet, "Existing file on disk meets quality cutoff: {0}", qualityProfile.Items[qualityProfile.GetIndex(qualityProfile.Cutoff).Index]);

                case UpgradeableRejectReason.CustomFormatCutoff:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatCutoffMet, "Existing file on disk meets Custom Format cutoff: {0}", qualityProfile.CutoffFormatScore);

                case UpgradeableRejectReason.CustomFormatScore:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatScore, "Existing file on disk has a equal or higher Custom Format score: {0}", qualityProfile.CalculateCustomFormatScore(customFormats));

                case UpgradeableRejectReason.MinCustomFormatScore:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatScoreIncrement, "Existing file on disk has Custom Format score within Custom Format score increment: {0}", qualityProfile.MinUpgradeFormatScore);

                case UpgradeableRejectReason.UpgradesNotAllowed:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskUpgradesNotAllowed, "Existing file on disk and Quality Profile '{0}' does not allow upgrades", qualityProfile.Name);
            }

            return null;
        }
    }
}
