using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class PlatformCollectionSearchCommand : Command
    {
        public int SeriesId { get; set; }

        public override bool SendUpdatesToClient => true;
    }
}
