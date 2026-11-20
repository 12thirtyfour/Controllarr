using NzbDrone.Core.Statistics;

namespace Sonarr.Api.V5.Statistics;

public class QualityProfileStatisticsResource
{
    public int QualityProfileId { get; set; }
    public required string Name { get; set; }
    public int PlatformCount { get; set; }
    public int RomFileCount { get; set; }
    public long SizeOnDisk { get; set; }
}

public static class QualityProfileStatisticsResourceMapper
{
    public static QualityProfileStatisticsResource MapToResource(this QualityProfileStatistics model)
    {
        return new QualityProfileStatisticsResource
        {
            QualityProfileId = model.QualityProfileId,
            Name = model.Name,
            PlatformCount = model.PlatformCount,
            RomFileCount = model.RomFileCount,
            SizeOnDisk = model.SizeOnDisk
        };
    }
}
