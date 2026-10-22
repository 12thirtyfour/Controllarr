using System.Collections.Generic;

namespace NzbDrone.Core.Parser.Rom
{
    public class RomReleaseInfo
    {
        public string Title { get; set; }
        public string Platform { get; set; }
        public List<string> Regions { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
        public string Revision { get; set; }
        public int? DiscNumber { get; set; }
        public int? DiscCount { get; set; }
        public List<string> Flags { get; set; } = new List<string>();
        public string Format { get; set; }
    }
}
