using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityFinderFixture
    {
        [TestCase(QualitySource.Television, 480)]
        [TestCase(QualitySource.Unknown, 480)]
        public void should_return_SDTV(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.SDTV);
        }

        [TestCase(QualitySource.Television, 720)]
        [TestCase(QualitySource.Unknown, 720)]
        public void should_return_HDTV_720p(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.HDTV720p);
        }

        [TestCase(QualitySource.Television, 1080)]
        [TestCase(QualitySource.Unknown, 1080)]
        public void should_return_HDTV_1080p(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.HDTV1080p);
        }

        [TestCase(QualitySource.Bluray, 720)]
        [TestCase(QualitySource.DVD, 720)]
        public void should_return_Bluray720p(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.Bluray720p);
        }

        [TestCase(QualitySource.Unknown, 0)]
        public void should_return_Unknown_without_throwing_when_multiple_qualities_share_source_and_resolution(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.Unknown);
        }

        [TestCase("!", "VerifiedGoodDump")]
        [TestCase("[!]", "VerifiedGoodDump")]
        [TestCase("b", "UnverifiedRaw")]
        [TestCase("[b1]", "UnverifiedRaw")]
        [TestCase("Beta", "BetaProto")]
        [TestCase("(Proto)", "BetaProto")]
        [TestCase("[T+Eng]", "TranslationPatch")]
        [TestCase("HorribleSubs", "Unknown")]
        [TestCase("House", "Unknown")]
        public void should_find_dump_tier_by_tag(string tag, string expectedQualityName)
        {
            QualityFinder.FindByDumpTag(tag).Name.Should().Be(expectedQualityName);
        }
    }
}
