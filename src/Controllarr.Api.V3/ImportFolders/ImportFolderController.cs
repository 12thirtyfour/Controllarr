using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ImportFolders;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V3.ImportFolders
{
    [V3ApiController("importfolder")]
    [Route("api/v3/importfolder")]
    [Route("api/v3/importsource")]
    public class ImportFolderController : RestControllerWithSignalR<ImportFolderResource, ImportFolder>
    {
        private readonly IImportFolderService _importFolderService;

        public ImportFolderController(IImportFolderService importFolderService,
                                     IBroadcastSignalRMessage signalRBroadcaster,
                                     PathExistsValidator pathExistsValidator)
            : base(signalRBroadcaster)
        {
            _importFolderService = importFolderService;

            SharedValidator.RuleFor(c => c.Path)
                .Cascade(CascadeMode.Stop)
                .IsValidPath()
                .SetValidator(pathExistsValidator);
        }

        protected override ImportFolderResource GetResourceById(int id)
        {
            var timeout = Request?.GetBooleanQueryParameter("timeout", true) ?? true;

            return _importFolderService.Get(id, timeout).ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<ImportFolderResource> CreateImportFolder([FromBody] ImportFolderResource importFolderResource)
        {
            var model = importFolderResource.ToModel();

            return Created(_importFolderService.Add(model).Id);
        }

        [HttpGet]
        [Produces("application/json")]
        public List<ImportFolderResource> GetImportFolders()
        {
            return _importFolderService.AllWithUnmappedFolders().ToResource();
        }

        [RestDeleteById]
        public void DeleteFolder(int id)
        {
            _importFolderService.Remove(id);
        }
    }
}
