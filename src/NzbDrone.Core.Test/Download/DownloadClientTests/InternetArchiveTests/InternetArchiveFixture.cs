using System;
using System.Collections.Concurrent;
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
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.InternetArchive;
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
            var manifestPath = Path.Combine(TempFolder, "appdata", "internet_archive_downloads.json");
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            _settings = new InternetArchiveSettings
            {
                UrlBase = "https://archive.org",
                AllowAnonymousFallback = true
            };
            Subject.Definition = new DownloadClientDefinition
            {
                Name = "Internet Archive",
                RemoveCompletedDownloads = true,
                RemoveFailedDownloads = true,
                Settings = _settings
            };

            Mocker.GetMock<IConfigService>().SetupGet(s => s.DownloadClientWorkingFolders)
                  .Returns(Path.Combine(TempFolder, "downloads"));

            Mocker.GetMock<IAppFolderInfo>().SetupGet(s => s.AppDataFolder)
                  .Returns(Path.Combine(TempFolder, "appdata"));

            Mocker.GetMock<IDiskProvider>().Setup(s => s.OpenWriteStream(It.IsAny<string>()))
                  .Returns(() => new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite, 81920, FileOptions.DeleteOnClose));
        }

        [TearDown]
        public void TearDown()
        {
            InternetArchive.ClearTrackedItems();
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
        public void should_use_app_data_downloads_when_download_path_is_not_set()
        {
            Subject.GetWorkingFolder().Should().Be(Path.Combine(TempFolder, "appdata", "downloads"));
        }

        [Test]
        public void should_use_configured_download_path_when_set()
        {
            var customPath = Path.Combine(TempFolder, "custom_downloads");
            _settings.DownloadPath = customPath;
            Subject.GetWorkingFolder().Should().Be(customPath);
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

            Func<Task> act = async () => await Subject.Download(remoteEpisode, null);

            await act.Should().ThrowAsync<DownloadClientException>().WithMessage("*403*");
            requests.Should().HaveCount(1);
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

        [Test]
        public async Task should_use_s3_credentials_and_primary_archive_from_indexer_when_downloader_settings_are_empty()
        {
            // Empty downloader settings
            _settings.S3AccessKey = string.Empty;
            _settings.S3SecretKey = string.Empty;
            _settings.PrimaryArchiveUrls = Array.Empty<string>();

            var indexerSettings = new InternetArchiveIndexerSettings
            {
                BaseUrl = "https://archive.org",
                S3AccessKey = "indexer_access_key",
                S3SecretKey = "indexer_secret_key",
                PrimaryArchiveUrls = new[] { "https://archive.org/details/htgdb-gamepacks" }
            };

            var indexerDef = new IndexerDefinition
            {
                Implementation = "InternetArchiveIndexer",
                Settings = indexerSettings
            };

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(f => f.All())
                  .Returns(new List<IndexerDefinition> { indexerDef });

            HttpRequest capturedRequest = null;
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<HttpRequest, CancellationToken>((req, token) => capturedRequest = req)
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0]));

            var release = new ReleaseInfo { DownloadUrl = "Super Mario World.sfc", Title = "Super Mario World" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            await Subject.Download(remoteEpisode, null);

            capturedRequest.Should().NotBeNull();
            capturedRequest.Url.FullUri.Should().Be("https://archive.org/download/htgdb-gamepacks/Super Mario World.sfc");
            capturedRequest.Headers.GetValues("Authorization").Should().ContainSingle();
            capturedRequest.Headers.GetValues("Authorization").First().Should().Be("LOW indexer_access_key:indexer_secret_key");
        }

        [Test]
        public void should_ensure_download_folder_exists_on_test()
        {
            var folder = Subject.GetWorkingFolder();
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(folder)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderWritable(folder)).Returns(true);

            var result = Subject.Test();

            result.IsValid.Should().BeTrue();
            Mocker.GetMock<IDiskProvider>().Verify(d => d.EnsureFolder(folder), Times.Once());
        }

        [Test]
        public async Task should_handle_special_characters_in_title_without_empty_filename()
        {
            var folder = Subject.GetWorkingFolder();
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(folder)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderWritable(folder)).Returns(true);

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new HttpResponse(new HttpRequest("https://archive.org"), new HttpHeader(), new byte[0]));

            var release = new ReleaseInfo { DownloadUrl = "https://archive.org/download/test/___", Title = "!!!" };
            var remoteEpisode = new RemoteEpisode { Release = release };

            var downloadId = await Subject.Download(remoteEpisode, null);

            downloadId.Should().NotBeNullOrWhiteSpace();
            downloadId.Should().StartWith(folder);
        }

        [Test]
        public void should_delete_companion_extracted_and_part_artifacts_even_when_primary_file_is_gone()
        {
            var workingFolder = Subject.GetWorkingFolder();
            var archivePath = Path.Combine(workingFolder, "StarStrike.7z");
            var partPath = archivePath + ".ia.part";
            var extractedDir = Path.Combine(workingFolder, "StarStrike_extracted");
            var extractGuidDir = Path.Combine(workingFolder, "StarStrike_extract_1234abcd");

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(workingFolder)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(archivePath)).Returns(false);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(partPath)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(extractedDir)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(extractGuidDir)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetDirectories(workingFolder)).Returns(new[] { extractedDir, extractGuidDir });

            var item = new DownloadClientItem
            {
                DownloadId = archivePath,
                Title = "StarStrike.7z",
                OutputPath = new OsPath(archivePath)
            };

            Subject.RemoveItem(item, true);

            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(partPath), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(extractedDir, true), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(extractGuidDir, true), Times.Once());
        }

        [Test]
        public void should_reconcile_leftover_files_safely_by_download_history_and_preserve_unrelated_files()
        {
            var workingFolder = Subject.GetWorkingFolder();
            _settings.DownloadPath = workingFolder;

            var importedArchive = Path.Combine(workingFolder, "ImportedGame.7z");
            var failedArchive = Path.Combine(workingFolder, "FailedGame.7z");
            var pendingArchive = Path.Combine(workingFolder, "PendingGame.7z");
            var stalePart = Path.Combine(workingFolder, "CrashedGame.7z.ia.part");
            var sabnzbdFile = Path.Combine(workingFolder, "SomeSabnzbdDownload.7z");
            var importedExtractedDir = Path.Combine(workingFolder, "ImportedGame_extracted");
            var sabnzbdExtractedDir = Path.Combine(workingFolder, "SomeSabnzbdDownload_extracted");

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(workingFolder)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFiles(workingFolder, false))
                  .Returns(new[] { importedArchive, failedArchive, pendingArchive, stalePart, sabnzbdFile });
            Mocker.GetMock<IDiskProvider>().Setup(d => d.GetDirectories(workingFolder))
                  .Returns(new[] { importedExtractedDir, sabnzbdExtractedDir });

            foreach (var f in new[] { importedArchive, failedArchive, pendingArchive, stalePart, sabnzbdFile })
            {
                Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(f)).Returns(true);
                Mocker.GetMock<IDiskProvider>().Setup(d => d.GetFileSize(f)).Returns(2048);
            }

            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(importedExtractedDir)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(sabnzbdExtractedDir)).Returns(true);

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestDownloadHistoryItem(importedArchive))
                  .Returns(new DownloadHistory
                  {
                      DownloadId = importedArchive,
                      EventType = DownloadHistoryEventType.DownloadImported,
                      Protocol = DownloadProtocol.Http,
                      SourceTitle = "Imported Game (USA)",
                      Data = new Dictionary<string, string> { { "DownloadClient", "InternetArchive" } }
                  });

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestDownloadHistoryItem(failedArchive))
                  .Returns(new DownloadHistory
                  {
                      DownloadId = failedArchive,
                      EventType = DownloadHistoryEventType.DownloadFailed,
                      Protocol = DownloadProtocol.Http,
                      SourceTitle = "Failed Game (USA)",
                      Data = new Dictionary<string, string> { { "DownloadClient", "InternetArchive" } }
                  });

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestDownloadHistoryItem(pendingArchive))
                  .Returns(new DownloadHistory
                  {
                      DownloadId = pendingArchive,
                      EventType = DownloadHistoryEventType.DownloadGrabbed,
                      Protocol = DownloadProtocol.Http,
                      SourceTitle = "Pending Game (USA)",
                      Data = new Dictionary<string, string> { { "DownloadClient", "InternetArchive" } }
                  });

            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestDownloadHistoryItem(sabnzbdFile))
                  .Returns(new DownloadHistory
                  {
                      DownloadId = "sabnzbd_nzo_123",
                      EventType = DownloadHistoryEventType.DownloadImported,
                      Protocol = DownloadProtocol.Usenet,
                      Data = new Dictionary<string, string> { { "DownloadClient", "Sabnzbd" } }
                  });

            var items = Subject.GetItems().ToList();

            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(importedArchive), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(importedExtractedDir, true), Times.AtLeastOnce());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(failedArchive), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(stalePart), Times.Once());

            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(pendingArchive), Times.Never());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(sabnzbdFile), Times.Never());
            Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFolder(sabnzbdExtractedDir, true), Times.Never());

            items.Should().ContainSingle(i => i.DownloadId == pendingArchive && i.Status == DownloadItemStatus.Completed);
        }

        [Test]
        public void should_reorder_queued_internet_archive_downloads_with_top_up_down_bottom()
        {
            var slot1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var slot2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q1Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q2Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q3Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q4Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                InternetArchive.EnqueueBackgroundDownloadForTest("slot1", "Active 1", () => slot1.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("slot2", "Active 2", () => slot2.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("q1", "Game 1", () => q1Gate.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("q2", "Game 2", () => q2Gate.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("q3", "Game 3", () => q3Gate.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("q4", "Game 4", () => q4Gate.Task);

                var initial = Subject.GetItems().ToDictionary(i => i.DownloadId);
                initial["slot1"].Status.Should().Be(DownloadItemStatus.Downloading);
                initial["slot1"].CanReorder.Should().BeFalse();
                initial["slot1"].QueuePosition.Should().BeNull();
                initial["q1"].QueuePosition.Should().Be(1);
                initial["q1"].CanReorder.Should().BeTrue();
                initial["q2"].QueuePosition.Should().Be(2);
                initial["q3"].QueuePosition.Should().Be(3);
                initial["q4"].QueuePosition.Should().Be(4);

                InternetArchive.ReorderQueuedDownloads(new[] { "q3" }, QueueReorderAction.Top);
                var afterTop = Subject.GetItems().ToDictionary(i => i.DownloadId);
                afterTop["q3"].QueuePosition.Should().Be(1);
                afterTop["q1"].QueuePosition.Should().Be(2);
                afterTop["q2"].QueuePosition.Should().Be(3);
                afterTop["q4"].QueuePosition.Should().Be(4);

                InternetArchive.ReorderQueuedDownloads(new[] { "q1" }, QueueReorderAction.Down);
                var afterDown = Subject.GetItems().ToDictionary(i => i.DownloadId);
                afterDown["q3"].QueuePosition.Should().Be(1);
                afterDown["q2"].QueuePosition.Should().Be(2);
                afterDown["q1"].QueuePosition.Should().Be(3);
                afterDown["q4"].QueuePosition.Should().Be(4);

                InternetArchive.ReorderQueuedDownloads(new[] { "q4" }, QueueReorderAction.Up);
                var afterUp = Subject.GetItems().ToDictionary(i => i.DownloadId);
                afterUp["q3"].QueuePosition.Should().Be(1);
                afterUp["q2"].QueuePosition.Should().Be(2);
                afterUp["q4"].QueuePosition.Should().Be(3);
                afterUp["q1"].QueuePosition.Should().Be(4);

                InternetArchive.ReorderQueuedDownloads(new[] { "q3", "q4" }, QueueReorderAction.Bottom);
                var afterBulkBottom = Subject.GetItems().ToDictionary(i => i.DownloadId);
                afterBulkBottom["q2"].QueuePosition.Should().Be(1);
                afterBulkBottom["q1"].QueuePosition.Should().Be(2);
                afterBulkBottom["q3"].QueuePosition.Should().Be(3);
                afterBulkBottom["q4"].QueuePosition.Should().Be(4);
            }
            finally
            {
                slot1.TrySetResult(true);
                slot2.TrySetResult(true);
                q1Gate.TrySetResult(true);
                q2Gate.TrySetResult(true);
                q3Gate.TrySetResult(true);
                q4Gate.TrySetResult(true);
            }
        }

        [Test]
        public async Task should_start_reordered_queued_download_next_when_active_slot_frees()
        {
            var slot1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var slot2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var qAGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var qBGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var qCGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var qCStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startedOrder = new ConcurrentQueue<string>();

            try
            {
                InternetArchive.EnqueueBackgroundDownloadForTest("slot1", "Active 1", () => slot1.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("slot2", "Active 2", () => slot2.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("qA", "Game A", () =>
                {
                    startedOrder.Enqueue("qA");
                    return qAGate.Task;
                });
                InternetArchive.EnqueueBackgroundDownloadForTest("qB", "Game B", () =>
                {
                    startedOrder.Enqueue("qB");
                    return qBGate.Task;
                });
                InternetArchive.EnqueueBackgroundDownloadForTest("qC", "Game C", () =>
                {
                    startedOrder.Enqueue("qC");
                    qCStarted.TrySetResult(true);
                    return qCGate.Task;
                });

                InternetArchive.ReorderQueuedDownloads(new[] { "qC" }, QueueReorderAction.Top);

                slot1.TrySetResult(true);
                await qCStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

                startedOrder.ToArray().Should().Equal("qC");

                var items = Subject.GetItems().ToDictionary(i => i.DownloadId);
                items["qC"].Status.Should().Be(DownloadItemStatus.Downloading);
                items["qC"].CanReorder.Should().BeFalse();
                items["qC"].QueuePosition.Should().BeNull();
                items["qA"].QueuePosition.Should().Be(1);
                items["qB"].QueuePosition.Should().Be(2);
            }
            finally
            {
                slot1.TrySetResult(true);
                slot2.TrySetResult(true);
                qAGate.TrySetResult(true);
                qBGate.TrySetResult(true);
                qCGate.TrySetResult(true);
            }
        }

        [Test]
        public async Task should_remove_queued_download_from_pending_queue_and_renumber_remaining()
        {
            var slot1 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var slot2 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q1Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q3Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q1Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q2Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var q3Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                InternetArchive.EnqueueBackgroundDownloadForTest("slot1", "Active 1", () => slot1.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("slot2", "Active 2", () => slot2.Task);
                InternetArchive.EnqueueBackgroundDownloadForTest("q1", "Game 1", () =>
                {
                    q1Started.TrySetResult(true);
                    return q1Gate.Task;
                });
                InternetArchive.EnqueueBackgroundDownloadForTest("q2", "Game 2", () =>
                {
                    q2Started.TrySetResult(true);
                    return Task.CompletedTask;
                });
                InternetArchive.EnqueueBackgroundDownloadForTest("q3", "Game 3", () =>
                {
                    q3Started.TrySetResult(true);
                    return q3Gate.Task;
                });

                Subject.RemoveItem(new DownloadClientItem { DownloadId = "q2" }, false);

                var afterRemove = Subject.GetItems().ToDictionary(i => i.DownloadId);
                afterRemove.Should().NotContainKey("q2");
                afterRemove["q1"].QueuePosition.Should().Be(1);
                afterRemove["q3"].QueuePosition.Should().Be(2);

                slot1.TrySetResult(true);
                await q1Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

                slot2.TrySetResult(true);
                await q3Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

                q2Started.Task.IsCompleted.Should().BeFalse();
            }
            finally
            {
                slot1.TrySetResult(true);
                slot2.TrySetResult(true);
                q1Gate.TrySetResult(true);
                q3Gate.TrySetResult(true);
            }
        }

        [Test]
        public void should_extract_identifier_only_for_compress_urls()
        {
            InternetArchive.TryExtractCompressIdentifier("https://archive.org/compress/alfapetsweden", out var baseUrl, out var id).Should().BeTrue();
            baseUrl.Should().Be("https://archive.org");
            id.Should().Be("alfapetsweden");

            InternetArchive.TryExtractCompressIdentifier("https://archive.org/download/alfapetsweden/Alfapet.chd", out _, out _).Should().BeFalse();
        }

        [Test]
        public void should_filter_non_rom_and_ia_junk_files_when_selecting_direct_download_files()
        {
            var files = new[]
            {
                new InternetArchiveFileItem { Name = "Alfapet (Sweden).chd", Size = "12345" },
                new InternetArchiveFileItem { Name = "alfapetsweden_meta.xml", Size = "500" },
                new InternetArchiveFileItem { Name = "alfapetsweden_files.xml", Size = "900" },
                new InternetArchiveFileItem { Name = "manual_jp2.zip", Size = "2000" },
                new InternetArchiveFileItem { Name = "manual_chocr.html.gz", Size = "300" },
                new InternetArchiveFileItem { Name = "__ia_thumb.jpg", Size = "100" },
                new InternetArchiveFileItem { Name = "soundtrack.wav", Size = "400" },
                new InternetArchiveFileItem { Name = "installer.exe", Size = "400" }
            };

            var selected = InternetArchive.SelectDirectDownloadFiles(files);

            selected.Should().ContainSingle().Which.Name.Should().Be("Alfapet (Sweden).chd");
        }

        [Test]
        public async Task should_bypass_compress_and_download_single_direct_file_when_metadata_resolves()
        {
            var requestedUrls = new List<string>();
            var metadataJson = "{\"result\":[{\"name\":\"Alfapet (Sweden).chd\",\"size\":\"4\"},{\"name\":\"alfapet_meta.xml\",\"size\":\"100\"}]}";

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((HttpRequest req, CancellationToken _) =>
                  {
                      requestedUrls.Add(req.Url.FullUri);
                      if (req.Url.FullUri.Contains("/metadata/", StringComparison.OrdinalIgnoreCase))
                      {
                          return new HttpResponse(req, new HttpHeader(), System.Text.Encoding.UTF8.GetBytes(metadataJson));
                      }

                      return new HttpResponse(req, new HttpHeader(), new byte[] { 1, 2, 3, 4 });
                  });

            var release = new ReleaseInfo
            {
                DownloadUrl = "https://archive.org/compress/alfapetsweden",
                Title = "Alfapet (Sweden)"
            };

            await Subject.Download(new RemoteEpisode { Release = release }, null);

            requestedUrls.Should().Contain(u => u.Contains("/metadata/alfapetsweden/files", StringComparison.OrdinalIgnoreCase));
            requestedUrls.Should().Contain(u => u.Contains("/download/alfapetsweden/Alfapet", StringComparison.OrdinalIgnoreCase));
            requestedUrls.Should().NotContain(u => u.Contains("/compress/", StringComparison.OrdinalIgnoreCase));

            var item = Subject.GetItems().Should().ContainSingle().Subject;
            item.OutputPath.FullPath.Should().EndWith("Alfapet (Sweden).chd");
        }

        [Test]
        public async Task should_bypass_compress_and_download_multi_file_disc_set_into_folder()
        {
            var requestedUrls = new List<string>();
            var metadataJson = "{\"result\":[{\"name\":\"Waynes World (USA).cue\",\"size\":\"2\"},{\"name\":\"Waynes World (USA) (Track 01).bin\",\"size\":\"4\"},{\"name\":\"waynes_files.xml\",\"size\":\"99\"}]}";

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((HttpRequest req, CancellationToken _) =>
                  {
                      requestedUrls.Add(req.Url.FullUri);
                      if (req.Url.FullUri.Contains("/metadata/", StringComparison.OrdinalIgnoreCase))
                      {
                          return new HttpResponse(req, new HttpHeader(), System.Text.Encoding.UTF8.GetBytes(metadataJson));
                      }

                      return new HttpResponse(req, new HttpHeader(), new byte[] { 1, 2, 3, 4 });
                  });

            var release = new ReleaseInfo
            {
                DownloadUrl = "https://archive.org/compress/waynesworld",
                Title = "Wayne's World (USA)"
            };

            var resultPath = await Subject.Download(new RemoteEpisode { Release = release }, null);

            requestedUrls.Should().Contain(u => u.Contains("/download/waynesworld/Waynes", StringComparison.OrdinalIgnoreCase));
            requestedUrls.Should().Contain(u => u.Contains(".cue", StringComparison.OrdinalIgnoreCase));
            requestedUrls.Should().Contain(u => u.Contains(".bin", StringComparison.OrdinalIgnoreCase));
            requestedUrls.Should().NotContain(u => u.Contains("/compress/", StringComparison.OrdinalIgnoreCase));

            resultPath.Should().NotEndWith(".zip");
        }

        [Test]
        public async Task should_fallback_to_compress_url_when_metadata_lookup_fails()
        {
            var requestedUrls = new List<string>();

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((HttpRequest req, CancellationToken _) =>
                  {
                      requestedUrls.Add(req.Url.FullUri);
                      if (req.Url.FullUri.Contains("/metadata/", StringComparison.OrdinalIgnoreCase))
                      {
                          return new HttpResponse(req, new HttpHeader(), Array.Empty<byte>(), System.Net.HttpStatusCode.NotFound);
                      }

                      return new HttpResponse(req, new HttpHeader(), new byte[] { 1, 2, 3, 4 });
                  });

            var release = new ReleaseInfo
            {
                DownloadUrl = "https://archive.org/compress/missing",
                Title = "Fallback Game"
            };

            await Subject.Download(new RemoteEpisode { Release = release }, null);

            requestedUrls.Should().Contain("https://archive.org/compress/missing");
        }
    }
}
