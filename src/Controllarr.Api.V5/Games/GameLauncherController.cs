using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games.Launcher;
using Sonarr.Http;

namespace Sonarr.Api.V5.Games
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
        public ActionResult<LibretroLauncherConfig> SaveConfig([FromBody] LibretroLauncherConfig? config)
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
            [FromQuery] string romPath = "",
            [FromQuery] int romFileId = 0)
        {
            var descriptor = _gameLauncherService.BuildLaunchDescriptor(gameId, platformFolder, romPath, romFileId);
            return Ok(descriptor);
        }

        [HttpGet("{gameId:int}/rom")]
        [HttpGet("{gameId:int}/rom/{fileName}")]
        public IActionResult StreamRomFile(
            [FromRoute] int gameId,
            [FromRoute] string? fileName = null,
            [FromQuery] string platformFolder = "",
            [FromQuery] string romPath = "",
            [FromQuery] int romFileId = 0)
        {
            var stream = _gameLauncherService.OpenRomStream(gameId, out var resolvedFileName, platformFolder, romPath, romFileId);
            if (stream == null)
            {
                return NotFound(new { message = "ROM file not found on disk." });
            }

            var downloadFileName = !string.IsNullOrWhiteSpace(resolvedFileName)
                ? resolvedFileName
                : (fileName ?? "game.rom");

            return File(stream, "application/octet-stream", downloadFileName, enableRangeProcessing: true);
        }

        [HttpGet("{gameId:int}/bios")]
        [HttpGet("{gameId:int}/bios/{fileName}")]
        public IActionResult StreamBiosFile(
            [FromRoute] int gameId,
            [FromRoute] string? fileName = null,
            [FromQuery] string platformFolder = "",
            [FromQuery] string romPath = "",
            [FromQuery] int romFileId = 0)
        {
            var stream = _gameLauncherService.OpenBiosStream(gameId, out var resolvedFileName, platformFolder, romPath, romFileId);
            if (stream == null)
            {
                return NotFound(new { message = "BIOS file not found on disk." });
            }

            var downloadFileName = !string.IsNullOrWhiteSpace(resolvedFileName)
                ? resolvedFileName
                : (fileName ?? "bios.bin");

            return File(stream, "application/octet-stream", downloadFileName, enableRangeProcessing: true);
        }

        [HttpPost("{gameId:int}/launch")]
        [Produces("application/json")]
        public ActionResult<DesktopLaunchResult> LaunchDesktopGame(
            [FromRoute] int gameId,
            [FromQuery] string platformFolder = "",
            [FromQuery] string romPath = "",
            [FromQuery] int romFileId = 0)
        {
            var result = _gameLauncherService.LaunchDesktopGame(gameId, platformFolder, romPath, romFileId);
            return Ok(result);
        }
    }
}
