using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.Disc;

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

    public enum SdCardFormatTransformation
    {
        CopyAsIs = 0,
        ZipSingleRom = 1,
        ExtractZipToRawRom = 2,
        CompressDiscToChd = 3,
        CompressDiscToRvz = 4,
        ConvertChdToCueBin = 5,
        ConvertRvzToIso = 6
    }

    public class SdCardProfileDescriptor
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string PathTemplate { get; set; }
        public bool UsesFat32AlphabeticalBuckets { get; set; }
        public bool SupportsZipPerRom { get; set; }
        public bool SupportsChdForOpticalDiscs { get; set; }
        public bool SupportsRvzForGameCubeWii { get; set; }
        public string OptimizationOptionLabel { get; set; } = "To save room, compress or convert files to optimal formats for target device";
    }

    public class SdCardExportItem
    {
        public string SourceFilePath { get; set; }
        public string PlatformFolder { get; set; }
        public string GameTitle { get; set; }
        public string InnerRomExtensionHint { get; set; }
    }

    public class SdCardExportPlan
    {
        public SdCardFormatTransformation Transformation { get; set; }
        public string RelativeDestinationPath { get; set; }
    }

    public class SdCardExportResult
    {
        public SdCardDeviceProfile Profile { get; set; }
        public string TargetMountPath { get; set; }
        public bool OptimizeFormatsForDevice { get; set; }
        public int ExportedCount { get; set; }
        public int ExportedMediaAssetCount { get; set; }
        public List<string> DestinationPaths { get; set; } = new List<string>();
        public List<string> ExportedMediaPaths { get; set; } = new List<string>();
        public List<SdCardFormatTransformation> AppliedTransformations { get; set; } = new List<SdCardFormatTransformation>();
    }

    public interface ISdCardExporterService
    {
        IReadOnlyList<SdCardProfileDescriptor> GetSupportedProfiles();
        string BuildDeviceRelativePath(SdCardDeviceProfile profile, string platformFolder, string gameTitle, string fileName);
        SdCardExportPlan ResolveExportPlan(
            SdCardDeviceProfile profile,
            string platformFolder,
            string gameTitle,
            string sourceFileName,
            bool optimizeFormatsForDevice = false,
            string innerRomExtensionHint = null);
        SdCardExportResult ExportToSdCard(
            SdCardDeviceProfile profile,
            string targetMountPath,
            IEnumerable<SdCardExportItem> items,
            bool overwrite = false,
            bool optimizeFormatsForDevice = false);
    }

    public class SdCardExporterService : ISdCardExporterService
    {
        private static readonly IReadOnlyList<SdCardProfileDescriptor> ProfileCatalog = new List<SdCardProfileDescriptor>
        {
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.MiSTer,
                Name = "MiSTer FPGA",
                Category = "FPGA",
                PathTemplate = "games/{Core}/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.AnaloguePocket,
                Name = "Analogue Pocket (openFPGA)",
                Category = "FPGA",
                PathTemplate = "Assets/{Core}/common/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.RetroArch,
                Name = "RetroArch",
                Category = "Multi-System Frontend",
                PathTemplate = "downloads/{Platform}/{FileName}",
                UsesFat32AlphabeticalBuckets = false,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = true
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.Batocera,
                Name = "Batocera / Knulli",
                Category = "Multi-System OS",
                PathTemplate = "roms/{SystemSlug}/{FileName}",
                UsesFat32AlphabeticalBuckets = false,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = true
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.EmulationStation,
                Name = "EmulationStation DE (ES-DE / EmuDeck)",
                Category = "Multi-System Frontend",
                PathTemplate = "ROMs/{SystemSlug}/{FileName}",
                UsesFat32AlphabeticalBuckets = false,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = true
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.OnionOS,
                Name = "OnionOS (Miyoo Mini / Plus)",
                Category = "Handheld CFW",
                PathTemplate = "Roms/{OnionSystem}/{FileName}",
                UsesFat32AlphabeticalBuckets = false,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.GarlicOS,
                Name = "GarlicOS / MuOS (Anbernic RG35XX)",
                Category = "Handheld CFW",
                PathTemplate = "Roms/{OnionSystem}/{FileName}",
                UsesFat32AlphabeticalBuckets = false,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.ArkOS,
                Name = "ArkOS / ROCKNIX / JELOS",
                Category = "Handheld CFW",
                PathTemplate = "roms/{SystemSlug}/{FileName}",
                UsesFat32AlphabeticalBuckets = false,
                SupportsZipPerRom = true,
                SupportsChdForOpticalDiscs = true,
                SupportsRvzForGameCubeWii = true
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.EverDrive,
                Name = "Krikzz EverDrive (N8 / GB / GBA / Mega EverDrive Pro / 64)",
                Category = "Flash Cart",
                PathTemplate = "{Platform}/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.FXPakPro,
                Name = "FXPak Pro / SD2SNES",
                Category = "Flash Cart",
                PathTemplate = "sd2snes/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.EZFlash,
                Name = "EZ-Flash (Omega / Definitive / Junior)",
                Category = "Flash Cart",
                PathTemplate = "ROMS/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.R4,
                Name = "R4 Wood / Flashcart",
                Category = "Flash Cart",
                PathTemplate = "Games/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.TwilightMenu,
                Name = "TWiLight Menu++ (DSi / 3DS)",
                Category = "Handheld CFW / Flash Cart",
                PathTemplate = "roms/nds/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.XStation,
                Name = "xStation (PlayStation 1 ODE)",
                Category = "Optical Drive Emulator (ODE)",
                PathTemplate = "00xstation/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.SwissGameCube,
                Name = "Swiss / GC Loader / Picoboot (GameCube)",
                Category = "Optical Drive Emulator (ODE)",
                PathTemplate = "games/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.Fenrir,
                Name = "Fenrir / Saroo / Satiator (Sega Saturn)",
                Category = "Optical Drive Emulator (ODE)",
                PathTemplate = "saturn/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            },
            new SdCardProfileDescriptor
            {
                Profile = SdCardDeviceProfile.GDEMU,
                Name = "GDEMU / MODE (Sega Dreamcast)",
                Category = "Optical Drive Emulator (ODE)",
                PathTemplate = "GDEMU/{AlphaBucket}/{FileName}",
                UsesFat32AlphabeticalBuckets = true,
                SupportsZipPerRom = false,
                SupportsChdForOpticalDiscs = false,
                SupportsRvzForGameCubeWii = false
            }
        };

        private static readonly HashSet<string> CartridgeRomExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".sfc",
            ".smc",
            ".nes",
            ".fds",
            ".gb",
            ".gbc",
            ".gba",
            ".md",
            ".gen",
            ".bin",
            ".sms",
            ".gg",
            ".pce",
            ".a26",
            ".a52",
            ".a78",
            ".n64",
            ".z64",
            ".v64",
            ".nds",
            ".32x",
            ".ngp",
            ".ngc",
            ".ws",
            ".wsc"
        };

        private static readonly HashSet<string> RawOpticalDiscExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cue",
            ".iso",
            ".gdi",
            ".gcm"
        };

        private static readonly HashSet<string> ChdSupportedPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PlayStation",
            "Sony PlayStation",
            "PSX",
            "PS1",
            "PlayStation 2",
            "Sony PlayStation 2",
            "PS2",
            "PSP",
            "Sega CD",
            "Mega-CD",
            "Mega CD",
            "Sega Saturn",
            "Saturn",
            "Dreamcast",
            "Sega Dreamcast",
            "TurboGrafx-CD",
            "PC Engine CD",
            "PCECD",
            "3DO",
            "Neo Geo CD"
        };

        private static readonly HashSet<string> RvzSupportedPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GameCube",
            "Nintendo GameCube",
            "GC",
            "Wii",
            "Nintendo Wii"
        };

        private static readonly Dictionary<string, string> DefaultPlatformRawExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            { "Sega CD", "SEGACD" },
            { "Mega-CD", "SEGACD" },
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
            { "Sega CD", "MegaCD" },
            { "Mega-CD", "MegaCD" },
            { "Master System", "SMS" },
            { "Game Gear", "GameGear" },
            { "PlayStation", "PSX" },
            { "Sony PlayStation", "PSX" },
            { "Sega Saturn", "Saturn" },
            { "TurboGrafx-16", "TGFX16" },
            { "TurboGrafx-CD", "TGFX16-CD" },
            { "PC Engine CD", "TGFX16-CD" }
        };

        private static readonly string[] MediaAssetTypes = new[]
        {
            "box2d",
            "box3d",
            "screenshots",
            "titlescreens",
            "marquee",
            "videos",
            "manuals"
        };

        private static readonly HashSet<string> AlphabeticalBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "#",
            "A-F",
            "G-M",
            "N-S",
            "T-Z"
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
            { "Sega CD", "segacd" },
            { "Mega-CD", "segacd" },
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
        private readonly IDiscCompressionService _discCompressionService;
        private readonly Logger _logger;

        public SdCardExporterService(
            IDiskProvider diskProvider,
            Logger logger,
            IDiscCompressionService discCompressionService = null)
        {
            _diskProvider = diskProvider;
            _discCompressionService = discCompressionService;
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

        public SdCardExportPlan ResolveExportPlan(
            SdCardDeviceProfile profile,
            string platformFolder,
            string gameTitle,
            string sourceFileName,
            bool optimizeFormatsForDevice = false,
            string innerRomExtensionHint = null)
        {
            var safeFileName = Path.GetFileName(sourceFileName);

            if (!optimizeFormatsForDevice)
            {
                return new SdCardExportPlan
                {
                    Transformation = SdCardFormatTransformation.CopyAsIs,
                    RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, safeFileName)
                };
            }

            var descriptor = ProfileCatalog.FirstOrDefault(p => p.Profile == profile);
            var ext = Path.GetExtension(safeFileName)?.ToLowerInvariant() ?? string.Empty;
            var safePlatform = platformFolder?.Trim() ?? string.Empty;

            if (descriptor != null)
            {
                // 1. Target supports CHD on optical consoles, and source is raw disc (.cue/.iso/.gdi)
                if (descriptor.SupportsChdForOpticalDiscs &&
                    RawOpticalDiscExtensions.Contains(ext) &&
                    ChdSupportedPlatforms.Contains(safePlatform))
                {
                    var chdFileName = Path.ChangeExtension(safeFileName, ".chd");
                    return new SdCardExportPlan
                    {
                        Transformation = SdCardFormatTransformation.CompressDiscToChd,
                        RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, chdFileName)
                    };
                }

                // 2. Target supports RVZ on GameCube/Wii, and source is raw disc (.iso/.gcm)
                if (descriptor.SupportsRvzForGameCubeWii &&
                    RawOpticalDiscExtensions.Contains(ext) &&
                    RvzSupportedPlatforms.Contains(safePlatform))
                {
                    var rvzFileName = Path.ChangeExtension(safeFileName, ".rvz");
                    return new SdCardExportPlan
                    {
                        Transformation = SdCardFormatTransformation.CompressDiscToRvz,
                        RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, rvzFileName)
                    };
                }

                // 3. Target does NOT support CHD (e.g. Mega EverDrive Pro for Sega CD, xStation, Fenrir, GDEMU), and source is .chd
                if (!descriptor.SupportsChdForOpticalDiscs && ext == ".chd")
                {
                    var cueFileName = Path.ChangeExtension(safeFileName, ".cue");
                    return new SdCardExportPlan
                    {
                        Transformation = SdCardFormatTransformation.ConvertChdToCueBin,
                        RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, cueFileName)
                    };
                }

                // 4. Target does NOT support RVZ (e.g. SwissGameCube / GC Loader), and source is .rvz
                if (!descriptor.SupportsRvzForGameCubeWii && ext == ".rvz")
                {
                    var isoFileName = Path.ChangeExtension(safeFileName, ".iso");
                    return new SdCardExportPlan
                    {
                        Transformation = SdCardFormatTransformation.ConvertRvzToIso,
                        RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, isoFileName)
                    };
                }

                // 5. Target supports per-ROM zip (e.g. MiSTer, RetroArch, OnionOS, Batocera) and source is an uncompressed cartridge ROM
                if (descriptor.SupportsZipPerRom &&
                    CartridgeRomExtensions.Contains(ext) &&
                    !ChdSupportedPlatforms.Contains(safePlatform))
                {
                    var zipFileName = Path.ChangeExtension(safeFileName, ".zip");
                    return new SdCardExportPlan
                    {
                        Transformation = SdCardFormatTransformation.ZipSingleRom,
                        RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, zipFileName)
                    };
                }

                // 6. Target does NOT support .zip (e.g. Analogue Pocket openFPGA, EverDrive, FXPak Pro, EZ-Flash) and source is .zip
                if (!descriptor.SupportsZipPerRom && ext == ".zip")
                {
                    var targetExt = innerRomExtensionHint.IsNotNullOrWhiteSpace()
                        ? (innerRomExtensionHint.StartsWith(".") ? innerRomExtensionHint : "." + innerRomExtensionHint)
                        : (DefaultPlatformRawExtension.TryGetValue(safePlatform, out var defaultExt) ? defaultExt : ".rom");

                    var rawFileName = Path.ChangeExtension(safeFileName, targetExt);
                    return new SdCardExportPlan
                    {
                        Transformation = SdCardFormatTransformation.ExtractZipToRawRom,
                        RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, rawFileName)
                    };
                }
            }

            return new SdCardExportPlan
            {
                Transformation = SdCardFormatTransformation.CopyAsIs,
                RelativeDestinationPath = BuildDeviceRelativePath(profile, platformFolder, gameTitle, safeFileName)
            };
        }

        public SdCardExportResult ExportToSdCard(
            SdCardDeviceProfile profile,
            string targetMountPath,
            IEnumerable<SdCardExportItem> items,
            bool overwrite = false,
            bool optimizeFormatsForDevice = false)
        {
            if (targetMountPath.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Target mount path is required.", nameof(targetMountPath));
            }

            var result = new SdCardExportResult
            {
                Profile = profile,
                TargetMountPath = targetMountPath,
                OptimizeFormatsForDevice = optimizeFormatsForDevice
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

                var plan = ResolveExportPlan(
                    profile,
                    item.PlatformFolder,
                    item.GameTitle,
                    Path.GetFileName(item.SourceFilePath),
                    optimizeFormatsForDevice,
                    item.InnerRomExtensionHint);

                var destinationPath = Path.Combine(targetMountPath, plan.RelativeDestinationPath);
                var destinationFolder = Path.GetDirectoryName(destinationPath);

                if (destinationFolder.IsNotNullOrWhiteSpace() && !_diskProvider.FolderExists(destinationFolder))
                {
                    _diskProvider.CreateFolder(destinationFolder);
                }

                var exported = ExecuteExportTransformation(
                    plan.Transformation,
                    item,
                    destinationPath,
                    destinationFolder,
                    overwrite,
                    out var finalOutputPath);

                if (exported)
                {
                    result.DestinationPaths.Add(finalOutputPath);
                    result.AppliedTransformations.Add(plan.Transformation);
                    result.ExportedCount++;

                    ExportPlatformMediaAndGamelist(destinationFolder, item, overwrite, result);
                }
            }

            return result;
        }

        private void ExportPlatformMediaAndGamelist(
            string destinationFolder,
            SdCardExportItem item,
            bool overwrite,
            SdCardExportResult result)
        {
            if (item?.SourceFilePath == null)
            {
                return;
            }

            var romDir = Path.GetDirectoryName(item.SourceFilePath);
            if (romDir.IsNullOrWhiteSpace())
            {
                return;
            }

            var candidateSourceDirs = new List<string> { romDir };
            var parentDir = Path.GetDirectoryName(romDir);
            if (parentDir.IsNotNullOrWhiteSpace())
            {
                candidateSourceDirs.Add(parentDir);
            }

            var targetPlatformDir = AlphabeticalBuckets.Contains(Path.GetFileName(destinationFolder))
                ? Path.GetDirectoryName(destinationFolder)
                : destinationFolder;

            if (targetPlatformDir.IsNullOrWhiteSpace())
            {
                return;
            }

            foreach (var sourceDir in candidateSourceDirs)
            {
                foreach (var assetType in MediaAssetTypes)
                {
                    var sourceMediaFolder = Path.Combine(sourceDir, "media", assetType);
                    if (_diskProvider.FolderExists(sourceMediaFolder))
                    {
                        var files = _diskProvider.GetFiles(sourceMediaFolder, false);
                        if (files != null)
                        {
                            foreach (var file in files)
                            {
                                var fileName = Path.GetFileName(file);
                                var targetMediaFolder = Path.Combine(targetPlatformDir, "media", assetType);
                                var targetFilePath = Path.Combine(targetMediaFolder, fileName);

                                if (!result.ExportedMediaPaths.Any(p => p.Equals(targetFilePath, StringComparison.OrdinalIgnoreCase)))
                                {
                                    if (!_diskProvider.FolderExists(targetMediaFolder))
                                    {
                                        _diskProvider.CreateFolder(targetMediaFolder);
                                    }

                                    _diskProvider.CopyFile(file, targetFilePath, overwrite);
                                    result.ExportedMediaAssetCount++;
                                    result.ExportedMediaPaths.Add(targetFilePath);
                                }
                            }
                        }
                    }
                }

                var sourceGamelist = Path.Combine(sourceDir, "gamelist.xml");
                if (_diskProvider.FileExists(sourceGamelist))
                {
                    var targetGamelist = Path.Combine(targetPlatformDir, "gamelist.xml");
                    if (!result.ExportedMediaPaths.Any(p => p.Equals(targetGamelist, StringComparison.OrdinalIgnoreCase)))
                    {
                        _diskProvider.CopyFile(sourceGamelist, targetGamelist, overwrite);
                        result.ExportedMediaPaths.Add(targetGamelist);
                    }
                }
            }
        }

        private bool ExecuteExportTransformation(
            SdCardFormatTransformation transformation,
            SdCardExportItem item,
            string destinationPath,
            string destinationFolder,
            bool overwrite,
            out string finalOutputPath)
        {
            finalOutputPath = destinationPath;

            switch (transformation)
            {
                case SdCardFormatTransformation.CompressDiscToChd:
                case SdCardFormatTransformation.CompressDiscToRvz:
                    if (_discCompressionService != null)
                    {
                        return _discCompressionService.CompressDisc(
                            item.SourceFilePath,
                            item.PlatformFolder,
                            out finalOutputPath,
                            destinationPath);
                    }

                    _diskProvider.CopyFile(item.SourceFilePath, destinationPath, overwrite);
                    return true;

                case SdCardFormatTransformation.ConvertChdToCueBin:
                case SdCardFormatTransformation.ConvertRvzToIso:
                    if (_discCompressionService != null)
                    {
                        var baseName = Path.GetFileNameWithoutExtension(destinationPath);
                        return _discCompressionService.DecompressDisc(
                            item.SourceFilePath,
                            destinationFolder,
                            out finalOutputPath,
                            baseName);
                    }

                    _diskProvider.CopyFile(item.SourceFilePath, destinationPath, overwrite);
                    return true;

                case SdCardFormatTransformation.ZipSingleRom:
                    if (File.Exists(item.SourceFilePath))
                    {
                        if (overwrite && File.Exists(destinationPath))
                        {
                            File.Delete(destinationPath);
                        }

                        using (var archive = ZipFile.Open(destinationPath, ZipArchiveMode.Create))
                        {
                            archive.CreateEntryFromFile(item.SourceFilePath, Path.GetFileName(item.SourceFilePath), CompressionLevel.Optimal);
                        }

                        return true;
                    }

                    _diskProvider.CopyFile(item.SourceFilePath, destinationPath, overwrite);
                    return true;

                case SdCardFormatTransformation.ExtractZipToRawRom:
                    if (File.Exists(item.SourceFilePath))
                    {
                        using (var archive = ZipFile.OpenRead(item.SourceFilePath))
                        {
                            var romEntry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name));
                            if (romEntry != null)
                            {
                                var actualFileName = Path.Combine(destinationFolder ?? string.Empty, romEntry.Name);
                                romEntry.ExtractToFile(actualFileName, overwrite);
                                finalOutputPath = actualFileName;
                                return true;
                            }
                        }
                    }

                    _diskProvider.CopyFile(item.SourceFilePath, destinationPath, overwrite);
                    return true;

                default:
                    _diskProvider.CopyFile(item.SourceFilePath, destinationPath, overwrite);
                    return true;
            }
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
