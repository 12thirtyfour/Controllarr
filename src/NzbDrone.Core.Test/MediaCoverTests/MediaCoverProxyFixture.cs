using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaCoverTests
{
    [TestFixture]
    public class MediaCoverProxyFixture : CoreTest<MediaCoverProxy>
    {
        private string _testUrl;
        private string _testHash;

        [SetUp]
        public void Setup()
        {
            _testUrl = "https://artworks.example.com/cover.jpg";
            _testHash = _testUrl.SHA256Hash();

            Subject.RegisterUrl(_testUrl);
        }

        [Test]
        public async Task should_set_suppress_http_error_on_request()
        {
            HttpRequest capturedRequest = null;

            Mocker.GetMock<IHttpClient>()
                  .Setup(h => h.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<HttpRequest, CancellationToken>((req, ct) => capturedRequest = req)
                  .ReturnsAsync(new HttpResponse(new HttpRequest(_testUrl), new HttpHeader(), new byte[] { 1, 2, 3 }, HttpStatusCode.OK));

            await Subject.GetImage(_testHash);

            capturedRequest.Should().NotBeNull();
            capturedRequest.SuppressHttpError.Should().BeTrue();
        }

        [Test]
        public async Task should_return_null_cleanly_when_http_status_is_not_found()
        {
            var req = new HttpRequest(_testUrl);
            var response = new HttpResponse(req, new HttpHeader(), new byte[0], HttpStatusCode.NotFound);

            Mocker.GetMock<IHttpClient>()
                  .Setup(h => h.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(response);

            var result = await Subject.GetImage(_testHash);

            result.Should().BeNullOrEmpty();
        }

        [Test]
        public async Task should_return_null_cleanly_when_response_data_is_empty()
        {
            var req = new HttpRequest(_testUrl);
            var response = new HttpResponse(req, new HttpHeader(), Array.Empty<byte>(), HttpStatusCode.OK);

            Mocker.GetMock<IHttpClient>()
                  .Setup(h => h.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(response);

            var result = await Subject.GetImage(_testHash);

            result.Should().BeNullOrEmpty();
        }

        [Test]
        public async Task should_handle_http_exception_and_return_null()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(h => h.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new HttpException(new HttpRequest(_testUrl), new HttpResponse(new HttpRequest(_testUrl), new HttpHeader(), new byte[0], HttpStatusCode.InternalServerError)));

            var result = await Subject.GetImage(_testHash);

            result.Should().BeNullOrEmpty();
        }

        [Test]
        public async Task should_return_image_bytes_on_successful_get()
        {
            var expectedBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            var req = new HttpRequest(_testUrl);
            var response = new HttpResponse(req, new HttpHeader(), expectedBytes, HttpStatusCode.OK);

            Mocker.GetMock<IHttpClient>()
                  .Setup(h => h.GetAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(response);

            var result = await Subject.GetImage(_testHash);

            result.Should().NotBeNull();
            result.Should().Equal(expectedBytes);
        }
    }
}
