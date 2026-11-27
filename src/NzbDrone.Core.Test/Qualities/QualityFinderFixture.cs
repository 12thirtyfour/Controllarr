using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityFinderFixture
    {
        [TestCase(QualitySource.Prototype, 0, "BetaProto")]
        [TestCase(QualitySource.Patch, 0, "TranslationPatch")]
        [TestCase(QualitySource.Unverified, 0, "UnverifiedRaw")]
        [TestCase(QualitySource.Verified, 0, "VerifiedGoodDump")]
        [TestCase(QualitySource.NoIntro, 0, "NoIntroVerified")]
        [TestCase(QualitySource.Redump, 0, "VerifiedRedump")]
        [TestCase(QualitySource.Unknown, 0, "Unknown")]
        public void should_find_quality_by_source(QualitySource source, int resolution, string expectedQualityName)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Name.Should().Be(expectedQualityName);
        }

        [TestCase("!", "VerifiedGoodDump")]
        [TestCase("[!]", "VerifiedGoodDump")]
        [TestCase("Redump", "VerifiedRedump")]
        [TestCase("No-Intro", "NoIntroVerified")]
        [TestCase("b", "UnverifiedRaw")]
        [TestCase("[b1]", "UnverifiedRaw")]
        [TestCase("Beta", "BetaProto")]
        [TestCase("(Proto)", "BetaProto")]
        [TestCase("[T+Eng]", "TranslationPatch")]
        [TestCase("UnknownTag", "Unknown")]
        public void should_find_dump_tier_by_tag(string tag, string expectedQualityName)
        {
            QualityFinder.FindByDumpTag(tag).Name.Should().Be(expectedQualityName);
        }
    }
}
