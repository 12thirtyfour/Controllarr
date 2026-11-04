using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class RomFileDeletedEvent : IEvent
    {
        public EpisodeFile EpisodeFile { get; private set; }
        public DeleteMediaFileReason Reason { get; private set; }

        public RomFileDeletedEvent(EpisodeFile episodeFile, DeleteMediaFileReason reason)
        {
            EpisodeFile = episodeFile;
            Reason = reason;
        }
    }
}
