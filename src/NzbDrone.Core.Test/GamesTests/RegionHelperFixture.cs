using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class RegionHelperFixture
    {
        [TestCase("japan-j3")]
        [TestCase("sp-1v1_3db8c")]
        [TestCase("sp1-j3")]
        [TestCase("sp1-u3")]
        [TestCase("sp1-u4")]
        [TestCase("srom")]
        [TestCase("sp1-u4.bin")]
        public void should_detect_neogeo_bios_names(string name)
        {
            RegionHelper.IsBios(name).Should().BeTrue();
        }

        [TestCase("Metal Slug")]
        [TestCase("King of Fighters '98")]
        public void should_not_flag_regular_neogeo_games_as_bios(string name)
        {
            RegionHelper.IsBios(name).Should().BeFalse();
        }
    }
}
