using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveIndexer : HttpIndexerBase<InternetArchiveIndexerSettings>
    {
        public override string Name => "InternetArchive";

        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;

        public InternetArchiveIndexer(
            IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger,
            ILocalizationService localizationService)
            : base(httpClient, indexerStatusService, configService, parsingService, logger, localizationService)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new InternetArchiveRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new InternetArchiveParser(Settings);
        }
    }
}
