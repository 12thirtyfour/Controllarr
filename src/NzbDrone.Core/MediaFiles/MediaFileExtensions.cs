using System;
using System.Collections.Generic;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public static class MediaFileExtensions
    {
        private static readonly Dictionary<string, Quality> FileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Console ROMs, Archives & Optical Disc Images
            { ".zip", Quality.Unknown },
            { ".7z", Quality.Unknown },
            { ".chd", Quality.Unknown },
            { ".rvz", Quality.Unknown },
            { ".ciso", Quality.Unknown },
            { ".gcm", Quality.Unknown },
            { ".wbfs", Quality.Unknown },
            { ".wux", Quality.Unknown },
            { ".wud", Quality.Unknown },
            { ".wua", Quality.Unknown },
            { ".rpx", Quality.Unknown },
            { ".xci", Quality.Unknown },
            { ".nsp", Quality.Unknown },
            { ".nsz", Quality.Unknown },
            { ".nes", Quality.Unknown },
            { ".unf", Quality.Unknown },
            { ".fds", Quality.Unknown },
            { ".sfc", Quality.Unknown },
            { ".smc", Quality.Unknown },
            { ".bs", Quality.Unknown },
            { ".z64", Quality.Unknown },
            { ".n64", Quality.Unknown },
            { ".v64", Quality.Unknown },
            { ".ndd", Quality.Unknown },
            { ".gb", Quality.Unknown },
            { ".gbc", Quality.Unknown },
            { ".gba", Quality.Unknown },
            { ".nds", Quality.Unknown },
            { ".dsi", Quality.Unknown },
            { ".3ds", Quality.Unknown },
            { ".cia", Quality.Unknown },
            { ".cci", Quality.Unknown },
            { ".vb", Quality.Unknown },
            { ".sg", Quality.Unknown },
            { ".sms", Quality.Unknown },
            { ".md", Quality.Unknown },
            { ".smd", Quality.Unknown },
            { ".gen", Quality.Unknown },
            { ".32x", Quality.Unknown },
            { ".cdi", Quality.Unknown },
            { ".gdi", Quality.Unknown },
            { ".cue", Quality.Unknown },
            { ".gg", Quality.Unknown },
            { ".pbp", Quality.Unknown },
            { ".cso", Quality.Unknown },
            { ".vpk", Quality.Unknown },
            { ".xbe", Quality.Unknown },
            { ".xex", Quality.Unknown },
            { ".pce", Quality.Unknown },
            { ".sgx", Quality.Unknown },
            { ".neo", Quality.Unknown },
            { ".ngp", Quality.Unknown },
            { ".ngc", Quality.Unknown },
            { ".a26", Quality.Unknown },
            { ".a52", Quality.Unknown },
            { ".a78", Quality.Unknown },
            { ".j64", Quality.Unknown },
            { ".jag", Quality.Unknown },
            { ".lnx", Quality.Unknown },
            { ".ws", Quality.Unknown },
            { ".wsc", Quality.Unknown },
            { ".col", Quality.Unknown },
            { ".int", Quality.Unknown },
            { ".vec", Quality.Unknown },
            { ".iso", Quality.Unknown },
            { ".bin", Quality.Unknown },
            { ".img", Quality.Unknown },
            { ".nrg", Quality.Unknown },
            { ".pkg", Quality.Unknown },
            { ".ips", Quality.Unknown },
            { ".bps", Quality.Unknown },
            { ".xdelta", Quality.Unknown },
            { ".vcdiff", Quality.Unknown },
        };

        public static HashSet<string> Extensions => new(FileExtensions.Keys, StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> DiskExtensions => new([".img", ".iso", ".bin", ".nrg"], StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> StreamingExtensions => new([], StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> RomExtensions => new(
        [
            ".zip", ".7z", ".chd", ".rvz", ".ciso", ".gcm", ".wbfs", ".wux", ".wud", ".wua", ".rpx", ".xci", ".nsp", ".nsz",
            ".nes", ".unf", ".fds", ".sfc", ".smc", ".bs", ".z64", ".n64", ".v64", ".ndd", ".gb", ".gbc", ".gba", ".nds", ".dsi",
            ".3ds", ".cia", ".cci", ".vb", ".sg", ".sms", ".md", ".smd", ".gen", ".32x", ".cdi", ".gdi", ".cue",
            ".gg", ".pbp", ".cso", ".vpk", ".xbe", ".xex", ".pce", ".sgx", ".neo", ".ngp", ".ngc", ".a26", ".a52",
            ".a78", ".j64", ".jag", ".lnx", ".ws", ".wsc", ".col", ".int", ".vec", ".iso", ".bin", ".img", ".nrg", ".pkg",
            ".ips", ".bps", ".xdelta", ".vcdiff"
        ],
        StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> PatchExtensions => new([".ips", ".bps", ".xdelta", ".vcdiff"], StringComparer.OrdinalIgnoreCase);

        public static Quality GetQualityForExtension(string extension)
        {
            return FileExtensions.TryGetValue(extension, out var quality) ? quality : Quality.Unknown;
        }
    }
}
