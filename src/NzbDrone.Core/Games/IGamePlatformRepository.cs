using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Games
{
    public interface IGamePlatformRepository : IBasicRepository<GamePlatform>
    {
        GamePlatform GetBySlug(string slug);
        GamePlatform GetByCode(string code);
    }
}
