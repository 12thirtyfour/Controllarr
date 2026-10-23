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
    [V3ApiController("romentry")]
    [V5ApiController("romentry")]
    public class RomEntryController : RestController<RomEntryResource>
    {
        private readonly IRomEntryRepository _romEntryRepository;

        public RomEntryController(IRomEntryRepository romEntryRepository)
        {
            _romEntryRepository = romEntryRepository;

            SharedValidator.RuleFor(c => c.Title).NotEmpty();
            SharedValidator.RuleFor(c => c.PlatformId).GreaterThan(0);
        }

        protected override RomEntryResource GetResourceById(int id)
        {
            return _romEntryRepository.Get(id).ToResource();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<RomEntryResource> GetRomEntries([FromQuery] int? platformId)
        {
            if (platformId.HasValue)
            {
                return _romEntryRepository.GetByPlatform(platformId.Value).ToResource();
            }

            return _romEntryRepository.All().ToResource();
        }

        [HttpGet("hash/{hash}")]
        [Produces("application/json")]
        public ActionResult<RomEntryResource> GetByHash([FromRoute] string hash)
        {
            var rom = _romEntryRepository.GetByHash(hash);
            if (rom == null)
            {
                return NotFound();
            }

            return Ok(rom.ToResource());
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<RomEntryResource> Create([FromBody] RomEntryResource resource)
        {
            var model = resource.ToModel();
            var inserted = _romEntryRepository.Insert(model);
            return Created(inserted.Id);
        }

        [RestDeleteById]
        public void DeleteRomEntry(int id)
        {
            _romEntryRepository.Delete(id);
        }
    }
}
