using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource.Game
{
    [TestFixture]
    public class GameMediaAssetServiceFixture : CoreTest<GameMediaAssetService>
    {
        [Test]
        public void should_cascade_scrapers_by_priority_to_complete_missing_assets_without_overwriting()
        {
            var tempPlatformDir = Path.Combine(Path.GetTempPath(), "Controllarr_AssetCascade_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempPlatformDir);

            try
            {
                // Pre-create box2d from a "higher-priority" source so we can verify it is NOT overwritten
                var box2dDir = Path.Combine(tempPlatformDir, "media", "box2d");
                Directory.CreateDirectory(box2dDir);
                var existingBox2dPath = Path.Combine(box2dDir, "Super Mario World (USA).png");
                var existingBytes = Encoding.UTF8.GetBytes("EXISTING_PRIORITY_1_BOX2D_ASSET_DATA_WITH_AT_LEAST_64_BYTES_PADDING_1234567890");
                File.WriteAllBytes(existingBox2dPath, existingBytes);

                Mocker.GetMock<NzbDrone.Core.Configuration.IConfigService>()
                    .SetupGet(c => c.EnableLaunchBox)
                    .Returns(true);

                var snesEntry = new PlatformCatalogEntry
                {
                    TvdbId = 1002,
                    Slug = "snes",
                    Name = "Super Nintendo Entertainment System",
                    LibretroSystemName = "Nintendo - Super Nintendo Entertainment System"
                };

                Mocker.GetMock<IGameCatalogService>()
                    .Setup(s => s.TryGetPlatformEntry(1002, out snesEntry))
                    .Returns(true);

                var fallbackScreenshotBytes = Encoding.UTF8.GetBytes("NEW_PRIORITY_4_SCREENSHOT_ASSET_DATA_WITH_AT_LEAST_64_BYTES_PADDING_1234567890");

                Mocker.GetMock<IHttpClient>()
                    .Setup(h => h.Execute(It.IsAny<HttpRequest>()))
                    .Returns((HttpRequest req) =>
                    {
                        return new HttpResponse(req, new HttpHeader(), fallbackScreenshotBytes);
                    });

                var platform = new Series
                {
                    Id = 1,
                    TvdbId = 1002,
                    Title = "Super Nintendo Entertainment System",
                    TitleSlug = "snes",
                    Path = tempPlatformDir
                };

                var romFile = new EpisodeFile
                {
                    Id = 10,
                    RelativePath = Path.Combine("USA", "Super Mario World (USA).sfc")
                };

                var game = new Episode
                {
                    Id = 100,
                    Title = "Super Mario World"
                };

                Subject.ScrapeMediaAssetsForImportedRom(platform, romFile, game);

                // Existing box2d must remain untouched ("complete the data — not replace it")
                File.ReadAllBytes(existingBox2dPath).Should().Equal(existingBytes);

                // Missing screenshot must be completed by the fallback scraper
                var screenshotPath = Path.Combine(tempPlatformDir, "media", "screenshots", "Super Mario World (USA).png");
                File.Exists(screenshotPath).Should().BeTrue();
                File.ReadAllBytes(screenshotPath).Should().Equal(fallbackScreenshotBytes);
            }
            finally
            {
                if (Directory.Exists(tempPlatformDir))
                {
                    Directory.Delete(tempPlatformDir, true);
                }
            }
        }

        [Test]
        public void should_update_gamelist_xml_on_platform_scan_rename_and_rom_delete()
        {
            var platform = new Series
            {
                Id = 7,
                Title = "Super Nintendo",
                Path = @"C:\Roms\SNES"
            };

            Mocker.GetMock<IConfigService>()
                  .Setup(c => c.MetadataGenerateGamelistXml)
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(platform.Path))
                  .Returns(true);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(m => m.GetFilesBySeries(platform.Id))
                  .Returns(new List<EpisodeFile>
                  {
                      new EpisodeFile { Id = 1, SeriesId = platform.Id, RelativePath = "USA/Chrono Trigger (USA).zip" }
                  });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(e => e.GetEpisodeBySeries(platform.Id))
                  .Returns(new List<Episode>
                  {
                      new Episode { Id = 10, EpisodeFileId = 1, Title = "Chrono Trigger" }
                  });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(platform.Id))
                  .Returns(platform);

            Subject.Handle(new PlatformScannedEvent(platform, new List<string>()));
            Subject.Handle(new PlatformRenamedEvent(platform, new List<RenamedRomFile>()));
            Subject.Handle(new RomFileDeletedEvent(new EpisodeFile { Id = 2, SeriesId = platform.Id }, DeleteMediaFileReason.Manual));

            Mocker.GetMock<IDiskProvider>().Verify(
                d => d.WriteAllText(
                    Path.Combine(platform.Path, "gamelist.xml"),
                    It.Is<string>(xml => xml.Contains("Chrono Trigger") && xml.Contains("./USA/Chrono Trigger (USA).zip"))),
                Times.Exactly(3));
        }
    }
}
