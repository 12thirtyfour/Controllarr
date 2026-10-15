using System;
using System.Collections.Generic;
using NzbDrone.Core.Games;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser.Model
{
    public class ParsedRomInfo
    {
        public string ReleaseTitle { get; set; }
        public string GameTitle { get; set; }
        public QualityModel Quality { get; set; }
        public bool IsFullSetArchive { get; set; }

        public List<string> Regions { get; set; } = new List<string>();
        public string Revision { get; set; }
        public string Version { get; set; }
        public List<string> HardwareTags { get; set; } = new List<string>();
        public List<string> DumpTags { get; set; } = new List<string>();
        public string DiscNumber { get; set; }
        public string TrackNumber { get; set; }
        public string Format { get; set; }

        public string ReleaseGroup { get; set; }
        public string ReleaseHash { get; set; }

        public override string ToString()
        {
            var parts = new List<string> { GameTitle };
            if (Regions.Count > 0) parts.Add($"({string.Join(",", Regions)})");
            if (Revision != null) parts.Add($"(Rev {Revision})");
            if (Version != null) parts.Add($"(v{Version})");
            parts.AddRange(HardwareTags);
            parts.AddRange(DumpTags);
            if (DiscNumber != null) parts.Add($"(Disc {DiscNumber})");
            if (TrackNumber != null) parts.Add($"(Track {TrackNumber})");
            parts.Add(Quality?.ToString() ?? "[Unknown Quality]");
            return string.Join(" ", parts);
        }
    }
}
