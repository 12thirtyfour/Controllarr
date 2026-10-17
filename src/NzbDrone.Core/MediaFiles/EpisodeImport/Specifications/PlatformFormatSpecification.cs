using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Platforms;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class PlatformFormatSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;

        // In a real implementation this would be injected
        private readonly PlatformFormatSettings _platformFormatSettings;

        public PlatformFormatSpecification(Logger logger)
        {
            _logger = logger;
            _platformFormatSettings = new PlatformFormatSettings(); // Mock for now
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            var fileInfo = new FileInfo(localEpisode.Path);

            // Zero-byte/corrupt stub rejection
            if (fileInfo.Length < 1024)
            {
                _logger.Debug("Rejecting file {0}: Size is less than 1KB ({1} bytes)", localEpisode.Path, fileInfo.Length);
                return ImportSpecDecision.Reject(ImportRejectionReason.PlatformFormatRejected, "File is less than 1KB (corrupt/stub)");
            }

            var extension = Path.GetExtension(localEpisode.Path).ToLowerInvariant();

            // Check profiles if available (mocked logic)
            var profile = _platformFormatSettings.Profiles.FirstOrDefault();
            if (profile != null)
            {
                if (!profile.AllowedExtensions.Contains(extension) && !profile.AllowedCustomExtensions.Contains(extension))
                {
                    return ImportSpecDecision.Reject(ImportRejectionReason.PlatformFormatRejected, $"Extension {extension} not allowed for platform");
                }

                if (fileInfo.Length < profile.MinimumSizeBytes)
                {
                    return ImportSpecDecision.Reject(ImportRejectionReason.PlatformFormatRejected, $"File smaller than allowed minimum: {fileInfo.Length} < {profile.MinimumSizeBytes}");
                }

                if (fileInfo.Length > profile.MaximumSizeBytes)
                {
                    return ImportSpecDecision.Reject(ImportRejectionReason.PlatformFormatRejected, $"File larger than allowed maximum: {fileInfo.Length} > {profile.MaximumSizeBytes}");
                }
            }

            return ImportSpecDecision.Accept();
        }
    }
}
