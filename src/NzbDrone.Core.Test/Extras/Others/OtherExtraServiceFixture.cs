using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Extras.Others;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Extras.Others
{
    [TestFixture]
    public class OtherExtraServiceFixture : CoreTest<OtherExtraService>
    {
        private Series _series;
        private EpisodeFile _episodeFile;
        private LocalEpisode _localEpisode;

        private string _seriesFolder;
        private string _episodeFolder;

        [SetUp]
        public void Setup()
        {
            _seriesFolder = @"C:\Test\TV\Series Title".AsOsAgnostic();
            _episodeFolder = @"C:\Test\Unsorted TV\Series.Title.S01".AsOsAgnostic();

            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Path = _seriesFolder)
                                     .Build();

            var episodes = Builder<Episode>.CreateListOfSize(1)
                                           .All()
                                           .With(e => e.SeasonNumber = 1)
                                           .Build()
                                           .ToList();

            _episodeFile = Builder<EpisodeFile>.CreateNew()
                                               .With(f => f.Path = Path.Combine(_series.Path, "Season 1", "Series Title - S01E01.mkv").AsOsAgnostic())
                                               .With(f => f.RelativePath = @"Season 1\Series Title - S01E01.mkv")
                                               .Build();

            _localEpisode = Builder<LocalEpisode>.CreateNew()
                                                 .With(l => l.Series = _series)
                                                 .With(l => l.Episodes = episodes)
                                                 .With(l => l.Path = Path.Combine(_episodeFolder, "Series.Title.S01E01.mkv").AsOsAgnostic())
                                                 .With(l => l.FileEpisodeInfo = new ParsedEpisodeInfo
                                                 {
                                                     SeasonNumbers = [1],
                                                     EpisodeNumbers = [1]
                                                 })
                                                 .Build();
        }

        [Test]
        [TestCase("Series Title - S01E01.nfo", "Series Title - S01E01.nfo")]
        [TestCase("Series.Title.S01E01.nfo", "Series Title - S01E01.nfo")]
        [TestCase("Series-Title-S01E01.nfo", "Series Title - S01E01.nfo")]
        [TestCase("Series Title S01E01.nfo", "Series Title - S01E01.nfo")]
        [TestCase("Series_Title_S01E01.nfo", "Series Title - S01E01.nfo")]
        [TestCase("S01E01.thumb.jpg", "Series Title - S01E01.jpg")]
        [TestCase(@"Series.Title.S01E01\thumb.jpg", "Series Title - S01E01.jpg")]
        public void should_import_matching_file(string filePath, string expectedOutputPath)
        {
            var files = new List<string> { Path.Combine(_episodeFolder, filePath).AsOsAgnostic() };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(1);

            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", expectedOutputPath).AsOsAgnostic()).Should().Be(true);
        }

        [Test]
        public void should_not_import_multiple_nfo_files()
        {
            var files = new List<string>
            {
                Path.Combine(_episodeFolder, "Series.Title.S01E01.nfo").AsOsAgnostic(),
                Path.Combine(_episodeFolder, "Series_Title_S01E01.nfo").AsOsAgnostic(),
            };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(1);
        }

        [Test]
        [TestCase(@"audio_folder_1\Series Title S01E01.mka", @"audio_folder_2\Series Title S01E01.mka", "Series Title - S01E01.1.mka", "Series Title - S01E01.2.mka")]
        public void should_import_all_files_with_same_name(string firstExtraFilePath, string secondExtraFilePath, string firstOutputPath, string secondOutputPath)
        {
            var files = new List<string>
            {
                Path.Combine(_episodeFolder, firstExtraFilePath).AsOsAgnostic(),
                Path.Combine(_episodeFolder, secondExtraFilePath).AsOsAgnostic()
            };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(2);

            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", firstOutputPath).AsOsAgnostic()).Should().Be(true);
            results[1].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", secondOutputPath).AsOsAgnostic()).Should().Be(true);
        }

        [Test]
        public void should_increment_suffix_for_each_duplicate_file()
        {
            var files = new List<string>
            {
                Path.Combine(_episodeFolder, @"audio_folder_1\Series Title S01E01.mka").AsOsAgnostic(),
                Path.Combine(_episodeFolder, @"audio_folder_2\Series Title S01E01.mka").AsOsAgnostic(),
                Path.Combine(_episodeFolder, @"audio_folder_3\Series Title S01E01.mka").AsOsAgnostic(),
            };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(3);
            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.1.mka").AsOsAgnostic()).Should().Be(true);
            results[1].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.2.mka").AsOsAgnostic()).Should().Be(true);
            results[2].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.3.mka").AsOsAgnostic()).Should().Be(true);
        }

        [Test]
        public void should_suffix_files_matched_by_filename_prefix()
        {
            var files = new List<string>
            {
                Path.Combine(_episodeFolder, "Series.Title.S01E01.behind_scenes.mka").AsOsAgnostic(),
                Path.Combine(_episodeFolder, "Series.Title.S01E01.commentary.mka").AsOsAgnostic(),
            };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(2);
            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.1.mka").AsOsAgnostic()).Should().Be(true);
            results[1].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.2.mka").AsOsAgnostic()).Should().Be(true);
        }

        [Test]
        public void should_suffix_files_matched_by_both_filename_and_episode_info()
        {
            var files = new List<string>
            {
                Path.Combine(_episodeFolder, "Series.Title.S01E01.behind_scenes.mka").AsOsAgnostic(),
                Path.Combine(_episodeFolder, @"extras\S01E01.mka").AsOsAgnostic(),
            };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(2);
            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.1.mka").AsOsAgnostic()).Should().Be(true);
            results[1].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.2.mka").AsOsAgnostic()).Should().Be(true);
        }

        [Test]
        public void should_not_suffix_when_other_files_do_not_match_episode()
        {
            var files = new List<string>
            {
                Path.Combine(_episodeFolder, "Series.Title.S01E01.mka").AsOsAgnostic(),
                Path.Combine(_episodeFolder, "Series.Title.S01E02.mka").AsOsAgnostic(),
            };

            var results = Subject.ImportFiles(_localEpisode, _episodeFile, files, true).ToList();

            results.Count.Should().Be(1);
            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("Season 1", "Series Title - S01E01.mka").AsOsAgnostic()).Should().Be(true);
        }

        [Test]
        public void should_import_and_rename_single_game_rom_extras_and_ignore_os_junk()
        {
            var romPath = Path.Combine(_episodeFolder, "Crash 'n Burn (USA).cue").AsOsAgnostic();
            var romFile = Builder<EpisodeFile>.CreateNew()
                .With(f => f.Path = Path.Combine(_series.Path, "USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).chd").AsOsAgnostic())
                .With(f => f.RelativePath = @"USA\Crash 'n Burn (USA)\Crash 'n Burn (USA).chd")
                .Build();

            var localRom = Builder<LocalEpisode>.CreateNew()
                .With(l => l.Series = _series)
                .With(l => l.Episodes = _localEpisode.Episodes)
                .With(l => l.Path = romPath)
                .With(l => l.OtherVideoFiles = false)
                .With(l => l.FileEpisodeInfo = null)
                .Build();

            var files = new List<string>
            {
                Path.Combine(_episodeFolder, "readme.txt").AsOsAgnostic(),
                Path.Combine(_episodeFolder, "cover.jpg").AsOsAgnostic(),
                Path.Combine(_episodeFolder, "Thumbs.db").AsOsAgnostic()
            };

            var results = Subject.ImportFiles(localRom, romFile, files, false).ToList();

            results.Should().HaveCount(2);
            results[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).1.txt").AsOsAgnostic()).Should().BeTrue();
            results[1].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("USA", "Crash 'n Burn (USA)", "Crash 'n Burn (USA).2.jpg").AsOsAgnostic()).Should().BeTrue();
        }

        [Test]
        public void should_set_permissions_on_destination_extra_file_path()
        {
            var sourceExtra = Path.Combine(_episodeFolder, "manual.pdf").AsOsAgnostic();
            var romPath = Path.Combine(_episodeFolder, "Chrono Trigger (USA).sfc").AsOsAgnostic();
            var romFile = Builder<EpisodeFile>.CreateNew()
                .With(f => f.Path = Path.Combine(_series.Path, "USA", "Chrono Trigger (USA).sfc").AsOsAgnostic())
                .With(f => f.RelativePath = Path.Combine("USA", "Chrono Trigger (USA).sfc"))
                .Build();

            var localRom = Builder<LocalEpisode>.CreateNew()
                .With(l => l.Series = _series)
                .With(l => l.Episodes = _localEpisode.Episodes)
                .With(l => l.Path = romPath)
                .With(l => l.OtherVideoFiles = false)
                .With(l => l.FileEpisodeInfo = null)
                .Build();

            var results = Subject.ImportFiles(localRom, romFile, new List<string> { sourceExtra }, false).ToList();

            results.Should().ContainSingle();
            var expectedDestPath = Path.Combine(_series.Path, "USA", "Chrono Trigger (USA).pdf").AsOsAgnostic();
            Mocker.GetMock<IMediaFileAttributeService>()
                .Verify(m => m.SetFilePermissions(It.Is<string>(p => p.PathEquals(expectedDestPath))), Times.Once());
            Mocker.GetMock<IMediaFileAttributeService>()
                .Verify(m => m.SetFilePermissions(It.Is<string>(p => p.PathEquals(sourceExtra))), Times.Never());
        }

        [Test]
        public void should_preserve_distinct_suffixes_for_multiple_extras_when_moving_after_rename()
        {
            var romFile = Builder<EpisodeFile>.CreateNew()
                .With(f => f.Id = 42)
                .With(f => f.RelativePath = Path.Combine("USA", "Chrono Trigger (USA) [Rev 1].sfc"))
                .Build();

            var existingExtras = new List<OtherExtraFile>
            {
                new OtherExtraFile
                {
                    Id = 1,
                    SeriesId = _series.Id,
                    EpisodeFileId = romFile.Id,
                    RelativePath = Path.Combine("USA", "Chrono Trigger (USA).1.txt"),
                    Extension = ".txt"
                },
                new OtherExtraFile
                {
                    Id = 2,
                    SeriesId = _series.Id,
                    EpisodeFileId = romFile.Id,
                    RelativePath = Path.Combine("USA", "Chrono Trigger (USA).2.txt"),
                    Extension = ".txt"
                }
            };

            Mocker.GetMock<IOtherExtraFileService>()
                .Setup(s => s.GetFilesBySeries(_series.Id))
                .Returns(existingExtras);

            var moved = Subject.MoveFilesAfterRename(_series, new List<EpisodeFile> { romFile }).ToList();

            moved.Should().HaveCount(2);
            moved[0].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("USA", "Chrono Trigger (USA) [Rev 1].1.txt").AsOsAgnostic()).Should().BeTrue();
            moved[1].RelativePath.AsOsAgnostic().PathEquals(Path.Combine("USA", "Chrono Trigger (USA) [Rev 1].2.txt").AsOsAgnostic()).Should().BeTrue();
        }
    }
}
