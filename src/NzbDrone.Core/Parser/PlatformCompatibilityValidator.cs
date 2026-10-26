using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Parser
{
    public static class PlatformCompatibilityValidator
    {
        private static readonly Dictionary<string, HashSet<string>> ExclusiveExtensionsByPlatform = new(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nes", ".unf", ".fds" },
            ["fds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".fds" },
            ["snes"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sfc", ".smc", ".bs" },
            ["satellaview"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bs" },
            ["n64"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".z64", ".n64", ".v64", ".ndd" },
            ["64dd"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ndd" },
            ["gb"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gb" },
            ["gbc"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gbc" },
            ["gba"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gba" },
            ["nds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nds", ".dsi" },
            ["3ds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".3ds", ".cia", ".cci" },
            ["switch"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz" },
            ["genesis"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".gen", ".smd" },
            ["32x"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".32x" },
            ["sms"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sms" },
            ["gg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gg" },
            ["pce"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pce", ".sgx" },
            ["tg16"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pce", ".sgx" },
            ["atari2600"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".a26" },
            ["atari7800"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".a78" },
            ["lynx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".lnx" },
            ["jaguar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".j64", ".jag" },
            ["neogeo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".neo" },
            ["ngpc"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ngc", ".ngp" },
            ["wonderswan"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ws", ".wsc" },
            ["vb"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vb" },
            ["ps1"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pbp" },
            ["ps3"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pkg" },
            ["psvita"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vpk" },
            ["wiiu"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wua", ".wux", ".wud", ".rpx" },
            ["gamecube"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gcm", ".ciso" },
            ["wii"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wbfs" }
        };

        private static readonly HashSet<string> CartridgePlatforms = new(StringComparer.OrdinalIgnoreCase)
        {
            "nes", "fds", "snes", "satellaview", "n64", "64dd", "gb", "gbc", "gba", "nds", "3ds", "switch",
            "genesis", "32x", "sms", "gg", "tg16", "pce", "atari2600", "atari7800", "lynx",
            "jaguar", "neogeo", "ngpc", "wonderswan", "vb", "psvita"
        };

        private static readonly HashSet<string> DiscExclusiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".iso", ".chd", ".cso", ".cue", ".gdi", ".cdi", ".rvz", ".wbfs", ".wua", ".wux", ".wud", ".nsp", ".xci"
        };

        private static readonly Dictionary<string, string[]> PlatformAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = new[] { "nes", "nintendo-entertainment-system", "nintendo entertainment system", "famicom" },
            ["fds"] = new[] { "fds", "famicom-disk-system", "famicom disk system", "disk system" },
            ["snes"] = new[] { "snes", "super-nintendo-entertainment-system", "super nintendo entertainment system", "super nintendo", "super nes", "super famicom", "sfc" },
            ["satellaview"] = new[] { "satellaview", "bs-x", "bsx" },
            ["n64"] = new[] { "n64", "nintendo-64", "nintendo 64", "ultra 64" },
            ["64dd"] = new[] { "64dd", "nintendo-64dd", "nintendo 64dd" },
            ["gamecube"] = new[] { "gamecube", "nintendo-gamecube", "nintendo gamecube", "gcn", "ngc" },
            ["wii"] = new[] { "wii", "nintendo-wii", "nintendo wii" },
            ["wiiu"] = new[] { "wii-u", "wiiu", "nintendo-wii-u", "nintendo wii u" },
            ["gb"] = new[] { "game-boy", "game boy", "gameboy", "dmg" },
            ["gbc"] = new[] { "game-boy-color", "game boy color", "gameboy color", "cgb" },
            ["gba"] = new[] { "game-boy-advance", "game boy advance", "gameboy advance", "agb" },
            ["nds"] = new[] { "nintendo-ds", "nintendo ds", "nds" },
            ["3ds"] = new[] { "nintendo-3ds", "nintendo 3ds", "3ds", "n3ds" },
            ["switch"] = new[] { "nintendo-switch", "nintendo switch", "switch", "nsw", "nx" },
            ["genesis"] = new[] { "sega-genesis", "sega genesis", "genesis", "mega-drive", "mega drive", "megadrive", "md" },
            ["segacd"] = new[] { "sega-cd", "sega cd", "segacd", "mega-cd", "mega cd", "scd" },
            ["32x"] = new[] { "sega-32x", "sega 32x", "32x", "super 32x" },
            ["saturn"] = new[] { "sega-saturn", "sega saturn", "saturn", "ss" },
            ["dreamcast"] = new[] { "sega-dreamcast", "sega dreamcast", "dreamcast", "dc" },
            ["sms"] = new[] { "sega-master-system", "sega master system", "master system", "sms" },
            ["gg"] = new[] { "sega-game-gear", "sega game gear", "game gear", "gg" },
            ["tg16"] = new[] { "turbografx-16", "turbografx 16", "turbografx", "tg16", "pc-engine", "pc engine", "pce" },
            ["tgcd"] = new[] { "turbografx-cd", "turbografx cd", "tg-cd", "tgcd", "pc-engine-cd", "pc engine cd", "pcecd" },
            ["ps1"] = new[] { "sony-playstation", "playstation", "ps1", "psx", "sony playstation", "psone" },
            ["ps2"] = new[] { "sony-playstation-2", "playstation-2", "playstation 2", "ps2", "sony playstation 2" },
            ["ps3"] = new[] { "sony-playstation-3", "playstation-3", "playstation 3", "ps3", "sony playstation 3" },
            ["psp"] = new[] { "sony-playstation-portable", "playstation-portable", "playstation portable", "psp", "sony psp" },
            ["psvita"] = new[] { "sony-playstation-vita", "playstation-vita", "playstation vita", "ps vita", "psv", "vita" },
            ["xbox"] = new[] { "xbox", "original xbox", "og xbox" },
            ["xbox360"] = new[] { "xbox-360", "xbox 360", "xbox360", "x360" },
            ["atari2600"] = new[] { "atari-2600", "atari 2600", "2600", "vcs" },
            ["atari7800"] = new[] { "atari-7800", "atari 7800", "7800" },
            ["lynx"] = new[] { "atari-lynx", "atari lynx", "lynx" },
            ["jaguar"] = new[] { "atari-jaguar", "atari jaguar", "jaguar" },
            ["jaguarcd"] = new[] { "atari-jaguar-cd", "atari jaguar cd", "jaguar cd" },
            ["neogeo"] = new[] { "neo-geo", "neo geo", "neogeo", "aes", "mvs" },
            ["ngpc"] = new[] { "neo-geo-pocket-color", "neo geo pocket color", "ngpc", "ngp" },
            ["wonderswan"] = new[] { "wonderswan", "wonderswan-color", "wonderswan color", "wsc", "ws" },
            ["vb"] = new[] { "virtual-boy", "virtual boy", "vb" }
        };

        private static readonly Regex PlatformTagRegex = new Regex(
            @"(?:\b|_|\[|\()(3DS|NDS|GBA|GBC|N64|SNES|NES|PS1|PS2|PS3|PSP|PSX|WIIU|WII|GAMECUBE|XBOX360|XBOX|VITA|SEGA|GENESIS|SATURN|DREAMCAST)(?:\b|_|\]|\))",
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

            return true;
        }

        private static string NormalizePlatformSlug(string slug, string name)
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
    }
}
