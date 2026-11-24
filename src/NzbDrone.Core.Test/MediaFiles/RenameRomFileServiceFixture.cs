using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles
{
    public class RenameRomFileServiceFixture : CoreTest<RenameRomFileService>
    {
        private Series _series;
        private List<EpisodeFile> _episodeFiles;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .Build();

            _episodeFiles = Builder<EpisodeFile>.CreateListOfSize(2)
                                                .All()
                                                .With(e => e.SeriesId = _series.Id)
                                                .With(e => e.SeasonNumber = 1)
                                                .Build()
                                                .ToList();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(_series.Id))
                  .Returns(_series);
        }

        private void GivenNoEpisodeFiles()
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<EpisodeFile>());
        }

        private void GivenEpisodeFiles()
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                  .Returns(_episodeFiles);
        }

        private void GivenMovedFiles()
        {
            Mocker.GetMock<IMoveRomFiles>()
                  .Setup(s => s.MoveEpisodeFile(It.IsAny<EpisodeFile>(), _series));
        }

        [Test]
        public void should_not_publish_event_if_no_files_to_rename()
        {
            GivenNoEpisodeFiles();

            Subject.Execute(new RenameFilesCommand(_series.Id, new List<int> { 1 }));

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<PlatformRenamedEvent>()), Times.Never());
        }

        [Test]
        public void should_not_publish_event_if_no_files_are_renamed()
        {
            GivenEpisodeFiles();

            Mocker.GetMock<IMoveRomFiles>()
                  .Setup(s => s.MoveEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<Series>()))
                  .Throws(new SameFilenameException("Same file name", "Filename"));

            Subject.Execute(new RenameFilesCommand(_series.Id, new List<int> { 1 }));

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<PlatformRenamedEvent>()), Times.Never());
        }

        [Test]
        public void should_publish_event_if_files_are_renamed()
        {
            GivenEpisodeFiles();
            GivenMovedFiles();

            Subject.Execute(new RenameFilesCommand(_series.Id, new List<int> { 1 }));

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<PlatformRenamedEvent>()), Times.Once());
        }

        [Test]
        public void should_update_moved_files()
        {
            GivenEpisodeFiles();
            GivenMovedFiles();

            Subject.Execute(new RenameFilesCommand(_series.Id, new List<int> { 1 }));

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Update(It.IsAny<EpisodeFile>()), Times.Exactly(2));
        }

        [Test]
        public void should_get_episodefiles_by_ids_only()
        {
            GivenEpisodeFiles();
            GivenMovedFiles();

            var files = new List<int> { 1 };

            Subject.Execute(new RenameFilesCommand(_series.Id, files));

            Mocker.GetMock<IMediaFileService>()
                  .Verify(v => v.Get(files), Times.Once());
        }

        [Test]
        public void should_clean_up_empty_subfolders_after_publishing_platform_renamed_event()
        {
            GivenEpisodeFiles();
            GivenMovedFiles();

            var callOrder = new List<string>();

            Mocker.GetMock<IEventAggregator>()
                  .Setup(v => v.PublishEvent(It.IsAny<PlatformRenamedEvent>()))
                  .Callback(() => callOrder.Add("event"));

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.RemoveEmptySubfolders(_series.Path))
                  .Callback(() => callOrder.Add("cleanup"));

            Subject.Execute(new RenameFilesCommand(_series.Id, new List<int> { 1 }));

            callOrder.Should().ContainInOrder("event", "cleanup");
        }

        [Test]
        public void should_clean_up_vacated_old_folders_including_os_junk_while_preserving_real_files()
        {
            var seriesDir = Path.Combine(Path.GetTempPath(), "controllarr_rename_cleanup_" + Guid.NewGuid().ToString("N"));
            try
            {
                var oldDir = Path.Combine(seriesDir, "OldRegion", "OldGame");
                var newDir = Path.Combine(seriesDir, "NewRegion");
                var keepDir = Path.Combine(seriesDir, "KeepRegion");
                Directory.CreateDirectory(oldDir);
                Directory.CreateDirectory(keepDir);

                var oldFile = Path.Combine(oldDir, "Game.chd");
                var junkFile = Path.Combine(oldDir, "Thumbs.db");
                var keepFile = Path.Combine(keepDir, "OtherGame.chd");
                File.WriteAllText(oldFile, "rom");
                File.WriteAllText(junkFile, "junk");
                File.WriteAllText(keepFile, "other");

                _series.Path = seriesDir;

                var episodeFile = new EpisodeFile
                {
                    Id = 1,
                    SeriesId = _series.Id,
                    SeasonNumber = 1,
                    RelativePath = Path.Combine("OldRegion", "OldGame", "Game.chd")
                };

                Mocker.GetMock<IMediaFileService>()
                      .Setup(s => s.Get(It.IsAny<IEnumerable<int>>()))
                      .Returns(new List<EpisodeFile> { episodeFile });

                Mocker.GetMock<IMoveRomFiles>()
                      .Setup(s => s.MoveEpisodeFile(episodeFile, _series))
                      .Callback(() =>
                      {
                          Directory.CreateDirectory(newDir);
                          File.Move(oldFile, Path.Combine(newDir, "Game.chd"));
                          episodeFile.RelativePath = Path.Combine("NewRegion", "Game.chd");
                      });

                Subject.Execute(new RenameFilesCommand(_series.Id, new List<int> { 1 }));

                Directory.Exists(oldDir).Should().BeFalse();
                Directory.Exists(Path.Combine(seriesDir, "OldRegion")).Should().BeFalse();
                File.Exists(Path.Combine(newDir, "Game.chd")).Should().BeTrue();
                File.Exists(keepFile).Should().BeTrue();
                Directory.Exists(seriesDir).Should().BeTrue();
            }
            finally
            {
                if (Directory.Exists(seriesDir))
                {
                    Directory.Delete(seriesDir, true);
                }
            }
        }
    }
}
