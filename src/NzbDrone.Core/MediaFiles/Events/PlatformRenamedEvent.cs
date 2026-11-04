using System.Collections.Generic;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class PlatformRenamedEvent : IEvent
    {
        public Series Series { get; private set; }
        public List<RenamedRomFile> RenamedFiles { get; private set; }

        public PlatformRenamedEvent(Series series, List<RenamedRomFile> renamedFiles)
        {
            Series = series;
            RenamedFiles = renamedFiles;
        }
    }
}
