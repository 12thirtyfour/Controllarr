using System;
using System.IO;
using System.Linq;
using NLog;

namespace NzbDrone.Core.MediaFiles.Synchronization
{
    public interface ISidecarSaveService
    {
        void RenameAdjacentSaves(string originalRomPath, string newRomPath);
    }

    public class SidecarSaveService : ISidecarSaveService
    {
        private readonly Logger _logger;
        private static readonly string[] SaveExtensions = { ".srm", ".sav", ".state", ".rtc" };

        public SidecarSaveService(Logger logger)
        {
            _logger = logger;
        }

        public void RenameAdjacentSaves(string originalRomPath, string newRomPath)
        {
            var originalDir = Path.GetDirectoryName(originalRomPath);
            var originalNameNoExt = Path.GetFileNameWithoutExtension(originalRomPath);

            var newDir = Path.GetDirectoryName(newRomPath);
            var newNameNoExt = Path.GetFileNameWithoutExtension(newRomPath);

            if (string.IsNullOrEmpty(originalDir) || string.IsNullOrEmpty(newDir))
                return;

            if (originalDir == newDir && originalNameNoExt == newNameNoExt)
                return;

            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(newDir);
            }

            var adjacentFiles = Directory.GetFiles(originalDir);

            foreach (var file in adjacentFiles)
            {
                var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (fileNameNoExt == originalNameNoExt && SaveExtensions.Contains(extension))
                {
                    var newFilePath = Path.Combine(newDir, newNameNoExt + extension);
                    _logger.Debug($"Moving sidecar save: {file} to {newFilePath}");

                    try
                    {
                        if (File.Exists(newFilePath))
                        {
                            File.Delete(newFilePath);
                        }
                        File.Move(file, newFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to move sidecar save {file}");
                    }
                }
            }
        }
    }
}
