using System;
using System.Collections.Generic;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public static class MediaFileExtensions
    {
        private static readonly Dictionary<string, Quality> FileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Unknown
            { ".webm", Quality.Unknown },

            // SDTV
            { ".m4v", Quality.SDTV },
            { ".3gp", Quality.SDTV },
            { ".nsv", Quality.SDTV },
            { ".ty", Quality.SDTV },
            { ".strm", Quality.SDTV },
            { ".rm", Quality.SDTV },
            { ".rmvb", Quality.SDTV },
            { ".m3u", Quality.SDTV },
            { ".ifo", Quality.SDTV },
            { ".mov", Quality.SDTV },
            { ".qt", Quality.SDTV },
            { ".divx", Quality.SDTV },
            { ".xvid", Quality.SDTV },
            { ".bivx", Quality.SDTV },
            { ".nrg", Quality.SDTV },
            { ".pva", Quality.SDTV },
            { ".wmv", Quality.SDTV },
            { ".asf", Quality.SDTV },
            { ".asx", Quality.SDTV },
            { ".ogm", Quality.SDTV },
            { ".ogv", Quality.SDTV },
            { ".m2v", Quality.SDTV },
            { ".avi", Quality.SDTV },
            { ".bin", Quality.SDTV },
            { ".dat", Quality.SDTV },
            { ".dvr-ms", Quality.SDTV },
            { ".mpg", Quality.SDTV },
            { ".mpeg", Quality.SDTV },
            { ".mp4", Quality.SDTV },
            { ".avc", Quality.SDTV },
            { ".vp3", Quality.SDTV },
            { ".svq3", Quality.SDTV },
            { ".nuv", Quality.SDTV },
            { ".viv", Quality.SDTV },
            { ".dv", Quality.SDTV },
            { ".fli", Quality.SDTV },
            { ".flv", Quality.SDTV },
            { ".wpl", Quality.SDTV },

            // DVD
            { ".img", Quality.DVD },
            { ".iso", Quality.DVD },
            { ".vob", Quality.DVD },

            // HD
            { ".mkv", Quality.HDTV720p },
            { ".ts", Quality.HDTV720p },
            { ".wtv", Quality.HDTV720p },

            // Bluray
            // Bluray
            { ".m2ts", Quality.Bluray720p },

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
            { ".rpx", Quality.Unknown },
            { ".xci", Quality.Unknown },
            { ".nsp", Quality.Unknown },
            { ".nes", Quality.Unknown },
            { ".unf", Quality.Unknown },
            { ".fds", Quality.Unknown },
            { ".sfc", Quality.Unknown },
            { ".smc", Quality.Unknown },
            { ".z64", Quality.Unknown },
            { ".n64", Quality.Unknown },
            { ".v64", Quality.Unknown },
            { ".gb", Quality.Unknown },
            { ".gbc", Quality.Unknown },
            { ".gba", Quality.Unknown },
            { ".nds", Quality.Unknown },
            { ".3ds", Quality.Unknown },
            { ".cia", Quality.Unknown },
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
            { ".neo", Quality.Unknown },
            { ".ngp", Quality.Unknown },
            { ".ngc", Quality.Unknown },
            { ".a26", Quality.Unknown },
            { ".a52", Quality.Unknown },
            { ".a78", Quality.Unknown },
            { ".j64", Quality.Unknown },
            { ".lnx", Quality.Unknown },
            { ".ws", Quality.Unknown },
            { ".wsc", Quality.Unknown },
            { ".col", Quality.Unknown },
            { ".int", Quality.Unknown },
            { ".vec", Quality.Unknown },
            { ".ips", Quality.Unknown },
            { ".bps", Quality.Unknown },
            { ".xdelta", Quality.Unknown },
            { ".vcdiff", Quality.Unknown },
        };

        public static HashSet<string> Extensions => new(FileExtensions.Keys, StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> DiskExtensions => new([".img", ".iso", ".vob"], StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> StreamingExtensions => new([".m3u", ".strm"], StringComparer.OrdinalIgnoreCase);

        public static HashSet<string> RomExtensions => new(
        [
            ".zip", ".7z", ".chd", ".rvz", ".ciso", ".gcm", ".wbfs", ".wux", ".wud", ".rpx", ".xci", ".nsp",
            ".nes", ".unf", ".fds", ".sfc", ".smc", ".z64", ".n64", ".v64", ".gb", ".gbc", ".gba", ".nds",
            ".3ds", ".cia", ".vb", ".sg", ".sms", ".md", ".smd", ".gen", ".32x", ".cdi", ".gdi", ".cue",
            ".gg", ".pbp", ".cso", ".vpk", ".xbe", ".xex", ".pce", ".neo", ".ngp", ".ngc", ".a26", ".a52",
            ".a78", ".j64", ".lnx", ".ws", ".wsc", ".col", ".int", ".vec", ".iso", ".bin", ".img", ".nrg",
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
