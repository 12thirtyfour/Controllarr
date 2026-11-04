using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformImportedEvent : IEvent
    {
        public List<int> SeriesIds { get; private set; }

        public PlatformImportedEvent(List<int> seriesIds)
        {
            SeriesIds = seriesIds;
        }
    }
}
