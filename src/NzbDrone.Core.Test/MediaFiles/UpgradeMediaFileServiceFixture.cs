using System;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    public class UpgradeMediaFileServiceFixture : CoreTest<UpgradeMediaFileService>
    {
        private EpisodeFile _episodeFile;
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode();
            _localEpisode.Series = new Series
                                   {
                                       Path = @"C:\Test\TV\Series".AsOsAgnostic()
                                   };

            _episodeFile = Builder<EpisodeFile>
                .CreateNew()
                .Build();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderExists(Directory.GetParent(_localEpisode.Series.Path).FullName))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.GetParentFolder(It.IsAny<string>()))
                  .Returns<string>(c => Path.GetDirectoryName(c));
        }

        private void GivenSingleEpisodeWithSingleEpisodeFile()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    RelativePath = @"Season 01\30.rock.s01e01.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        private void GivenMultipleEpisodesWithSingleEpisodeFile()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(2)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    RelativePath = @"Season 01\30.rock.s01e01.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        private void GivenMultipleEpisodesWithMultipleEpisodeFiles()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(2)
                                                     .TheFirst(1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 1,
                                                                                    RelativePath = @"Season 01\30.rock.s01e01.avi",
                                                                                })
                                                     .TheNext(1)
                                                     .With(e => e.EpisodeFile = new EpisodeFile
                                                                                {
                                                                                    Id = 2,
                                                                                    RelativePath = @"Season 01\30.rock.s01e02.avi",
                                                                                })
                                                     .Build()
                                                     .ToList();
        }

        [Test]
        public void should_delete_single_episode_file_once()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_the_same_episode_file_only_once()
        {
            GivenMultipleEpisodesWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_delete_multiple_different_episode_files()
        {
            GivenMultipleEpisodesWithMultipleEpisodeFiles();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Test]
        public void should_delete_episode_file_from_database()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(It.IsAny<EpisodeFile>(), DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_delete_existing_file_fromdb_if_file_doesnt_exist()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localEpisode.Episodes.Single().EpisodeFile, DeleteMediaFileReason.Upgrade), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_not_try_to_recyclebin_existing_file_if_file_doesnt_exist()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Mocker.GetMock<IDiskProvider>()
                .Setup(c => c.FileExists(It.IsAny<string>()))
                .Returns(false);

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IRecycleBinProvider>().Verify(v => v.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_return_old_episode_file_in_oldFiles()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode).OldFiles.Count.Should().Be(1);
        }

        [Test]
        public void should_return_old_episode_files_in_oldFiles()
        {
            GivenMultipleEpisodesWithMultipleEpisodeFiles();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode).OldFiles.Count.Should().Be(2);
        }

        [Test]
        public void should_throw_if_there_are_existing_episode_files_and_the_root_folder_is_missing()
        {
            GivenSingleEpisodeWithSingleEpisodeFile();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(c => c.FolderExists(Directory.GetParent(_localEpisode.Series.Path).FullName))
                  .Returns(false);

            Assert.Throws<RootFolderNotFoundException>(() => Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode));

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localEpisode.Episodes.Single().EpisodeFile, DeleteMediaFileReason.Upgrade), Times.Never());
        }

        [Test]
        public void should_import_if_existing_file_doesnt_exist_in_db()
        {
            _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                                                     .All()
                                                     .With(e => e.EpisodeFileId = 1)
                                                     .With(e => e.EpisodeFile = new LazyLoaded<EpisodeFile>(null))
                                                     .Build()
                                                     .ToList();

            Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

            Mocker.GetMock<IMediaFileService>().Verify(v => v.Delete(_localEpisode.Episodes.Single().EpisodeFile, It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }

        [Test]
        public void should_delete_companion_tracks_when_upgrading_disc_sheet()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "controllarr_upgrade_disc_" + Guid.NewGuid().ToString("N"));
            try
            {
                var platformDir = Path.Combine(tempRoot, "Saturn");
                var gameDir = Path.Combine(platformDir, "Panzer");
                Directory.CreateDirectory(gameDir);

                var cuePath = Path.Combine(gameDir, "Panzer.cue");
                var binPath = Path.Combine(gameDir, "Panzer (Track 01).bin");
                File.WriteAllText(cuePath, "FILE \"Panzer (Track 01).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
                File.WriteAllBytes(binPath, new byte[64]);

                _localEpisode.Series = new Series { Path = platformDir };
                _localEpisode.Episodes = Builder<Episode>.CreateListOfSize(1)
                    .All()
                    .With(e => e.EpisodeFileId = 1)
                    .With(e => e.EpisodeFile = new EpisodeFile
                    {
                        Id = 1,
                        RelativePath = Path.Combine("Panzer", "Panzer.cue")
                    })
                    .Build()
                    .ToList();

                Mocker.GetMock<IDiskProvider>()
                      .Setup(c => c.FolderExists(tempRoot))
                      .Returns(true);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(c => c.GetParentFolder(platformDir))
                      .Returns(tempRoot);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(c => c.GetParentFolder(cuePath))
                      .Returns(gameDir);
                Mocker.GetMock<IDiskProvider>()
                      .Setup(c => c.GetParentFolder(binPath))
                      .Returns(gameDir);

                Subject.UpgradeEpisodeFile(_episodeFile, _localEpisode);

                Mocker.GetMock<IRecycleBinProvider>()
                      .Verify(v => v.DeleteFile(cuePath, Path.Combine("Saturn", "Panzer")), Times.Once());
                Mocker.GetMock<IRecycleBinProvider>()
                      .Verify(v => v.DeleteFile(binPath, Path.Combine("Saturn", "Panzer")), Times.Once());
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
