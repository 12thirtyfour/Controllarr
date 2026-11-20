using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Statistics;

public class QualityStatistics
{
    public Quality Quality { get; set; }
    public int RomFileCount { get; set; }
    public long SizeOnDisk { get; set; }
}
