using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Bios;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("bios")]
    [V5ApiController("bios")]
    public class BiosController : Controller
    {
        private readonly IBiosService _biosService;

        public BiosController(IBiosService biosService)
        {
            _biosService = biosService;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<BiosFile> GetMatrix([FromQuery] string platformSlug = null, [FromQuery] string biosDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(biosDirectory))
            {
                return _biosService.ScanAndVerifyBiosDirectory(biosDirectory, platformSlug);
            }

            return _biosService.GetCatalog(platformSlug);
        }
    }
}
