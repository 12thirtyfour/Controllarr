using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Releases;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class FullSeasonSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IReleaseProfileService _releaseProfileService;
        private readonly Logger _logger;

        public FullSeasonSpecification(IReleaseProfileService releaseProfileService, Logger logger)
        {
            _releaseProfileService = releaseProfileService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (subject.ParsedEpisodeInfo.FullSeason && !AllowedWithoutAllEpisodesAired(subject))
            {
                _logger.Debug("Checking if all games in full region release have released. {0}", subject.Release.Title);

                if (subject.Episodes.Any(e => !e.AirDateUtc.HasValue || e.AirDateUtc.Value.After(DateTime.UtcNow.AddHours(24))))
                {
                    _logger.Debug("Full region release {0} rejected. All games haven't released yet.", subject.Release.Title);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.FullSeasonNotAired, "Full region release rejected. All games haven't released yet.");
                }
            }

            return DownloadSpecDecision.Accept();
        }

        private bool AllowedWithoutAllEpisodesAired(RemoteEpisode subject)
        {
            var releaseProfiles = _releaseProfileService.EnabledForTags(subject.Series.Tags, subject.Release.IndexerId);

            if (releaseProfiles.Empty())
            {
                return false;
            }

            // If multiple Release Profiles apply, the most restrictive one (i.e. the one that still
            // requires all episodes to have aired) wins.
            var bestProfile = releaseProfiles.OrderBy(p => p.AllowSeasonPackWithoutAllEpisodesAired).First();

            return bestProfile.AllowSeasonPackWithoutAllEpisodesAired;
        }
    }
}
