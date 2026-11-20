using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Commands
{
    public class RescanPlatformCommand : Command
    {
        public int? SeriesId { get; set; }

        public override bool SendUpdatesToClient => true;

        public RescanPlatformCommand()
        {
        }

        public RescanPlatformCommand(int seriesId)
        {
            SeriesId = seriesId;
        }
    }
}
