using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.LibraryImportTests
{
    [TestFixture]
    public class UnmanagedDiskScannerFixture : CoreTest<UnmanagedDiskScanner>
    {
        private Mock<IGamePlatformRepository> _platformRepo;
        private Mock<IGameTitleRepository> _titleRepo;
        private string _tempPath;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<NzbDrone.Core.Games.IPlatformCatalogService>().Setup(c => c.GetAll()).Returns(new System.Collections.Generic.List<NzbDrone.Core.Games.PlatformDefinition>() {
                new NzbDrone.Core.Games.PlatformDefinition { RomFileExtensions = new System.Collections.Generic.List<string> { ".smc", ".sfc", ".nes" } }
            });
            _platformRepo = Mocker.GetMock<IGamePlatformRepository>();
            _titleRepo = Mocker.GetMock<IGameTitleRepository>();

            var datCache = Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>();
            datCache.Setup(c => c.FindByCrc32(Moq.It.IsAny<string>())).Returns(new NzbDrone.Core.Dat.DatEntry());
            datCache.Setup(c => c.FindByMd5(Moq.It.IsAny<string>())).Returns(new NzbDrone.Core.Dat.DatEntry());
            datCache.Setup(c => c.FindBySha1(Moq.It.IsAny<string>())).Returns(new NzbDrone.Core.Dat.DatEntry());

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

            _titleRepo.Setup(s => s.All()).Returns(new List<GameTitle>
            {
                new GameTitle { Id = 1, Title = "Super Mario World", PlatformId = 1 }
            });
            _platformRepo.Setup(s => s.All()).Returns(new List<GamePlatform>
            {
                new GamePlatform { Id = 1, Name = "SNES" }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().GameTitle.Title.Should().Be("Super Mario World");
            results.First().GamePlatform.Name.Should().Be("SNES");
            results.First().IsHighConfidenceMatch.Should().BeTrue();
            results.First().Hashes.Should().ContainKey("SHA-1");
        }

        [Test]
        public void Scan_BucketedDirectory_ShouldReturnItems()
        {
            // Arrange
            var bucketDir = Path.Combine(_tempPath, "A-F");
            Directory.CreateDirectory(bucketDir);
            var romPath = Path.Combine(bucketDir, "Chrono Trigger (USA).sfc");
            File.WriteAllBytes(romPath, new byte[] { 1, 2, 3 });

            _titleRepo.Setup(s => s.All()).Returns(new List<GameTitle>
            {
                new GameTitle { Id = 2, Title = "Chrono Trigger", PlatformId = 1 }
            });
            _platformRepo.Setup(s => s.All()).Returns(new List<GamePlatform>
            {
                new GamePlatform { Id = 1, Name = "SNES" }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().GameTitle.Title.Should().Be("Chrono Trigger");
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

            _titleRepo.Setup(s => s.All()).Returns(new List<GameTitle>
            {
                new GameTitle { Id = 3, Title = "Game", PlatformId = 2 }
            });

            // Act
            var results = Subject.Scan(_tempPath);

            // Assert
            results.Should().HaveCount(1);
            results.First().Hashes.Should().ContainKey("CRC32");
            results.First().Hashes.Should().ContainKey("SHA-1");
        }
    }
}
