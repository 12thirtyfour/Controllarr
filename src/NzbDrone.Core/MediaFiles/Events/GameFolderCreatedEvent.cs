using NzbDrone.Common.Messaging;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class GameFolderCreatedEvent : IEvent
    {
        public Series Series { get; private set; }
        public EpisodeFile EpisodeFile { get; private set; }
        public string SeriesFolder { get; set; }
        public string SeasonFolder { get; set; }
        public string EpisodeFolder { get; set; }

        public GameFolderCreatedEvent(Series series, EpisodeFile episodeFile)
        {
            Series = series;
            EpisodeFile = episodeFile;
        }
    }
}
