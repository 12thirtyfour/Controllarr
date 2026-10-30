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
        public void should_search_by_query_and_never_include_tv_ids_or_season_parameters()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "rid", "tvmazeid", "imdbid", "tmdbid", "season", "ep" };
            _capabilities.SupportsAggregateIdSearch = true;

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);

            results.GetAllTiers().Should().HaveCount(1);
            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("t=search");
            page.Url.Query.Should().Contain("q=Monkey%20Island");
            page.Url.Query.Should().NotContain("tvdbid=");
            page.Url.Query.Should().NotContain("rid=");
            page.Url.Query.Should().NotContain("tvmazeid=");
            page.Url.Query.Should().NotContain("imdbid=");
            page.Url.Query.Should().NotContain("tmdbid=");
            page.Url.Query.Should().NotContain("season=");
            page.Url.Query.Should().NotContain("ep=");
        }

        [Test]
        public void should_url_encode_special_characters_in_query()
        {
            _singleEpisodeSearchCriteria.SceneTitles[0] = "Edith & Little";

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetAllTiers().Should().HaveCount(2);

            var pages = results.GetAllTiers().Select(t => t.First()).ToList();

            pages.Any(p => p.Url.Query.Contains("q=Edith%20%26%20Little")).Should().BeTrue();
            pages.All(p => !p.Url.Query.Contains(" & ")).Should().BeTrue();
        }

        [Test]
        public void should_search_by_game_title_when_scene_titles_are_empty()
        {
            _singleEpisodeSearchCriteria.SceneTitles = new List<string>();
            _singleEpisodeSearchCriteria.Episodes = new List<Tv.Episode>
            {
                new Tv.Episode { Title = "Super Mario World", SeasonNumber = 1, EpisodeNumber = 1 }
            };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("q=Super%20Mario%20World");
            page.Url.Query.Should().NotContain("season=");
            page.Url.Query.Should().NotContain("ep=");
        }

        [Test]
        public void should_search_by_platform_title_when_no_game_or_scene_titles()
        {
            _singleEpisodeSearchCriteria.SceneTitles = new List<string>();
            _singleEpisodeSearchCriteria.Episodes = new List<Tv.Episode>();
            _singleEpisodeSearchCriteria.Series = new Tv.Series { Title = "Super Nintendo Entertainment System" };

            var results = Subject.GetSearchRequests(_singleEpisodeSearchCriteria);
            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("q=Super%20Nintendo%20Entertainment%20System");
        }

        [Test]
        public void should_allow_season_search_as_collection_search_without_season_parameter()
        {
            _capabilities.SupportedTvSearchParameters = new[] { "q", "tvdbid", "season" };

            var results = Subject.GetSearchRequests(_seasonSearchCriteria);
            results.GetAllTiers().Should().HaveCount(1);

            var page = results.GetAllTiers().First().First();

            page.Url.Query.Should().Contain("t=search");
            page.Url.Query.Should().Contain("q=Monkey%20Island");
            page.Url.Query.Should().NotContain("season=");
            page.Url.Query.Should().NotContain("tvdbid=");
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
