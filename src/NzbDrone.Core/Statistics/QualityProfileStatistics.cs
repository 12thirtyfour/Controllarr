namespace NzbDrone.Core.Statistics;

public class QualityProfileStatistics
{
    public int QualityProfileId { get; set; }
    public string Name { get; set; }
    public int PlatformCount { get; set; }
    public int RomFileCount { get; set; }
    public long SizeOnDisk { get; set; }
}
