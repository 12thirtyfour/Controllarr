using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.SeriesStats;

namespace Sonarr.Api.V5.Series;

public class RegionStatisticsResource
{
    public DateTime? NextAiring { get; set; }
    public DateTime? PreviousAiring { get; set; }
    public int RomFileCount { get; set; }
    public int GameCount { get; set; }
    public int TotalGameCount { get; set; }
    public int MonitoredGameCount { get; set; }
    public long SizeOnDisk { get; set; }
    public List<string>? ReleaseGroups { get; set; }
    public List<ReleaseType>? ReleaseTypes { get; set; }
    public List<Quality>? RomFileQualities { get; set; }

    public decimal PercentOfGames
    {
        get
        {
            if (GameCount == 0)
            {
                return 0;
            }

            return (decimal)RomFileCount / (decimal)GameCount * 100;
        }
    }
}

public class SeasonStatisticsResource : RegionStatisticsResource
{
}

public static class RegionStatisticsResourceMapper
{
    public static SeasonStatisticsResource ToResource(this SeasonStatistics model)
    {
        return new SeasonStatisticsResource
        {
            NextAiring = model.NextAiring,
            PreviousAiring = model.PreviousAiring,
            RomFileCount = model.EpisodeFileCount,
            GameCount = model.EpisodeCount,
            TotalGameCount = model.TotalEpisodeCount,
            MonitoredGameCount = model.MonitoredEpisodeCount,
            SizeOnDisk = model.SizeOnDisk,
            ReleaseGroups = model.ReleaseGroups,
            ReleaseTypes = model.ReleaseTypes,
            RomFileQualities = model.EpisodeFileQualities
        };
    }
}
