namespace Sonarr.Api.V5.Series;

public class PlatformEditorDeleteResource
{
    public List<int> SeriesIds { get; set; } = [];
    public bool DeleteFiles { get; set; }
}

public class SeriesEditorDeleteResource : PlatformEditorDeleteResource
{
}
