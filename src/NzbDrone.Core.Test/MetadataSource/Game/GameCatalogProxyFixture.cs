using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.Game
{
    [TestFixture]
    public class GameCatalogProxyFixture : CoreTest<GameCatalogProxy>
    {
        [Test]
        public void should_return_pokemon_games_when_searching_for_pokemon()
        {
            var results = Subject.SearchForNewSeries("Pokemon", Language.English);

            results.Should().NotBeEmpty();
            results.Should().Contain(r => r.Title.Contains("Pokemon - Emerald Version"));
            results.Should().Contain(r => r.Title.Contains("Pokemon - FireRed Version"));
            results.Should().Contain(r => r.Title.Contains("Pokemon - Red Version"));
            results.All(r => r.TvdbId >= 100000).Should().BeTrue();
        }

        [Test]
        public void should_return_empty_when_searching_for_tv_show()
        {
            var results = Subject.SearchForNewSeries("Breaking Bad", Language.English);

            results.Should().BeEmpty();
        }

        [Test]
        public void should_return_platform_when_searching_for_console_name_or_alias()
        {
            var results = Subject.SearchForNewSeries("SNES", Language.English);

            results.Should().NotBeEmpty();
            results.First().Title.Should().Be("Super Nintendo Entertainment System");
            results.First().TitleSlug.Should().Be("snes");
            results.First().Seasons.Should().HaveCount(7);
        }

        [Test]
        public void should_return_platform_and_regional_games_from_get_series_info()
        {
            var tuple = Subject.GetSeriesInfo(1011, Language.English, null);

            tuple.Item1.Title.Should().Be("Game Boy Advance");
            tuple.Item1.Seasons.Should().HaveCount(7);
            tuple.Item2.Should().NotBeEmpty();
            tuple.Item2.Should().Contain(e => e.Title.Contains("Pokemon - Emerald Version") && e.SeasonNumber == GameCatalogProxy.RegionUsaSeason);
        }

        [Test]
        public void should_resolve_parent_platform_when_get_series_info_called_with_game_id()
        {
            var searchResults = Subject.SearchForNewSeries("Pokemon Emerald", Language.English);
            var emerald = searchResults.First(r => r.Title.Contains("Pokemon - Emerald Version"));

            var tuple = Subject.GetSeriesInfo(emerald.TvdbId, Language.English, null);

            tuple.Item1.Title.Should().Be("Game Boy Advance");
            Subject.HasPendingGameSelections(tuple.Item1.TvdbId).Should().BeTrue();
            tuple.Item2.Where(e => e.Monitored).Should().ContainSingle(e => e.TvdbId == emerald.TvdbId);

            Subject.ClearPendingPlatformSelections(tuple.Item1.TvdbId);
        }

        [Test]
        public void should_populate_full_nes_catalog_and_separate_unlicensed_and_prerelease_versions()
        {
            var sampleDat = @"
game (
	name ""Super Mario Bros. (USA)""
	rom ( name ""Super Mario Bros. (USA).nes"" size 40976 )
)
game (
	name ""Super Mario Bros. (USA) (Rev 1)""
	rom ( name ""Super Mario Bros. (USA) (Rev 1).nes"" size 40976 )
)
game (
	name ""Super Mario Bros. (USA) (Beta)""
	rom ( name ""Super Mario Bros. (USA) (Beta).nes"" size 40976 )
)
game (
	name ""Bible Adventures (USA)""
	rom ( name ""Bible Adventures (USA) (Unl).nes"" size 131088 )
)
game (
	name ""Micro Mages (World)""
	rom ( name ""Micro Mages (World) (Aftermarket) (Unl).nes"" size 40976 )
)";

            Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>()
                .Setup(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.FullUri.Contains("no-intro"))))
                .Returns((NzbDrone.Common.Http.HttpRequest r) => new NzbDrone.Common.Http.HttpResponse(r, new NzbDrone.Common.Http.HttpHeader(), sampleDat));

            var tuple = Subject.GetSeriesInfo(1001, Language.English, null);

            tuple.Item1.Title.Should().Be("Nintendo Entertainment System");
            tuple.Item1.Images.Should().Contain(i => i.RemoteUrl == "/Content/Images/Consoles/nes.png");

            var usaEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionUsaSeason).ToList();
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros.");
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros. (Rev 1)");
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros. (Beta)");

            var unlicensedEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionUnlicensedSeason).ToList();
            unlicensedEpisodes.Should().Contain(e => e.Title.Contains("Bible Adventures"));

            var homebrewEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionHomebrewSeason).ToList();
            homebrewEpisodes.Should().Contain(e => e.Title.Contains("Micro Mages"));

            GameCatalogProxy.IsPreReleaseVersion("Proto", "Star Fox 2 (USA) (Proto)").Should().BeTrue();
            GameCatalogProxy.IsPreReleaseVersion("Beta", "Legend of Zelda, The (USA) (Beta)").Should().BeTrue();
            GameCatalogProxy.IsPreReleaseVersion("Retail (v1.0)", "Super Mario Bros. (World)").Should().BeFalse();
        }
    }
}
