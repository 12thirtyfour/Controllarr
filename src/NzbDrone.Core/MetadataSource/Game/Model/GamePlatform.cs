namespace NzbDrone.Core.MetadataSource.Game.Model
{
    public class GamePlatform
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public int? Generation { get; set; }
        public int? ReleaseYear { get; set; }
    }
}
