using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Games.VirtualLibrary
{
    public class VirtualLibraryProfile
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string TargetPath { get; set; }
        public string HardwarePreset { get; set; } = "mister";
        public bool GroupMultiDiscFolders { get; set; } = true;
        public bool AutoSyncOnRefresh { get; set; } = true;
        public DateTime? LastSyncedAt { get; set; }
        public int LinkedConsoleCount { get; set; }
        public string LastSyncMessage { get; set; }
    }

    public class VirtualLibraryPresetInfo
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ExampleLayout { get; set; }
    }

    public interface IVirtualLibraryService
    {
        List<VirtualLibraryPresetInfo> GetPresets();
        List<VirtualLibraryProfile> GetAll();
        VirtualLibraryProfile Save(VirtualLibraryProfile profile);
        void Delete(int id);
        VirtualLibraryProfile SyncVirtualLibrary(int id);
        void SyncAllAuto();
    }

    public class VirtualLibraryService : IVirtualLibraryService, IHandle<PlatformRefreshCompleteEvent>
    {
        private static readonly List<VirtualLibraryPresetInfo> Presets = new List<VirtualLibraryPresetInfo>
        {
            new VirtualLibraryPresetInfo
            {
                Key = "mister",
                Name = "MiSTer FPGA",
                Description = "Standard MiSTer FPGA network/SD layout under /games/{Core}/",
                ExampleLayout = "{Target}/games/NES, {Target}/games/SNES, {Target}/games/Genesis, {Target}/games/PSX"
            },
            new VirtualLibraryPresetInfo
            {
                Key = "retroarch",
                Name = "RetroArch / Libretro",
                Description = "Canonical Libretro database folder names for RetroArch playlist & directory scanning",
                ExampleLayout = "{Target}/Nintendo - Nintendo Entertainment System, {Target}/Sega - Mega Drive - Genesis"
            },
            new VirtualLibraryPresetInfo
            {
                Key = "analogue_pocket",
                Name = "Analogue Pocket (openFPGA)",
                Description = "Analogue Pocket openFPGA /Assets/{core}/common/ directory structure",
                ExampleLayout = "{Target}/Assets/nes/common, {Target}/Assets/snes/common, {Target}/Assets/genesis/common"
            },
            new VirtualLibraryPresetInfo
            {
                Key = "onionos",
                Name = "OnionOS / GarlicOS Handhelds",
                Description = "Miyoo Mini & RG35XX /Roms/{SystemCode}/ folder structure",
                ExampleLayout = "{Target}/Roms/FC, {Target}/Roms/SFC, {Target}/Roms/MD, {Target}/Roms/PS"
            },
            new VirtualLibraryPresetInfo
            {
                Key = "batocera",
                Name = "Batocera / Knulli / ES-DE (EmulationStation)",
                Description = "EmulationStation & Batocera /roms/{system}/ directory conventions",
                ExampleLayout = "{Target}/roms/nes, {Target}/roms/snes, {Target}/roms/megadrive, {Target}/roms/psx"
            },
            new VirtualLibraryPresetInfo
            {
                Key = "everdrive",
                Name = "Krikzz EverDrive / Flash Carts & ODEs",
                Description = "Clean root console directories for hardware flash carts and optical drive emulators",
                ExampleLayout = "{Target}/NES, {Target}/SNES, {Target}/MegaDrive, {Target}/N64, {Target}/GBA"
            }
        };

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly ISeriesService _seriesService;
        private readonly Logger _logger;
        private readonly object _lock = new object();

        public VirtualLibraryService(
            IAppFolderInfo appFolderInfo,
            ISeriesService seriesService,
            Logger logger)
        {
            _appFolderInfo = appFolderInfo;
            _seriesService = seriesService;
            _logger = logger;
        }

        public List<VirtualLibraryPresetInfo> GetPresets()
        {
            return Presets;
        }

        public List<VirtualLibraryProfile> GetAll()
        {
            lock (_lock)
            {
                return LoadProfilesInternal();
            }
        }

        public VirtualLibraryProfile Save(VirtualLibraryProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            lock (_lock)
            {
                var list = LoadProfilesInternal();
                if (profile.Id <= 0)
                {
                    profile.Id = list.Any() ? list.Max(p => p.Id) + 1 : 1;
                    list.Add(profile);
                }
                else
                {
                    var idx = list.FindIndex(p => p.Id == profile.Id);
                    if (idx >= 0)
                    {
                        list[idx] = profile;
                    }
                    else
                    {
                        list.Add(profile);
                    }
                }

                SaveProfilesInternal(list);
            }

            return SyncVirtualLibrary(profile.Id);
        }

        public void Delete(int id)
        {
            lock (_lock)
            {
                var list = LoadProfilesInternal();
                list.RemoveAll(p => p.Id == id);
                SaveProfilesInternal(list);
            }
        }

        public VirtualLibraryProfile SyncVirtualLibrary(int id)
        {
            VirtualLibraryProfile profile;
            lock (_lock)
            {
                var list = LoadProfilesInternal();
                profile = list.FirstOrDefault(p => p.Id == id);
                if (profile == null)
                {
                    return null;
                }
            }

            if (string.IsNullOrWhiteSpace(profile.TargetPath))
            {
                return profile;
            }

            try
            {
                Directory.CreateDirectory(profile.TargetPath);
                var allPlatforms = _seriesService?.GetAllSeries() ?? new List<Series>();
                var linkedCount = 0;
                var expectedLinkPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var platform in allPlatforms)
                {
                    if (string.IsNullOrWhiteSpace(platform.Path) || !Directory.Exists(platform.Path))
                    {
                        continue;
                    }

                    var canonicalSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(
                        platform.TitleSlug,
                        platform.Title);

                    if (!IsPlatformSupportedByPreset(profile.HardwarePreset, canonicalSlug))
                    {
                        continue;
                    }

                    var relativeTargetFolder = MapPlatformToPresetRelativePath(
                        profile.HardwarePreset,
                        canonicalSlug,
                        platform.TitleSlug ?? platform.Title);

                    var linkFullPath = Path.GetFullPath(Path.Combine(profile.TargetPath, relativeTargetFolder));
                    expectedLinkPaths.Add(linkFullPath);

                    EnsureParentRealDirectories(profile.TargetPath, linkFullPath);

                    var parentDir = Path.GetDirectoryName(linkFullPath);
                    if (!string.IsNullOrWhiteSpace(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    if (TryCreateDirectorySymlinkOrJunction(linkFullPath, platform.Path))
                    {
                        linkedCount++;
                    }
                }

                PruneStaleSymlinks(profile.TargetPath, expectedLinkPaths);

                profile.LinkedConsoleCount = linkedCount;
                profile.LastSyncedAt = DateTime.UtcNow;
                profile.LastSyncMessage = $"Synced {linkedCount} console folder{(linkedCount == 1 ? string.Empty : "s")} via symlinks.";
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to sync Virtual Library '{0}' at {1}", profile.Name, profile.TargetPath);
                profile.LastSyncMessage = $"Sync error: {ex.Message}";
            }

            lock (_lock)
            {
                var list = LoadProfilesInternal();
                var idx = list.FindIndex(p => p.Id == profile.Id);
                if (idx >= 0)
                {
                    list[idx] = profile;
                    SaveProfilesInternal(list);
                }
            }

            return profile;
        }

        public void SyncAllAuto()
        {
            var autoProfiles = GetAll().Where(p => p.AutoSyncOnRefresh).ToList();
            foreach (var profile in autoProfiles)
            {
                SyncVirtualLibrary(profile.Id);
            }
        }

        public void Handle(PlatformRefreshCompleteEvent message)
        {
            SyncAllAuto();
        }

        private static readonly Dictionary<string, HashSet<string>> PresetPlatformRestrictions =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["mister"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "nes", "fds", "snes", "satellaview", "sufamiturbo", "sgb", "n64", "64dd",
                    "gb", "gbc", "gba", "gameandwatch", "pokemonmini", "pokemini",
                    "sms", "gg", "gamegear", "sg1000", "genesis", "32x", "sega32x", "segacd", "saturn",
                    "ps1", "tg16", "tgcd", "supergrafx", "neogeo", "neogeocd",
                    "atari2600", "atari5200", "atari7800", "lynx", "jaguar", "jaguarcd",
                    "colecovision", "intellivision", "vectrex", "ws", "wonderswan", "wonderswancolor", "pcfx",
                    "supervision", "arduboy", "arcadia2001", "odyssey2", "videopacplus", "creativision", "vc4000", "cdi",
                    "cd32", "channelf", "scv", "pv1000", "gx4000", "studio2", "gamemaster", "adventurevision"
                },
                ["analogue_pocket"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "gb", "gbc", "gba", "ereader", "nes", "fds", "snes", "satellaview", "sufamiturbo",
                    "sms", "gg", "gamegear", "sg1000", "genesis",
                    "tg16", "supergrafx", "tgcd", "neogeo",
                    "atari2600", "atari5200", "atari7800", "lynx",
                    "ws", "wonderswan", "wonderswancolor", "pocketchallengev2", "ngp", "ngpc",
                    "colecovision", "intellivision", "supervision", "arduboy", "pokemini", "pokemonmini",
                    "virtualboy", "vb", "creativision", "arcadia2001", "odyssey2", "videopacplus", "channelf", "gameandwatch",
                    "studio2", "gamemaster", "adventurevision", "pv1000", "scv"
                },
                ["onionos"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "gb", "gbc", "gba", "ereader", "nes", "fds", "snes", "satellaview", "sufamiturbo", "sgb",
                    "sms", "gg", "gamegear", "sg1000", "genesis", "32x", "sega32x", "segacd", "pico",
                    "ps1", "tg16", "tgcd", "supergrafx", "neogeo", "neogeocd",
                    "atari2600", "atari5200", "atari7800", "lynx",
                    "colecovision", "intellivision", "vectrex",
                    "ws", "wonderswan", "wonderswancolor", "pocketchallengev2", "ngp", "ngpc",
                    "supervision", "arduboy", "gameandwatch",
                    "odyssey2", "videopacplus", "channelf", "pokemini", "pokemonmini", "virtualboy", "vb", "ds", "nds", "dsiware",
                    "scv", "gx4000", "arcadia2001", "creativision", "studio2", "vc4000", "pv1000", "gamemaster", "adventurevision"
                },
                ["everdrive"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "nes", "fds", "snes", "satellaview", "sufamiturbo", "sgb", "n64", "64dd",
                    "gb", "gbc", "gba", "ereader", "nds",
                    "sms", "gg", "gamegear", "sg1000", "genesis", "32x", "sega32x", "segacd", "pico",
                    "tg16", "tgcd", "supergrafx", "neogeo", "neogeocd",
                    "atari2600", "atari5200", "atari7800", "lynx", "jaguar", "jaguarcd", "colecovision", "intellivision",
                    "ws", "wonderswan", "wonderswancolor", "ngp", "ngpc", "channelf", "vectrex", "vb", "virtualboy",
                    "odyssey2", "videopacplus",
                    "ps1", "saturn", "dreamcast", "gamecube", "3do", "pcfx", "cd32", "cdi"
                }
            };

        public static bool IsPlatformSupportedByPreset(string preset, string canonicalSlug)
        {
            if (string.IsNullOrWhiteSpace(canonicalSlug))
            {
                return false;
            }

            if (PresetPlatformRestrictions.TryGetValue(preset ?? "mister", out var allowed))
            {
                return allowed.Contains(canonicalSlug);
            }

            return true;
        }

        internal static string ResolvePresetRelativePath(string preset, string canonicalSlug, string slugOrTitle = null)
        {
            return MapPlatformToPresetRelativePath(preset, canonicalSlug, slugOrTitle);
        }

        private static string MapPlatformToPresetRelativePath(string preset, string canonicalSlug, string slugOrTitle)
        {
            var norm = !string.IsNullOrWhiteSpace(canonicalSlug)
                ? canonicalSlug.ToLowerInvariant()
                : (slugOrTitle ?? string.Empty).ToLowerInvariant();

            switch ((preset ?? "mister").ToLowerInvariant())
            {
                case "mister":
                    return Path.Combine("games", GetMisterCoreRelativePath(norm));
                case "retroarch":
                    return GetRetroArchSystemName(norm);
                case "analogue_pocket":
                    return GetPocketRelativePath(norm);
                case "onionos":
                    return Path.Combine("Roms", GetOnionSystemCode(norm));
                case "batocera":
                    return Path.Combine("roms", GetBatoceraSystemSlug(norm));
                case "everdrive":
                    return GetMisterCoreName(norm);
                default:
                    return Path.Combine("games", GetMisterCoreRelativePath(norm));
            }
        }

        private static string GetMisterCoreRelativePath(string norm)
        {
            switch (norm)
            {
                case "nes":
                    return Path.Combine("NES", "NES");
                case "fds":
                    return Path.Combine("NES", "FDS");
                case "snes":
                    return Path.Combine("SNES", "SNES");
                case "satellaview":
                    return Path.Combine("SNES", "Satellaview");
                case "sufamiturbo":
                    return Path.Combine("SNES", "SufamiTurbo");
                case "sgb":
                    return "SGB";
                case "n64":
                    return Path.Combine("N64", "N64");
                case "64dd":
                    return Path.Combine("N64", "64DD");
                case "gb":
                    return Path.Combine("GAMEBOY", "GB");
                case "gbc":
                    return Path.Combine("GAMEBOY", "GBC");
                case "sms":
                    return Path.Combine("SMS", "SMS");
                case "gg":
                case "gamegear":
                    return Path.Combine("SMS", "GameGear");
                case "sg1000":
                    return Path.Combine("SMS", "SG1000");
                case "atari7800":
                    return Path.Combine("Atari7800", "Atari7800");
                case "atari2600":
                    return Path.Combine("Atari7800", "Atari2600");
                case "tg16":
                    return Path.Combine("TGFX16", "TGFX16");
                case "supergrafx":
                    return Path.Combine("TGFX16", "SuperGrafx");
                case "tgcd":
                    return "TGFX16-CD";
                case "ws":
                    return Path.Combine("WonderSwan", "WonderSwan");
                case "wonderswan":
                case "wonderswancolor":
                    return Path.Combine("WonderSwan", "WonderSwanColor");
                case "odyssey2":
                    return Path.Combine("ODYSSEY2", "Odyssey2");
                case "videopacplus":
                    return Path.Combine("ODYSSEY2", "VideopacPlus");
                default:
                    return GetMisterCoreName(norm);
            }
        }

        private static string GetPocketRelativePath(string norm)
        {
            switch (norm)
            {
                case "nes":
                    return Path.Combine("Assets", "nes", "common", "NES");
                case "fds":
                    return Path.Combine("Assets", "nes", "common", "Famicom Disk System");
                case "snes":
                    return Path.Combine("Assets", "snes", "common", "SNES");
                case "satellaview":
                    return Path.Combine("Assets", "snes", "common", "Satellaview");
                case "sufamiturbo":
                    return Path.Combine("Assets", "snes", "common", "SufamiTurbo");
                case "gba":
                    return Path.Combine("Assets", "gba", "common", "GBA");
                case "ereader":
                    return Path.Combine("Assets", "gba", "common", "e-Reader");
                case "tg16":
                    return Path.Combine("Assets", "pce", "common", "PCE");
                case "supergrafx":
                    return Path.Combine("Assets", "pce", "common", "SuperGrafx");
                case "tgcd":
                    return Path.Combine("Assets", "pcecd", "common");
                case "ws":
                    return Path.Combine("Assets", "wonderswan", "common", "WonderSwan");
                case "wonderswan":
                case "wonderswancolor":
                    return Path.Combine("Assets", "wonderswan", "common", "WonderSwan Color");
                case "pocketchallengev2":
                    return Path.Combine("Assets", "wonderswan", "common", "Pocket Challenge V2");
                case "ngp":
                    return Path.Combine("Assets", "ngp", "common", "NGP");
                case "ngpc":
                    return Path.Combine("Assets", "ngp", "common", "NGPC");
                case "odyssey2":
                    return Path.Combine("Assets", "odyssey2", "common", "Odyssey2");
                case "videopacplus":
                    return Path.Combine("Assets", "odyssey2", "common", "VideopacPlus");
                default:
                    return Path.Combine("Assets", GetPocketCoreName(norm), "common");
            }
        }

        private static string GetMisterCoreName(string norm)
        {
            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "SNES";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "NES";
            }

            if (norm.Contains("famicom-disk") || norm == "fds")
            {
                return "FDS";
            }

            if (norm == "satellaview")
            {
                return "Satellaview";
            }

            if (norm == "sufamiturbo")
            {
                return "SufamiTurbo";
            }

            if (norm == "sgb")
            {
                return "SGB";
            }

            if (norm.Contains("nintendo-64") || norm == "n64")
            {
                return "N64";
            }

            if (norm == "64dd")
            {
                return "64DD";
            }

            if (norm == "ereader")
            {
                return "eReader";
            }

            if (norm == "nds" || norm == "ds")
            {
                return "NDS";
            }

            if (norm.Contains("game-boy-advance") || norm == "gba")
            {
                return "GBA";
            }

            if (norm.Contains("game-boy-color") || norm == "gbc")
            {
                return "GBC";
            }

            if (norm.Contains("game-boy") || norm == "gb")
            {
                return "Gameboy";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive") || norm == "md" || norm == "megadrive")
            {
                return "Genesis";
            }

            if (norm.Contains("sega-cd") || norm.Contains("mega-cd") || norm == "segacd" || norm == "megacd" || norm == "scd")
            {
                return "MegaCD";
            }

            if (norm.Contains("32x"))
            {
                return "S32X";
            }

            if (norm.Contains("master-system") || norm == "sms")
            {
                return "SMS";
            }

            if (norm == "gg" || norm.Contains("gamegear") || norm.Contains("game-gear"))
            {
                return "GameGear";
            }

            if (norm == "sg1000")
            {
                return "SG1000";
            }

            if (norm == "pico")
            {
                return "Pico";
            }

            if (norm == "supervision")
            {
                return "Supervision";
            }

            if (norm == "arduboy")
            {
                return "Arduboy";
            }

            if (norm == "arcadia2001")
            {
                return "Arcadia";
            }

            if (norm == "creativision")
            {
                return "CreatiVision";
            }

            if (norm == "odyssey2")
            {
                return "ODYSSEY2";
            }

            if (norm == "videopacplus")
            {
                return "VideopacPlus";
            }

            if (norm == "vc4000")
            {
                return "VC4000";
            }

            if (norm == "channelf")
            {
                return "ChannelF";
            }

            if (norm == "cdi")
            {
                return "CDi";
            }

            if (norm == "cd32")
            {
                return "Amiga";
            }

            if (norm == "3do")
            {
                return "3DO";
            }

            if (norm == "gamecube" || norm == "gc")
            {
                return "GameCube";
            }

            if (norm == "dreamcast")
            {
                return "Dreamcast";
            }

            if (norm == "pcfx")
            {
                return "PCFX";
            }

            if (norm == "scv")
            {
                return "SCV";
            }

            if (norm == "pv1000")
            {
                return "PV1000";
            }

            if (norm == "gx4000")
            {
                return "Amstrad";
            }

            if (norm == "studio2")
            {
                return "Studio2";
            }

            if (norm == "gamemaster")
            {
                return "GameMaster";
            }

            if (norm == "adventurevision")
            {
                return "AdventureVision";
            }

            if (norm == "atari2600")
            {
                return "Atari2600";
            }

            if (norm == "atari5200")
            {
                return "Atari5200";
            }

            if (norm == "atari7800")
            {
                return "Atari7800";
            }

            if (norm == "lynx")
            {
                return "AtariLynx";
            }

            if (norm == "jaguar")
            {
                return "Jaguar";
            }

            if (norm == "jaguarcd")
            {
                return "JaguarCD";
            }

            if (norm == "colecovision")
            {
                return "Coleco";
            }

            if (norm == "intellivision")
            {
                return "Intellivision";
            }

            if (norm == "vectrex")
            {
                return "Vectrex";
            }

            if (norm == "ws")
            {
                return "WonderSwan";
            }

            if (norm == "wonderswan" || norm == "wonderswancolor")
            {
                return "WonderSwanColor";
            }

            if (norm == "ngp")
            {
                return "NGP";
            }

            if (norm == "ngpc")
            {
                return "NGPC";
            }

            if (norm == "vb" || norm == "virtualboy")
            {
                return "VirtualBoy";
            }

            if (norm == "pokemonmini" || norm == "pokemini")
            {
                return "PokemonMini";
            }

            if (norm == "gameandwatch")
            {
                return "GameNWatch";
            }

            if (norm.Contains("saturn"))
            {
                return "Saturn";
            }

            if (norm.Contains("playstation-2") || norm == "ps2")
            {
                return "PS2";
            }

            if (norm.Contains("playstation") || norm == "ps1" || norm == "psx")
            {
                return "PSX";
            }

            if (norm.Contains("turbografx-cd") || norm == "tgcd" || norm == "pcecd")
            {
                return "TGFX16-CD";
            }

            if (norm.Contains("supergrafx"))
            {
                return "SuperGrafx";
            }

            if (norm.Contains("turbografx") || norm == "tg16" || norm == "pce")
            {
                return "TGFX16";
            }

            if (norm.Contains("neo-geo-cd") || norm == "neogeocd")
            {
                return "NeoGeoCD";
            }

            if (norm.Contains("neo-geo") || norm == "neogeo")
            {
                return "NeoGeo";
            }

            return norm.ToUpperInvariant();
        }

        private static string GetRetroArchSystemName(string norm)
        {
            if (norm == "switcheshop")
            {
                return "Nintendo - Switch (Digital)";
            }

            if (MetadataSource.Game.GameCatalogProxy.TryResolveBuiltInPlatform(0, norm, norm, out var builtIn) &&
                !string.IsNullOrWhiteSpace(builtIn.LibretroSystemName))
            {
                return builtIn.LibretroSystemName;
            }

            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "Nintendo - Super Nintendo Entertainment System";
            }

            if (norm == "sufamiturbo")
            {
                return "Nintendo - Sufami Turbo";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "Nintendo - Nintendo Entertainment System";
            }

            if (norm.Contains("famicom-disk") || norm == "fds")
            {
                return "Nintendo - Family Computer Disk System";
            }

            if (norm.Contains("nintendo-64") || norm == "n64")
            {
                return "Nintendo - Nintendo 64";
            }

            if (norm.Contains("game-boy-advance") || norm == "gba")
            {
                return "Nintendo - Game Boy Advance";
            }

            if (norm.Contains("game-boy-color") || norm == "gbc")
            {
                return "Nintendo - Game Boy Color";
            }

            if (norm.Contains("game-boy") || norm == "gb")
            {
                return "Nintendo - Game Boy";
            }

            if (norm == "nds" || norm == "ds")
            {
                return "Nintendo - Nintendo DS";
            }

            if (norm == "3ds")
            {
                return "Nintendo - Nintendo 3DS";
            }

            if (norm == "gamecube" || norm == "gc")
            {
                return "Nintendo - GameCube";
            }

            if (norm == "wii")
            {
                return "Nintendo - Wii";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive") || norm == "md" || norm == "megadrive")
            {
                return "Sega - Mega Drive - Genesis";
            }

            if (norm.Contains("sega-cd") || norm.Contains("mega-cd") || norm == "segacd" || norm == "megacd" || norm == "scd")
            {
                return "Sega - Mega-CD - Sega CD";
            }

            if (norm.Contains("32x"))
            {
                return "Sega - 32X";
            }

            if (norm.Contains("master-system") || norm == "sms")
            {
                return "Sega - Master System - Mark III";
            }

            if (norm == "gg" || norm.Contains("gamegear") || norm.Contains("game-gear"))
            {
                return "Sega - Game Gear";
            }

            if (norm == "sg1000")
            {
                return "Sega - SG-1000";
            }

            if (norm == "pico")
            {
                return "Sega - PICO";
            }

            if (norm.Contains("saturn"))
            {
                return "Sega - Saturn";
            }

            if (norm.Contains("dreamcast"))
            {
                return "Sega - Dreamcast";
            }

            if (norm.Contains("playstation-2") || norm == "ps2")
            {
                return "Sony - PlayStation 2";
            }

            if (norm.Contains("playstation-portable") || norm == "psp")
            {
                return "Sony - PlayStation Portable";
            }

            if (norm == "ps1" || norm == "psx" || (norm.Contains("playstation") && !norm.Contains('2') && !norm.Contains('3') && !norm.Contains("portable") && !norm.Contains("vita")))
            {
                return "Sony - PlayStation";
            }

            if (norm.Contains("turbografx-cd") || norm == "tgcd" || norm == "pcecd")
            {
                return "NEC - PC Engine CD - TurboGrafx-CD";
            }

            if (norm == "supergrafx")
            {
                return "NEC - PC Engine SuperGrafx";
            }

            if (norm.Contains("turbografx-16") || norm.Contains("turbografx") || norm == "tg16" || norm == "pce")
            {
                return "NEC - PC Engine - TurboGrafx 16";
            }

            if (norm == "ngp")
            {
                return "SNK - Neo Geo Pocket";
            }

            if (norm.Contains("neo-geo-pocket") || norm == "ngpc")
            {
                return "SNK - Neo Geo Pocket Color";
            }

            if (norm.Contains("neo-geo-cd") || norm == "neogeocd")
            {
                return "SNK - Neo Geo CD";
            }

            if (norm.Contains("neo-geo") || norm == "neogeo")
            {
                return "SNK - Neo Geo";
            }

            if (norm == "ws")
            {
                return "Bandai - WonderSwan";
            }

            if (norm == "wonderswan" || norm == "wonderswancolor")
            {
                return "Bandai - WonderSwan Color";
            }

            if (norm == "cd32")
            {
                return "Commodore - CD32";
            }

            if (norm == "supervision")
            {
                return "Watara - Supervision";
            }

            if (norm == "arduboy")
            {
                return "Arduboy Inc - Arduboy";
            }

            if (norm == "gameandwatch")
            {
                return "Handheld Electronic Game";
            }

            if (norm == "arcadia2001")
            {
                return "Emerson - Arcadia 2001";
            }

            if (norm == "scv")
            {
                return "Epoch - Super Cassette Vision";
            }

            if (norm == "gx4000")
            {
                return "Amstrad - GX4000";
            }

            if (norm == "videopacplus")
            {
                return "Philips - Videopac+";
            }

            if (norm == "creativision")
            {
                return "VTech - CreatiVision";
            }

            if (norm == "3do")
            {
                return "The 3DO Company - 3DO";
            }

            if (norm == "atari2600")
            {
                return "Atari - 2600";
            }

            if (norm == "atari7800")
            {
                return "Atari - 7800";
            }

            if (norm == "lynx")
            {
                return "Atari - Lynx";
            }

            return norm;
        }

        private static string GetPocketCoreName(string norm)
        {
            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "snes";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "nes";
            }

            if (norm.Contains("famicom-disk") || norm == "fds")
            {
                return "fds";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive") || norm == "md" || norm == "megadrive")
            {
                return "genesis";
            }

            if (norm.Contains("master-system") || norm == "sms")
            {
                return "sms";
            }

            if (norm == "gg" || norm.Contains("gamegear") || norm.Contains("game-gear"))
            {
                return "gg";
            }

            if (norm == "sg1000")
            {
                return "sg1000";
            }

            if (norm.Contains("game-boy-advance") || norm == "gba")
            {
                return "gba";
            }

            if (norm.Contains("game-boy-color") || norm == "gbc")
            {
                return "gbc";
            }

            if (norm.Contains("game-boy") || norm == "gb")
            {
                return "gb";
            }

            if (norm.Contains("turbografx") || norm == "tg16" || norm == "pce" || norm == "supergrafx")
            {
                return "pce";
            }

            if (norm.Contains("neo-geo-pocket") || norm == "ngpc" || norm == "ngp")
            {
                return "ngp";
            }

            if (norm.Contains("neo-geo") || norm == "neogeo")
            {
                return "ng";
            }

            if (norm.Contains("wonderswan") || norm == "ws" || norm == "wsc")
            {
                return "wonderswan";
            }

            if (norm == "supervision")
            {
                return "supervision";
            }

            if (norm == "arduboy")
            {
                return "arduboy";
            }

            if (norm == "gameandwatch")
            {
                return "gameandwatch";
            }

            if (norm == "creativision")
            {
                return "creativision";
            }

            if (norm == "arcadia2001")
            {
                return "arcadia2001";
            }

            if (norm == "videopacplus" || norm == "odyssey2")
            {
                return "odyssey2";
            }

            if (norm == "channelf")
            {
                return "channelf";
            }

            if (norm == "tgcd" || norm == "pcecd")
            {
                return "pcecd";
            }

            if (norm == "studio2")
            {
                return "studio2";
            }

            if (norm == "gamemaster")
            {
                return "gamemaster";
            }

            if (norm == "adventurevision")
            {
                return "adventurevision";
            }

            if (norm == "pv1000")
            {
                return "pv1000";
            }

            if (norm == "scv")
            {
                return "scv";
            }

            if (norm == "atari2600" || norm.Contains("atari-2600"))
            {
                return "2600";
            }

            if (norm == "atari5200" || norm.Contains("atari-5200"))
            {
                return "5200";
            }

            if (norm == "atari7800" || norm.Contains("atari-7800"))
            {
                return "7800";
            }

            if (norm.Contains("lynx"))
            {
                return "lynx";
            }

            if (norm.Contains("coleco") || norm == "colecovision")
            {
                return "coleco";
            }

            if (norm.Contains("intellivision"))
            {
                return "intv";
            }

            if (norm.Contains("pokemon-mini") || norm == "pokemonmini" || norm == "pokemini")
            {
                return "poke";
            }

            if (norm.Contains("virtual-boy") || norm == "vb" || norm == "virtualboy")
            {
                return "vb";
            }

            return norm;
        }

        private static string GetOnionSystemCode(string norm)
        {
            if (norm == "satellaview")
            {
                return "SATELLAVIEW";
            }

            if (norm == "sufamiturbo")
            {
                return "SUFAMI";
            }

            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "SFC";
            }

            if (norm.Contains("famicom-disk") || norm == "fds")
            {
                return "FDS";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "FC";
            }

            if (norm == "pico")
            {
                return "PICO";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive") || norm == "md" || norm == "megadrive")
            {
                return "MD";
            }

            if (norm.Contains("sega-cd") || norm.Contains("mega-cd") || norm == "segacd" || norm == "megacd" || norm == "scd")
            {
                return "SEGACD";
            }

            if (norm.Contains("32x"))
            {
                return "THIRTYTWOX";
            }

            if (norm.Contains("master-system") || norm == "sms")
            {
                return "MS";
            }

            if (norm == "gg" || norm.Contains("gamegear") || norm.Contains("game-gear"))
            {
                return "GG";
            }

            if (norm == "sg1000")
            {
                return "SG";
            }

            if (norm == "ereader")
            {
                return "EREADER";
            }

            if (norm.Contains("game-boy-advance") || norm == "gba")
            {
                return "GBA";
            }

            if (norm.Contains("game-boy-color") || norm == "gbc")
            {
                return "GBC";
            }

            if (norm.Contains("game-boy") || norm == "gb")
            {
                return "GB";
            }

            if (norm == "dsiware")
            {
                return "DSIWARE";
            }

            if (norm == "nds" || norm == "ds")
            {
                return "NDS";
            }

            if (norm.Contains("virtual-boy") || norm == "vb" || norm == "virtualboy")
            {
                return "VB";
            }

            if (norm.Contains("playstation") || norm == "ps1" || norm == "psx")
            {
                return "PS";
            }

            if (norm.Contains("turbografx-cd") || norm == "tgcd" || norm == "pcecd")
            {
                return "PCECD";
            }

            if (norm == "supergrafx")
            {
                return "SGFX";
            }

            if (norm.Contains("turbografx") || norm == "tg16" || norm == "pce")
            {
                return "PCE";
            }

            if (norm == "ngpc")
            {
                return "NGPC";
            }

            if (norm.Contains("neo-geo-pocket") || norm == "ngp")
            {
                return "NGP";
            }

            if (norm.Contains("neo-geo-cd") || norm == "neogeocd")
            {
                return "NEOCD";
            }

            if (norm.Contains("neo-geo") || norm == "neogeo")
            {
                return "NEOGEO";
            }

            if (norm == "ws")
            {
                return "WS";
            }

            if (norm == "wonderswan" || norm == "wonderswancolor")
            {
                return "WSC";
            }

            if (norm == "pocketchallengev2")
            {
                return "PC2";
            }

            if (norm == "supervision")
            {
                return "SUPERVISION";
            }

            if (norm == "arduboy")
            {
                return "ARDUBOY";
            }

            if (norm == "gameandwatch")
            {
                return "GW";
            }

            if (norm == "odyssey2")
            {
                return "ODYSSEY";
            }

            if (norm == "videopacplus")
            {
                return "VIDEOPAC";
            }

            if (norm == "channelf")
            {
                return "CHANNELF";
            }

            if (norm == "atari2600" || norm.Contains("atari-2600"))
            {
                return "ATARI";
            }

            if (norm == "atari5200" || norm.Contains("atari-5200"))
            {
                return "FIFTYTWOHUNDRED";
            }

            if (norm == "atari7800" || norm.Contains("atari-7800"))
            {
                return "SEVENTYEIGHTHUNDRED";
            }

            if (norm.Contains("lynx"))
            {
                return "LYNX";
            }

            if (norm == "colecovision")
            {
                return "COLECO";
            }

            if (norm == "pokemonmini" || norm == "pokemini")
            {
                return "POKE";
            }

            if (norm == "arcadia2001")
            {
                return "ARCADIA";
            }

            if (norm == "creativision")
            {
                return "CRVISION";
            }

            if (norm == "gamemaster")
            {
                return "GMASTER";
            }

            if (norm == "adventurevision")
            {
                return "ADVISION";
            }

            if (norm == "scv")
            {
                return "SCV";
            }

            if (norm == "gx4000")
            {
                return "GX4000";
            }

            if (norm == "studio2")
            {
                return "STUDIO2";
            }

            if (norm == "vc4000")
            {
                return "VC4000";
            }

            if (norm == "pv1000")
            {
                return "PV1000";
            }

            return norm.ToUpperInvariant();
        }

        private static string GetBatoceraSystemSlug(string norm)
        {
            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "snes";
            }

            if (norm == "sufamiturbo")
            {
                return "sufami";
            }

            if (norm.Contains("famicom-disk") || norm == "fds")
            {
                return "fds";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "nes";
            }

            if (norm.Contains("nintendo-64") || norm == "n64")
            {
                return "n64";
            }

            if (norm == "64dd")
            {
                return "n64dd";
            }

            if (norm.Contains("game-boy-advance") || norm == "gba")
            {
                return "gba";
            }

            if (norm.Contains("game-boy-color") || norm == "gbc")
            {
                return "gbc";
            }

            if (norm.Contains("game-boy") || norm == "gb")
            {
                return "gb";
            }

            if (norm == "nds" || norm == "ds")
            {
                return "nds";
            }

            if (norm == "3ds")
            {
                return "3ds";
            }

            if (norm == "gamecube" || norm == "gc")
            {
                return "gc";
            }

            if (norm == "wii")
            {
                return "wii";
            }

            if (norm == "wiiu")
            {
                return "wiiu";
            }

            if (norm == "switch")
            {
                return "switch";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive") || norm == "md" || norm == "megadrive")
            {
                return "megadrive";
            }

            if (norm.Contains("sega-cd") || norm.Contains("mega-cd") || norm == "segacd" || norm == "megacd" || norm == "scd")
            {
                return "segacd";
            }

            if (norm.Contains("32x"))
            {
                return "sega32x";
            }

            if (norm.Contains("master-system") || norm == "sms")
            {
                return "mastersystem";
            }

            if (norm == "gg" || norm.Contains("gamegear") || norm.Contains("game-gear"))
            {
                return "gamegear";
            }

            if (norm == "sg1000")
            {
                return "sg1000";
            }

            if (norm == "pico")
            {
                return "pico";
            }

            if (norm.Contains("playstation-2") || norm == "ps2")
            {
                return "ps2";
            }

            if (norm.Contains("playstation-3") || norm == "ps3")
            {
                return "ps3";
            }

            if (norm.Contains("playstation-portable") || norm == "psp")
            {
                return "psp";
            }

            if (norm == "ps1" || norm == "psx" || (norm.Contains("playstation") && !norm.Contains('2') && !norm.Contains('3') && !norm.Contains("portable") && !norm.Contains("vita")))
            {
                return "psx";
            }

            if (norm.Contains("turbografx-cd") || norm == "tgcd" || norm == "pcecd")
            {
                return "pcenginecd";
            }

            if (norm == "supergrafx")
            {
                return "supergrafx";
            }

            if (norm.Contains("turbografx-16") || norm.Contains("turbografx") || norm == "tg16" || norm == "pce")
            {
                return "pcengine";
            }

            if (norm.Contains("neo-geo-cd") || norm == "neogeocd")
            {
                return "neogeocd";
            }

            if (norm == "ngp")
            {
                return "ngp";
            }

            if (norm == "ngpc")
            {
                return "ngpc";
            }

            if (norm.Contains("neo-geo") || norm == "neogeo")
            {
                return "neogeo";
            }

            if (norm == "ws")
            {
                return "wswan";
            }

            if (norm == "wonderswan" || norm == "wonderswancolor")
            {
                return "wswanc";
            }

            if (norm == "cd32")
            {
                return "amigacd32";
            }

            if (norm == "supervision")
            {
                return "supervision";
            }

            if (norm == "arduboy")
            {
                return "arduboy";
            }

            if (norm == "gameandwatch")
            {
                return "lcdgames";
            }

            if (norm == "arcadia2001")
            {
                return "arcadia";
            }

            if (norm == "scv")
            {
                return "scv";
            }

            if (norm == "gx4000")
            {
                return "gx4000";
            }

            if (norm == "videopacplus")
            {
                return "videopacplus";
            }

            if (norm == "odyssey2")
            {
                return "o2em";
            }

            if (norm == "creativision")
            {
                return "crvision";
            }

            if (norm == "jaguar")
            {
                return "atarijaguar";
            }

            if (norm == "jaguarcd")
            {
                return "atarijaguarcd";
            }

            if (norm == "vb" || norm == "virtualboy")
            {
                return "virtualboy";
            }

            if (norm == "pokemonmini" || norm == "pokemini")
            {
                return "pokemini";
            }

            if (norm == "superacan")
            {
                return "supracan";
            }

            if (norm == "gamemaster")
            {
                return "gmaster";
            }

            if (norm == "adventurevision")
            {
                return "advision";
            }

            return norm;
        }

        private void EnsureParentRealDirectories(string targetRoot, string linkFullPath)
        {
            try
            {
                var rootFull = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var current = Path.GetDirectoryName(linkFullPath);

                while (!string.IsNullOrWhiteSpace(current))
                {
                    var trimmed = current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(trimmed, rootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (Directory.Exists(trimmed))
                    {
                        var dirInfo = new DirectoryInfo(trimmed);
                        if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            Directory.Delete(trimmed);
                        }
                    }

                    current = Path.GetDirectoryName(trimmed);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed ensuring parent directories for {0}", linkFullPath);
            }
        }

        private void PruneStaleSymlinks(string targetRoot, HashSet<string> expectedLinkPaths)
        {
            try
            {
                if (!Directory.Exists(targetRoot))
                {
                    return;
                }

                PruneStaleSymlinksRecursive(targetRoot, expectedLinkPaths, isRoot: true);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed pruning stale symlinks in {0}", targetRoot);
            }
        }

        private void PruneStaleSymlinksRecursive(string dir, HashSet<string> expectedLinkPaths, bool isRoot)
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                var fullSubDir = Path.GetFullPath(subDir);
                var info = new DirectoryInfo(fullSubDir);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    if (!expectedLinkPaths.Contains(fullSubDir))
                    {
                        Directory.Delete(fullSubDir);
                    }
                }
                else
                {
                    PruneStaleSymlinksRecursive(fullSubDir, expectedLinkPaths, isRoot: false);

                    if (!isRoot && !Directory.EnumerateFileSystemEntries(fullSubDir).Any())
                    {
                        Directory.Delete(fullSubDir);
                    }
                }
            }
        }

        private static readonly char[] UnsafeWindowsPathChars = new[] { '"', '%', '&', '|', '<', '>', '^', '!', ';', '\r', '\n', '\0' };

        internal static bool IsSafeWindowsJunctionPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                return false;
            }

            return path.IndexOfAny(UnsafeWindowsPathChars) < 0;
        }

        private bool TryCreateDirectorySymlinkOrJunction(string linkPath, string targetPath)
        {
            try
            {
                if (Directory.Exists(linkPath))
                {
                    var dirInfo = new DirectoryInfo(linkPath);
                    if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        Directory.Delete(linkPath);
                    }
                    else
                    {
                        return true;
                    }
                }

                Directory.CreateSymbolicLink(linkPath, targetPath);
                return true;
            }
            catch (Exception ex)
            {
                if (OsInfo.IsWindows)
                {
                    try
                    {
                        var fullLinkPath = Path.GetFullPath(linkPath);
                        var fullTargetPath = Path.GetFullPath(targetPath);

                        if (!IsSafeWindowsJunctionPath(fullLinkPath) || !IsSafeWindowsJunctionPath(fullTargetPath))
                        {
                            _logger.Warn("Blocked Windows junction creation with unsafe path characters: {0} -> {1}", linkPath, targetPath);
                            return false;
                        }

                        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{fullLinkPath}\" \"{fullTargetPath}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = false,
                            RedirectStandardError = false
                        };

                        using (var proc = Process.Start(psi))
                        {
                            if (proc != null && proc.WaitForExit(3000) && proc.ExitCode == 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception jEx)
                    {
                        _logger.Debug(jEx, "Windows junction fallback failed for {0}", linkPath);
                    }
                }

                _logger.Debug(ex, "Could not create symlink at {0} -> {1}", linkPath, targetPath);
                return false;
            }
        }

        private string GetStorageFilePath()
        {
            var appData = _appFolderInfo?.AppDataFolder ?? Path.GetTempPath();
            return Path.Combine(appData, "virtual_libraries.json");
        }

        private List<VirtualLibraryProfile> LoadProfilesInternal()
        {
            try
            {
                var path = GetStorageFilePath();
                if (!File.Exists(path))
                {
                    return new List<VirtualLibraryProfile>();
                }

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<VirtualLibraryProfile>>(json) ?? new List<VirtualLibraryProfile>();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to read virtual_libraries.json");
                return new List<VirtualLibraryProfile>();
            }
        }

        private void SaveProfilesInternal(List<VirtualLibraryProfile> profiles)
        {
            try
            {
                var path = GetStorageFilePath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to write virtual_libraries.json");
            }
        }
    }
}
