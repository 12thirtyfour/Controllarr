using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.MediaFiles.GameImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

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

        [Test]
        public void should_accept_when_file_matches_platform_exclusive_extension()
        {
            File.WriteAllBytes(_testFilePath, new byte[2048]);
            var pathWithExt = _testFilePath + ".3ds";
            File.Move(_testFilePath, pathWithExt);
            _testFilePath = pathWithExt;

            _localEpisode.Path = pathWithExt;
            _localEpisode.Series = new Series
            {
                Title = "Nintendo 3DS",
                TitleSlug = "3ds"
            };

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_file_does_not_match_platform_extension()
        {
            File.WriteAllBytes(_testFilePath, new byte[2048]);
            var pathWithExt = _testFilePath + ".iso";
            File.Move(_testFilePath, pathWithExt);
            _testFilePath = pathWithExt;

            _localEpisode.Path = pathWithExt;
            _localEpisode.Series = new Series
            {
                Title = "Nintendo 3DS",
                TitleSlug = "3ds"
            };

            var decision = Subject.IsSatisfiedBy(_localEpisode, null);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.PlatformFormatRejected);
        }
    }
}
