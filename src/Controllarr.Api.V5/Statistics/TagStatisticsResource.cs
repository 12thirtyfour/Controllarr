using NzbDrone.Core.Statistics;

namespace Sonarr.Api.V5.Statistics;

public class TagStatisticsResource
{
    public int TagId { get; set; }
    public required string Label { get; set; }
    public int PlatformCount { get; set; }
    public int RomFileCount { get; set; }
    public long SizeOnDisk { get; set; }
}

public static class TagStatisticsResourceMapper
{
    public static TagStatisticsResource MapToResource(this TagStatistics model)
    {
        return new TagStatisticsResource
        {
            TagId = model.TagId,
            Label = model.Label,
            PlatformCount = model.PlatformCount,
            RomFileCount = model.RomFileCount,
            SizeOnDisk = model.SizeOnDisk
        };
    }
}
