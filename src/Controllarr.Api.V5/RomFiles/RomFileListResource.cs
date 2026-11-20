namespace Sonarr.Api.V5.RomFiles
{
    public class RomFileListResource
    {
        public List<int> RomFileIds { get; set; } = [];
    }

    public class EpisodeFileListResource : RomFileListResource
    {
    }
}
