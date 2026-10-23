using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MediaFiles.Export;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    public class SdCardExportRequestResource
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string TargetMountPath { get; set; }
        public bool Overwrite { get; set; }
        public List<SdCardExportItem> Items { get; set; } = new List<SdCardExportItem>();
    }

    [V3ApiController("sdcardexport")]
    public class SdCardExportController : Controller
    {
        private readonly ISdCardExporterService _sdCardExporterService;

        public SdCardExportController(ISdCardExporterService sdCardExporterService)
        {
            _sdCardExporterService = sdCardExporterService;
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<SdCardExportResult> Export([FromBody] SdCardExportRequestResource request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetMountPath))
            {
                return BadRequest("TargetMountPath is required.");
            }

            var result = _sdCardExporterService.ExportToSdCard(
                request.Profile,
                request.TargetMountPath,
                request.Items,
                request.Overwrite);

            return Ok(result);
        }
    }
}
