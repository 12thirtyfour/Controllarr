using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    public static class RomParser
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Regex RegionRegex = new Regex(@"\((?:USA|Europe|Japan|World|Asia|Australia|Brazil|Canada|China|Denmark|Finland|France|Germany|Greece|Hong Kong|India|Italy|Korea|Latin America|Mexico|Netherlands|Norway|Poland|Portugal|Russia|Scandinavia|Spain|Sweden|Taiwan|UK|United Kingdom|,\s*)+\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RevisionRegex = new Regex(@"(?:\(|\[)(?:Rev\.?\s*(?<rev>[A-Za-z0-9.]+)|v(?<ver>\d+(?:\.\d+)*[A-Za-z]?)|Version\s*(?<ver>[\d.]+)|Alt\s*(?<alt>\d*)|PRG(?<prg>\d+))(?:\)|\])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HardwareRegex = new Regex(@"\((NES 2\.0|Headered|Unheadered)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DumpRegex = new Regex(@"\((Beta|Proto|Demo|Sample)\)|\[(?<tag>!|b|t|h|f|a|T\+[A-Za-z]+|T\-[A-Za-z]+\s*by.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DiscTrackRegex = new Regex(@"\((?:Disc\s*(?<disc>\d+(?:\s*of\s*\d+)?)|Track\s*(?<track>\d+))\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FullSetRegex = new Regex(@"\((Complete Set|Full Set|1G1R|Parent-Clone|[\d\-]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FormatTokenRegex = new Regex(@"\b(3ds|cia|cci|nds|dsi|gba|gbc|gb|nes|snes|sfc|smc|n64|z64|v64|iso|ciso|chd|rvz|gcm|wbfs|wux|wud|wua|nsp|nsz|xci|pbp|cso|vpk|pkg|bin|cue|gdi|cdi|zip|7z)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BracketCleanupRegex = new Regex(@"\[.*?\]|\(.*?\)", RegexOptions.Compiled);
        private static readonly Regex IndexPrefixRegex = new Regex(@"^(?:\[#?\d+\]|\(#?\d+\)|#\d+|0\d{1,4})\s*[-_.)]?\s*", RegexOptions.Compiled);

        private static readonly string[] IgnoredPrefixes = { "_Top", "A-F", "G-K", "1 USA", "2 Japan", "Translations" };

        public static string CleanIndexPrefix(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var cleaned = title.Trim();
            while (IndexPrefixRegex.IsMatch(cleaned))
            {
                var next = IndexPrefixRegex.Replace(cleaned, string.Empty).Trim();
                if (next == cleaned || string.IsNullOrWhiteSpace(next))
                {
                    break;
                }

                cleaned = next;
            }

            return cleaned;
        }

        public static ParsedRomInfo ParseRomName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var result = new ParsedRomInfo { ReleaseTitle = title };
            var normalizedTitle = title;

            // Extract filename if path
            if (title.Contains('/') || title.Contains('\\'))
            {
                normalizedTitle = Path.GetFileName(title);
                var dirParts = title.Replace('\\', '/').Split('/');
                foreach (var part in dirParts)
                {
                    if (FullSetRegex.IsMatch(part))
                    {
                        result.IsFullSetArchive = true;
                    }
                }
            }

            // Strip extension only if it is an actual file extension at the end of the string (no spaces or parentheses)
            var ext = Path.GetExtension(normalizedTitle);
            if (!string.IsNullOrEmpty(ext) && Regex.IsMatch(ext, @"^\.[A-Za-z0-9]{1,6}$"))
            {
                result.Format = ext.TrimStart('.');
                normalizedTitle = Path.GetFileNameWithoutExtension(normalizedTitle);
            }

            if (string.IsNullOrWhiteSpace(result.Format))
            {
                var fmtMatch = FormatTokenRegex.Match(normalizedTitle);
                if (fmtMatch.Success)
                {
                    result.Format = fmtMatch.Value.ToUpperInvariant();
                }
            }

            // Regions
            foreach (Match match in RegionRegex.Matches(normalizedTitle))
            {
                var content = match.Value.Trim('(', ')');
                var tokens = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in tokens)
                {
                    var trimmed = token.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        result.Regions.Add(trimmed);
                    }
                }

                normalizedTitle = normalizedTitle.Replace(match.Value, "");
            }

            // Revision / Version
            foreach (Match revMatch in RevisionRegex.Matches(normalizedTitle))
            {
                if (revMatch.Groups["rev"].Success && string.IsNullOrWhiteSpace(result.Revision))
                {
                    result.Revision = revMatch.Groups["rev"].Value;
                }

                if (revMatch.Groups["ver"].Success && string.IsNullOrWhiteSpace(result.Version))
                {
                    result.Version = revMatch.Groups["ver"].Value;
                }

                normalizedTitle = normalizedTitle.Replace(revMatch.Value, "");
            }

            // Hardware
            foreach (Match match in HardwareRegex.Matches(normalizedTitle))
            {
                result.HardwareTags.Add(match.Groups[1].Value);
                normalizedTitle = normalizedTitle.Replace(match.Value, "");
            }

            // Dump tags
            foreach (Match match in DumpRegex.Matches(normalizedTitle))
            {
                result.DumpTags.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups["tag"].Value);
                normalizedTitle = normalizedTitle.Replace(match.Value, "");
            }

            // Disc / Track
            var discMatch = DiscTrackRegex.Match(normalizedTitle);
            if (discMatch.Success)
            {
                if (discMatch.Groups["disc"].Success)
                {
                    result.DiscNumber = discMatch.Groups["disc"].Value;
                }

                if (discMatch.Groups["track"].Success)
                {
                    result.TrackNumber = discMatch.Groups["track"].Value;
                }

                normalizedTitle = normalizedTitle.Replace(discMatch.Value, "");
            }

            // Full set checks
            var fullSetMatch = FullSetRegex.Match(normalizedTitle);
            if (fullSetMatch.Success)
            {
                result.IsFullSetArchive = true;
                normalizedTitle = normalizedTitle.Replace(fullSetMatch.Value, "");
            }

            // Quality
            result.Quality = new QualityModel(Quality.Unknown);
            if (result.DumpTags.Contains("!"))
            {
                result.Quality = new QualityModel(Quality.VerifiedGoodDump);
            }
            else if (result.DumpTags.Contains("b"))
            {
                result.Quality = new QualityModel(Quality.UnverifiedRaw);
            }
            else if (result.DumpTags.Any(t => t.StartsWith("T+", StringComparison.OrdinalIgnoreCase) || t.StartsWith("T-", StringComparison.OrdinalIgnoreCase)))
            {
                result.Quality = new QualityModel(Quality.TranslationPatch);
            }
            else if (result.DumpTags.Any(t => t.Equals("Beta", StringComparison.OrdinalIgnoreCase) || t.Equals("Proto", StringComparison.OrdinalIgnoreCase)))
            {
                result.Quality = new QualityModel(Quality.BetaProto);
            }

            // Clean game title
            normalizedTitle = CleanIndexPrefix(BracketCleanupRegex.Replace(normalizedTitle, "").Trim());

            // Strip ignored prefixes if present
            foreach (var prefix in IgnoredPrefixes)
            {
                if (normalizedTitle.StartsWith(prefix + " "))
                {
                    normalizedTitle = normalizedTitle.Substring(prefix.Length + 1).Trim();
                }
            }

            normalizedTitle = CleanIndexPrefix(normalizedTitle);
            result.GameTitle = normalizedTitle;

            return result;
        }
    }
}
