using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games.VirtualLibrary;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("virtuallibrary")]
    [V5ApiController("virtuallibrary")]
    public class VirtualLibraryController : Controller
    {
        private readonly IVirtualLibraryService _virtualLibraryService;

        public VirtualLibraryController(IVirtualLibraryService virtualLibraryService)
        {
            _virtualLibraryService = virtualLibraryService;
        }

        [HttpGet]
        [Produces("application/json")]
        public ActionResult<List<VirtualLibraryProfile>> GetAll()
        {
            return Ok(_virtualLibraryService.GetAll());
        }

        [HttpGet("presets")]
        [Produces("application/json")]
        public ActionResult<List<VirtualLibraryPresetInfo>> GetPresets()
        {
            return Ok(_virtualLibraryService.GetPresets());
        }

        [HttpPost]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<VirtualLibraryProfile> Save([FromBody] VirtualLibraryProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Name) || string.IsNullOrWhiteSpace(profile.TargetPath))
            {
                return BadRequest("Name and TargetPath are required.");
            }

            return Ok(_virtualLibraryService.Save(profile));
        }

        [HttpPost("{id:int}/sync")]
        [Produces("application/json")]
        public ActionResult<VirtualLibraryProfile> Sync(int id)
        {
            var synced = _virtualLibraryService.SyncVirtualLibrary(id);
            if (synced == null)
            {
                return NotFound();
            }

            return Ok(synced);
        }

        [HttpDelete("{id:int}")]
        public ActionResult Delete(int id)
        {
            _virtualLibraryService.Delete(id);
            return Ok();
        }
    }
}
