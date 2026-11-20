using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class QualityAllowedByProfileSpecificationFixture : CoreTest<QualityAllowedByProfileSpecification>
    {
        private RemoteEpisode _remoteEpisode;

        public static object[] CandidateQualities =
        {
            new object[] { Quality.VerifiedGoodDump },
            new object[] { Quality.VerifiedRedump },
            new object[] { Quality.NoIntroVerified },
            new object[] { Quality.UnverifiedRaw },
            new object[] { Quality.Unknown },
            new object[] { Quality.DVD },
            new object[] { Quality.HDTV720p }
        };

        [SetUp]
        public void Setup()
        {
            _remoteEpisode = new RemoteEpisode
            {
                Series = new Series(),
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.VerifiedGoodDump) },
            };
        }

        [Test]
        [TestCaseSource(nameof(CandidateQualities))]
        public void should_accept_all_candidate_qualities_without_profile_rejection(Quality qualityType)
        {
            _remoteEpisode.ParsedEpisodeInfo.Quality.Quality = qualityType;

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }
    }
}
