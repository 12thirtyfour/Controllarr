using System.Collections.Generic;
using NzbDrone.Core.Tv;

namespace Sonarr.Api.V3.SeasonPass
{
    public class PlatformPassResource
    {
        public List<SeasonPassSeriesResource> Series { get; set; }
        public MonitoringOptions MonitoringOptions { get; set; }
    }

    public class SeasonPassResource : PlatformPassResource
    {
    }
}
