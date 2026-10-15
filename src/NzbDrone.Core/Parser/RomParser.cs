using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser
{
    public static class RomParser
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static readonly Regex RegionRegex = new Regex(@"\((USA|Europe|Japan|World|En,Fr,De|Asia|Australia|Brazil|Canada|China|France|Germany|Italy|Korea|Netherlands|Spain|Sweden|Taiwan|UK)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RevisionRegex = new Regex(@"\((?:Rev\s*(?<rev>[A-Z0-9]+)|v(?<ver>\d+(?:\.\d+)*))\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex HardwareRegex = new Regex(@"\((NES 2\.0|Headered|Unheadered)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DumpRegex = new Regex(@"\((Beta|Proto|Demo|Sample)\)|\[(?<tag>!|b|t|h|f|a|T\+[A-Za-z]+|T\-[A-Za-z]+\s*by.*?)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DiscTrackRegex = new Regex(@"\((?:Disc\s*(?<disc>\d+(?:\s*of\s*\d+)?)|Track\s*(?<track>\d+))\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FullSetRegex = new Regex(@"\((Complete Set|Full Set|1G1R|Parent-Clone|[\d\-]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] IgnoredPrefixes = { "_Top", "A-F", "G-K", "1 USA", "2 Japan", "Translations" };

        public static ParsedRomInfo ParseRomName(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

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
                        result.IsFullSetArchive = true;
                }
            }

            // Strip extension
            result.Format = Path.GetExtension(normalizedTitle)?.TrimStart('.');
            if (!string.IsNullOrEmpty(result.Format))
                normalizedTitle = Path.GetFileNameWithoutExtension(normalizedTitle);

            // Regions
            foreach (Match match in RegionRegex.Matches(normalizedTitle))
            {
                result.Regions.Add(match.Groups[1].Value);
                normalizedTitle = normalizedTitle.Replace(match.Value, "");
            }

            // Revision / Version
            var revMatch = RevisionRegex.Match(normalizedTitle);
            if (revMatch.Success)
            {
                if (revMatch.Groups["rev"].Success) result.Revision = revMatch.Groups["rev"].Value;
                if (revMatch.Groups["ver"].Success) result.Version = revMatch.Groups["ver"].Value;
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
                if (discMatch.Groups["disc"].Success) result.DiscNumber = discMatch.Groups["disc"].Value;
                if (discMatch.Groups["track"].Success) result.TrackNumber = discMatch.Groups["track"].Value;
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
                result.Quality = new QualityModel(Quality.VerifiedGoodDump);
            else if (result.DumpTags.Contains("b"))
                result.Quality = new QualityModel(Quality.UnverifiedRaw);
            else if (result.DumpTags.Any(t => t.StartsWith("T+") || t.StartsWith("T-")))
                result.Quality = new QualityModel(Quality.TranslationPatch);
            else if (result.DumpTags.Any(t => new[] { "Beta", "Proto" }.Contains(t)))
                result.Quality = new QualityModel(Quality.BetaProto);

            // Clean game title
            normalizedTitle = Regex.Replace(normalizedTitle, @"\[.*?\]|\(.*?\)", "").Trim();

            // Strip ignored prefixes if present
            foreach (var prefix in IgnoredPrefixes)
            {
                if (normalizedTitle.StartsWith(prefix + " "))
                {
                    normalizedTitle = normalizedTitle.Substring(prefix.Length + 1).Trim();
                }
            }

            result.GameTitle = normalizedTitle;

            return result;
        }
    }
}
