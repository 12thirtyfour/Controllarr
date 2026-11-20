using NzbDrone.Core.Languages;

namespace Sonarr.Api.V5.Platforms;

public class PlatformFolderResource
{
    public required Language Language { get; set; }
    public required string Folder { get; set; }
}

public class SeriesFolderResource : PlatformFolderResource
{
}
