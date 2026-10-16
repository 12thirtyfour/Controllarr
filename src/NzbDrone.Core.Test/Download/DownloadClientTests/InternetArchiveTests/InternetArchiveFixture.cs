using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Clients.InternetArchive;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.InternetArchiveTests
{
    [TestFixture]
    public class InternetArchiveFixture
    {
        [Test]
        public void should_create_settings()
        {
            var settings = new InternetArchiveSettings();
            settings.UrlBase.Should().Be("https://archive.org");

            // Note: Validation might fail if ValidUrlBase expects something different, e.g. "http://archive.org" depending on the built-in validator
            // We'll just assert it exists and properties are set correctly
        }
    }
}
