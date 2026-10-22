using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Games
{
    public class RomEntryRepository : BasicRepository<RomEntry>, IRomEntryRepository
    {
        public RomEntryRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<RomEntry> GetByPlatform(int platformId)
        {
            return Query(c => c.PlatformId == platformId);
        }

        public RomEntry GetByHash(string hash)
        {
            return Query(c => c.Hash == hash).FirstOrDefault();
        }
    }
}
