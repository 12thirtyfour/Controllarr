using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.InternetArchiveTests
{
    [TestFixture]
    public class InternetArchiveFixture : CoreTest<InternetArchive>
    {
        private InternetArchiveSettings _settings;

        [SetUp]
        public void SetUp()
        {
            InternetArchive.ClearTrackedItems();
            _settings = new InternetArchiveSettings
            {
                UrlBase = "https://archive.org",
                AllowAnonymousFallback = true
            };
            Subject.Definition = new DownloadClientDefinition { Settings = _settings };

            Mocker.GetMock<IConfigService>().SetupGet(s => s.DownloadClientWorkingFolders)
                  .Returns(Path.Combine(TempFolder, "downloads"));

            Mocker.GetMock<IDiskProvider>().Setup(s => s.OpenWriteStream(It.IsAny<string>()))
                  .Returns(() => new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, 81920, FileOptions.DeleteOnClose));
        }

        [Test]
        public void should_create_settings_and_use_http_protocol()
        {
            var settings = new InternetArchiveSettings();
            settings.UrlBase.Should().Be("https://archive.org");
            settings.AllowAnonymousFallback.Should().BeTrue();
            settings.PreferredArchives.Should().NotBeNull();
            Subject.Protocol.Should().Be(DownloadProtocol.Http);
        }

        [Test]
        public async Task should_resolve_relative_url_with_preferred_archive()
        {
            _settings.PreferredArchives = new[] { "nointro.snes" };

            HttpRequest capturedRequest = null;
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<HttpRequest, CancellationToken>((req, token) => capturedRequest = req)
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), Array.Empty<byte>()));

            var release = new ReleaseInfo { DownloadUrl = "Chrono Trigger (USA).zip", Title = "Chrono Trigger" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            await Subject.Download(remoteEpisode, null);

            capturedRequest.Should().NotBeNull();
            capturedRequest.Url.FullUri.Should().Be("https://archive.org/download/nointro.snes/Chrono Trigger (USA).zip");
            Subject.IsPreferredArchiveUrl(capturedRequest.Url.FullUri).Should().BeTrue();
        }

        [Test]
        public async Task should_include_authorization_header_when_keys_are_set()
        {
            _settings.S3AccessKey = "  my_access_key  ";
            _settings.S3SecretKey = "  my_secret_key  ";

            HttpRequest capturedRequest = null;
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<HttpRequest, CancellationToken>((req, token) => capturedRequest = req)
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0]));

            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test.zip", Title = "Test Game" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            await Subject.Download(remoteEpisode, null);

            capturedRequest.Should().NotBeNull();
            capturedRequest.Headers.GetValues("Authorization").Should().ContainSingle();
            capturedRequest.Headers.GetValues("Authorization").First().Should().Be("LOW my_access_key:my_secret_key");
        }

        [Test]
        public async Task should_omit_authorization_header_when_keys_are_empty()
        {
            _settings.S3AccessKey = "   ";
            _settings.S3SecretKey = "";

            HttpRequest capturedRequest = null;
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<HttpRequest, CancellationToken>((req, token) => capturedRequest = req)
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0]));

            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test.zip", Title = "Test Game" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            await Subject.Download(remoteEpisode, null);

            capturedRequest.Should().NotBeNull();
            capturedRequest.Headers.ContainsKey("Authorization").Should().BeFalse();
        }

        [Test]
        public async Task should_fallback_to_anonymous_on_403()
        {
            _settings.S3AccessKey = "my_access_key";
            _settings.S3SecretKey = "my_secret_key";
            _settings.AllowAnonymousFallback = true;

            var requests = new List<HttpRequest>();

            var unauthorizedResponse = new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0], System.Net.HttpStatusCode.Forbidden);
            var okResponse = new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0], System.Net.HttpStatusCode.OK);

            Mocker.GetMock<IHttpClient>()
                  .SetupSequence(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(unauthorizedResponse)
                  .ReturnsAsync(okResponse);

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<HttpRequest, CancellationToken>((req, token) =>
                  {
                      // We need to copy the headers because the same request object is modified in the fallback
                      var clonedReq = new HttpRequest(req.Url.FullUri);
                      if (req.Headers.ContainsKey("Authorization"))
                      {
                          clonedReq.Headers.Add("Authorization", req.Headers.GetValues("Authorization").First());
                      }

                      requests.Add(clonedReq);
                  })
                  .Returns((HttpRequest req, CancellationToken token) =>
                  {
                      if (requests.Count == 1)
                      {
                          return Task.FromResult(unauthorizedResponse);
                      }

                      return Task.FromResult(okResponse);
                  });

            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test.zip", Title = "Test Game" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            await Subject.Download(remoteEpisode, null);

            requests.Should().HaveCount(2);
            requests[0].Headers.ContainsKey("Authorization").Should().BeTrue();
            requests[1].Headers.ContainsKey("Authorization").Should().BeFalse();
        }

        [Test]
        public async Task should_not_fallback_to_anonymous_on_403_if_disabled()
        {
            _settings.S3AccessKey = "my_access_key";
            _settings.S3SecretKey = "my_secret_key";
            _settings.AllowAnonymousFallback = false;

            var requests = new List<HttpRequest>();

            var unauthorizedResponse = new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0], System.Net.HttpStatusCode.Forbidden);

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(unauthorizedResponse)
                  .Callback<HttpRequest, CancellationToken>((req, token) => requests.Add(req));

            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test.zip", Title = "Test Game" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            var result = await Subject.Download(remoteEpisode, null);

            requests.Should().HaveCount(1);
            result.Should().BeNull();
        }

        [Test]
        public void should_throw_SecurityException_on_path_traversal()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0]));

            // Inject a malicious title that will result in path traversal
            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test.zip", Title = "../../../Malicious" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            Func<Task> act = async () => await Subject.Download(remoteEpisode, null);

            act.Should().ThrowAsync<SecurityException>().WithMessage("*Path Traversal*");
        }

        [Test]
        public async Task should_report_completed_item_in_get_items_and_remove_item()
        {
            var payload = new byte[4096];
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org/download/test.zip"), new HttpHeader(), payload));

            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test.zip", Title = "Super Mario Bros. (USA)", Size = payload.Length };
            var remoteEpisode = new RemoteEpisode { Release = release };

            var downloadId = await Subject.Download(remoteEpisode, null);

            var items = Subject.GetItems().ToList();
            items.Should().ContainSingle();
            items[0].DownloadId.Should().Be(downloadId);
            items[0].Title.Should().Be("Super Mario Bros. (USA)");
            items[0].TotalSize.Should().Be(payload.Length);
            items[0].RemainingSize.Should().Be(0);
            items[0].Status.Should().Be(DownloadItemStatus.Completed);

            Subject.RemoveItem(items[0], false);
            Subject.GetItems().Should().BeEmpty();
        }
    }
}
