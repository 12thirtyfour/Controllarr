using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Igdb;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.Igdb
{
    [TestFixture]
    public class IgdbTokenProviderFixture : CoreTest<IgdbTokenProvider>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.IgdbClientId)
                .Returns("fake_client_id");
            Mocker.GetMock<IConfigService>()
                .SetupGet(s => s.IgdbClientSecret)
                .Returns("fake_client_secret");
        }

        [Test]
        public void should_return_token_and_cache_it()
        {
            var jsonResponse = @"{ ""access_token"": ""my_access_token"", ""expires_in"": 3600, ""token_type"": ""bearer"" }";

            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                .Returns(new HttpResponse(new HttpRequest("http://any"), new HttpHeader(), jsonResponse, HttpStatusCode.OK));

            var token1 = Subject.GetAccessToken();
            var token2 = Subject.GetAccessToken();

            token1.Should().Be("my_access_token");
            token2.Should().Be("my_access_token");

            Mocker.GetMock<IHttpClient>().Verify(s => s.Execute(It.IsAny<HttpRequest>()), Times.Once);
        }
    }
}
