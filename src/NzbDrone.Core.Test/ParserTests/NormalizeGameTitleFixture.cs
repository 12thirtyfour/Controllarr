using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class NormalizeGameTitleFixture : CoreTest
    {
        [TestCase("Super Mario World", "super mario world")]
        [TestCase("A.B,C;", "a b c")]
        [TestCase("Super  Mario  World", "super mario world")]
        [TestCase("Chrono Trigger (1)", "chrono trigger")]
        public void should_normalize_game_title(string input, string expected)
        {
            var result = Parser.Parser.NormalizeEpisodeTitle(input);

            result.Should().Be(expected);
        }
    }
}
