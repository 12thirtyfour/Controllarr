using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Games
{
    public interface IGameTitleRepository : IBasicRepository<GameTitle>
    {
    }

    public class GameTitleRepository : BasicRepository<GameTitle>, IGameTitleRepository
    {
        public GameTitleRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
