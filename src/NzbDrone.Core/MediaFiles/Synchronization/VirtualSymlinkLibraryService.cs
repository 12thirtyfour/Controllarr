using System;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace NzbDrone.Core.MediaFiles.Synchronization
{
    public enum SymlinkTargetProfile
    {
        MiSTer = 0,
        RetroArch = 1,
        EmulationStation = 2,
        Batocera = 3
    }

    public interface IVirtualSymlinkLibraryService
    {
        string ResolveTargetFolder(SymlinkTargetProfile profile, string platformSlug);
        string BuildVirtualSymlinkPath(string virtualRoot, SymlinkTargetProfile profile, string platformSlug, string romFileName);
        List<string> SyncVirtualLibrary(string virtualRoot, SymlinkTargetProfile profile, IEnumerable<(string PlatformSlug, string SourceRomPath)> romEntries, bool createFilesystemLinks = true);
    }

    public class VirtualSymlinkLibraryService : IVirtualSymlinkLibraryService
    {
        private static readonly Dictionary<string, string> MisterFolderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nes", "NES" },
            { "fds", "NES" },
            { "snes", "SNES" },
            { "n64", "N64" },
            { "gb", "GAMEBOY" },
            { "gbc", "GBC" },
            { "gba", "GBA" },
            { "genesis", "Genesis" },
            { "segacd", "MegaCD" },
            { "32x", "S32X" },
            { "saturn", "Saturn" },
            { "tg16", "TGFX16" },
            { "tgcd", "TGFX16-CD" },
            { "ps1", "PSX" }
        };

        private static readonly Dictionary<string, string> RetroArchFolderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nes", "Nintendo - Nintendo Entertainment System" },
            { "snes", "Nintendo - Super Nintendo Entertainment System" },
            { "n64", "Nintendo - Nintendo 64" },
            { "gamecube", "Nintendo - GameCube" },
            { "gb", "Nintendo - Game Boy" },
            { "gbc", "Nintendo - Game Boy Color" },
            { "gba", "Nintendo - Game Boy Advance" },
            { "nds", "Nintendo - Nintendo DS" },
            { "genesis", "Sega - Mega Drive - Genesis" },
            { "segacd", "Sega - Mega-CD - Sega CD" },
            { "saturn", "Sega - Saturn" },
            { "dreamcast", "Sega - Dreamcast" },
            { "tg16", "NEC - PC Engine - TurboGrafx 16" },
            { "ps1", "Sony - PlayStation" },
            { "ps2", "Sony - PlayStation 2" },
            { "psp", "Sony - PlayStation Portable" }
        };

        private static readonly Dictionary<string, string> EsBatoceraFolderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "nes", "nes" },
            { "fds", "fds" },
            { "snes", "snes" },
            { "n64", "n64" },
            { "gamecube", "gc" },
            { "wii", "wii" },
            { "gb", "gb" },
            { "gbc", "gbc" },
            { "gba", "gba" },
            { "nds", "nds" },
            { "3ds", "3ds" },
            { "switch", "switch" },
            { "genesis", "megadrive" },
            { "segacd", "segacd" },
            { "32x", "sega32x" },
            { "saturn", "saturn" },
            { "dreamcast", "dreamcast" },
            { "tg16", "pcengine" },
            { "tgcd", "pcenginecd" },
            { "ps1", "psx" },
            { "ps2", "ps2" },
            { "psp", "psp" }
        };

        private readonly Logger _logger;

        public VirtualSymlinkLibraryService(Logger logger)
        {
            _logger = logger;
        }

        public string ResolveTargetFolder(SymlinkTargetProfile profile, string platformSlug)
        {
            var key = platformSlug?.Trim() ?? string.Empty;

            if (profile == SymlinkTargetProfile.MiSTer)
            {
                var coreFolder = MisterFolderMap.TryGetValue(key, out var mapped) ? mapped : key.ToUpperInvariant();
                return Path.Combine("games", coreFolder);
            }

            if (profile == SymlinkTargetProfile.RetroArch)
            {
                var playlistFolder = RetroArchFolderMap.TryGetValue(key, out var mapped) ? mapped : key;
                return Path.Combine("roms", playlistFolder);
            }

            var esFolder = EsBatoceraFolderMap.TryGetValue(key, out var esMapped) ? esMapped : key.ToLowerInvariant();
            return Path.Combine("roms", esFolder);
        }

        public string BuildVirtualSymlinkPath(string virtualRoot, SymlinkTargetProfile profile, string platformSlug, string romFileName)
        {
            var relativeSystemDir = ResolveTargetFolder(profile, platformSlug);
            return Path.Combine(virtualRoot ?? string.Empty, relativeSystemDir, Path.GetFileName(romFileName ?? string.Empty));
        }

        public List<string> SyncVirtualLibrary(
            string virtualRoot,
            SymlinkTargetProfile profile,
            IEnumerable<(string PlatformSlug, string SourceRomPath)> romEntries,
            bool createFilesystemLinks = true)
        {
            var generatedLinks = new List<string>();
            if (string.IsNullOrWhiteSpace(virtualRoot) || romEntries == null)
            {
                return generatedLinks;
            }

            foreach (var (platformSlug, sourceRomPath) in romEntries)
            {
                if (string.IsNullOrWhiteSpace(sourceRomPath))
                {
                    continue;
                }

                var linkPath = BuildVirtualSymlinkPath(virtualRoot, profile, platformSlug, Path.GetFileName(sourceRomPath));
                generatedLinks.Add(linkPath);

                if (createFilesystemLinks)
                {
                    try
                    {
                        var parentDir = Path.GetDirectoryName(linkPath);
                        if (!string.IsNullOrWhiteSpace(parentDir) && !Directory.Exists(parentDir))
                        {
                            Directory.CreateDirectory(parentDir);
                        }

                        if (File.Exists(linkPath))
                        {
                            File.Delete(linkPath);
                        }

                        File.CreateSymbolicLink(linkPath, sourceRomPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Could not create symbolic link at {0} -> {1}", linkPath, sourceRomPath);
                    }
                }
            }

            return generatedLinks;
        }
    }
}
