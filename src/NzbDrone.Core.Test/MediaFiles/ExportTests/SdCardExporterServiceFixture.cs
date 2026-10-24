using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.MediaFiles.Export;

namespace NzbDrone.Core.Test.MediaFiles.ExportTests
{
    [TestFixture]
    public class SdCardExporterServiceFixture
    {
        private Mock<IDiskProvider> _diskProvider;
        private Mock<IDiscCompressionService> _discCompressionService;
        private SdCardExporterService _subject;

        [SetUp]
        public void Setup()
        {
            _diskProvider = new Mock<IDiskProvider>();
            _discCompressionService = new Mock<IDiscCompressionService>();
            _subject = new SdCardExporterService(
                _diskProvider.Object,
                LogManager.GetCurrentClassLogger(),
                _discCompressionService.Object);
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
        public void should_keep_files_as_is_when_format_optimization_is_not_opted_in()
        {
            var plan = _subject.ResolveExportPlan(
                SdCardDeviceProfile.MiSTer,
                "SNES",
                "Super Mario World",
                "Super Mario World.sfc",
                optimizeFormatsForDevice: false);

            plan.Transformation.Should().Be(SdCardFormatTransformation.CopyAsIs);
            plan.RelativeDestinationPath.Should().Be(Path.Combine("games", "SNES", "N-S", "Super Mario World.sfc"));
        }

        [Test]
        public void should_zip_cartridge_roms_and_use_chd_on_mister_when_opted_in()
        {
            var snesPlan = _subject.ResolveExportPlan(
                SdCardDeviceProfile.MiSTer,
                "SNES",
                "Super Mario World",
                "Super Mario World.sfc",
                optimizeFormatsForDevice: true);

            snesPlan.Transformation.Should().Be(SdCardFormatTransformation.ZipSingleRom);
            snesPlan.RelativeDestinationPath.Should().Be(Path.Combine("games", "SNES", "N-S", "Super Mario World.zip"));

            var segaCdPlan = _subject.ResolveExportPlan(
                SdCardDeviceProfile.MiSTer,
                "Sega CD",
                "Sonic CD",
                "Sonic CD.cue",
                optimizeFormatsForDevice: true);

            segaCdPlan.Transformation.Should().Be(SdCardFormatTransformation.CompressDiscToChd);
            segaCdPlan.RelativeDestinationPath.Should().Be(Path.Combine("games", "MegaCD", "N-S", "Sonic CD.chd"));
        }

        [Test]
        public void should_convert_chd_to_cue_bin_for_flashcarts_and_odes_that_do_not_support_chd_when_opted_in()
        {
            var everDriveSegaCdPlan = _subject.ResolveExportPlan(
                SdCardDeviceProfile.EverDrive,
                "Sega CD",
                "Sonic CD",
                "Sonic CD.chd",
                optimizeFormatsForDevice: true);

            everDriveSegaCdPlan.Transformation.Should().Be(SdCardFormatTransformation.ConvertChdToCueBin);
            everDriveSegaCdPlan.RelativeDestinationPath.Should().Be(Path.Combine("Sega CD", "N-S", "Sonic CD.cue"));

            var xStationPlan = _subject.ResolveExportPlan(
                SdCardDeviceProfile.XStation,
                "PlayStation",
                "Final Fantasy VII",
                "Final Fantasy VII.chd",
                optimizeFormatsForDevice: true);

            xStationPlan.Transformation.Should().Be(SdCardFormatTransformation.ConvertChdToCueBin);
            xStationPlan.RelativeDestinationPath.Should().Be(Path.Combine("00xstation", "A-F", "Final Fantasy VII.cue"));
        }

        [Test]
        public void should_extract_zip_for_flashcarts_and_analogue_pocket_when_opted_in()
        {
            var pocketPlan = _subject.ResolveExportPlan(
                SdCardDeviceProfile.AnaloguePocket,
                "SNES",
                "Chrono Trigger",
                "Chrono Trigger.zip",
                optimizeFormatsForDevice: true);

            pocketPlan.Transformation.Should().Be(SdCardFormatTransformation.ExtractZipToRawRom);
            pocketPlan.RelativeDestinationPath.Should().Be(Path.Combine("Assets", "snes", "common", "A-F", "Chrono Trigger.sfc"));
        }

        [Test]
        public void should_return_complete_supported_profile_catalog()
        {
            var profiles = _subject.GetSupportedProfiles();

            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.MiSTer && p.SupportsZipPerRom && p.SupportsChdForOpticalDiscs);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.RetroArch && p.SupportsZipPerRom && p.SupportsChdForOpticalDiscs);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.EverDrive && !p.SupportsChdForOpticalDiscs && !p.SupportsZipPerRom);
            profiles.Should().Contain(p => p.Profile == SdCardDeviceProfile.AnaloguePocket && !p.SupportsZipPerRom);
        }

        [Test]
        public void should_invoke_chd_decompression_when_exporting_sega_cd_chd_to_everdrive_with_optimization_opted_in()
        {
            var sourceChd = Path.Combine("C:", "Library", "Sega", "Sega CD", "Sonic CD (USA).chd");
            var mountPath = Path.Combine("E:", "EverDrive");
            var expectedDestFolder = Path.Combine(mountPath, "Sega CD", "N-S");
            var expectedCueOut = Path.Combine(expectedDestFolder, "Sonic CD (USA).cue");

            _diskProvider.Setup(d => d.FileExists(sourceChd)).Returns(true);
            _diskProvider.Setup(d => d.FolderExists(expectedDestFolder)).Returns(false);
            _discCompressionService
                .Setup(c => c.DecompressDisc(sourceChd, expectedDestFolder, out expectedCueOut, "Sonic CD (USA)"))
                .Returns(true);

            var result = _subject.ExportToSdCard(
                SdCardDeviceProfile.EverDrive,
                mountPath,
                new List<SdCardExportItem>
                {
                    new SdCardExportItem
                    {
                        SourceFilePath = sourceChd,
                        PlatformFolder = "Sega CD",
                        GameTitle = "Sonic CD"
                    }
                },
                overwrite: true,
                optimizeFormatsForDevice: true);

            result.ExportedCount.Should().Be(1);
            result.AppliedTransformations.Should().ContainSingle().Which.Should().Be(SdCardFormatTransformation.ConvertChdToCueBin);
            result.DestinationPaths.Should().ContainSingle().Which.Should().Be(expectedCueOut);
        }

        [Test]
        public void should_copy_platform_media_assets_and_gamelist_xml_during_export()
        {
            var sourcePlatformDir = Path.Combine("C:", "Library", "SNES");
            var sourceRomPath = Path.Combine(sourcePlatformDir, "Super Mario World.sfc");
            var sourceMediaBox2dDir = Path.Combine(sourcePlatformDir, "media", "box2d");
            var sourceBox2dFile = Path.Combine(sourceMediaBox2dDir, "Super Mario World.png");
            var sourceGamelistPath = Path.Combine(sourcePlatformDir, "gamelist.xml");

            var mountPath = Path.Combine("E:", "ExportSD");
            var targetPlatformDir = Path.Combine(mountPath, "roms", "snes");
            var targetMediaBox2dDir = Path.Combine(targetPlatformDir, "media", "box2d");
            var targetBox2dFile = Path.Combine(targetMediaBox2dDir, "Super Mario World.png");
            var targetGamelistPath = Path.Combine(targetPlatformDir, "gamelist.xml");

            _diskProvider.Setup(d => d.FileExists(sourceRomPath)).Returns(true);
            _diskProvider.Setup(d => d.FolderExists(sourceMediaBox2dDir)).Returns(true);
            _diskProvider.Setup(d => d.GetFiles(sourceMediaBox2dDir, false)).Returns(new[] { sourceBox2dFile });
            _diskProvider.Setup(d => d.FileExists(sourceGamelistPath)).Returns(true);

            var result = _subject.ExportToSdCard(
                SdCardDeviceProfile.Batocera,
                mountPath,
                new List<SdCardExportItem>
                {
                    new SdCardExportItem
                    {
                        SourceFilePath = sourceRomPath,
                        PlatformFolder = "SNES",
                        GameTitle = "Super Mario World"
                    }
                },
                overwrite: true);

            result.ExportedCount.Should().Be(1);
            result.ExportedMediaAssetCount.Should().Be(1);
            result.ExportedMediaPaths.Should().Contain(targetBox2dFile);
            result.ExportedMediaPaths.Should().Contain(targetGamelistPath);

            _diskProvider.Verify(d => d.CopyFile(sourceRomPath, It.IsAny<string>(), true), Times.Once);
            _diskProvider.Verify(d => d.CreateFolder(targetMediaBox2dDir), Times.Once);
            _diskProvider.Verify(d => d.CopyFile(sourceBox2dFile, targetBox2dFile, true), Times.Once);
            _diskProvider.Verify(d => d.CopyFile(sourceGamelistPath, targetGamelistPath, true), Times.Once);
        }
    }
}
