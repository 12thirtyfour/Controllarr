using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.GameImport.Specifications
{
    public class MatchesGrabSpecification : IImportDecisionEngineSpecification
    {
        private readonly IEpisodeService _episodeService;
        private readonly Logger _logger;

        public MatchesGrabSpecification(IEpisodeService episodeService, Logger logger)
        {
            _episodeService = episodeService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.ExistingFile)
            {
                return ImportSpecDecision.Accept();
            }

            var releaseInfo = localEpisode.Release;

            if (releaseInfo == null || releaseInfo.EpisodeIds.Empty())
            {
                return ImportSpecDecision.Accept();
            }

            if (releaseInfo.ReleaseType == ReleaseType.SeasonPack)
            {
                return ImportSpecDecision.Accept();
            }

            var unexpected = localEpisode.Episodes.Where(e => releaseInfo.EpisodeIds.All(o => o != e.Id)).ToList();

            if (unexpected.Any() && _episodeService != null && releaseInfo.EpisodeIds.Any())
            {
                var grabbedEpisodes = _episodeService.GetEpisodes(releaseInfo.EpisodeIds) ?? new List<Episode>();
                var grabbedTitles = grabbedEpisodes
                    .Select(g => g.Title?.CleanSeriesTitle())
                    .Where(t => t.IsNotNullOrWhiteSpace())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var cleanReleaseTitle = releaseInfo.Title?.CleanSeriesTitle();

                unexpected = unexpected.Where(e =>
                {
                    if (e.SeriesId != 0 && grabbedEpisodes.Any(g => g.SeriesId == e.SeriesId))
                    {
                        var cleanTitle = e.Title?.CleanSeriesTitle();
                        if (cleanTitle != null)
                        {
                            if (grabbedTitles.Contains(cleanTitle))
                            {
                                return false;
                            }

                            if (cleanReleaseTitle != null && cleanReleaseTitle.Contains(cleanTitle, StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }).ToList();
            }

            if (unexpected.Any())
            {
                _logger.Debug("Unexpected game(s) in file: {0}", FormatGame(unexpected));

                if (unexpected.Count == 1)
                {
                    return ImportSpecDecision.Reject(ImportRejectionReason.EpisodeNotFoundInRelease, "Game '{0}' was not found in the grabbed release: {1}", FormatGame(unexpected), releaseInfo.Title);
                }

                return ImportSpecDecision.Reject(ImportRejectionReason.EpisodeNotFoundInRelease, "Games '{0}' were not found in the grabbed release: {1}", FormatGame(unexpected), releaseInfo.Title);
            }

            return ImportSpecDecision.Accept();
        }

        private string FormatGame(List<Episode> episodes)
        {
            return string.Join(", ", episodes.Select(e => string.IsNullOrWhiteSpace(e.Title) ? $"#{e.Id}" : e.Title));
        }
    }
}
