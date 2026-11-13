using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController]
    [Route("api/v3/game")]
    [Route("api/v3/episode")]
    public class GameController : GameControllerWithSignalR
    {
        public GameController(ISeriesService seriesService,
                             IEpisodeService episodeService,
                             IUpgradableSpecification upgradableSpecification,
                             ICustomFormatCalculationService formatCalculator,
                             IBroadcastSignalRMessage signalRBroadcaster)
            : base(episodeService, seriesService, upgradableSpecification, formatCalculator, signalRBroadcaster)
        {
        }

        [HttpGet]
        [Produces("application/json")]
        public List<EpisodeResource> GetEpisodes(int? seriesId, int? seasonNumber, [FromQuery]List<int> episodeIds, int? episodeFileId, bool includeSeries = false, bool includeEpisodeFile = false, bool includeImages = false)
        {
            if (seriesId.HasValue)
            {
                if (seasonNumber.HasValue)
                {
                    return MapToResource(_episodeService.GetEpisodesBySeason(seriesId.Value, seasonNumber.Value), includeSeries, includeEpisodeFile, includeImages);
                }

                return MapToResource(_episodeService.GetEpisodeBySeries(seriesId.Value), includeSeries, includeEpisodeFile, includeImages);
            }
            else if (episodeIds.Any())
            {
                return MapToResource(_episodeService.GetEpisodes(episodeIds), includeSeries, includeEpisodeFile, includeImages);
            }
            else if (episodeFileId.HasValue)
            {
                return MapToResource(_episodeService.GetEpisodesByFileId(episodeFileId.Value), includeSeries, includeEpisodeFile, includeImages);
            }

            throw new BadRequestException("seriesId or episodeIds must be provided");
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<EpisodeResource> SetEpisodeMonitored([FromRoute] int id, [FromBody] EpisodeResource resource)
        {
            var monitored = resource.Monitored;
            _episodeService.SetEpisodeMonitored(id, monitored);

            var episode = _episodeService.GetEpisode(id);
            if (monitored && episode != null)
            {
                _seriesService.EnsureMonitoredForGames(new[] { episode });
            }

            resource = MapToResource(episode, false, false, false);

            return Accepted(resource);
        }

        [HttpPut("monitor")]
        [Consumes("application/json")]
        public IActionResult SetEpisodesMonitored([FromBody] EpisodesMonitoredResource resource, [FromQuery] bool includeImages = false)
        {
            if (resource.EpisodeIds.Count == 1)
            {
                _episodeService.SetEpisodeMonitored(resource.EpisodeIds.First(), resource.Monitored);
            }
            else
            {
                _episodeService.SetMonitored(resource.EpisodeIds, resource.Monitored);
            }

            var episodes = _episodeService.GetEpisodes(resource.EpisodeIds);
            if (resource.Monitored && episodes != null)
            {
                _seriesService.EnsureMonitoredForGames(episodes);
            }

            var resources = MapToResource(episodes, false, false, includeImages);

            return Accepted(resources);
        }
    }
}
