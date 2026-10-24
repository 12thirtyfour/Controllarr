using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games.RetroAchievements;
using Sonarr.Http;

namespace Sonarr.Api.V3.Games
{
    public class RetroAchievementsCredentialsResource
    {
        public string Username { get; set; }
        public string WebApiKey { get; set; }
    }

    public class RetroAchievementsProgressRequestResource
    {
        public string Username { get; set; }
        public string GameTitle { get; set; }
        public string RomMd5Hash { get; set; }
        public List<RetroAchievementBadge> Badges { get; set; } = new List<RetroAchievementBadge>();
    }

    [V3ApiController("retroachievements")]
    [V5ApiController("retroachievements")]
    public class RetroAchievementsController : Controller
    {
        private readonly IRetroAchievementsService _retroAchievementsService;

        public RetroAchievementsController(IRetroAchievementsService retroAchievementsService)
        {
            _retroAchievementsService = retroAchievementsService;
        }

        [HttpPost("credentials")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<bool> ValidateCredentials([FromBody] RetroAchievementsCredentialsResource resource)
        {
            if (resource == null)
            {
                return BadRequest();
            }

            var isValid = _retroAchievementsService.ValidateCredentials(resource.Username, resource.WebApiKey);
            return Ok(isValid);
        }

        [HttpGet("library")]
        [Produces("application/json")]
        public ActionResult<List<RetroAchievementsGameProgress>> GetLibraryProgress(
            [FromQuery] string username = null,
            [FromQuery] string webApiKey = null)
        {
            var results = _retroAchievementsService.GetUserLibraryProgress(username, webApiKey);
            return Ok(results);
        }

        [HttpPost("progress")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<RetroAchievementsGameProgress> GetMasteryProgress([FromBody] RetroAchievementsProgressRequestResource request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            var progress = _retroAchievementsService.CalculateMasteryProgress(
                request.Username,
                request.GameTitle,
                request.RomMd5Hash,
                request.Badges);

            return Ok(progress);
        }
    }
}
