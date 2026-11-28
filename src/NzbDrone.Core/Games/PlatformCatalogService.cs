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
                RomFileExtensions = new List<string> { ".ngc" }
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
                RomFileExtensions = new List<string> { ".wsc" }
            },
            new PlatformDefinition
            {
                Name = "3DO Interactive Multiplayer",
                Slug = "3do",
                Manufacturer = "The 3DO Company",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "3DO" },
                    { "EU", "3DO" },
                    { "JP", "3DO" }
                },
                RomFileExtensions = new List<string> { ".chd", ".iso", ".cue", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Atari 5200",
                Slug = "atari5200",
                Manufacturer = "Atari",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Atari 5200" },
                    { "EU", "Atari 5200" },
                    { "JP", "Atari 5200" }
                },
                RomFileExtensions = new List<string> { ".a52", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "ColecoVision",
                Slug = "colecovision",
                Manufacturer = "Coleco",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "ColecoVision" },
                    { "EU", "ColecoVision" },
                    { "JP", "ColecoVision" }
                },
                RomFileExtensions = new List<string> { ".col", ".rom", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Mattel Intellivision",
                Slug = "intellivision",
                Manufacturer = "Mattel",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Intellivision" },
                    { "EU", "Intellivision" },
                    { "JP", "Intellivision" }
                },
                RomFileExtensions = new List<string> { ".int", ".rom", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "GCE Vectrex",
                Slug = "vectrex",
                Manufacturer = "GCE",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Vectrex" },
                    { "EU", "Vectrex" },
                    { "JP", "Vectrex" }
                },
                RomFileExtensions = new List<string> { ".vec", ".gam", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "NEC PC-FX",
                Slug = "pcfx",
                Manufacturer = "NEC",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PC-FX" },
                    { "EU", "PC-FX" },
                    { "JP", "PC-FX" }
                },
                RomFileExtensions = new List<string> { ".chd", ".cue", ".ccd", ".iso" }
            },
            new PlatformDefinition
            {
                Name = "Pokemon Mini",
                Slug = "pokemonmini",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Pokemon Mini" },
                    { "EU", "Pokemon Mini" },
                    { "JP", "Pokemon Mini" }
                },
                RomFileExtensions = new List<string> { ".min" }
            },
            new PlatformDefinition
            {
                Name = "Magnavox Odyssey 2",
                Slug = "odyssey2",
                Manufacturer = "Magnavox",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Odyssey 2" },
                    { "EU", "Videopac" },
                    { "JP", "Odyssey 2" }
                },
                RomFileExtensions = new List<string> { ".bin", ".o2" }
            },
            new PlatformDefinition
            {
                Name = "Fairchild Channel F",
                Slug = "channelf",
                Manufacturer = "Fairchild",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Channel F" },
                    { "EU", "Channel F" },
                    { "JP", "Channel F" }
                },
                RomFileExtensions = new List<string> { ".chf", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Philips CD-i",
                Slug = "cdi",
                Manufacturer = "Philips",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Philips CD-i" },
                    { "EU", "Philips CD-i" },
                    { "JP", "Philips CD-i" }
                },
                RomFileExtensions = new List<string> { ".chd", ".cue", ".iso", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "SNK Neo Geo CD",
                Slug = "neogeocd",
                Manufacturer = "SNK",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Neo Geo CD" },
                    { "EU", "Neo Geo CD" },
                    { "JP", "Neo Geo CD" }
                },
                RomFileExtensions = new List<string> { ".chd", ".cue", ".bin", ".iso" }
            },
            new PlatformDefinition
            {
                Name = "NEC PC Engine SuperGrafx",
                Slug = "supergrafx",
                Manufacturer = "NEC",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "SuperGrafx" },
                    { "EU", "SuperGrafx" },
                    { "JP", "PC Engine SuperGrafx" }
                },
                RomFileExtensions = new List<string> { ".sgx", ".pce" }
            },
            new PlatformDefinition
            {
                Name = "Bandai WonderSwan",
                Slug = "ws",
                Manufacturer = "Bandai",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "WonderSwan" },
                    { "EU", "WonderSwan" },
                    { "JP", "WonderSwan" }
                },
                RomFileExtensions = new List<string> { ".ws" }
            },
            new PlatformDefinition
            {
                Name = "SNK Neo Geo Pocket",
                Slug = "ngp",
                Manufacturer = "SNK",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Neo Geo Pocket" },
                    { "EU", "Neo Geo Pocket" },
                    { "JP", "Neo Geo Pocket" }
                },
                RomFileExtensions = new List<string> { ".ngp" }
            },
            new PlatformDefinition
            {
                Name = "Sega SG-1000",
                Slug = "sg1000",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "SG-1000" },
                    { "EU", "SG-1000" },
                    { "JP", "SG-1000" }
                },
                RomFileExtensions = new List<string> { ".sg", ".sf", ".sc", ".sf7", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Commodore Amiga CD32",
                Slug = "cd32",
                Manufacturer = "Commodore",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Amiga CD32" },
                    { "EU", "Amiga CD32" },
                    { "JP", "Amiga CD32" }
                },
                RomFileExtensions = new List<string> { ".chd", ".cue", ".bin", ".iso" }
            },
            new PlatformDefinition
            {
                Name = "Sega Pico",
                Slug = "pico",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Sega Pico" },
                    { "EU", "Sega Pico" },
                    { "JP", "Sega Pico" }
                },
                RomFileExtensions = new List<string> { ".md", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Sega Advanced Pico Beena",
                Slug = "beena",
                Manufacturer = "Sega",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Beena" },
                    { "EU", "Beena" },
                    { "JP", "Advanced Pico Beena" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Emerson Arcadia 2001",
                Slug = "arcadia2001",
                Manufacturer = "Emerson",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Arcadia 2001" },
                    { "EU", "Arcadia 2001" },
                    { "JP", "Arcadia 2001" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Epoch Super Cassette Vision",
                Slug = "scv",
                Manufacturer = "Epoch",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Super Cassette Vision" },
                    { "EU", "Yeno Super Cassette Vision" },
                    { "JP", "Super Cassette Vision" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Epoch Cassette Vision",
                Slug = "cassettevision",
                Manufacturer = "Epoch",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Cassette Vision" },
                    { "EU", "Cassette Vision" },
                    { "JP", "Cassette Vision" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Casio Loopy",
                Slug = "loopy",
                Manufacturer = "Casio",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Casio Loopy" },
                    { "EU", "Casio Loopy" },
                    { "JP", "Casio Loopy" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Casio PV-1000",
                Slug = "pv1000",
                Manufacturer = "Casio",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Casio PV-1000" },
                    { "EU", "Casio PV-1000" },
                    { "JP", "Casio PV-1000" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Funtech Super A'Can",
                Slug = "superacan",
                Manufacturer = "Funtech",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Super A'Can" },
                    { "EU", "Super A'Can" },
                    { "JP", "Super A'Can" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Interton VC 4000",
                Slug = "vc4000",
                Manufacturer = "Interton",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "VC 4000" },
                    { "EU", "Interton VC 4000" },
                    { "JP", "VC 4000" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "RCA Studio II",
                Slug = "studio2",
                Manufacturer = "RCA",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "RCA Studio II" },
                    { "EU", "RCA Studio II" },
                    { "JP", "RCA Studio II" }
                },
                RomFileExtensions = new List<string> { ".st2", ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Amstrad GX4000",
                Slug = "gx4000",
                Manufacturer = "Amstrad",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Amstrad GX4000" },
                    { "EU", "Amstrad GX4000" },
                    { "JP", "Amstrad GX4000" }
                },
                RomFileExtensions = new List<string> { ".cpr", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Philips Videopac+ G7400",
                Slug = "videopacplus",
                Manufacturer = "Philips",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Videopac+" },
                    { "EU", "Philips Videopac+" },
                    { "JP", "Videopac+" }
                },
                RomFileExtensions = new List<string> { ".bin", ".o2" }
            },
            new PlatformDefinition
            {
                Name = "VTech CreatiVision",
                Slug = "creativision",
                Manufacturer = "VTech",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "CreatiVision" },
                    { "EU", "VTech CreatiVision" },
                    { "JP", "CreatiVision" }
                },
                RomFileExtensions = new List<string> { ".rom", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "VTech V.Smile",
                Slug = "vsmile",
                Manufacturer = "VTech",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "V.Smile" },
                    { "EU", "V.Smile" },
                    { "JP", "V.Smile" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Zeebo",
                Slug = "zeebo",
                Manufacturer = "Tectoy / Qualcomm",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Zeebo" },
                    { "EU", "Zeebo" },
                    { "JP", "Zeebo" }
                },
                RomFileExtensions = new List<string> { ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Konami Picno",
                Slug = "picno",
                Manufacturer = "Konami",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Konami Picno" },
                    { "EU", "Konami Picno" },
                    { "JP", "Konami Picno" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Microsoft Xbox 360 Digital",
                Slug = "xbox360digital",
                Manufacturer = "Microsoft",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Xbox 360 Digital" },
                    { "EU", "Xbox 360 Digital" },
                    { "JP", "Xbox 360 Digital" }
                },
                RomFileExtensions = new List<string> { ".xex", ".god", ".iso" }
            },
            new PlatformDefinition
            {
                Name = "Sony PlayStation 3 PSN",
                Slug = "ps3psn",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PS3 PSN" },
                    { "EU", "PS3 PSN" },
                    { "JP", "PS3 PSN" }
                },
                RomFileExtensions = new List<string> { ".pkg", ".rap", ".edat", ".iso" }
            },
            new PlatformDefinition
            {
                Name = "Sony PlayStation Portable PSN",
                Slug = "psppsn",
                Manufacturer = "Sony",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "PSP PSN" },
                    { "EU", "PSP PSN" },
                    { "JP", "PSP PSN" }
                },
                RomFileExtensions = new List<string> { ".pkg", ".pbp", ".iso", ".cso" }
            },
            new PlatformDefinition
            {
                Name = "Watara Supervision",
                Slug = "supervision",
                Manufacturer = "Watara",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Supervision" },
                    { "EU", "Watara Supervision" },
                    { "JP", "Supervision" }
                },
                RomFileExtensions = new List<string> { ".sv", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Tiger Game.com",
                Slug = "gamecom",
                Manufacturer = "Tiger Electronics",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game.com" },
                    { "EU", "Game.com" },
                    { "JP", "Game.com" }
                },
                RomFileExtensions = new List<string> { ".tgc", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Entex Adventure Vision",
                Slug = "adventurevision",
                Manufacturer = "Entex",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Adventure Vision" },
                    { "EU", "Adventure Vision" },
                    { "JP", "Adventure Vision" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Hartung Game Master",
                Slug = "gamemaster",
                Manufacturer = "Hartung",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game Master" },
                    { "EU", "Hartung Game Master" },
                    { "JP", "Game Master" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "GamePark GP32",
                Slug = "gp32",
                Manufacturer = "GamePark",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "GP32" },
                    { "EU", "GP32" },
                    { "JP", "GP32" }
                },
                RomFileExtensions = new List<string> { ".smc", ".fxe", ".gxb", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Arduboy",
                Slug = "arduboy",
                Manufacturer = "Arduboy Inc.",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Arduboy" },
                    { "EU", "Arduboy" },
                    { "JP", "Arduboy" }
                },
                RomFileExtensions = new List<string> { ".hex", ".arduboy" }
            },
            new PlatformDefinition
            {
                Name = "Nintendo Game & Watch",
                Slug = "gameandwatch",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Game & Watch" },
                    { "EU", "Game & Watch" },
                    { "JP", "Game & Watch" }
                },
                RomFileExtensions = new List<string> { ".mgw", ".gnw" }
            },
            new PlatformDefinition
            {
                Name = "LeapFrog Leapster",
                Slug = "leapster",
                Manufacturer = "LeapFrog",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Leapster" },
                    { "EU", "Leapster" },
                    { "JP", "Leapster" }
                },
                RomFileExtensions = new List<string> { ".bin", ".rom" }
            },
            new PlatformDefinition
            {
                Name = "Benesse Pocket Challenge V2",
                Slug = "pocketchallengev2",
                Manufacturer = "Benesse",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Pocket Challenge V2" },
                    { "EU", "Pocket Challenge V2" },
                    { "JP", "Pocket Challenge V2" }
                },
                RomFileExtensions = new List<string> { ".pc2", ".ws", ".bin" }
            },
            new PlatformDefinition
            {
                Name = "Bandai Sufami Turbo",
                Slug = "sufamiturbo",
                Manufacturer = "Bandai",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "Sufami Turbo" },
                    { "EU", "Sufami Turbo" },
                    { "JP", "Sufami Turbo" }
                },
                RomFileExtensions = new List<string> { ".st", ".sfc", ".smc" }
            },
            new PlatformDefinition
            {
                Name = "Nintendo e-Reader",
                Slug = "ereader",
                Manufacturer = "Nintendo",
                RegionalConsoleNames = new Dictionary<string, string>
                {
                    { "US", "e-Reader" },
                    { "EU", "e-Reader" },
                    { "JP", "Card e-Reader" }
                },
                RomFileExtensions = new List<string> { ".raw", ".bin", ".gba" }
            }
        };

        public static readonly HashSet<string> KnownPlatformSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "nes", "fds", "snes", "satellaview", "n64", "64dd", "gamecube", "wii", "wiiware", "wiiu", "wiiueshop",
            "gb", "gbc", "gba", "nds", "dsiware", "3ds", "3dseshop", "switch", "switcheshop",
            "genesis", "segacd", "32x", "saturn", "dreamcast", "sms", "gg", "sg1000", "pico", "beena",
            "tg16", "supergrafx", "tgcd",
            "ps1", "ps2", "psp", "psppsn", "ps3", "ps3psn", "psvita",
            "xbox", "xbox360", "xbox360digital",
            "atari2600", "atari5200", "atari7800", "lynx", "jaguar", "jaguarcd",
            "neogeo", "neogeocd", "ngp", "ngpc", "ws", "wonderswan", "vb",
            "3do", "cd32", "colecovision", "intellivision", "vectrex", "pcfx", "pokemonmini", "odyssey2", "videopacplus", "channelf", "cdi",
            "arcadia2001", "scv", "cassettevision", "loopy", "pv1000", "superacan", "vc4000", "studio2", "gx4000", "creativision", "vsmile", "zeebo", "picno",
            "supervision", "gamecom", "adventurevision", "gamemaster", "gp32", "arduboy", "gameandwatch", "leapster", "pocketchallengev2",
            "sufamiturbo", "ereader"
        };

        public static readonly HashSet<string> CartridgePlatforms = new(StringComparer.OrdinalIgnoreCase)
        {
            "nes", "fds", "snes", "satellaview", "sufamiturbo", "n64", "64dd", "gb", "gbc", "gba", "ereader", "nds", "dsiware", "3ds", "3dseshop", "switch", "switcheshop",
            "genesis", "32x", "sms", "gg", "sg1000", "pico", "beena", "tg16", "pce", "supergrafx", "atari2600", "atari5200", "atari7800", "lynx",
            "jaguar", "neogeo", "ngp", "ngpc", "ws", "wonderswan", "vb", "psvita",
            "colecovision", "intellivision", "vectrex", "pokemonmini", "odyssey2", "videopacplus", "channelf",
            "arcadia2001", "scv", "cassettevision", "loopy", "pv1000", "superacan", "vc4000", "studio2", "gx4000", "creativision", "vsmile", "zeebo", "picno",
            "supervision", "gamecom", "adventurevision", "gamemaster", "gp32", "arduboy", "gameandwatch", "leapster", "pocketchallengev2"
        };

        public static readonly Dictionary<string, HashSet<string>> ExclusiveExtensionsByPlatform = new(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nes", ".unf" },
            ["fds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".fds" },
            ["snes"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sfc", ".smc" },
            ["satellaview"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bs" },
            ["sufamiturbo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".st" },
            ["n64"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".z64", ".n64", ".v64" },
            ["64dd"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ndd" },
            ["gb"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gb" },
            ["gbc"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gbc" },
            ["gba"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gba" },
            ["ereader"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".raw" },
            ["nds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nds", ".dsi" },
            ["dsiware"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nds", ".dsi" },
            ["3ds"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".3ds", ".cia", ".cci" },
            ["3dseshop"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cia", ".3ds", ".cci" },
            ["switch"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nsp", ".xci", ".nsz" },
            ["switcheshop"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".nsp", ".nsz" },
            ["genesis"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".gen", ".smd" },
            ["pico"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md" },
            ["32x"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".32x" },
            ["sms"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sms" },
            ["gg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gg" },
            ["sg1000"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sg", ".sf", ".sc", ".sf7" },
            ["pce"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pce" },
            ["tg16"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pce" },
            ["supergrafx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sgx", ".pce" },
            ["atari2600"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".a26" },
            ["atari5200"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".a52" },
            ["atari7800"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".a78" },
            ["colecovision"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".col" },
            ["intellivision"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".int" },
            ["vectrex"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vec" },
            ["pokemonmini"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".min" },
            ["odyssey2"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".o2" },
            ["videopacplus"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".o2" },
            ["channelf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".chf" },
            ["studio2"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".st2" },
            ["gx4000"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cpr" },
            ["lynx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".lnx" },
            ["jaguar"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".j64", ".jag" },
            ["neogeo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".neo" },
            ["ngp"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ngp" },
            ["ngpc"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ngc" },
            ["ws"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ws" },
            ["wonderswan"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wsc" },
            ["vb"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vb" },
            ["supervision"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sv" },
            ["gamecom"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".tgc" },
            ["gp32"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".smc", ".fxe", ".gxb" },
            ["arduboy"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".hex", ".arduboy" },
            ["gameandwatch"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mgw", ".gnw" },
            ["pocketchallengev2"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pc2", ".ws" },
            ["ps1"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pbp" },
            ["psp"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cso", ".pbp" },
            ["psppsn"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pkg", ".pbp", ".cso" },
            ["ps3"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pkg" },
            ["ps3psn"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pkg", ".rap", ".edat" },
            ["psvita"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".vpk" },
            ["wiiu"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wua", ".wux", ".wud", ".rpx" },
            ["wiiueshop"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wua", ".wux", ".wud", ".rpx" },
            ["gamecube"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".gcm", ".ciso" },
            ["wii"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wbfs" },
            ["wiiware"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".wad" }
        };

        public static readonly Dictionary<string, string[]> PlatformAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = new[] { "nes", "nintendo-entertainment-system", "nintendo entertainment system", "famicom" },
            ["fds"] = new[] { "fds", "famicom-disk-system", "famicom disk system", "disk system" },
            ["snes"] = new[] { "snes", "super-nintendo-entertainment-system", "super nintendo entertainment system", "super nintendo", "super nes", "super famicom", "sfc" },
            ["satellaview"] = new[] { "satellaview", "bs-x", "bsx", "nintendo-satellaview", "nintendo satellaview" },
            ["sufamiturbo"] = new[] { "sufamiturbo", "sufami-turbo", "sufami turbo", "bandai sufami turbo", "nintendo sufami turbo" },
            ["n64"] = new[] { "n64", "nintendo-64", "nintendo 64", "ultra 64" },
            ["64dd"] = new[] { "64dd", "n64dd", "nintendo64dd", "nintendo-64dd", "nintendo 64dd" },
            ["gamecube"] = new[] { "gamecube", "nintendo-gamecube", "nintendo gamecube", "gcn", "ngc" },
            ["wii"] = new[] { "wii", "nintendo-wii", "nintendo wii" },
            ["wiiware"] = new[] { "wiiware", "wii-ware", "nintendo-wiiware", "nintendo wiiware", "wii eshop", "nintendo wii eshop", "wii shop" },
            ["wiiu"] = new[] { "wii-u", "wiiu", "nintendo-wii-u", "nintendo wii u" },
            ["wiiueshop"] = new[] { "wiiueshop", "wiiu-eshop", "wii-u-eshop", "wii u eshop", "wiiu eshop", "nintendo wii u eshop", "nintendo-wii-u-eshop", "wii u digital" },
            ["gb"] = new[] { "game-boy", "game boy", "gameboy", "dmg" },
            ["gbc"] = new[] { "game-boy-color", "game boy color", "gameboy color", "cgb" },
            ["gba"] = new[] { "game-boy-advance", "game boy advance", "gameboy advance", "agb" },
            ["ereader"] = new[] { "ereader", "e-reader", "nintendo-e-reader", "nintendo e-reader", "card e-reader" },
            ["nds"] = new[] { "nintendo-ds", "nintendo ds", "nds" },
            ["dsiware"] = new[] { "dsiware", "dsi-ware", "dsi shop", "nintendo dsi shop", "nintendo-dsi-shop", "nintendo dsiware" },
            ["3ds"] = new[] { "nintendo-3ds", "nintendo 3ds", "3ds", "n3ds" },
            ["3dseshop"] = new[] { "3dseshop", "3ds-eshop", "3ds eshop", "nintendo 3ds eshop", "nintendo-3ds-eshop", "3ds digital", "eshop 3ds" },
            ["switch"] = new[] { "nintendo-switch", "nintendo switch", "switch", "nsw", "nx" },
            ["switcheshop"] = new[] { "switcheshop", "switch-eshop", "switch eshop", "nintendo switch eshop", "nintendo-switch-eshop", "nsw eshop", "switch digital" },
            ["genesis"] = new[] { "sega-genesis", "sega genesis", "genesis", "mega-drive", "mega drive", "megadrive", "md" },
            ["segacd"] = new[] { "sega-cd", "sega cd", "segacd", "mega-cd", "mega cd", "megacd", "scd" },
            ["32x"] = new[] { "sega-32x", "sega 32x", "sega32x", "32x", "super 32x" },
            ["saturn"] = new[] { "sega-saturn", "sega saturn", "saturn", "ss" },
            ["dreamcast"] = new[] { "sega-dreamcast", "sega dreamcast", "dreamcast", "dc" },
            ["sms"] = new[] { "sega-master-system", "sega master system", "master system", "sms" },
            ["gg"] = new[] { "sega-game-gear", "sega game gear", "game gear", "gamegear", "gg" },
            ["sg1000"] = new[] { "sg1000", "sg-1000", "sega-sg-1000", "sega sg-1000", "sc-3000" },
            ["pico"] = new[] { "pico", "sega-pico", "sega pico" },
            ["beena"] = new[] { "beena", "sega-beena", "sega beena", "advanced pico beena" },
            ["tg16"] = new[] { "turbografx-16", "turbografx 16", "turbografx", "tg16", "pc-engine", "pc engine", "pce" },
            ["supergrafx"] = new[] { "supergrafx", "pc-engine-supergrafx", "pc engine supergrafx", "nec pc engine supergrafx", "sgx" },
            ["tgcd"] = new[]
            {
                "turbografx-cd", "turbografx cd", "turbografxcd", "turbografx-16 cd", "turbografx-16-cd", "turbografx 16 cd",
                "tg-cd", "tgcd", "tg16-cd", "tg16 cd", "tg16cd", "tgfx16-cd", "tgfx16 cd",
                "pc-engine-cd", "pc engine cd", "pcenginecd", "pcecd", "pce-cd", "pce cd",
                "pcecdrom", "pcecdrom2", "pce-cdrom2",
                "pc engine cd-rom2", "pc engine super cd-rom2"
            },
            ["ps1"] = new[] { "sony-playstation", "playstation", "ps1", "psx", "sony playstation", "psone" },
            ["ps2"] = new[] { "sony-playstation-2", "playstation-2", "playstation 2", "ps2", "ps2dvd", "ps2cd", "sony playstation 2" },
            ["ps3"] = new[] { "sony-playstation-3", "playstation-3", "playstation 3", "ps3", "sony playstation 3" },
            ["ps3psn"] = new[] { "ps3psn", "ps3-psn", "ps3 psn", "playstation 3 psn", "sony playstation 3 (psn)" },
            ["psp"] = new[] { "sony-playstation-portable", "playstation-portable", "playstation portable", "psp", "sony psp", "psx2psp", "ps2psp" },
            ["psppsn"] = new[] { "psppsn", "psp-psn", "psp psn", "playstation portable psn", "sony playstation portable (psn)" },
            ["psvita"] = new[] { "sony-playstation-vita", "playstation-vita", "playstation vita", "ps vita", "psv", "vita" },
            ["xbox"] = new[] { "xbox", "original xbox", "og xbox", "xboxdvd" },
            ["xbox360"] = new[] { "xbox-360", "xbox 360", "xbox360", "x360" },
            ["xbox360digital"] = new[] { "xbox360digital", "xbox-360-digital", "xbox 360 digital", "xbox 360 (digital)", "xbox live arcade", "xbla" },
            ["atari2600"] = new[] { "atari-2600", "atari 2600", "2600", "vcs" },
            ["atari7800"] = new[] { "atari-7800", "atari 7800", "7800" },
            ["lynx"] = new[] { "atari-lynx", "atari lynx", "lynx" },
            ["jaguar"] = new[] { "atari-jaguar", "atari jaguar", "jaguar" },
            ["jaguarcd"] = new[] { "jaguarcd", "atarijaguarcd", "atari-jaguar-cd", "atari jaguar cd", "jaguar cd", "jagcd" },
            ["neogeo"] = new[] { "neo-geo", "neo geo", "neogeo", "aes", "mvs" },
            ["neogeocd"] = new[] { "neogeocd", "neo-geo-cd", "neo geo cd", "snk neo geo cd", "ngcd" },
            ["ngp"] = new[] { "ngp", "neo-geo-pocket", "neo geo pocket", "snk neo geo pocket" },
            ["ngpc"] = new[] { "neo-geo-pocket-color", "neo geo pocket color", "ngpc" },
            ["ws"] = new[] { "ws", "wonderswan", "bandai-wonderswan", "bandai wonderswan" },
            ["wonderswan"] = new[] { "wonderswan-color", "wonderswan color", "bandai wonderswan color", "wsc" },
            ["vb"] = new[] { "virtual-boy", "virtual boy", "vb" },
            ["3do"] = new[] { "3do", "3do interactive multiplayer", "the 3do company 3do" },
            ["cd32"] = new[] { "cd32", "amiga-cd32", "amiga cd32", "commodore cd32", "commodore amiga cd32" },
            ["atari5200"] = new[] { "atari-5200", "atari 5200", "5200", "a5200" },
            ["colecovision"] = new[] { "colecovision", "coleco-colecovision", "coleco colecovision", "coleco" },
            ["intellivision"] = new[] { "intellivision", "mattel-intellivision", "mattel intellivision", "intv" },
            ["vectrex"] = new[] { "vectrex", "gce-vectrex", "gce vectrex" },
            ["pcfx"] = new[] { "pc-fx", "pcfx", "nec-pc-fx", "nec pc-fx" },
            ["pokemonmini"] = new[] { "pokemon-mini", "pokemon mini", "pokemonmini", "pokemini", "nintendo pokemon mini" },
            ["odyssey2"] = new[] { "odyssey-2", "odyssey 2", "odyssey2", "magnavox odyssey 2", "videopac" },
            ["videopacplus"] = new[] { "videopacplus", "videopac+", "philips videopac+", "g7400", "videopac+ g7400" },
            ["channelf"] = new[] { "channel-f", "channel f", "channelf", "fairchild channel f" },
            ["cdi"] = new[] { "cd-i", "cdi", "philips cd-i", "philips-cd-i" },
            ["arcadia2001"] = new[] { "arcadia2001", "arcadia-2001", "arcadia 2001", "emerson arcadia 2001" },
            ["scv"] = new[] { "scv", "super-cassette-vision", "super cassette vision", "epoch super cassette vision" },
            ["cassettevision"] = new[] { "cassettevision", "cassette-vision", "cassette vision", "epoch cassette vision" },
            ["loopy"] = new[] { "loopy", "casio-loopy", "casio loopy" },
            ["pv1000"] = new[] { "pv1000", "pv-1000", "casio-pv-1000", "casio pv-1000" },
            ["superacan"] = new[] { "superacan", "super-acan", "super acan", "super a'can", "funtech super acan", "funtech super a'can" },
            ["vc4000"] = new[] { "vc4000", "vc-4000", "vc 4000", "interton vc 4000" },
            ["studio2"] = new[] { "studio2", "studio-ii", "studio ii", "rca studio ii" },
            ["gx4000"] = new[] { "gx4000", "amstrad-gx4000", "amstrad gx4000" },
            ["creativision"] = new[] { "creativision", "vtech-creativision", "vtech creativision" },
            ["vsmile"] = new[] { "vsmile", "v-smile", "v.smile", "vtech v.smile" },
            ["zeebo"] = new[] { "zeebo", "mobile-zeebo", "mobile zeebo" },
            ["picno"] = new[] { "picno", "konami-picno", "konami picno" },
            ["supervision"] = new[] { "supervision", "watara-supervision", "watara supervision" },
            ["gamecom"] = new[] { "gamecom", "game.com", "tiger-game.com", "tiger game.com" },
            ["adventurevision"] = new[] { "adventurevision", "adventure-vision", "adventure vision", "entex adventure vision" },
            ["gamemaster"] = new[] { "gamemaster", "game-master", "game master", "hartung game master" },
            ["gp32"] = new[] { "gp32", "gamepark-gp32", "gamepark gp32" },
            ["arduboy"] = new[] { "arduboy", "arduboy inc - arduboy" },
            ["gameandwatch"] = new[] { "gameandwatch", "game-and-watch", "game & watch", "nintendo game & watch", "handheld electronic game" },
            ["leapster"] = new[] { "leapster", "leapfrog-leapster", "leapfrog leapster", "leapster learning game system" },
            ["pocketchallengev2"] = new[] { "pocketchallengev2", "pocket-challenge-v2", "pocket challenge v2", "benesse pocket challenge v2" }
        };

        public static readonly Dictionary<string, string> CanonicalPlatformNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["nes"] = "Nintendo Entertainment System",
            ["fds"] = "Nintendo Famicom Disk System",
            ["snes"] = "Super Nintendo Entertainment System",
            ["satellaview"] = "Nintendo Satellaview",
            ["sufamiturbo"] = "Bandai Sufami Turbo",
            ["n64"] = "Nintendo 64",
            ["64dd"] = "Nintendo 64DD",
            ["gamecube"] = "Nintendo GameCube",
            ["wii"] = "Nintendo Wii",
            ["wiiware"] = "Nintendo WiiWare",
            ["wiiu"] = "Nintendo Wii U",
            ["wiiueshop"] = "Nintendo Wii U eShop",
            ["gb"] = "Nintendo Game Boy",
            ["gbc"] = "Nintendo Game Boy Color",
            ["gba"] = "Nintendo Game Boy Advance",
            ["ereader"] = "Nintendo e-Reader",
            ["nds"] = "Nintendo DS",
            ["dsiware"] = "Nintendo DSi Shop",
            ["3ds"] = "Nintendo 3DS",
            ["3dseshop"] = "Nintendo 3DS eShop",
            ["switch"] = "Nintendo Switch",
            ["switcheshop"] = "Nintendo Switch eShop",
            ["genesis"] = "Sega Genesis",
            ["segacd"] = "Sega CD",
            ["32x"] = "Sega 32X",
            ["saturn"] = "Sega Saturn",
            ["dreamcast"] = "Sega Dreamcast",
            ["sms"] = "Sega Master System",
            ["gg"] = "Sega Game Gear",
            ["sg1000"] = "Sega SG-1000",
            ["pico"] = "Sega Pico",
            ["beena"] = "Sega Advanced Pico Beena",
            ["tg16"] = "NEC TurboGrafx-16",
            ["supergrafx"] = "NEC PC Engine SuperGrafx",
            ["tgcd"] = "NEC TurboGrafx-CD",
            ["ps1"] = "Sony PlayStation",
            ["ps2"] = "Sony PlayStation 2",
            ["ps3"] = "Sony PlayStation 3",
            ["ps3psn"] = "Sony PlayStation 3 PSN",
            ["psp"] = "Sony PlayStation Portable",
            ["psppsn"] = "Sony PlayStation Portable PSN",
            ["psvita"] = "Sony PlayStation Vita",
            ["xbox"] = "Microsoft Xbox",
            ["xbox360"] = "Microsoft Xbox 360",
            ["xbox360digital"] = "Microsoft Xbox 360 Digital",
            ["atari2600"] = "Atari 2600",
            ["atari7800"] = "Atari 7800",
            ["lynx"] = "Atari Lynx",
            ["jaguar"] = "Atari Jaguar",
            ["jaguarcd"] = "Atari Jaguar CD",
            ["neogeo"] = "SNK Neo Geo",
            ["neogeocd"] = "SNK Neo Geo CD",
            ["ngp"] = "SNK Neo Geo Pocket",
            ["ngpc"] = "SNK Neo Geo Pocket Color",
            ["ws"] = "Bandai WonderSwan",
            ["wonderswan"] = "Bandai WonderSwan Color",
            ["vb"] = "Nintendo Virtual Boy",
            ["3do"] = "3DO Interactive Multiplayer",
            ["cd32"] = "Commodore Amiga CD32",
            ["atari5200"] = "Atari 5200",
            ["colecovision"] = "ColecoVision",
            ["intellivision"] = "Mattel Intellivision",
            ["vectrex"] = "GCE Vectrex",
            ["pcfx"] = "NEC PC-FX",
            ["pokemonmini"] = "Nintendo Pokemon Mini",
            ["odyssey2"] = "Magnavox Odyssey 2",
            ["videopacplus"] = "Philips Videopac+ G7400",
            ["channelf"] = "Fairchild Channel F",
            ["cdi"] = "Philips CD-i",
            ["arcadia2001"] = "Emerson Arcadia 2001",
            ["scv"] = "Epoch Super Cassette Vision",
            ["cassettevision"] = "Epoch Cassette Vision",
            ["loopy"] = "Casio Loopy",
            ["pv1000"] = "Casio PV-1000",
            ["superacan"] = "Funtech Super A'Can",
            ["vc4000"] = "Interton VC 4000",
            ["studio2"] = "RCA Studio II",
            ["gx4000"] = "Amstrad GX4000",
            ["creativision"] = "VTech CreatiVision",
            ["vsmile"] = "VTech V.Smile",
            ["zeebo"] = "Zeebo",
            ["picno"] = "Konami Picno",
            ["supervision"] = "Watara Supervision",
            ["gamecom"] = "Tiger Game.com",
            ["adventurevision"] = "Entex Adventure Vision",
            ["gamemaster"] = "Hartung Game Master",
            ["gp32"] = "GamePark GP32",
            ["arduboy"] = "Arduboy",
            ["gameandwatch"] = "Nintendo Game & Watch",
            ["leapster"] = "LeapFrog Leapster",
            ["pocketchallengev2"] = "Benesse Pocket Challenge V2"
        };

        private static readonly HashSet<string> DiscBasedPlatformSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ps1", "ps2", "ps3", "ps4", "ps5", "psp",
            "segacd", "saturn", "dreamcast",
            "gamecube", "wii", "wiiu",
            "tgcd", "jaguarcd", "xbox", "xbox360", "xboxone", "xboxseriesx",
            "3do", "neogeocd", "cdi", "pcfx", "cd32", "pippin"
        };

        public static bool IsDiscBasedPlatform(string platformSlug, string platformTitle = null)
        {
            var normalized = Parser.PlatformCompatibilityValidator.NormalizePlatformSlug(platformSlug, platformTitle);
            return !string.IsNullOrWhiteSpace(normalized) && DiscBasedPlatformSlugs.Contains(normalized);
        }

        public static readonly Dictionary<string, string> DefaultPlatformRawExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            { "snes", ".sfc" },
            { "Super Nintendo Entertainment System", ".sfc" },
            { "satellaview", ".bs" },
            { "Nintendo Satellaview", ".bs" },
            { "sufamiturbo", ".st" },
            { "Bandai Sufami Turbo", ".st" },
            { "nes", ".nes" },
            { "Nintendo Entertainment System", ".nes" },
            { "fds", ".fds" },
            { "Nintendo Famicom Disk System", ".fds" },
            { "gb", ".gb" },
            { "Game Boy", ".gb" },
            { "Nintendo Game Boy", ".gb" },
            { "gbc", ".gbc" },
            { "Game Boy Color", ".gbc" },
            { "Nintendo Game Boy Color", ".gbc" },
            { "gba", ".gba" },
            { "Game Boy Advance", ".gba" },
            { "Nintendo Game Boy Advance", ".gba" },
            { "ereader", ".raw" },
            { "Nintendo e-Reader", ".raw" },
            { "genesis", ".md" },
            { "Sega Genesis", ".md" },
            { "Mega Drive", ".md" },
            { "32x", ".32x" },
            { "Sega 32X", ".32x" },
            { "sms", ".sms" },
            { "Master System", ".sms" },
            { "Sega Master System", ".sms" },
            { "gg", ".gg" },
            { "Game Gear", ".gg" },
            { "Sega Game Gear", ".gg" },
            { "sg1000", ".sg" },
            { "Sega SG-1000", ".sg" },
            { "SG-1000", ".sg" },
            { "pico", ".md" },
            { "Sega Pico", ".md" },
            { "beena", ".bin" },
            { "Sega Advanced Pico Beena", ".bin" },
            { "n64", ".z64" },
            { "Nintendo 64", ".z64" },
            { "64dd", ".ndd" },
            { "Nintendo 64DD", ".ndd" },
            { "nds", ".nds" },
            { "Nintendo DS", ".nds" },
            { "dsiware", ".nds" },
            { "Nintendo DSi Shop", ".nds" },
            { "3ds", ".3ds" },
            { "Nintendo 3DS", ".3ds" },
            { "3dseshop", ".cia" },
            { "Nintendo 3DS eShop", ".cia" },
            { "switch", ".nsp" },
            { "Nintendo Switch", ".nsp" },
            { "switcheshop", ".nsp" },
            { "Nintendo Switch eShop", ".nsp" },
            { "wiiware", ".wad" },
            { "Nintendo WiiWare", ".wad" },
            { "wiiueshop", ".wua" },
            { "Nintendo Wii U eShop", ".wua" },
            { "vb", ".vb" },
            { "Nintendo Virtual Boy", ".vb" },
            { "tg16", ".pce" },
            { "TurboGrafx-16", ".pce" },
            { "NEC TurboGrafx-16", ".pce" },
            { "supergrafx", ".sgx" },
            { "NEC PC Engine SuperGrafx", ".sgx" },
            { "atari2600", ".a26" },
            { "Atari 2600", ".a26" },
            { "atari5200", ".a52" },
            { "Atari 5200", ".a52" },
            { "atari7800", ".a78" },
            { "Atari 7800", ".a78" },
            { "lynx", ".lnx" },
            { "Atari Lynx", ".lnx" },
            { "jaguar", ".j64" },
            { "Atari Jaguar", ".j64" },
            { "neogeo", ".neo" },
            { "SNK Neo Geo", ".neo" },
            { "colecovision", ".col" },
            { "intellivision", ".int" },
            { "Mattel Intellivision", ".int" },
            { "vectrex", ".vec" },
            { "GCE Vectrex", ".vec" },
            { "pokemonmini", ".min" },
            { "Pokemon Mini", ".min" },
            { "Nintendo Pokemon Mini", ".min" },
            { "odyssey2", ".bin" },
            { "Magnavox Odyssey 2", ".bin" },
            { "videopacplus", ".bin" },
            { "Philips Videopac+ G7400", ".bin" },
            { "channelf", ".chf" },
            { "Fairchild Channel F", ".chf" },
            { "ws", ".ws" },
            { "Bandai WonderSwan", ".ws" },
            { "wonderswan", ".wsc" },
            { "Bandai WonderSwan Color", ".wsc" },
            { "ngp", ".ngp" },
            { "SNK Neo Geo Pocket", ".ngp" },
            { "ngpc", ".ngc" },
            { "SNK Neo Geo Pocket Color", ".ngc" },
            { "arcadia2001", ".bin" },
            { "Emerson Arcadia 2001", ".bin" },
            { "scv", ".bin" },
            { "Epoch Super Cassette Vision", ".bin" },
            { "cassettevision", ".bin" },
            { "Epoch Cassette Vision", ".bin" },
            { "loopy", ".bin" },
            { "Casio Loopy", ".bin" },
            { "pv1000", ".bin" },
            { "Casio PV-1000", ".bin" },
            { "superacan", ".bin" },
            { "Funtech Super A'Can", ".bin" },
            { "vc4000", ".bin" },
            { "Interton VC 4000", ".bin" },
            { "studio2", ".st2" },
            { "RCA Studio II", ".st2" },
            { "gx4000", ".cpr" },
            { "Amstrad GX4000", ".cpr" },
            { "creativision", ".rom" },
            { "VTech CreatiVision", ".rom" },
            { "vsmile", ".bin" },
            { "VTech V.Smile", ".bin" },
            { "zeebo", ".bin" },
            { "picno", ".bin" },
            { "Konami Picno", ".bin" },
            { "supervision", ".sv" },
            { "Watara Supervision", ".sv" },
            { "gamecom", ".tgc" },
            { "Tiger Game.com", ".tgc" },
            { "adventurevision", ".bin" },
            { "Entex Adventure Vision", ".bin" },
            { "gamemaster", ".bin" },
            { "Hartung Game Master", ".bin" },
            { "gp32", ".smc" },
            { "GamePark GP32", ".smc" },
            { "arduboy", ".hex" },
            { "gameandwatch", ".mgw" },
            { "Nintendo Game & Watch", ".mgw" },
            { "leapster", ".bin" },
            { "LeapFrog Leapster", ".bin" },
            { "pocketchallengev2", ".pc2" },
            { "Benesse Pocket Challenge V2", ".pc2" },
            { "psvita", ".vpk" },
            { "ps3psn", ".pkg" },
            { "psppsn", ".pkg" },
            { "xbox360digital", ".xex" }
        };

        public static string GetDefaultRawExtension(string platformSlugOrTitle)
        {
            if (string.IsNullOrWhiteSpace(platformSlugOrTitle))
            {
                return ".rom";
            }

            var trimmed = platformSlugOrTitle.Trim();
            if (DefaultPlatformRawExtension.TryGetValue(trimmed, out var direct))
            {
                return direct;
            }

            var normalized = Parser.PlatformCompatibilityValidator.NormalizePlatformSlug(trimmed, trimmed);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                if (DefaultPlatformRawExtension.TryGetValue(normalized, out var slugExt))
                {
                    return slugExt;
                }

                var platformDef = Platforms.FirstOrDefault(p => string.Equals(p.Slug, normalized, StringComparison.OrdinalIgnoreCase));
                if (platformDef?.RomFileExtensions != null && platformDef.RomFileExtensions.Count > 0)
                {
                    return platformDef.RomFileExtensions[0];
                }
            }

            return ".rom";
        }

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
