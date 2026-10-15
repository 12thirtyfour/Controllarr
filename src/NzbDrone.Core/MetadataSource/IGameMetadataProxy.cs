using System;
using System.Collections.Generic;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    public interface IGameMetadataProxy
    {
        List<GamePlatform> GetPlatforms();
        Tuple<GameTitle, List<GameRelease>> GetGameInfo(string providerId, Language language);
        List<GameTitle> SearchForNewGames(string title, Language language);
    }
}
