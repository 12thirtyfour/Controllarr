namespace Sonarr.Api.V5.Episodes;

public class GamesMonitoredResource
{
    public required List<int> EpisodeIds { get; set; }
    public bool Monitored { get; set; }
}

public class EpisodesMonitoredResource : GamesMonitoredResource
{
}
