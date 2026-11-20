using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Dat.Events
{
    public class DatDatabaseUpdatedEvent : IEvent
    {
        public DatFile DatFile { get; }
        public int EntryCount { get; }

        public DatDatabaseUpdatedEvent(DatFile datFile, int entryCount)
        {
            DatFile = datFile;
            EntryCount = entryCount;
        }
    }
}
