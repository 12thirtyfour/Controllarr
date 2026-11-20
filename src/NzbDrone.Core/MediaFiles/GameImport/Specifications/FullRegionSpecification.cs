using System.IO;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.GameImport.Specifications
{
    public class FullRegionSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public FullRegionSpecification(Logger logger)
        {
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.FileEpisodeInfo == null)
            {
                return ImportSpecDecision.Accept();
            }

            var extension = Path.GetExtension(localEpisode.Path);
            if (!string.IsNullOrWhiteSpace(extension) && MediaFileExtensions.RomExtensions.Contains(extension))
            {
                return ImportSpecDecision.Accept();
            }

            if (localEpisode.FileEpisodeInfo.FullSeason)
            {
                _logger.Debug("Single archive file detected as containing all games in the region.");
                return ImportSpecDecision.Reject(ImportRejectionReason.FullRegion, "Single archive file contains all games in region. Review file name or manually import");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
