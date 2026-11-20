using Sonarr.Api.V5.Platforms;

namespace Sonarr.Api.V5.PlatformPass;

public class PlatformPassPlatformResource
{
    public int Id { get; set; }
    public bool? Monitored { get; set; }
    public List<SeasonResource> Seasons { get; set; } = [];
}

public class SeasonPassSeriesResource : PlatformPassPlatformResource
{
}
