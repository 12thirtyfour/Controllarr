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
            ".iso", ".chd", ".cso", ".cue", ".gdi", ".cdi", ".rvz", ".wbfs", ".wua", ".wux", ".wud"
        };

        private static readonly Regex OpticalDiscIndicatorRegex = new Regex(
            @"(?:\b|_|\[|\()(?:iso|chd|cso|cue|gdi|cdi|rvz|wbfs|wua|wux|wud)\b|\.(?:iso|chd|cso|cue|gdi|cdi|rvz|wbfs|wua|wux|wud)(?:\b|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex VirtualConsoleIndicatorRegex = new Regex(
            @"(?:\b|_|\[|\()(?:virtual[\s._-]*console|vc[\s._-]*wad)(?:\b|_|\]|\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex NkitIndicatorRegex = new Regex(
            @"(?:\b|_|\[|\()nkit(?:\b|_|\]|\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, string[]> PlatformAliases = PlatformCatalogService.PlatformAliases;

        private static readonly Regex PlatformTagRegex = new Regex(
            @"(?:\b|_|\[|\()(3DS|NDS|GBA|GBC|N64|SNES|NES|PS1|PS2|PS3|PSP|PSX|WIIU|WII|GAMECUBE|XBOX360|XBOX|VITA|SEGACD|SCD|MEGACD|32X|GENESIS|MEGADRIVE|SATURN|DREAMCAST|SMS|GG)(?:\b|_|\]|\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly (Regex Pattern, string PlatformSlug)[] PlatformDetectionPatterns = new[]
        {
            // Nintendo Switch
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+switch|switch|nsw|nx)(?:\b|_|\]|\))|\bHAC-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "switch"),

            // Nintendo Wii U
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+wii\s*u|wii\s*u|wii-u|wiiu)(?:\b|_|\]|\))|\bWUP-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wiiu"),

            // Nintendo Wii
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+wii|wii)(?:\b|_|\]|\))|\bRVL-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wii"),

            // Nintendo GameCube
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+gamecube|game\s*cube|gamecube|gcn|ngc)(?:\b|_|\]|\))|\bDOL-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "gamecube"),

            // Nintendo 64DD (must precede N64)
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s*64\s*dd|nintendo\s*64dd|n64dd|64dd)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "64dd"),

            // Nintendo 64
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s*64|ultra\s*64|n64)(?:\b|_|\]|\))|\bNUS-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "n64"),

            // Super Nintendo / Super Famicom
            (new Regex(@"(?:\b|_|\[|\()(?:super\s+nintendo(?:\s+entertainment\s+system)?|super\s+nes|super\s+famicom|snes|sfc)(?:\b|_|\]|\))|\b(?:SHVC|SNS)-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "snes"),

            // Famicom Disk System
            (new Regex(@"(?:\b|_|\[|\()(?:famicom\s+disk\s+system|fds)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "fds"),

            // NES / Famicom
            (new Regex(@"(?:\b|_|\[|\(|@)(?:nintendo\s+entertainment\s+system|nes(?:\s*2\.0)?|famicom)(?:\b|_|\]|\))|\b(?:NES|HVC)-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "nes"),

            // Game Boy Advance
            (new Regex(@"(?:\b|_|\[|\()(?:game\s*boy\s+advance|gba|agb)(?:\b|_|\]|\))|\bAGB-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "gba"),

            // Game Boy Color
            (new Regex(@"(?:\b|_|\[|\()(?:game\s*boy\s+color|gbc|cgb)(?:\b|_|\]|\))|\bCGB-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "gbc"),

            // Game Boy
            (new Regex(@"(?:\b|_|\[|\()(?:game\s*boy|gameboy|dmg)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "gb"),

            // Nintendo 3DS
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+3ds|3ds|n3ds)(?:\b|_|\]|\))|\bCTR-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "3ds"),

            // Nintendo DS
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+ds|nds|dsi)(?:\b|_|\]|\))|\bNTR-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "nds"),

            // Virtual Boy
            (new Regex(@"(?:\b|_|\[|\()(?:virtual\s*boy|vb)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "vb"),

            // Sony PlayStation 5
            (new Regex(@"(?:\b|_|\[|\()(?:sony\s+playstation\s*5|playstation\s*5|ps5)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ps5"),

            // Sony PlayStation 4
            (new Regex(@"(?:\b|_|\[|\()(?:sony\s+playstation\s*4|playstation\s*4|ps4)(?:\b|_|\]|\))|\bCUSA-\d{5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ps4"),

            // Sony PlayStation 3
            (new Regex(@"(?:\b|_|\[|\()(?:sony\s+playstation\s*3|playstation\s*3|ps3)(?:\b|_|\]|\))|\b(?:BLUS|BLES|BCUS|BCES)-\d{5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ps3"),

            // Sony PlayStation 2
            (new Regex(@"(?:\b|_|\[|\()(?:sony\s+playstation\s*2|playstation\s*2|ps2|ps2dvd|ps2cd)(?:\b|_|\]|\))|\b(?:SLUS|SLES|SCES|SLPS)-2\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ps2"),

            // Sony PlayStation 1 / PSX
            (new Regex(@"(?:\b|_|\[|\()(?:sony\s+playstation|playstation\s*1|playstation|ps1|psx|psone)(?:\b|_|\]|\))|\b(?:SLUS|SLES|SCES|SCUS|SLPS)-0\d{4}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ps1"),

            // PlayStation Portable
            (new Regex(@"(?:\b|_|\[|\()(?:playstation\s+portable|sony\s+psp|psp|psx2psp|ps2psp)(?:\b|_|\]|\))|\b(?:ULUS|ULES|UCUS|UCES)-\d{5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "psp"),

            // PlayStation Vita
            (new Regex(@"(?:\b|_|\[|\()(?:playstation\s+vita|ps\s*vita|psvita|psv)(?:\b|_|\]|\))|\b(?:PCSB|PCSE|PCSG|PCSF)-\d{5}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "psvita"),

            // Xbox 360
            (new Regex(@"(?:\b|_|\[|\()(?:xbox\s*360|x360)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "xbox360"),

            // Xbox One
            (new Regex(@"(?:\b|_|\[|\()(?:xbox\s*one|xbo|xb1)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "xboxone"),

            // Xbox (Original)
            (new Regex(@"(?:\b|_|\[|\()(?:original\s+xbox|og\s+xbox|xbox|xboxdvd)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "xbox"),

            // Sega Genesis / Mega Drive
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s+genesis|mega\s*drive|megadrive)(?:\b|_|\]|\))|(?:^|[\s@\[\(\._\-])\s*genesis\s*(?:$|[\s\]\)\._\-])", RegexOptions.IgnoreCase | RegexOptions.Compiled), "genesis"),

            // Sega CD / Mega CD
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s*cd|mega\s*cd|segacd|megacd|scd)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "segacd"),

            // Sega 32X
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s*32x|super\s*32x|32x)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "32x"),

            // Sega Saturn
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s+saturn)(?:\b|_|\]|\))|(?:\[|\(|\.|_|-)\s*saturn\s*(?:\]|\)|\.|_|-)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "saturn"),

            // Sega Dreamcast
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s+dreamcast|dreamcast|dc)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "dreamcast"),

            // Sega Master System
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s+master\s+system|master\s+system|sms)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "sms"),

            // Sega Game Gear
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s+game\s+gear|game\s+gear|gg)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "gg"),

            // TurboGrafx-16 / PC Engine
            (new Regex(@"(?:\b|_|\[|\()(?:turbografx(?:-16|\s*16)?|pc\s*engine|tg16|pce)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "tg16"),

            // TurboGrafx-CD / PC Engine CD
            (new Regex(@"(?:\b|_|\[|\()(?:turbografx\s*cd|pc\s*engine\s*cd|tgcd|pcecd)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "tgcd"),

            // Neo Geo Pocket
            (new Regex(@"(?:\b|_|\[|\()(?:neo\s*geo\s*pocket(?:\s*color)?|ngpc|ngp)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ngpc"),

            // Neo Geo
            (new Regex(@"(?:\b|_|\[|\()(?:neo\s*geo|neogeo|aes|mvs)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "neogeo"),

            // Atari 2600 / 7800 / Lynx / Jaguar
            (new Regex(@"(?:\b|_|\[|\()(?:atari\s*2600|a2600)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "atari2600"),
            (new Regex(@"(?:\b|_|\[|\()(?:atari\s*7800|a7800)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "atari7800"),
            (new Regex(@"(?:\b|_|\[|\()(?:atari\s*lynx|lynx)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "lynx"),
            (new Regex(@"(?:\b|_|\[|\()(?:atari\s*jaguar\s*cd|jaguar\s*cd)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "jaguarcd"),
            (new Regex(@"(?:\b|_|\[|\()(?:atari\s*jaguar|jaguar)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "jaguar"),

            // WonderSwan
            (new Regex(@"(?:\b|_|\[|\()(?:wonderswan(?:\s*color)?|wsc|ws)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wonderswan"),
        };

        public static HashSet<string> DetectPlatforms(string text)
        {
            var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(text))
            {
                return detected;
            }

            foreach (var (pattern, platformSlug) in PlatformDetectionPatterns)
            {
                if (pattern.IsMatch(text))
                {
                    detected.Add(platformSlug);
                }
            }

            var ext = Path.GetExtension(text);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                var extSlug = GetPlatformByExtension(ext.ToLowerInvariant());
                if (!string.IsNullOrWhiteSpace(extSlug))
                {
                    detected.Add(extSlug);
                }
            }

            if (detected.Contains("wiiu"))
            {
                detected.Remove("wii");
            }

            if (detected.Contains("ps5") || detected.Contains("ps4") || detected.Contains("ps3") || detected.Contains("ps2"))
            {
                detected.Remove("ps1");
            }

            if (detected.Contains("gba") || detected.Contains("gbc"))
            {
                detected.Remove("gb");
            }

            if (detected.Contains("xbox360") || detected.Contains("xboxone"))
            {
                detected.Remove("xbox");
            }

            if (detected.Contains("tgcd"))
            {
                detected.Remove("tg16");
            }

            if (detected.Contains("jaguarcd"))
            {
                detected.Remove("jaguar");
            }

            if (detected.Contains("ngpc"))
            {
                detected.Remove("neogeo");
            }

            if (detected.Contains("32x") || detected.Contains("segacd"))
            {
                detected.Remove("genesis");
            }

            if (detected.Contains("snes"))
            {
                detected.Remove("nes");
            }

            if (detected.Contains("64dd"))
            {
                detected.Remove("n64");
            }

            if (detected.Contains("psp") && Regex.IsMatch(text, @"\b(?:psx2psp|ps2psp)\b", RegexOptions.IgnoreCase))
            {
                detected.Remove("ps1");
                detected.Remove("ps2");
            }

            return detected;
        }

        public static bool IsPlatformMatch(string detectedSlug, string targetSlug)
        {
            if (string.Equals(detectedSlug, targetSlug, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if ((targetSlug is "tg16" && detectedSlug is "pce" or "tgcd") ||
                (targetSlug is "pce" && detectedSlug is "tg16" or "pcecd") ||
                (targetSlug is "snes" && detectedSlug is "satellaview") ||
                (targetSlug is "nes" && detectedSlug is "fds") ||
                (targetSlug is "n64" && detectedSlug is "64dd"))
            {
                return true;
            }

            return false;
        }

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

            // Reject Virtual Console / VC WAD packages for non-VC platforms
            if (VirtualConsoleIndicatorRegex.IsMatch(filePathOrName) &&
                targetSlug is not ("wii" or "wiiu" or "3ds" or "switch"))
            {
                return false;
            }

            // Reject NKit images for non-GameCube/Wii platforms
            if (NkitIndicatorRegex.IsMatch(filePathOrName) &&
                targetSlug is not ("gamecube" or "wii"))
            {
                return false;
            }

            // Cartridge platforms cannot have disc/optical indicators in release title, format, or archive ID
            if (isCartridge)
            {
                if (OpticalDiscIndicatorRegex.IsMatch(filePathOrName))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(itemFormat) && OpticalDiscIndicatorRegex.IsMatch(itemFormat))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(archiveId) && OpticalDiscIndicatorRegex.IsMatch(archiveId))
                {
                    return false;
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

            // Check detected platforms from the file name, release title, item format, and archive ID
            var detected = DetectPlatforms(filePathOrName);
            if (!string.IsNullOrWhiteSpace(itemFormat))
            {
                foreach (var p in DetectPlatforms(itemFormat))
                {
                    detected.Add(p);
                }
            }

            if (!string.IsNullOrWhiteSpace(archiveId))
            {
                foreach (var p in DetectPlatforms(archiveId))
                {
                    detected.Add(p);
                }
            }

            // If an incorrect platform is detected or conflicts with target platform, reject. If no platform name is detected, allow it.
            if (detected.Count > 0)
            {
                var matchesTarget = detected.Any(p => IsPlatformMatch(p, targetSlug));
                if (!matchesTarget || detected.Any(p => !IsPlatformMatch(p, targetSlug)))
                {
                    return false;
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

        public static bool IsLocalFileCompatible(string filePathOrName, string platformSlug, string platformName = null)
        {
            if (string.IsNullOrWhiteSpace(platformSlug) || string.IsNullOrWhiteSpace(filePathOrName))
            {
                return true;
            }

            var targetSlug = NormalizePlatformSlug(platformSlug, platformName);
            var isCartridge = CartridgePlatforms.Contains(targetSlug);

            var ext = Path.GetExtension(filePathOrName);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                ext = ext.ToLowerInvariant();

                if (ext is ".exe" or ".msi" or ".bat" or ".cmd" or ".com" or ".appimage" or ".dmg")
                {
                    return false;
                }

                if (isCartridge && DiscExclusiveExtensions.Contains(ext))
                {
                    return false;
                }

                foreach (var (platform, extensions) in ExclusiveExtensionsByPlatform)
                {
                    if (!string.Equals(platform, targetSlug, StringComparison.OrdinalIgnoreCase) &&
                        extensions.Contains(ext))
                    {
                        if (ExclusiveExtensionsByPlatform.TryGetValue(targetSlug, out var targetExts) &&
                            targetExts.Contains(ext))
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            return true;
        }

        public static readonly HashSet<string> IgnoredRegionOrBucketFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "usa", "europe", "japan", "world", "asia", "australia", "germany", "france", "spain", "italy",
            "korea", "china", "brazil", "netherlands", "sweden", "norway", "denmark", "finland", "poland", "russia",
            "en", "ja", "fr", "de", "es", "it", "a-z", "0-9", "a-m", "n-z", "bios", "translations", "hacks",
            "unlicensed", "homebrew", "rom hacks"
        };

        private static readonly Regex SmdbCategoryBucketRegex = new(
            @"^\d+[\.\)]?\s+(game\s*boy|super\s+game\s*boy|emulation\s+extras|bad\s+dumps|hacks|homebrew|bios|translations|unlicensed|usa|europe|japan|world)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            if (name.Length == 1)
            {
                return true;
            }

            if (SmdbCategoryBucketRegex.IsMatch(name))
            {
                return true;
            }

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
                        if (item.Alias.Length <= 4 &&
                            !Regex.IsMatch(lowerNameDehyphenated, $@"(?<![a-z]){Regex.Escape(item.Alias)}(?![a-z])"))
                        {
                            continue;
                        }

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

            var fileExt = !string.IsNullOrWhiteSpace(nameOrSlugOrFilename)
                ? Path.GetExtension(nameOrSlugOrFilename)
                : string.Empty;

            var isKnownRomOrArchiveExt = !string.IsNullOrWhiteSpace(fileExt) &&
                                         (fileExt.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                                          fileExt.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
                                          GetPlatformByExtension(fileExt) != null ||
                                          DiscExclusiveExtensions.Contains(fileExt));

            var clean = !string.IsNullOrWhiteSpace(nameOrSlugOrFilename)
                ? (isKnownRomOrArchiveExt ? Path.GetFileNameWithoutExtension(nameOrSlugOrFilename).Trim() : nameOrSlugOrFilename.Trim())
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
