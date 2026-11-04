using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformAddedEvent : IEvent
    {
        public Series Series { get; private set; }

        public PlatformAddedEvent(Series series)
        {
            Series = series;
        }
    }
}
