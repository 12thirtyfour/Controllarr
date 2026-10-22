using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.Game.Model
{
    public class RomReleaseInfo
    {
        public string FileName { get; set; }
        public string Hash { get; set; }
        public string Region { get; set; }
        public List<string> Languages { get; set; }
        public string Format { get; set; }
    }
}
