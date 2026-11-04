using System.Collections.Generic;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class PlatformRenamedEvent : IEvent
    {
        public Series Series { get; private set; }
        public List<RenamedEpisodeFile> RenamedFiles { get; private set; }

        public PlatformRenamedEvent(Series series, List<RenamedEpisodeFile> renamedFiles)
        {
            Series = series;
            RenamedFiles = renamedFiles;
        }
    }
}
