using NzbDrone.Core.Indexers;
using NzbDrone.Core.Profiles.Delay;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Profiles.Delay;

public class DelayProfileResource : RestResource
{
    public bool EnableUsenet { get; set; }
    public bool EnableTorrent { get; set; }
    public bool EnableHttp { get; set; }
    public DownloadProtocol PreferredProtocol { get; set; }
    public List<DownloadProtocol> ProtocolPriority { get; set; } = [];
    public int UsenetDelay { get; set; }
    public int TorrentDelay { get; set; }
    public int HttpDelay { get; set; }
    public bool BypassIfHighestQuality { get; set; }
    public bool BypassIfAboveCustomFormatScore { get; set; }
    public int MinimumCustomFormatScore { get; set; }
    public int Order { get; set; }
    public HashSet<int> Tags { get; set; } = [];
}

public static class DelayProfileResourceMapper
{
    public static DelayProfileResource ToResource(this DelayProfile model)
    {
        return new DelayProfileResource
        {
            Id = model.Id,
            EnableUsenet = model.EnableUsenet,
            EnableTorrent = model.EnableTorrent,
            EnableHttp = model.EnableHttp,
            PreferredProtocol = model.PreferredProtocol,
            ProtocolPriority = model.ProtocolPriority != null ? new List<DownloadProtocol>(model.ProtocolPriority) : [],
            UsenetDelay = model.UsenetDelay,
            TorrentDelay = model.TorrentDelay,
            HttpDelay = model.HttpDelay,
            BypassIfHighestQuality = model.BypassIfHighestQuality,
            BypassIfAboveCustomFormatScore = model.BypassIfAboveCustomFormatScore,
            MinimumCustomFormatScore = model.MinimumCustomFormatScore,
            Order = model.Order,
            Tags = new HashSet<int>(model.Tags)
        };
    }

    public static DelayProfile ToModel(this DelayProfileResource resource)
    {
        return new DelayProfile
        {
            Id = resource.Id,
            EnableUsenet = resource.EnableUsenet,
            EnableTorrent = resource.EnableTorrent,
            EnableHttp = resource.EnableHttp,
            PreferredProtocol = resource.PreferredProtocol,
            ProtocolPriority = resource.ProtocolPriority != null ? new List<DownloadProtocol>(resource.ProtocolPriority) : [],
            UsenetDelay = resource.UsenetDelay,
            TorrentDelay = resource.TorrentDelay,
            HttpDelay = resource.HttpDelay,
            BypassIfHighestQuality = resource.BypassIfHighestQuality,
            BypassIfAboveCustomFormatScore = resource.BypassIfAboveCustomFormatScore,
            MinimumCustomFormatScore = resource.MinimumCustomFormatScore,
            Order = resource.Order,
            Tags = new HashSet<int>(resource.Tags)
        };
    }

    public static List<DelayProfileResource> ToResource(this IEnumerable<DelayProfile> models)
    {
        return models.Select(m => m.ToResource()).ToList();
    }
}
