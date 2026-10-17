using System.Collections.Generic;

namespace NzbDrone.Core.Platforms
{
    public class PlatformFormatProfile
    {
        public string GamePlatform { get; set; }
        public List<string> AllowedExtensions { get; set; } = new List<string>();
        public List<string> AllowedCustomExtensions { get; set; } = new List<string>();
        public long MinimumSizeBytes { get; set; }
        public long MaximumSizeBytes { get; set; }
    }
}
