using System.Collections.Generic;

namespace Sonarr.Api.V3.Episodes
{
    public class GamesMonitoredResource
    {
        public List<int> EpisodeIds { get; set; }
        public bool Monitored { get; set; }
    }

    public class EpisodesMonitoredResource : GamesMonitoredResource
    {
    }
}
