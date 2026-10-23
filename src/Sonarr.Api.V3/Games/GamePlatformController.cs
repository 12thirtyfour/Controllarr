using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Games;
using Sonarr.Api.V3.Games.Model;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V3.Games
{
    [V3ApiController("gameplatform")]
    [V5ApiController("gameplatform")]
    public class GamePlatformController : RestController<GamePlatformResource>
    {
        private readonly IGamePlatformRepository _gamePlatformRepository;

        public GamePlatformController(IGamePlatformRepository gamePlatformRepository)
        {
            _gamePlatformRepository = gamePlatformRepository;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();
        }

        protected override GamePlatformResource GetResourceById(int id)
        {
            return _gamePlatformRepository.Get(id).ToResource();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<GamePlatformResource> GetAll()
        {
            return _gamePlatformRepository.All().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<GamePlatformResource> Create([FromBody] GamePlatformResource resource)
        {
            var model = resource.ToModel();
            var inserted = _gamePlatformRepository.Insert(model);
            return Created(inserted.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<GamePlatformResource> Update([FromBody] GamePlatformResource resource)
        {
            var model = resource.ToModel();
            _gamePlatformRepository.Update(model);
            return Accepted(resource.Id);
        }

        [RestDeleteById]
        public void DeletePlatform(int id)
        {
            _gamePlatformRepository.Delete(id);
        }
    }
}
