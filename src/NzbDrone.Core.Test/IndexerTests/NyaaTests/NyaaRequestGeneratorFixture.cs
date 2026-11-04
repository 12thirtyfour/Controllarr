using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers.Nyaa;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NyaaTests
{
    public class NyaaRequestGeneratorFixture : CoreTest<NyaaRequestGenerator>
    {
        private SingleEpisodeSearchCriteria _singleSearchCriteria;
        private SeasonSearchCriteria _seasonSearchCriteria;
        private SpecialEpisodeSearchCriteria _specialSearchCriteria;
        private AnimeEpisodeSearchCriteria _animeSearchCriteria;
        private AnimeSeasonSearchCriteria _animeSeasonSearchCriteria;

        [SetUp]
        public void SetUp()
        {
            Subject.Settings = new NyaaSettings()
            {
                BaseUrl = "http://127.0.0.1:1234/",
            };

            _singleSearchCriteria = new SingleEpisodeSearchCriteria()
            {
                SceneTitles = new List<string>() { "Chrono Trigger" },
                SeasonNumber = 1,
                EpisodeNumber = 1
            };

            _seasonSearchCriteria = new SeasonSearchCriteria()
            {
                SceneTitles = new List<string>() { "Super Nintendo" },
                SeasonNumber = 1,
            };

            _specialSearchCriteria = new SpecialEpisodeSearchCriteria()
            {
                EpisodeQueryTitles = new[] { "Pokemon Emerald Rogue" }
            };

            _animeSearchCriteria = new AnimeEpisodeSearchCriteria()
            {
                SceneTitles = new List<string>() { "Naruto" },
                AbsoluteEpisodeNumber = 9
            };

            _animeSeasonSearchCriteria = new AnimeSeasonSearchCriteria()
            {
                SceneTitles = new List<string>() { "Naruto" },
                SeasonNumber = 3
            };
        }

        [Test]
        public void should_search_game_single()
        {
            var results = Subject.GetSearchRequests(_singleSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);
            var page = results.GetAllTiers().First().First();

            page.Url.FullUri.Should().Contain("term=Chrono+Trigger");
            page.Url.FullUri.Should().Contain("cats=6_2&filter=1");
        }

        [Test]
        public void should_search_platform_collection()
        {
            var results = Subject.GetSearchRequests(_seasonSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);
            var page = results.GetAllTiers().First().First();

            page.Url.FullUri.Should().Contain("term=Super+Nintendo");
            page.Url.FullUri.Should().Contain("cats=6_2&filter=1");
        }

        [Test]
        public void should_search_special_query()
        {
            var results = Subject.GetSearchRequests(_specialSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);
            var page = results.GetAllTiers().First().First();

            page.Url.FullUri.Should().Contain("term=Pokemon+Emerald+Rogue");
            page.Url.FullUri.Should().Contain("cats=6_2&filter=1");
        }

        [Test]
        public void should_return_empty_for_anime_criteria()
        {
            Subject.GetSearchRequests(_animeSearchCriteria).GetAllTiers().Should().BeEmpty();
            Subject.GetSearchRequests(_animeSeasonSearchCriteria).GetAllTiers().Should().BeEmpty();
        }
    }
}
