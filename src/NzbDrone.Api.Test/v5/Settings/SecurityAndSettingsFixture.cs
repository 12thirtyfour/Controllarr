using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Export;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Games;
using Sonarr.Api.V5.Settings;
using V3MediaCoverController = Sonarr.Api.V3.MediaCovers.MediaCoverController;
using V5MediaCoverController = Sonarr.Api.V5.MediaCovers.MediaCoverController;

namespace NzbDrone.Api.Test.v5.Settings
{
    [TestFixture]
    public class SecurityAndSettingsFixture : TestBase
    {
        [Test]
        public void should_map_auto_download_required_bios_and_console_region_in_v5_settings()
        {
            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.AutoDownloadRequiredBios).Returns(true);
            configService.Setup(c => c.ConsoleRegion).Returns("JP");

            var configFileProvider = new Mock<IConfigFileProvider>();
            configFileProvider.Setup(c => c.Theme).Returns("snes");

            var mediaResource = MediaManagementConfigResourceMapper.ToResource(configService.Object);
            var uiResource = UiSettingsResourceMapper.ToResource(configFileProvider.Object, configService.Object);

            mediaResource.AutoDownloadRequiredBios.Should().BeTrue();
            uiResource.ConsoleRegion.Should().Be("JP");
        }

        [TestCase("../secret.jpg")]
        [TestCase("..\\secret.jpg")]
        [TestCase("sub/folder.jpg")]
        [TestCase("..")]
        public void should_reject_path_traversal_in_v3_and_v5_mediacover_controllers(string filename)
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "controllarr_mc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var appFolder = new Mock<IAppFolderInfo>();
                appFolder.Setup(a => a.AppDataFolder).Returns(tempRoot);
                var diskProvider = new Mock<IDiskProvider>();

                var v3 = new V3MediaCoverController(appFolder.Object, diskProvider.Object);
                var v5 = new V5MediaCoverController(appFolder.Object, diskProvider.Object);

                v3.GetMediaCover(1, filename).Should().BeOfType<NotFoundResult>();
                v5.GetMediaCover(1, filename).Result.Should().BeOfType<NotFound>();
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Test]
        public void should_block_sdcard_export_when_source_path_is_outside_configured_libraries()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "controllarr_sdsec_" + Guid.NewGuid().ToString("N"));
            var libDir = Path.Combine(tempRoot, "library", "SNES");
            var outsideFile = Path.Combine(tempRoot, "config", "config.xml");
            var mountDir = Path.Combine(tempRoot, "sd");

            Directory.CreateDirectory(libDir);
            Directory.CreateDirectory(Path.GetDirectoryName(outsideFile));
            File.WriteAllText(outsideFile, "<Config/>");

            try
            {
                var exporter = new Mock<ISdCardExporterService>();
                var seriesService = new Mock<ISeriesService>();
                seriesService.Setup(s => s.GetAllSeries()).Returns(new List<Series>
                {
                    new Series { Id = 1, Title = "SNES", TitleSlug = "snes", Path = libDir }
                });

                var controller = new SdCardExportController(exporter.Object, seriesService.Object);

                var result = controller.Export(new SdCardExportRequestResource
                {
                    Profile = SdCardDeviceProfile.OnionOS,
                    TargetMountPath = mountDir,
                    Items = new List<SdCardExportItem>
                    {
                        new SdCardExportItem
                        {
                            SourceFilePath = outsideFile,
                            PlatformFolder = "SNES",
                            GameTitle = "Secret"
                        }
                    }
                });

                result.Result.Should().BeOfType<BadRequestObjectResult>();
                exporter.Verify(
                    e => e.ExportToSdCard(
                        It.IsAny<SdCardDeviceProfile>(),
                        It.IsAny<string>(),
                        It.IsAny<IEnumerable<SdCardExportItem>>(),
                        It.IsAny<bool>(),
                        It.IsAny<bool>()),
                    Times.Never);
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
