using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.Aggregation.Aggregators
{
    public interface IAggregateRemoteGame
    {
        RemoteEpisode Aggregate(RemoteEpisode remoteEpisode);
    }
}
