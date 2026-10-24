using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Games.Launcher
{
    public class LibretroLauncherConfig
    {
        public bool EnableWebPlayer { get; set; } = true;
        public string PreferredLaunchMode { get; set; } = "web";
        public string RetroArchExecutablePath { get; set; } = string.Empty;
        public string LibretroCoresDirectory { get; set; } = string.Empty;
        public string RetroArchConfigPath { get; set; } = string.Empty;
        public string EmulatorJsDataPath { get; set; } = "https://cdn.emulatorjs.org/stable/data/";
        public string DefaultShader { get; set; } = "none";
        public bool AutoInjectBios { get; set; } = true;
        public bool PersistBatterySaves { get; set; } = true;
        public bool EnableRetroAchievementsInPlayer { get; set; } = true;
        public bool FullScreenOnLaunch { get; set; }
        public Dictionary<string, string> CoreOverrides { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public class LibretroPlatformCoreOption
    {
        public string PlatformKey { get; set; }
        public string PlatformName { get; set; }
        public string DefaultCore { get; set; }
        public string SelectedCore { get; set; }
        public List<string> AvailableCores { get; set; } = new List<string>();
        public bool SupportsWebAssembly { get; set; }
        public string EmulatorJsCoreName { get; set; }
    }

    public class GameLaunchDescriptor
    {
        public int GameId { get; set; }
        public int SeriesId { get; set; }
        public string GameTitle { get; set; } = string.Empty;
        public string PlatformTitle { get; set; } = string.Empty;
        public string PlatformFolder { get; set; } = string.Empty;
        public string RomPath { get; set; } = string.Empty;
        public string RomFileName { get; set; } = string.Empty;
        public bool HasRomFile { get; set; }
        public string LibretroCore { get; set; } = string.Empty;
        public string EmulatorJsCore { get; set; } = string.Empty;
        public bool SupportsWebAssembly { get; set; }
        public string PreferredLaunchMode { get; set; } = "web";
        public string Shader { get; set; } = "none";
        public string EmulatorJsDataPath { get; set; } = "https://cdn.emulatorjs.org/stable/data/";
        public string RomStreamUrl { get; set; } = string.Empty;
        public string BiosStreamUrl { get; set; } = string.Empty;
        public string DesktopProtocolUri { get; set; } = string.Empty;
        public string WebPlayerUrl { get; set; } = string.Empty;
    }

    public class DesktopLaunchResult
    {
        public bool Success { get; set; }
        public string LaunchedMode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public GameLaunchDescriptor Descriptor { get; set; }
    }

    public interface IGameLauncherService
    {
        LibretroLauncherConfig GetConfig();
        LibretroLauncherConfig SaveConfig(LibretroLauncherConfig config);
        List<LibretroPlatformCoreOption> GetPlatformCoreOptions();
        string ResolveLibretroCore(string platformFolder);
        GameLaunchDescriptor BuildLaunchDescriptor(int gameId, string platformFolder = "", string romPath = "");
        DesktopLaunchResult LaunchDesktopGame(int gameId, string platformFolder = "", string romPath = "");
        Stream OpenRomStream(int gameId, out string fileName);
    }

    public class GameLauncherService : IGameLauncherService
    {
        private sealed class PlatformCoreDef
        {
            public string PlatformKey { get; set; }
            public string PlatformName { get; set; }
            public string[] Aliases { get; set; }
            public string DefaultCore { get; set; }
            public string[] AvailableCores { get; set; }
            public bool SupportsWasm { get; set; }
            public string EmulatorJsCore { get; set; }
        }

        private static readonly List<PlatformCoreDef> PlatformDefs = new List<PlatformCoreDef>
        {
            new PlatformCoreDef
            {
                PlatformKey = "NES",
                PlatformName = "Nintendo Entertainment System / Famicom",
                Aliases = new[] { "NES", "Nintendo Entertainment System", "Famicom", "Famicom Disk System", "fds" },
                DefaultCore = "nestopia",
                AvailableCores = new[] { "nestopia", "fceumm", "mesen", "quicknes" },
                SupportsWasm = true,
                EmulatorJsCore = "nes"
            },
            new PlatformCoreDef
            {
                PlatformKey = "SNES",
                PlatformName = "Super Nintendo / Super Famicom",
                Aliases = new[] { "SNES", "Super Nintendo Entertainment System", "Super Nintendo", "Super Famicom", "sfc" },
                DefaultCore = "snes9x",
                AvailableCores = new[] { "snes9x", "snes9x2010", "bsnes", "mesen-s" },
                SupportsWasm = true,
                EmulatorJsCore = "snes"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Boy",
                PlatformName = "Nintendo Game Boy",
                Aliases = new[] { "Game Boy", "GB", "Nintendo Game Boy" },
                DefaultCore = "sameboy",
                AvailableCores = new[] { "sameboy", "gambatte", "mgba", "gearboy" },
                SupportsWasm = true,
                EmulatorJsCore = "gb"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Boy Color",
                PlatformName = "Nintendo Game Boy Color",
                Aliases = new[] { "Game Boy Color", "GBC", "Nintendo Game Boy Color" },
                DefaultCore = "sameboy",
                AvailableCores = new[] { "sameboy", "gambatte", "mgba", "gearboy" },
                SupportsWasm = true,
                EmulatorJsCore = "gbc"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Boy Advance",
                PlatformName = "Nintendo Game Boy Advance",
                Aliases = new[] { "Game Boy Advance", "GBA", "Nintendo Game Boy Advance" },
                DefaultCore = "mgba",
                AvailableCores = new[] { "mgba", "vba_next", "gpsp", "beetle_gba" },
                SupportsWasm = true,
                EmulatorJsCore = "gba"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Nintendo 64",
                PlatformName = "Nintendo 64",
                Aliases = new[] { "Nintendo 64", "N64" },
                DefaultCore = "mupen64plus_next",
                AvailableCores = new[] { "mupen64plus_next", "parallel_n64" },
                SupportsWasm = true,
                EmulatorJsCore = "n64"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Nintendo DS",
                PlatformName = "Nintendo DS",
                Aliases = new[] { "Nintendo DS", "NDS" },
                DefaultCore = "melonds",
                AvailableCores = new[] { "melonds", "desmume", "desmume2015" },
                SupportsWasm = true,
                EmulatorJsCore = "nds"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Genesis",
                PlatformName = "Sega Genesis / Mega Drive",
                Aliases = new[] { "Genesis", "Sega Genesis", "Mega Drive", "Sega Mega Drive", "md" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "picodrive", "blastem" },
                SupportsWasm = true,
                EmulatorJsCore = "segaMD"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Master System",
                PlatformName = "Sega Master System / Game Gear",
                Aliases = new[] { "Master System", "Sega Master System", "SMS", "Game Gear", "Sega Game Gear", "GG" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "picodrive", "gearsystem" },
                SupportsWasm = true,
                EmulatorJsCore = "segaMS"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Sega CD",
                PlatformName = "Sega CD / Mega-CD & 32X",
                Aliases = new[] { "Sega CD", "Mega-CD", "Sega 32X", "32X", "segacd", "sega32x" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "picodrive" },
                SupportsWasm = true,
                EmulatorJsCore = "segaCD"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Sega Saturn",
                PlatformName = "Sega Saturn",
                Aliases = new[] { "Sega Saturn", "Saturn", "saturn" },
                DefaultCore = "yabause",
                AvailableCores = new[] { "yabause", "beetle_saturn", "kronos" },
                SupportsWasm = true,
                EmulatorJsCore = "segaSaturn"
            },
            new PlatformCoreDef
            {
                PlatformKey = "PlayStation",
                PlatformName = "Sony PlayStation (PS1)",
                Aliases = new[] { "PlayStation", "Sony PlayStation", "PS1", "PSX", "psx" },
                DefaultCore = "mednafen_psx_hw",
                AvailableCores = new[] { "mednafen_psx_hw", "pcsx_rearmed", "swanstation", "mednafen_psx" },
                SupportsWasm = true,
                EmulatorJsCore = "psx"
            },
            new PlatformCoreDef
            {
                PlatformKey = "PSP",
                PlatformName = "Sony PlayStation Portable (PSP)",
                Aliases = new[] { "PlayStation Portable", "Sony PlayStation Portable", "PSP", "psp" },
                DefaultCore = "ppsspp",
                AvailableCores = new[] { "ppsspp" },
                SupportsWasm = true,
                EmulatorJsCore = "psp"
            },
            new PlatformCoreDef
            {
                PlatformKey = "TurboGrafx-16",
                PlatformName = "NEC TurboGrafx-16 / PC Engine",
                Aliases = new[] { "TurboGrafx-16", "PC Engine", "TurboGrafx-CD", "PCE", "tg16", "pce" },
                DefaultCore = "mednafen_pce_fast",
                AvailableCores = new[] { "mednafen_pce_fast", "mednafen_supergrafx" },
                SupportsWasm = true,
                EmulatorJsCore = "pce"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Neo Geo",
                PlatformName = "SNK Neo Geo / Arcade",
                Aliases = new[] { "Neo Geo", "Neo Geo AES", "Neo Geo MVS", "Neo Geo CD", "Arcade", "neogeo", "arcade" },
                DefaultCore = "fbneo",
                AvailableCores = new[] { "fbneo", "mame2003_plus", "neocd" },
                SupportsWasm = true,
                EmulatorJsCore = "arcade"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Atari 2600",
                PlatformName = "Atari 2600 / 7800 / Lynx / Jaguar",
                Aliases = new[] { "Atari 2600", "Atari 7800", "Atari Lynx", "Atari Jaguar", "atari2600", "jaguar" },
                DefaultCore = "stella",
                AvailableCores = new[] { "stella", "prosystem", "handy", "virtualjaguar" },
                SupportsWasm = true,
                EmulatorJsCore = "atari2600"
            },
            new PlatformCoreDef
            {
                PlatformKey = "PlayStation 2",
                PlatformName = "Sony PlayStation 2 (PS2)",
                Aliases = new[] { "PlayStation 2", "Sony PlayStation 2", "PS2", "ps2" },
                DefaultCore = "pcsx2",
                AvailableCores = new[] { "pcsx2", "play" },
                SupportsWasm = false,
                EmulatorJsCore = "ps2"
            },
            new PlatformCoreDef
            {
                PlatformKey = "GameCube",
                PlatformName = "Nintendo GameCube / Wii",
                Aliases = new[] { "GameCube", "Nintendo GameCube", "Wii", "Nintendo Wii", "ngc", "wii" },
                DefaultCore = "dolphin",
                AvailableCores = new[] { "dolphin" },
                SupportsWasm = false,
                EmulatorJsCore = "dolphin"
            }
        };

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;
        private readonly object _lock = new object();
        private LibretroLauncherConfig _inMemoryConfig;

        public GameLauncherService(
            IAppFolderInfo appFolderInfo = null,
            ISeriesService seriesService = null,
            IEpisodeService episodeService = null,
            IMediaFileService mediaFileService = null,
            Logger logger = null)
        {
            _appFolderInfo = appFolderInfo;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public LibretroLauncherConfig GetConfig()
        {
            lock (_lock)
            {
                return LoadConfigInternal();
            }
        }

        public LibretroLauncherConfig SaveConfig(LibretroLauncherConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(config.PreferredLaunchMode))
                {
                    config.PreferredLaunchMode = "web";
                }

                if (string.IsNullOrWhiteSpace(config.EmulatorJsDataPath))
                {
                    config.EmulatorJsDataPath = "https://cdn.emulatorjs.org/stable/data/";
                }

                if (string.IsNullOrWhiteSpace(config.DefaultShader))
                {
                    config.DefaultShader = "none";
                }

                config.CoreOverrides ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _inMemoryConfig = config;
                SaveConfigInternal(config);
                return config;
            }
        }

        public List<LibretroPlatformCoreOption> GetPlatformCoreOptions()
        {
            var config = GetConfig();
            return PlatformDefs.Select(def =>
            {
                var selected = def.DefaultCore;
                if (config.CoreOverrides != null &&
                    config.CoreOverrides.TryGetValue(def.PlatformKey, out var customCore) &&
                    !string.IsNullOrWhiteSpace(customCore))
                {
                    selected = customCore;
                }

                return new LibretroPlatformCoreOption
                {
                    PlatformKey = def.PlatformKey,
                    PlatformName = def.PlatformName,
                    DefaultCore = def.DefaultCore,
                    SelectedCore = selected,
                    AvailableCores = def.AvailableCores.ToList(),
                    SupportsWebAssembly = def.SupportsWasm,
                    EmulatorJsCoreName = def.EmulatorJsCore
                };
            }).ToList();
        }

        public string ResolveLibretroCore(string platformFolder)
        {
            var def = MatchPlatformDef(platformFolder);
            if (def != null)
            {
                var config = GetConfig();
                if (config.CoreOverrides != null)
                {
                    if (config.CoreOverrides.TryGetValue(def.PlatformKey, out var overrideCore) &&
                        !string.IsNullOrWhiteSpace(overrideCore))
                    {
                        return overrideCore.Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(platformFolder) &&
                        config.CoreOverrides.TryGetValue(platformFolder.Trim(), out var directOverride) &&
                        !string.IsNullOrWhiteSpace(directOverride))
                    {
                        return directOverride.Trim();
                    }
                }

                return def.DefaultCore;
            }

            return "retroarch_auto";
        }

        public GameLaunchDescriptor BuildLaunchDescriptor(int gameId, string platformFolder = "", string romPath = "")
        {
            var config = GetConfig();
            var safePlatform = platformFolder?.Trim() ?? string.Empty;
            var safeRomPath = romPath?.Trim() ?? string.Empty;
            var gameTitle = string.Empty;
            var platformTitle = safePlatform;
            var seriesId = 0;
            var resolvedGameId = gameId;

            if (_episodeService != null && gameId > 0)
            {
                try
                {
                    Episode episode = null;
                    try
                    {
                        episode = _episodeService.GetEpisode(gameId);
                    }
                    catch
                    {
                        episode = null;
                    }

                    if (episode == null && _seriesService != null)
                    {
                        try
                        {
                            var asSeries = _seriesService.GetSeries(gameId);
                            if (asSeries != null)
                            {
                                seriesId = asSeries.Id;
                                platformTitle = asSeries.Title ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(safePlatform))
                                {
                                    safePlatform = asSeries.Title ?? asSeries.TitleSlug ?? string.Empty;
                                }

                                var seriesEpisodes = _episodeService.GetEpisodeBySeries(asSeries.Id) ?? new List<Episode>();
                                episode = seriesEpisodes.FirstOrDefault(e => e.EpisodeFileId > 0) ?? seriesEpisodes.FirstOrDefault();
                                if (episode != null)
                                {
                                    resolvedGameId = episode.Id;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore lookup fallback errors
                        }
                    }

                    if (episode != null)
                    {
                        gameTitle = episode.Title ?? string.Empty;
                        seriesId = episode.SeriesId;

                        Series series = null;
                        if (_seriesService != null && episode.SeriesId > 0)
                        {
                            try
                            {
                                series = _seriesService.GetSeries(episode.SeriesId);
                            }
                            catch
                            {
                                series = null;
                            }
                        }

                        if (series != null)
                        {
                            platformTitle = series.Title ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(safePlatform))
                            {
                                safePlatform = series.Title ?? series.TitleSlug ?? string.Empty;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(safeRomPath) && episode.EpisodeFileId > 0 && _mediaFileService != null)
                        {
                            try
                            {
                                var romFile = _mediaFileService.Get(episode.EpisodeFileId);
                                if (romFile != null)
                                {
                                    if (!string.IsNullOrWhiteSpace(romFile.Path))
                                    {
                                        safeRomPath = romFile.Path;
                                    }
                                    else if (series != null && !string.IsNullOrWhiteSpace(series.Path) && !string.IsNullOrWhiteSpace(romFile.RelativePath))
                                    {
                                        safeRomPath = Path.Combine(series.Path, romFile.RelativePath);
                                    }
                                    else
                                    {
                                        safeRomPath = romFile.RelativePath ?? string.Empty;
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore media file lookup failure
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Debug(ex, "Unable to hydrate game metadata for launch descriptor {0}", gameId);
                }
            }

            var matchedDef = MatchPlatformDef(safePlatform);
            var core = ResolveLibretroCore(safePlatform);
            var supportsWasm = matchedDef != null && matchedDef.SupportsWasm;
            var emulatorJsCore = matchedDef?.EmulatorJsCore ?? core;

            var encodedRom = Uri.EscapeDataString(safeRomPath);
            var encodedCore = Uri.EscapeDataString(core);
            var romFileName = !string.IsNullOrWhiteSpace(safeRomPath)
                ? Path.GetFileName(safeRomPath)
                : string.Empty;
            var hasRomFile = !string.IsNullOrWhiteSpace(safeRomPath) &&
                             (File.Exists(safeRomPath) || romPath.IsNotNullOrWhiteSpace());

            return new GameLaunchDescriptor
            {
                GameId = resolvedGameId,
                SeriesId = seriesId,
                GameTitle = !string.IsNullOrWhiteSpace(gameTitle)
                    ? gameTitle
                    : (!string.IsNullOrWhiteSpace(romFileName) ? Path.GetFileNameWithoutExtension(romFileName) : $"Game #{resolvedGameId}"),
                PlatformTitle = !string.IsNullOrWhiteSpace(platformTitle) ? platformTitle : safePlatform,
                PlatformFolder = safePlatform,
                RomPath = safeRomPath,
                RomFileName = romFileName,
                HasRomFile = hasRomFile,
                LibretroCore = core,
                EmulatorJsCore = emulatorJsCore,
                SupportsWebAssembly = supportsWasm,
                PreferredLaunchMode = config.PreferredLaunchMode ?? "web",
                Shader = config.DefaultShader ?? "none",
                EmulatorJsDataPath = config.EmulatorJsDataPath ?? "https://cdn.emulatorjs.org/stable/data/",
                RomStreamUrl = $"/api/v3/gamelauncher/{resolvedGameId}/rom",
                DesktopProtocolUri = $"controllarr://play/{resolvedGameId}?core={encodedCore}&rom={encodedRom}",
                WebPlayerUrl = $"/webplayer?gameId={resolvedGameId}&core={encodedCore}&rom={encodedRom}"
            };
        }

        public DesktopLaunchResult LaunchDesktopGame(int gameId, string platformFolder = "", string romPath = "")
        {
            var descriptor = BuildLaunchDescriptor(gameId, platformFolder, romPath);
            var config = GetConfig();

            if (!string.IsNullOrWhiteSpace(config.RetroArchExecutablePath) && File.Exists(config.RetroArchExecutablePath))
            {
                try
                {
                    var args = new List<string>();
                    if (!string.IsNullOrWhiteSpace(config.LibretroCoresDirectory))
                    {
                        var ext = OsInfo.IsWindows ? ".dll" : (OsInfo.IsOsx ? ".dylib" : ".so");
                        var candidateCore = Path.Combine(config.LibretroCoresDirectory, $"{descriptor.LibretroCore}_libretro{ext}");
                        if (File.Exists(candidateCore))
                        {
                            args.Add($"-L \"{candidateCore}\"");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(config.RetroArchConfigPath) && File.Exists(config.RetroArchConfigPath))
                    {
                        args.Add($"--config \"{config.RetroArchConfigPath}\"");
                    }

                    if (config.FullScreenOnLaunch)
                    {
                        args.Add("-f");
                    }

                    if (!string.IsNullOrWhiteSpace(descriptor.RomPath))
                    {
                        args.Add($"\"{descriptor.RomPath}\"");
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = config.RetroArchExecutablePath,
                        Arguments = string.Join(" ", args),
                        UseShellExecute = false
                    };

                    Process.Start(psi);

                    return new DesktopLaunchResult
                    {
                        Success = true,
                        LaunchedMode = "retroarch",
                        Message = $"Launched '{descriptor.GameTitle}' in RetroArch using core '{descriptor.LibretroCore}'.",
                        Descriptor = descriptor
                    };
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Failed to launch RetroArch desktop process for game {0}", gameId);
                    return new DesktopLaunchResult
                    {
                        Success = false,
                        LaunchedMode = "retroarch",
                        Message = $"Failed to launch RetroArch executable: {ex.Message}",
                        Descriptor = descriptor
                    };
                }
            }

            return new DesktopLaunchResult
            {
                Success = true,
                LaunchedMode = config.PreferredLaunchMode ?? "web",
                Message = descriptor.SupportsWebAssembly
                    ? $"Ready to launch '{descriptor.GameTitle}' in Libretro WebPlayer ({descriptor.LibretroCore})."
                    : $"Ready to launch '{descriptor.GameTitle}' via desktop protocol URI ({descriptor.DesktopProtocolUri}).",
                Descriptor = descriptor
            };
        }

        public Stream OpenRomStream(int gameId, out string fileName)
        {
            fileName = string.Empty;
            var descriptor = BuildLaunchDescriptor(gameId);
            if (string.IsNullOrWhiteSpace(descriptor.RomPath) || !File.Exists(descriptor.RomPath))
            {
                return null;
            }

            fileName = Path.GetFileName(descriptor.RomPath);
            return new FileStream(descriptor.RomPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static PlatformCoreDef MatchPlatformDef(string platformFolder)
        {
            if (string.IsNullOrWhiteSpace(platformFolder))
            {
                return null;
            }

            var trimmed = platformFolder.Trim();
            foreach (var def in PlatformDefs)
            {
                if (def.PlatformKey.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    def.PlatformName.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    def.Aliases.Any(a => a.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
                {
                    return def;
                }
            }

            foreach (var def in PlatformDefs)
            {
                if (def.Aliases.Any(a => a.Length >= 3 && trimmed.Contains(a, StringComparison.OrdinalIgnoreCase)))
                {
                    return def;
                }
            }

            return null;
        }

        private string GetConfigFilePath()
        {
            if (_appFolderInfo == null || string.IsNullOrWhiteSpace(_appFolderInfo.AppDataFolder))
            {
                return null;
            }

            return Path.Combine(_appFolderInfo.AppDataFolder, "libretro_config.json");
        }

        private LibretroLauncherConfig LoadConfigInternal()
        {
            if (_inMemoryConfig != null)
            {
                return _inMemoryConfig;
            }

            try
            {
                var path = GetConfigFilePath();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<LibretroLauncherConfig>(json);
                    if (loaded != null)
                    {
                        loaded.CoreOverrides = loaded.CoreOverrides != null
                            ? new Dictionary<string, string>(loaded.CoreOverrides, StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        _inMemoryConfig = loaded;
                        return _inMemoryConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "Failed to read libretro_config.json");
            }

            _inMemoryConfig = new LibretroLauncherConfig();
            return _inMemoryConfig;
        }

        private void SaveConfigInternal(LibretroLauncherConfig config)
        {
            try
            {
                var path = GetConfigFilePath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger?.Warn(ex, "Failed to write libretro_config.json");
            }
        }
    }
}
