using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Games
{
    public interface IPlatformCatalogService
    {
        List<PlatformDefinition> GetAll();
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

        public List<PlatformDefinition> GetAll()
        {
            return Platforms.ToList();
        }
    }
}
