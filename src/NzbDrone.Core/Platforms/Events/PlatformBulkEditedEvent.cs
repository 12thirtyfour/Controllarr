using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformBulkEditedEvent : IEvent
    {
        public List<Series> Series { get; private set; }

        public PlatformBulkEditedEvent(List<Series> series)
        {
            Series = series;
        }
    }
}
