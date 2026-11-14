using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Games
{
    public static class RegionHelper
    {
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
            "[T+", "[T-", "(T+", "(T-", "Translation", "Translated", "Retranslation", "Fan Translation",
            "Kaizo", "Randomizer", "Redux", "Rebalance", "Romhack", "ROM Hack",
            "(Mod)", "[Mod]", "Modded", "(Improvement)", "[Improvement]", "(Restoration)", "[Restoration]",
            "(Uncensored)", "[Uncensored]", "(Colorized)", "[Colorized]", "(Colourized)", "[Colourized]",
            "(Color)", "[Color]", "(Color ", "Color Hack", "Color Edition",
            "(Enhancement)", "[Enhancement]", "(Enhancements)", "[Enhancements]", "(Enhanced)", "[Enhanced]",
            "(Fix)", "[Fix]", "(Fixes)", "[Fixes]", "(Bugfix)", "(Bug Fix)",
            "(Patch)", "[Patch]", "(Patches)", "[Patches]", "(Patched)",
            "(FastROM)", "[FastROM]", "(MSU-1)", "[MSU-1]", "(MSU1)",
            "(SGB Enhanced)", "[SGB Enhanced]",
            "(DX)", "[DX]"
        };

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
                "europe" or "eu" or "eur" or "e" or "pal" or "uk" or "united kingdom" or "australia" or "germany" or "france" or "spain" or "italy" or "netherlands" or "sweden" or "denmark" or "finland" or "norway" or "portugal" or "russia" or "poland" or "greece" or "scandinavia" => 2,
                "japan" or "jp" or "ja" or "jpn" or "j" or "ntsc-j" or "asia" or "korea" or "ntsc-k" or "china" or "taiwan" or "hong kong" => 3,
                "world" or "global" or "w" or "jue" or "uej" => 4,
                "homebrew" => 5,
                "rom hacks" or "rom hack" or "translation" or "translations" => 6,
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

                if (RomHackTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)))
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
                    text.Contains("240p Test Suite", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("240p Suite", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("240p_", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("240p-", StringComparison.OrdinalIgnoreCase) ||
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
                    text.Contains("\\Aftermarket\\", StringComparison.OrdinalIgnoreCase))
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
                    text.Contains("Pirate", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Bootleg", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("Demake", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("/Unlicensed/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\Unlicensed\\", StringComparison.OrdinalIgnoreCase))
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
                    text.Contains("/BIOS/", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("\\BIOS\\", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith("/BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith("\\BIOS", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("bios_", StringComparison.OrdinalIgnoreCase) ||
                    text.EndsWith(".ipl", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("disksys.rom", StringComparison.OrdinalIgnoreCase) ||
                    text.Equals("cd_bios.rom", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
