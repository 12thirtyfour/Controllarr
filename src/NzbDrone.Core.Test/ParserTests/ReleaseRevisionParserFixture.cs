using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class ReleaseRevisionParserFixture : CoreTest
    {
        [TestCase("Super Mario 64 (USA).z64", 1, false)]
        [TestCase("Super Mario 64 (USA) [v2].z64", 2, false)]
        [TestCase("Chrono Trigger (USA) v2.sfc", 2, false)]
        [TestCase("The Legend of Zelda (USA) PROPER.nes", 2, false)]
        [TestCase("Sonic the Hedgehog (USA) REPACK.md", 2, true)]
        [TestCase("Sonic the Hedgehog (USA) REPACK2.md", 3, true)]
        public void should_be_able_to_parse_repack(string title, int version, bool isRepack)
        {
            var result = QualityParser.ParseQuality(title);
            result.Revision.Version.Should().Be(version);
            result.Revision.IsRepack.Should().Be(isRepack);
        }
    }
}
