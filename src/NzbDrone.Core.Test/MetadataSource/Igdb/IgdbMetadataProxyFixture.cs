using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Igdb;
using NzbDrone.Core.Languages;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.Igdb
{
    [TestFixture]
    public class IgdbMetadataProxyFixture : TestBase<IgdbMetadataProxy>
    {
        private Mock<IIgdbTokenManager> _tokenManagerMock;
        private Mock<IConfigService> _configServiceMock;

        [SetUp]
        public void Setup()
        {
            _tokenManagerMock = Mocker.GetMock<IIgdbTokenManager>();
            _configServiceMock = Mocker.GetMock<IConfigService>();

            _tokenManagerMock.Setup(t => t.GetToken()).Returns("mock_token");
            _configServiceMock.Setup(c => c.IgdbClientId).Returns("mock_client_id");
        }

        [Test]
        public void GetGameInfo_ShouldThrowException_WhenProviderIdIsInvalid()
        {
            Assert.Throws<ArgumentException>(() => Subject.GetGameInfo("invalid:123", new Language()));
            Assert.Throws<ArgumentException>(() => Subject.GetGameInfo("igdb:abc", new Language()));
        }

        [Test]
        public void GetPlatforms_ShouldDeserializeCorrectly()
        {
            var responseJson = @"[{""id"": 1, ""name"": ""NES"", ""slug"": ""nes"", ""summary"": ""8-bit"", ""generation"": 3}]";
            var mockResponse = new HttpResponse<List<IgdbMetadataProxy.IgdbPlatform>>(new HttpResponse(new HttpRequestBuilder("https://api.igdb.com/v4/platforms").Build(), new HttpHeader(), responseJson));

            var httpClientMock = Mocker.GetMock<IHttpClient>();
            httpClientMock.Setup(c => c.Post<List<IgdbMetadataProxy.IgdbPlatform>>(It.IsAny<HttpRequest>())).Returns(mockResponse);

            var platforms = Subject.GetPlatforms();

            platforms.Should().NotBeNull();
            platforms.Should().HaveCount(1);
            platforms.First().IgdbId.Should().Be(1);
            platforms.First().Name.Should().Be("NES");
            platforms.First().Slug.Should().Be("nes");
            platforms.First().Overview.Should().Be("8-bit");
        }

        [Test]
        public void GetGameInfo_ShouldDeserializeCorrectly()
        {
            var responseJson = @"[{""id"": 123, ""name"": ""Super Mario"", ""summary"": ""Great game"", ""cover"": {""url"": ""//image.url""}, ""release_dates"": [{""id"": 1, ""region"": 2}]}]";
            var mockResponse = new HttpResponse<List<IgdbMetadataProxy.IgdbGame>>(new HttpResponse(new HttpRequestBuilder("https://api.igdb.com/v4/games").Build(), new HttpHeader(), responseJson));

            var httpClientMock = Mocker.GetMock<IHttpClient>();
            httpClientMock.Setup(c => c.Post<List<IgdbMetadataProxy.IgdbGame>>(It.IsAny<HttpRequest>())).Returns(mockResponse);

            var gameInfo = Subject.GetGameInfo("igdb:123", new Language());

            gameInfo.Should().NotBeNull();
            gameInfo.Item1.IgdbId.Should().Be(123);
            gameInfo.Item1.Title.Should().Be("Super Mario");
            gameInfo.Item1.Summary.Should().Be("Great game");
            gameInfo.Item1.CoverArtworkUrl.Should().Be("//image.url");
        }
    }
}
