namespace Sonarr.Api.V5.EpisodeFiles
{
    public class RomFileListResource
    {
        public List<int> EpisodeFileIds { get; set; } = [];
    }

    public class EpisodeFileListResource : RomFileListResource
    {
    }
}
