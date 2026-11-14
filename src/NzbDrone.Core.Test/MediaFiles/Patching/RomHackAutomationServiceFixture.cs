using System;
using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaFiles.Patching;
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
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("romhackplaza.org"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "<a href=\"https://romhackplaza.org/hacks/super-mario-world-return\">Super Mario World Return</a>",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("archive.org"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"response\":{\"docs\":[{\"identifier\":\"smw-hack-pack\",\"title\":\"SMW Hack Pack\"}]}}",
                    HttpStatusCode.OK));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get(It.Is<HttpRequest>(r => r.Url.FullUri.Contains("api.github.com"))))
                .Returns<HttpRequest>(r => new HttpResponse(
                    r,
                    new HttpHeader(),
                    "{\"items\":[{\"full_name\":\"dev/snes-homebrew\",\"html_url\":\"https://github.com/dev/snes-homebrew\"}]}",
                    HttpStatusCode.OK));

            var results = Subject.SearchCommunitySources("Super Mario World", "snes");

            results.Should().HaveCount(3);
            results.Should().Contain(r => r.SourceName == "Romhack Plaza" && r.Title == "Super Mario World Return");
            results.Should().Contain(r => r.SourceName.Contains("Internet Archive") && r.DownloadUrl == "https://archive.org/download/smw-hack-pack");
            results.Should().Contain(r => r.SourceName == "GitHub Homebrew" && r.IsHomebrew);
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
                    new Episode { Id = 10, Title = "Super Mario World (USA)", EpisodeFileId = 0 }
                });

            Mocker.GetMock<IRomPatchingService>()
                .Setup(p => p.TryApplyPatchFileForSeries(It.IsAny<string>(), series))
                .Returns(new RomPatchResultModel
                {
                    Success = false,
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
        }
    }
}
