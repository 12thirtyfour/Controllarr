using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.Export
{
    public enum SdCardDeviceProfile
    {
        OnionOS = 0,
        AnaloguePocket = 1,
        EverDrive = 2,
        FXPakPro = 3,
        EZFlash = 4,
        R4 = 5,
        MiSTer = 6,
        RetroArch = 7,
        Batocera = 8,
        EmulationStation = 9,
        GarlicOS = 10,
        ArkOS = 11,
        TwilightMenu = 12,
        SwissGameCube = 13,
        XStation = 14,
        Fenrir = 15,
        GDEMU = 16
    }

    public class SdCardProfileDescriptor
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string PathTemplate { get; set; }
        public bool UsesFat32AlphabeticalBuckets { get; set; }
    }

    public class SdCardExportItem
    {
        public string SourceFilePath { get; set; }
        public string PlatformFolder { get; set; }
        public string GameTitle { get; set; }
    }

    public class SdCardExportResult
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string TargetMountPath { get; set; }
        public int ExportedCount { get; set; }
        public List<string> DestinationPaths { get; set; } = new List<string>();
    }

    public interface ISdCardExporterService
    {
        IReadOnlyList<SdCardProfileDescriptor> GetSupportedProfiles();
        string BuildDeviceRelativePath(SdCardDeviceProfile profile, string platformFolder, string gameTitle, string fileName);
        SdCardExportResult ExportToSdCard(SdCardDeviceProfile profile, string targetMountPath, IEnumerable<SdCardExportItem> items, bool overwrite = false);
    }

    public class SdCardExporterService : ISdCardExporterService
    {
        private static readonly IReadOnlyList<SdCardProfileDescriptor> ProfileCatalog = new List<SdCardProfileDescriptor>
        {
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.MiSTer, Name = "MiSTer FPGA", Category = "FPGA", PathTemplate = "games/{Core}/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.AnaloguePocket, Name = "Analogue Pocket (openFPGA)", Category = "FPGA", PathTemplate = "Assets/{Core}/common/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.RetroArch, Name = "RetroArch", Category = "Multi-System Frontend", PathTemplate = "downloads/{Platform}/{FileName}", UsesFat32AlphabeticalBuckets = false },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.Batocera, Name = "Batocera / Knulli", Category = "Multi-System OS", PathTemplate = "roms/{SystemSlug}/{FileName}", UsesFat32AlphabeticalBuckets = false },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.EmulationStation, Name = "EmulationStation DE (ES-DE / EmuDeck)", Category = "Multi-System Frontend", PathTemplate = "ROMs/{SystemSlug}/{FileName}", UsesFat32AlphabeticalBuckets = false },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.OnionOS, Name = "OnionOS (Miyoo Mini / Plus)", Category = "Handheld CFW", PathTemplate = "Roms/{OnionSystem}/{FileName}", UsesFat32AlphabeticalBuckets = false },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.GarlicOS, Name = "GarlicOS / MuOS (Anbernic RG35XX)", Category = "Handheld CFW", PathTemplate = "Roms/{OnionSystem}/{FileName}", UsesFat32AlphabeticalBuckets = false },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.ArkOS, Name = "ArkOS / ROCKNIX / JELOS", Category = "Handheld CFW", PathTemplate = "roms/{SystemSlug}/{FileName}", UsesFat32AlphabeticalBuckets = false },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.EverDrive, Name = "Krikzz EverDrive (N8 / GB / GBA / MD / 64)", Category = "Flash Cart", PathTemplate = "{Platform}/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.FXPakPro, Name = "FXPak Pro / SD2SNES", Category = "Flash Cart", PathTemplate = "sd2snes/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.EZFlash, Name = "EZ-Flash (Omega / Definitive / Junior)", Category = "Flash Cart", PathTemplate = "ROMS/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.R4, Name = "R4 Wood / Flashcart", Category = "Flash Cart", PathTemplate = "Games/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.TwilightMenu, Name = "TWiLight Menu++ (DSi / 3DS)", Category = "Handheld CFW / Flash Cart", PathTemplate = "roms/nds/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.XStation, Name = "xStation (PlayStation 1 ODE)", Category = "Optical Drive Emulator (ODE)", PathTemplate = "00xstation/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.SwissGameCube, Name = "Swiss / GC Loader / Picoboot (GameCube)", Category = "Optical Drive Emulator (ODE)", PathTemplate = "games/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.Fenrir, Name = "Fenrir / Saroo / Satiator (Sega Saturn)", Category = "Optical Drive Emulator (ODE)", PathTemplate = "saturn/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true },
            new SdCardProfileDescriptor { Profile = SdCardDeviceProfile.GDEMU, Name = "GDEMU / MODE (Sega Dreamcast)", Category = "Optical Drive Emulator (ODE)", PathTemplate = "GDEMU/{AlphaBucket}/{FileName}", UsesFat32AlphabeticalBuckets = true }
        };

        private static readonly Dictionary<string, string> OnionOsSystemMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SNES", "SFC" },
            { "Super Nintendo Entertainment System", "SFC" },
            { "NES", "FC" },
            { "Nintendo Entertainment System", "FC" },
            { "Game Boy", "GB" },
            { "Game Boy Color", "GBC" },
            { "Game Boy Advance", "GBA" },
            { "Nintendo DS", "NDS" },
            { "Genesis", "MD" },
            { "Sega Genesis", "MD" },
            { "Mega Drive", "MD" },
            { "Master System", "MS" },
            { "Game Gear", "GG" },
            { "PlayStation", "PS" },
            { "Sony PlayStation", "PS" }
        };

        private static readonly Dictionary<string, string> AnaloguePocketCoreMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SNES", "snes" },
            { "Super Nintendo Entertainment System", "snes" },
            { "NES", "nes" },
            { "Nintendo Entertainment System", "nes" },
            { "Game Boy", "gb" },
            { "Game Boy Color", "gbc" },
            { "Game Boy Advance", "gba" },
            { "Genesis", "genesis" },
            { "Sega Genesis", "genesis" },
            { "Master System", "sms" },
            { "Game Gear", "gg" }
        };

        private static readonly Dictionary<string, string> MisterCoreMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SNES", "SNES" },
            { "Super Nintendo Entertainment System", "SNES" },
            { "NES", "NES" },
            { "Nintendo Entertainment System", "NES" },
            { "Game Boy", "GAMEBOY" },
            { "Game Boy Color", "GBC" },
            { "Game Boy Advance", "GBA" },
            { "Nintendo 64", "N64" },
            { "Genesis", "Genesis" },
            { "Sega Genesis", "Genesis" },
            { "Mega Drive", "Genesis" },
            { "Master System", "SMS" },
            { "Game Gear", "GameGear" },
            { "PlayStation", "PSX" },
            { "Sony PlayStation", "PSX" },
            { "Sega Saturn", "Saturn" },
            { "TurboGrafx-16", "TGFX16" }
        };

        private static readonly Dictionary<string, string> EmulationStationSlugMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SNES", "snes" },
            { "Super Nintendo Entertainment System", "snes" },
            { "NES", "nes" },
            { "Nintendo Entertainment System", "nes" },
            { "Game Boy", "gb" },
            { "Game Boy Color", "gbc" },
            { "Game Boy Advance", "gba" },
            { "Nintendo 64", "n64" },
            { "Nintendo DS", "nds" },
            { "GameCube", "gc" },
            { "Nintendo GameCube", "gc" },
            { "Genesis", "megadrive" },
            { "Sega Genesis", "megadrive" },
            { "Mega Drive", "megadrive" },
            { "Master System", "mastersystem" },
            { "Game Gear", "gamegear" },
            { "Sega Saturn", "saturn" },
            { "Dreamcast", "dreamcast" },
            { "Sega Dreamcast", "dreamcast" },
            { "PlayStation", "psx" },
            { "Sony PlayStation", "psx" },
            { "PlayStation 2", "ps2" }
        };

        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public SdCardExporterService(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public IReadOnlyList<SdCardProfileDescriptor> GetSupportedProfiles()
        {
            return ProfileCatalog;
        }

        public string BuildDeviceRelativePath(SdCardDeviceProfile profile, string platformFolder, string gameTitle, string fileName)
        {
            var safePlatform = platformFolder.IsNotNullOrWhiteSpace() ? platformFolder.Trim() : "Unknown";
            var safeFileName = Path.GetFileName(fileName);
            var bucket = ResolveAlphabeticalBucket(gameTitle.IsNotNullOrWhiteSpace() ? gameTitle : safeFileName);

            switch (profile)
            {
                case SdCardDeviceProfile.OnionOS:
                case SdCardDeviceProfile.GarlicOS:
                    var onionSystem = OnionOsSystemMap.TryGetValue(safePlatform, out var mappedOnion) ? mappedOnion : safePlatform.ToUpperInvariant();
                    return Path.Combine("Roms", onionSystem, safeFileName);

                case SdCardDeviceProfile.AnaloguePocket:
                    var pocketCore = AnaloguePocketCoreMap.TryGetValue(safePlatform, out var mappedCore) ? mappedCore : safePlatform.ToLowerInvariant();
                    return Path.Combine("Assets", pocketCore, "common", bucket, safeFileName);

                case SdCardDeviceProfile.MiSTer:
                    var misterCore = MisterCoreMap.TryGetValue(safePlatform, out var mappedMister) ? mappedMister : safePlatform;
                    return Path.Combine("games", misterCore, bucket, safeFileName);

                case SdCardDeviceProfile.RetroArch:
                    return Path.Combine("downloads", safePlatform, safeFileName);

                case SdCardDeviceProfile.Batocera:
                case SdCardDeviceProfile.ArkOS:
                    var batoceraSlug = EmulationStationSlugMap.TryGetValue(safePlatform, out var mappedBatocera) ? mappedBatocera : safePlatform.ToLowerInvariant();
                    return Path.Combine("roms", batoceraSlug, safeFileName);

                case SdCardDeviceProfile.EmulationStation:
                    var esSlug = EmulationStationSlugMap.TryGetValue(safePlatform, out var mappedEs) ? mappedEs : safePlatform.ToLowerInvariant();
                    return Path.Combine("ROMs", esSlug, safeFileName);

                case SdCardDeviceProfile.EverDrive:
                    return Path.Combine(safePlatform, bucket, safeFileName);

                case SdCardDeviceProfile.FXPakPro:
                    return Path.Combine("sd2snes", bucket, safeFileName);

                case SdCardDeviceProfile.EZFlash:
                    return Path.Combine("ROMS", bucket, safeFileName);

                case SdCardDeviceProfile.R4:
                    return Path.Combine("Games", bucket, safeFileName);

                case SdCardDeviceProfile.TwilightMenu:
                    var twilightSlug = EmulationStationSlugMap.TryGetValue(safePlatform, out var mappedTwilight) ? mappedTwilight : "nds";
                    return Path.Combine("roms", twilightSlug, bucket, safeFileName);

                case SdCardDeviceProfile.SwissGameCube:
                    return Path.Combine("games", bucket, safeFileName);

                case SdCardDeviceProfile.XStation:
                    return Path.Combine("00xstation", bucket, safeFileName);

                case SdCardDeviceProfile.Fenrir:
                    return Path.Combine("saturn", bucket, safeFileName);

                case SdCardDeviceProfile.GDEMU:
                    return Path.Combine("GDEMU", bucket, safeFileName);

                default:
                    return Path.Combine(safePlatform, safeFileName);
            }
        }

        public SdCardExportResult ExportToSdCard(SdCardDeviceProfile profile, string targetMountPath, IEnumerable<SdCardExportItem> items, bool overwrite = false)
        {
            if (targetMountPath.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Target mount path is required.", nameof(targetMountPath));
            }

            var result = new SdCardExportResult
            {
                Profile = profile,
                TargetMountPath = targetMountPath
            };

            if (items == null)
            {
                return result;
            }

            foreach (var item in items.Where(i => i != null && i.SourceFilePath.IsNotNullOrWhiteSpace()))
            {
                if (!_diskProvider.FileExists(item.SourceFilePath))
                {
                    _logger.Warn("Skipping missing source ROM during SD export: {0}", item.SourceFilePath);
                    continue;
                }

                var relativePath = BuildDeviceRelativePath(
                    profile,
                    item.PlatformFolder,
                    item.GameTitle,
                    Path.GetFileName(item.SourceFilePath));

                var destinationPath = Path.Combine(targetMountPath, relativePath);
                var destinationFolder = Path.GetDirectoryName(destinationPath);

                if (destinationFolder.IsNotNullOrWhiteSpace() && !_diskProvider.FolderExists(destinationFolder))
                {
                    _diskProvider.CreateFolder(destinationFolder);
                }

                _diskProvider.CopyFile(item.SourceFilePath, destinationPath, overwrite);
                result.DestinationPaths.Add(destinationPath);
                result.ExportedCount++;
            }

            return result;
        }

        private static string ResolveAlphabeticalBucket(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "#";
            }

            var firstChar = char.ToUpperInvariant(title.Trim()[0]);
            if (!char.IsLetter(firstChar))
            {
                return "#";
            }

            if (firstChar >= 'A' && firstChar <= 'F')
            {
                return "A-F";
            }

            if (firstChar >= 'G' && firstChar <= 'M')
            {
                return "G-M";
            }

            if (firstChar >= 'N' && firstChar <= 'S')
            {
                return "N-S";
            }

            return "T-Z";
        }
    }
}
