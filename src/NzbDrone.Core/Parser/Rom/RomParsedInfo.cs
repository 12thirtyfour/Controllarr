using System.Collections.Generic;

namespace NzbDrone.Core.Parser.Rom
{
    public class RomParsedInfo
    {
        public string GameTitle { get; set; }
        public string PlatformTag { get; set; }
        public List<string> Regions { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
        public string Revision { get; set; }
        public bool IsVerifiedDump { get; set; }
        public string FormatExtension { get; set; }
    }
}
