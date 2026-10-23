using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.GamesTests
{
    [TestFixture]
    public class CollectionStrategyServiceFixture : CoreTest<CollectionStrategyService>
    {
        private List<ParsedRomInfo> _candidates;

        [SetUp]
        public void Setup()
        {
            _candidates = new List<ParsedRomInfo>
            {
                RomParser.ParseRomName("Chrono Trigger (USA).sfc"),
                RomParser.ParseRomName("Chrono Trigger (USA) (Rev A).sfc"),
                RomParser.ParseRomName("Chrono Trigger (Japan).sfc"),
                RomParser.ParseRomName("Chrono Trigger (Japan) (Rev A).sfc")
            };
        }

        [Test]
        public void Should_select_single_winner_per_console_under_1g1r()
        {
            var results = Subject.FilterCandidates(
                _candidates,
                CollectionStrategy.OneGameOneRom,
                RevisionPreference.LatestRevision);

            results.Should().ContainSingle();
            results.First().Regions.Should().Contain("USA");
            results.First().Revision.Should().Be("A");
        }

        [Test]
        public void Should_select_one_winner_per_region_under_1g1r_regional()
        {
            var results = Subject.FilterCandidates(
                _candidates,
                CollectionStrategy.OneGamePerRegionPerConsole,
                RevisionPreference.LatestRevision);

            results.Should().HaveCount(2);
            results.Should().Contain(r => r.Regions.Contains("USA") && r.Revision == "A");
            results.Should().Contain(r => r.Regions.Contains("Japan") && r.Revision == "A");
        }

        [Test]
        public void Should_respect_console_level_strategy_override()
        {
            var platform = new GamePlatform
            {
                Name = "Super Nintendo",
                CollectionStrategy = CollectionStrategy.OneGamePerRegionPerConsole
            };

            Subject.ResolveEffectiveStrategy(platform, CollectionStrategy.OneGameOneRom)
                   .Should().Be(CollectionStrategy.OneGamePerRegionPerConsole);
        }
    }
}
