using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.GameImport.Manual;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.GameImport.Manual
{
    [TestFixture]
    public class ManualImportServiceFixture : CoreTest<ManualImportService>
    {
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "mis_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Test]
        public void should_deduplicate_companion_bin_when_submitted_with_cue_in_manual_import_command()
        {
            var cuePath = Path.Combine(_tempDir, "Game (USA).cue");
            var binPath = Path.Combine(_tempDir, "Game (USA).bin");
            File.WriteAllText(cuePath, "FILE \"Game (USA).bin\" BINARY\r\n  TRACK 01 MODE1/2352\r\n    INDEX 01 00:00:00\r\n");
            File.WriteAllBytes(binPath, new byte[4096]);

            var libraryPath = Path.Combine(_tempDir, "Library");
            Directory.CreateDirectory(libraryPath);

            var platform = new Series { Id = 1, Title = "Philips CD-i", TitleSlug = "cdi", Path = libraryPath };
            var game = new Episode { Id = 10, SeasonNumber = 1, EpisodeNumber = 1, Title = "The Apprentice", SeriesId = 1 };

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(1)).Returns(platform);
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(new List<Episode> { game });
            Mocker.GetMock<NzbDrone.Core.MediaFiles.GameImport.Aggregation.IAggregationService>()
                .Setup(a => a.Augment(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                .Returns<LocalEpisode, DownloadClientItem>((le, dl) => le);

            var importedPaths = new List<string>();
            Mocker.GetMock<IImportApprovedGames>()
                .Setup(i => i.Import(It.IsAny<List<ImportDecision>>(), true, null, ImportMode.Move))
                .Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDl, dl, mode) =>
                {
                    importedPaths.Add(decisions[0].LocalEpisode.Path);
                    return new List<ImportResult>
                    {
                        new ImportResult(decisions[0], new EpisodeFile { SeriesId = 1, SeasonNumber = 1, RelativePath = "Game (USA).cue" })
                    };
                });

            var command = new ManualImportCommand
            {
                ImportMode = ImportMode.Move,
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile { Path = cuePath, SeriesId = 1, GameIds = new List<int> { 10 }, Quality = new QualityModel(Quality.VerifiedGoodDump) },
                    new ManualImportFile { Path = binPath, SeriesId = 1, GameIds = new List<int> { 10 }, Quality = new QualityModel(Quality.VerifiedGoodDump) }
                }
            };

            Subject.Execute(command);

            importedPaths.Should().ContainSingle().Which.Should().Be(cuePath);
        }
    }
}
