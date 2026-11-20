using System.Collections.Generic;

namespace NzbDrone.Core.Platforms
{
    public class PlatformFormatSettings
    {
        public List<PlatformFormatProfile> Profiles { get; set; } = new List<PlatformFormatProfile>();
    }
}
