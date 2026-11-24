using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Archive;
using SharpCompress.Archives;

namespace NzbDrone.Core.Parser
{
    public static class PlatformCompatibilityValidator
    {
        private const int MaxDetectedCacheSize = 4096;

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

        private static readonly Regex PcNonConsoleRegex = new Regex(
            @"[\(\[]\s*(?:PC(?![\s._-]*(?:Engine|FX))\b[^\)\]]*|IBM[\s._-]*PC|Windows(?:\s+(?:3\.1|95|98|ME|2000|XP|Vista|7|8|10|11))?|Win(?:32|64|95|98|XP)|MS-?DOS|DOS|DOSBox|Mac(?:OS)?|OS\s*X|Linux|Steam|GOG|PC-?98|PC-?88|iOS|iPhone|iPad|Android|Roblox|OST|Soundtrack|FLAC|MP3|PDF|EPUB|CBR|CBZ|Strategy[\s._-]+Guide)\s*[\)\]]|\b(?:pc[\s._-]+(?:games?|images?|dis[ck]s?|cd(?:-?rom)?|dvd(?:-?rom)?|isos?|rips?|repacks?|versions?|ports?|releases?|editions?|install(?:er)?s?|setup|cracks?|patches?|compatibles?)|(?:for|on)[\s._-]+pc|ibm[\s._-]+pc|ms-?dos|dos[\s._-]+(?:images?|dis[ck]s?|versions?|ports?|games?)|dosbox|win3\.?1|win95|win98|win32|win64|windows[\s._-]+(?:3\.1|95|98|xp|vista|7|8|10|11|version|port|release|edition|installer|setup)|(?:macos|linux|android)[\s._-]+(?:version|port|release|edition|installer|setup|apk)|gog[\s._-]+(?:games?|installer|edition|rip)|steam[\s._-]+(?:rip|repack|edition)|apple\s*ii|commodore\s*64|zx\s*spectrum|amstrad\s+cpc|pc-98|pc-88|roblox|bloxcards?wiki|strategy[\s._-]+guide|official[\s._-]+soundtrack|original[\s._-]+soundtrack)\b|(?-i:\biOS\b)|softwarelibrary_msdos|softwarelibrary_win|softwarelibrary_mac|softwarelibrary_c64|softwarelibrary_amiga",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, string[]> PlatformAliases = PlatformCatalogService.PlatformAliases;

        private static readonly Regex PlatformTagRegex = new Regex(
            @"(?:\b|_|\[|\()(3DS|NDS|GBA|GBC|N64|SNES|NES|PS1|PS2|PS3|PSP|PSX|WIIU|WII|GAMECUBE|XBOX360|XBOX|VITA|SEGACD|SCD|MEGACD|32X|GENESIS|MEGADRIVE|SATURN|DREAMCAST|SMS|GG)(?:\b|_|\]|\))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly (Regex Pattern, string PlatformSlug)[] PlatformDetectionPatterns = new[]
        {
            // Nintendo Switch eShop (must precede Nintendo Switch)
            (new Regex(@"(?:\b|_|\[|\()(?:(?:nintendo\s+)?switch\s*e\s*shop|nsw\s*e\s*shop|e\s*shop\s*nsw|switch\s*\(?(?:eshop|digital)\)?)(?:\b|_|\]|\))|(?:e\s*shop.*\b(?:switch|nsw|nx)\b|\b(?:switch|nsw|nx)\b.*e\s*shop)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "switcheshop"),

            // Nintendo Switch
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+switch|nsw|nx)(?:\b|_|\]|\))|(?:\[|\()switch(?:\]|\))|\bHAC-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "switch"),

            // Nintendo Wii U eShop (must precede Nintendo Wii U)
            (new Regex(@"(?:\b|_|\[|\()(?:(?:nintendo\s+)?wii\s*u\s*(?:e\s*shop|digital)|wiiu\s*(?:e\s*shop|digital)|wii\s*u\s*\(?(?:eshop|digital)\)?)(?:\b|_|\]|\))|(?:e\s*shop.*\bwii\s*u\b|\bwii\s*u\b.*e\s*shop)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wiiueshop"),

            // Nintendo Wii U
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+wii\s*u|wii\s*u|wii-u|wiiu)(?:\b|_|\]|\))|\bWUP-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wiiu"),

