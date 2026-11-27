using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class ReleaseGroupParserFixture : CoreTest
    {
        [TestCase("Super.Mario.64.(USA).[No-Intro]-Mercury", "Mercury")]
        [TestCase("Final.Fantasy.VII.(USA).(Disc.1).[Redump]-RedumpGrp", "RedumpGrp")]
        [TestCase("The.Legend.of.Zelda.(USA).[!].[TOSEC]", "TOSEC")]
        [TestCase("Chrono.Trigger.(USA).[!]-SMDB", "SMDB")]
        [TestCase("Super.Metroid.(Japan,USA).[No-Intro]-HTGDB.sfc", "HTGDB")]
        [TestCase("Chrono Trigger (USA) [!].sfc", null)]
        [TestCase("Super Mario World (USA) (Rev 1).sfc", null)]
        [TestCase("Mario Kart 64 (USA) [!]", null)]
        public void should_parse_release_group(string title, string expected)
        {
            Parser.ReleaseGroupParser.ParseReleaseGroup(title).Should().Be(expected);
        }
    }
}
