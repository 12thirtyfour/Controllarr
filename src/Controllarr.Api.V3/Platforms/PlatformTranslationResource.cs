using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv;

namespace Sonarr.Api.V3.Platforms
{
    public class PlatformTranslationResource
    {
        public Language Language { get; set; }
        public string Title { get; set; }
        public string Overview { get; set; }
    }

    public static class PlatformTranslationResourceMapper
    {
        public static PlatformTranslationResource ToResource(this PlatformTranslation model)
        {
            if (model == null)
            {
                return null;
            }

            return new PlatformTranslationResource
            {
                Language = model.Language,
                Title = model.Title,
                Overview = model.Overview
            };
        }

        public static PlatformTranslation ToModel(this PlatformTranslationResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new PlatformTranslation
            {
                Language = resource.Language,
                Title = resource.Title,
                Overview = resource.Overview
            };
        }

        public static List<PlatformTranslationResource> ToResource(this IEnumerable<PlatformTranslation> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static List<PlatformTranslation> ToModel(this IEnumerable<PlatformTranslationResource> resources)
        {
            return resources.Select(ToModel).ToList();
        }
    }
}
