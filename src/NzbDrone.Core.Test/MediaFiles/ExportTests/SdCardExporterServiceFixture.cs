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
        [TestCase(SdCardDeviceProfile.GarlicOS, "Game Boy Advance", "Metroid Fusion", "Metroid Fusion.gba", "Roms", "GBA", "Metroid Fusion.gba")]
        [TestCase(SdCardDeviceProfile.AnaloguePocket, "SNES", "Chrono Trigger", "Chrono Trigger.sfc", "Assets", "snes", "Chrono Trigger.sfc")]
        [TestCase(SdCardDeviceProfile.MiSTer, "Genesis", "Sonic the Hedgehog", "Sonic the Hedgehog.md", "games", "Genesis", "Sonic the Hedgehog.md")]
        [TestCase(SdCardDeviceProfile.RetroArch, "SNES", "EarthBound", "EarthBound.sfc", "downloads", "SNES", "EarthBound.sfc")]
        [TestCase(SdCardDeviceProfile.Batocera, "Genesis", "Streets of Rage 2", "Streets of Rage 2.md", "roms", "megadrive", "Streets of Rage 2.md")]
        [TestCase(SdCardDeviceProfile.ArkOS, "PlayStation", "Castlevania", "Castlevania.chd", "roms", "psx", "Castlevania.chd")]
        [TestCase(SdCardDeviceProfile.EmulationStation, "SNES", "Super Metroid", "Super Metroid.sfc", "ROMs", "snes", "Super Metroid.sfc")]
        [TestCase(SdCardDeviceProfile.EverDrive, "SNES", "F-Zero", "F-Zero.sfc", "SNES", "A-F", "F-Zero.sfc")]
        [TestCase(SdCardDeviceProfile.FXPakPro, "SNES", "Star Fox", "Star Fox.sfc", "sd2snes", "N-S", "Star Fox.sfc")]
        [TestCase(SdCardDeviceProfile.TwilightMenu, "Nintendo DS", "Mario Kart DS", "Mario Kart DS.nds", "roms", "nds", "Mario Kart DS.nds")]
        [TestCase(SdCardDeviceProfile.XStation, "PlayStation", "Metal Gear Solid", "Metal Gear Solid.chd", "00xstation", "G-M", "Metal Gear Solid.chd")]
        [TestCase(SdCardDeviceProfile.SwissGameCube, "GameCube", "Luigi's Mansion", "Luigis Mansion.rvz", "games", "G-M", "Luigis Mansion.rvz")]
        [TestCase(SdCardDeviceProfile.Fenrir, "Sega Saturn", "Panzer Dragoon Saga", "Panzer Dragoon Saga.chd", "saturn", "N-S", "Panzer Dragoon Saga.chd")]
        [TestCase(SdCardDeviceProfile.GDEMU, "Dreamcast", "Soulcalibur", "Soulcalibur.chd", "GDEMU", "N-S", "Soulcalibur.chd")]
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
        public void should_return_complete_supported_profile_catalog()
        {
            var profiles = _subject.GetSupportedProfiles();

            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.MiSTer);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.RetroArch);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.Batocera);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.EmulationStation);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.OnionOS);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.AnaloguePocket);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.EverDrive);
        }

        [Test]
        public void should_export_verified_roms_to_sd_card_mount_path()
        {
            var sourceRom = Path.Combine("C:", "Library", "Nintendo", "SNES", "Super Metroid (JU) [!].sfc");
            var mountPath = Path.Combine("E:", "SDCard");

            _diskProvider.Setup(d => d.FileExists(sourceRom)).Returns(true);
            _diskProvider.Setup(d => d.FolderExists(It.IsAny<string>())).Returns(false);

            var result = _subject.ExportToSdCard(
                SdCardDeviceProfile.MiSTer,
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
