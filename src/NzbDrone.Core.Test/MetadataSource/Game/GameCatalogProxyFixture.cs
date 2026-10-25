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
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros." && e.FinaleType == "Rev 1");
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros." && e.FinaleType == "Beta");

            var unlicensedEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionUnlicensedSeason).ToList();
            unlicensedEpisodes.Should().Contain(e => e.Title.Contains("Bible Adventures"));

            var homebrewEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionHomebrewSeason).ToList();
            homebrewEpisodes.Should().Contain(e => e.Title.Contains("Micro Mages"));

            GameCatalogProxy.IsPreReleaseVersion("Proto", "Star Fox 2 (USA) (Proto)").Should().BeTrue();
            GameCatalogProxy.IsPreReleaseVersion("Beta", "Legend of Zelda, The (USA) (Beta)").Should().BeTrue();
            GameCatalogProxy.IsPreReleaseVersion("Retail (v1.0)", "Super Mario Bros. (World)").Should().BeFalse();
        }

        [Test]
        public void should_cross_reference_separate_databases_without_populating_duplicates()
        {
            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetEntriesForPlatform("snes", Moq.It.IsAny<string>(), Moq.It.IsAny<string>()))
                .Returns(new System.Collections.Generic.List<NzbDrone.Core.Dat.DatEntry>
                {
                    new NzbDrone.Core.Dat.DatEntry
                    {
                        DatFileId = 1,
                        GameName = "EarthBound (USA)",
                        RomName = "EarthBound (USA).sfc",
                        Sha1 = "1111111111111111111111111111111111111111"
                    },
                    new NzbDrone.Core.Dat.DatEntry
                    {
                        DatFileId = 2,
                        GameName = "1 Super Nintendo/USA/EarthBound (USA).sfc",
                        RomName = "1 Super Nintendo/USA/EarthBound (USA).sfc",
                        Sha1 = "1111111111111111111111111111111111111111"
                    },
                    new NzbDrone.Core.Dat.DatEntry
                    {
                        DatFileId = 3,
                        GameName = "Terranigma (Europe)",
                        RomName = "Terranigma (Europe).sfc",
                        Sha1 = "2222222222222222222222222222222222222222"
                    },
                    new NzbDrone.Core.Dat.DatEntry
                    {
                        DatFileId = 4,
                        GameName = "1 Super Nintendo/Europe/Terranigma (Europe).sfc",
                        RomName = "1 Super Nintendo/Europe/Terranigma (Europe).sfc",
                        Sha256 = "3333333333333333333333333333333333333333333333333333333333333333"
                    }
                });

            Subject.InvalidatePlatformCache(1003);
            var tuple = Subject.GetSeriesInfo(1003, Language.English, null);

            tuple.Item2.Where(e => e.Title == "EarthBound" && e.SeasonNumber == GameCatalogProxy.RegionUsaSeason)
                .Should().ContainSingle();
            tuple.Item2.Where(e => e.Title == "Terranigma" && e.SeasonNumber == GameCatalogProxy.RegionEuropeSeason)
                .Should().ContainSingle();
        }

        [Test]
        public void should_default_to_decrypted_catalog_for_nds_and_support_encrypted_preference_and_smdb_and_nswdb()
        {
            var decryptedNdsDat = @"
game (
	name ""Chrono Trigger (USA) (Decrypted)""
	rom ( name ""Chrono Trigger (USA).nds"" size 67108864 )
)";
            var smdbContent = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899\tNeoSD/World/Garou - Mark of the Wolves (World).neo\t1122334455667788990011223344556677889900\t11223344556677889900112233445566\tdeadbeef";
            var nswdbXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<releases>
  <release>
    <name>Metroid Dread</name>
    <region>USA</region>
    <imgcrc>12345678</imgcrc>
    <revision>0</revision>
  </release>
</releases>";

            var httpMock = Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>();
            httpMock
                .Setup(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.FullUri.Contains("DS%20(Decrypted).dat"))))
                .Returns((NzbDrone.Common.Http.HttpRequest r) => new NzbDrone.Common.Http.HttpResponse(r, new NzbDrone.Common.Http.HttpHeader(), decryptedNdsDat));

            httpMock
                .Setup(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.FullUri.Contains("NeoSD%20SMDB.txt"))))
                .Returns((NzbDrone.Common.Http.HttpRequest r) => new NzbDrone.Common.Http.HttpResponse(r, new NzbDrone.Common.Http.HttpHeader(), smdbContent));

            httpMock
                .Setup(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.FullUri.Contains("nswdb.com/xml.php"))))
                .Returns((NzbDrone.Common.Http.HttpRequest r) => new NzbDrone.Common.Http.HttpResponse(r, new NzbDrone.Common.Http.HttpHeader(), nswdbXml));

            Subject.InvalidatePlatformCache();

            // 1. Nintendo DS (1012) includes both Decrypted and Encrypted versions under the same base game
            //    and monitors NotEncrypted by default, Encrypted when "encrypted", or both when "both".
            var ndsNotEncrypted = Subject.GetSeriesInfo(1012, Language.English, "notEncrypted");
            ndsNotEncrypted.Item2.Should().Contain(e => e.Title.Contains("Chrono Trigger"));
            var heartGoldUnencrypted = ndsNotEncrypted.Item2.First(e => e.Title == "Pokemon - HeartGold Version" && e.FinaleType != "Encrypted");
            var heartGoldEncrypted = ndsNotEncrypted.Item2.First(e => e.Title == "Pokemon - HeartGold Version" && e.FinaleType == "Encrypted");
            heartGoldUnencrypted.Monitored.Should().BeTrue();
            heartGoldEncrypted.Monitored.Should().BeFalse();

            var ndsEncrypted = Subject.GetSeriesInfo(1012, Language.English, "encrypted");
            ndsEncrypted.Item2.First(e => e.Title == "Pokemon - HeartGold Version" && e.FinaleType != "Encrypted").Monitored.Should().BeFalse();
            ndsEncrypted.Item2.First(e => e.Title == "Pokemon - HeartGold Version" && e.FinaleType == "Encrypted").Monitored.Should().BeTrue();

            var ndsBoth = Subject.GetSeriesInfo(1012, Language.English, "both");
            ndsBoth.Item2.First(e => e.Title == "Pokemon - HeartGold Version" && e.FinaleType != "Encrypted").Monitored.Should().BeTrue();
            ndsBoth.Item2.First(e => e.Title == "Pokemon - HeartGold Version" && e.FinaleType == "Encrypted").Monitored.Should().BeTrue();

            // 2. SNK Neo Geo (1038) syncs SmokeMonster NeoSD SMDB
            var neoGeoTuple = Subject.GetSeriesInfo(1038, Language.English, null);
            neoGeoTuple.Item2.Should().Contain(e => e.Title.Contains("Garou - Mark of the Wolves"));

            // 3. Nintendo Switch (1014) syncs NSWDB XML
            var switchTuple = Subject.GetSeriesInfo(1014, Language.English, null);
            switchTuple.Item2.Should().Contain(e => e.Title.Contains("Metroid Dread"));

            // 4. Standalone Arcade platform (1041) is not present
            Subject.TryGetPlatformEntry(1041, out _).Should().BeFalse();
        }
    }
}
