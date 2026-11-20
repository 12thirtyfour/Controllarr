using NzbDrone.Common.Extensions;
using NzbDrone.Core.ImportFolders;
using NzbDrone.Core.RootFolders;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.ImportFolders;

public class ImportFolderResource : RestResource
{
    public string? Path { get; set; }
    public bool Accessible { get; set; }
    public bool IsEmpty { get; set; }
    public long? FreeSpace { get; set; }
    public long? TotalSpace { get; set; }
    public bool ContainsRomFiles { get; set; }

    public List<UnmappedFolder> UnmappedFolders { get; set; } = [];
}

public static class ImportFolderResourceMapper
{
    public static ImportFolderResource ToResource(this ImportFolder model)
    {
        return new ImportFolderResource
        {
            Id = model.Id,
            Path = model.Path.GetCleanPath(),
            Accessible = model.Accessible,
            IsEmpty = model.IsEmpty,
            FreeSpace = model.FreeSpace,
            TotalSpace = model.TotalSpace,
            ContainsRomFiles = model.ContainsRomFiles,
            UnmappedFolders = model.UnmappedFolders
        };
    }

    public static ImportFolder ToModel(this ImportFolderResource resource)
    {
        return new ImportFolder
        {
            Id = resource.Id,
            Path = resource.Path
        };
    }

    public static List<ImportFolderResource> ToResource(this IEnumerable<ImportFolder> models)
    {
        return models.Select(ToResource).ToList();
    }
}
