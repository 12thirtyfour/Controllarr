using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V3.Series
{
    [V3ApiController("series/import")]
    [Route("api/v3/platform/import")]
    [Route("api/v3/series/import")]
    public class PlatformImportController : Controller
    {
        private readonly IAddSeriesService _addSeriesService;

        public PlatformImportController(IAddSeriesService addSeriesService)
        {
            _addSeriesService = addSeriesService;
        }

        [HttpPost]
        public object Import([FromBody] List<SeriesResource> resource)
        {
            var newSeries = resource.ToModel();

            return _addSeriesService.AddSeries(newSeries).ToResource();
        }
    }
}
