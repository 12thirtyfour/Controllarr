using System.Collections.Generic;

namespace Sonarr.Api.V3.Series
{
    public class PlatformEditorDeleteResource
    {
        public List<int> SeriesIds { get; set; }
        public bool DeleteFiles { get; set; }
    }

    public class SeriesEditorDeleteResource : PlatformEditorDeleteResource
    {
    }
}
