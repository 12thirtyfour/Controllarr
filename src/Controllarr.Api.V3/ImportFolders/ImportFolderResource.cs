using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.ImportFolders;
using NzbDrone.Core.RootFolders;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.ImportFolders
{
    public class ImportFolderResource : RestResource
    {
        public string Path { get; set; }
        public bool Accessible { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }
        public bool ContainsRomFiles { get; set; }

        public List<UnmappedFolder> UnmappedFolders { get; set; }
    }

    public static class ImportFolderResourceMapper
    {
        public static ImportFolderResource ToResource(this ImportFolder model)
        {
            if (model == null)
            {
                return null;
            }

            return new ImportFolderResource
            {
                Id = model.Id,
                Path = model.Path.GetCleanPath(),
                Accessible = model.Accessible,
                FreeSpace = model.FreeSpace,
                TotalSpace = model.TotalSpace,
                ContainsRomFiles = model.ContainsRomFiles,
                UnmappedFolders = model.UnmappedFolders
            };
        }

        public static ImportFolder ToModel(this ImportFolderResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new ImportFolder
            {
                Id = resource.Id,
                Path = resource.Path,
                Accessible = resource.Accessible,
                FreeSpace = resource.FreeSpace,
                TotalSpace = resource.TotalSpace,
                ContainsRomFiles = resource.ContainsRomFiles,
                UnmappedFolders = resource.UnmappedFolders
            };
        }

        public static List<ImportFolderResource> ToResource(this IEnumerable<ImportFolder> models)
        {
            if (models == null)
            {
                return new List<ImportFolderResource>();
            }

            var result = new List<ImportFolderResource>();
            foreach (var model in models)
            {
                result.Add(model.ToResource());
            }

            return result;
        }
    }
}
