using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ImportFolders;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V5.ImportFolders;

[V5ApiController]
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
    public Results<Created<ImportFolderResource>, NotFound> CreateImportFolder([FromBody] ImportFolderResource importFolderResource)
    {
        var model = importFolderResource.ToModel();

        return TypedCreated(_importFolderService.Add(model).Id);
    }

    [HttpGet]
    [Produces("application/json")]
    public Ok<List<ImportFolderResource>> GetImportFolders()
    {
        return TypedResults.Ok(_importFolderService.AllWithUnmappedFolders().ToResource());
    }

    [RestDeleteById]
    public void DeleteFolder(int id)
    {
        _importFolderService.Remove(id);
    }
}
