using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Igdb;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.Igdb
{
    [TestFixture]
    public class IgdbTokenManagerFixture : TestBase<IgdbTokenManager>
    {
        private Mock<IHttpClient> _httpClientMock;
        private Mock<IConfigService> _configServiceMock;

        [SetUp]
        public void Setup()
        {
            _httpClientMock = Mocker.GetMock<IHttpClient>();
            _configServiceMock = Mocker.GetMock<IConfigService>();
        }

        [Test]
        public void GetToken_ShouldReturnNull_WhenCredentialsAreMissing()
        {
            _configServiceMock.Setup(c => c.IgdbClientId).Returns(string.Empty);
            _configServiceMock.Setup(c => c.IgdbClientSecret).Returns(string.Empty);

            var token = Subject.GetToken();

            token.Should().BeNull();
            _httpClientMock.Verify(c => c.Post(It.IsAny<HttpRequest>()), Times.Never());
        }
    }
}
