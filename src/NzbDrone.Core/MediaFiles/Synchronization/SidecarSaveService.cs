using System;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Core.MediaFiles.EpisodeImport;

namespace NzbDrone.Core.MediaFiles.Synchronization
{
    public interface ISidecarSaveService
    {
        void RenameAdjacentSaves(string originalRomPath, string newRomPath, ImportMode mode = ImportMode.Move);
    }

    public class SidecarSaveService : ISidecarSaveService
    {
        private readonly Logger _logger;
        private static readonly string[] SaveExtensions = { ".srm", ".sav", ".state", ".rtc", ".eep", ".flash" };

        public SidecarSaveService(Logger logger)
        {
            _logger = logger;
        }

        public void RenameAdjacentSaves(string originalRomPath, string newRomPath, ImportMode mode = ImportMode.Move)
        {
            var originalDir = Path.GetDirectoryName(originalRomPath);
            var originalNameNoExt = Path.GetFileNameWithoutExtension(originalRomPath);

            var newDir = Path.GetDirectoryName(newRomPath);
            var newNameNoExt = Path.GetFileNameWithoutExtension(newRomPath);

            if (string.IsNullOrEmpty(originalDir) || string.IsNullOrEmpty(newDir))
            {
                return;
            }

            if (originalDir == newDir && originalNameNoExt == newNameNoExt)
            {
                return;
            }

            if (!Directory.Exists(newDir))
            {
                Directory.CreateDirectory(newDir);
            }

            var adjacentFiles = Directory.GetFiles(originalDir);

            foreach (var file in adjacentFiles)
            {
                var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).ToLowerInvariant();

                // Check for exact state save extensions like .state, .state0, .state.auto
                var isSaveExtension = SaveExtensions.Contains(extension) ||
                                       extension.StartsWith(".state") ||
                                       extension == ".state.auto";

                if (fileNameNoExt == originalNameNoExt && isSaveExtension)
                {
                    var newFilePath = Path.Combine(newDir, newNameNoExt + extension);
                    _logger.Debug($"Processing sidecar save: {file} to {newFilePath} with mode {mode}");

                    try
                    {
                        if (File.Exists(newFilePath))
                        {
                            File.Delete(newFilePath);
                        }

                        if (mode == ImportMode.Copy)
                        {
                            File.Copy(file, newFilePath);
                        }
                        else
                        {
                            File.Move(file, newFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to process sidecar save {file}");
                    }
                }
            }
        }
    }
}
