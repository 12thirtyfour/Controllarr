using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Games
{
    public class GameTitle : ModelBase
    {
        public int PlatformId { get; set; }
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public int IgdbId { get; set; }
        public bool Monitored { get; set; }
        public string ProviderId { get; set; }

        public List<GameRelease> Releases { get; set; }

        public GameTitle()
        {
            Releases = new List<GameRelease>();
        }
    }
}
