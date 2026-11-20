using System.Collections.Generic;

namespace NzbDrone.Core.Games
{
    public class PlatformDefinition
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Manufacturer { get; set; }
        public Dictionary<string, string> RegionalConsoleNames { get; set; } = new Dictionary<string, string>();
        public List<string> RomFileExtensions { get; set; } = new List<string>();
    }
}
