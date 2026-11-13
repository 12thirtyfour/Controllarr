using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        GameLaunchDescriptor BuildLaunchDescriptor(int gameId, string platformFolder = "", string romPath = "", int romFileId = 0);
        DesktopLaunchResult LaunchDesktopGame(int gameId, string platformFolder = "", string romPath = "", int romFileId = 0);
        Stream OpenRomStream(int gameId, out string fileName, string platformFolder = "", string romPath = "", int romFileId = 0);
        Stream OpenBiosStream(int gameId, out string fileName, string platformFolder = "", string romPath = "", int romFileId = 0);
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
                Aliases = new[] { "NES", "Nintendo Entertainment System", "Nintendo - Nintendo Entertainment System", "Famicom", "Famicom Disk System", "fds" },
                DefaultCore = "nestopia",
                AvailableCores = new[] { "nestopia", "fceumm", "mesen", "quicknes" },
                SupportsWasm = true,
                EmulatorJsCore = "nes"
            },
            new PlatformCoreDef
            {
                PlatformKey = "SNES",
                PlatformName = "Super Nintendo / Super Famicom",
                Aliases = new[] { "SNES", "Super Nintendo Entertainment System", "Nintendo - Super Nintendo Entertainment System", "Super Nintendo", "Super Famicom", "sfc", "Satellaview", "satellaview" },
                DefaultCore = "snes9x",
                AvailableCores = new[] { "snes9x", "snes9x2010", "bsnes", "mesen-s" },
                SupportsWasm = true,
                EmulatorJsCore = "snes"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Boy",
                PlatformName = "Nintendo Game Boy",
                Aliases = new[] { "Game Boy", "GB", "Nintendo Game Boy", "Nintendo - Game Boy", "gameboy" },
                DefaultCore = "sameboy",
                AvailableCores = new[] { "sameboy", "gambatte", "mgba", "gearboy" },
                SupportsWasm = true,
                EmulatorJsCore = "gb"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Boy Color",
                PlatformName = "Nintendo Game Boy Color",
                Aliases = new[] { "Game Boy Color", "GBC", "Nintendo Game Boy Color", "Nintendo - Game Boy Color", "gameboy color" },
                DefaultCore = "sameboy",
                AvailableCores = new[] { "sameboy", "gambatte", "mgba", "gearboy" },
                SupportsWasm = true,
                EmulatorJsCore = "gbc"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Boy Advance",
                PlatformName = "Nintendo Game Boy Advance",
                Aliases = new[] { "Game Boy Advance", "GBA", "Nintendo Game Boy Advance", "Nintendo - Game Boy Advance", "gameboy advance" },
                DefaultCore = "mgba",
                AvailableCores = new[] { "mgba", "vba_next", "gpsp", "beetle_gba" },
                SupportsWasm = true,
                EmulatorJsCore = "gba"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Virtual Boy",
                PlatformName = "Nintendo Virtual Boy",
                Aliases = new[] { "Virtual Boy", "VB", "Nintendo Virtual Boy", "Nintendo - Virtual Boy" },
                DefaultCore = "beetle_vb",
                AvailableCores = new[] { "beetle_vb", "mednafen_vb" },
                SupportsWasm = true,
                EmulatorJsCore = "vb"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Nintendo 64",
                PlatformName = "Nintendo 64",
                Aliases = new[] { "Nintendo 64", "N64", "Nintendo - Nintendo 64", "64DD", "64dd" },
                DefaultCore = "mupen64plus_next",
                AvailableCores = new[] { "mupen64plus_next", "parallel_n64" },
                SupportsWasm = true,
                EmulatorJsCore = "n64"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Nintendo DS",
                PlatformName = "Nintendo DS",
                Aliases = new[] { "Nintendo DS", "NDS", "Nintendo - Nintendo DS" },
                DefaultCore = "melonds",
                AvailableCores = new[] { "melonds", "desmume", "desmume2015" },
                SupportsWasm = true,
                EmulatorJsCore = "nds"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Genesis",
                PlatformName = "Sega Genesis / Mega Drive",
                Aliases = new[] { "Genesis", "Sega Genesis", "Sega - Mega Drive - Genesis", "Mega Drive", "Sega Mega Drive", "md" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "picodrive", "blastem" },
                SupportsWasm = true,
                EmulatorJsCore = "segaMD"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Master System",
                PlatformName = "Sega Master System",
                Aliases = new[] { "Master System", "Sega Master System", "Sega - Master System - Mark III", "SMS" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "picodrive", "gearsystem" },
                SupportsWasm = true,
                EmulatorJsCore = "segaMS"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Game Gear",
                PlatformName = "Sega Game Gear",
                Aliases = new[] { "Game Gear", "Sega Game Gear", "Sega - Game Gear", "GG" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "gearsystem", "picodrive" },
                SupportsWasm = true,
                EmulatorJsCore = "segaGG"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Sega CD",
                PlatformName = "Sega CD / Mega-CD",
                Aliases = new[] { "Sega CD", "Mega-CD", "Sega - Mega-CD - Sega CD", "segacd" },
                DefaultCore = "genesis_plus_gx",
                AvailableCores = new[] { "genesis_plus_gx", "picodrive" },
                SupportsWasm = true,
                EmulatorJsCore = "segaCD"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Sega 32X",
                PlatformName = "Sega 32X",
                Aliases = new[] { "Sega 32X", "32X", "Sega - 32X", "sega32x" },
                DefaultCore = "picodrive",
                AvailableCores = new[] { "picodrive" },
                SupportsWasm = true,
                EmulatorJsCore = "sega32x"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Sega Saturn",
                PlatformName = "Sega Saturn",
                Aliases = new[] { "Sega Saturn", "Saturn", "Sega - Saturn", "saturn" },
                DefaultCore = "yabause",
                AvailableCores = new[] { "yabause", "beetle_saturn", "kronos" },
                SupportsWasm = true,
                EmulatorJsCore = "segaSaturn"
            },
            new PlatformCoreDef
            {
                PlatformKey = "PlayStation",
                PlatformName = "Sony PlayStation (PS1)",
                Aliases = new[] { "PlayStation", "Sony PlayStation", "Sony - PlayStation", "PS1", "PSX", "psx" },
                DefaultCore = "mednafen_psx_hw",
                AvailableCores = new[] { "mednafen_psx_hw", "pcsx_rearmed", "swanstation", "mednafen_psx" },
                SupportsWasm = true,
                EmulatorJsCore = "psx"
            },
            new PlatformCoreDef
            {
                PlatformKey = "PSP",
                PlatformName = "Sony PlayStation Portable (PSP)",
                Aliases = new[] { "PlayStation Portable", "Sony PlayStation Portable", "Sony - PlayStation Portable", "PSP", "psp" },
                DefaultCore = "ppsspp",
                AvailableCores = new[] { "ppsspp" },
                SupportsWasm = true,
                EmulatorJsCore = "psp"
            },
            new PlatformCoreDef
            {
                PlatformKey = "TurboGrafx-16",
                PlatformName = "NEC TurboGrafx-16 / PC Engine",
                Aliases = new[] { "TurboGrafx-16", "PC Engine", "NEC - PC Engine - TurboGrafx 16", "TurboGrafx-CD", "PCE", "tg16", "tgcd", "pce" },
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
                PlatformKey = "Neo Geo Pocket",
                PlatformName = "SNK Neo Geo Pocket Color",
                Aliases = new[] { "Neo Geo Pocket Color", "Neo Geo Pocket", "SNK - Neo Geo Pocket Color", "ngpc", "ngp" },
                DefaultCore = "mednafen_ngp",
                AvailableCores = new[] { "mednafen_ngp", "race" },
                SupportsWasm = true,
                EmulatorJsCore = "ngp"
            },
            new PlatformCoreDef
            {
                PlatformKey = "WonderSwan",
                PlatformName = "Bandai WonderSwan / Color",
                Aliases = new[] { "WonderSwan Color", "WonderSwan", "Bandai - WonderSwan Color", "Bandai - WonderSwan", "wonderswan", "wsc", "ws" },
                DefaultCore = "mednafen_wswan",
                AvailableCores = new[] { "mednafen_wswan" },
                SupportsWasm = true,
                EmulatorJsCore = "ws"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Atari 2600",
                PlatformName = "Atari 2600",
                Aliases = new[] { "Atari 2600", "Atari - 2600", "atari2600" },
                DefaultCore = "stella",
                AvailableCores = new[] { "stella", "stella2014" },
                SupportsWasm = true,
                EmulatorJsCore = "atari2600"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Atari 7800",
                PlatformName = "Atari 7800",
                Aliases = new[] { "Atari 7800", "Atari - 7800", "atari7800" },
                DefaultCore = "prosystem",
                AvailableCores = new[] { "prosystem" },
                SupportsWasm = true,
                EmulatorJsCore = "atari7800"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Atari Lynx",
                PlatformName = "Atari Lynx",
                Aliases = new[] { "Atari Lynx", "Atari - Lynx", "lynx" },
                DefaultCore = "handy",
                AvailableCores = new[] { "handy", "mednafen_lynx" },
                SupportsWasm = true,
                EmulatorJsCore = "lynx"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Atari Jaguar",
                PlatformName = "Atari Jaguar",
                Aliases = new[] { "Atari Jaguar", "Atari - Jaguar", "Jaguar CD", "jaguar", "jaguarcd" },
                DefaultCore = "virtualjaguar",
                AvailableCores = new[] { "virtualjaguar" },
                SupportsWasm = true,
                EmulatorJsCore = "jaguar"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Atari 5200",
                PlatformName = "Atari 5200",
                Aliases = new[] { "Atari 5200", "Atari - 5200", "atari5200", "5200" },
                DefaultCore = "a5200",
                AvailableCores = new[] { "a5200", "atari800" },
                SupportsWasm = true,
                EmulatorJsCore = "atari5200"
            },
            new PlatformCoreDef
            {
                PlatformKey = "ColecoVision",
                PlatformName = "ColecoVision",
                Aliases = new[] { "ColecoVision", "Coleco - ColecoVision", "coleco", "colecovision" },
                DefaultCore = "gearcoleco",
                AvailableCores = new[] { "gearcoleco", "bluemsx" },
                SupportsWasm = true,
                EmulatorJsCore = "coleco"
            },
            new PlatformCoreDef
            {
                PlatformKey = "3DO",
                PlatformName = "3DO Interactive Multiplayer",
                Aliases = new[] { "3DO", "3DO Interactive Multiplayer", "The 3DO Company - 3DO", "3do" },
                DefaultCore = "opera",
                AvailableCores = new[] { "opera" },
                SupportsWasm = true,
                EmulatorJsCore = "3do"
            },
            new PlatformCoreDef
            {
                PlatformKey = "Dreamcast",
                PlatformName = "Sega Dreamcast",
                Aliases = new[] { "Dreamcast", "Sega Dreamcast", "Sega - Dreamcast", "dreamcast", "dc" },
                DefaultCore = "flycast",
                AvailableCores = new[] { "flycast" },
                SupportsWasm = false,
                EmulatorJsCore = "dreamcast"
            },
            new PlatformCoreDef
            {
                PlatformKey = "PlayStation 2",
                PlatformName = "Sony PlayStation 2 (PS2)",
                Aliases = new[] { "PlayStation 2", "Sony PlayStation 2", "Sony - PlayStation 2", "PS2", "ps2" },
                DefaultCore = "pcsx2",
                AvailableCores = new[] { "pcsx2", "play" },
                SupportsWasm = false,
                EmulatorJsCore = "ps2"
            },
            new PlatformCoreDef
            {
                PlatformKey = "GameCube",
                PlatformName = "Nintendo GameCube / Wii",
                Aliases = new[] { "GameCube", "Nintendo GameCube", "Wii", "Nintendo Wii", "gamecube", "ngc", "wii" },
                DefaultCore = "dolphin",
                AvailableCores = new[] { "dolphin" },
                SupportsWasm = false,
                EmulatorJsCore = "dolphin"
            }
        };

        private static readonly string[] PreferredRomEntryExtensions = new[]
        {
            ".cue", ".gb", ".gbc", ".gba", ".nes", ".unf", ".fds", ".sfc", ".smc", ".bs",
            ".z64", ".n64", ".v64", ".ndd", ".nds", ".dsi", ".md", ".gen", ".smd", ".32x",
            ".sms", ".gg", ".pce", ".sgx", ".vb", ".ws", ".wsc", ".ngp", ".ngc", ".lnx",
            ".a26", ".a52", ".a78", ".j64", ".jag", ".neo", ".iso", ".chd", ".pbp", ".cso", ".bin"
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

        public GameLaunchDescriptor BuildLaunchDescriptor(int gameId, string platformFolder = "", string romPath = "", int romFileId = 0)
        {
            var config = GetConfig();
            var safePlatform = platformFolder?.Trim() ?? string.Empty;
            var safeRomPath = romPath?.Trim() ?? string.Empty;
            var gameTitle = string.Empty;
            var platformTitle = safePlatform;
            var seriesId = 0;
            var resolvedGameId = gameId;
            var resolvedRomFileId = romFileId;
            Series series = null;

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
                                series = asSeries;
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
                        if (resolvedRomFileId <= 0 && episode.EpisodeFileId > 0)
                        {
                            resolvedRomFileId = episode.EpisodeFileId;
                        }

                        if (series == null && _seriesService != null && episode.SeriesId > 0)
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
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Debug(ex, "Unable to hydrate game metadata for launch descriptor {0}", gameId);
                }
            }

            if (string.IsNullOrWhiteSpace(safeRomPath) && resolvedRomFileId > 0 && _mediaFileService != null)
            {
                safeRomPath = ResolveRomFilePath(resolvedRomFileId, ref series, ref seriesId, ref safePlatform, ref platformTitle);
            }

            if (!string.IsNullOrWhiteSpace(safeRomPath) && !Path.IsPathRooted(safeRomPath) && series != null && !string.IsNullOrWhiteSpace(series.Path))
            {
                var candidatePath = Path.Combine(series.Path, safeRomPath);
                if (File.Exists(candidatePath))
                {
                    safeRomPath = candidatePath;
                }
            }

            var matchedDef = MatchPlatformDef(safePlatform);
            if (matchedDef == null && series != null && !string.IsNullOrWhiteSpace(series.TitleSlug))
            {
                matchedDef = MatchPlatformDef(series.TitleSlug);
            }

            if (matchedDef == null && !string.IsNullOrWhiteSpace(safeRomPath))
            {
                matchedDef = InferPlatformDefFromRomFile(safeRomPath);
                if (matchedDef != null && string.IsNullOrWhiteSpace(safePlatform))
                {
                    safePlatform = matchedDef.PlatformKey;
                }
            }

            var core = matchedDef != null
                ? ResolveLibretroCore(matchedDef.PlatformKey)
                : ResolveLibretroCore(safePlatform);
            var supportsWasm = matchedDef != null && matchedDef.SupportsWasm;
            var emulatorJsCore = matchedDef?.EmulatorJsCore ?? core;

            var encodedRom = Uri.EscapeDataString(safeRomPath);
            var encodedCore = Uri.EscapeDataString(core);
            var romFileName = !string.IsNullOrWhiteSpace(safeRomPath)
                ? Path.GetFileName(safeRomPath)
                : string.Empty;
            var hasRomFile = !string.IsNullOrWhiteSpace(safeRomPath) &&
                             (File.Exists(safeRomPath) || romPath.IsNotNullOrWhiteSpace());

            var streamFileName = romFileName;
            if (!string.IsNullOrWhiteSpace(safeRomPath) &&
                File.Exists(safeRomPath) &&
                ShouldExtractZipForWebPlayer(safeRomPath, emulatorJsCore))
            {
                var innerEntryName = PeekZipRomEntryFileName(safeRomPath);
                if (!string.IsNullOrWhiteSpace(innerEntryName))
                {
                    streamFileName = innerEntryName;
                }
            }

            var streamRoute = !string.IsNullOrWhiteSpace(streamFileName)
                ? $"/api/v5/gamelauncher/{resolvedGameId}/rom/{Uri.EscapeDataString(streamFileName)}"
                : $"/api/v5/gamelauncher/{resolvedGameId}/rom";

            var streamQueryParts = new List<string>();
            if (resolvedRomFileId > 0)
            {
                streamQueryParts.Add($"romFileId={resolvedRomFileId}");
            }

            if (!string.IsNullOrWhiteSpace(safePlatform))
            {
                streamQueryParts.Add($"platformFolder={Uri.EscapeDataString(safePlatform)}");
            }

            if (!string.IsNullOrWhiteSpace(safeRomPath))
            {
                streamQueryParts.Add($"romPath={encodedRom}");
            }

            var romStreamUrl = streamQueryParts.Count > 0
                ? $"{streamRoute}?{string.Join("&", streamQueryParts)}"
                : streamRoute;

            var biosStreamUrl = string.Empty;
            if (config.AutoInjectBios)
            {
                var biosPath = ResolveBiosFilePath(emulatorJsCore, romFileName, safeRomPath, series);
                if (!string.IsNullOrWhiteSpace(biosPath))
                {
                    var biosFileName = Path.GetFileName(biosPath);
                    var biosRoute = $"/api/v5/gamelauncher/{resolvedGameId}/bios/{Uri.EscapeDataString(biosFileName)}";
                    biosStreamUrl = streamQueryParts.Count > 0
                        ? $"{biosRoute}?{string.Join("&", streamQueryParts)}"
                        : biosRoute;
                }
            }

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
                RomStreamUrl = romStreamUrl,
                BiosStreamUrl = biosStreamUrl,
                DesktopProtocolUri = $"controllarr://play/{resolvedGameId}?core={encodedCore}&rom={encodedRom}",
                WebPlayerUrl = $"/webplayer?gameId={resolvedGameId}&core={encodedCore}&rom={encodedRom}"
            };
        }

        public DesktopLaunchResult LaunchDesktopGame(int gameId, string platformFolder = "", string romPath = "", int romFileId = 0)
        {
            var descriptor = BuildLaunchDescriptor(gameId, platformFolder, romPath, romFileId);
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

        public Stream OpenRomStream(int gameId, out string fileName, string platformFolder = "", string romPath = "", int romFileId = 0)
        {
            fileName = string.Empty;
            var descriptor = BuildLaunchDescriptor(gameId, platformFolder, romPath, romFileId);
            if (string.IsNullOrWhiteSpace(descriptor.RomPath) || !File.Exists(descriptor.RomPath))
            {
                return null;
            }

            if (ShouldExtractZipForWebPlayer(descriptor.RomPath, descriptor.EmulatorJsCore))
            {
                var extracted = TryOpenZipRomStream(descriptor.RomPath, out var innerFileName);
                if (extracted != null)
                {
                    fileName = innerFileName;
                    return extracted;
                }
            }

            fileName = Path.GetFileName(descriptor.RomPath);
            return new FileStream(descriptor.RomPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public Stream OpenBiosStream(int gameId, out string fileName, string platformFolder = "", string romPath = "", int romFileId = 0)
        {
            fileName = string.Empty;
            var descriptor = BuildLaunchDescriptor(gameId, platformFolder, romPath, romFileId);
            Series series = null;
            if (_seriesService != null && descriptor.SeriesId > 0)
            {
                try
                {
                    series = _seriesService.GetSeries(descriptor.SeriesId);
                }
                catch
                {
                    series = null;
                }
            }

            var biosPath = ResolveBiosFilePath(descriptor.EmulatorJsCore, descriptor.RomFileName, descriptor.RomPath, series);
            if (string.IsNullOrWhiteSpace(biosPath) || !File.Exists(biosPath))
            {
                return null;
            }

            fileName = Path.GetFileName(biosPath);
            return new FileStream(biosPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private string ResolveBiosFilePath(string emulatorJsCore, string romFileName, string romPath, Series series)
        {
            var candidates = GetPreferredBiosFileNames(emulatorJsCore, romFileName);
            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            var searchDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(series?.Path))
            {
                searchDirs.Add(Path.Combine(series.Path, "BIOS"));
            }

            if (!string.IsNullOrWhiteSpace(romPath))
            {
                var romDir = Path.GetDirectoryName(romPath);
                if (!string.IsNullOrWhiteSpace(romDir))
                {
                    searchDirs.Add(Path.Combine(romDir, "BIOS"));
                    var parentDir = Path.GetDirectoryName(romDir);
                    if (!string.IsNullOrWhiteSpace(parentDir))
                    {
                        searchDirs.Add(Path.Combine(parentDir, "BIOS"));
                    }
                }
            }

            if (_appFolderInfo != null && !string.IsNullOrWhiteSpace(_appFolderInfo.AppDataFolder))
            {
                searchDirs.Add(Path.Combine(_appFolderInfo.AppDataFolder, "bios"));
                if (!string.IsNullOrWhiteSpace(series?.TitleSlug))
                {
                    searchDirs.Add(Path.Combine(_appFolderInfo.AppDataFolder, "bios", series.TitleSlug));
                }
            }

            foreach (var dir in searchDirs.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var fileName in candidates)
                {
                    var fullPath = Path.Combine(dir, fileName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            return string.Empty;
        }

        private static List<string> GetPreferredBiosFileNames(string emulatorJsCore, string romFileName)
        {
            if (string.IsNullOrWhiteSpace(emulatorJsCore))
            {
                return new List<string>();
            }

            if (emulatorJsCore.Equals("segaCD", StringComparison.OrdinalIgnoreCase))
            {
                var name = romFileName ?? string.Empty;
                if (name.Contains("(Europe", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("(EUR", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("(PAL", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "bios_CD_E.bin", "bios_CD_U.bin", "bios_CD_J.bin" };
                }

                if (name.Contains("(Japan", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("(JPN", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<string> { "bios_CD_J.bin", "bios_CD_U.bin", "bios_CD_E.bin" };
                }

                return new List<string> { "bios_CD_U.bin", "bios_CD_E.bin", "bios_CD_J.bin" };
            }

            if (emulatorJsCore.Equals("psx", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "scph5501.bin", "scph5500.bin", "scph5502.bin", "scph1001.bin", "PSXONPSP660.bin" };
            }

            if (emulatorJsCore.Equals("segaSaturn", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "sega_101.bin", "mpr-17933.bin", "saturn_bios.bin" };
            }

            if (emulatorJsCore.Equals("gba", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "gba_bios.bin" };
            }

            if (emulatorJsCore.Equals("lynx", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "lynxboot.img" };
            }

            if (emulatorJsCore.Equals("pce", StringComparison.OrdinalIgnoreCase) ||
                emulatorJsCore.Equals("pcengine", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "syscard3.pce" };
            }

            if (emulatorJsCore.Equals("nes", StringComparison.OrdinalIgnoreCase) &&
                (romFileName ?? string.Empty).EndsWith(".fds", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "disksys.rom" };
            }

            if (emulatorJsCore.Equals("atari5200", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "5200.rom" };
            }

            if (emulatorJsCore.Equals("coleco", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "colecovision.rom", "coleco.rom" };
            }

            if (emulatorJsCore.Equals("3do", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "panafz10.bin", "panafz1.bin", "goldstar.bin" };
            }

            if (emulatorJsCore.Equals("arcade", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string> { "neogeo.zip" };
            }

            return new List<string>();
        }

        private string ResolveRomFilePath(
            int romFileId,
            ref Series series,
            ref int seriesId,
            ref string safePlatform,
            ref string platformTitle)
        {
            try
            {
                var romFile = _mediaFileService.Get(romFileId);
                if (romFile == null)
                {
                    return string.Empty;
                }

                if (series == null && _seriesService != null && romFile.SeriesId > 0)
                {
                    try
                    {
                        series = _seriesService.GetSeries(romFile.SeriesId);
                    }
                    catch
                    {
                        series = null;
                    }
                }

                if (series != null)
                {
                    if (seriesId <= 0)
                    {
                        seriesId = series.Id;
                    }

                    if (string.IsNullOrWhiteSpace(platformTitle))
                    {
                        platformTitle = series.Title ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(safePlatform))
                    {
                        safePlatform = series.Title ?? series.TitleSlug ?? string.Empty;
                    }
                }

                if (!string.IsNullOrWhiteSpace(romFile.Path) && File.Exists(romFile.Path))
                {
                    return romFile.Path;
                }

                if (series != null && !string.IsNullOrWhiteSpace(series.Path) && !string.IsNullOrWhiteSpace(romFile.RelativePath))
                {
                    return Path.Combine(series.Path, romFile.RelativePath);
                }

                if (!string.IsNullOrWhiteSpace(romFile.Path))
                {
                    return romFile.Path;
                }

                return romFile.RelativePath ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ShouldExtractZipForWebPlayer(string romPath, string emulatorJsCore)
        {
            if (string.IsNullOrWhiteSpace(romPath))
            {
                return false;
            }

            var ext = Path.GetExtension(romPath);
            if (!ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(emulatorJsCore) &&
                (emulatorJsCore.Equals("arcade", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("fbneo", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("mame2003_plus", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("neocd", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(emulatorJsCore) &&
                (emulatorJsCore.Equals("segaCD", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("psx", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("segaSaturn", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("3do", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("pce", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("pcengine", StringComparison.OrdinalIgnoreCase) ||
                 emulatorJsCore.Equals("jaguar", StringComparison.OrdinalIgnoreCase)) &&
                File.Exists(romPath))
            {
                try
                {
                    using var archive = ZipFile.OpenRead(romPath);
                    var fileEntries = archive.Entries.Where(e => !string.IsNullOrWhiteSpace(e.Name) && e.Length > 0).ToList();
                    if (fileEntries.Count > 1 ||
                        fileEntries.Any(e => Path.GetExtension(e.Name).Equals(".cue", StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private static ZipArchiveEntry SelectBestRomZipEntry(ZipArchive archive)
        {
            var validEntries = archive.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name) && e.Length > 0)
                .ToList();

            if (validEntries.Count == 0)
            {
                return null;
            }

            foreach (var preferredExt in PreferredRomEntryExtensions)
            {
                var match = validEntries.FirstOrDefault(e =>
                    Path.GetExtension(e.Name).Equals(preferredExt, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match;
                }
            }

            var romExtMatch = validEntries.FirstOrDefault(e =>
            {
                var ext = Path.GetExtension(e.Name);
                return !string.IsNullOrWhiteSpace(ext) &&
                       !ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                       !ext.Equals(".7z", StringComparison.OrdinalIgnoreCase) &&
                       MediaFileExtensions.RomExtensions.Contains(ext);
            });

            return romExtMatch ?? validEntries.OrderByDescending(e => e.Length).FirstOrDefault();
        }

        private static string PeekZipRomEntryFileName(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var best = SelectBestRomZipEntry(archive);
                return best?.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static Stream TryOpenZipRomStream(string zipPath, out string innerFileName)
        {
            innerFileName = string.Empty;
            ZipArchive archive = null;
            try
            {
                archive = ZipFile.OpenRead(zipPath);
                var best = SelectBestRomZipEntry(archive);
                if (best == null)
                {
                    archive.Dispose();
                    return null;
                }

                var entryStream = best.Open();
                innerFileName = best.Name;
                return new ZipEntryStream(archive, entryStream, best.Length);
            }
            catch
            {
                archive?.Dispose();
                return null;
            }
        }

        private sealed class ZipEntryStream : Stream
        {
            private readonly ZipArchive _archive;
            private readonly Stream _inner;
            private readonly long _length;
            private long _position;
            private bool _disposed;

            public ZipEntryStream(ZipArchive archive, Stream inner, long length)
            {
                _archive = archive;
                _inner = inner;
                _length = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;

            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _inner.Read(buffer, offset, count);
                _position += read;
                return read;
            }

            public override int Read(Span<byte> buffer)
            {
                var read = _inner.Read(buffer);
                _position += read;
                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
                _position += read;
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                _position += read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (!_disposed && disposing)
                {
                    _disposed = true;
                    _inner.Dispose();
                    _archive.Dispose();
                }

                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    await _inner.DisposeAsync().ConfigureAwait(false);
                    _archive.Dispose();
                }

                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static PlatformCoreDef InferPlatformDefFromRomFile(string romPath)
        {
            var ext = Path.GetExtension(romPath);
            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) && File.Exists(romPath))
            {
                var innerName = PeekZipRomEntryFileName(romPath);
                if (!string.IsNullOrWhiteSpace(innerName))
                {
                    ext = Path.GetExtension(innerName);
                }
            }

            if (string.IsNullOrWhiteSpace(ext))
            {
                return null;
            }

            return ext.ToLowerInvariant() switch
            {
                ".gb" => MatchPlatformDef("Game Boy"),
                ".gbc" => MatchPlatformDef("Game Boy Color"),
                ".gba" => MatchPlatformDef("Game Boy Advance"),
                ".nes" or ".unf" or ".fds" => MatchPlatformDef("NES"),
                ".sfc" or ".smc" or ".bs" => MatchPlatformDef("SNES"),
                ".z64" or ".n64" or ".v64" or ".ndd" => MatchPlatformDef("Nintendo 64"),
                ".nds" or ".dsi" => MatchPlatformDef("Nintendo DS"),
                ".md" or ".gen" or ".smd" => MatchPlatformDef("Genesis"),
                ".sms" or ".sg" => MatchPlatformDef("Master System"),
                ".gg" => MatchPlatformDef("Game Gear"),
                ".32x" => MatchPlatformDef("Sega 32X"),
                ".pce" or ".sgx" => MatchPlatformDef("TurboGrafx-16"),
                ".vb" => MatchPlatformDef("Virtual Boy"),
                ".ws" or ".wsc" => MatchPlatformDef("WonderSwan"),
                ".ngp" or ".ngc" => MatchPlatformDef("Neo Geo Pocket"),
                ".a26" => MatchPlatformDef("Atari 2600"),
                ".a78" => MatchPlatformDef("Atari 7800"),
                ".lnx" => MatchPlatformDef("Atari Lynx"),
                ".j64" or ".jag" => MatchPlatformDef("Atari Jaguar"),
                ".pbp" => MatchPlatformDef("PlayStation"),
                ".cso" => MatchPlatformDef("PSP"),
                _ => null
            };
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