            // Nintendo WiiWare (must precede Nintendo Wii)
            (new Regex(@"(?:\b|_|\[|\()(?:(?:nintendo\s+)?wii\s*ware|wii\s*(?:e\s*shop|shop\s*channel|digital)|wii\s*\(?(?:wiiware|virtual\s+console)\)?)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "wiiware"),

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
            (new Regex(@"(?:\b|_|\[|\()(?:game\s*boy|gameboy|dmg)(?:\b|_|\]|\))|(?<![0-9]\s*)(?:\b|_|\[|\()gb(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "gb"),

            // Nintendo 3DS eShop (must precede Nintendo 3DS)
            (new Regex(@"(?:\b|_|\[|\()(?:(?:nintendo\s+)?(?:new\s+)?3ds\s*(?:e\s*shop|digital)|3ds\s*\(?(?:eshop|digital)\)?)(?:\b|_|\]|\))|(?:e\s*shop.*\b3ds\b|\b3ds\b.*e\s*shop)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "3dseshop"),

            // Nintendo 3DS
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+3ds|3ds|n3ds)(?:\b|_|\]|\))|\bCTR-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "3ds"),

            // Nintendo DSiWare / DSi Shop (must precede Nintendo DS)
            (new Regex(@"(?:\b|_|\[|\()(?:(?:nintendo\s+)?dsi\s*ware|(?:nintendo\s+)?dsi\s*shop|dsi\s*digital)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "dsiware"),

            // Nintendo DS
            (new Regex(@"(?:\b|_|\[|\()(?:nintendo\s+ds|nds|dsi)(?:\b|_|\]|\))|\bNTR-[A-Za-z0-9\-]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "nds"),

            // Virtual Boy
            (new Regex(@"(?:\b|_|\[|\()(?:virtual\s*boy|flashboy\+?|vb)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "vb"),

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
            (new Regex(@"(?:\b|_|\[|\()(?:xbox\s*360|x360|xbla|xbox\s+live\s+arcade)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "xbox360"),

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
            (new Regex(@"(?:\b|_|\[|\()(?:sega\s+dreamcast|dreamcast)(?:\b|_|\]|\))|(?:\[|\()dc(?:\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "dreamcast"),

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

            // 3DO
            (new Regex(@"(?:\b|_|\[|\()(?:3do(?:\s+interactive\s+multiplayer)?)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "3do"),

            // Atari 5200
            (new Regex(@"(?:\b|_|\[|\()(?:atari\s*5200|a5200)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "atari5200"),

            // ColecoVision
            (new Regex(@"(?:\b|_|\[|\()(?:colecovision|coleco)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "colecovision"),

            // Mattel Intellivision
            (new Regex(@"(?:\b|_|\[|\()(?:intellivision|intv)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "intellivision"),

            // GCE Vectrex
            (new Regex(@"(?:\b|_|\[|\()(?:vectrex)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "vectrex"),

            // NEC PC-FX
            (new Regex(@"(?:\b|_|\[|\()(?:pc-?fx)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "pcfx"),

            // Nintendo Pokemon Mini
            (new Regex(@"(?:\b|_|\[|\()(?:pok[eé]mon\s*mini|pokemini)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "pokemonmini"),

            // Magnavox Odyssey 2 / Videopac
            (new Regex(@"(?:\b|_|\[|\()(?:odyssey\s*2|odyssey2|videopac)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "odyssey2"),

            // Fairchild Channel F
            (new Regex(@"(?:\b|_|\[|\()(?:channel\s*f|channelf)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "channelf"),

            // Philips CD-i
            (new Regex(@"(?:\b|_|\[|\()(?:philips\s*cd-?i|cd-?i)(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled), "cdi"),
        };

        private static readonly Regex EshopTagRegex = new(@"(?:\b|_|\[|\()e\s*shop(?:\b|_|\]|\))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PsxToPspTagRegex = new(@"\b(?:psx2psp|ps2psp)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, string[]> DetectedPlatformsCache = new(StringComparer.Ordinal);

        public static HashSet<string> DetectPlatforms(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (DetectedPlatformsCache.TryGetValue(text, out var cached))
            {
                return new HashSet<string>(cached, StringComparer.OrdinalIgnoreCase);
            }

            var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            if (EshopTagRegex.IsMatch(text))
            {
                if (detected.Contains("switch"))
                {
                    detected.Add("switcheshop");
                }

                if (detected.Contains("wiiu"))
                {
                    detected.Add("wiiueshop");
                }

                if (detected.Contains("3ds"))
                {
                    detected.Add("3dseshop");
                }
            }

            if (detected.Contains("switcheshop"))
            {
                detected.Remove("switch");
            }

            if (detected.Contains("wiiueshop"))
            {
                detected.Remove("wiiu");
                detected.Remove("wii");
            }

            if (detected.Contains("wiiware"))
            {
                detected.Remove("wii");
            }

            if (detected.Contains("3dseshop"))
            {
                detected.Remove("3ds");
            }

            if (detected.Contains("dsiware"))
            {
                detected.Remove("nds");
            }

            if (detected.Contains("wiiu"))
            {
                detected.Remove("wii");
            }

            if (detected.Contains("ps5") || detected.Contains("ps4") || detected.Contains("ps3") || detected.Contains("ps2") || detected.Contains("psp") || detected.Contains("psvita"))
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

            if (detected.Contains("fds"))
            {
                detected.Remove("nes");
            }

            if (detected.Contains("64dd"))
            {
                detected.Remove("n64");
            }

            if (detected.Contains("32x") || detected.Contains("segacd"))
            {
                detected.Remove("genesis");
            }

            if (detected.Contains("snes"))
            {
                detected.Remove("nes");
            }

            if (detected.Contains("psp") && PsxToPspTagRegex.IsMatch(text))
            {
                detected.Remove("ps1");
                detected.Remove("ps2");
            }

            if (DetectedPlatformsCache.Count >= MaxDetectedCacheSize)
            {
                DetectedPlatformsCache.Clear();
            }

            DetectedPlatformsCache[text] = detected.Count == 0 ? Array.Empty<string>() : detected.ToArray();
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
                (targetSlug is "n64" && detectedSlug is "64dd") ||
                (targetSlug is "wiiware" && detectedSlug is "wii") ||
                (targetSlug is "wiiueshop" && detectedSlug is "wiiu") ||
                (targetSlug is "dsiware" && detectedSlug is "nds") ||
                (targetSlug is "3dseshop" && detectedSlug is "3ds") ||
                (targetSlug is "switcheshop" && detectedSlug is "switch"))
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

            if (!IsLocalFileCompatible(filePathOrName, platformSlug, platformName))
            {
                return false;
            }

            var targetSlug = NormalizePlatformSlug(platformSlug, platformName);
            var isCartridge = CartridgePlatforms.Contains(targetSlug);

            // Reject Virtual Console / VC WAD packages for non-VC platforms
            if (VirtualConsoleIndicatorRegex.IsMatch(filePathOrName) &&
                targetSlug is not ("wii" or "wiiware" or "wiiu" or "wiiueshop" or "3ds" or "3dseshop" or "switch" or "switcheshop"))
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
                var formatPlatforms = DetectPlatforms(itemFormat);
                if (formatPlatforms.Count > 0 && !formatPlatforms.Any(p => IsPlatformMatch(p, targetSlug)))
                {
                    return false;
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

                var archivePlatforms = DetectPlatforms(archiveId);
                if (archivePlatforms.Count > 0 && !archivePlatforms.Any(p => IsPlatformMatch(p, targetSlug)))
                {
                    return false;
                }
            }

            // Check directory / folder clues and filename separately in filePathOrName
            var normalizedPath = filePathOrName.Replace('\\', '/');
            var dirParts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (dirParts.Length > 1)
            {
                // Inspect directory segments (excluding the filename at the end)
                for (var i = 0; i < dirParts.Length - 1; i++)
                {
                    var dir = dirParts[i].Trim();
                    if (string.IsNullOrWhiteSpace(dir) || IsRegionOrBucketFolder(dir))
                    {
                        continue;
                    }

                    var dirPlatforms = DetectPlatforms(dir);
                    if (dirPlatforms.Count == 0)
                    {
                        foreach (var (platform, aliases) in PlatformAliases)
                        {
                            if (aliases.Any(a => string.Equals(dir, a, StringComparison.OrdinalIgnoreCase)))
                            {
                                dirPlatforms.Add(platform);
                            }
                        }
                    }

                    if (dirPlatforms.Count > 0 && !dirPlatforms.Any(p => IsPlatformMatch(p, targetSlug)))
                    {
                        return false;
                    }
                }
            }

            var fileNamePart = dirParts.Length > 0 ? dirParts[^1] : filePathOrName;
            var filePlatforms = DetectPlatforms(fileNamePart);
            if (filePlatforms.Count > 0)
            {
                var matchesTarget = filePlatforms.Any(p => IsPlatformMatch(p, targetSlug));
                if (!matchesTarget || filePlatforms.Any(p => !IsPlatformMatch(p, targetSlug)))
                {
                    return false;
                }
            }

            var lowerPathOrName = filePathOrName.ToLowerInvariant();
            if (lowerPathOrName.Contains("gog-installer") ||
                lowerPathOrName.Contains("official-gog") ||
                lowerPathOrName.Contains("official gog") ||
                lowerPathOrName.Contains("gog.com") ||
                IsPcOrNonConsoleRelease(filePathOrName) ||
                IsPcOrNonConsoleRelease(archiveId))
            {
                return false;
            }

            return true;
        }

        public static bool IsPcOrNonConsoleRelease(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && PcNonConsoleRegex.IsMatch(text);
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

                if ((ext == ".zip" || ext == ".7z" || ext == ".rar") && File.Exists(filePathOrName))
                {
                    try
                    {
                        if (ext == ".zip")
                        {
                            using var archive = ZipFile.OpenRead(filePathOrName);
                            foreach (var entry in archive.Entries)
                            {
                                if (ArchiveRepackService.IsJunkEntry(entry.FullName, entry.Name))
                                {
                                    continue;
                                }

                                var innerExt = Path.GetExtension(entry.Name);
                                if (string.IsNullOrWhiteSpace(innerExt) ||
                                    ArchiveRepackService.IsArchiveExtension(innerExt))
                                {
                                    continue;
                                }

                                if (!IsExtensionCompatibleWithPlatform(innerExt, targetSlug, isCartridge))
                                {
                                    return false;
                                }
                            }
                        }
                        else
                        {
                            using var archive = ArchiveFactory.OpenArchive(filePathOrName);
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.IsDirectory || string.IsNullOrWhiteSpace(entry.Key))
                                {
                                    continue;
                                }

                                var entryKey = entry.Key.Replace('\\', '/');
                                var entryName = Path.GetFileName(entryKey);
                                if (ArchiveRepackService.IsJunkEntry(entryKey, entryName))
                                {
                                    continue;
                                }

                                var innerExt = Path.GetExtension(entryName);
                                if (string.IsNullOrWhiteSpace(innerExt) ||
                                    ArchiveRepackService.IsArchiveExtension(innerExt))
                                {
                                    continue;
                                }

                                if (!IsExtensionCompatibleWithPlatform(innerExt, targetSlug, isCartridge))
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore unreadable/non-archive files and fall back to outer extension check
                    }
                }
                else if (!IsExtensionCompatibleWithPlatform(ext, targetSlug, isCartridge))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsExtensionCompatibleWithPlatform(string ext, string targetSlug, bool isCartridge)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                return true;
            }

            ext = ext.ToLowerInvariant();

            if (ext is ".exe" or ".msi" or ".bat" or ".cmd" or ".com" or ".appimage" or ".dmg" or ".apk" or ".ipa" or ".rbxl" or ".rbxm")
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
            if (ext == ".fds")
            {
                return "fds";
            }

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

        private static readonly List<(string Slug, string Alias, Regex BoundaryRegex)> AllAliasesOrdered = PlatformAliases
            .SelectMany(p => p.Value.Select(a => (
                Slug: p.Key,
                Alias: a,
                BoundaryRegex: a.Length <= 4
                    ? new Regex($@"(?<![a-z]){Regex.Escape(a)}(?![a-z])", RegexOptions.Compiled)
                    : null)))
            .OrderByDescending(x => x.Alias.Length)
            .ToList();

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

                foreach (var item in AllAliasesOrdered)
                {
                    if (item.Alias.Length >= 3 && (lowerName.Contains(item.Alias) || lowerNameDehyphenated.Contains(item.Alias)))
                    {
                        if (item.BoundaryRegex != null && !item.BoundaryRegex.IsMatch(lowerNameDehyphenated))
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
            @"\b(season\s*\d{1,2}|s\d{1,2}\s*(?:complete|pack)|complete\s*season)\b",
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

        public static string ExtractArchiveId(string infoUrl)
        {
            if (!string.IsNullOrWhiteSpace(infoUrl))
            {
                var detailsIdx = infoUrl.IndexOf("/details/", StringComparison.OrdinalIgnoreCase);
                if (detailsIdx >= 0)
                {
                    var sub = infoUrl[(detailsIdx + 9)..];
                    var endIdx = sub.IndexOfAny(new[] { '/', '?', '#' });
                    return endIdx >= 0 ? sub[..endIdx] : sub;
                }
            }

            return null;
        }

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
