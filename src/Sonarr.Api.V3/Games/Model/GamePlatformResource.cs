using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Games;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Games.Model
{
    public class GamePlatformResource : RestResource
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public int IgdbId { get; set; }
        public string Overview { get; set; }
        public string Path { get; set; }
        public bool Monitored { get; set; }
        public string RootFolderPath { get; set; }
        public CollectionStrategy CollectionStrategy { get; set; }
        public RevisionPreference RevisionPreference { get; set; }
        public string Code { get; set; }
        public int? ReleaseYear { get; set; }
    }

    public static class GamePlatformResourceMapper
    {
        public static GamePlatformResource ToResource(this GamePlatform model)
        {
            if (model == null)
            {
                return null;
            }

            return new GamePlatformResource
            {
                Id = model.Id,
                Name = model.Name,
                Slug = model.Slug,
                IgdbId = model.IgdbId,
                Overview = model.Overview,
                Path = model.Path,
                Monitored = model.Monitored,
                RootFolderPath = model.RootFolderPath,
                CollectionStrategy = model.CollectionStrategy,
                RevisionPreference = model.RevisionPreference,
                Code = model.Code,
                ReleaseYear = model.ReleaseYear
            };
        }

        public static GamePlatform ToModel(this GamePlatformResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new GamePlatform
            {
                Id = resource.Id,
                Name = resource.Name,
                Slug = resource.Slug,
                IgdbId = resource.IgdbId,
                Overview = resource.Overview,
                Path = resource.Path,
                Monitored = resource.Monitored,
                RootFolderPath = resource.RootFolderPath,
                CollectionStrategy = resource.CollectionStrategy,
                RevisionPreference = resource.RevisionPreference,
                Code = resource.Code,
                ReleaseYear = resource.ReleaseYear
            };
        }

        public static List<GamePlatformResource> ToResource(this IEnumerable<GamePlatform> models)
        {
            return models?.Select(ToResource).ToList();
        }
    }
}
