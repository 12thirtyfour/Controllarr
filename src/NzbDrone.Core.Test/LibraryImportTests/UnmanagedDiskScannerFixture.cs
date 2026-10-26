using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryImportTests
{
    [TestFixture]
    public class UnmanagedDiskScannerFixture : CoreTest<UnmanagedDiskScanner>
    {
        private Mock<ISeriesService> _seriesService;
        private Mock<IEpisodeService> _episodeService;
        private string _tempPath;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IPlatformCatalogService>().Setup(c => c.GetAll()).Returns(new List<PlatformDefinition>
            {
                new PlatformDefinition { RomFileExtensions = new List<string> { ".smc", ".sfc", ".nes" } }
            });
            _seriesService = Mocker.GetMock<ISeriesService>();
            _episodeService = Mocker.GetMock<IEpisodeService>();

            var datCache = Mocker.GetMock<IDatCacheService>();
            datCache.Setup(c => c.FindByCrc32(It.IsAny<string>())).Returns(new DatEntry());
            datCache.Setup(c => c.FindByMd5(It.IsAny<string>())).Returns(new DatEntry());
            datCache.Setup(c => c.FindBySha1(It.IsAny<string>())).Returns(new DatEntry());
            datCache.Setup(c => c.FindBySha256(It.IsAny<string>())).Returns(new DatEntry());

            _tempPath = Path.Combine(Path.GetTempPath(), "UnmanagedDiskScannerTests");
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }

            Directory.CreateDirectory(_tempPath);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
        }

        [Test]
        public void Scan_FlatDirectory_ShouldReturnItems()
        {
            // Arrange
            var romPath = Path.Combine(_tempPath, "Super Mario World (USA).sfc");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, Title = "SNES" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(1)).Returns(new List<Episode>
            {
                new Episode { Id = 1, Title = "Super Mario World", SeriesId = 1 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Episodes.First().Title.Should().Be("Super Mario World");
            results.First().Series.Title.Should().Be("SNES");
            results.First().IsHighConfidenceMatch.Should().BeTrue();
            results.First().Hashes.Should().ContainKey("SHA-256");
            results.First().Hashes.Should().ContainKey("SHA-1");
            results.First().Hashes.Should().ContainKey("MD5");
            results.First().Hashes.Should().ContainKey("CRC32");
        }

        [Test]
        public void Scan_BucketedDirectory_ShouldReturnItems()
        {
            // Arrange
            var bucketDir = Path.Combine(_tempPath, "A-F");
            Directory.CreateDirectory(bucketDir);
            var romPath = Path.Combine(bucketDir, "Chrono Trigger (USA).sfc");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, Title = "SNES" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(1)).Returns(new List<Episode>
            {
                new Episode { Id = 2, Title = "Chrono Trigger", SeriesId = 1 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Episodes.First().Title.Should().Be("Chrono Trigger");
        }

        [Test]
        public void Inspect_ZipHashes_WithoutExtraction()
        {
            // Arrange
            var zipPath = Path.Combine(_tempPath, "Game.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("Game.nes");
                using (var stream = entry.Open())
                {
                    stream.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);
                }
            }

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 2, Title = "NES" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(2)).Returns(new List<Episode>
            {
                new Episode { Id = 3, Title = "Game", SeriesId = 2 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Hashes.Should().ContainKey("CRC32");
            results.First().Hashes.Should().ContainKey("SHA-1");
            results.First().Hashes.Should().ContainKey("SHA-256");
        }

        [Test]
        public void Scan_ShouldResolveGameFromUnifiedDatHashWhenFilenameIsObscured()
        {
            var romPath = Path.Combine(_tempPath, "unlabeled_dump_01.sfc");
            File.WriteAllBytes(romPath, new byte[] { 9, 8, 7 });

            Mocker.GetMock<IDatCacheService>()
                .Setup(c => c.FindBySha256(It.IsAny<string>()))
                .Returns(new DatEntry
                {
                    GameName = "EarthBound (USA)",
                    RomName = "EarthBound (USA).sfc"
                });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, Title = "Super Nintendo Entertainment System" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(1)).Returns(new List<Episode>
            {
                new Episode { Id = 42, Title = "EarthBound", SeriesId = 1, SeasonNumber = 1 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Series.Title.Should().Be("Super Nintendo Entertainment System");
            results.First().Episodes.First().Title.Should().Be("EarthBound");
            results.First().SeasonNumber.Should().Be(1);
            results.First().IsHighConfidenceMatch.Should().BeTrue();
        }

        [Test]
        public void Scan_MultiRomCollectionArchive_ShouldReturnIndividualItemsForEachRom()
        {
            var zipPath = Path.Combine(_tempPath, "@NES2.0 2023-12-16.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry1 = archive.CreateEntry("Super Mario Bros (USA).nes");
                using (var s1 = entry1.Open())
                {
                    s1.Write(new byte[] { 10, 20, 30 }, 0, 3);
                }

                var entry2 = archive.CreateEntry("The Legend of Zelda (USA).nes");
                using (var s2 = entry2.Open())
                {
                    s2.Write(new byte[] { 40, 50, 60 }, 0, 3);
                }

                var entryJunk = archive.CreateEntry("readme.txt");
                using (var sJunk = entryJunk.Open())
                {
                    sJunk.Write(new byte[] { 1, 1, 1 }, 0, 3);
                }
            }

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 101, Title = "Super Mario Bros", SeriesId = 10 },
                new Episode { Id = 102, Title = "The Legend of Zelda", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(2);
            results.Should().Contain(i => i.Name == "Super Mario Bros (USA)" && i.Path.Contains(".zip#Super Mario Bros (USA).nes"));
            results.Should().Contain(i => i.Name == "The Legend of Zelda (USA)" && i.Path.Contains(".zip#The Legend of Zelda (USA).nes"));
            results.All(i => i.Series != null && i.Series.Title == "Nintendo Entertainment System").Should().BeTrue();
            results.All(i => i.Hashes.ContainsKey("CRC32")).Should().BeTrue();
        }

        [Test]
        public void Scan_ShouldIgnoreNonGameFilesAndJunkInsideDirectories()
        {
            var romPath = Path.Combine(_tempPath, "Mega Man 2 (USA).nes");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            var junkVideo = Path.Combine(_tempPath, "trailer.mp4");
            File.WriteAllBytes(junkVideo, new byte[] { 4, 5, 6 });

            var junkText = Path.Combine(_tempPath, "info.nfo");
            File.WriteAllBytes(junkText, new byte[] { 7, 8, 9 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 103, Title = "Mega Man 2", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Mega Man 2");
            results.First().Path.Should().EndWith("Mega Man 2 (USA).nes");
        }

        [Test]
        public void Scan_RegionSubfolders_ShouldAutodetectPlatformFromExclusiveExtension()
        {
            var usaDir = Path.Combine(_tempPath, "USA");
            Directory.CreateDirectory(usaDir);
            var romPath = Path.Combine(usaDir, "Castlevania (USA).nes");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 10, Title = "Nintendo Entertainment System", CleanTitle = "nes" }
            });
            _episodeService.Setup(s => s.GetEpisodeBySeries(10)).Returns(new List<Episode>
            {
                new Episode { Id = 104, Title = "Castlevania", SeriesId = 10 }
            });

            var results = Subject.Scan(_tempPath);

            results.Should().HaveCount(1);
            results.First().Series.Should().NotBeNull();
            results.First().Series.Title.Should().Be("Nintendo Entertainment System");
            results.First().Episodes.First().Title.Should().Be("Castlevania");
        }
    }
}
