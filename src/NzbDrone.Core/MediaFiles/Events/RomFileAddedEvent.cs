using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class RomFileAddedEvent : IEvent
    {
        public EpisodeFile EpisodeFile { get; private set; }

        public RomFileAddedEvent(EpisodeFile episodeFile)
        {
            EpisodeFile = episodeFile;
        }
    }
}
