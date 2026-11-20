using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformAddCompletedEvent : IEvent
    {
        public Series Series { get; private set; }

        public PlatformAddCompletedEvent(Series series)
        {
            Series = series;
        }
    }
}
