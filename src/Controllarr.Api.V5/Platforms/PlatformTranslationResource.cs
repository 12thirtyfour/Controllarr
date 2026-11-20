using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv;

namespace Sonarr.Api.V5.Platforms
{
    public class PlatformTranslationResource
    {
        public Language? Language { get; set; }
        public string? Title { get; set; }
        public string? Overview { get; set; }
    }

    public static class PlatformTranslationResourceMapper
    {
        public static PlatformTranslationResource ToResource(this PlatformTranslation model)
        {
            return new PlatformTranslationResource
            {
                Language = model.Language,
                Title = model.Title,
                Overview = model.Overview
            };
        }

        public static List<PlatformTranslationResource> ToResource(this IEnumerable<PlatformTranslation> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
