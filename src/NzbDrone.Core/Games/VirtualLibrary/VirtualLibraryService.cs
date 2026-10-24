using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Messaging.Events;
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

    public class VirtualLibraryService : IVirtualLibraryService, IHandle<SeriesRefreshCompleteEvent>
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

                foreach (var platform in allPlatforms)
                {
                    if (string.IsNullOrWhiteSpace(platform.Path) || !Directory.Exists(platform.Path))
                    {
                        continue;
                    }

                    var relativeTargetFolder = MapPlatformToPresetRelativePath(
                        profile.HardwarePreset,
                        platform.TitleSlug ?? platform.Title);

                    var linkFullPath = Path.Combine(profile.TargetPath, relativeTargetFolder);
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

        public void Handle(SeriesRefreshCompleteEvent message)
        {
            SyncAllAuto();
        }

        private static string MapPlatformToPresetRelativePath(string preset, string slugOrTitle)
        {
            var norm = (slugOrTitle ?? string.Empty).ToLowerInvariant();

            switch ((preset ?? "mister").ToLowerInvariant())
            {
                case "mister":
                    return Path.Combine("games", GetMisterCoreName(norm));
                case "retroarch":
                    return GetRetroArchSystemName(norm);
                case "analogue_pocket":
                    return Path.Combine("Assets", GetPocketCoreName(norm), "common");
                case "onionos":
                    return Path.Combine("Roms", GetOnionSystemCode(norm));
                case "batocera":
                    return Path.Combine("roms", GetBatoceraSystemSlug(norm));
                case "everdrive":
                    return GetMisterCoreName(norm);
                default:
                    return GetMisterCoreName(norm);
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

            if (norm.Contains("famicom-disk"))
            {
                return "FDS";
            }

            if (norm.Contains("nintendo-64") || norm == "n64")
            {
                return "N64";
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

            if (norm.Contains("genesis") || norm.Contains("mega-drive"))
            {
                return "Genesis";
            }

            if (norm.Contains("sega-cd") || norm.Contains("mega-cd"))
            {
                return "MegaCD";
            }

            if (norm.Contains("32x"))
            {
                return "S32X";
            }

            if (norm.Contains("master-system"))
            {
                return "SMS";
            }

            if (norm.Contains("saturn"))
            {
                return "Saturn";
            }

            if (norm.Contains("playstation-2") || norm == "ps2")
            {
                return "PS2";
            }

            if (norm.Contains("playstation") || norm == "ps1")
            {
                return "PSX";
            }

            if (norm.Contains("turbografx-cd"))
            {
                return "TGFX16-CD";
            }

            if (norm.Contains("turbografx"))
            {
                return "TGFX16";
            }

            if (norm.Contains("neo-geo"))
            {
                return "NeoGeo";
            }

            return norm.ToUpperInvariant();
        }

        private static string GetRetroArchSystemName(string norm)
        {
            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "Nintendo - Super Nintendo Entertainment System";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "Nintendo - Nintendo Entertainment System";
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

            if (norm.Contains("genesis") || norm.Contains("mega-drive"))
            {
                return "Sega - Mega Drive - Genesis";
            }

            if (norm.Contains("sega-cd"))
            {
                return "Sega - Mega-CD - Sega CD";
            }

            if (norm.Contains("saturn"))
            {
                return "Sega - Saturn";
            }

            if (norm.Contains("dreamcast"))
            {
                return "Sega - Dreamcast";
            }

            if (norm.Contains("playstation") && !norm.Contains('2') && !norm.Contains("portable"))
            {
                return "Sony - PlayStation";
            }

            if (norm.Contains("turbografx-16"))
            {
                return "NEC - PC Engine - TurboGrafx 16";
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

            if (norm.Contains("genesis") || norm.Contains("mega-drive"))
            {
                return "genesis";
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

            if (norm.Contains("turbografx"))
            {
                return "pce";
            }

            if (norm.Contains("neo-geo"))
            {
                return "ng";
            }

            return norm;
        }

        private static string GetOnionSystemCode(string norm)
        {
            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "SFC";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "FC";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive"))
            {
                return "MD";
            }

            if (norm.Contains("sega-cd"))
            {
                return "SEGACD";
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

            if (norm.Contains("playstation"))
            {
                return "PS";
            }

            if (norm.Contains("turbografx"))
            {
                return "PCE";
            }

            return norm.ToUpperInvariant();
        }

        private static string GetBatoceraSystemSlug(string norm)
        {
            if (norm.Contains("super-nintendo") || norm == "snes")
            {
                return "snes";
            }

            if (norm.Contains("nintendo-entertainment-system") || norm == "nes")
            {
                return "nes";
            }

            if (norm.Contains("nintendo-64") || norm == "n64")
            {
                return "n64";
            }

            if (norm.Contains("genesis") || norm.Contains("mega-drive"))
            {
                return "megadrive";
            }

            if (norm.Contains("sega-cd"))
            {
                return "segacd";
            }

            if (norm.Contains("playstation") && !norm.Contains('2'))
            {
                return "psx";
            }

            if (norm.Contains("turbografx-16"))
            {
                return "pcengine";
            }

            return norm;
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
                        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };

                        using (var proc = Process.Start(psi))
                        {
                            proc?.WaitForExit(3000);
                            if (proc?.ExitCode == 0)
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
