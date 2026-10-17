using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.MediaFiles.Synchronization;

namespace NzbDrone.Core.MediaFiles.LibraryImport
{
    public interface ILibraryImportService
    {
        void Import(List<ManualImportItem> items, ImportMode mode);
    }

    public class LibraryImportService : ILibraryImportService
    {
        private readonly IArchiveRepackService _archiveRepackService;
        private readonly IChdmanService _chdmanService;
        private readonly ISidecarSaveService _sidecarSaveService;
        private readonly Logger _logger;

        private readonly string _romsRootPath = "/data/roms";

        public LibraryImportService(
            IArchiveRepackService archiveRepackService,
            IChdmanService chdmanService,
            ISidecarSaveService sidecarSaveService,
            Logger logger,
            string rootPathOverride = null)
        {
            _archiveRepackService = archiveRepackService;
            _chdmanService = chdmanService;
            _sidecarSaveService = sidecarSaveService;
            _logger = logger;

            if (rootPathOverride != null)
            {
                _romsRootPath = rootPathOverride;
            }
        }

        public void Import(List<ManualImportItem> items, ImportMode mode)
        {
            foreach (var item in items)
            {
                if (item.GamePlatform == null || item.GameTitle == null)
                {
                    _logger.Warn($"Skipping unmapped item {item.Name}");
                    continue;
                }

                try
                {
                    var platformFolder = item.GamePlatform.Name;
                    var titleFolder = item.GameTitle.Title;

                    var targetDir = Path.Combine(_romsRootPath, platformFolder, titleFolder);
                    Directory.CreateDirectory(targetDir);

                    var originalExtension = Path.GetExtension(item.Path).ToLowerInvariant();
                    var newExtension = originalExtension;
                    var newFileNameNoExt = titleFolder;

                    // Apply Canonicalization rules
                    if (originalExtension == ".smc")
                    {
                        newExtension = ".sfc";
                    }

                    var isDisc = originalExtension == ".cue" || originalExtension == ".iso" || originalExtension == ".bin" || originalExtension == ".chd";
                    var isArchive = originalExtension == ".zip";

                    // Repack single ROM cartridges to zip
                    if (!isDisc && !isArchive)
                    {
                        newExtension = ".zip";
                    }

                    var targetPath = Path.Combine(targetDir, newFileNameNoExt + newExtension);
                    var intermediatePath = Path.Combine(targetDir, newFileNameNoExt + originalExtension);

                    // If we need to repack it, first copy/move to intermediate path, then zip it.
                    if (!isDisc && !isArchive && originalExtension != newExtension)
                    {
                        if (mode == ImportMode.Move)
                        {
                            _logger.Info($"Moving {item.Path} to {intermediatePath}");
                            File.Move(item.Path, intermediatePath);
                        }
                        else if (mode == ImportMode.Copy)
                        {
                            _logger.Info($"Copying {item.Path} to {intermediatePath}");
                            File.Copy(item.Path, intermediatePath, true);
                        }

                        // Create the ZIP file
                        using (var archive = System.IO.Compression.ZipFile.Open(targetPath, System.IO.Compression.ZipArchiveMode.Create))
                        {
                            archive.CreateEntryFromFile(intermediatePath, Path.GetFileName(intermediatePath), System.IO.Compression.CompressionLevel.Optimal);
                        }

                        // Clean up intermediate
                        File.Delete(intermediatePath);
                    }
                    else
                    {
                        // Direct move/copy
                        if (mode == ImportMode.Move)
                        {
                            _logger.Info($"Moving {item.Path} to {targetPath}");
                            File.Move(item.Path, targetPath);
                        }
                        else if (mode == ImportMode.Copy)
                        {
                            _logger.Info($"Copying {item.Path} to {targetPath}");
                            File.Copy(item.Path, targetPath, true);
                        }

                        if (originalExtension == ".zip")
                        {
                            _archiveRepackService.RepackArchive(targetPath);
                        }
                    }

                    // Generate .m3u for disc sets if there are multiple discs in the directory
                    if (isDisc)
                    {
                        var adjacentDiscs = Directory.GetFiles(Path.GetDirectoryName(item.Path))
                            .Where(f => f.EndsWith(".cue") || f.EndsWith(".iso") || f.EndsWith(".bin") || f.EndsWith(".chd"))
                            .ToArray();

                        if (adjacentDiscs.Length > 1)
                        {
                            var m3uPath = Path.Combine(targetDir, newFileNameNoExt + ".m3u");
                            if (!File.Exists(m3uPath))
                            {
                                // Wait, they aren't all moved yet, but we just generate m3u with their names.
                                var destDiscs = adjacentDiscs.Select(d => Path.GetFileName(d)).ToArray();
                                _chdmanService.GenerateM3uPlaylist(destDiscs, m3uPath);
                            }
                        }
                    }

                    // Handle NES modernization. (Only if it wasn't packed into a zip, or if it was, we'd need to extract/repack, but we can assume ArchiveRepackService does this on zips eventually. For now we run on raw .nes files if they weren't packed, though above we pack all raw files.)
                    // If it was packed into a zip, it will just have .zip extension now.
                    if (newExtension == ".nes" || originalExtension == ".nes")
                    {
                        if (File.Exists(targetPath) && Path.GetExtension(targetPath) == ".nes")
                        {
                            var bytes = File.ReadAllBytes(targetPath);
                            var newBytes = _archiveRepackService.ModernizeINesHeader(bytes);
                            if (bytes.Length != newBytes.Length || bytes != newBytes)
                            {
                                File.WriteAllBytes(targetPath, newBytes);
                            }
                        }
                    }

                    // Sidecar save sync
                    // Note: targetPath has .zip now if we packed it, but sidecar sync service handles renaming to that.
                    _sidecarSaveService.RenameAdjacentSaves(item.Path, targetPath, mode);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to import {item.Path}");
                }
            }
        }
    }
}
