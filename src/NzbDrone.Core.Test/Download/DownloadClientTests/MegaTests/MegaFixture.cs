using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.Mega;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.MegaTests
{
    [TestFixture]
    public class MegaFixture : CoreTest<Mega>
    {
        [TestCase("https://mega.nz/file/abc123#key456", true)]
        [TestCase("https://mega.nz/folder/abc123#key456", true)]
        [TestCase("https://mega.co.nz/#!abc123!key456", true)]
        [TestCase("https://archive.org/download/test/rom.zip", false)]
        [TestCase("https://example.com/file.zip", false)]
        [TestCase("", false)]
        public void should_identify_mega_urls(string url, bool expected)
        {
            Mega.IsMegaUrl(url).Should().Be(expected);
        }

        [Test]
        public void should_reject_non_mega_urls_so_download_service_can_fall_back()
        {
            var remoteEpisode = new RemoteEpisode
            {
                Release = new ReleaseInfo
                {
                    Title = "Test Game",
                    DownloadUrl = "https://archive.org/download/test/rom.zip"
                }
            };

            Assert.ThrowsAsync<DownloadClientException>(async () =>
                await Subject.Download(remoteEpisode, null));
        }

        [Test]
        public void should_validate_anonymous_and_paired_credentials()
        {
            new MegaSettings().Validate().IsValid.Should().BeTrue("anonymous mode has no required credentials");

            new MegaSettings { Email = "user@example.com" }
                .Validate().IsValid.Should().BeFalse("password is required when email is set");

            new MegaSettings { Password = "secret" }
                .Validate().IsValid.Should().BeFalse("email is required when password is set");

            new MegaSettings { Email = "user@example.com", Password = "secret" }
                .Validate().IsValid.Should().BeTrue();
        }
    }
}
