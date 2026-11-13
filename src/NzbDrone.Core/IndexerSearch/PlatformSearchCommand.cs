using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.IndexerSearch
{
    public class PlatformSearchCommand : Command
    {
        public int SeriesId { get; set; }
        public bool SearchCollectionFirst { get; set; }

        public override bool SendUpdatesToClient => true;

        public PlatformSearchCommand()
        {
        }

        public PlatformSearchCommand(int seriesId)
        {
            SeriesId = seriesId;
        }
    }
}
