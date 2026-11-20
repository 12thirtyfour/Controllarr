using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformRefreshStartingEvent : IEvent
    {
        public bool ManualTrigger { get; set; }

        public PlatformRefreshStartingEvent(bool manualTrigger)
        {
            ManualTrigger = manualTrigger;
        }
    }
}
