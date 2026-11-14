using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MediaFiles.Patching;
using Sonarr.Http;

namespace Sonarr.Api.V5.Games
{
    [V3ApiController("romhack")]
    [V5ApiController("romhack")]
    public class RomHackController : Controller
    {
        private readonly IRomHackAutomationService _romHackAutomationService;

        public RomHackController(IRomHackAutomationService romHackAutomationService)
        {
            _romHackAutomationService = romHackAutomationService;
        }

        [HttpGet("search")]
        [Produces("application/json")]
        public ActionResult<List<RomHackSearchResult>> Search([FromQuery] string query, [FromQuery] string? platformSlug = null)
        {
            return Ok(_romHackAutomationService.SearchCommunitySources(query, platformSlug));
        }

        [HttpPost("acquire")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<RomHackAcquireResult> Acquire([FromBody] RomHackAcquireRequest? request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            return Ok(_romHackAutomationService.AcquireAndCreate(request));
        }
    }
}
