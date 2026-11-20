using System.Collections.Generic;

namespace NzbDrone.Core.Statistics;

public class LibraryStatistics
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
    public List<QualityProfileStatistics> QualityProfileStatistics { get; set; }
    public List<QualityStatistics> QualityStatistics { get; set; }
    public List<TagStatistics> TagStatistics { get; set; }
}
