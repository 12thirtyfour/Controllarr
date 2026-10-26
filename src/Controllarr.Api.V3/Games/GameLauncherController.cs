using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games.Launcher;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("gamelauncher")]
    [V5ApiController("gamelauncher")]
    public class GameLauncherController : Controller
    {
        private readonly IGameLauncherService _gameLauncherService;

        public GameLauncherController(IGameLauncherService gameLauncherService)
        {
            _gameLauncherService = gameLauncherService;
        }

        [HttpGet("config")]
        [Produces("application/json")]
        public ActionResult<LibretroLauncherConfig> GetConfig()
        {
            return Ok(_gameLauncherService.GetConfig());
        }

        [HttpPost("config")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<LibretroLauncherConfig> SaveConfig([FromBody] LibretroLauncherConfig config)
        {
            if (config == null)
            {
                return BadRequest("Configuration payload is required.");
            }

            return Ok(_gameLauncherService.SaveConfig(config));
        }

        [HttpGet("cores")]
        [Produces("application/json")]
        public ActionResult<List<LibretroPlatformCoreOption>> GetPlatformCoreOptions()
        {
            return Ok(_gameLauncherService.GetPlatformCoreOptions());
        }

        [HttpGet("{gameId:int}")]
        [Produces("application/json")]
        public ActionResult<GameLaunchDescriptor> GetLaunchDescriptor(
            [FromRoute] int gameId,
            [FromQuery] string platformFolder = "",
            [FromQuery] string romPath = "")
        {
            var descriptor = _gameLauncherService.BuildLaunchDescriptor(gameId, platformFolder, romPath);
            return Ok(descriptor);
        }

        [HttpGet("{gameId:int}/rom")]
        public IActionResult StreamRomFile([FromRoute] int gameId)
        {
            var stream = _gameLauncherService.OpenRomStream(gameId, out var fileName);
            if (stream == null)
            {
                return NotFound(new { message = "ROM file not found on disk." });
            }

            return File(stream, "application/octet-stream", fileName, enableRangeProcessing: true);
        }

        [HttpPost("{gameId:int}/launch")]
        [Produces("application/json")]
        public ActionResult<DesktopLaunchResult> LaunchDesktopGame(
            [FromRoute] int gameId,
            [FromQuery] string platformFolder = "",
            [FromQuery] string romPath = "")
        {
            var result = _gameLauncherService.LaunchDesktopGame(gameId, platformFolder, romPath);
            return Ok(result);
        }
    }
}
