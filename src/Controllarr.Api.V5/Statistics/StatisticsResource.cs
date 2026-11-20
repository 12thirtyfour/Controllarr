using NzbDrone.Core.Statistics;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Statistics;

public class StatisticsResource : RestResource
{
    public int PlatformCount { get; set; }
    public int MonitoredPlatformCount { get; set; }
    public int CompletedPlatformCount { get; set; }
    public int ContinuingPlatformCount { get; set; }
    public int EndedPlatformCount { get; set; }
    public int UpcomingPlatformCount { get; set; }
    public int DeletedPlatformCount { get; set; }
    public int StandardPlatformCount { get; set; }
    public int DailyPlatformCount { get; set; }
    public int AnimePlatformCount { get; set; }
    public int RegionCount { get; set; }
    public int CompletedRegionCount { get; set; }
    public int TotalGameCount { get; set; }
    public int MonitoredGameCount { get; set; }
    public int DownloadedGameCount { get; set; }
    public int MissingGameCount { get; set; }
    public int UnreleasedGameCount { get; set; }
    public int RomFileCount { get; set; }
    public long SizeOnDisk { get; set; }
    public required List<QualityProfileStatisticsResource> QualityProfileStatistics { get; set; }
    public required List<QualityStatisticsResource> QualityStatistics { get; set; }
    public required List<TagStatisticsResource> TagStatistics { get; set; }
}

public static class StatisticsResourceMapper
{
    public static StatisticsResource MapToResource(this LibraryStatistics model)
    {
        return new StatisticsResource
        {
            PlatformCount = model.PlatformCount,
            MonitoredPlatformCount = model.MonitoredPlatformCount,
            CompletedPlatformCount = model.CompletedPlatformCount,
            ContinuingPlatformCount = model.ContinuingPlatformCount,
            EndedPlatformCount = model.EndedPlatformCount,
            UpcomingPlatformCount = model.UpcomingPlatformCount,
            DeletedPlatformCount = model.DeletedPlatformCount,
            StandardPlatformCount = model.StandardPlatformCount,
            DailyPlatformCount = model.DailyPlatformCount,
            AnimePlatformCount = model.AnimePlatformCount,
            RegionCount = model.RegionCount,
            CompletedRegionCount = model.CompletedRegionCount,
            TotalGameCount = model.TotalGameCount,
            MonitoredGameCount = model.MonitoredGameCount,
            DownloadedGameCount = model.DownloadedGameCount,
            MissingGameCount = model.MissingGameCount,
            UnreleasedGameCount = model.UnreleasedGameCount,
            RomFileCount = model.RomFileCount,
            SizeOnDisk = model.SizeOnDisk,
            QualityProfileStatistics = model.QualityProfileStatistics.ConvertAll(QualityProfileStatisticsResourceMapper.MapToResource),
            QualityStatistics = model.QualityStatistics.ConvertAll(QualityStatisticsResourceMapper.MapToResource),
            TagStatistics = model.TagStatistics.ConvertAll(TagStatisticsResourceMapper.MapToResource)
        };
    }
}
