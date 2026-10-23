using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles.Export;

namespace NzbDrone.Core.Test.MediaFiles.ExportTests
{
    [TestFixture]
    public class SdCardExporterServiceFixture
    {
        private Mock<IDiskProvider> _diskProvider;
        private SdCardExporterService _subject;

        [SetUp]
        public void Setup()
        {
            _diskProvider = new Mock<IDiskProvider>();
            _subject = new SdCardExporterService(_diskProvider.Object, LogManager.GetCurrentClassLogger());
        }

        [TestCase(SdCardDeviceProfile.OnionOS, "SNES", "Super Mario World", "Super Mario World.sfc", "Roms", "SFC", "Super Mario World.sfc")]
        [TestCase(SdCardDeviceProfile.AnaloguePocket, "SNES", "Chrono Trigger", "Chrono Trigger.sfc", "Assets", "snes", "Chrono Trigger.sfc")]
        [TestCase(SdCardDeviceProfile.EverDrive, "SNES", "F-Zero", "F-Zero.sfc", "SNES", "A-F", "F-Zero.sfc")]
        [TestCase(SdCardDeviceProfile.FXPakPro, "SNES", "Star Fox", "Star Fox.sfc", "sd2snes", "N-S", "Star Fox.sfc")]
        public void should_build_device_specific_relative_path(
            SdCardDeviceProfile profile,
            string platformFolder,
            string gameTitle,
            string fileName,
            string expectedRootSegment,
            string expectedSecondSegment,
            string expectedLeaf)
        {
            var relativePath = _subject.BuildDeviceRelativePath(profile, platformFolder, gameTitle, fileName);

            relativePath.Should().StartWith(Path.Combine(expectedRootSegment, expectedSecondSegment));
            relativePath.Should().EndWith(expectedLeaf);
        }

        [Test]
        public void should_export_verified_roms_to_sd_card_mount_path()
        {
            var sourceRom = Path.Combine("C:", "Library", "Nintendo", "SNES", "Super Metroid (JU) [!].sfc");
            var mountPath = Path.Combine("E:", "SDCard");

            _diskProvider.Setup(d => d.FileExists(sourceRom)).Returns(true);
            _diskProvider.Setup(d => d.FolderExists(It.IsAny<string>())).Returns(false);

            var result = _subject.ExportToSdCard(
                SdCardDeviceProfile.EverDrive,
                mountPath,
                new List<SdCardExportItem>
                {
                    new SdCardExportItem
                    {
                        SourceFilePath = sourceRom,
                        PlatformFolder = "SNES",
                        GameTitle = "Super Metroid"
                    }
                },
                overwrite: true);

            result.ExportedCount.Should().Be(1);
            result.DestinationPaths.Should().ContainSingle();
            _diskProvider.Verify(d => d.CreateFolder(It.IsAny<string>()), Times.Once);
            _diskProvider.Verify(d => d.CopyFile(sourceRom, It.IsAny<string>(), true), Times.Once);
        }
    }
}
