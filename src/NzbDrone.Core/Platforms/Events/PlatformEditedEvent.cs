using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Tv.Events
{
    public class PlatformEditedEvent : IEvent
    {
        public Series Series { get; private set; }
        public Series OldSeries { get; private set; }
        public bool EpisodesChanged { get; private set; }

        public PlatformEditedEvent(Series series, Series oldSeries, bool episodesChanged = false)
        {
            Series = series;
            OldSeries = oldSeries;
            EpisodesChanged = episodesChanged;
        }
    }
}
