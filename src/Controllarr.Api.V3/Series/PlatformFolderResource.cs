using NzbDrone.Core.Languages;

namespace Sonarr.Api.V3.Series
{
    public class PlatformFolderResource
    {
        public Language Language { get; set; }
        public string Folder { get; set; }
    }

    public class SeriesFolderResource : PlatformFolderResource
    {
    }
}
