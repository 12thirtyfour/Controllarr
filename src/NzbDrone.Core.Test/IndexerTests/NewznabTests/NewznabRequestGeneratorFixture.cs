using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    public class NewznabRequestGeneratorFixture : CoreTest<NewznabRequestGenerator>
    {
        private SingleEpisodeSearchCriteria _singleEpisodeSearchCriteria;
        private SeasonSearchCriteria _seasonSearchCriteria;
        private AnimeEpisodeSearchCriteria _animeSearchCriteria;
        private AnimeSeasonSearchCriteria _animeSeasonSearchCriteria;
        private NewznabCapabilities _capabilities;

        [SetUp]
        public void SetUp()
        {
            Subject.Definition = new IndexerDefinition
            {
                Name = "Newznab"
            };

            Subject.Settings = new NewznabSettings()
            {
                BaseUrl = "http://127.0.0.1:1234/",
                Categories = new[] { 1, 2 },
                ApiKey = "abcd",
            };

            _singleEpisodeSearchCriteria = new SingleEpisodeSearchCriteria
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string> { "Monkey Island" },
                SeasonNumber = 1,
                EpisodeNumber = 2
            };

            _seasonSearchCriteria = new SeasonSearchCriteria
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string> { "Monkey Island" },
                SeasonNumber = 1,
            };

            _animeSearchCriteria = new AnimeEpisodeSearchCriteria()
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string>() { "Monkey+Island" },
                AbsoluteEpisodeNumber = 100,
                SeasonNumber = 5,
                EpisodeNumber = 4
            };

            _animeSeasonSearchCriteria = new AnimeSeasonSearchCriteria()
            {
                Series = new Tv.Series { TvRageId = 10, TvdbId = 20, TvMazeId = 30, ImdbId = "t40", TmdbId = 50 },
                SceneTitles = new List<string> { "Monkey Island" },
                SeasonNumber = 3,
            };

            _capabilities = new NewznabCapabilities();

            Mocker.GetMock<INewznabCapabilitiesProvider>()
                .Setup(v => v.GetCapabilities(It.IsAny<NewznabSettings>()))
                .Returns(_capabilities);
        }

        [Test]
        public void should_use_all_categories_for_feed()
        {
            var results = Subject.GetRecentRequests();

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("&cat=1,2&");
        }

        [Test]
        public void should_not_have_duplicate_categories()
        {
            Subject.Settings.Categories = new[] { 1, 2, 3 };

            var results = Subject.GetRecentRequests();

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.FullUri.Should().Contain("&cat=1,2,3&");
        }

        [Test]
        public void should_return_empty_for_anime_episode_search()
        {
            var results = Subject.GetSearchRequests(_animeSearchCriteria);
            results.GetAllTiers().Should().BeEmpty();
        }

        [Test]
        public void should_return_empty_for_anime_season_search()
        {
            var results = Subject.GetSearchRequests(_animeSeasonSearchCriteria);
            results.GetAllTiers().Should().BeEmpty();
        }

        [Test]
        public void should_not_search_by_rid_if_not_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().NotContain("rid=10");
            page.Url.Query.Should().Contain("q=Monkey");
        }

        [Test]
        public void should_search_by_rid_if_supported()
        {
            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("rid=10");
        }

        [Test]
        public void should_not_search_by_tvdbid_if_not_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().NotContain("rid=10");
            page.Url.Query.Should().Contain("q=Monkey");
        }

        [Test]
        public void should_search_by_tvdbid_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
        }

        [Test]
        public void should_search_by_tvmaze_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvmazeid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvmazeid=30");
        }

        [Test]
        public void should_search_by_imdbid_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "imdbid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("imdbid=t40");
        }

        [Test]
        public void should_search_by_tmdb_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tmdbid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tmdbid=50");
        }

        [Test]
        public void should_prefer_search_by_tvdbid_if_rid_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "season", "ep" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
            page.Url.Query.Should().NotContain("rid=10");
        }

        [Test]
        public void should_use_aggregrated_id_search_if_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
            page.Url.Query.Should().Contain("rid=10");
        }

        [Test]
        public void should_not_use_aggregrated_id_search_if_no_ids_supported()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true; // Turns true if indexer supplies supportedParams.

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(1);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_not_use_aggregrated_id_search_if_no_ids_are_known()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true; // Turns true if indexer supplies supportedParams.

            _singleEpisodeSearchCriteria.Series.TvRageId = 0;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            var page = results.GetTier(0).First().First();

            page.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_fallback_to_title()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "title", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tvdbid=20");
            pageTier2.Url.Query.Should().NotContain("rid=10");
            pageTier2.Url.Query.Should().NotContain("q=");
            pageTier2.Url.Query.Should().Contain("title=Monkey%20Island");
        }

        [Test]
        public void should_url_encode_title()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "title", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            _singleEpisodeSearchCriteria.SceneTitles[0] = "Elith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tvdbid=20");
            pageTier2.Url.Query.Should().NotContain("rid=10");
            pageTier2.Url.Query.Should().NotContain("q=");
            pageTier2.Url.Query.Should().Contain("title=Elith%20%26%20Little");
        }

        [Test]
        public void should_fallback_to_q()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(2);

            var pageTier2 = results.GetTier(1).First().First();

            pageTier2.Url.Query.Should().NotContain("tvdbid=20");
            pageTier2.Url.Query.Should().NotContain("rid=10");
            pageTier2.Url.Query.Should().Contain("q=");
        }

        [Test]
        public void should_encode_raw_title()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _capabilities.TvTextSearchEngine = "raw";
            _singleEpisodeSearchCriteria.SceneTitles[0] = "Edith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(1);

            var pageTier = results.GetTier(0).First().First();

            pageTier.Url.Query.Should().Contain("q=Edith%20%26%20Little");
            pageTier.Url.Query.Should().NotContain(" & ");
            pageTier.Url.Query.Should().Contain("%26");
        }

        [Test]
        public void should_use_clean_title_and_encode()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _capabilities.TvTextSearchEngine = "sphinx";
            _singleEpisodeSearchCriteria.SceneTitles[0] = "Edith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.Tiers.Should().Be(1);

            var pageTier = results.GetTier(0).First().First();

            pageTier.Url.Query.Should().Contain("q=Edith%20and%20Little");
            pageTier.Url.Query.Should().Contain("and");
            pageTier.Url.Query.Should().NotContain(" & ");
            pageTier.Url.Query.Should().NotContain("%26");
        }

        [Test]
        public void should_allow_season_search_even_if_episode_search_is_not_allowed()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "season" };

            var results = Subject.GetSearchRequests(_seasonSearchCriteria);
            results.GetTier(0).Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("tvdbid=20");
        }

        [Test]
        public void should_not_allow_season_search_if_season_param_is_not_allowed()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid" };

            var results = Subject.GetSearchRequests(_seasonSearchCriteria);
            results.GetTier(0).Should().HaveCount(0);
        }

        [Test]
        public void should_search_games_using_general_search_with_q_and_no_season_or_episode_params()
        {
            Subject.MaxPages = 1;
            var gameCriteria = new SingleEpisodeSearchCriteria
            {
                Series = new Tv.Series { Title = "Nintendo 3DS", TitleSlug = "3ds", TvdbId = 1045 },
                Episodes = new List<Tv.Episode>
                {
                    new Tv.Episode { Title = "Pokemon Ultra Sun", SeasonNumber = 1, EpisodeNumber = 4 }
                },
                SeasonNumber = 1,
                EpisodeNumber = 4
            };

            var results = Subject.GetSearchRequests(gameCriteria);

            results.GetAllTiers().Should().HaveCount(1);
            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("t=search");
            page.Url.Query.Should().Contain("q=Pokemon%20Ultra%20Sun");
            page.Url.Query.Should().NotContain("season=");
            page.Url.Query.Should().NotContain("ep=");
            page.Url.Query.Should().NotContain("tvdbid=");
        }

        [Test]
        public void should_search_platform_collections_using_collection_terms_and_no_season_param()
        {
            Subject.MaxPages = 1;
            var collectionCriteria = new SeasonSearchCriteria
            {
                Series = new Tv.Series { Title = "Nintendo 64", TitleSlug = "n64", TvdbId = 1005 },
                IsPlatformCollectionSearch = true,
                SceneTitles = new List<string> { "@N64", "[Nintendo - Nintendo 64", "Nintendo 64 complete" },
                SeasonNumber = 1
            };

            var results = Subject.GetSearchRequests(collectionCriteria);

            results.GetAllTiers().Should().HaveCount(3);
            var pages = results.GetAllTiers().Select(t => t.First()).ToList();
            pages.Should().HaveCount(3);

            pages.Any(p => p.Url.Query.Contains("q=%40N64")).Should().BeTrue();
            pages.All(p => !p.Url.Query.Contains("season=")).Should().BeTrue();
            pages.All(p => p.Url.Query.Contains("t=search")).Should().BeTrue();
        }
    }
}
