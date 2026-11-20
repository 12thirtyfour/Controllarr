using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.Platforms
{
    [V5ApiController("series/import")]
    [Route("api/v5/platform/import")]
    [Route("api/v5/series/import")]
    public class PlatformImportController : Controller
    {
        private readonly IAddPlatformService _addSeriesService;

        public PlatformImportController(IAddPlatformService addSeriesService)
        {
            _addSeriesService = addSeriesService;
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public Ok<List<SeriesResource>> Import([FromBody] List<SeriesResource> resource)
        {
            var newSeries = resource.ToModel();

            return TypedResults.Ok(_addSeriesService.AddSeries(newSeries).ToResource());
        }
    }
}
