using System.Collections.Generic;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser.Model
{
    /// <summary>
    /// Represents the parsed metadata extracted from a ROM filename.
    /// </summary>
    public class ParsedRomInfo
    {
        /// <summary>
        /// Gets or sets the original release title.
        /// </summary>
        public string ReleaseTitle { get; set; }

        /// <summary>
        /// Gets or sets the base game title.
        /// </summary>
        public string GameTitle { get; set; }

        /// <summary>
        /// Gets or sets the parsed quality of the ROM.
        /// </summary>
        public QualityModel Quality { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the archive is a full set (e.g. 1G1R).
        /// </summary>
        public bool IsFullSetArchive { get; set; }

        /// <summary>
        /// Gets or sets the list of regions.
        /// </summary>
        public List<string> Regions { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the revision.
        /// </summary>
        public string Revision { get; set; }

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the list of hardware tags.
        /// </summary>
        public List<string> HardwareTags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the list of dump tags.
        /// </summary>
        public List<string> DumpTags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the disc number.
        /// </summary>
        public string DiscNumber { get; set; }

        /// <summary>
        /// Gets or sets the track number.
        /// </summary>
        public string TrackNumber { get; set; }

        /// <summary>
        /// Gets or sets the file format or extension.
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Gets or sets the release group.
        /// </summary>
        public string ReleaseGroup { get; set; }

        /// <summary>
        /// Gets or sets the release hash.
        /// </summary>
        public string ReleaseHash { get; set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            var parts = new List<string> { GameTitle };
            if (Regions.Count > 0)
            {
                parts.Add($"({string.Join(",", Regions)})");
            }

            if (Revision != null)
            {
                parts.Add($"(Rev {Revision})");
            }

            if (Version != null)
            {
                parts.Add($"(v{Version})");
            }

            parts.AddRange(HardwareTags);
            parts.AddRange(DumpTags);
            if (DiscNumber != null)
            {
                parts.Add($"(Disc {DiscNumber})");
            }

            if (TrackNumber != null)
            {
                parts.Add($"(Track {TrackNumber})");
            }

            parts.Add(Quality?.ToString() ?? "[Unknown Quality]");
            return string.Join(" ", parts);
        }
    }
}
