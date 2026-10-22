using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Games
{
    public interface IRomEntryRepository : IBasicRepository<RomEntry>
    {
        List<RomEntry> GetByPlatform(int platformId);
        RomEntry GetByHash(string hash);
    }
}
