using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Profiles.Qualities;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Indexers
{
    public abstract class ReleaseControllerBase : RestController<ReleaseResource>
    {
        private readonly QualityProfile _qualityProfile;

        public ReleaseControllerBase(IQualityProfileService qualityProfileService)
        {
            _qualityProfile = qualityProfileService.GetDefaultProfile(string.Empty);
        }

        [NonAction]
        public override Results<Ok<ReleaseResource>, NotFound> GetResourceByIdWithErrorHandler(int id)
        {
            return base.GetResourceByIdWithErrorHandler(id);
        }

        protected override ReleaseResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        protected virtual List<ReleaseResource> MapDecisions(IEnumerable<DownloadDecision> decisions)
        {
            var result = new List<ReleaseResource>();

            foreach (var downloadDecision in decisions)
            {
                if (PlatformCompatibilityValidator.IsTvOrVideoRelease(downloadDecision.RemoteEpisode?.Release?.Title))
                {
                    continue;
                }

                if (downloadDecision.RemoteEpisode?.Series != null &&
                    !string.IsNullOrWhiteSpace(downloadDecision.RemoteEpisode.Release?.Title) &&
                    !PlatformCompatibilityValidator.IsCompatible(downloadDecision.RemoteEpisode.Release.Title, downloadDecision.RemoteEpisode.Series.TitleSlug, downloadDecision.RemoteEpisode.Series.Title))
                {
                    continue;
                }

                var release = MapDecision(downloadDecision, result.Count);

                result.Add(release);
            }

            return result;
        }

        protected virtual ReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var release = decision.ToResource();

            release.ReleaseWeight = initialWeight;

            var qualityId = release.Quality?.Quality?.Id ?? 0;
            var index = _qualityProfile?.GetIndex(release.Quality.Quality).Index ?? qualityId;
            release.QualityWeight = index * 100;

            release.QualityWeight += release.Quality.Revision.Real * 10;
            release.QualityWeight += release.Quality.Revision.Version;

            return release;
        }
    }
}
