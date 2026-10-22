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
        private static readonly Regex SceneGroupRegex = new Regex(@"[-_](?<group>[A-Za-z0-9]+)(?:(?:\s*\[.*\])*)$", RegexOptions.Compiled);
        private static readonly Regex CrcRegex = new Regex(@"^[A-Fa-f0-9]{8}$", RegexOptions.Compiled);
        private static readonly Regex DiscRegex = new Regex(@"^Disc\s*(?<num>\d+)(?:\s*of\s*(?<count>\d+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FlagsRegex = new Regex(@"^([!bthfa]|T\+[A-Za-z]+|T\-[A-Za-z]+\s*by.*?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, string> StandardRegions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "USA", "USA" },
            { "EUR", "Europe" },
            { "Europe", "Europe" },
            { "JPN", "Japan" },
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

        public RomReleaseInfo Parse(string releaseTitle)
        {
            if (string.IsNullOrWhiteSpace(releaseTitle))
            {
                return null;
            }

            var result = new RomReleaseInfo();
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
                result.Format = ext;
                workingTitle = Path.GetFileNameWithoutExtension(workingTitle);
            }

            // Scene groups
            var sceneMatch = SceneGroupRegex.Match(workingTitle);
            if (sceneMatch.Success)
            {
                workingTitle = workingTitle.Substring(0, sceneMatch.Index);
            }

            // Extract tags inside brackets [...] and parens (...)
            var matches = BracketOrParenRegex.Matches(workingTitle);
            foreach (Match match in matches)
            {
                var tagContent = match.Groups["tag"].Value.Trim();

                // Check CRC
                if (CrcRegex.IsMatch(tagContent))
                {
                    continue; // Just strip it
                }

                // Check revision e.g. "Rev 1", "v1.1", "Rev A", "Beta", "Proto", "Demo"
                if (tagContent.StartsWith("Rev ", StringComparison.OrdinalIgnoreCase) ||
                    (tagContent.StartsWith("Rev", StringComparison.OrdinalIgnoreCase) && tagContent.Length > 3 && char.IsDigit(tagContent[3])) ||
                    (tagContent.StartsWith("v", StringComparison.OrdinalIgnoreCase) && tagContent.Length > 1 && char.IsDigit(tagContent[1])) ||
                    tagContent.Equals("Beta", StringComparison.OrdinalIgnoreCase) ||
                    tagContent.Equals("Proto", StringComparison.OrdinalIgnoreCase) ||
                    tagContent.Equals("Demo", StringComparison.OrdinalIgnoreCase))
                {
                    if (result.Revision == null)
                    {
                        result.Revision = tagContent;
                    }

                    continue;
                }

                // Check platform tag in brackets/parens
                if (KnownPlatforms.Contains(tagContent) && result.Platform == null)
                {
                    result.Platform = KnownPlatforms.First(p => p.Equals(tagContent, StringComparison.OrdinalIgnoreCase));
                    continue;
                }

                // Check disc info
                var discMatch = DiscRegex.Match(tagContent);
                if (discMatch.Success)
                {
                    if (int.TryParse(discMatch.Groups["num"].Value, out var num))
                    {
                        result.DiscNumber = num;
                    }

                    if (discMatch.Groups["count"].Success && int.TryParse(discMatch.Groups["count"].Value, out var count))
                    {
                        result.DiscCount = count;
                    }

                    continue;
                }

                // Check flags
                if (FlagsRegex.IsMatch(tagContent))
                {
                    if (!result.Flags.Contains(tagContent))
                    {
                        result.Flags.Add(tagContent);
                    }

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
            if (result.Platform == null && !string.IsNullOrEmpty(result.Format))
            {
                if (ExtensionPlatformMap.TryGetValue(result.Format, out var platformFromExt))
                {
                    result.Platform = platformFromExt;
                }
            }

            // Clean game title by removing bracketed/parenthetical metadata
            var cleanTitle = BracketOrParenRegex.Replace(workingTitle, "").Trim();

            // Remove extra whitespace or trailing punctuation/dashes
            cleanTitle = Regex.Replace(cleanTitle, @"\s+", " ").Trim(' ', '-', '_');

            result.Title = cleanTitle;

            return result;
        }

        public int CalculateRomPriority(RomReleaseInfo info, List<string> preferredRegions)
        {
            if (info == null)
            {
                return 0;
            }

            var score = 0;

            // Preferred region matching
            if (preferredRegions != null && preferredRegions.Any() && info.Regions.Any())
            {
                // Score based on position in preferred list
                for (var i = 0; i < preferredRegions.Count; i++)
                {
                    if (info.Regions.Contains(preferredRegions[i], StringComparer.OrdinalIgnoreCase))
                    {
                        // Higher score for earlier matches in the list
                        score += 1000 - (i * 100);
                        break; // Score based on best matching region
                    }
                }
            }

            // Good dumps
            if (info.Flags.Contains("!"))
            {
                score += 500;
            }

            // Bad dumps
            if (info.Flags.Contains("b"))
            {
                score -= 1000;
            }

            // Hacks, trained, alt
            if (info.Flags.Contains("h") || info.Flags.Contains("t") || info.Flags.Contains("a"))
            {
                score -= 500;
            }

            // Prototypes/Betas/Demos (penalize if looking for official release, but keep them if nothing else)
            if (info.Revision != null && (
                info.Revision.Equals("Beta", StringComparison.OrdinalIgnoreCase) ||
                info.Revision.Equals("Proto", StringComparison.OrdinalIgnoreCase) ||
                info.Revision.Equals("Demo", StringComparison.OrdinalIgnoreCase)))
            {
                score -= 200;
            }

            // Revisions
            if (info.Revision != null && info.Revision.StartsWith("Rev", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            return score;
        }
    }
}
