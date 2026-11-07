using NLog;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.GameImport.Specifications
{
    public class SameGamesImportSpecification : IImportDecisionEngineSpecification
    {
        private readonly SameGamesSpecification _sameEpisodesSpecification;
        private readonly Logger _logger;

        public SameGamesImportSpecification(SameGamesSpecification sameEpisodesSpecification, Logger logger)
        {
            _sameEpisodesSpecification = sameEpisodesSpecification;
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Permanent;

        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            if (_sameEpisodesSpecification.IsSatisfiedBy(localEpisode.Episodes))
            {
                return ImportSpecDecision.Accept();
            }

            _logger.Debug("ROM file on disk contains more games than this file contains");
            return ImportSpecDecision.Reject(ImportRejectionReason.ExistingFileHasMoreGames, "ROM file on disk contains more games than this file contains");
        }
    }
}
