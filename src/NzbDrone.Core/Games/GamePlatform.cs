using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Games
{
    public class GamePlatform : ModelBase
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public int IgdbId { get; set; }
        public string Overview { get; set; }
        public string Path { get; set; }
        public bool Monitored { get; set; }
        public string RootFolderPath { get; set; }
        public CollectionStrategy CollectionStrategy { get; set; }
        public RevisionPreference RevisionPreference { get; set; }
        public string Code { get; set; }
        public int? ReleaseYear { get; set; }
    }
}
