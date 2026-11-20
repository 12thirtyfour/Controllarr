using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class PlatformPackOnlySpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public PlatformPackOnlySpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            var searchCriteria = information.SearchCriteria;

            if (searchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            var isCollectionPackage = subject.ParsedEpisodeInfo?.FullSeason == true ||
                                      subject.ParsedRomInfo?.IsFullSetArchive == true;

            if (searchCriteria is SeasonSearchCriteria { IsPlatformCollectionSearch: true })
            {
                if (!isCollectionPackage)
                {
                    _logger.Debug("Release {0}: single ROM release rejected during collection search; collection package required.", subject.Release.Title);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.NotSeasonPack, "Single ROM release rejected during collection search; collection package required.");
                }

                return DownloadSpecDecision.Accept();
            }

            if (searchCriteria is SingleEpisodeSearchCriteria)
            {
                if (isCollectionPackage)
                {
                    _logger.Debug("Release {0}: collection package rejected during individual game search; single ROM release required.", subject.Release.Title);
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.FullSeason, "Collection package rejected during individual game search; single ROM release required.");
                }

                return DownloadSpecDecision.Accept();
            }

            if (searchCriteria.Episodes.Count == 1)
            {
                return DownloadSpecDecision.Accept();
            }

            if (subject.Release.SeasonSearchMaximumSingleEpisodeAge > 0)
            {
                if (subject.Series.SeriesType == SeriesTypes.Standard && !subject.ParsedEpisodeInfo.FullSeason && subject.Episodes.Count >= 1)
                {
                    // test against episodes of the same season in the current search, and make sure they have an air date
                    var subset = searchCriteria.Episodes.Where(e => e.AirDateUtc.HasValue && e.SeasonNumber == subject.Episodes.First().SeasonNumber).ToList();

                    if (subset.Count > 0 && subset.Max(e => e.AirDateUtc).Value.Before(DateTime.UtcNow - TimeSpan.FromDays(subject.Release.SeasonSearchMaximumSingleEpisodeAge)))
                    {
                        _logger.Debug("Release {0}: last game in this region released more than {1} days ago, region pack required.", subject.Release.Title, subject.Release.SeasonSearchMaximumSingleEpisodeAge);
                        return DownloadSpecDecision.Reject(DownloadRejectionReason.NotSeasonPack, "Last game in this region released more than {0} days ago, region pack required.", subject.Release.SeasonSearchMaximumSingleEpisodeAge);
                    }
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
