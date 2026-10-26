using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv;

namespace Sonarr.Api.V5.Series
{
    public class PlatformTranslationResource
    {
        public Language? Language { get; set; }
        public string? Title { get; set; }
        public string? Overview { get; set; }
    }

    public class SeriesTranslationResource : PlatformTranslationResource
    {
    }

    public static class PlatformTranslationResourceMapper
    {
        public static SeriesTranslationResource ToResource(this SeriesTranslation model)
        {
            return new SeriesTranslationResource
            {
                Language = model.Language,
                Title = model.Title,
                Overview = model.Overview
            };
        }

        public static List<SeriesTranslationResource> ToResource(this IEnumerable<SeriesTranslation> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
