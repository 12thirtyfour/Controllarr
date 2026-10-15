using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Games
{
    public interface IGamePlatformRepository : IBasicRepository<GamePlatform>
    {
    }

    public class GamePlatformRepository : BasicRepository<GamePlatform>, IGamePlatformRepository
    {
        public GamePlatformRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
