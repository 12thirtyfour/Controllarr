using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Games
{
    public class GamePlatformRepository : BasicRepository<GamePlatform>, IGamePlatformRepository
    {
        public GamePlatformRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public GamePlatform GetBySlug(string slug)
        {
            return Query(c => c.Slug == slug).FirstOrDefault();
        }

        public GamePlatform GetByCode(string code)
        {
            return Query(c => c.Code == code).FirstOrDefault();
        }
    }
}
