using System.IO;
using System.IO.Compression;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaFiles.Archive
{
    public interface IArchiveExtractionService
    {
        void ExtractArchive(string archivePath, string destinationDirectory, bool isFullSetArchive);
        Task ExtractArchiveAsync(string archivePath, string destinationDirectory, bool isFullSetArchive, CancellationToken cancellationToken = default);
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

            var destinationDirectoryFullPath = Path.GetFullPath(destinationDirectory);
            if (!destinationDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                destinationDirectoryFullPath += Path.DirectorySeparatorChar;
            }

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue; // Skip directories
                    }

                    if (!extractAll)
                    {
                        _logger.Debug($"Skipping {entry.FullName} as ExtractFullSetCompletely is false and wanted check is pending.");
                        continue;
                    }

                    var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, entry.FullName));
                    if (!destinationPath.StartsWith(destinationDirectoryFullPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SecurityException($"Zip Slip extraction attempt blocked for entry: {entry.FullName}");
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

        public async Task ExtractArchiveAsync(string archivePath, string destinationDirectory, bool isFullSetArchive, CancellationToken cancellationToken = default)
        {
            var extractAll = !isFullSetArchive || _configService.ExtractFullSetCompletely;

            var destinationDirectoryFullPath = Path.GetFullPath(destinationDirectory);
            if (!destinationDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                destinationDirectoryFullPath += Path.DirectorySeparatorChar;
            }

            using (var archive = await ZipFile.OpenReadAsync(archivePath, cancellationToken))
            {
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue; // Skip directories
                    }

                    if (!extractAll)
                    {
                        _logger.Debug($"Skipping {entry.FullName} as ExtractFullSetCompletely is false and wanted check is pending.");
                        continue;
                    }

                    var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, entry.FullName));
                    if (!destinationPath.StartsWith(destinationDirectoryFullPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SecurityException($"Zip Slip extraction attempt blocked for entry: {entry.FullName}");
                    }

                    var entryDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                    {
                        Directory.CreateDirectory(entryDir);
                    }

                    using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                    await using (var entryStream = await entry.OpenAsync(cancellationToken))
                    {
                        await entryStream.CopyToAsync(destStream, cancellationToken);
                    }
                }
            }
        }
    }
}
