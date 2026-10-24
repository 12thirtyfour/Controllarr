using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.MetadataSource.Game.Model;

namespace NzbDrone.Core.MetadataSource.Game
{
    public class GameMetadataProxyStub
    {
        public Task<List<GamePlatform>> SearchPlatformsAsync(string query)
        {
            return Task.FromResult(new List<GamePlatform>());
        }

        public Task<List<GameMetadata>> SearchGamesAsync(string title, int? platformId)
        {
            return Task.FromResult(new List<GameMetadata>());
        }

        public Task<GameMetadata> GetGameDetailsAsync(int gameId)
        {
            return Task.FromResult<GameMetadata>(null);
        }

        public Task<GameMetadata> GetGameByHashAsync(string romHash)
        {
            return Task.FromResult<GameMetadata>(null);
        }
    }
}
