using NzbDrone.Core.Download;

namespace Sonarr.Api.V5.Queue;

public class QueueReorderResource
{
    public List<int> Ids { get; set; } = [];
    public QueueReorderAction Action { get; set; }
}
