using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.GameImport.Specifications
{
    public class SplitGameSpecification : IImportDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public SplitGameSpecification(Logger logger)
        {
            _logger = logger;
        }

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (localEpisode.FileEpisodeInfo == null)
            {
                return ImportSpecDecision.Accept();
            }

            if (localEpisode.FileEpisodeInfo.IsSplitEpisode)
            {
                _logger.Debug("Single game split into multiple files");
                return ImportSpecDecision.Reject(ImportRejectionReason.SplitGame, "Single game split into multiple files");
            }

            return ImportSpecDecision.Accept();
        }
    }
}
