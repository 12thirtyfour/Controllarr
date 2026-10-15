using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Platforms
{
    [TestFixture]
    public class PlatformFormatSpecificationFixture : CoreTest<PlatformFormatSpecification>
    {
        private LocalEpisode _localEpisode;
        private string _testFilePath;

        [SetUp]
        public void Setup()
        {
            _testFilePath = Path.GetTempFileName();
            _localEpisode = new LocalEpisode
            {
                Path = _testFilePath
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }

        [Test]
        public void should_reject_when_file_is_under_1kb()
        {
            // Write a tiny amount of bytes (stub)
            File.WriteAllBytes(_testFilePath, new byte[500]);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_file_is_over_1kb_and_allowed()
        {
            // Write larger than 1kb
            File.WriteAllBytes(_testFilePath, new byte[2048]);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }
    }
}
