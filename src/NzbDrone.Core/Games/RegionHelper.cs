using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.Games
{
    public static class RegionHelper
    {
        public static readonly IReadOnlyDictionary<string, string> LegacySeasonFolderMappings =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Specials", "Unlicensed" },
                { "Season 0", "Unlicensed" },
                { "Season 00", "Unlicensed" },
                { "Season 1", "USA" },
                { "Season 01", "USA" },
                { "Season 2", "Europe" },
                { "Season 02", "Europe" },
                { "Season 3", "Japan" },
                { "Season 03", "Japan" },
                { "Season 4", "World" },
                { "Season 04", "World" },
                { "Season 5", "Homebrew" },
                { "Season 05", "Homebrew" },
                { "Season 6", "ROM Hacks" },
                { "Season 06", "ROM Hacks" }
            };

        private static readonly string[] KnownPokemonHacks = new[]
        {
            "Brown", "Prism", "Ash Gray", "AshGray", "Glazed", "Light Platinum", "Flora Sky",
            "Clover", "Gaia", "Radical Red", "Unbound", "Liquid Crystal", "Dark Rising",
            "Orange Islands", "Crystal Clear", "Polished Crystal", "Red++", "Red ++", "Blue++",
            "Blue ++", "PureRGB", "Shin Red", "Bronze", "Coral", "Silver Chronicles",
            "Gold and Silver 97", "Spaceworld 97", "Adventure Red", "Vega", "Procyon", "Deneb",
            "Snakewood", "Blasting Off", "Mirage", "Fuligin", "Quartz", "Victory Fire",
            "Resolute", "Mega Power", "Nameless", "Emerald Rogue", "Inclement Emerald",
            "Modern Emerald", "ROWE", "Odyssey", "Sovereign of the Skies", "Dreams", "Sors",
            "Crown", "Vintage White", "Blaze Black", "Volt White", "Sacred Gold", "Storm Silver",
            "Renegade Platinum", "Rising Sun", "Waning Moon", "Photonic Sun", "Prismatic Moon",
            "Luminescent Platinum", "Theta Emerald", "Altered Emerald", "Blazing Emerald",
            "Fire Gold", "Vietnamese Crystal", "Fool's Gold", "Rocket Edition", "Moemon"
        };

        private static readonly string[] KnownFranchiseHacks = new[]
        {
            "Return to Dinosaur Land", "Ancient Stone Tablets", "Parallel Worlds",
            "Goddess of Wisdom", "Super Zero Mission", "Crimson Echoes", "Flames of Eternity",
            "Brave New World", "Chorus of Mysteries", "In the Mushroom Kingdom",
            "Star Road", "Super Mario 64 Land", "Super Mario 3Mix", "Super Mario Land DX",
            "Super Mario Land 2 DX", "Conker's Pocket Tales (Color)", "Conker's Pocket Tales Color"
        };

        private static readonly string[] RomHackTokens = new[]
        {
            "(Hack", "[Hack", "Hack)", "Hack]", "Hack by", "[h]", "[h1]", "[h2]", "[h3]", "[h4]", "[h5]",
            "[T+", "[T-", "(T+", "(T-", "(Translation)", "[Translation]", "Translation Hack", "Fan Translation",
            "(Translated)", "[Translated]", "(Retranslation)", "[Retranslation]",
            "(Kaizo)", "[Kaizo]", "Kaizo Mario", "(Randomizer)", "[Randomizer]", "(Redux)", "[Redux]", "Redux Hack",
            "(Rebalance)", "[Rebalance]", "Rebalance Hack", "Romhack", "ROM Hack",
            "(Mod)", "[Mod]", "Modded", "(Improvement)", "[Improvement]", "(Restoration)", "[Restoration]",
            "(Uncensored)", "[Uncensored]", "(Colorized)", "[Colorized]", "(Colourized)", "[Colourized]",
            "(Color)", "[Color]", "(Color ", "Color Hack", "Color Edition",
            "(Enhancement)", "[Enhancement]", "(Enhancements)", "[Enhancements]", "(Enhanced)", "[Enhanced]",
            "(Fix)", "[Fix]", "(Fixes)", "[Fixes]", "(Bugfix)", "(Bug Fix)",
            "(Patch)", "[Patch]", "(Patches)", "[Patches]", "(Patched)",
            "(FastROM)", "[FastROM]", "(MSU-1)", "[MSU-1]", "(MSU1)",
            "(DX)", "[DX]"
        };

        private static readonly Regex GoodToolsOrTosecHackRegex = new(
            @"\[(?:tr\s+[^\]]+|h(?:\d+|I)?(?:\s+[^\]]*)?|t\d*(?:\s+[^\]]*)?|f\d*(?:\s+[^\]]*)?)\]|\bT[+-](?:Eng|En|Spa|Fre|Ger|Ita|Por|Rus|Chi|Kor)\b|\((?:english|french|spanish|german|italian|portuguese|russian|chinese|korean|polish|dutch)\s+translation\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PirateTagRegex = new(
            @"\[p\d*(?:\s+[^\]]*)?\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string GetRegionName(int regionNumber)
        {
            return regionNumber switch
            {
                0 => "Unlicensed",
                1 => "USA",
                2 => "Europe",
                3 => "Japan",
                4 => "World",
                5 => "Homebrew",
                6 => "ROM Hacks & Translations",
                7 => "Test & Utilities",
                8 => "Magazines & Books",
                99 => "BIOS",
                _ => $"Region {regionNumber}"
            };
        }

        public static int GetRegionNumber(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                return 1;
            }

            var r = region.Trim().ToLowerInvariant();
            return r switch
            {
                "unlicensed" => 0,
                "usa" or "us" or "u" or "north america" or "na" or "canada" or "brazil" => 1,
                "europe" or "eu" or "eur" or "e" or "pal" or "uk" or "united kingdom" or "australia" or "germany" or "france" or "spain" or "italy" or "netherlands" or "belgium" or "austria" or "switzerland" or "sweden" or "denmark" or "finland" or "norway" or "portugal" or "russia" or "poland" or "greece" or "scandinavia" => 2,
                "japan" or "jp" or "ja" or "jpn" or "j" or "ntsc-j" or "asia" or "korea" or "ntsc-k" or "china" or "taiwan" or "hong kong" => 3,
                "world" or "global" or "w" or "jue" or "uej" => 4,
                "homebrew" => 5,
                "rom hacks" or "rom hack" or "translation" or "translations" => 6,
                "test & utilities" or "test and utilities" or "test & utility" or "test and utility" or "utilities" or "utility" or "tools" or "diagnostic" or "diagnostics" => 7,
                "magazines & books" or "magazines and books" or "magazines" or "magazine" or "books" or "book" => 8,
                "bios" or "firmware" => 99,
                _ => 1
            };
        }

        public static bool IsRomHackOrTranslation(string title, string releaseTitle = null, IEnumerable<string> dumpTags = null, string verificationSource = null)
        {
            if (!string.IsNullOrWhiteSpace(verificationSource) && (
                verificationSource.Contains("ROM Hacks", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Hacks", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Enhancement", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Color Hack", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (dumpTags != null && dumpTags.Any(t =>
                t.StartsWith("T+", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("T-", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("h", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("t", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var texts = new[] { title, releaseTitle };
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (RomHackTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
                    GoodToolsOrTosecHackRegex.IsMatch(text))
                {
                    return true;
                }

                if (text.Contains("/Hacks/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Hacks\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Translations/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Translations\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Translations & Hacks/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Translations & Hacks\\", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var isPokemon = text.Contains("Pokemon", StringComparison.OrdinalIgnoreCase) ||
                                text.Contains("Pokémon", StringComparison.OrdinalIgnoreCase) ||
                                text.Contains("Pocket Monsters", StringComparison.OrdinalIgnoreCase);

                if (isPokemon && KnownPokemonHacks.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (KnownFranchiseHacks.Any(h => text.Contains(h, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Regex TestOrUtilityRegex = new Regex(
            @"\((?:Test(?:\s*\d+)?|Sprite\s+Test|Sound\s+Test|YM\s+Sound\s+Test|Ice\s+Stress\s+Test|Font\s+Test|POKEY\s+Utility|Service\s+Test|Utility|Utilities|Diagnostic|Diagnostics)\)|\[(?:Test|Utility|Utilities|Diagnostic)\]|240p\s+(?:Test\s+Suite|Suite|MegaCD)|240p[_-]|\b(?:Font\s+Test|Font\s+Check|Utility\s+Cart(?:ridge)?|Diagnostic\s+Test|Diagnostic\s+Cartridge|Diagnostic\s+Program|Diagnostic\s+Dis[ck]|Service\s+Test|Service\s+Cartridge|Service\s+Program|Service\s+Dis[ck]|Test\s+Cartridge|Test\s+Program|Test\s+Suite|Test\s+ROM|Test\s+Dis[ck]|Check\s+Program|Development\s+Card|Controller\s+Test|Sound\s+Tester|Sound\s+Test|Audio\s+Test|Video\s+Test|Display\s+Test|Interlace\s+Test|DMA\s+Test|(?<!Photographic\s+)Memory\s+Test(?!\s+with\b)|Hardware\s+Test|System\s+Test|Port\s+Test|Burn[- ]In\s+Test|Aging\s+Test|Palette\s+Tool\s+Test|Sprite\s+Engine\s+Test|Graphics\s+Modes\s+Test|Randomization\s+Test|Stress\s+Test|Charge\s+Check|Save\s+Dev\s+Test|Paddle\s+Test|Keypad\s+Delay\s+Test|POKEY\s+Test|AtariVox(?:\s+Speech)?\s+Test|Color\s+Bar|Action\s+Replay(?:\s+MAX)?|Pro\s+Action\s+Replay|GameShark|Game\s+Shark|Game\s+Genie|Code\s*Breaker(?!\s*(?:Puzzle|Game)\b)|Xploder|Xterminator|Gold\s+Finger|Swap\s+Magic|FreeLoader|Save\s+Manager|Memory\s+Card\s+Manager|Backup\s+Utility|Format\s+Utility|Transfer\s+Utility|Dis[ck]\s+Utility|System\s+Utility|Setup\s+Utility|Network\s+Access\s+Disc|Network\s+Startup\s+Disc|Broadband\s+Navigator)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsTestOrUtility(string title, string releaseTitle = null, IEnumerable<string> dumpTags = null, string verificationSource = null)
        {
            if (!string.IsNullOrWhiteSpace(verificationSource) && (
                verificationSource.Contains("Test & Utilities", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Test and Utilities", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Tools & Service", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Tools & Test", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Service Test", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Test Carts", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Utilities", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Diagnostic", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (dumpTags != null && dumpTags.Any(t =>
                t.Equals("Test", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Utility", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Utilities", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Diagnostic", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var texts = new[] { title, releaseTitle };
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.Contains("/Tools & Service", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Tools & Service", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Tools & Test", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Tools & Test", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Test & Utilities/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Test & Utilities\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Utilities/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Utilities\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Diagnostics/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Diagnostics\\", StringComparison.OrdinalIgnoreCase) ||
                    TestOrUtilityRegex.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Regex MagazineOrBookRegex = new(
            @"\b(?:Magazine|Magazin|Issue\s*#?\s*\d+|Volume\s+\d+\s*-\s*Issue|(?:Magazine|CD-i|Issue).*Vol\.\s*\d+|Vol\.\s*\d+\s*[-:]\s*(?:Issue|Magazine)|CD-?Online|CD-?I\s+World|Titel-Neuheiten|News\s+and\s+Views|Encyclop(?:a?edia|edie)|Universallexikon|Lexikon|Dictionary|Woordenboek|Worterbuch|(?:World|Interactive|3D|Picture|Road|Historical)\s+Atlas|Beauty\s+Guide|Driving\s+Guide|Interactive\s+Storybook|Interactive\s+Book|Audio\s+Book|Interactive\s+Catalog(?:ue)?|Product\s+Preview|Demonstration\s+Disc|Demo\s+Disc|Title\s+Sampler|CD-i\s+Awards|Awards\s+-\s+International\s+CD-i|ICDIA\s+Summit|Accent\s+Health|POS\s+&\s+Training|Training\s+Disc|College\s+Search|Historia\s+de(?:l)?\s+|English\s+Interactive|Vom\s+Kloster\s+zum\s+Museum)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IsMagazineOrBook(string title, string releaseTitle = null, IEnumerable<string> dumpTags = null, string verificationSource = null)
        {
            if (!string.IsNullOrWhiteSpace(verificationSource) && (
                verificationSource.Contains("Magazines", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Books", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (dumpTags != null && dumpTags.Any(t =>
                t.Equals("Magazine", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Book", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var texts = new[] { title, releaseTitle };
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.Contains("/Magazines & Books/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Magazines & Books\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Magazines/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Magazines\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Books/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Books\\", StringComparison.OrdinalIgnoreCase) ||
                    MagazineOrBookRegex.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsHomebrewOrAftermarket(string title, string releaseTitle = null, IEnumerable<string> dumpTags = null, string verificationSource = null)
        {
            if (!string.IsNullOrWhiteSpace(verificationSource) && (
                verificationSource.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Aftermarket", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Public Domain", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var texts = new[] { title, releaseTitle };
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.Contains("Aftermarket", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("RetroUSB", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("8-Bit Xmas", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("8 BIT XMAS", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Byte-Off", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("NESDev", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(PD)", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[PD]", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Public Domain", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(GD)", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[GD]", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("GameDrive", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(HB)", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[HB]", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Homebrew/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Homebrew\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/4 Homebrew/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\4 Homebrew\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Aftermarket/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Aftermarket\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(Atari ST)", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(AMIGA)", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsUnlicensed(string title, string releaseTitle = null, IEnumerable<string> dumpTags = null, string verificationSource = null)
        {
            if (!string.IsNullOrWhiteSpace(verificationSource) && (
                verificationSource.Contains("Unlicensed", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Pirate", StringComparison.OrdinalIgnoreCase) ||
                verificationSource.Contains("Bootleg", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var texts = new[] { title, releaseTitle };
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.Contains("(Unl", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[Unl", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Unlicensed", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(Pirate", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[Pirate", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Pirate Cart", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Pirate Multicart", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Pirate/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Pirate\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(Bootleg", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[Bootleg", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Bootleg/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Bootleg\\", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Bootleg Cart", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Bootleg Multicart", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(Demake", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[Demake", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Unlicensed/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Unlicensed\\", StringComparison.OrdinalIgnoreCase) ||
                    PirateTagRegex.IsMatch(text))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsBios(string title, string releaseTitle = null, IEnumerable<string> dumpTags = null, string verificationSource = null)
        {
            if (!string.IsNullOrWhiteSpace(verificationSource) &&
                (verificationSource.Contains("BIOS", StringComparison.OrdinalIgnoreCase) ||
                 verificationSource.Contains("Firmware", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (dumpTags != null && dumpTags.Any(t =>
                t.Equals("BIOS", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Firmware", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var texts = new[] { title, releaseTitle };
            foreach (var text in texts)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(BIOS)", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("(Firmware)", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("[Firmware]", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Boot ROM", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("System BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Neo Geo BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("NeoGeo BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/BIOS/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\BIOS\\", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith("/BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith("\\BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("bios_", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("uni-bios", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("unibios", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith(".ipl", StringComparison.OrdinalIgnoreCase) ||
                    IsKnownNeoGeoBiosName(text) ||
                    text.Equals("disksys.rom", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("cd_bios.rom", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly HashSet<string> NeoGeoBiosNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "neogeo",
            "neogeo.zip",
            "neocdz.zip",
            "neocd.bin",
            "neocd_f.rom",
            "neocd_sf.rom",
            "neocd_t.rom",
            "neocd_st.rom",
            "neocd_z.rom",
            "neocd_sz.rom",
            "front-sp1.bin",
            "top-sp1.bin",
            "sp-s2.sp1",
            "sp-s3.sp1",
            "sp-e.sp1",
            "sp-j2.sp1",
            "sp-j3.rom",
            "sp-1v1_3db8c",
            "sp-1v1_3db8c.bin",
            "sp-45",
            "sp-45.sp1",
            "sp-u2",
            "sp-u2.sp1",
            "sp1-j3",
            "sp1-j3.bin",
            "sp1-j3.rom",
            "sp1-u3",
            "sp1-u3.bin",
            "sp1-u3.rom",
            "sp1-u4",
            "sp1-u4.bin",
            "sp1-u4.rom",
            "sp1.jipan.1024",
            "asia-s3",
            "asia-s3.rom",
            "vs-bios",
            "vs-bios.rom",
            "aes-bios",
            "aes-bios.bin",
            "japan-j3",
            "japan-j3.bin",
            "usa_2slt",
            "usa_2slt.bin",
            "000-lo",
            "000-lo.lo",
            "ng-lo",
            "ng-lo.rom",
            "ng-sfix",
            "ng-sfix.rom",
            "ng-sm1",
            "ng-sm1.rom",
            "sfix",
            "sfix.sfix",
            "sm1",
            "sm1.sm1",
            "srom",
            "srom.bin",
            "srom.rom",
            "neopen.bios"
        };

        private static bool IsKnownNeoGeoBiosName(string text)
        {
            var trimmed = text.Trim();
            var fileName = System.IO.Path.GetFileName(trimmed);
            var fileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(trimmed);
            return NeoGeoBiosNames.Contains(fileName) ||
                   NeoGeoBiosNames.Contains(fileNameNoExt) ||
                   NeoGeoBiosNames.Contains(trimmed);
        }
    }
}
