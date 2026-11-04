using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformUpdatedEvent : IEvent
    {
        public Series Series { get; private set; }

        public PlatformUpdatedEvent(Series series)
        {
            Series = series;
        }
    }
}
