using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Games;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Games.Model
{
    public class RomEntryResource : RestResource
    {
        public int PlatformId { get; set; }
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public string Region { get; set; }
        public string Language { get; set; }
        public string Revision { get; set; }
        public string Hash { get; set; }
    }

    public static class RomEntryResourceMapper
    {
        public static RomEntryResource ToResource(this RomEntry model)
        {
            if (model == null)
            {
                return null;
            }

            return new RomEntryResource
            {
                Id = model.Id,
                PlatformId = model.PlatformId,
                Title = model.Title,
                CleanTitle = model.CleanTitle,
                Region = model.Region,
                Language = model.Language,
                Revision = model.Revision,
                Hash = model.Hash
            };
        }

        public static RomEntry ToModel(this RomEntryResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new RomEntry
            {
                Id = resource.Id,
                PlatformId = resource.PlatformId,
                Title = resource.Title,
                CleanTitle = resource.CleanTitle,
                Region = resource.Region,
                Language = resource.Language,
                Revision = resource.Revision,
                Hash = resource.Hash
            };
        }

        public static List<RomEntryResource> ToResource(this IEnumerable<RomEntry> models)
        {
            return models?.Select(ToResource).ToList();
        }
    }
}
