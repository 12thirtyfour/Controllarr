using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class DownloadedGamesImportServiceFixture : CoreTest<DownloadedGamesImportService>
    {
        private string _droneFactory = "c:\\drop\\".AsOsAgnostic();
        private string[] _subFolders = new[] { "c:\\root\\foldername".AsOsAgnostic() };
        private string[] _videoFiles = new[] { "c:\\root\\foldername\\30.rock.s01e01.ext".AsOsAgnostic() };

        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(_videoFiles);

            Mocker.GetMock<IDiskScanService>().Setup(c => c.FilterPaths(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>()))
                  .Returns<string, IEnumerable<string>, bool>((b, s, c) => s.ToList());

            Mocker.GetMock<IDiskProvider>().Setup(c => c.GetDirectories(It.IsAny<string>()))
                  .Returns(_subFolders);

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                  .Returns(new List<ImportResult>());

            Mocker.SetConstant<IRejectedFileExtensionService>(Mocker.Resolve<RejectedFileExtensionService>());

            var downloadItem = Builder<DownloadClientItem>.CreateNew()
                .With(v => v.DownloadId = "sab1")
                .With(v => v.Status = DownloadItemStatus.Downloading)
                .Build();

            var remoteEpisode = Builder<RemoteEpisode>.CreateNew()
                .With(v => v.Series = new Series())
                .Build();

            _trackedDownload = new TrackedDownload
            {
                DownloadItem = downloadItem,
                RemoteEpisode = remoteEpisode,
                State = TrackedDownloadState.Downloading
            };
        }

        private void GivenValidSeries()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetSeries(It.IsAny<string>()))
                  .Returns(Builder<Series>.CreateNew().Build());
        }

        private void GivenSuccessfulImport()
        {
            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                  .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), It.IsAny<bool>(), It.IsAny<DownloadClientItem>(), It.IsAny<ImportMode>()))
                  .Returns(imported.Select(i => new ImportResult(i)).ToList())
                  .Callback(() => WasImportedResponse());
        }

        private void WasImportedResponse()
        {
            Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(Array.Empty<string>());
        }

        [Test]
        public void should_search_for_series_using_folder_name()
        {
            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IParsingService>().Verify(c => c.GetSeries("foldername"), Times.Once());
        }

        [Test]
        public void should_skip_if_file_is_in_use_by_another_process()
        {
            GivenValidSeries();

            Mocker.GetMock<IDiskProvider>().Setup(c => c.IsFileLocked(It.IsAny<string>()))
                  .Returns(true);

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            VerifyNoImport();
        }

        [Test]
        public void should_skip_if_no_series_found()
        {
            Mocker.GetMock<IParsingService>().Setup(c => c.GetSeries("foldername")).Returns((Series)null);

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IMakeImportDecision>()
                .Verify(c => c.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), It.IsAny<bool>(), true),
                    Times.Never());

            VerifyNoImport();
        }

        [Test]
        public void should_not_import_if_folder_is_a_series_path()
        {
            GivenValidSeries();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.SeriesPathExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskScanService>()
                  .Setup(c => c.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(Array.Empty<string>());

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IDiskScanService>()
                  .Verify(v => v.GetVideoFiles(It.IsAny<string>(), true), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_not_delete_folder_if_no_files_were_imported()
        {
            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), false, null, ImportMode.Auto))
                  .Returns(new List<ImportResult>());

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.GetFolderSize(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_delete_folder_if_files_were_imported_and_video_files_remain()
        {
            GivenValidSeries();

            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                  .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                  .Returns(imported.Select(i => new ImportResult(i)).ToList());

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_delete_folder_if_files_were_imported_and_only_sample_files_remain()
        {
            GivenValidSeries();

            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                  .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                  .Returns(imported.Select(i => new ImportResult(i)).ToList());

            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<Series>(),
                      It.IsAny<string>(),
                      It.IsAny<bool>()))
                  .Returns(DetectSampleResult.Sample);

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Once());
        }

        [TestCase("_UNPACK_")]
        [TestCase("_FAILED_")]
        public void should_remove_unpack_from_folder_name(string prefix)
        {
            var folderName = "30.rock.s01e01.pilot.hdtv-lol";
            var folders = new[] { string.Format(@"C:\Test\Unsorted\{0}{1}", prefix, folderName).AsOsAgnostic() };

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetDirectories(It.IsAny<string>()))
                  .Returns(folders);

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IParsingService>()
                .Verify(v => v.GetSeries(folderName), Times.Once());

            Mocker.GetMock<IParsingService>()
                .Verify(v => v.GetSeries(It.Is<string>(s => s.StartsWith(prefix))), Times.Never());
        }

        [Test]
        public void should_return_importresult_on_unknown_series()
        {
            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>()))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>()))
                  .Returns(true);

            var fileName = @"C:\folder\file.mkv".AsOsAgnostic();

            var result = Subject.ProcessPath(fileName);

            result.Should().HaveCount(1);
            result.First().ImportDecision.Should().NotBeNull();
            result.First().ImportDecision.LocalEpisode.Should().NotBeNull();
            result.First().ImportDecision.LocalEpisode.Path.Should().Be(fileName);
            result.First().Result.Should().Be(ImportResultType.Rejected);
        }

        [Test]
        public void should_not_delete_if_there_is_large_rar_file()
        {
            GivenValidSeries();

            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                  .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                  .Returns(imported.Select(i => new ImportResult(i)).ToList());

            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<Series>(),
                      It.IsAny<string>(),
                      It.IsAny<bool>()))
                  .Returns(DetectSampleResult.Sample);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFiles(It.IsAny<string>(), true))
                  .Returns(new[] { _videoFiles.First().Replace(".ext", ".rar") });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(It.IsAny<string>()))
                  .Returns(15.Megabytes());

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_use_folder_if_folder_import()
        {
            GivenValidSeries();

            var folderName = @"C:\media\ba09030e-1234-1234-1234-123456789abc\[HorribleSubs] Maria the Virgin Witch - 09 [720p]".AsOsAgnostic();
            var fileName = @"C:\media\ba09030e-1234-1234-1234-123456789abc\[HorribleSubs] Maria the Virgin Witch - 09 [720p]\[HorribleSubs] Maria the Virgin Witch - 09 [720p].mkv".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(folderName))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(folderName, false))
                  .Returns(new[] { fileName });

            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            Subject.ProcessPath(fileName);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), It.Is<ParsedEpisodeInfo>(v => v.AbsoluteEpisodeNumbers.First() == 9), true), Times.Once());
        }

        [Test]
        public void should_not_use_folder_if_file_import()
        {
            GivenValidSeries();

            var fileName = @"C:\media\ba09030e-1234-1234-1234-123456789abc\Torrents\[Homebrew] Maria the Witch - 09.nes".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(fileName))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(fileName))
                  .Returns(true);

            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            var result = Subject.ProcessPath(fileName);

            Mocker.GetMock<IMakeImportDecision>()
                  .Verify(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true), Times.Once());
        }

        [Test]
        public void should_not_process_if_file_and_folder_do_not_exist()
        {
            var folderName = @"C:\media\ba09030e-1234-1234-1234-123456789abc\[HorribleSubs] Maria the Virgin Witch - 09 [720p]".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(folderName))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(folderName))
                  .Returns(false);

            Subject.ProcessPath(folderName).Should().BeEmpty();

            Mocker.GetMock<IParsingService>()
                .Verify(v => v.GetSeries(It.IsAny<string>()), Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_not_delete_if_no_files_were_imported()
        {
            GivenValidSeries();

            var localEpisode = new LocalEpisode();

            var imported = new List<ImportDecision>();
            imported.Add(new ImportDecision(localEpisode));

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                  .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                  .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                  .Returns(new List<ImportResult>());

            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<Series>(),
                      It.IsAny<string>(),
                      It.IsAny<bool>()))
                  .Returns(DetectSampleResult.Sample);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(It.IsAny<string>()))
                  .Returns(15.Megabytes());

            Subject.ProcessRootFolder(new DirectoryInfo(_droneFactory));

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Never());
        }

        [Test]
        public void should_not_delete_folder_after_import()
        {
            GivenValidSeries();

            GivenSuccessfulImport();

            _trackedDownload.DownloadItem.CanMoveFiles = false;

            Subject.ProcessPath(_droneFactory, ImportMode.Auto, _trackedDownload.RemoteEpisode.Series, _trackedDownload.DownloadItem);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Never());
        }

        [Test]
        public void should_not_delete_folder_or_warn_when_unimported_rom_file_remains()
        {
            GivenValidSeries();

            Mocker.GetMock<IDiskScanService>()
                  .Setup(s => s.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(new[] { @"c:\root\foldername\Bishoujo Alien Battle (Japan) (Unl).fds" });

            Subject.ShouldDeleteFolder(new DirectoryInfo(_droneFactory), new Series()).Should().BeFalse();

            ExceptionVerification.ExpectedWarns(0);
        }

        [Test]
        public void should_delete_folder_if_importmode_move()
        {
            GivenValidSeries();

            GivenSuccessfulImport();

            _trackedDownload.DownloadItem.CanMoveFiles = false;

            Subject.ProcessPath(_droneFactory, ImportMode.Move, _trackedDownload.RemoteEpisode.Series, _trackedDownload.DownloadItem);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Once());
        }

        [Test]
        public void should_not_delete_folder_if_importmode_copy()
        {
            GivenValidSeries();

            GivenSuccessfulImport();

            _trackedDownload.DownloadItem.CanMoveFiles = true;

            Subject.ProcessPath(_droneFactory, ImportMode.Copy, _trackedDownload.RemoteEpisode.Series, _trackedDownload.DownloadItem);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(v => v.DeleteFolder(It.IsAny<string>(), true), Times.Never());
        }

        [Test]
        public void should_return_rejection_if_nothing_imported_and_contains_rar_file()
        {
            GivenValidSeries();

            var path = @"C:\Test\Unsorted\Series.Title.S01E01.abc-Sonarr".AsOsAgnostic();
            var imported = new List<ImportDecision>();

            Mocker.GetMock<IMakeImportDecision>()
                .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                .Returns(imported.Select(i => new ImportResult(i)).ToList());

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(It.IsAny<string>(), true))
                .Returns(new[] { _videoFiles.First().Replace(".ext", ".rar") });

            var result = Subject.ProcessPath(path);

            result.Count.Should().Be(1);
            result.First().Result.Should().Be(ImportResultType.Rejected);
        }

        [Test]
        public void should_return_rejection_if_nothing_imported_and_contains_executable_file()
        {
            GivenValidSeries();

            var path = @"C:\Test\Unsorted\Series.Title.S01E01.abc-Sonarr".AsOsAgnostic();
            var imported = new List<ImportDecision>();

            Mocker.GetMock<IMakeImportDecision>()
                .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), null, true, true))
                .Returns(imported);

            Mocker.GetMock<IImportApprovedGames>()
                .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto))
                .Returns(imported.Select(i => new ImportResult(i)).ToList());

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetFiles(It.IsAny<string>(), true))
                .Returns(new[] { _videoFiles.First().Replace(".ext", ".exe") });

            var result = Subject.ProcessPath(path);

            result.Count.Should().Be(1);
            result.First().Result.Should().Be(ImportResultType.Rejected);
        }

        [Test]
        public void should_reject_if_download_is_multi_season()
        {
            GivenValidSeries();

            _trackedDownload.DownloadItem.Title = "Series Title S01-S11";

            var folderName = @"C:\media\ba09030e-1234-1234-1234-123456789abc\[HorribleSubs] Maria the Virgin Witch - 09 [720p]".AsOsAgnostic();

            Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(folderName))
                .Returns(true);

            var result = Subject.ProcessPath(folderName, ImportMode.Auto, _trackedDownload.RemoteEpisode.Series, _trackedDownload.DownloadItem);

            result.Count.Should().Be(1);
            result.First().Result.Should().Be(ImportResultType.Rejected);
            result.First().ImportDecision.Rejections.First().Reason.Should().Be(ImportRejectionReason.MultiRegion);

            Mocker.GetMock<IParsingService>().Setup(c => c.GetSeries("foldername")).Returns((Series)null);

            Mocker.GetMock<IMakeImportDecision>()
                .Verify(c => c.GetImportDecisions(It.IsAny<List<string>>(), It.IsAny<Series>(), It.IsAny<DownloadClientItem>(), It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), It.IsAny<bool>(), true),
                    Times.Never());

            VerifyNoImport();
        }

        [Test]
        public void should_reject_with_duplicate_library_hash_when_failed_verification_matches_existing_library_file()
        {
            GivenValidSeries();

            var tempFile = Path.Combine(Path.GetTempPath(), $"controllarr-dup-test-{Guid.NewGuid():N}.nes");
            File.WriteAllBytes(tempFile, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            try
            {
                var platform = _trackedDownload.RemoteEpisode.Series;
                Mocker.GetMock<ISeriesService>()
                    .Setup(s => s.GetAllSeries())
                    .Returns(new List<Series> { platform });

                Mocker.GetMock<IDiskProvider>()
                    .Setup(s => s.FolderExists(tempFile))
                    .Returns(false);

                Mocker.GetMock<IDiskProvider>()
                    .Setup(s => s.FileExists(tempFile))
                    .Returns(true);

                var rejectedLocalEpisode = new LocalEpisode
                {
                    Path = tempFile,
                    Series = platform,
                    RomHashes = new Dictionary<string, string>
                    {
                        { "SHA-1", "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef" }
                    }
                };

                var rejectedDecision = new ImportDecision(
                    rejectedLocalEpisode,
                    new ImportRejection(ImportRejectionReason.UnverifiedRomHash, "ROM hash not found in DAT"));

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), platform, _trackedDownload.DownloadItem, It.IsAny<ParsedEpisodeInfo>(), null, true))
                    .Returns(new List<ImportDecision> { rejectedDecision });

                Mocker.GetMock<IMediaFileService>()
                    .Setup(s => s.GetFilesBySeriesIds(It.IsAny<List<int>>()))
                    .Returns(new List<EpisodeFile>
                    {
                        new EpisodeFile
                        {
                            SeriesId = platform.Id,
                            RelativePath = "Existing Game.nes",
                            ReleaseHash = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef"
                        }
                    });

                var result = Subject.ProcessPath(tempFile, ImportMode.Auto, platform, _trackedDownload.DownloadItem);

                result.Count.Should().Be(1);
                result.First().Result.Should().Be(ImportResultType.Rejected);
                result.First().ImportDecision.Rejections.First().Reason.Should().Be(ImportRejectionReason.DuplicateLibraryHash);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private void VerifyNoImport()
        {
            Mocker.GetMock<IImportApprovedGames>().Verify(c => c.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto),
                Times.Never());
        }

        private void VerifyImport()
        {
            Mocker.GetMock<IImportApprovedGames>().Verify(c => c.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Auto),
                Times.Once());
        }

        [Test]
        public void should_not_extract_3do_zip_when_verification_decision_is_rejected()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_reject_zip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var zipPath = Path.Combine(tempDir, "Crash 'n Burn (USA).zip");
                CreateTest3doZip(zipPath);

                var platform = new Series { Id = 5, Title = "3DO Interactive Multiplayer", TitleSlug = "3do" };
                var downloadItem = new DownloadClientItem { DownloadId = "ia-1", Title = "Crash 'n Burn (USA)", CanMoveFiles = true };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(zipPath)).Returns(false);
                Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(zipPath)).Returns(true);

                var rejectedDecision = new ImportDecision(
                    new LocalEpisode { Path = zipPath, Series = platform },
                    new ImportRejection(ImportRejectionReason.UnverifiedRomHash, "Unverified ROM Hash"));

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.Is<List<string>>(l => l.Contains(zipPath)), platform, downloadItem, It.IsAny<ParsedEpisodeInfo>(), null, true))
                    .Returns(new List<ImportDecision> { rejectedDecision });

                var expectedRejectedResult = new List<ImportResult> { new ImportResult(rejectedDecision, "Unverified ROM Hash") };
                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.Is<List<ImportDecision>>(d => d.Count == 1 && !d[0].Approved), true, downloadItem, ImportMode.Auto))
                    .Returns(expectedRejectedResult);

                var results = Subject.ProcessPath(zipPath, ImportMode.Auto, platform, downloadItem);

                results.Should().ContainSingle().Which.Result.Should().Be(ImportResultType.Rejected);
                File.Exists(zipPath).Should().BeTrue();
                Directory.GetDirectories(tempDir).Should().BeEmpty();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_extract_verified_3do_zip_import_cue_trash_temp_junk_and_delete_archive_on_move()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_move_zip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var zipPath = Path.Combine(tempDir, "Crash 'n Burn (USA).zip");
                CreateTest3doZip(zipPath);

                var platform = new Series { Id = 5, Title = "3DO Interactive Multiplayer", TitleSlug = "3do", PreferredArchiveFormat = "auto" };
                var game = new Episode { Id = 10, SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash 'n Burn" };
                var downloadItem = new DownloadClientItem { DownloadId = "ia-2", Title = "Crash 'n Burn (USA)", CanMoveFiles = true };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(zipPath)).Returns(false);
                Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(zipPath)).Returns(true);
                Mocker.GetMock<IDiskProvider>().Setup(c => c.DeleteFile(zipPath)).Callback<string>(p => File.Delete(p));

                var verifiedDecision = new ImportDecision(new LocalEpisode
                {
                    Path = zipPath,
                    Series = platform,
                    Episodes = new List<Episode> { game },
                    RomHashVerified = true
                });

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.Is<List<string>>(l => l.Contains(zipPath)), platform, downloadItem, It.IsAny<ParsedEpisodeInfo>(), null, true))
                    .Returns(new List<ImportDecision> { verifiedDecision });

                string importedCuePath = null;
                var binExistedDuringImport = false;
                var metaXmlExistedDuringImport = false;

                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, downloadItem, ImportMode.Move))
                    .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDl, item, mode) =>
                    {
                        var local = decisions.Single().LocalEpisode;
                        importedCuePath = local.Path;
                        var extractFolder = Path.GetDirectoryName(importedCuePath);
                        binExistedDuringImport = File.Exists(Path.Combine(extractFolder, "Crash 'n Burn (USA).bin"));
                        metaXmlExistedDuringImport = Directory.GetFiles(extractFolder, "*_meta.xml", SearchOption.AllDirectories).Any();
                        return new List<ImportResult> { new ImportResult(decisions.Single()) };
                    });

                var results = Subject.ProcessPath(zipPath, ImportMode.Auto, platform, downloadItem);

                results.Should().ContainSingle().Which.Result.Should().Be(ImportResultType.Imported);
                importedCuePath.Should().NotBeNull();
                Path.GetExtension(importedCuePath).Should().Be(".cue");
                binExistedDuringImport.Should().BeTrue();
                metaXmlExistedDuringImport.Should().BeTrue();
                Directory.GetDirectories(tempDir).Should().BeEmpty();
                File.Exists(zipPath).Should().BeFalse();
                Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(zipPath), Times.Once());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_extract_verified_3do_zip_and_preserve_original_archive_on_copy()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_copy_zip_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var zipPath = Path.Combine(tempDir, "Crash 'n Burn (USA).zip");
                CreateTest3doZip(zipPath);

                var platform = new Series { Id = 5, Title = "3DO Interactive Multiplayer", TitleSlug = "3do", PreferredArchiveFormat = "auto" };
                var game = new Episode { Id = 10, SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash 'n Burn" };
                var downloadItem = new DownloadClientItem { DownloadId = "ia-3", Title = "Crash 'n Burn (USA)", CanMoveFiles = false };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(zipPath)).Returns(false);
                Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(zipPath)).Returns(true);

                var verifiedDecision = new ImportDecision(new LocalEpisode
                {
                    Path = zipPath,
                    Series = platform,
                    Episodes = new List<Episode> { game },
                    RomHashVerified = true
                });

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.Is<List<string>>(l => l.Contains(zipPath)), platform, downloadItem, It.IsAny<ParsedEpisodeInfo>(), null, true))
                    .Returns(new List<ImportDecision> { verifiedDecision });

                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, downloadItem, ImportMode.Copy))
                    .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDl, item, mode) =>
                        new List<ImportResult> { new ImportResult(decisions.Single()) });

                var results = Subject.ProcessPath(zipPath, ImportMode.Auto, platform, downloadItem);

                results.Should().ContainSingle().Which.Result.Should().Be(ImportResultType.Imported);
                Directory.GetDirectories(tempDir).Should().BeEmpty();
                File.Exists(zipPath).Should().BeTrue();
                Mocker.GetMock<IDiskProvider>().Verify(d => d.DeleteFile(zipPath), Times.Never());
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static void CreateTest3doZip(string zipPath)
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            using (var s = archive.CreateEntry("Crash 'n Burn (USA).cue").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("FILE \"Crash 'n Burn (USA).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n"));
            }

            using (var s = archive.CreateEntry("Crash 'n Burn (USA).bin").Open())
            {
                s.Write(new byte[2048]);
            }

            using (var s = archive.CreateEntry("crash_n_burn_meta.xml").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("<metadata/>"));
            }

            using (var s = archive.CreateEntry("readme.txt").Open())
            {
                s.Write(Encoding.UTF8.GetBytes("IA readme"));
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_extract_tar_gz_archive_in_folder_import_valid_rom_and_respect_move_or_copy(bool canMoveFiles)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_targz_ok_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var tarGzPath = Path.Combine(tempDir, "Super Mario World (USA).tar.gz");
                CreateTestTarGz(tarGzPath, ("Super Mario World (USA).sfc", new byte[] { 1, 2, 3, 4 }));

                var platform = new Series { Id = 8, Title = "Super Nintendo Entertainment System", TitleSlug = "snes", Path = Path.Combine(tempDir, "Library") };
                var game = new Episode { Id = 22, SeasonNumber = 1, EpisodeNumber = 1, Title = "Super Mario World" };
                var downloadItem = new DownloadClientItem { DownloadId = "dl-targz", Title = "Super Mario World (USA)", CanMoveFiles = canMoveFiles };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>())).Returns<string>(p => Directory.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>())).Returns<string>(p => File.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(tempDir, true)).Returns(new[] { tarGzPath });
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(It.Is<string>(p => p != tempDir), It.IsAny<bool>()))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<string>());
                Mocker.GetMock<IDiskProvider>().Setup(c => c.DeleteFile(It.IsAny<string>())).Callback<string>(p =>
                {
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                    }
                });

                Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(tempDir, It.IsAny<bool>())).Returns(Array.Empty<string>());
                Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(It.Is<string>(p => p != tempDir), It.IsAny<bool>()))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*.sfc", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<string>());

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.Is<List<string>>(l => l.Any(f => f.EndsWith(".sfc", StringComparison.OrdinalIgnoreCase))), platform, downloadItem, It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), true))
                    .Returns<List<string>, Series, DownloadClientItem, ParsedEpisodeInfo, ParsedEpisodeInfo, bool>((paths, ser, item, dlInfo, folderInfo, check) =>
                        paths.Select(p => new ImportDecision(new LocalEpisode { Path = p, Series = ser, Episodes = new List<Episode> { game }, RomHashVerified = true })).ToList());

                var expectedMode = canMoveFiles ? ImportMode.Move : ImportMode.Copy;
                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, downloadItem, expectedMode))
                    .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((d, newDl, item, mode) =>
                        d.Where(x => x.Approved).Select(x => new ImportResult(x)).ToList());

                var results = Subject.ProcessPath(tempDir, ImportMode.Auto, platform, downloadItem);

                results.Should().ContainSingle().Which.Result.Should().Be(ImportResultType.Imported);
                Directory.GetDirectories(tempDir).Should().BeEmpty();
                File.Exists(tarGzPath).Should().Be(!canMoveFiles);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_reject_with_archive_file_when_corrupt_rar_cannot_be_extracted()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_bad_rar_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var badRarPath = Path.Combine(tempDir, "Corrupt Game (USA).rar");
                File.WriteAllBytes(badRarPath, Encoding.UTF8.GetBytes("not a valid rar payload"));

                var platform = new Series { Id = 8, Title = "Super Nintendo Entertainment System", TitleSlug = "snes", Path = Path.Combine(tempDir, "Library") };
                var downloadItem = new DownloadClientItem { DownloadId = "dl-badrar", Title = "Corrupt Game (USA)", CanMoveFiles = true };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(tempDir)).Returns(true);
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(tempDir, true)).Returns(new[] { badRarPath });
                Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(tempDir, It.IsAny<bool>())).Returns(Array.Empty<string>());

                var results = Subject.ProcessPath(tempDir, ImportMode.Auto, platform, downloadItem);

                results.Should().ContainSingle();
                results[0].Result.Should().Be(ImportResultType.Rejected);
                results[0].ImportDecision.Rejections.Should().ContainSingle()
                    .Which.Reason.Should().Be(ImportRejectionReason.ArchiveFile);
                results[0].Errors.First().Should().NotContain("might need to be extracted");
                Directory.GetDirectories(tempDir).Should().BeEmpty();
                ExceptionVerification.ExpectedWarns(1);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_reject_with_archive_file_when_extracted_archive_contains_no_valid_roms()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_empty_targz_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var tarGzPath = Path.Combine(tempDir, "OnlyDocs (USA).tar.gz");
                CreateTestTarGz(tarGzPath, ("readme.txt", Encoding.UTF8.GetBytes("no rom here")));

                var platform = new Series { Id = 8, Title = "Super Nintendo Entertainment System", TitleSlug = "snes", Path = Path.Combine(tempDir, "Library") };
                var downloadItem = new DownloadClientItem { DownloadId = "dl-emptytargz", Title = "OnlyDocs (USA)", CanMoveFiles = true };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>())).Returns<string>(p => Directory.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(tempDir, true)).Returns(new[] { tarGzPath });
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(It.Is<string>(p => p != tempDir), true))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*", SearchOption.AllDirectories) : Array.Empty<string>());
                Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>())).Returns(Array.Empty<string>());
                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, downloadItem, It.IsAny<ImportMode>()))
                    .Returns(new List<ImportResult>());

                var results = Subject.ProcessPath(tempDir, ImportMode.Auto, platform, downloadItem);

                results.Should().ContainSingle();
                results[0].Result.Should().Be(ImportResultType.Rejected);
                results[0].ImportDecision.Rejections.Should().ContainSingle()
                    .Which.Reason.Should().Be(ImportRejectionReason.ArchiveFile);
                results[0].Errors.First().Should().Contain("did not contain any valid importable ROM files");
                Directory.GetDirectories(tempDir).Should().BeEmpty();
                ExceptionVerification.ExpectedWarns(1);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static void CreateTestTarGz(string tarGzPath, params (string Name, byte[] Data)[] entries)
        {
            using var fileStream = File.Create(tarGzPath);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            using var tarWriter = new TarWriter(gzipStream, TarEntryFormat.Pax);

            foreach (var (name, data) in entries)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
                {
                    DataStream = new MemoryStream(data)
                };
                tarWriter.WriteEntry(entry);
            }
        }

        [Test]
        public void should_extract_nested_archives_recursively_skip_ia_junk_and_import_valid_rom()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_nested_ia_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var innerGameZip = Path.Combine(tempDir, "nested_source.zip");
                using (var zip = ZipFile.Open(innerGameZip, ZipArchiveMode.Create))
                {
                    using var s = zip.CreateEntry("Chrono Trigger (USA).sfc").Open();
                    s.Write(new byte[2048]);
                }

                var jp2Zip = Path.Combine(tempDir, "manual_jp2_src.zip");
                using (var zip = ZipFile.Open(jp2Zip, ZipArchiveMode.Create))
                {
                    using var s = zip.CreateEntry("0001.jp2").Open();
                    s.Write(new byte[128]);
                }

                var outerZipPath = Path.Combine(tempDir, "Chrono Trigger (USA).zip");
                using (var zip = ZipFile.Open(outerZipPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(innerGameZip, "Chrono Trigger (USA).zip");
                    zip.CreateEntryFromFile(jp2Zip, "manual_jp2.zip");
                    using var gz = zip.CreateEntry("manual_chocr.html.gz").Open();
                    gz.Write(new byte[] { 0x1F, 0x8B, 8, 0 });
                }

                File.Delete(innerGameZip);
                File.Delete(jp2Zip);

                var platform = new Series { Id = 8, Title = "Super Nintendo Entertainment System", TitleSlug = "snes", Path = Path.Combine(tempDir, "Library") };
                var game = new Episode { Id = 42, SeasonNumber = 1, EpisodeNumber = 1, Title = "Chrono Trigger" };
                var downloadItem = new DownloadClientItem { DownloadId = "dl-ia-nested", Title = "Chrono Trigger (USA)", CanMoveFiles = false };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>())).Returns<string>(p => Directory.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>())).Returns<string>(p => File.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.EnsureFolder(It.IsAny<string>())).Callback<string>(p => Directory.CreateDirectory(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.DeleteFolder(It.IsAny<string>(), true)).Callback<string, bool>((p, r) =>
                {
                    if (Directory.Exists(p))
                    {
                        Directory.Delete(p, r);
                    }
                });
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetDirectories(It.IsAny<string>()))
                    .Returns<string>(p => Directory.Exists(p) ? Directory.GetDirectories(p) : Array.Empty<string>());
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<string>());

                Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<string>());

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), platform, downloadItem, It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), true))
                    .Returns<List<string>, Series, DownloadClientItem, ParsedEpisodeInfo, ParsedEpisodeInfo, bool>((paths, ser, item, dlInfo, folderInfo, check) =>
                        paths.Select(p =>
                        {
                            if (p.EndsWith(".sfc", StringComparison.OrdinalIgnoreCase))
                            {
                                return new ImportDecision(new LocalEpisode { Path = p, Series = ser, Episodes = new List<Episode> { game }, RomHashVerified = true });
                            }

                            return new ImportDecision(
                                new LocalEpisode { Path = p, Series = ser },
                                new ImportRejection(ImportRejectionReason.UnverifiedRomHash, "Nested archive requires extraction"));
                        }).ToList());

                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, downloadItem, ImportMode.Copy))
                    .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((d, newDl, item, mode) =>
                        d.Where(x => x.Approved).Select(x => new ImportResult(x)).ToList());

                var results = Subject.ProcessPath(outerZipPath, ImportMode.Auto, platform, downloadItem);

                results.Should().Contain(r => r.Result == ImportResultType.Imported &&
                                              r.ImportDecision.LocalEpisode.Path.EndsWith("Chrono Trigger (USA).sfc", StringComparison.OrdinalIgnoreCase));
                Directory.GetDirectories(tempDir).Should().BeEmpty();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void should_extract_top_level_archive_rejected_at_depth_zero_to_inspect_inner_archive()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "dgi_depth0_reject_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var tarGzPath = Path.Combine(tempDir, "inner.tar.gz");
                CreateTestTarGz(tarGzPath, ("Donkey Kong Country (USA).sfc", new byte[2048]));

                var outerZipPath = Path.Combine(tempDir, "Donkey Kong Country (USA).zip");
                using (var zip = ZipFile.Open(outerZipPath, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(tarGzPath, "inner.tar.gz");
                }

                File.Delete(tarGzPath);

                var platform = new Series { Id = 8, Title = "Super Nintendo Entertainment System", TitleSlug = "snes", Path = Path.Combine(tempDir, "Library") };
                var game = new Episode { Id = 43, SeasonNumber = 1, EpisodeNumber = 1, Title = "Donkey Kong Country" };
                var downloadItem = new DownloadClientItem { DownloadId = "dl-depth0", Title = "Donkey Kong Country (USA)", CanMoveFiles = false };

                Mocker.GetMock<IDiskProvider>().Setup(c => c.FolderExists(It.IsAny<string>())).Returns<string>(p => Directory.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.FileExists(It.IsAny<string>())).Returns<string>(p => File.Exists(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.EnsureFolder(It.IsAny<string>())).Callback<string>(p => Directory.CreateDirectory(p));
                Mocker.GetMock<IDiskProvider>().Setup(c => c.DeleteFolder(It.IsAny<string>(), true)).Callback<string, bool>((p, r) =>
                {
                    if (Directory.Exists(p))
                    {
                        Directory.Delete(p, r);
                    }
                });
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetDirectories(It.IsAny<string>()))
                    .Returns<string>(p => Directory.Exists(p) ? Directory.GetDirectories(p) : Array.Empty<string>());
                Mocker.GetMock<IDiskProvider>().Setup(c => c.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<string>());

                Mocker.GetMock<IDiskScanService>().Setup(c => c.GetVideoFiles(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns<string, bool>((p, rec) => Directory.Exists(p) ? Directory.GetFiles(p, "*.zip", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                        .Concat(Directory.GetFiles(p, "*.sfc", rec ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)).ToArray() : Array.Empty<string>());

                Mocker.GetMock<IMakeImportDecision>()
                    .Setup(s => s.GetImportDecisions(It.IsAny<List<string>>(), platform, downloadItem, It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), true))
                    .Returns<List<string>, Series, DownloadClientItem, ParsedEpisodeInfo, ParsedEpisodeInfo, bool>((paths, ser, item, dlInfo, folderInfo, check) =>
                        paths.Select(p =>
                        {
                            if (p.EndsWith(".sfc", StringComparison.OrdinalIgnoreCase))
                            {
                                return new ImportDecision(new LocalEpisode { Path = p, Series = ser, Episodes = new List<Episode> { game }, RomHashVerified = true });
                            }

                            return new ImportDecision(
                                new LocalEpisode { Path = p, Series = ser },
                                new ImportRejection(ImportRejectionReason.UnverifiedRomHash, "Unverified archive hash"));
                        }).ToList());

                Mocker.GetMock<IImportApprovedGames>()
                    .Setup(s => s.Import(It.IsAny<List<ImportDecision>>(), true, downloadItem, ImportMode.Copy))
                    .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((d, newDl, item, mode) =>
                        d.Where(x => x.Approved).Select(x => new ImportResult(x)).ToList());

                var results = Subject.ProcessPath(outerZipPath, ImportMode.Auto, platform, downloadItem);

                results.Should().Contain(r => r.Result == ImportResultType.Imported &&
                                              r.ImportDecision.LocalEpisode.Path.EndsWith("Donkey Kong Country (USA).sfc", StringComparison.OrdinalIgnoreCase));
                Directory.GetDirectories(tempDir).Should().BeEmpty();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
