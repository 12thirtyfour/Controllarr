using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Games
{
    [TestFixture]
    public class CollectionStrategyServiceFixture : CoreTest<CollectionStrategyService>
    {
        [Test]
        public void should_never_consider_demo_proto_or_beta_when_only_prerelease_exists_in_1g1r()
        {
            var candidates = new List<ParsedRomInfo>
            {
                new ParsedRomInfo { ReleaseTitle = "Star Fox 2 (USA) (Proto)", Regions = new List<string> { "USA" }, DumpTags = new List<string> { "Proto" } },
                new ParsedRomInfo { ReleaseTitle = "Super Mario World (USA) (Beta)", Regions = new List<string> { "USA" }, DumpTags = new List<string> { "Beta" } },
                new ParsedRomInfo { ReleaseTitle = "Zelda (USA) (Demo)", Regions = new List<string> { "USA" }, DumpTags = new List<string> { "Demo" } }
            };

            var result = Subject.FilterCandidates(
                candidates,
                CollectionStrategy.OneGameOneRom,
                RevisionPreference.LatestRevision,
                null,
                new List<string> { "USA", "Europe", "Japan" }).ToList();

            result.Should().BeEmpty();
        }

        [Test]
        public void should_select_released_version_and_ignore_prereleases_in_1g1r()
        {
            var candidates = new List<ParsedRomInfo>
            {
                new ParsedRomInfo { ReleaseTitle = "Super Mario Bros. 3 (USA) (Proto)", Regions = new List<string> { "USA" }, DumpTags = new List<string> { "Proto" } },
                new ParsedRomInfo { ReleaseTitle = "Super Mario Bros. 3 (USA)", Regions = new List<string> { "USA" } },
                new ParsedRomInfo { ReleaseTitle = "Super Mario Bros. 3 (USA) (Rev 1)", Regions = new List<string> { "USA" }, Revision = "Rev 1" }
            };

            var initialResult = Subject.FilterCandidates(
                candidates,
                CollectionStrategy.OneGameOneRom,
                RevisionPreference.InitialRelease,
                null,
                new List<string> { "USA" }).ToList();

            initialResult.Should().ContainSingle();
            initialResult[0].ReleaseTitle.Should().Be("Super Mario Bros. 3 (USA)");

            var highestResult = Subject.FilterCandidates(
                candidates,
                CollectionStrategy.OneGameOneRom,
                RevisionPreference.LatestRevision,
                null,
                new List<string> { "USA" }).ToList();

            highestResult.Should().ContainSingle();
            highestResult[0].ReleaseTitle.Should().Be("Super Mario Bros. 3 (USA) (Rev 1)");
        }

        [Test]
        public void should_include_prereleases_in_preservationist_strategy()
        {
            var candidates = new List<ParsedRomInfo>
            {
                new ParsedRomInfo { ReleaseTitle = "Star Fox 2 (USA) (Proto)", Regions = new List<string> { "USA" }, DumpTags = new List<string> { "Proto" } },
                new ParsedRomInfo { ReleaseTitle = "Super Mario Bros. 3 (USA)", Regions = new List<string> { "USA" } }
            };

            var result = Subject.FilterCandidates(
                candidates,
                CollectionStrategy.Preservationist,
                RevisionPreference.LatestRevision,
                null,
                new List<string> { "USA" }).ToList();

            result.Should().HaveCount(2);
        }
    }
}
