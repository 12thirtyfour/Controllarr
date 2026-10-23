using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games.Launcher;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("gamelauncher")]
    public class GameLauncherController : Controller
    {
        private readonly IGameLauncherService _gameLauncherService;

        public GameLauncherController(IGameLauncherService gameLauncherService)
        {
            _gameLauncherService = gameLauncherService;
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
    }
}
