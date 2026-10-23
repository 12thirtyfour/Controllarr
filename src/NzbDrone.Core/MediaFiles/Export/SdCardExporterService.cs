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
        R4 = 5
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
        string BuildDeviceRelativePath(SdCardDeviceProfile profile, string platformFolder, string gameTitle, string fileName);
        SdCardExportResult ExportToSdCard(SdCardDeviceProfile profile, string targetMountPath, IEnumerable<SdCardExportItem> items, bool overwrite = false);
    }

    public class SdCardExporterService : ISdCardExporterService
    {
        private static readonly Dictionary<string, string> OnionOsSystemMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SNES", "SFC" },
            { "Super Nintendo Entertainment System", "SFC" },
            { "NES", "FC" },
            { "Nintendo Entertainment System", "FC" },
            { "Game Boy", "GB" },
            { "Game Boy Color", "GBC" },
            { "Game Boy Advance", "GBA" },
            { "Genesis", "MD" },
            { "Sega Genesis", "MD" },
            { "Mega Drive", "MD" },
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
            { "Sega Genesis", "genesis" }
        };

        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public SdCardExporterService(IDiskProvider diskProvider, Logger logger)
        {
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public string BuildDeviceRelativePath(SdCardDeviceProfile profile, string platformFolder, string gameTitle, string fileName)
        {
            var safePlatform = platformFolder.IsNotNullOrWhiteSpace() ? platformFolder.Trim() : "Unknown";
            var safeFileName = Path.GetFileName(fileName);
            var bucket = ResolveAlphabeticalBucket(gameTitle.IsNotNullOrWhiteSpace() ? gameTitle : safeFileName);

            switch (profile)
            {
                case SdCardDeviceProfile.OnionOS:
                    var onionSystem = OnionOsSystemMap.TryGetValue(safePlatform, out var mappedOnion) ? mappedOnion : safePlatform.ToUpperInvariant();
                    return Path.Combine("Roms", onionSystem, safeFileName);

                case SdCardDeviceProfile.AnaloguePocket:
                    var pocketCore = AnaloguePocketCoreMap.TryGetValue(safePlatform, out var mappedCore) ? mappedCore : safePlatform.ToLowerInvariant();
                    return Path.Combine("Assets", pocketCore, "common", bucket, safeFileName);

                case SdCardDeviceProfile.EverDrive:
                    return Path.Combine(safePlatform, bucket, safeFileName);

                case SdCardDeviceProfile.FXPakPro:
                    return Path.Combine("sd2snes", bucket, safeFileName);

                case SdCardDeviceProfile.EZFlash:
                    return Path.Combine("ROMS", bucket, safeFileName);

                case SdCardDeviceProfile.R4:
                    return Path.Combine("Games", bucket, safeFileName);

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
