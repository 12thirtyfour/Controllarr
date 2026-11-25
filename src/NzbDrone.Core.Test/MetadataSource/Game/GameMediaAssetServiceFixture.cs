using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
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

        [Test]
        public void should_skip_gamelist_xml_rewrite_on_missing_from_disk_or_unlinked_rom_deletions()
        {
            var platform = new Series
            {
                Id = 8,
                Title = "Nintendo Entertainment System",
                Path = @"C:\Roms\NES"
            };

            Mocker.GetMock<IConfigService>()
                  .Setup(c => c.MetadataGenerateGamelistXml)
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FolderExists(platform.Path))
                  .Returns(true);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(platform.Id))
                  .Returns(platform);

            Subject.Handle(new RomFileDeletedEvent(new EpisodeFile { Id = 1, SeriesId = platform.Id }, DeleteMediaFileReason.MissingFromDisk));
            Subject.Handle(new RomFileDeletedEvent(new EpisodeFile { Id = 2, SeriesId = platform.Id }, DeleteMediaFileReason.NoLinkedEpisodes));

            Mocker.GetMock<IDiskProvider>().Verify(
                d => d.WriteAllText(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never());
        }

        [Test]
        public void should_sync_metadata_scrapers_json_when_saving_metadata_sources_and_asset_settings()
        {
            var appDataDir = Path.Combine(Path.GetTempPath(), "Controllarr_MetaSync_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(appDataDir);

            try
            {
                var enableIgdb = false;
                var igdbClientId = string.Empty;
                var igdbClientSecret = string.Empty;
                var generateGamelistXml = true;
                var boxArt3d = true;

                var configMock = Mocker.GetMock<IConfigService>();
                configMock.SetupProperty(c => c.EnableLaunchBox, true);
                configMock.Setup(c => c.EnableIgdb).Returns(() => enableIgdb);
                configMock.SetupSet(c => c.EnableIgdb = It.IsAny<bool>()).Callback<bool>(v => enableIgdb = v);
                configMock.Setup(c => c.IgdbClientId).Returns(() => igdbClientId);
                configMock.SetupSet(c => c.IgdbClientId = It.IsAny<string>()).Callback<string>(v => igdbClientId = v);
                configMock.Setup(c => c.IgdbClientSecret).Returns(() => igdbClientSecret);
                configMock.SetupSet(c => c.IgdbClientSecret = It.IsAny<string>()).Callback<string>(v => igdbClientSecret = v);
                configMock.Setup(c => c.MetadataGenerateGamelistXml).Returns(() => generateGamelistXml);
                configMock.SetupSet(c => c.MetadataGenerateGamelistXml = It.IsAny<bool>()).Callback<bool>(v => generateGamelistXml = v);
                configMock.Setup(c => c.MetadataBoxArt3d).Returns(() => boxArt3d);
                configMock.SetupSet(c => c.MetadataBoxArt3d = It.IsAny<bool>()).Callback<bool>(v => boxArt3d = v);

                Mocker.GetMock<IAppFolderInfo>()
                      .Setup(a => a.AppDataFolder)
                      .Returns(appDataDir);

                Subject.SaveMetadataSources(new MetadataSourceConfigModel
                {
                    EnableLaunchBox = true,
                    EnableIgdb = true,
                    IgdbClientId = "my-client-id",
                    IgdbClientSecret = "my-client-secret"
                });

                Subject.SaveMediaAssetSettings(new GameMediaAssetSettingsModel
                {
                    GenerateGamelistXml = false,
                    BoxArt3d = false,
                    BoxArt2d = true,
                    Screenshots = true,
                    TitleScreens = true,
                    ClearLogos = true,
                    ManualsPdf = true,
                    PublishingMetadata = true
                });

                var unified = Subject.GetUnifiedSettings();
                unified.GenerateGamelistXml.Should().BeFalse();
                var igdb = unified.Scrapers.Find(s => s.Id == "igdb");
                igdb.Should().NotBeNull();
                igdb.Enabled.Should().BeTrue();
                igdb.ClientId.Should().Be("my-client-id");
                igdb.ClientSecret.Should().Be("my-client-secret");
                igdb.BoxArt3d.Should().BeFalse();
            }
            finally
            {
                if (Directory.Exists(appDataDir))
                {
                    Directory.Delete(appDataDir, true);
                }
            }
        }

        [Test]
        public void should_cache_igdb_oauth_token_across_consecutive_scrapes()
        {
            var tempPlatformDir = Path.Combine(Path.GetTempPath(), "Controllarr_IgdbToken_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempPlatformDir);

            try
            {
                var configMock = Mocker.GetMock<IConfigService>();
                configMock.SetupGet(c => c.EnableIgdb).Returns(true);
                configMock.SetupGet(c => c.EnableLaunchBox).Returns(false);
                configMock.SetupGet(c => c.IgdbClientId).Returns("twitch-id");
                configMock.SetupGet(c => c.IgdbClientSecret).Returns("twitch-secret");
                configMock.SetupGet(c => c.MetadataBoxArt2d).Returns(true);

                var tokenCalls = 0;
                var igdbCalls = 0;

                Mocker.GetMock<IHttpClient>()
                    .Setup(h => h.Execute(It.IsAny<HttpRequest>()))
                    .Returns((HttpRequest req) =>
                    {
                        var url = req.Url.ToString();
                        if (url.Contains("id.twitch.tv/oauth2/token", StringComparison.OrdinalIgnoreCase))
                        {
                            tokenCalls++;
                            return new HttpResponse(req, new HttpHeader(), "{\"access_token\":\"cached-igdb-token\",\"expires_in\":3600}");
                        }

                        if (url.Contains("api.igdb.com/v4/games", StringComparison.OrdinalIgnoreCase))
                        {
                            igdbCalls++;
                            return new HttpResponse(req, new HttpHeader(), "[{\"cover\":{\"image_id\":\"co1234\"}}]");
                        }

                        var fakePng = Encoding.UTF8.GetBytes("FAKE_PNG_ASSET_PAYLOAD_AT_LEAST_64_BYTES_LONG_0123456789012345678901");
                        return new HttpResponse(req, new HttpHeader(), fakePng);
                    });

                var platform = new Series
                {
                    Id = 2,
                    TvdbId = 1002,
                    Title = "Super Nintendo",
                    TitleSlug = "snes",
                    Path = tempPlatformDir
                };

                Subject.ScrapeMediaAssetsForImportedRom(
                    platform,
                    new EpisodeFile { Id = 1, RelativePath = Path.Combine("USA", "Game One (USA).sfc") },
                    new Episode { Id = 1, Title = "Game One" });

                Subject.ScrapeMediaAssetsForImportedRom(
                    platform,
                    new EpisodeFile { Id = 2, RelativePath = Path.Combine("USA", "Game Two (USA).sfc") },
                    new Episode { Id = 2, Title = "Game Two" });

                tokenCalls.Should().Be(1);
                igdbCalls.Should().Be(2);
            }
            finally
            {
                if (Directory.Exists(tempPlatformDir))
                {
                    Directory.Delete(tempPlatformDir, true);
                }
            }
        }

        [TestCase("MAME 2003-Plus Reference Set", "mame2003.dat")]
        [TestCase("Commodore - Amiga", "amiga.dat")]
        [TestCase("Microsoft - MSX2", "msx2.dat")]
        [TestCase("IBM PC / MS-DOS", "msdos.dat")]
        public void should_reject_manual_import_for_arcade_and_computer_dats(string catalogName, string fileName)
        {
            const string datXml = @"<?xml version=""1.0""?>
<datafile>
  <header>
    <name>{0}</name>
    <description>{0}</description>
  </header>
  <game name=""Sample"">
    <rom name=""sample.bin"" size=""16"" crc=""12345678"" />
  </game>
</datafile>";

            var formatted = string.Format(datXml, catalogName);
            var datFile = new NzbDrone.Core.Dat.DatFile { Name = catalogName, Description = catalogName };
            var entries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry { GameName = "Sample", RomName = "sample.bin", Crc32 = "12345678" }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatParserService>()
                .Setup(p => p.Parse(It.IsAny<Stream>()))
                .Returns((datFile, entries));

            var result = Subject.ImportDatOrSmdb(new DatImportRequestModel
            {
                FileName = fileName,
                Content = formatted
            });

            result.EntryCount.Should().Be(0);
            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Verify(c => c.Insert(It.IsAny<NzbDrone.Core.Dat.DatFile>(), It.IsAny<List<NzbDrone.Core.Dat.DatEntry>>()), Times.Never());
        }

        [Test]
        public void should_allow_console_dat_imports_for_cd32_and_gx4000_while_still_filtering_computers()
        {
            NzbDrone.Core.MetadataSource.Game.GameMediaAssetService.IsUnsupportedArcadeOrComputerPlatform("Commodore - CD32").Should().BeFalse();
            NzbDrone.Core.MetadataSource.Game.GameMediaAssetService.IsUnsupportedArcadeOrComputerPlatform("Commodore - Amiga CD32").Should().BeFalse();
            NzbDrone.Core.MetadataSource.Game.GameMediaAssetService.IsUnsupportedArcadeOrComputerPlatform("Amstrad - GX4000").Should().BeFalse();

            NzbDrone.Core.MetadataSource.Game.GameMediaAssetService.IsUnsupportedArcadeOrComputerPlatform("Commodore - 64").Should().BeTrue();
            NzbDrone.Core.MetadataSource.Game.GameMediaAssetService.IsUnsupportedArcadeOrComputerPlatform("Commodore - Amiga").Should().BeTrue();
            NzbDrone.Core.MetadataSource.Game.GameMediaAssetService.IsUnsupportedArcadeOrComputerPlatform("Amstrad - CPC").Should().BeTrue();
        }
    }
}
