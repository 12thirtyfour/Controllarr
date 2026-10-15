using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Games
{
    public class GameRelease : ModelBase
    {
        public int GameTitleId { get; set; }
        public int PlatformId { get; set; }
        public string Title { get; set; }
        public string ReleaseName { get; set; }
        public string VersionTag { get; set; }
        public string Revision { get; set; }
        public string Region { get; set; }
        public bool Monitored { get; set; }
        public int FileId { get; set; }
    }
}
