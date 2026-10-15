using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Games
{
    public interface IGameReleaseRepository : IBasicRepository<GameRelease>
    {
    }

    public class GameReleaseRepository : BasicRepository<GameRelease>, IGameReleaseRepository
    {
        public GameReleaseRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
