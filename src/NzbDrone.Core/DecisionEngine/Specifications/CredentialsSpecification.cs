using System;
using NLog;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.InternetArchive;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class CredentialsSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IProvideDownloadClient _downloadClientProvider;
        private readonly IIndexerFactory _indexerFactory;
        private readonly Logger _logger;

        public CredentialsSpecification(
            IProvideDownloadClient downloadClientProvider,
            IIndexerFactory indexerFactory,
            Logger logger)
        {
            _downloadClientProvider = downloadClientProvider;
            _indexerFactory = indexerFactory;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            var release = subject?.Release;
            if (release == null)
            {
                return DownloadSpecDecision.Accept();
            }

            if (!release.RequiresCredentials)
            {
                return DownloadSpecDecision.Accept();
            }

            var hasCredentials = false;

            // Check if matching indexer has S3 keys populated
            if (release.IndexerId > 0)
            {
                try
                {
                    var indexerDef = _indexerFactory.Get(release.IndexerId);
                    if (indexerDef?.Settings is InternetArchiveIndexerSettings iaIndexerSettings)
                    {
                        if (!string.IsNullOrWhiteSpace(iaIndexerSettings.S3AccessKey) &&
                            !string.IsNullOrWhiteSpace(iaIndexerSettings.S3SecretKey))
                        {
                            hasCredentials = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex, "Failed to inspect indexer {0} for credentials", release.IndexerId);
                }
            }

            // Check if matching download client has S3 keys populated
            if (!hasCredentials)
            {
                try
                {
                    var downloadClients = _downloadClientProvider.GetDownloadClients(
                        release.DownloadProtocol,
                        release.IndexerId,
                        false,
                        subject.Series?.Tags);

                    foreach (var client in downloadClients)
                    {
                        if (client.Definition?.Settings is InternetArchiveSettings iaClientSettings)
                        {
                            if (!string.IsNullOrWhiteSpace(iaClientSettings.S3AccessKey) &&
                                !string.IsNullOrWhiteSpace(iaClientSettings.S3SecretKey))
                            {
                                hasCredentials = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex, "Failed to inspect download clients for credentials");
                }
            }

            if (hasCredentials)
            {
                release.MissingCredentials = false;
                return DownloadSpecDecision.Accept();
            }

            release.MissingCredentials = true;
            _logger.Debug("[{0}] Requires credentials (S3 keys) which are not configured", release.Title);
            return DownloadSpecDecision.Reject(
                DownloadRejectionReason.RequiresCredentials,
                "Download requires credentials (S3 Access/Secret Key) which are not configured");
        }
    }
}
