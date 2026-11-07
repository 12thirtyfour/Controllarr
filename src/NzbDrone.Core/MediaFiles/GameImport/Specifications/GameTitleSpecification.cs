using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.GameImport.Specifications
{
    public class GameTitleSpecification : IImportDecisionEngineSpecification
    {
        private readonly IConfigService _configService;
        private readonly IBuildFileNames _buildFileNames;
        private readonly IEpisodeService _episodeService;
        private readonly Logger _logger;

        public GameTitleSpecification(IConfigService configService,
                                         IBuildFileNames buildFileNames,
                                         IEpisodeService episodeService,
                                         Logger logger)
        {
            _configService = configService;
            _buildFileNames = buildFileNames;
            _episodeService = episodeService;
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.ExistingFile)
            {
                _logger.Debug("{0} is in series folder, skipping check", localEpisode.Path);
                return ImportSpecDecision.Accept();
            }

            var episodeTitleRequired = _configService.GameTitleRequired;

            if (episodeTitleRequired == GameTitleRequiredType.Never)
            {
                _logger.Debug("Game titles are never required, skipping check");
                return ImportSpecDecision.Accept();
            }

            if (!_buildFileNames.RequiresEpisodeTitle(localEpisode.Series, localEpisode.Episodes))
            {
                _logger.Debug("File name format does not require game title, skipping check");
                return ImportSpecDecision.Accept();
            }

            var episodes = localEpisode.Episodes;
            var firstEpisode = episodes.First();
            var episodesInSeason = _episodeService.GetEpisodesBySeason(firstEpisode.SeriesId, firstEpisode.SeasonNumber);
            var allEpisodesOnTheSameDay = firstEpisode.AirDateUtc.HasValue && episodes.All(e =>
                                              !e.AirDateUtc.HasValue ||
                                              e.AirDateUtc.Value == firstEpisode.AirDateUtc.Value);

            if (episodeTitleRequired == GameTitleRequiredType.BulkSeasonReleases &&
                allEpisodesOnTheSameDay &&
                episodesInSeason.Count(e => !e.AirDateUtc.HasValue ||
                                            e.AirDateUtc.Value == firstEpisode.AirDateUtc.Value) < 4)
            {
                _logger.Debug("Game title only required for bulk collection releases");
                return ImportSpecDecision.Accept();
            }

            foreach (var episode in episodes)
            {
                var airDateUtc = episode.AirDateUtc;
                var title = episode.Title;

                if (airDateUtc.HasValue && airDateUtc.Value.Before(DateTime.UtcNow.AddHours(-48)))
                {
                    _logger.Debug("Game released more than 48 hours ago");
                    continue;
                }

                if (title.IsNullOrWhiteSpace())
                {
                    _logger.Debug("Game does not have a title");

                    return ImportSpecDecision.Reject(ImportRejectionReason.TitleMissing, "Game does not have a title");
                }

                if (title.Equals("TBA"))
                {
                    _logger.Debug("Game has a TBA title");

                    return ImportSpecDecision.Reject(ImportRejectionReason.TitleTba, "Game has a TBA title");
                }
            }

            return ImportSpecDecision.Accept();
        }
    }
}
