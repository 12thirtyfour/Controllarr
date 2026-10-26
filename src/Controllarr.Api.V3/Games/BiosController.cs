using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Bios;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    public class BiosDownloadRequestResource
    {
        public string PlatformSlug { get; set; }
        public string FileName { get; set; }
    }

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

        [HttpPost("download")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public List<BiosFile> DownloadMissing([FromBody] BiosDownloadRequestResource request)
        {
            return _biosService.DownloadMissingBios(request?.PlatformSlug, request?.FileName);
        }
    }
}
