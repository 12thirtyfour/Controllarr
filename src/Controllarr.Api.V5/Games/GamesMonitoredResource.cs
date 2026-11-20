namespace Sonarr.Api.V5.Games;

public class GamesMonitoredResource
{
    public required List<int> GameIds { get; set; }
    public bool Monitored { get; set; }
}

public class EpisodesMonitoredResource : GamesMonitoredResource
{
}
