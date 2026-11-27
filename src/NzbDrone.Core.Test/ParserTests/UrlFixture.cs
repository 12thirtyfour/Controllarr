using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class UrlFixture : CoreTest
    {
        [TestCase("[www.test.com] - Super Mario World (USA).sfc", "Super Mario World (USA).sfc")]
        [TestCase("test.net - Chrono Trigger (USA).sfc", "Chrono Trigger (USA).sfc")]
        [TestCase("[www.test-hyphen.co.uk] - The Legend of Zelda (USA).nes", "The Legend of Zelda (USA).nes")]
        [TestCase("[test123.org] - Sonic the Hedgehog (USA).md", "Sonic the Hedgehog (USA).md")]
        [TestCase("Super Mario 64 (USA)-GROUP[www.romsite.org]", "Super Mario 64 (USA)-GROUP")]
        public void should_strip_url_prefixes_and_postfixes(string rawTitle, string expectedClean)
        {
            var cleaned = ParserCommon.WebsitePrefixRegex.Replace(rawTitle);
            cleaned = ParserCommon.WebsitePostfixRegex.Replace(cleaned);
            cleaned.Trim().Should().Be(expectedClean);
        }

        [TestCase("Super.Mario.64.USA.N64-DUMP[www.abb.com]", "DUMP")]
        [TestCase("Chrono.Trigger.USA.SNES-PROPER[www.academy.org]", "PROPER")]
        [TestCase("Final.Fantasy.VII.USA.PS1-REDUMP[www.aetna.net]", "REDUMP")]
        public void should_not_parse_url_in_group(string title, string expected)
        {
            ReleaseGroupParser.ParseReleaseGroup(title).Should().Be(expected);
        }
    }
}
