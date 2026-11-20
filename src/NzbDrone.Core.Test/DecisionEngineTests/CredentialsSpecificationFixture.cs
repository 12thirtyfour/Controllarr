using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.InternetArchive;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class CredentialsSpecificationFixture : CoreTest<CredentialsSpecification>
    {
        private RemoteEpisode _remoteEpisode;
        private ReleaseInfo _release;

        [SetUp]
        public void Setup()
        {
            _release = new ReleaseInfo
            {
                Title = "Chrono Trigger (USA)",
                IndexerId = 1,
                DownloadProtocol = DownloadProtocol.Http
            };

            _remoteEpisode = new RemoteEpisode
            {
                Release = _release,
                Series = new Series()
            };

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(It.IsAny<DownloadProtocol>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<HashSet<int>>()))
                  .Returns(new List<IDownloadClient>());
        }

        [Test]
        public void should_accept_when_release_does_not_require_credentials()
        {
            _release.RequiresCredentials = false;

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeTrue();
            _release.MissingCredentials.Should().BeFalse();
        }

        [Test]
        public void should_reject_when_release_requires_credentials_and_none_are_configured()
        {
            _release.RequiresCredentials = true;

            var indexerDef = new IndexerDefinition
            {
                Id = 1,
                Settings = new InternetArchiveIndexerSettings
                {
                    S3AccessKey = string.Empty,
                    S3SecretKey = string.Empty
                }
            };

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.Get(1))
                  .Returns(indexerDef);

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.RequiresCredentials);
            _release.MissingCredentials.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_release_requires_credentials_and_indexer_has_s3_keys()
        {
            _release.RequiresCredentials = true;

            var indexerDef = new IndexerDefinition
            {
                Id = 1,
                Settings = new InternetArchiveIndexerSettings
                {
                    S3AccessKey = "my_access_key",
                    S3SecretKey = "my_secret_key"
                }
            };

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.Get(1))
                  .Returns(indexerDef);

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeTrue();
            _release.MissingCredentials.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_release_requires_credentials_and_download_client_has_s3_keys()
        {
            _release.RequiresCredentials = true;

            var mockClient = new Mock<IDownloadClient>();
            mockClient.SetupGet(c => c.Definition)
                      .Returns(new DownloadClientDefinition
                      {
                          Settings = new InternetArchiveSettings
                          {
                              S3AccessKey = "client_key",
                              S3SecretKey = "client_secret"
                          }
                      });

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(s => s.GetDownloadClients(DownloadProtocol.Http, 1, false, It.IsAny<HashSet<int>>()))
                  .Returns(new List<IDownloadClient> { mockClient.Object });

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation());

            decision.Accepted.Should().BeTrue();
            _release.MissingCredentials.Should().BeFalse();
        }
    }
}
