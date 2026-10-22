using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Game.Model;
using NzbDrone.Core.MetadataSource.Igdb;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.Igdb
{
    [TestFixture]
    public class IgdbMetadataProxyFixture : CoreTest<IgdbMetadataProxy>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IIgdbTokenProvider>()
                .Setup(s => s.GetAccessToken())
                .Returns("fake_token");

            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.IgdbClientId)
                .Returns("fake_client_id");
        }

        [Test]
        public async Task SearchPlatformsAsync_should_return_platforms_on_success()
        {
            var jsonResponse = @"[
                { ""id"": 7, ""name"": ""PlayStation"", ""slug"": ""ps1"", ""generation"": 5, ""summary"": ""Sony PlayStation"" },
                { ""id"": 8, ""name"": ""PlayStation 2"", ""slug"": ""ps2"", ""generation"": 6, ""summary"": ""Sony PlayStation 2"" }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.ExecuteAsync(It.IsAny<HttpRequest>(), default))
                .ReturnsAsync(new HttpResponse(new HttpRequest("http://any"), new HttpHeader(), jsonResponse, HttpStatusCode.OK));

            var result = await Subject.SearchPlatformsAsync("PlayStation");

            result.Should().HaveCount(2);
            result.First().Name.Should().Be("PlayStation");
            result.First().Id.Should().Be(7);
            result.First().Generation.Should().Be(5);
        }

        [Test]
        public async Task SearchGamesAsync_should_return_games_on_success()
        {
            var jsonResponse = @"[
                {
                    ""id"": 100,
                    ""name"": ""Zelda"",
                    ""summary"": ""A good game"",
                    ""cover"": { ""url"": ""//images.igdb.com/cover.jpg"" },
                    ""first_release_date"": 1577836800,
                    ""platforms"": [ 4, 21 ]
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.ExecuteAsync(It.IsAny<HttpRequest>(), default))
                .ReturnsAsync(new HttpResponse(new HttpRequest("http://any"), new HttpHeader(), jsonResponse, HttpStatusCode.OK));

            var result = await Subject.SearchGamesAsync("Zelda", null);

            result.Should().HaveCount(1);
            var game = result.First();
            game.Id.Should().Be(100);
            game.Title.Should().Be("Zelda");
            game.CoverUrl.Should().Be("https://images.igdb.com/cover.jpg");
            game.ReleaseDate.HasValue.Should().BeTrue();
            game.PlatformId.Should().Be(4);
        }

        [Test]
        public async Task GetGameDetailsAsync_should_return_details_with_companies()
        {
            var jsonResponse = @"[
                {
                    ""id"": 100,
                    ""name"": ""Zelda"",
                    ""summary"": ""A good game"",
                    ""cover"": { ""url"": ""//images.igdb.com/cover.jpg"" },
                    ""first_release_date"": 1577836800,
                    ""platforms"": [ 4 ],
                    ""involved_companies"": [
                        { ""developer"": true, ""publisher"": false, ""company"": { ""name"": ""Nintendo"" } },
                        { ""developer"": false, ""publisher"": true, ""company"": { ""name"": ""Nintendo of America"" } }
                    ]
                }
            ]";

            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.ExecuteAsync(It.IsAny<HttpRequest>(), default))
                .ReturnsAsync(new HttpResponse(new HttpRequest("http://any"), new HttpHeader(), jsonResponse, HttpStatusCode.OK));

            var result = await Subject.GetGameDetailsAsync(100);

            result.Should().NotBeNull();
            result.Id.Should().Be(100);
            result.Developer.Should().Be("Nintendo");
            result.Publisher.Should().Be("Nintendo of America");
        }
    }
}
