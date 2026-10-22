using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Games
{
    public class RomEntry : ModelBase
    {
        public int PlatformId { get; set; }
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public string Region { get; set; }
        public string Language { get; set; }
        public string Revision { get; set; }
        public string Hash { get; set; }
    }
}
