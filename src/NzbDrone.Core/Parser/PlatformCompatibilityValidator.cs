using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.Parser
{
    public static class PlatformCompatibilityValidator
    {
        private static readonly Dictionary<string, HashSet<string>> ExclusiveExtensionsByPlatform = PlatformCatalogService.ExclusiveExtensionsByPlatform;

        private static readonly HashSet<string> CartridgePlatforms = PlatformCatalogService.CartridgePlatforms;

        private static readonly HashSet<string> DiscExclusiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".iso", ".chd", ".cso", ".cue", ".gdi", ".cdi", ".rvz", ".wbfs", ".wua", ".wux", ".wud", ".nsp", ".xci"
        };

        private static readonly Dictionary<string, string[]> PlatformAliases = PlatformCatalogService.PlatformAliases;

        private static readonly Regex PlatformTagRegex = new Regex(
            @"(?:\b|_|\[|\()(3DS|NDS|GBA|GBC|N64|SNES|NES|PS1|PS2|PS3|PSP|PSX|WIIU|WII|GAMECUBE|XBOX360|XBOX|VITA|SEGACD|SCD|MEGACD|32X|GENESIS|MEGADRIVE|SATURN|DREAMCAST|SMS|GG)(?:\b|_|\]|\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsCompatible(string filePathOrName, string platformSlug, string platformName = null, string itemFormat = null, string archiveId = null)
        {
            if (string.IsNullOrWhiteSpace(platformSlug))
            {
                return true;
            }

            var targetSlug = NormalizePlatformSlug(platformSlug, platformName);
            var isCartridge = CartridgePlatforms.Contains(targetSlug);

            var ext = Path.GetExtension(filePathOrName);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                ext = ext.ToLowerInvariant();

                // Reject PC executables and installers for console platforms
                if (ext is ".exe" or ".msi" or ".bat" or ".cmd" or ".com" or ".appimage" or ".dmg")
                {
                    return false;
                }

                // Cartridge platforms cannot have disc/optical exclusive extensions
                if (isCartridge && DiscExclusiveExtensions.Contains(ext))
                {
                    return false;
                }

                // If file extension belongs exclusively to another platform, reject
                foreach (var (platform, extensions) in ExclusiveExtensionsByPlatform)
                {
                    if (!string.Equals(platform, targetSlug, StringComparison.OrdinalIgnoreCase) &&
                        extensions.Contains(ext))
                    {
                        // Check if the target platform also shares this extension (e.g. tg16 and pce, or satellaview and snes)
                        if (ExclusiveExtensionsByPlatform.TryGetValue(targetSlug, out var targetExts) &&
                            targetExts.Contains(ext))
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            // Check item format if provided (e.g. from archive.org metadata "file.Format")
            if (!string.IsNullOrWhiteSpace(itemFormat))
            {
                var lowerFormat = itemFormat.ToLowerInvariant();
                foreach (var (platform, aliases) in PlatformAliases)
                {
                    if (string.Equals(platform, targetSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (aliases.Any(a => a.Length > 3 && lowerFormat.Contains(a)))
                    {
                        return false;
                    }
                }
            }

            // Check archive identifier (e.g. "nointro.gba" or "redump.psx")
            if (!string.IsNullOrWhiteSpace(archiveId))
            {
                var lowerArchiveId = archiveId.ToLowerInvariant();

                // Reject PC / GOG installer packages
                if (lowerArchiveId.Contains("gog-installer") ||
                    lowerArchiveId.Contains("official-gog") ||
                    lowerArchiveId.Contains("gog_games") ||
                    lowerArchiveId.Contains("goggames"))
                {
                    return false;
                }

                foreach (var (platform, aliases) in PlatformAliases)
                {
                    if (string.Equals(platform, targetSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (aliases.Any(a => lowerArchiveId.StartsWith($"nointro.{a}") ||
                                         lowerArchiveId.StartsWith($"redump.{a}") ||
                                         lowerArchiveId.Contains($"htgdb-{a}")))
                    {
                        return false;
                    }
                }
            }

            // Check directory / folder clues in filePathOrName
            var normalizedPath = filePathOrName.Replace('\\', '/');
            var dirParts = normalizedPath.Split('/');
            if (dirParts.Length > 1)
            {
                // Inspect directory segments (excluding the filename at the end)
                for (var i = 0; i < dirParts.Length - 1; i++)
                {
                    var dir = dirParts[i].Trim();
                    if (string.IsNullOrWhiteSpace(dir))
                    {
                        continue;
                    }

                    var lowerDir = dir.ToLowerInvariant();
                    foreach (var (platform, aliases) in PlatformAliases)
                    {
                        if (string.Equals(platform, targetSlug, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // If directory exactly matches another platform's slug or name, reject
                        if (aliases.Any(a => string.Equals(lowerDir, a, StringComparison.OrdinalIgnoreCase) ||
                                             lowerDir.Contains($"nintendo - {a}") ||
                                             lowerDir.Contains($"sega - {a}") ||
                                             lowerDir.Contains($"sony - {a}") ||
                                             lowerDir.Contains($"atari - {a}") ||
                                             lowerDir.Contains($"nec - {a}")))
                        {
                            return false;
                        }
                    }
                }
            }

            // Check filename platform tags (e.g. "Pokemon Ultra Sun USA 3DS-SUXXORS" or "@NES2.0")
            var fileName = Path.GetFileName(filePathOrName);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var match = PlatformTagRegex.Match(fileName);
                if (match.Success)
                {
                    var tag = match.Groups[1].Value.ToLowerInvariant();
                    var tagPlatform = NormalizePlatformSlug(tag, null);
                    if (!string.Equals(tagPlatform, targetSlug, StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if target platform's aliases contain this tag
                        if (!PlatformAliases.TryGetValue(targetSlug, out var targetAliases) ||
                            !targetAliases.Any(a => string.Equals(a, tag, StringComparison.OrdinalIgnoreCase)))
                        {
                            return false;
                        }
                    }
                }
            }

            var lowerPathOrName = filePathOrName.ToLowerInvariant();
            if (lowerPathOrName.Contains("gog-installer") ||
                lowerPathOrName.Contains("official-gog") ||
                lowerPathOrName.Contains("official gog") ||
                lowerPathOrName.Contains("gog.com"))
            {
                return false;
            }

            return true;
        }

        public static readonly HashSet<string> IgnoredRegionOrBucketFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "usa", "europe", "japan", "world", "asia", "australia", "germany", "france", "spain", "italy",
            "korea", "china", "brazil", "netherlands", "sweden", "norway", "denmark", "finland", "poland", "russia",
            "en", "ja", "fr", "de", "es", "it", "no-intro", "redump", "tosec", "htgdb", "smdb", "1g1r",
            "packs", "roms", "games", "rom", "game", "a-z", "0-9", "a-m", "n-z", "bios", "translations", "hacks"
        };

        public static string GetPlatformByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            var ext = extension.StartsWith(".") ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
            foreach (var (platform, extensions) in ExclusiveExtensionsByPlatform)
            {
                if (extensions.Contains(ext))
                {
                    return platform;
                }
            }

            return null;
        }

        public static bool IsRegionOrBucketFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return false;
            }

            var name = folderName.Trim().ToLowerInvariant();
            return IgnoredRegionOrBucketFolders.Contains(name);
        }

        public static string[] GetAliasesForPlatform(string platformSlugOrName)
        {
            if (string.IsNullOrWhiteSpace(platformSlugOrName))
            {
                return Array.Empty<string>();
            }

            var slug = NormalizePlatformSlug(platformSlugOrName, platformSlugOrName);
            if (!string.IsNullOrWhiteSpace(slug) && PlatformAliases.TryGetValue(slug, out var aliases))
            {
                return aliases;
            }

            return Array.Empty<string>();
        }

        public static string NormalizePlatformSlug(string slug, string name)
        {
            var raw = (slug ?? string.Empty).ToLowerInvariant().Trim();
            var rawDehyphenated = raw.Replace('-', ' ');

            if (PlatformAliases.ContainsKey(raw))
            {
                return raw;
            }

            foreach (var (canonicalSlug, aliases) in PlatformAliases)
            {
                if (aliases.Any(a => string.Equals(a, raw, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(a, rawDehyphenated, StringComparison.OrdinalIgnoreCase)))
                {
                    return canonicalSlug;
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                var lowerName = name.ToLowerInvariant().Trim();
                var lowerNameDehyphenated = lowerName.Replace('-', ' ');

                foreach (var (canonicalSlug, aliases) in PlatformAliases)
                {
                    if (aliases.Any(a => string.Equals(a, lowerName, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a, lowerNameDehyphenated, StringComparison.OrdinalIgnoreCase)))
                    {
                        return canonicalSlug;
                    }
                }

                var allAliasesOrdered = PlatformAliases
                    .SelectMany(p => p.Value.Select(a => (Slug: p.Key, Alias: a)))
                    .OrderByDescending(x => x.Alias.Length)
                    .ToList();

                foreach (var item in allAliasesOrdered)
                {
                    if (item.Alias.Length >= 3 && (lowerName.Contains(item.Alias) || lowerNameDehyphenated.Contains(item.Alias)))
                    {
                        return item.Slug;
                    }
                }
            }

            return raw;
        }

        public static readonly Dictionary<string, string> CanonicalPlatformNames = PlatformCatalogService.CanonicalPlatformNames;

        public static string ResolvePlatformName(string nameOrSlugOrFilename, string extension = null)
        {
            if (string.IsNullOrWhiteSpace(nameOrSlugOrFilename) && string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            var clean = !string.IsNullOrWhiteSpace(nameOrSlugOrFilename)
                ? Path.GetFileNameWithoutExtension(nameOrSlugOrFilename).Trim()
                : string.Empty;

            var slug = NormalizePlatformSlug(null, clean);
            if (!string.IsNullOrWhiteSpace(slug) && CanonicalPlatformNames.TryGetValue(slug, out var canonName))
            {
                return canonName;
            }

            var ext = !string.IsNullOrWhiteSpace(extension)
                ? extension
                : Path.GetExtension(nameOrSlugOrFilename);

            if (!string.IsNullOrWhiteSpace(ext))
            {
                var extSlug = GetPlatformByExtension(ext);
                if (!string.IsNullOrWhiteSpace(extSlug) && CanonicalPlatformNames.TryGetValue(extSlug, out var extCanonName))
                {
                    return extCanonName;
                }
            }

            return null;
        }

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".avi", ".mp4", ".m4v", ".wmv", ".mov", ".flv", ".ts", ".m2ts", ".vob", ".mpg", ".mpeg", ".webm", ".divx"
        };

        private static readonly Regex TvEpisodePattern = new(
            @"\b(s\d{1,2}[._ -]?e\d{1,3}|\d{1,2}x\d{1,3})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TvSeasonPattern = new(
            @"\b(season\s*\d{1,2}|s\d{1,2}\s*(complete|pack)?|complete\s*season)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VideoCodecOrSourcePattern = new(
            @"\b(hdtv|pdtv|dsr|bluray|bdrip|brrip|dvdrip|webrip|web-dl|webdl|x264|x265|h264|h265|xvid|divx|hevc|avc|remux|telecine|telesync|camrip)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VideoResolutionPattern = new(
            @"\b(480p|576p|720p|1080p|1080i|2160p|4k|uhd)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AnimeSubgroupPattern = new(
            @"^\[(?<subgroup>[^\]]+)\]\s*[^\[\(\]]+\s*-\s*\d{2,4}\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsTvOrVideoRelease(string title, string extension = null)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            var ext = !string.IsNullOrWhiteSpace(extension)
                ? extension
                : Path.GetExtension(title);

            if (!string.IsNullOrWhiteSpace(ext) && VideoExtensions.Contains(ext))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                if (TvEpisodePattern.IsMatch(title))
                {
                    return true;
                }

                if (TvSeasonPattern.IsMatch(title))
                {
                    return true;
                }

                if (VideoCodecOrSourcePattern.IsMatch(title))
                {
                    return true;
                }

                if (VideoResolutionPattern.IsMatch(title))
                {
                    return true;
                }

                if (AnimeSubgroupPattern.IsMatch(title))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
