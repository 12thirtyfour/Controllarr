using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.MediaFiles.LibraryImport;
using NzbDrone.Core.MediaFiles.Synchronization;
using NzbDrone.Core.Test.Framework;
using FluentAssertions;

namespace NzbDrone.Core.Test.LibraryImportTests
{
    [TestFixture]
    public class LibraryImportServiceFixture : CoreTest<LibraryImportService>
    {
        private Mock<IArchiveRepackService> _archiveRepackService;
        private Mock<IChdmanService> _chdmanService;
        private Mock<ISidecarSaveService> _sidecarSaveService;
        private string _tempPath;

        [SetUp]
        public void Setup()
        {
            _archiveRepackService = Mocker.GetMock<IArchiveRepackService>();
            _chdmanService = Mocker.GetMock<IChdmanService>();
            _sidecarSaveService = Mocker.GetMock<ISidecarSaveService>();

            _tempPath = Path.Combine(Path.GetTempPath(), "LibraryImportServiceTests");
            if (Directory.Exists(_tempPath))
            {
                Directory.Delete(_tempPath, true);
            }
            Directory.CreateDirectory(_tempPath);

            Mocker.SetConstant<string>(_tempPath);
        }

        private LibraryImportService GetSubject()
        {
            return new LibraryImportService(
                _archiveRepackService.Object,
                _chdmanService.Object,
                _sidecarSaveService.Object,
                new NLog.LogFactory().GetCurrentClassLogger(),
                _tempPath);
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
        public void Import_CopyMode_ShouldCopyFile()
        {
            // Arrange
            var sourcePath = Path.Combine(_tempPath, "Game.chd");
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });

            var item = new ManualImportItem
            {
                Path = sourcePath,
                Name = "Game.chd",
                GamePlatform = new GamePlatform { Name = "PSX" },
                GameTitle = new GameTitle { Title = "Game" }
            };

            // Act
            GetSubject().Import(new List<ManualImportItem> { item }, ImportMode.Copy);

            // Assert
            File.Exists(sourcePath).Should().BeTrue(); // Should still exist (Copy)
            var expectedTarget = Path.Combine(_tempPath, "PSX", "Game", "Game.chd");

            _sidecarSaveService.Verify(s => s.RenameAdjacentSaves(sourcePath, expectedTarget, ImportMode.Copy), Times.Once);
        }

        [Test]
        public void Import_MoveMode_ShouldMoveFile()
        {
            // Arrange
            var sourcePath = Path.Combine(_tempPath, "GameToMove.chd");
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });

            var item = new ManualImportItem
            {
                Path = sourcePath,
                Name = "GameToMove.chd",
                GamePlatform = new GamePlatform { Name = "PSX" },
                GameTitle = new GameTitle { Title = "GameToMove" }
            };

            // Act
            GetSubject().Import(new List<ManualImportItem> { item }, ImportMode.Move);

            // Assert
            File.Exists(sourcePath).Should().BeFalse(); // Should not exist (Move)
            var expectedTarget = Path.Combine(_tempPath, "PSX", "GameToMove", "GameToMove.chd");

            _sidecarSaveService.Verify(s => s.RenameAdjacentSaves(sourcePath, expectedTarget, ImportMode.Move), Times.Once);
        }

        [Test]
        public void Canonicalize_RomFormat_SmcToSfc_And_Zip()
        {
            // Arrange
            var sourcePath = Path.Combine(_tempPath, "HeaderedGame.smc");
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });

            var item = new ManualImportItem
            {
                Path = sourcePath,
                Name = "HeaderedGame.smc",
                GamePlatform = new GamePlatform { Name = "SNES" },
                GameTitle = new GameTitle { Title = "HeaderedGame" }
            };

            // Act
            GetSubject().Import(new List<ManualImportItem> { item }, ImportMode.Copy);

            // Assert
            var expectedTarget = Path.Combine(_tempPath, "SNES", "HeaderedGame", "HeaderedGame.zip");
            _sidecarSaveService.Verify(s => s.RenameAdjacentSaves(sourcePath, expectedTarget, ImportMode.Copy), Times.Once);
            File.Exists(expectedTarget).Should().BeTrue();
        }

        [Test]
        public void Generate_M3uPlaylist_ForMultiDisc()
        {
            // Arrange
            var sourceDir = Path.Combine(_tempPath, "MultiDisc");
            Directory.CreateDirectory(sourceDir);
            var sourcePath1 = Path.Combine(sourceDir, "Game (Disc 1).cue");
            var sourcePath2 = Path.Combine(sourceDir, "Game (Disc 2).cue");
            File.WriteAllBytes(sourcePath1, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(sourcePath2, new byte[] { 1, 2, 3 });

            var item = new ManualImportItem
            {
                Path = sourcePath1,
                Name = "Game (Disc 1).cue",
                GamePlatform = new GamePlatform { Name = "PSX" },
                GameTitle = new GameTitle { Title = "Game" }
            };

            // Act
            GetSubject().Import(new List<ManualImportItem> { item }, ImportMode.Copy);

            // Assert
            var expectedM3u = Path.Combine(_tempPath, "PSX", "Game", "Game.m3u");
            _chdmanService.Verify(s => s.GenerateM3uPlaylist(It.IsAny<string[]>(), expectedM3u), Times.Once);
        }
    }
}
