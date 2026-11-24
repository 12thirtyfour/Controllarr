using System;
using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.MediaFileDeletionService
{
    [TestFixture]
    public class DeleteRomFileFixture : CoreTest<Core.MediaFiles.MediaFileDeletionService>
    {
        private const string ROOT_FOLDER = @"C:\Test\TV";
        private Series _series;
        private EpisodeFile _episodeFile;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Path = Path.Combine(ROOT_FOLDER, "Series Title"))
                                     .Build();

            _episodeFile = Builder<EpisodeFile>.CreateNew()
                                               .With(f => f.RelativePath = "Series Title - S01E01")
                                               .With(f => f.Path = Path.Combine(_series.Path, "Series Title - S01E01"))
                                               .Build();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetParentFolder(_series.Path))
                  .Returns(ROOT_FOLDER);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetParentFolder(_episodeFile.Path))
                  .Returns(_series.Path);
        }

        private void GivenRootFolderExists()
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.GetBestRootFolderPath(_series.Path))
                .Returns(ROOT_FOLDER);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(ROOT_FOLDER))
                  .Returns(true);
        }

        private void GivenRootFolderHasFolders()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetDirectories(ROOT_FOLDER))
                  .Returns(new[] { _series.Path });
        }

        private void GivenSeriesFolderExists()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(_series.Path))
                  .Returns(true);
        }

        [Test]
        public void should_throw_if_root_folder_does_not_exist()
        {
            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteEpisodeFile(_series, _episodeFile));
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_should_throw_if_root_folder_is_empty()
        {
            GivenRootFolderExists();

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteEpisodeFile(_series, _episodeFile));
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_delete_from_db_if_series_folder_does_not_exist()
        {
            GivenRootFolderExists();
            GivenRootFolderHasFolders();

            Subject.DeleteEpisodeFile(_series, _episodeFile);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_episodeFile, DeleteMediaFileReason.Manual), Times.Once());
            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(_episodeFile.Path, It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_delete_from_db_if_episode_file_does_not_exist()
        {
            GivenRootFolderExists();
            GivenRootFolderHasFolders();
            GivenSeriesFolderExists();

            Subject.DeleteEpisodeFile(_series, _episodeFile);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_episodeFile, DeleteMediaFileReason.Manual), Times.Once());
            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(_episodeFile.Path, It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_delete_from_disk_and_db_if_episode_file_exists()
        {
            GivenRootFolderExists();
            GivenRootFolderHasFolders();
            GivenSeriesFolderExists();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(_episodeFile.Path))
                  .Returns(true);

            Subject.DeleteEpisodeFile(_series, _episodeFile);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(_episodeFile.Path, "Series Title"), Times.Once());
            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_episodeFile, DeleteMediaFileReason.Manual), Times.Once());
        }

        [Test]
        public void should_handle_error_deleting_episode_file()
        {
            GivenRootFolderExists();
            GivenRootFolderHasFolders();
            GivenSeriesFolderExists();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(_episodeFile.Path))
                  .Returns(true);

            Mocker.GetMock<IRecycleBinProvider>()
                  .Setup(s => s.DeleteFile(_episodeFile.Path, "Series Title"))
                  .Throws(new IOException());

            Assert.Throws<NzbDroneClientException>(() => Subject.DeleteEpisodeFile(_series, _episodeFile));

            ExceptionVerification.ExpectedErrors(1);
            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(_episodeFile.Path, "Series Title"), Times.Once());
            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_episodeFile, DeleteMediaFileReason.Manual), Times.Never());
        }

        [Test]
        public void should_delete_series_folder_when_delete_files_is_true()
        {
            GivenSeriesFolderExists();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(new Dictionary<int, string>());

            Subject.Handle(new PlatformDeletedEvent(new List<Series> { _series }, true, false));

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFolder(_series.Path), Times.Once());
        }

        [Test]
        public void should_not_delete_series_folder_when_delete_files_is_false()
        {
            GivenSeriesFolderExists();

            Subject.Handle(new PlatformDeletedEvent(new List<Series> { _series }, false, false));

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_delete_series_folder_if_path_is_parent_of_another_series()
        {
            GivenSeriesFolderExists();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(new Dictionary<int, string> { { _series.Id + 1, Path.Combine(_series.Path, "SubPlatform") } });

            Subject.Handle(new PlatformDeletedEvent(new List<Series> { _series }, true, false));

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFolder(It.IsAny<string>()), Times.Never());
            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_delete_bios_files_when_platform_deleted_with_delete_files_true()
        {
            GivenSeriesFolderExists();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesPaths())
                  .Returns(new Dictionary<int, string>());

            Subject.Handle(new PlatformDeletedEvent(new List<Series> { _series }, true, false));

            Mocker.GetMock<IBiosService>().Verify(v => v.DeletePlatformBiosFiles(_series), Times.Once());
        }

        [Test]
        public void should_not_delete_bios_files_when_platform_deleted_with_delete_files_false()
        {
            GivenSeriesFolderExists();

            Subject.Handle(new PlatformDeletedEvent(new List<Series> { _series }, false, false));

            Mocker.GetMock<IBiosService>().Verify(v => v.DeletePlatformBiosFiles(It.IsAny<Series>()), Times.Never());
        }

        [Test]
        public void should_delete_bios_files_when_executing_delete_platform_files_command()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(_series.Id))
                  .Returns(_series);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFilesBySeries(_series.Id))
                  .Returns(new List<EpisodeFile>());

            Subject.Execute(new DeletePlatformFilesCommand { SeriesIds = new List<int> { _series.Id } });

            Mocker.GetMock<IBiosService>().Verify(v => v.DeletePlatformBiosFiles(_series), Times.Once());
        }

        [Test]
        public void should_delete_companion_tracks_when_deleting_disc_sheet()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "controllarr_delete_disc_" + Guid.NewGuid().ToString("N"));
            try
            {
                var platformDir = Path.Combine(tempRoot, "3DO");
                var gameDir = Path.Combine(platformDir, "Crash");
                Directory.CreateDirectory(gameDir);

                var cuePath = Path.Combine(gameDir, "Crash.cue");
                var binPath = Path.Combine(gameDir, "Crash (Track 01).bin");
                File.WriteAllText(cuePath, "FILE \"Crash (Track 01).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
                File.WriteAllBytes(binPath, new byte[64]);

                var series = Builder<Series>.CreateNew()
                    .With(s => s.Path = platformDir)
                    .Build();
                var episodeFile = Builder<EpisodeFile>.CreateNew()
                    .With(f => f.RelativePath = Path.Combine("Crash", "Crash.cue"))
                    .Build();

                Mocker.GetMock<IRootFolderService>()
                      .Setup(s => s.GetBestRootFolderPath(platformDir))
                      .Returns(tempRoot);

                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.FolderExists(tempRoot))
                      .Returns(true);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.GetDirectories(tempRoot))
                      .Returns(new[] { platformDir });
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.FolderExists(platformDir))
                      .Returns(true);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.FileExists(cuePath))
                      .Returns(true);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.FileExists(binPath))
                      .Returns(true);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.GetParentFolder(platformDir))
                      .Returns(tempRoot);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.GetParentFolder(cuePath))
                      .Returns(gameDir);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(s => s.GetParentFolder(binPath))
                      .Returns(gameDir);

                Subject.DeleteEpisodeFile(series, episodeFile);

                Mocker.GetMock<IRecycleBinProvider>()
                      .Verify(v => v.DeleteFile(cuePath, Path.Combine("3DO", "Crash")), Times.Once());
                Mocker.GetMock<IRecycleBinProvider>()
                      .Verify(v => v.DeleteFile(binPath, Path.Combine("3DO", "Crash")), Times.Once());
                Mocker.GetMock<IMediaFileService>()
                      .Verify(v => v.Delete(episodeFile, DeleteMediaFileReason.Manual), Times.Once());
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }
    }
}
