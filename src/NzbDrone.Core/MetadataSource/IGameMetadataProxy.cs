using System;
using System.Collections.Generic;
using NzbDrone.Core.Games;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.MetadataSource
{
    public interface IGameMetadataProxy
    {
        List<GamePlatform> GetPlatforms();
        Tuple<GameTitle, List<GameRelease>> GetGameInfo(string providerId, Language language);
        List<GameTitle> SearchForNewGames(string title, Language language);
    }
}
