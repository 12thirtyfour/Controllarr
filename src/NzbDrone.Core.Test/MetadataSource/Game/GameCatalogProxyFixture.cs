using System.Collections.Generic;
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
            tuple.Item1.Seasons.Should().NotBeEmpty();
            tuple.Item1.Seasons.Should().OnlyContain(s => tuple.Item2.Any(e => e.SeasonNumber == s.SeasonNumber));
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
            tuple.Item2.Select(e => e.AbsoluteEpisodeNumber).Should().OnlyHaveUniqueItems();
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
  <release>
    <name>(EN)</name>
    <releasename>Lapis_x_Labyrinth_Limited_Edition_NSW-SUXXORS</releasename>
    <region>WLD</region>
    <imgcrc>87654321</imgcrc>
    <revision>0</revision>
  </release>
  <release>
    <name>(EN) (WORLD)</name>
    <region>WLD</region>
    <imgcrc>11223344</imgcrc>
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
            switchTuple.Item2.Should().Contain(e => e.Title.Contains("Lapis x Labyrinth Limited Edition"));
            switchTuple.Item2.Should().NotContain(e => e.Title == "(EN) (WORLD)");
            switchTuple.Item2.Should().NotContain(e => e.Title == "(EN)");

            // 4. Standalone Arcade platform (1041) is not present
            Subject.TryGetPlatformEntry(1041, out _).Should().BeFalse();
        }

        [Test]
        public void should_not_duplicate_3ds_titles_when_cross_referencing_different_sources()
        {
            Subject.InvalidatePlatformCache();

            var tuple = Subject.GetSeriesInfo(1013, Language.English, "notEncrypted");
            tuple.Item1.Title.Should().Be("Nintendo 3DS");

            var moonGames = tuple.Item2.Where(e => e.Title.Contains("Moon", System.StringComparison.OrdinalIgnoreCase)).ToList();
            var unencryptedMoon = moonGames.Where(e => e.FinaleType != "Encrypted").ToList();
            unencryptedMoon.Should().ContainSingle(e => e.Title == "Pokemon Moon");

            var rubyGames = tuple.Item2.Where(e => e.Title.Contains("Omega Ruby", System.StringComparison.OrdinalIgnoreCase)).ToList();
            var unencryptedRuby = rubyGames.Where(e => e.FinaleType != "Encrypted").ToList();
            unencryptedRuby.Should().ContainSingle(e => e.Title == "Pokemon Omega Ruby");

            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokémon Moon").Should().Be("pokemonmoon");
            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokemon - Moon").Should().Be("pokemonmoon");
            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokémon™ Moon").Should().Be("pokemonmoon");
            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokemon Moon").Should().Be("pokemonmoon");

            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokémon Omega Ruby").Should().Be("pokemonomegaruby");
            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokemon - Omega Ruby").Should().Be("pokemonomegaruby");
            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokémon™ Omega Ruby").Should().Be("pokemonomegaruby");
            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Pokemon Omega Ruby").Should().Be("pokemonomegaruby");
        }

        [Test]
        public void should_parse_gametdb_xml_and_populate_games_and_dat_entries()
        {
            Subject.TryGetPlatformEntry(1014, out var switchPlatform).Should().BeTrue();

            var sampleXml = @"<xml>
  <game name=""Super Mario Odyssey (USA)"">
    <id>AAACA</id>
    <type>Switch</type>
    <region>USA</region>
    <locale lang=""EN"">
      <title>Super Mario Odyssey</title>
      <synopsis>Explore incredible places far from the Mushroom Kingdom.</synopsis>
    </locale>
    <date year=""2017"" month=""10"" day=""27""/>
    <rom version=""1.0.0"" crc=""340DE7BC"" md5=""0123456789abcdef0123456789abcdef"" sha1=""0123456789abcdef0123456789abcdef01234567"" size=""5668962304"" name=""Super Mario Odyssey (USA).xci"" />
  </game>
</xml>";

            var results = Subject.ParseGameTdbXmlContent(switchPlatform, sampleXml, new HashSet<string>());

            results.Should().NotBeEmpty();
            var game = results.First();
            game.Title.Should().Be("Super Mario Odyssey");
            game.RegionSeasonNumber.Should().Be(GameCatalogProxy.RegionUsaSeason);
            game.ReleaseYear.Should().Be(2017);
            game.Overview.Should().Contain("Mushroom Kingdom");
            game.ExpectedCrc32.Should().Be("340DE7BC");
            game.ExpectedMd5.Should().Be("0123456789abcdef0123456789abcdef");
            game.ExpectedSha1.Should().Be("0123456789abcdef0123456789abcdef01234567");

            GameCatalogProxy.GetGameTdbUrl("switch").Should().Be("https://www.gametdb.com/switchtdb.zip");
            GameCatalogProxy.GetGameTdbUrl("wii").Should().Be("https://www.gametdb.com/wiitdb.zip");
            GameCatalogProxy.GetGameTdbUrl("wiiu").Should().Be("https://www.gametdb.com/wiiutdb.zip");
            GameCatalogProxy.GetGameTdbUrl("3ds").Should().Be("https://www.gametdb.com/3dstdb.zip");
            GameCatalogProxy.GetGameTdbUrl("nes").Should().BeNull();
        }

        [Test]
        public void should_classify_gametdb_switch_regions_correctly_when_names_contain_language_tags()
        {
            Subject.TryGetPlatformEntry(1014, out var switchPlatform).Should().BeTrue();

            var sampleXml = @"<xml>
  <game name=""Monster Fruit Academy (EN,FR,JA,KO,ZHCN)"">
    <id>AABBA</id>
    <region>ALL</region>
    <languages>EN,FR,JA,KO,ZHCN</languages>
    <locale lang=""EN"">
      <title>Monster Fruit Academy</title>
    </locale>
  </game>
  <game name=""MODEL Debut 2 #nicola (JA)"">
    <id>AABBB</id>
    <region>JPN</region>
    <languages>JA</languages>
    <locale lang=""JA"">
      <title>MODEL Debut 2 #nicola</title>
    </locale>
  </game>
  <game name=""PJ MASKS: HEROES OF THE NIGHT (EN,FR,DE,ES,IT,NL,PT,SE,DK,NO,FI,AR,ZHCN,RU,PL)"">
    <id>A22EA</id>
    <region>EUR</region>
    <locale lang=""EN"">
      <title>PJ MASKS: HEROES OF THE NIGHT</title>
    </locale>
  </game>
  <game name=""PJ MASKS: HEROES OF THE NIGHT (EN,FR,DE,ES,IT,NL,PT,SE,DK,NO,FI,AR,ZHCN,RU,PL)"">
    <id>A22EB</id>
    <region>USA</region>
    <locale lang=""EN"">
      <title>PJ MASKS: HEROES OF THE NIGHT</title>
    </locale>
  </game>
  <game name=""Romancing SaGa Remastered (JA,KO,ZHCN,ZHTW)"">
    <id>AABBC</id>
    <region>KOR,TWN,CHN</region>
    <languages>JA,KO,ZHCN,ZHTW</languages>
    <locale lang=""EN"">
      <title>Romancing SaGa Remastered</title>
    </locale>
  </game>
</xml>";

            var results = Subject.ParseGameTdbXmlContent(switchPlatform, sampleXml, new HashSet<string>());

            results.Should().NotBeEmpty();

            var monsterFruit = results.First(g => g.NoIntroName.Contains("Monster Fruit Academy"));
            monsterFruit.RegionSeasonNumber.Should().Be(GameCatalogProxy.RegionWorldSeason);
            monsterFruit.Title.Should().Be("Monster Fruit Academy");

            var modelDebut = results.First(g => g.NoIntroName.Contains("MODEL Debut 2 #nicola"));
            modelDebut.RegionSeasonNumber.Should().Be(GameCatalogProxy.RegionJapanSeason);
            modelDebut.Title.Should().Be("MODEL Debut 2 #nicola");

            var pjMasksEur = results.First(g => g.NoIntroName.Contains("PJ MASKS") && g.RegionSeasonNumber == GameCatalogProxy.RegionEuropeSeason);
            pjMasksEur.RegionSeasonNumber.Should().Be(GameCatalogProxy.RegionEuropeSeason);

            var pjMasksUsa = results.First(g => g.NoIntroName.Contains("PJ MASKS") && g.RegionSeasonNumber == GameCatalogProxy.RegionUsaSeason);
            pjMasksUsa.RegionSeasonNumber.Should().Be(GameCatalogProxy.RegionUsaSeason);

            results.Where(g => g.Title == "PJ MASKS: HEROES OF THE NIGHT").Should().HaveCount(2);

            var romancingSaga = results.First(g => g.NoIntroName.Contains("Romancing SaGa Remastered"));
            romancingSaga.RegionSeasonNumber.Should().Be(GameCatalogProxy.RegionJapanSeason);

            GameCatalogProxy.ResolveCatalogRegionLabel("ALL", "JA", null, "Some Game (JA)").Should().Be("Japan");
            GameCatalogProxy.ResolveCatalogRegionLabel("PAL", "EN,FR,DE", null, null).Should().Be("Europe");
            GameCatalogProxy.ResolveCatalogRegionLabel("NTSC-U", "EN", null, null).Should().Be("USA");
        }

        [Test]
        public void should_preserve_titles_without_truncating_rev_or_numbers()
        {
            Subject.TryGetPlatformEntry(1014, out var switchPlatform);

            var revUp = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(switchPlatform, "Rev Up! RC Grand Prix (USA)", 2020, "Test");
            revUp.Should().NotBeNull();
            revUp.Title.Should().Be("Rev Up! RC Grand Prix");

            var alteredBeast = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(switchPlatform, "Altered Beast (USA)", 2018, "Test");
            alteredBeast.Should().NotBeNull();
            alteredBeast.Title.Should().Be("Altered Beast");

            var agent007 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(switchPlatform, "007: GoldenEye (USA)", 2023, "Test");
            agent007.Should().NotBeNull();
            agent007.Title.Should().Be("007: GoldenEye");

            var racing007 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(switchPlatform, "007 Racing (USA)", 2000, "Test");
            racing007.Should().NotBeNull();
            racing007.Title.Should().Be("007 Racing");
        }

        [Test]
        public void should_populate_multi_disc_game_with_disc_1_as_primary_and_all_discs_monitored()
        {
            Subject.TryGetPlatformEntry(1016, out var segaCdPlatform);
            segaCdPlatform.Should().NotBeNull();

            var disc1 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "Night Trap (USA) (Disc 1)", 1992, "Test");
            var disc2 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "Night Trap (USA) (Disc 2)", 1992, "Test");

            disc1.Should().NotBeNull();
            disc2.Should().NotBeNull();

            disc1.BaseTitle.Should().Be("Night Trap");
            disc2.BaseTitle.Should().Be("Night Trap");
            disc1.RevisionTag.Should().Be("Disc 1");
            disc2.RevisionTag.Should().Be("Disc 2");

            var list = new List<GameCatalogEntry> { disc1, disc2 };

            // When building the episodes for Sega CD with multi-disc game
            var cachedDatEntries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry { GameName = "Night Trap (USA) (Disc 1)", RomName = "Night Trap (USA) (Disc 1).chd", Region = "USA" },
                new NzbDrone.Core.Dat.DatEntry { GameName = "Night Trap (USA) (Disc 2)", RomName = "Night Trap (USA) (Disc 2).chd", Region = "USA" }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(d => d.GetEntriesForPlatform(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<string>()))
                .Returns(cachedDatEntries);

            Subject.InvalidatePlatformCache(1016);
            var result = Subject.GetSeriesInfo(1016, Language.English, "notEncrypted");

            var nightTrapEpisodes = result.Item2.Where(e => e.Title == "Night Trap").ToList();
            nightTrapEpisodes.Should().HaveCount(2);

            var epDisc1 = nightTrapEpisodes.First(e => e.FinaleType == "Disc 1");
            var epDisc2 = nightTrapEpisodes.First(e => e.FinaleType == "Disc 2");

            // Both discs must be monitored on initial add
            epDisc1.Monitored.Should().BeTrue();
            epDisc2.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_identify_update_version_and_sort_order()
        {
            GameCatalogProxy.IsUpdateVersion("Update v1.2").Should().BeTrue();
            GameCatalogProxy.IsUpdateVersion("Retail (v1.0)").Should().BeFalse();
            GameCatalogProxy.GetRevisionSortOrder("Update v1.1").Should().BeLessThan(GameCatalogProxy.GetRevisionSortOrder("Update v1.2"));
            GameCatalogProxy.GetRevisionSortOrder("Update v1.2.0").Should().BeLessThan(GameCatalogProxy.GetRevisionSortOrder("Update v1.2.1"));
        }

        [Test]
        public void should_pair_update_with_base_game_and_bundle_with_base_title()
        {
            Subject.TryGetPlatformEntry(1040, out var n3dsPlatform);
            if (n3dsPlatform == null)
            {
                Subject.TryGetPlatformEntry(1011, out n3dsPlatform);
            }

            n3dsPlatform.Should().NotBeNull();

            var baseEntry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(n3dsPlatform, "Pokemon Moon (USA)", 2016, "Test");
            var updateEntry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(n3dsPlatform, "Pokemon Moon (USA) (Update v1.2)", 2017, "Test");

            baseEntry.Should().NotBeNull();
            updateEntry.Should().NotBeNull();

            baseEntry.BaseTitle.Should().Be("Pokemon Moon");
            updateEntry.BaseTitle.Should().Be("Pokemon Moon");
            baseEntry.RevisionTag.Should().Be("Retail (v1.0)");
            updateEntry.RevisionTag.Should().Be("Update v1.2");
        }

        [Test]
        public void should_reject_invalid_game_titles_composed_only_of_language_and_region_tags()
        {
            GameCatalogProxy.IsValidGameTitle("(EN) (WORLD)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("(EN)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("(WORLD)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("(USA)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("[EUR]").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Super Mario Odyssey (World)").Should().BeTrue();
        }

        [Test]
        public void should_clean_scene_release_titles_for_switch()
        {
            GameCatalogProxy.CleanSceneReleaseTitle("Taiko_no_Tatsujin_Gesshoku_no_Kishi_JAP_NSW-Caravan.xci")
                .Should().Be("Taiko no Tatsujin Gesshoku no Kishi");
            GameCatalogProxy.CleanSceneReleaseTitle("Lapis_x_Labyrinth_Limited_Edition_NSW-SUXXORS")
                .Should().Be("Lapis x Labyrinth Limited Edition");
        }
    }
}
