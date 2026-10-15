using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaFiles.Archive
{
    public interface IArchiveExtractionService
    {
        void ExtractArchive(string archivePath, string destinationDirectory, bool isFullSetArchive);
    }

    public class ArchiveExtractionService : IArchiveExtractionService
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ArchiveExtractionService(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public void ExtractArchive(string archivePath, string destinationDirectory, bool isFullSetArchive)
        {
            var extractAll = !isFullSetArchive || _configService.ExtractFullSetCompletely;

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue; // Skip directories

                    if (!extractAll)
                    {
                        _logger.Debug($"Skipping {entry.FullName} as ExtractFullSetCompletely is false and wanted check is pending.");
                        continue;
                    }

                    var destinationPath = Path.Combine(destinationDirectory, entry.FullName);
                    var fullDestDirPath = Path.GetFullPath(destinationDirectory + Path.DirectorySeparatorChar);
                    if (!Path.GetFullPath(destinationPath).StartsWith(fullDestDirPath))
                    {
                        _logger.Warn($"Skipping {entry.FullName} due to Zip Slip vulnerability protection.");
                        continue;
                    }

                    var entryDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                    {
                        Directory.CreateDirectory(entryDir);
                    }

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        }
    }
}
