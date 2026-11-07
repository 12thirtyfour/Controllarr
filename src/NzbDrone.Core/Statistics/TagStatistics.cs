namespace NzbDrone.Core.Statistics;

public class TagStatistics
{
    public int TagId { get; set; }
    public string Label { get; set; }
    public int PlatformCount { get; set; }
    public int RomFileCount { get; set; }
    public long SizeOnDisk { get; set; }
}
