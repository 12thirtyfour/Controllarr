using NzbDrone.Core.Tv;

namespace Sonarr.Api.V5.Series
{
    public class RegionTypeResource
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public List<int> SeasonNumbers { get; set; } = [];
        public int GameCount { get; set; }
    }

    public class SeasonTypeResource : RegionTypeResource
    {
    }

    public static class RegionTypeResourceMapper
    {
        public static SeasonTypeResource ToResource(this SeasonType model)
        {
            return new SeasonTypeResource
            {
                Name = model.Name,
                Type = model.Type,
                SeasonNumbers = model.SeasonNumbers,
                GameCount = model.EpisodeCount
            };
        }

        public static List<SeasonTypeResource> ToResource(this IEnumerable<SeasonType> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
