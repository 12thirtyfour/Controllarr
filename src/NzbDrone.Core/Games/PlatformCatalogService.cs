using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Games
{
    public interface IPlatformCatalogService
    {
        List<PlatformDefinition> GetAll();
        PlatformDefinition FindBySlug(string slug);
    }

    public class PlatformCatalogService : IPlatformCatalogService
    {
        private static readonly List<PlatformDefinition> Platforms = new List<PlatformDefinition>
        {
            new PlatformDefinition
            {
                Name = "Nintendo Entertainment System",
                Slug = "nes",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "NES" },
                    { "EU", "NES" },
                    { "JP", "Famicom" }
                },
                RomFileExtensions = new List<string> { ".nes", ".unf" }
            },
            new PlatformDefinition
            {
                Name = "Famicom Disk System",
                Slug = "fds",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Famicom Disk System" },
                    { "EU", "Famicom Disk System" },
                    { "JP", "Famicom Disk System" }
                },
                RomFileExtensions = new List<string> { ".fds" }
            },
            new PlatformDefinition
            {
                Name = "Super Nintendo Entertainment System",
                Slug = "snes",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Super Nintendo" },
                    { "EU", "Super Nintendo" },
                    { "JP", "Super Famicom" }
                },
                RomFileExtensions = new List<string> { ".sfc", ".smc" }
            },
            new PlatformDefinition
            {
                Name = "Satellaview",
                Slug = "satellaview",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Satellaview" },
                    { "EU", "Satellaview" },
                    { "JP", "Satellaview" }
                },
                RomFileExtensions = new List<string> { ".bs" }
            },
            new PlatformDefinition
            {
                Name = "Nintendo 64",
                Slug = "n64",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Nintendo 64" },
                    { "EU", "Nintendo 64" },
                    { "JP", "Nintendo 64" }
                },
                RomFileExtensions = new List<string> { ".z64", ".n64", ".v64" }
            },
            new PlatformDefinition
            {
                Name = "64DD",
                Slug = "64dd",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "64DD" },
                    { "EU", "64DD" },
                    { "JP", "64DD" }
                },
                RomFileExtensions = new List<string> { ".ndd" }
            },
            new PlatformDefinition
            {
                Name = "GameCube",
                Slug = "gamecube",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "GameCube" },
                    { "EU", "GameCube" },
                    { "JP", "GameCube" }
                },
                RomFileExtensions = new List<string> { ".iso", ".ciso", ".gcm", ".rvz" }
            },
            new PlatformDefinition
            {
                Name = "Wii",
                Slug = "wii",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Wii" },
                    { "EU", "Wii" },
                    { "JP", "Wii" }
                },
                RomFileExtensions = new List<string> { ".iso", ".wbfs", ".rvz" }
            },
            new PlatformDefinition
            {
                Name = "Game Boy",
                Slug = "gb",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game Boy" },
                    { "EU", "Game Boy" },
                    { "JP", "Game Boy" }
                },
                RomFileExtensions = new List<string> { ".gb" }
            },
            new PlatformDefinition
            {
                Name = "Game Boy Color",
                Slug = "gbc",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game Boy Color" },
                    { "EU", "Game Boy Color" },
                    { "JP", "Game Boy Color" }
                },
                RomFileExtensions = new List<string> { ".gbc" }
            },
            new PlatformDefinition
            {
                Name = "Game Boy Advance",
                Slug = "gba",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game Boy Advance" },
                    { "EU", "Game Boy Advance" },
                    { "JP", "Game Boy Advance" }
                },
                RomFileExtensions = new List<string> { ".gba" }
            },
            new PlatformDefinition
            {
                Name = "Nintendo DS",
                Slug = "nds",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Nintendo DS" },
                    { "EU", "Nintendo DS" },
                    { "JP", "Nintendo DS" }
                },
                RomFileExtensions = new List<string> { ".nds" }
            },
            new PlatformDefinition
            {
                Name = "Nintendo 3DS",
                Slug = "3ds",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Nintendo 3DS" },
                    { "EU", "Nintendo 3DS" },
                    { "JP", "Nintendo 3DS" }
                },
                RomFileExtensions = new List<string> { ".3ds", ".cia" }
            },
            new PlatformDefinition
            {
                Name = "Nintendo Switch",
                Slug = "switch",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Nintendo Switch" },
                    { "EU", "Nintendo Switch" },
                    { "JP", "Nintendo Switch" }
                },
                RomFileExtensions = new List<string> { ".nsp", ".xci", ".nsz" }
            },
            new PlatformDefinition
            {
                Name = "Sega Genesis",
                Slug = "genesis",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Genesis" },
                    { "EU", "Mega Drive" },
                    { "JP", "Mega Drive" }
                },
                RomFileExtensions = new List<string> { ".md", ".gen", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Sega CD",
                Slug = "segacd",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Sega CD" },
                    { "EU", "Mega-CD" },
                    { "JP", "Mega-CD" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd" }
            },
            new PlatformDefinition
            {
                Name = "Sega 32X",
                Slug = "32x",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Sega 32X" },
                    { "EU", "Sega 32X" },
                    { "JP", "Sega 32X" }
                },
                RomFileExtensions = new List<string> { ".32x" }
            },
            new PlatformDefinition
            {
                Name = "Sega Saturn",
                Slug = "saturn",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Saturn" },
                    { "EU", "Saturn" },
                    { "JP", "Saturn" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd" }
            },
            new PlatformDefinition
            {
                Name = "Sega Dreamcast",
                Slug = "dreamcast",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Dreamcast" },
                    { "EU", "Dreamcast" },
                    { "JP", "Dreamcast" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd", ".gdi" }
            },
            new PlatformDefinition
            {
                Name = "TurboGrafx-16",
                Slug = "tg16",
                Manufacturer = "NEC",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "TurboGrafx-16" },
                    { "EU", "TurboGrafx" },
                    { "JP", "PC Engine" }
                },
                RomFileExtensions = new List<string> { ".pce" }
            },
            new PlatformDefinition
            {
                Name = "TurboGrafx-CD",
                Slug = "tgcd",
                Manufacturer = "NEC",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "TurboGrafx-CD" },
                    { "EU", "TurboGrafx-CD" },
                    { "JP", "PC Engine CD" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd" }
            },
            new PlatformDefinition
            {
                Name = "PlayStation",
                Slug = "ps1",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PlayStation" },
                    { "EU", "PlayStation" },
                    { "JP", "PlayStation" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd", ".pbp" }
            },
            new PlatformDefinition
            {
                Name = "PlayStation 2",
                Slug = "ps2",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PlayStation 2" },
                    { "EU", "PlayStation 2" },
                    { "JP", "PlayStation 2" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd" }
            },
            new PlatformDefinition
            {
                Name = "PlayStation Portable",
                Slug = "psp",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PlayStation Portable" },
                    { "EU", "PlayStation Portable" },
                    { "JP", "PlayStation Portable" }
                },
                RomFileExtensions = new List<string> { ".iso", ".cso" }
            },
            new PlatformDefinition
            {
                Name = "Jaguar CD",
                Slug = "jaguarcd",
                Manufacturer = "Atari",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Jaguar CD" },
                    { "EU", "Jaguar CD" },
                    { "JP", "Jaguar CD" }
                },
                RomFileExtensions = new List<string> { ".cue", ".bin", ".iso", ".chd" }
            },
            new PlatformDefinition
            {
                Name = "Wii U",
                Slug = "wiiu",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Wii U" },
                    { "EU", "Wii U" },
                    { "JP", "Wii U" }
                },
                RomFileExtensions = new List<string> { ".wua", ".wud", ".wux", ".rpx" }
            },
            new PlatformDefinition
            {
                Name = "Virtual Boy",
                Slug = "vb",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Virtual Boy" },
                    { "EU", "Virtual Boy" },
                    { "JP", "Virtual Boy" }
                },
                RomFileExtensions = new List<string> { ".vb" }
            },
            new PlatformDefinition
            {
                Name = "Sega Master System",
                Slug = "sms",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Master System" },
                    { "EU", "Master System" },
                    { "JP", "SG-1000 Mark III" }
                },
                RomFileExtensions = new List<string> { ".sms" }
            },
            new PlatformDefinition
            {
                Name = "Sega Game Gear",
                Slug = "gg",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game Gear" },
                    { "EU", "Game Gear" },
                    { "JP", "Game Gear" }
                },
                RomFileExtensions = new List<string> { ".gg" }
            },
            new PlatformDefinition
            {
                Name = "PlayStation 3",
                Slug = "ps3",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PlayStation 3" },
                    { "EU", "PlayStation 3" },
                    { "JP", "PlayStation 3" }
                },
                RomFileExtensions = new List<string> { ".iso", ".pkg" }
            },
            new PlatformDefinition
            {
                Name = "PlayStation Vita",
                Slug = "psvita",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PlayStation Vita" },
                    { "EU", "PlayStation Vita" },
                    { "JP", "PlayStation Vita" }
                },
                RomFileExtensions = new List<string> { ".vpk" }
            },
            new PlatformDefinition
            {
                Name = "Xbox",
                Slug = "xbox",
                Manufacturer = "Microsoft",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Xbox" },
                    { "EU", "Xbox" },
                    { "JP", "Xbox" }
                },
                RomFileExtensions = new List<string> { ".iso", ".xiso" }
            },
            new PlatformDefinition
            {
                Name = "Xbox 360",
                Slug = "xbox360",
                Manufacturer = "Microsoft",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Xbox 360" },
                    { "EU", "Xbox 360" },
                    { "JP", "Xbox 360" }
                },
                RomFileExtensions = new List<string> { ".iso", ".xex", ".god" }
            },
            new PlatformDefinition
            {
                Name = "Atari 2600",
                Slug = "atari2600",
                Manufacturer = "Atari",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Atari 2600" },
                    { "EU", "Atari 2600" },
                    { "JP", "Atari 2600" }
                },
                RomFileExtensions = new List<string> { ".a26", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Atari 7800",
                Slug = "atari7800",
                Manufacturer = "Atari",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Atari 7800" },
                    { "EU", "Atari 7800" },
                    { "JP", "Atari 7800" }
                },
                RomFileExtensions = new List<string> { ".a78", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Atari Lynx",
                Slug = "lynx",
                Manufacturer = "Atari",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Atari Lynx" },
                    { "EU", "Atari Lynx" },
                    { "JP", "Atari Lynx" }
                },
                RomFileExtensions = new List<string> { ".lnx" }
            },
            new PlatformDefinition
            {
                Name = "Atari Jaguar",
                Slug = "jaguar",
                Manufacturer = "Atari",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Atari Jaguar" },
                    { "EU", "Atari Jaguar" },
                    { "JP", "Atari Jaguar" }
                },
                RomFileExtensions = new List<string> { ".j64", ".jag" }
            },
            new PlatformDefinition
            {
                Name = "Neo Geo",
                Slug = "neogeo",
                Manufacturer = "SNK",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Neo Geo" },
                    { "EU", "Neo Geo" },
                    { "JP", "Neo Geo" }
                },
                RomFileExtensions = new List<string> { ".zip", ".7z", ".neo" }
            },
            new PlatformDefinition
            {
                Name = "Neo Geo Pocket Color",
                Slug = "ngpc",
                Manufacturer = "SNK",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Neo Geo Pocket Color" },
                    { "EU", "Neo Geo Pocket Color" },
                    { "JP", "Neo Geo Pocket Color" }
                },
                RomFileExtensions = new List<string> { ".ngc", ".ngp" }
            },
            new PlatformDefinition
            {
                Name = "WonderSwan Color",
                Slug = "wonderswan",
                Manufacturer = "Bandai",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "WonderSwan Color" },
                    { "EU", "WonderSwan Color" },
                    { "JP", "WonderSwan Color" }
                },
                RomFileExtensions = new List<string> { ".wsc", ".ws" }
            },
            new PlatformDefinition
            {
                Name = "Arcade",
                Slug = "arcade",
                Manufacturer = "Arcade",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Arcade" },
                    { "EU", "Arcade" },
                    { "JP", "Arcade" }
                },
                RomFileExtensions = new List<string> { ".zip", ".7z", ".chd" }
            }
        };

        public static readonly HashSet<string> KnownPlatformSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "nes", "fds", "snes", "satellaview", "n64", "64dd", "gamecube", "wii", "wiiu",
            "gb", "gbc", "gba", "nds", "3ds", "switch",
            "genesis", "segacd", "32x", "saturn", "dreamcast", "sms", "gg",
            "tg16", "tgcd",
            "ps1", "ps2", "psp", "ps3", "psvita",
            "xbox", "xbox360",
            "atari2600", "atari7800", "lynx", "jaguar", "jaguarcd",
            "neogeo", "ngpc", "wonderswan", "vb", "arcade"
        };

        public static readonly HashSet<string> CartridgePlatforms = new(StringComparer.OrdinalIgnoreCase)
        {
            "nes", "fds", "snes", "satellaview", "n64", "64dd", "gb", "gbc", "gba", "nds", "3ds", "switch",
            "genesis", "32x", "sms", "gg", "tg16", "pce", "atari2600", "atari7800", "lynx",
            "jaguar", "neogeo", "ngpc", "wonderswan", "vb", "psvita"
        };

        public static readonly Dictionary<string, HashSet<string>> ExclusiveExtensionsByPlatform = new(StringComparer.OrdinalIgnoreCase)
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

        public static readonly Dictionary<string, string[]> PlatformAliases = new(StringComparer.OrdinalIgnoreCase)
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

        public static readonly Dictionary<string, string> CanonicalPlatformNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = "Nintendo Entertainment System",
            ["fds"] = "Famicom Disk System",
            ["snes"] = "Super Nintendo Entertainment System",
            ["satellaview"] = "BS Satellaview",
            ["n64"] = "Nintendo 64",
            ["64dd"] = "Nintendo 64DD",
            ["gamecube"] = "Nintendo GameCube",
            ["wii"] = "Nintendo Wii",
            ["wiiu"] = "Nintendo Wii U",
            ["gb"] = "Nintendo Game Boy",
            ["gbc"] = "Nintendo Game Boy Color",
            ["gba"] = "Nintendo Game Boy Advance",
            ["nds"] = "Nintendo DS",
            ["3ds"] = "Nintendo 3DS",
            ["switch"] = "Nintendo Switch",
            ["genesis"] = "Sega Genesis",
            ["segacd"] = "Sega CD",
            ["32x"] = "Sega 32X",
            ["saturn"] = "Sega Saturn",
            ["dreamcast"] = "Sega Dreamcast",
            ["sms"] = "Sega Master System",
            ["gg"] = "Sega Game Gear",
            ["tg16"] = "NEC TurboGrafx-16",
            ["tgcd"] = "NEC TurboGrafx-CD",
            ["ps1"] = "Sony PlayStation",
            ["ps2"] = "Sony PlayStation 2",
            ["ps3"] = "Sony PlayStation 3",
            ["psp"] = "Sony PlayStation Portable",
            ["psvita"] = "Sony PlayStation Vita",
            ["xbox"] = "Microsoft Xbox",
            ["xbox360"] = "Microsoft Xbox 360",
            ["atari2600"] = "Atari 2600",
            ["atari7800"] = "Atari 7800",
            ["lynx"] = "Atari Lynx",
            ["jaguar"] = "Atari Jaguar",
            ["jaguarcd"] = "Atari Jaguar CD",
            ["neogeo"] = "SNK Neo Geo",
            ["ngpc"] = "SNK Neo Geo Pocket Color",
            ["wonderswan"] = "Bandai WonderSwan",
            ["vb"] = "Nintendo Virtual Boy"
        };

        public static readonly Dictionary<string, string> DefaultPlatformRawExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            { "SNES", ".sfc" },
            { "Super Nintendo Entertainment System", ".sfc" },
            { "NES", ".nes" },
            { "Nintendo Entertainment System", ".nes" },
            { "Game Boy", ".gb" },
            { "Game Boy Color", ".gbc" },
            { "Game Boy Advance", ".gba" },
            { "Genesis", ".md" },
            { "Sega Genesis", ".md" },
            { "Mega Drive", ".md" },
            { "Master System", ".sms" },
            { "Game Gear", ".gg" },
            { "Nintendo 64", ".z64" },
            { "Nintendo DS", ".nds" },
            { "TurboGrafx-16", ".pce" }
        };

        public List<PlatformDefinition> GetAll()
        {
            return Platforms.ToList();
        }

        public PlatformDefinition FindBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return Platforms.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
    }
}
