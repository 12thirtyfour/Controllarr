using System.Collections.Generic;
using Sonarr.Api.V3.Platforms;

namespace Sonarr.Api.V3.PlatformPass
{
    public class PlatformPassPlatformResource
    {
        public int Id { get; set; }
        public bool? Monitored { get; set; }
        public List<SeasonResource> Seasons { get; set; }
    }

    public class SeasonPassSeriesResource : PlatformPassPlatformResource
    {
    }
}
