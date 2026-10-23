using System;
using System.Collections.Generic;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Games.Launcher
{
    public class GameLaunchDescriptor
    {
        public int GameId { get; set; }
        public string PlatformFolder { get; set; }
        public string RomPath { get; set; }
        public string LibretroCore { get; set; }
        public bool SupportsWebAssembly { get; set; }
        public string DesktopProtocolUri { get; set; }
        public string WebPlayerUrl { get; set; }
    }

    public interface IGameLauncherService
    {
        string ResolveLibretroCore(string platformFolder);
        GameLaunchDescriptor BuildLaunchDescriptor(int gameId, string platformFolder, string romPath);
    }

    public class GameLauncherService : IGameLauncherService
    {
        private static readonly Dictionary<string, (string Core, bool SupportsWasm)> CoreMappings =
            new Dictionary<string, (string Core, bool SupportsWasm)>(StringComparer.OrdinalIgnoreCase)
            {
                { "SNES", ("snes9x", true) },
                { "Super Nintendo Entertainment System", ("snes9x", true) },
                { "NES", ("nestopia", true) },
                { "Nintendo Entertainment System", ("nestopia", true) },
                { "Game Boy", ("sameboy", true) },
                { "Game Boy Color", ("sameboy", true) },
                { "Game Boy Advance", ("mgba", true) },
                { "Nintendo 64", ("mupen64plus_next", true) },
                { "Nintendo DS", ("melonds", true) },
                { "Genesis", ("genesis_plus_gx", true) },
                { "Sega Genesis", ("genesis_plus_gx", true) },
                { "Mega Drive", ("genesis_plus_gx", true) },
                { "Master System", ("genesis_plus_gx", true) },
                { "Game Gear", ("genesis_plus_gx", true) },
                { "PlayStation", ("mednafen_psx_hw", true) },
                { "Sony PlayStation", ("mednafen_psx_hw", true) },
                { "Atari 2600", ("stella", true) },
                { "PlayStation 2", ("pcsx2", false) },
                { "GameCube", ("dolphin", false) },
                { "Nintendo GameCube", ("dolphin", false) }
            };

        public string ResolveLibretroCore(string platformFolder)
        {
            if (platformFolder.IsNotNullOrWhiteSpace() &&
                CoreMappings.TryGetValue(platformFolder.Trim(), out var mapping))
            {
                return mapping.Core;
            }

            return "retroarch_auto";
        }

        public GameLaunchDescriptor BuildLaunchDescriptor(int gameId, string platformFolder, string romPath)
        {
            var safePlatform = platformFolder?.Trim() ?? string.Empty;
            var safeRomPath = romPath?.Trim() ?? string.Empty;
            var core = ResolveLibretroCore(safePlatform);
            var supportsWasm = safePlatform.IsNotNullOrWhiteSpace() &&
                CoreMappings.TryGetValue(safePlatform, out var mapping) &&
                mapping.SupportsWasm;

            var encodedRom = Uri.EscapeDataString(safeRomPath);
            var encodedCore = Uri.EscapeDataString(core);

            return new GameLaunchDescriptor
            {
                GameId = gameId,
                PlatformFolder = safePlatform,
                RomPath = safeRomPath,
                LibretroCore = core,
                SupportsWebAssembly = supportsWasm,
                DesktopProtocolUri = $"controllarr://play/{gameId}?core={encodedCore}&rom={encodedRom}",
                WebPlayerUrl = $"/webplayer?gameId={gameId}&core={encodedCore}&rom={encodedRom}"
            };
        }
    }
}
