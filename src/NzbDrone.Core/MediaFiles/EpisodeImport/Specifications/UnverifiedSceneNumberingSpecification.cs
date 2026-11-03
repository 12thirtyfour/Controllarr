using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.EpisodeImport.Specifications
{
    public class UnverifiedSceneNumberingSpecification : IImportDecisionEngineSpecification
    {
        public ImportSpecDecision IsSatisfiedBy(LocalEpisode localEpisode, DownloadClientItem downloadClientItem)
        {
            return ImportSpecDecision.Accept();
        }
    }
}
