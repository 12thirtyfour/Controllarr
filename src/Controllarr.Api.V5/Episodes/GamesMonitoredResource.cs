namespace Sonarr.Api.V5.Episodes;

public class GamesMonitoredResource
{
    public required List<int> GameIds { get; set; }
    public bool Monitored { get; set; }
}

public class EpisodesMonitoredResource : GamesMonitoredResource
{
}
