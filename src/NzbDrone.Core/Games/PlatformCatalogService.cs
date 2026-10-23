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
            }
        };

        public List<PlatformDefinition> GetAll()
        {
            return Platforms.ToList();
        }
    }
}
