using System;
using System.Collections.Generic;
using NzbDrone.Core.Games;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.MetadataSource
{
    public class MockGameMetadataProxy : IGameMetadataProxy
    {
        public List<GamePlatform> GetPlatforms()
        {
            return new List<GamePlatform>
            {
                new GamePlatform { Id = 1, Name = "Nintendo Entertainment System" },
                new GamePlatform { Id = 2, Name = "Super Nintendo Entertainment System" }
            };
        }

        public Tuple<GameTitle, List<GameRelease>> GetGameInfo(string providerId, Language language)
        {
            var title = new GameTitle
            {
                Id = 1,
                Title = "Super Mario Bros.",
                ProviderId = providerId
            };

            var releases = new List<GameRelease>
            {
                new GameRelease { Id = 1, ReleaseName = "Super Mario Bros. (USA, Europe)", Region = "USA, Europe", Revision = "Rev A" },
                new GameRelease { Id = 2, ReleaseName = "Super Mario Bros. (Japan)", Region = "Japan", Revision = "" }
            };

            title.Releases = releases;

            return new Tuple<GameTitle, List<GameRelease>>(title, releases);
        }

        public List<GameTitle> SearchForNewGames(string title, Language language)
        {
            return new List<GameTitle>
            {
                new GameTitle { Id = 1, Title = title, ProviderId = "igdb:1234" }
            };
        }
    }
}
