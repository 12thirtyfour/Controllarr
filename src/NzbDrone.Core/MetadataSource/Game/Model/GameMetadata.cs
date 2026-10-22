using System;

namespace NzbDrone.Core.MetadataSource.Game.Model
{
    public class GameMetadata
    {
        public int Id { get; set; }
        public int PlatformId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string CoverUrl { get; set; }
        public string Developer { get; set; }
        public string Publisher { get; set; }
    }
}
