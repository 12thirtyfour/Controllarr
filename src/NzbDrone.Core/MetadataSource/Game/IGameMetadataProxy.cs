using System.Collections.Generic;
using System.Threading.Tasks;
using NzbDrone.Core.MetadataSource.Game.Model;

namespace NzbDrone.Core.MetadataSource.Game
{
    public interface IGameMetadataProxy
    {
        Task<List<GamePlatform>> SearchPlatformsAsync(string query);
        Task<List<GameMetadata>> SearchGamesAsync(string title, int? platformId);
        Task<GameMetadata> GetGameDetailsAsync(int gameId);
        Task<GameMetadata> GetGameByHashAsync(string romHash);
    }
}
