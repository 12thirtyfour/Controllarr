using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("gamesearch")]
    [V5ApiController("gamesearch")]
    public class GameSearchController : Controller
    {
        private readonly ISmartAddService _smartAddService;

        public GameSearchController(ISmartAddService smartAddService)
        {
            _smartAddService = smartAddService;
        }

        [HttpGet]
        [Produces("application/json")]
        public List<GameTitle> Search(
            [FromQuery] string term,
            [FromQuery] bool allowUnofficial = false,
            [FromQuery] string expectedHash = null)
        {
            return _smartAddService.SearchGlobalGames(term, allowUnofficial, expectedHash);
        }

        [HttpPost("smartadd")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<SmartAddResult> SmartAdd([FromBody] SmartAddRequest request)
        {
            var result = _smartAddService.SmartAddGame(request);
            if (!result.Accepted)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}
