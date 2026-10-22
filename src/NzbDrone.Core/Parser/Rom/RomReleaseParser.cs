using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Parser.Rom
{
    public class RomReleaseParser : IRomReleaseParser
    {
        private static readonly Regex BracketOrParenRegex = new Regex(@"\[(?<tag>[^\]]+)\]|\((?<tag>[^\)]+)\)", RegexOptions.Compiled);
        private static readonly Regex VerifiedDumpRegex = new Regex(@"\[!\]|Redump|Verified|GoodDump", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, string> StandardRegions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "USA", "USA" },
            { "EUR", "EUR" },
            { "Europe", "Europe" },
            { "JPN", "JPN" },
            { "Japan", "Japan" },
            { "World", "World" },
            { "Asia", "Asia" },
            { "Australia", "Australia" },
            { "Brazil", "Brazil" },
            { "Canada", "Canada" },
            { "China", "China" },
            { "France", "France" },
            { "Germany", "Germany" },
            { "Italy", "Italy" },
            { "Korea", "Korea" },
            { "Netherlands", "Netherlands" },
            { "Spain", "Spain" },
            { "Sweden", "Sweden" },
            { "Taiwan", "Taiwan" },
            { "UK", "UK" }
        };

        private static readonly HashSet<string> KnownLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "En", "Ja", "Es", "Fr", "De", "It", "Nl", "Pt", "Sv", "No", "Da", "Fi", "Pl", "Ru", "Zh", "Ko",
            "English", "Japanese", "Spanish", "French", "German", "Italian"
        };

        private static readonly HashSet<string> KnownPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GBA", "SNES", "PS2", "NES", "N64", "NDS", "3DS", "PSX", "PS1", "PSP",
            "GameCube", "GC", "Wii", "DC", "Saturn", "SS", "SMS", "GG", "PCE", "TG16", "Genesis", "MD"
        };

        private static readonly Dictionary<string, string> ExtensionPlatformMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".gba", "GBA" },
            { ".sfc", "SNES" },
            { ".smc", "SNES" },
            { ".nes", "NES" },
            { ".z64", "N64" },
            { ".n64", "N64" },
            { ".nds", "NDS" },
            { ".3ds", "3DS" },
            { ".gb", "GB" },
            { ".gbc", "GBC" }
        };

        public RomParsedInfo Parse(string releaseTitle)
        {
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                return null;
            }

            var result = new RomParsedInfo();
            var workingTitle = releaseTitle;

            // Strip path if present
            if (workingTitle.Contains('/') || workingTitle.Contains('\\'))
            {
                workingTitle = Path.GetFileName(workingTitle);
            }

            // Extension
            var ext = Path.GetExtension(workingTitle);
            if (!string.IsNullOrEmpty(ext))
            {
                result.FormatExtension = ext;
                workingTitle = Path.GetFileNameWithoutExtension(workingTitle);
            }

            // Check verified dump markers in raw title (e.g. [!], Redump)
            if (VerifiedDumpRegex.IsMatch(releaseTitle))
            {
                result.IsVerifiedDump = true;
            }

            // Extract tags inside brackets [...] and parens (...)
            var matches = BracketOrParenRegex.Matches(workingTitle);
            foreach (Match match in matches)
            {
                var tagContent = match.Groups["tag"].Value.Trim();

                // Check revision e.g. "Rev 1", "v1.1", "Rev A"
                if (tagContent.StartsWith("Rev ", StringComparison.OrdinalIgnoreCase) ||
                    tagContent.StartsWith("Rev", StringComparison.OrdinalIgnoreCase) && tagContent.Length > 3 && char.IsDigit(tagContent[3]) ||
                    tagContent.StartsWith("v", StringComparison.OrdinalIgnoreCase) && tagContent.Length > 1 && char.IsDigit(tagContent[1]))
                {
                    if (result.Revision == null)
                    {
                        result.Revision = tagContent;
                    }

                    continue;
                }

                // Check platform tag in brackets/parens
                if (KnownPlatforms.Contains(tagContent) && result.PlatformTag == null)
                {
                    result.PlatformTag = KnownPlatforms.First(p => p.Equals(tagContent, StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                // Check multi-item comma separated tag like "USA, Europe" or "En,Fr,De"
                var parts = tagContent.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (StandardRegions.TryGetValue(trimmed, out var stdRegion))
                    {
                        if (!result.Regions.Contains(stdRegion))
                        {
                            result.Regions.Add(stdRegion);
                        }
                    }
                    else if (KnownLanguages.Contains(trimmed))
                    {
                        if (!result.Languages.Contains(trimmed))
                        {
                            result.Languages.Add(trimmed);
                        }
                    }
                }
            }

            // Fallback platform from extension if not found in tags
            if (result.PlatformTag == null && !string.IsNullOrEmpty(result.FormatExtension))
            {
                if (ExtensionPlatformMap.TryGetValue(result.FormatExtension, out var platformFromExt))
                {
                    result.PlatformTag = platformFromExt;
                }
            }

            // Clean game title by removing bracketed/parenthetical metadata
            var cleanTitle = BracketOrParenRegex.Replace(workingTitle, "").Trim();
            // Remove extra whitespace or trailing punctuation/dashes
            cleanTitle = Regex.Replace(cleanTitle, @"\s+", " ").Trim(' ', '-', '_');

            result.GameTitle = cleanTitle;

            return result;
        }
    }
}
