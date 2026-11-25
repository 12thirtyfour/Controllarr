using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Dat;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Patching;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.Patching
{
    [TestFixture]
    public class RomHackAutomationServiceFixture : CoreTest<RomHackAutomationService>
    {
        [Test]
        public void should_search_community_sources_and_parse_results()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.StartsWith("https://romhackplaza.org/?s="))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<a href=\"https://romhackplaza.org/hacks/super-mario-world-return\">Super Mario World Return</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri == "https://romhackplaza.org/hacks/super-mario-world-return")))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<div>Base ROM: Super Mario World (USA)</div><div>CRC32: b19ed489</div><a href=\"https://romhackplaza.org/files/smw-return.bps\">Download BPS</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("archive.org/advancedsearch.php"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"response\":{\"docs\":[{\"identifier\":\"smw-hack-pack\",\"title\":\"SMW Hack Pack\"}]}}",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri == "https://archive.org/metadata/smw-hack-pack")))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"files\":[{\"name\":\"patches/Super Mario World Return.ips\",\"size\":\"4096\"},{\"name\":\"patches/Super Mario World Return.bps\",\"size\":\"2048\"}]}",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("api.github.com/search/repositories"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"items\":[{\"full_name\":\"dev/snes-homebrew\",\"html_url\":\"https://github.com/dev/snes-homebrew\"}]}",
                    HttpStatusCode.OK));

            var results = Subject.SearchCommunitySources("Super Mario World", "snes");

            results.Should().Contain(r =>
                r.SourceName == "Romhack Plaza" &&
                r.Title == "Super Mario World Return" &&
                r.PatchFormat == "BPS" &&
                r.HasDirectDownload &&
                r.DownloadUrl == "https://romhackplaza.org/files/smw-return.bps" &&
                r.BaseGameTitle == "Super Mario World (USA)" &&
                r.BaseRomCrc32 == "b19ed489");

            results.Should().Contain(r =>
                r.SourceName.Contains("Internet Archive") &&
                r.PatchFormat == "BPS" &&
                r.HasDirectDownload &&
                r.DownloadUrl == "https://archive.org/download/smw-hack-pack/patches/Super%20Mario%20World%20Return.bps");

            results.Should().Contain(r => r.SourceName == "GitHub Homebrew" && r.IsHomebrew);
        }

        [Test]
        public void should_reject_html_webpage_when_acquiring_patch()
        {
            var series = new Series { Id = 1, Title = "Game Boy Advance", TitleSlug = "gba" };
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(1))
                .Returns(series);

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.DownloadFile(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    File.WriteAllText(path, "<!DOCTYPE html><html><body>Not a patch</body></html>");
                });

            var result = Subject.AcquireAndCreate(new RomHackAcquireRequest
            {
                SeriesId = 1,
                Title = "Pokemon Orange Islands",
                DownloadUrl = "https://example.com/page.html",
                SearchMissingBaseRom = false
            });

            result.Success.Should().BeFalse();
            result.BaseRomMissing.Should().BeFalse();
            result.Message.Should().Contain("HTML webpage");
            Mocker.GetMock<IRomPatchingService>()
                .Verify(p => p.ApplyPatchToLibrary(It.IsAny<RomPatchRequestModel>()), Times.Never);
        }

        [Test]
        public void should_flag_missing_base_rom_when_patch_cannot_find_retail_base()
        {
            var series = new Series { Id = 1, Title = "Super Nintendo", TitleSlug = "snes" };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(1))
                .Returns(series);

            Mocker.GetMock<IEpisodeService>()
                .Setup(e => e.GetEpisodeBySeries(1))
                .Returns(new List<Episode>
                {
                    new Episode { Id = 10, SeasonNumber = 1, Title = "Super Mario World (USA)", EpisodeFileId = 0 }
                });

            Mocker.GetMock<IRomPatchingService>()
                .Setup(p => p.TryApplyPatchFileForSeries(It.IsAny<string>(), series))
                .Returns(new RomPatchResultModel
                {
                    Success = false,
                    BaseRomMissing = true,
                    Message = "Base retail ROM not found in library."
                });

            var result = Subject.AcquireAndCreate(new RomHackAcquireRequest
            {
                SeriesId = 1,
                Title = "SMW Translation",
                BaseGameTitle = "Super Mario World",
                DownloadUrl = "https://example.com/smw.bps"
            });

            result.Success.Should().BeFalse();
            result.BaseRomMissing.Should().BeTrue();
            result.BaseRomSearchQueued.Should().BeTrue();
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(q => q.Push(It.Is<GameSearchCommand>(c => c.GameIds.Contains(10)), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void should_lookup_missing_base_rom_by_dat_crc32_and_queue_retail_search()
        {
            var series = new Series { Id = 2, Title = "Game Boy Advance", TitleSlug = "gba" };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(2))
                .Returns(series);

            Mocker.GetMock<IDatCacheService>()
                .Setup(d => d.FindByCrc32("dd88761c"))
                .Returns(new DatEntry { GameName = "Pokemon - FireRed Version (USA, Europe)" });

            Mocker.GetMock<IEpisodeService>()
                .Setup(e => e.GetEpisodeBySeries(2))
                .Returns(new List<Episode>
                {
                    new Episode { Id = 42, SeasonNumber = 1, Title = "Pokemon - FireRed Version (USA, Europe)", EpisodeFileId = 0 }
                });

            Mocker.GetMock<IRomPatchingService>()
                .Setup(p => p.ApplyPatchToLibrary(It.IsAny<RomPatchRequestModel>()))
                .Returns(new RomPatchResultModel
                {
                    Success = false,
                    BaseRomMissing = true,
                    ExpectedBaseRomCrc32 = "dd88761c",
                    Format = "BPS",
                    Message = "No matching retail base ROM with CRC32 dd88761c was found."
                });

            var result = Subject.AcquireAndCreate(new RomHackAcquireRequest
            {
                SeriesId = 2,
                Title = "Pokemon Orange Islands Beta 5.7",
                DownloadUrl = "https://example.com/orange_islands.bps",
                SearchMissingBaseRom = true
            });

            result.Success.Should().BeFalse();
            result.BaseRomMissing.Should().BeTrue();
            result.BaseRomSearchQueued.Should().BeTrue();
            Mocker.GetMock<IManageCommandQueue>()
                .Verify(q => q.Push(It.Is<GameSearchCommand>(c => c.GameIds.Contains(42)), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void should_acquire_patch_and_import_for_region_rom_hack_episode()
        {
            var series = new Series { Id = 3, Title = "Game Boy Advance", TitleSlug = "gba", Path = Path.GetTempPath() };
            var episode = new Episode
            {
                Id = 301,
                SeriesId = 3,
                SeasonNumber = GameCatalogProxy.RegionRomHacks,
                Title = "Pokemon Orange Islands (Hack)"
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(3))
                .Returns(series);

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.StartsWith("https://romhackplaza.org/?s="))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<a href=\"https://romhackplaza.org/hacks/pokemon-orange-islands\">Pokemon Orange Islands</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri == "https://romhackplaza.org/hacks/pokemon-orange-islands")))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<div>Platform: Game Boy Advance</div><div>Base ROM: Pokemon - FireRed Version (USA)</div><a href=\"https://romhackplaza.org/files/orange_islands.ips\">Download IPS</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.DownloadFile("https://romhackplaza.org/files/orange_islands.ips", It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    File.WriteAllBytes(path, Encoding.ASCII.GetBytes("PATCHEOF"));
                });

            Mocker.GetMock<IRomPatchingService>()
                .Setup(p => p.ApplyPatchToLibrary(It.Is<RomPatchRequestModel>(req =>
                    req.SeriesId == 3 &&
                    req.TargetEpisodeId == 301 &&
                    req.BaseGameTitleHint == "Pokemon - FireRed Version (USA)")))
                .Returns(new RomPatchResultModel
                {
                    Success = true,
                    Format = "IPS",
                    BaseRomName = "Pokemon - FireRed Version (USA).gba",
                    OutputRomPath = "/roms/Nintendo/GBA/ROM Hacks/Pokemon Orange Islands (Hack).gba",
                    OutputCrc32 = "12345678",
                    EpisodeId = 301,
                    EpisodeFileId = 777
                });

            var result = Subject.TryAcquirePatchForGame(series, episode);

            result.Success.Should().BeTrue();
            result.PatchFormat.Should().Be("IPS");
            result.PatchSource.Should().Be("Romhack Plaza");
            result.EpisodeId.Should().Be(301);
            result.EpisodeFileId.Should().Be(777);
            result.BaseRomName.Should().Be("Pokemon - FireRed Version (USA).gba");
        }

        [Test]
        public void should_acquire_patch_and_import_for_region_translations_episode()
        {
            var series = new Series { Id = 4, Title = "Super Nintendo Entertainment System", TitleSlug = "snes", Path = Path.GetTempPath() };
            var episode = new Episode
            {
                Id = 401,
                SeriesId = 4,
                SeasonNumber = GameCatalogProxy.RegionTranslations,
                Title = "Bahamut Lagoon [T-En by DeJap]"
            };

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(4))
                .Returns(series);

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.StartsWith("https://romhackplaza.org/?s="))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<a href=\"https://romhackplaza.org/translations/bahamut-lagoon\">Bahamut Lagoon</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri == "https://romhackplaza.org/translations/bahamut-lagoon")))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<div>Platform: Super Nintendo Entertainment System</div><div>Base ROM: Bahamut Lagoon (Japan)</div><a href=\"https://romhackplaza.org/files/bahamut_en.ips\">Download IPS</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.DownloadFile("https://romhackplaza.org/files/bahamut_en.ips", It.IsAny<string>()))
                .Callback<string, string>((_, path) =>
                {
                    File.WriteAllBytes(path, Encoding.ASCII.GetBytes("PATCHEOF"));
                });

            Mocker.GetMock<IRomPatchingService>()
                .Setup(p => p.ApplyPatchToLibrary(It.Is<RomPatchRequestModel>(req =>
                    req.SeriesId == 4 &&
                    req.TargetEpisodeId == 401 &&
                    req.BaseGameTitleHint == "Bahamut Lagoon (Japan)")))
                .Returns(new RomPatchResultModel
                {
                    Success = true,
                    Format = "IPS",
                    BaseRomName = "Bahamut Lagoon (Japan).sfc",
                    OutputRomPath = "/roms/Nintendo/SNES/Translations/Bahamut Lagoon [T-En by DeJap].sfc",
                    OutputCrc32 = "87654321",
                    EpisodeId = 401,
                    EpisodeFileId = 888
                });

            var result = Subject.TryAcquirePatchForGame(series, episode);

            result.Success.Should().BeTrue();
            result.EpisodeId.Should().Be(401);
            result.EpisodeFileId.Should().Be(888);
        }

        [Test]
        public void should_ignore_romhack_plaza_nav_links_and_archive_book_scans()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.StartsWith("https://romhackplaza.org/?s="))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<a href=\"https://romhackplaza.org/news/secret-of-mana-reborn-historic-update\">Secret of Mana Reborn</a>" +
                    "<a href=\"https://romhackplaza.org/database\">Database</a>" +
                    "<a href=\"https://romhackplaza.org/queue\">Queue</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("archive.org/advancedsearch.php"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"response\":{\"docs\":[{\"identifier\":\"isbn_9780764112300\",\"title\":\"Barron's Ace\"},{\"identifier\":\"random-book\",\"title\":\"Unrelated Book\"}]}}",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri == "https://archive.org/metadata/random-book")))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"files\":[{\"name\":\"random-book_jp2.zip\",\"size\":\"12345\"},{\"name\":\"other-stuff.zip\",\"size\":\"999\"}]}",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("api.github.com/search/repositories"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"items\":[]}",
                    HttpStatusCode.OK));

            var results = Subject.SearchCommunitySources("Ace", "atari2600", includeHomebrew: false);

            results.Should().BeEmpty();
            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("/news/") || r.Url.FullUri.EndsWith("/database") || r.Url.FullUri.EndsWith("/queue"))), Times.Never);
            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("isbn_9780764112300"))), Times.Never);
        }

        [Test]
        public void should_not_download_html_when_detail_page_has_no_direct_patch_link()
        {
            var series = new Series { Id = 5, Title = "Atari 2600", TitleSlug = "atari2600" };
            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(5))
                .Returns(series);

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri == "https://romhackplaza.org/hacks/acid-fish")))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<html><body><div>Acid Fish page with no download link</div></body></html>",
                    HttpStatusCode.OK));

            var result = Subject.AcquireAndCreate(new RomHackAcquireRequest
            {
                SeriesId = 5,
                Title = "Acid Fish",
                DownloadUrl = "https://romhackplaza.org/hacks/acid-fish",
                PageUrl = "https://romhackplaza.org/hacks/acid-fish"
            });

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("No direct binary patch download link");
            Mocker.GetMock<IHttpClient>()
                .Verify(c => c.DownloadFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
