using NzbDrone.Core.Languages;

namespace Sonarr.Api.V5.Series;

public class PlatformFolderResource
{
    public required Language Language { get; set; }
    public required string Folder { get; set; }
}

public class SeriesFolderResource : PlatformFolderResource
{
}
