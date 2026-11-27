using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class QualityParserFixture : CoreTest
    {
        [TestCase("Super Mario World (USA) [!].sfc")]
        [TestCase("Chrono Trigger (USA) [Verified].sfc")]
        [TestCase("The Legend of Zelda (USA) [GoodDump].nes")]
        public void should_parse_verified_good_dump(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.VerifiedGoodDump);
        }

        [TestCase("Xenogears (USA) (Disc 1) [Redump].chd")]
        [TestCase("Castlevania - Symphony of the Night (USA) (Redump).bin")]
        public void should_parse_verified_redump(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.VerifiedRedump);
        }

        [TestCase("Super Mario Bros. (World) [No-Intro].nes")]
        [TestCase("Metroid (USA) (NoIntro).nes")]
        public void should_parse_nointro_verified(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.NoIntroVerified);
        }

        [TestCase("Sonic the Hedgehog (USA) (Beta).md")]
        [TestCase("EarthBound (USA) (Proto).sfc")]
        [TestCase("Panzer Dragoon (USA) (Demo).chd")]
        public void should_parse_beta_proto(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.BetaProto);
        }

        [TestCase("Mother 3 (Japan) [T+Eng1.3].gba")]
        [TestCase("Final Fantasy V (Japan) [h1].sfc")]
        [TestCase("Seiken Densetsu 3 (Translation).sfc")]
        public void should_parse_translation_patch(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.TranslationPatch);
        }

        [TestCase("Donkey Kong (USA) [b1].nes")]
        [TestCase("Contra (USA) [Unverified].nes")]
        public void should_parse_unverified_raw(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.UnverifiedRaw);
        }

        [TestCase("Super Mario World (USA).sfc")]
        public void should_parse_unknown_when_no_dump_tag_present(string title)
        {
            var result = QualityParser.ParseQuality(title);
            result.Quality.Should().Be(Quality.Unknown);
        }

        [TestCase("Super Mario 64 (USA) PROPER.z64", 2, false)]
        [TestCase("Super Mario 64 (USA) REPACK.z64", 2, true)]
        public void should_parse_proper_and_repack_revisions(string title, int expectedVersion, bool expectedRepack)
        {
            var result = QualityParser.ParseQuality(title);
            result.Revision.Version.Should().Be(expectedVersion);
            result.Revision.IsRepack.Should().Be(expectedRepack);
        }
    }
}
