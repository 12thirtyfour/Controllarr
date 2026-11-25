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
            results.Where(r => r.TitleSlug != "pokemonmini").All(r => r.TvdbId >= 100000).Should().BeTrue();
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
            results.First().Seasons.Should().HaveCount(9);
        }

        [Test]
        public void should_return_platform_and_regional_games_from_get_series_info()
        {
            var tuple = Subject.GetSeriesInfo(1011, Language.English, null);

            tuple.Item1.Title.Should().Be("Nintendo Game Boy Advance");
            tuple.Item1.Seasons.Should().NotBeEmpty();
            tuple.Item1.Seasons.Should().OnlyContain(s => tuple.Item2.Any(e => e.SeasonNumber == s.SeasonNumber));
            tuple.Item2.Should().NotBeEmpty();
            tuple.Item2.Should().Contain(e => e.Title.Contains("Pokemon - Emerald Version") && e.SeasonNumber == GameCatalogProxy.RegionUsa);
        }

        [Test]
        public void should_resolve_parent_platform_when_get_series_info_called_with_game_id()
        {
            var searchResults = Subject.SearchForNewSeries("Pokemon Emerald", Language.English);
            var emerald = searchResults.First(r => r.Title.Contains("Pokemon - Emerald Version"));

            var tuple = Subject.GetSeriesInfo(emerald.TvdbId, Language.English, null);

            tuple.Item1.Title.Should().Be("Nintendo Game Boy Advance");
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

            var usaEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionUsa).ToList();
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros.");
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros." && e.FinaleType == "Rev 1");
            usaEpisodes.Should().Contain(e => e.Title == "Super Mario Bros." && e.FinaleType == "Beta");

            var unlicensedEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionUnlicensed).ToList();
            unlicensedEpisodes.Should().Contain(e => e.Title.Contains("Bible Adventures"));

            var homebrewEpisodes = tuple.Item2.Where(e => e.SeasonNumber == GameCatalogProxy.RegionHomebrew).ToList();
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

            tuple.Item2.Where(e => e.Title == "EarthBound" && e.SeasonNumber == GameCatalogProxy.RegionUsa)
                .Should().ContainSingle();
            tuple.Item2.Where(e => e.Title == "Terranigma" && e.SeasonNumber == GameCatalogProxy.RegionEurope)
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
                .Setup(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.FullUri.Contains("Darksoft%20Neo%20Geo%20SMDB.txt") || r.Url.FullUri.Contains("NeoSD%20SMDB.txt"))))
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

            // 4. Standalone Arcade platform is not present
            Subject.GetAllPlatforms(Language.English).Should().NotContain(p => p.TitleSlug == "arcade");
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
            game.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);
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
            monsterFruit.RegionNumber.Should().Be(GameCatalogProxy.RegionWorld);
            monsterFruit.Title.Should().Be("Monster Fruit Academy");

            var modelDebut = results.First(g => g.NoIntroName.Contains("MODEL Debut 2 #nicola"));
            modelDebut.RegionNumber.Should().Be(GameCatalogProxy.RegionJapan);
            modelDebut.Title.Should().Be("MODEL Debut 2 #nicola");

            var pjMasksEur = results.First(g => g.NoIntroName.Contains("PJ MASKS") && g.RegionNumber == GameCatalogProxy.RegionEurope);
            pjMasksEur.RegionNumber.Should().Be(GameCatalogProxy.RegionEurope);

            var pjMasksUsa = results.First(g => g.NoIntroName.Contains("PJ MASKS") && g.RegionNumber == GameCatalogProxy.RegionUsa);
            pjMasksUsa.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);

            results.Where(g => g.Title == "PJ MASKS: HEROES OF THE NIGHT").Should().HaveCount(2);

            var romancingSaga = results.First(g => g.NoIntroName.Contains("Romancing SaGa Remastered"));
            romancingSaga.RegionNumber.Should().Be(GameCatalogProxy.RegionJapan);

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
            GameCatalogProxy.IsUpdateVersion("TU1").Should().BeTrue();
            GameCatalogProxy.IsUpdateVersion("Title Update 2").Should().BeTrue();
            GameCatalogProxy.IsPatchUpdateVersion("TU1").Should().BeTrue();
            GameCatalogProxy.IsPatchUpdateVersion("Title Update 2").Should().BeTrue();
            GameCatalogProxy.IsPatchUpdateVersion("DLC").Should().BeFalse();
            GameCatalogProxy.IsUpdateVersion("Retail (v1.0)").Should().BeFalse();
            GameCatalogProxy.GetRevisionSortOrder("Update v1.1").Should().BeLessThan(GameCatalogProxy.GetRevisionSortOrder("Update v1.2"));
            GameCatalogProxy.GetRevisionSortOrder("Update v1.2.0").Should().BeLessThan(GameCatalogProxy.GetRevisionSortOrder("Update v1.2.1"));
            GameCatalogProxy.GetRevisionSortOrder("TU2").Should().BeLessThan(GameCatalogProxy.GetRevisionSortOrder("TU10"));
            GameCatalogProxy.GetRevisionSortOrder("Title Update 2").Should().BeLessThan(GameCatalogProxy.GetRevisionSortOrder("Title Update 10"));
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
        public void should_deduplicate_identical_hashes_with_scrambled_names_while_preserving_distinct_hashes()
        {
            GameCatalogProxy.HasMatchingHash(
                null,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                null,
                "12345678",
                new GameCatalogEntry
                {
                    ExpectedSha1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
                    ExpectedCrc32 = "12345678"
                }).Should().BeTrue();

            // Even if CRC32 collides, conflicting stronger hashes (SHA1) must NOT match
            GameCatalogProxy.HasMatchingHash(
                null,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                null,
                "12345678",
                new GameCatalogEntry
                {
                    ExpectedSha1 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                    ExpectedCrc32 = "12345678"
                }).Should().BeFalse();
        }

        [Test]
        public void should_reject_audio_files_soundtracks_and_secondary_tracks()
        {
            GameCatalogProxy.IsValidGameTitle("03,04)MJ'sMoonwalker-SmoothCriminal").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("03TurtleVillage2").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("04FiendsPath").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("01Wilderness").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("05DeathAdder").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("05)SmoothCriminal-Solo(loop)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("02TurtleVillage1.wav").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Track 01").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Track 02").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Audio Track 2").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Data Track 1").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Sonic CD (USA) (Track 02)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Sonic CD (USA) (Track 2)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Sonic CD (USA) (Track 10)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Sonic CD (USA) [Track 03]").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("MD+ Moonwalker (Thriller).cue").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Golden Axe (loop)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Battle (noloopneeded)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Lords of Thunder (USA) (Track 08).wav").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("HideTheBaby_JAP 2.mp3").Should().BeFalse();
        }

        [Test]
        public void should_accept_valid_game_titles_with_numbers_and_hyphens()
        {
            GameCatalogProxy.IsValidGameTitle("007 - GoldenEye (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("007: The World Is Not Enough (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("10-Yard Fight (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("1942 (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("240p Test Suite (World)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("24K 4-in-1 (Asia)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("16t (Japan)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("3-D Tetris (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("7th Saga, The (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("Sonic CD (USA)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("Final Fantasy VII (USA) (Disc 1)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("Final Fantasy VII (USA) (Disc 2)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("Resident Evil 2 (USA) (Disc 1) (Leon)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("Resident Evil 2 (USA) (Disc 2) (Claire)").Should().BeTrue();
            GameCatalogProxy.IsValidGameTitle("R4 - Ridge Racer Type 4 (USA)").Should().BeTrue();
        }

        [Test]
        public void should_strip_track_one_from_game_and_rom_titles()
        {
            GameCatalogProxy.StripTrackOne("Sonic CD (USA) (Track 01)").Should().Be("Sonic CD (USA)");
            GameCatalogProxy.StripTrackOne("Sonic CD (USA) (Track 1)").Should().Be("Sonic CD (USA)");
            GameCatalogProxy.StripTrackOne("Sonic CD (USA) [Track 01]").Should().Be("Sonic CD (USA)");
            GameCatalogProxy.StripTrackOne("3x3 Eyes - Sanjiyan Henjou (Japan) (Rev 1) (Track 1)").Should().Be("3x3 Eyes - Sanjiyan Henjou (Japan) (Rev 1)");
        }

        [Test]
        public void should_clean_scene_release_titles_for_switch()
        {
            GameCatalogProxy.CleanSceneReleaseTitle("Taiko_no_Tatsujin_Gesshoku_no_Kishi_JAP_NSW-Caravan.xci")
                .Should().Be("Taiko no Tatsujin Gesshoku no Kishi");
            GameCatalogProxy.CleanSceneReleaseTitle("Lapis_x_Labyrinth_Limited_Edition_NSW-SUXXORS")
                .Should().Be("Lapis x Labyrinth Limited Edition");
        }

        [Test]
        public void should_classify_gameboy_rom_hacks_and_enhancements_into_rom_hacks_season()
        {
            Subject.TryGetPlatformEntry(1009, out var gbPlatform);
            gbPlatform.Should().NotBeNull();

            var hack1 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Super Mario Land (World) (DX)", 1989, "SmokeMonster HTGDB / SMDB");
            var hack2 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Metroid II - Return of Samus (USA) (Color)", 1991, "SmokeMonster HTGDB / SMDB");
            var hack3 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Red (USA) (Colorized)", 1996, "SmokeMonster HTGDB / SMDB");
            var hack4 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "The Legend of Zelda - Link's Awakening (USA) (Enhancement)", 1993, "SmokeMonster HTGDB / SMDB");
            var hack5 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Mega Man - Dr. Wily's Revenge (USA) (Patch)", 1991, "SmokeMonster HTGDB / SMDB");
            var hack6 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Castlevania - The Adventure (USA) (Fix)", 1989, "SmokeMonster HTGDB / SMDB");
            var hack7 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Donkey Kong Land (USA) (SGB Enhanced)", 1995, "SmokeMonster HTGDB / SMDB");

            hack1.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            hack2.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            hack3.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            hack4.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            hack5.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            hack6.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            hack7.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);

            var official1 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Super Mario Land (World)", 1989, "No-Intro");
            var official2 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Metroid II - Return of Samus (USA)", 1991, "No-Intro");

            official1.RegionNumber.Should().Be(GameCatalogProxy.RegionWorld);
            official2.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);
        }

        [Test]
        public void should_classify_pokemon_brown_and_famous_hacks_into_rom_hacks_season()
        {
            Subject.TryGetPlatformEntry(1009, out var gbPlatform);
            gbPlatform.Should().NotBeNull();

            var brown1 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon - Brown Version (USA)", 1998, "No-Intro / Redump DAT");
            var brown2 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Brown (USA)", 1998, "Cross-Referenced DAT / SMDB Catalog");
            var brown3 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Brown", 1998, "Test");
            var brown4 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Brown 2014 [Hack by Koolboyman]", 2014, "ROM Hacks & Translations");
            var prism = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Prism (USA)", 2016, "SmokeMonster HTGDB / SMDB");
            var ashGray = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Ash Gray (USA)", 2012, "Test");
            var radicalRed = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Radical Red (USA)", 2020, "Test");
            var unbound = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Unbound (USA)", 2022, "Test");
            var emeraldRogue = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon Emerald Rogue (USA)", 2023, "Test");
            var dinoLand = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Super Mario World - Return to Dinosaur Land (USA)", 1995, "Test");

            brown1.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            brown2.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            brown3.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            brown4.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            prism.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            ashGray.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            radicalRed.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            unbound.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            emeraldRogue.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);
            dinoLand.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);

            // Official Pokémon releases must NOT be classified as ROM hacks
            var officialRed = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon - Red Version (USA, Europe)", 1998, "No-Intro");
            var officialEmerald = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Pokemon - Emerald Version (USA, Europe)", 2005, "No-Intro");

            officialRed.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);
            officialEmerald.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);

            // ParsedRomInfo should also report Season 6 for ROM hacks
            var parsedBrown = Parser.RomParser.ParseRomName("Pokemon Brown (USA).gb");
            parsedBrown.RegionNumber.Should().Be(GameCatalogProxy.RegionRomHacks);

            var parsedOfficial = Parser.RomParser.ParseRomName("Pokemon - Red Version (USA, Europe).gb");
            parsedOfficial.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);
        }

        [Test]
        public void should_isolate_bios_files_and_reject_from_regular_game_library()
        {
            Subject.TryGetPlatformEntry(1013, out var segaCdPlatform);
            segaCdPlatform.Should().NotBeNull();

            // IsValidGameTitle must reject BIOS titles
            GameCatalogProxy.IsValidGameTitle("[BIOS] Sega CD (USA) (v1.10)").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("bios_CD_U.bin").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("disksys.rom").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("cd_bios.rom").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Famicom Disk System (Japan) (Boot ROM)").Should().BeFalse();

            // CreateCatalogEntryFromNoIntroName must return null for BIOS dumps
            var biosEntry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "[BIOS] Sega CD (USA) (v1.10)", 1992, "No-Intro");
            biosEntry.Should().BeNull();

            // Canonical region folder resolution must map BIOS files to "BIOS"
            GameCatalogProxy.ResolveCanonicalRegionFolderForRom("BIOS/bios_CD_U.bin", "bios_CD_U.bin").Should().Be("BIOS");
            GameCatalogProxy.ResolveCanonicalRegionFolderForRom("EverDrive/BIOS/Mega-CD 1 EU.bin", "Mega-CD 1 EU.bin").Should().Be("BIOS");

            // RegionHelper.IsBios checks
            NzbDrone.Core.Games.RegionHelper.IsBios("[BIOS] Sega CD (USA)").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("bios_CD_U.bin").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("disksys.rom").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("cd_bios.rom").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("boot.ipl").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("Sonic CD (USA)").Should().BeFalse();
        }

        [Test]
        public void should_classify_tg16_and_short_region_codes_into_correct_regions()
        {
            Subject.TryGetPlatformEntry(1020, out var tg16Platform).Should().BeTrue();

            var jpShort = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(tg16Platform, "15-in-1 Mega Collection - Backtracking Ten Years (J)", 1990, "SmokeMonster HTGDB / SMDB");
            jpShort.Should().NotBeNull();
            jpShort.RegionNumber.Should().Be(GameCatalogProxy.RegionJapan);

            var euShort = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(tg16Platform, "Sample Game (E)", 1990, "SmokeMonster HTGDB / SMDB");
            euShort.Should().NotBeNull();
            euShort.RegionNumber.Should().Be(GameCatalogProxy.RegionEurope);

            var worldShort = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(tg16Platform, "Sample Game (W)", 1990, "SmokeMonster HTGDB / SMDB");
            worldShort.Should().NotBeNull();
            worldShort.RegionNumber.Should().Be(GameCatalogProxy.RegionWorld);

            var usaShort = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(tg16Platform, "Sample Game (U)", 1990, "SmokeMonster HTGDB / SMDB");
            usaShort.Should().NotBeNull();
            usaShort.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);

            var pcEnginePath = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(tg16Platform, "Untagged PC Engine Game", 1990, "SmokeMonster HTGDB / SMDB | Turbo EverDrive/2 PC Engine - A-M/Untagged PC Engine Game.pce");
            pcEnginePath.Should().NotBeNull();
            pcEnginePath.RegionNumber.Should().Be(GameCatalogProxy.RegionJapan);

            var superGrafxPath = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(tg16Platform, "Untagged SuperGrafx Game", 1990, "SmokeMonster HTGDB / SMDB | Turbo EverDrive/3 SuperGrafx/Untagged SuperGrafx Game.sgx");
            superGrafxPath.Should().NotBeNull();
            superGrafxPath.RegionNumber.Should().Be(GameCatalogProxy.RegionJapan);
        }

        [Test]
        public void should_return_all_supported_platforms_with_unique_identifiers_for_bulk_add()
        {
            var platforms = Subject.GetAllPlatforms(Language.English);

            platforms.Should().HaveCount(91);
            platforms.Select(p => p.TvdbId).Should().OnlyHaveUniqueItems();
            platforms.Select(p => p.Title).Should().OnlyHaveUniqueItems();
            platforms.Select(p => p.TitleSlug).Should().OnlyHaveUniqueItems();
            platforms.Should().OnlyContain(p => p.TvdbId > 0 && !string.IsNullOrWhiteSpace(p.Title) && !string.IsNullOrWhiteSpace(p.TitleSlug));
            platforms.Should().OnlyContain(p => p.Seasons != null && p.Seasons.Count > 0);
            platforms.Select(p => p.TitleSlug).Should().Contain(new[]
            {
                "3do", "atari5200", "colecovision", "intellivision", "vectrex",
                "pcfx", "pokemonmini", "odyssey2", "channelf", "cdi"
            });
        }

        [Test]
        public void should_classify_240p_test_suite_as_test_and_utilities_and_underscore_regions_correctly()
        {
            Subject.TryGetPlatformEntry(1022, out var segaCdPlatform).Should().BeTrue();

            var megaCd240pEu = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "240p_MegaCD_EU", 2014, "Redump");
            megaCd240pEu.Should().NotBeNull();
            megaCd240pEu.RegionNumber.Should().Be(GameCatalogProxy.RegionTestAndUtilities);

            var suite240p = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "240p Test Suite (USA)", 2014, "Redump");
            suite240p.Should().NotBeNull();
            suite240p.RegionNumber.Should().Be(GameCatalogProxy.RegionTestAndUtilities);

            var euUnderscore = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "Sample_Game_EU", 1993, "Redump");
            euUnderscore.Should().NotBeNull();
            euUnderscore.RegionNumber.Should().Be(GameCatalogProxy.RegionEurope);

            var jpUnderscore = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "Sample_Game_JP", 1993, "Redump");
            jpUnderscore.Should().NotBeNull();
            jpUnderscore.RegionNumber.Should().Be(GameCatalogProxy.RegionJapan);
        }

        [Test]
        public void should_reject_raw_title_ids_and_eboot_and_normalize_abbreviations()
        {
            GameCatalogProxy.IsValidGameTitle("00000000TU_1C421UH_201QS00000000.0000000G0EN80").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("000500001f940e00").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("EBoot").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("1ST_READ.BIN").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("Halo 3 (USA)").Should().BeTrue();

            GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Donkey Kong Jr")
                .Should().Be(GameCatalogProxy.NormalizeCanonicalCrossRefTitle("Donkey Kong Junior"));

            Subject.TryGetPlatformEntry(1038, out var jaguarPlatform).Should().BeTrue();
            var gdEntry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(jaguarPlatform, "Sample Homebrew (USA) (GD)", 2020, "SmokeMonster");
            gdEntry.Should().NotBeNull();
            gdEntry.RegionNumber.Should().Be(GameCatalogProxy.RegionHomebrew);
        }

        [Test]
        public void should_filter_wiiu_xbox360_artifacts_and_fds_virtual_console()
        {
            GameCatalogProxy.IsValidGameTitle("00050000-1010EC00").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("00000001.app").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("title.tmd").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("58410A5A").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("TU_10L621R_000000C0").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("tu00000001_00000000").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("21271645TU_12K225K_000000O000000.00000000000O6").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("213B2D4CTU_12K21US_0000004000000.0000000000081").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("4d5307e6_00000000").Should().BeFalse();
            GameCatalogProxy.IsValidGameTitle("$SystemUpdate").Should().BeFalse();

            Subject.TryGetPlatformEntry(1002, out var fdsPlatform).Should().BeTrue();
            GameCatalogProxy.CreateCatalogEntryFromNoIntroName(fdsPlatform, "Super Mario Bros. (Japan) (Wii Virtual Console)", 1986, "No-Intro")
                .Should().BeNull();
            GameCatalogProxy.CreateCatalogEntryFromNoIntroName(fdsPlatform, "Metroid (Japan) (3DS Virtual Console)", 1986, "No-Intro")
                .Should().BeNull();
            var nativeFds = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(fdsPlatform, "Metroid (Japan)", 1986, "No-Intro");
            nativeFds.Should().NotBeNull();
            nativeFds.Title.Should().Be("Metroid");
        }

        [Test]
        public void should_filter_virtual_boy_smdb_non_games_and_strip_pad_suffix()
        {
            Subject.TryGetPlatformEntry(1027, out var vbPlatform).Should().BeTrue();
            var smdbContent = string.Join("\n", new[]
            {
                "348a85b1030056edb91b9411da8df01847f787e813be803efd315d3520849fc9\tVirtual Boy/1 USA - A-Z/3-D Tetris (USA)_pad.vb\t08a1ce0ea0337a3b91e890d4423b1b3501a0e20d\t7e8014d0d080b80b2eaf77da8894efa4\tb45b10b3\t2097152",
                "1111111111111111111111111111111111111111111111111111111111111111\tVirtual Boy/unpadded/1 USA - A-Z/3-D Tetris (USA).vb\t1111111111111111111111111111111111111111\t11111111111111111111111111111111\tbb71b522\t1048576",
                "2222222222222222222222222222222222222222222222222222222222222222\tVirtual Boy/4 Game Boy Emulator/Legend of Zelda - Link's Awakening.vb\t2222222222222222222222222222222222222222\t22222222222222222222222222222222\t12345678\t1048576",
                "3333333333333333333333333333333333333333333333333333333333333333\tVirtual Boy/4 Game Boy Emulator/Scripts/1942 (USA, Europe).gbc\t3333333333333333333333333333333333333333\t33333333333333333333333333333333\t23456789\t524288",
                "4444444444444444444444444444444444444444444444444444444444444444\tVirtual Boy/5 Tools & Service Test Carts/VB Sound Tester (PD) v1 Frostgiant.vb\t4444444444444444444444444444444444444444\t44444444444444444444444444444444\t25ee69f2\t1048576",
                "5555555555555555555555555555555555555555555555555555555555555555\tVirtual Boy/unpadded/4 Hacks/InsmEdit/msvbvm50.dll\t5555555555555555555555555555555555555555\t55555555555555555555555555555555\t3456789a\t1347344",
                "6666666666666666666666666666666666666666666666666666666666666666\tVirtual Boy/unpadded/4 Homebrew/Demos & Intros/Hello, World! Demo V1.1 by Amos Bieler (PD).vb\t6666666666666666666666666666666666666666\t66666666666666666666666666666666\t456789ab\t1048576"
            });

            var method = typeof(GameCatalogProxy).GetMethod(
                "ParseSmokeMonsterSmdbContent",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var entries = (List<GameCatalogEntry>)method.Invoke(
                Subject,
                new object[] { vbPlatform, smdbContent, null, seenNames, false, null });

            entries.Should().HaveCount(2);
            entries.Should().Contain(e => e.Title == "3-D Tetris" && e.NoIntroName == "3-D Tetris (USA)" && e.RegionNumber == GameCatalogProxy.RegionUsa);
            entries.Should().Contain(e => e.NoIntroName.Contains("VB Sound Tester") && e.RegionNumber == GameCatalogProxy.RegionTestAndUtilities);
        }

        [Test]
        public void should_resolve_game_release_year_via_crc_and_normalized_title_in_libretro_dat()
        {
            Subject.TryGetPlatformEntry(1027, out var vbPlatform).Should().BeTrue();
            var datContent = string.Join("\n", new[]
            {
                "game (",
                "\tname \"3-D Tetris (USA)\"",
                "\trom ( name \"3-D Tetris (USA) (Rev 1).vb\" size 1048576 crc BB71B522 )",
                ")",
                "game (",
                "\tname \"Innsmouth no Yakata (Japan)\"",
                "\trom ( name \"Innsmouth no Yakata (Japan).vb\" size 1048576 crc 83CB6A00 )",
                ")"
            });

            var releaseDateInfoType = typeof(GameCatalogProxy).GetNestedType("ReleaseDateInfo", System.Reflection.BindingFlags.NonPublic);
            releaseDateInfoType.Should().NotBeNull();
            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), releaseDateInfoType);
            var lookup = System.Activator.CreateInstance(dictType, System.StringComparer.OrdinalIgnoreCase);

            var info1996 = System.Activator.CreateInstance(releaseDateInfoType);
            releaseDateInfoType.GetProperty("Year").SetValue(info1996, 1996);
            releaseDateInfoType.GetProperty("ReleaseDate").SetValue(info1996, "1996");
            dictType.GetMethod("set_Item").Invoke(lookup, new[] { "3-D Tetris", info1996 });

            var info1996Crc = System.Activator.CreateInstance(releaseDateInfoType);
            releaseDateInfoType.GetProperty("Year").SetValue(info1996Crc, 1996);
            releaseDateInfoType.GetProperty("ReleaseDate").SetValue(info1996Crc, "1996");
            dictType.GetMethod("set_Item").Invoke(lookup, new[] { "crc:83CB6A00", info1996Crc });

            var parseMethod = typeof(GameCatalogProxy).GetMethod(
                "ParseLibretroDatContent",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            parseMethod.Should().NotBeNull();

            var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var entries = (List<GameCatalogEntry>)parseMethod.Invoke(
                null,
                new object[] { vbPlatform, datContent, "No-Intro", lookup, seenNames, null });

            entries.Should().HaveCount(2);
            entries[0].ReleaseYear.Should().Be(1996);
            entries[0].ReleaseDateString.Should().Be("1996");
            entries[1].ReleaseYear.Should().Be(1996);
            entries[1].ReleaseDateString.Should().Be("1996");
        }

        [Test]
        public void should_deduplicate_romhacks_across_libretro_dat_and_smdb_by_hash()
        {
            Subject.TryGetPlatformEntry(1010, out var snesPlatform).Should().BeTrue();
            var datContent = string.Join("\n", new[]
            {
                "game (",
                "\tname \"Chrono Trigger - Crimson Echoes [Hack by Kajar Laboratories]\"",
                "\trom ( name \"Chrono Trigger (USA).smc\" size 6291968 crc 2EBCEB79 md5 24955542c1ea9c5bbd21a07479e64047 sha1 a43632026948df3d288f0bb03f6c83ee8e76bdd5 )",
                ")"
            });

            var smdbContent = string.Join("\n", new[]
            {
                "d9fdfc05640522d39e1bb5ff4d082a8a93b9e7817fe6632bebe03303091d7d0c\tSD2SNES/4 Hacks/Hacks - A-R/Chrono Trigger Crimson Echoes (Hack) Kajar Laboratories.sfc\ta43632026948df3d288f0bb03f6c83ee8e76bdd5\t24955542c1ea9c5bbd21a07479e64047\t2ebceb79\t6291968",
                "d9fdfc05640522d39e1bb5ff4d082a8a93b9e7817fe6632bebe03303091d7d0c\tSD2SNES/4 Hacks/Selections/Chrono Trigger Crimson Echoes (Hack) Kajar Laboratories.sfc\ta43632026948df3d288f0bb03f6c83ee8e76bdd5\t24955542c1ea9c5bbd21a07479e64047\t2ebceb79\t6291968",
                "c28ab3ff76060214724e5ce1b8daafcb1eb49c4c72fd6f1258e04dde19d0208d\tSD2SNES/4 Hacks/Hacks - S-Z/Super Metroid Hacks/SM Ancient Chozo (Hack) v1.1 Albert V.sfc\tfa7328f225cf1ffa6d1f34487fb66e63c03ad944\ta817bc339d87dbb68fb03dae8decbcd9\t0f3890f5\t3244032"
            });

            var parseDat = typeof(GameCatalogProxy).GetMethod(
                "ParseLibretroDatContent",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var parseSmdb = typeof(GameCatalogProxy).GetMethod(
                "ParseSmokeMonsterSmdbContent",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            parseDat.Should().NotBeNull();
            parseSmdb.Should().NotBeNull();

            var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var seenHashes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            var datEntries = (List<GameCatalogEntry>)parseDat.Invoke(
                null,
                new object[] { snesPlatform, datContent, "ROM Hacks & Translations", null, seenNames, seenHashes });
            var smdbEntries = (List<GameCatalogEntry>)parseSmdb.Invoke(
                Subject,
                new object[] { snesPlatform, smdbContent, null, seenNames, false, seenHashes });

            datEntries.Should().HaveCount(1);
            smdbEntries.Should().HaveCount(1);
            smdbEntries[0].ExpectedSha1.Should().Be("fa7328f225cf1ffa6d1f34487fb66e63c03ad944");
        }

        [Test]
        public void should_filter_neogeo_bios_files_from_game_regions()
        {
            NzbDrone.Core.Games.RegionHelper.IsBios("neogeo.zip").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("uni-bios_4_0.rom").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("sp-s2.sp1").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("asia-s3.rom").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("neocd.bin").Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsBios("000-lo.lo").Should().BeTrue();

            Subject.TryGetPlatformEntryBySlug("neogeo", out var neoGeoPlatform).Should().BeTrue();
            var datContent = string.Join("\n", new[]
            {
                "game (",
                "\tname \"neogeo\"",
                "\trom ( name \"neogeo.zip\" size 1400000 crc 12345678 )",
                ")",
                "game (",
                "\tname \"uni-bios_4_0.rom\"",
                "\trom ( name \"uni-bios_4_0.rom\" size 131072 crc 23456789 )",
                ")",
                "game (",
                "\tname \"Metal Slug (World)\"",
                "\trom ( name \"mslug.neo\" size 12582912 crc 3456789A )",
                ")"
            });

            var parseDat = typeof(GameCatalogProxy).GetMethod(
                "ParseLibretroDatContent",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            parseDat.Should().NotBeNull();

            var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var seenHashes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var entries = (List<GameCatalogEntry>)parseDat.Invoke(
                null,
                new object[] { neoGeoPlatform, datContent, "FBNeo / Neo Geo AES", null, seenNames, seenHashes });

            entries.Should().HaveCount(1);
            entries[0].Title.Should().Be("Metal Slug");
        }

        [TestCase("21CDDF46tu00000004_00000000")]
        [TestCase("435907D5000B0000tu00000001_00000000")]
        [TestCase("4541082900000001Foo Fighters - The Colour and the Shape")]
        [TestCase("4541086600000001GF2-DE-452861460830263780771277")]
        [TestCase("4541091400000001Quiet_Company_-_It's_Better_to_Spend_Money_Like_There's_No_Tomorrow_Than_Spend_Tonight_Like_There's_No_Money")]
        [TestCase("4B5907DC000000024FADBY4C65EKZ20CFFOR66F360D32ISOE174DA874B")]
        [TestCase("4FC9256Ftu00000001_00000000")]
        public void should_reject_xbox360_title_updates_and_dlc_content_ids(string title)
        {
            GameCatalogProxy.IsValidGameTitle(title).Should().BeFalse();
        }

        [Test]
        public void should_fallback_to_rom_name_when_cached_dat_entry_has_prose_description_as_game_name()
        {
            Subject.TryGetPlatformEntryBySlug("wiiu", out var wiiUPlatform).Should().BeTrue();

            var datFile = new NzbDrone.Core.Dat.DatFile
            {
                Id = 9001,
                Name = "Nintendo - Nintendo Wii U (Wii U Catalog)",
                Category = "Nintendo - Wii U (Libretro)"
            };

            var datEntries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9001,
                    GameName = ",000 years ago, Emperor Geardo of the Naga Empire sent an army of machine soldiers to invade the sky kingdom of Garuda. Thanks to the...",
                    RomName = "Rodea the Sky Soldier (Japan).wux",
                    Sha1 = "1111111111111111111111111111111111111111"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9001,
                    GameName = "♯FE is a crossover role-playing video game developed by Atlus and published by Nintendo for the Wii U home console. The game is based on...",
                    RomName = "Gen'ei Ibunroku FE (Japan).wux",
                    Sha1 = "2222222222222222222222222222222222222222"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9001,
                    GameName = "13 is the Magic Number for Monster High! What if a lamp was more than it seemed? What if Whisp had...won? Monster High: 13 Wishes is a...",
                    RomName = "Monster High - 13 Wishes (USA).wux",
                    Sha1 = "3333333333333333333333333333333333333333"
                }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetAllDatFiles())
                .Returns(new List<NzbDrone.Core.Dat.DatFile> { datFile });

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetEntriesForPlatform(wiiUPlatform.Slug, wiiUPlatform.Name, wiiUPlatform.LibretroSystemName))
                .Returns(datEntries);

            var tuple = Subject.GetSeriesInfo(wiiUPlatform.TvdbId, Language.English, null);

            tuple.Item2.Should().Contain(e => e.Title == "Rodea the Sky Soldier");
            tuple.Item2.Should().Contain(e => e.Title == "Gen'ei Ibunroku FE");
            tuple.Item2.Should().Contain(e => e.Title == "Monster High - 13 Wishes");
            tuple.Item2.Should().NotContain(e => e.Title.Contains("Emperor Geardo"));
            tuple.Item2.Should().NotContain(e => e.Title.Contains("crossover role-playing"));
            tuple.Item2.Should().NotContain(e => e.Title.Contains("Magic Number for Monster High"));
        }

        [TestCase("320D Font Test (20190705)", true)]
        [TestCase("7800 Utility Cart (20161210)", true)]
        [TestCase("Diagnostic Test Cartridge (19xx) (Atari)", true)]
        [TestCase("Atari 7800 Development Card (USA)", true)]
        [TestCase("Mario Bros (Ice Stress Test) (20210315)", true)]
        [TestCase("Sprite Engine Test 2 (20210320)", true)]
        [TestCase("Font Check (20201202)", true)]
        [TestCase("Action Replay (USA) (Unl)", true)]
        [TestCase("GameShark Pro (USA) (v3.2)", true)]
        [TestCase("Test Drive (USA)", false)]
        [TestCase("Test Drive II - The Duel (USA)", false)]
        public void should_classify_utility_and_test_carts_into_test_and_utilities_while_allowing_real_games(string title, bool expectedTestOrUtility)
        {
            GameCatalogProxy.IsValidGameTitle(title).Should().BeTrue();
            NzbDrone.Core.Games.RegionHelper.IsTestOrUtility(title).Should().Be(expectedTestOrUtility);

            Subject.TryGetPlatformEntryBySlug("atari7800", out var platform).Should().BeTrue();
            var entry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(platform, title, 1990, "No-Intro");
            entry.Should().NotBeNull();
            entry.RegionNumber.Should().Be(expectedTestOrUtility ? GameCatalogProxy.RegionTestAndUtilities : GameCatalogProxy.RegionUsa);
        }

        [TestCase("CD-I World - Volume 1 - Issue 1 (USA)", true)]
        [TestCase("CD-Online - Issue 01 - Internet on TV (UK)", true)]
        [TestCase("1993 CD-i Awards (USA)", true)]
        [TestCase("ASICS Interactive Catalogue on CD-i 1995 (Germany)", true)]
        [TestCase("Bertelsmann Universallexikon (Germany)", true)]
        [TestCase("Accent Health - April 2003 50 + Silver CDi 0403 (USA)", true)]
        [TestCase("Bose - Home Cinema Sound System - Demonstration Disc (Europe)", true)]
        [TestCase("Hotel Mario (USA)", false)]
        [TestCase("Link - The Faces of Evil (USA)", false)]
        [TestCase("7th Guest, The (USA) (Disc 1) (The Game)", false)]
        public void should_classify_cdi_magazines_and_books_into_dedicated_region(string title, bool expectedMagazineOrBook)
        {
            NzbDrone.Core.Games.RegionHelper.IsMagazineOrBook(title).Should().Be(expectedMagazineOrBook);

            Subject.TryGetPlatformEntryBySlug("cdi", out var platform).Should().BeTrue();
            var entry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(platform, title, 1994, "Redump");
            entry.Should().NotBeNull();
            if (expectedMagazineOrBook)
            {
                entry.RegionNumber.Should().Be(GameCatalogProxy.RegionMagazinesAndBooks);
            }
            else
            {
                entry.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);
            }
        }

        [TestCase("Brantano Interactive (Belgium)")]
        [TestCase("Alpine Adventure (Austria)")]
        [TestCase("Swiss Alpine Challenge (Switzerland)")]
        public void should_classify_belgium_austria_and_switzerland_as_europe(string title)
        {
            Subject.TryGetPlatformEntryBySlug("cdi", out var platform).Should().BeTrue();
            var entry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(platform, title, 1995, "Redump");
            entry.Should().NotBeNull();
            entry.RegionNumber.Should().Be(GameCatalogProxy.RegionEurope);
        }

        [Test]
        public void should_strip_side_tags_for_multi_side_grouping()
        {
            GameCatalogProxy.DiscTagRegex.IsMatch("Sample Title (Side 1)").Should().BeTrue();
            GameCatalogProxy.DiscTagRegex.IsMatch("Sample Title (Side 2)").Should().BeTrue();
        }

        [Test]
        public void should_skip_smdb_demos_while_cataloging_tools_folders()
        {
            GameCatalogProxy.ShouldSkipSmdbPath("Atari 7800/4 Homebrew/Demos/3D Test Demo 1 (PAL).a78").Should().BeTrue();
            GameCatalogProxy.ShouldSkipSmdbPath("Atari 7800/5 Tools & Service Test Carts/320D Font Test (20190705).a78").Should().BeFalse();
            GameCatalogProxy.ShouldSkipSmdbPath("Atari 7800/1 NTSC A-Z/Food Fight (NTSC) (Atari) (1987).a78").Should().BeFalse();
            GameCatalogProxy.ShouldSkipSmdbPath("Atari Jaguar/2 Jaguar CD/Baldies.j64").Should().BeTrue();
            GameCatalogProxy.ShouldSkipSmdbPath("Atari Jaguar/5 Tools & Test Carts/Memory Test.j64").Should().BeFalse();
        }

        [Test]
        public void should_reclassify_jaguar_homebrew_and_atari_st_ports_out_of_retail()
        {
            Subject.TryGetPlatformEntryBySlug("jaguar", out var jaguarPlatform).Should().BeTrue();

            var noIntroDat = new NzbDrone.Core.Dat.DatFile
            {
                Id = 9101,
                Name = "Atari - Jaguar (No-Intro)",
                Category = "No-Intro"
            };
            var smdbDat = new NzbDrone.Core.Dat.DatFile
            {
                Id = 9102,
                Name = "Atari - Jaguar (SmokeMonster SMDB)",
                Category = "SmokeMonster HTGDB / SMDB"
            };

            var datEntries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9101,
                    GameName = "1943 (World)",
                    RomName = "1943 (World).j64",
                    Sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9101,
                    GameName = "Barbarian (World) (Atari ST)",
                    RomName = "Barbarian (World) (Atari ST).j64",
                    Sha1 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9102,
                    GameName = "1943 (World)",
                    RomName = "1943 (World).j64",
                    SmdbPath = "Atari Jaguar/4 Homebrew/1943 (World).j64",
                    Sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9102,
                    GameName = "Alien Syndrome (World)",
                    RomName = "Alien Syndrome (World).j64",
                    SmdbPath = "Atari Jaguar/3 Atari ST Ports/Alien Syndrome (World).j64",
                    Sha1 = "cccccccccccccccccccccccccccccccccccccccc"
                }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetAllDatFiles())
                .Returns(new List<NzbDrone.Core.Dat.DatFile> { noIntroDat, smdbDat });

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetEntriesForPlatform(jaguarPlatform.Slug, jaguarPlatform.Name, jaguarPlatform.LibretroSystemName))
                .Returns(datEntries);

            var tuple = Subject.GetSeriesInfo(jaguarPlatform.TvdbId, Language.English, null);

            tuple.Item2.Should().Contain(e => e.Title == "1943" && e.SeasonNumber == GameCatalogProxy.RegionHomebrew);
            tuple.Item2.Should().NotContain(e => e.Title == "1943" && e.SeasonNumber == GameCatalogProxy.RegionWorld);
            tuple.Item2.Should().Contain(e => e.Title == "Barbarian" && e.SeasonNumber == GameCatalogProxy.RegionHomebrew);
            tuple.Item2.Should().Contain(e => e.Title == "Alien Syndrome" && e.SeasonNumber == GameCatalogProxy.RegionHomebrew);
        }

        [Test]
        public void should_not_overwrite_vita_game_title_with_opaque_pkg_filename()
        {
            Subject.TryGetPlatformEntryBySlug("psvita", out var vitaPlatform).Should().BeTrue();

            var datContent = string.Join("\n", new[]
            {
                "game (",
                "\tname \"1001 Spikes (USA)\"",
                "\trom ( name \"oneMraZVABcmthdgwvoKVMXIqpXgyCrnKrRdTPfkpIZzAqJdfUONQAOsasQqRtua.pkg\" size 122689456 crc F0AD5A2D )",
                ")"
            });

            var parseDat = typeof(GameCatalogProxy).GetMethod(
                "ParseLibretroDatContent",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            parseDat.Should().NotBeNull();

            var seenNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var seenHashes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var entries = (List<GameCatalogEntry>)parseDat.Invoke(
                null,
                new object[] { vitaPlatform, datContent, "No-Intro", null, seenNames, seenHashes });

            entries.Should().ContainSingle();
            entries[0].Title.Should().Be("1001 Spikes");
            GameCatalogProxy.IsValidGameTitle("aAcNzoilEdpKajXJobEQWFfxbTmlGdxiQqQoptBjpxJEByDfWIOXfqeBERDNMMtvotJsMPOLazeSdTgqTyTaxELgKwhvxbstygkjl").Should().BeFalse();
        }

        [Test]
        public void should_pair_psvita_and_cross_platform_dlc_entries_with_base_games()
        {
            Subject.TryGetPlatformEntryBySlug("psvita", out var vitaPlatform).Should().BeTrue();

            var datFile = new NzbDrone.Core.Dat.DatFile
            {
                Id = 9002,
                Name = "Sony - PlayStation Vita (No-Intro)",
                Category = "No-Intro"
            };

            var datEntries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9002,
                    GameName = "AeternoBlade (USA)",
                    RomName = "AeternoBlade (USA).vpk",
                    Sha1 = "1111111111111111111111111111111111111111"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9002,
                    GameName = "AeternoBlade - Agent Costume (USA) (DLC)",
                    RomName = "AeternoBlade - Agent Costume (USA) (DLC).vpk",
                    Sha1 = "2222222222222222222222222222222222222222"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9002,
                    GameName = "Accel World vs. Sword Art Online - Millennium Twilight (Japan)",
                    RomName = "Accel World vs. Sword Art Online - Millennium Twilight (Japan).vpk",
                    Sha1 = "3333333333333333333333333333333333333333"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9002,
                    GameName = "Accel World vs. Sword Art Online - Millennium Twilight - Gunfire Equipment Gun Gale Online (Japan) (DLC)",
                    RomName = "Accel World vs. Sword Art Online - Millennium Twilight - Gunfire Equipment Gun Gale Online (Japan) (DLC).vpk",
                    Sha1 = "4444444444444444444444444444444444444444"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9002,
                    GameName = "Gravity Rush (USA)",
                    RomName = "Gravity Rush (USA).vpk",
                    Sha1 = "6666666666666666666666666666666666666666"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9002,
                    GameName = "Gravity Rush - Spy Pack (USA) (Add-on)",
                    RomName = "Gravity Rush - Spy Pack (USA) (Add-on).vpk",
                    Sha1 = "5555555555555555555555555555555555555555"
                }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetAllDatFiles())
                .Returns(new List<NzbDrone.Core.Dat.DatFile> { datFile });

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetEntriesForPlatform(vitaPlatform.Slug, vitaPlatform.Name, vitaPlatform.LibretroSystemName))
                .Returns(datEntries);

            var tuple = Subject.GetSeriesInfo(vitaPlatform.TvdbId, Language.English, null);

            var agentCostume = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("AeternoBlade - Agent Costume (USA) (DLC)"));
            agentCostume.Should().NotBeNull();
            agentCostume.Title.Should().Be("AeternoBlade");
            agentCostume.FinaleType.Should().Be("DLC • Agent Costume");

            var saoDlc = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Accel World vs. Sword Art Online - Millennium Twilight - Gunfire Equipment Gun Gale Online (Japan) (DLC)"));
            saoDlc.Should().NotBeNull();
            saoDlc.Title.Should().Be("Accel World vs. Sword Art Online - Millennium Twilight");
            saoDlc.FinaleType.Should().Be("DLC • Gunfire Equipment Gun Gale Online");

            var gravityDlc = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Gravity Rush - Spy Pack (USA) (Add-on)"));
            gravityDlc.Should().NotBeNull();
            gravityDlc.Title.Should().Be("Gravity Rush");
            gravityDlc.FinaleType.Should().Be("DLC • Spy Pack");
        }

        [Test]
        public void should_pair_ps_vita_subtitle_mismatch_unlock_key_theme_and_orphaned_dlc_entries()
        {
            Subject.InvalidatePlatformCache();
            Subject.TryGetPlatformEntry(1031, out var vitaPlatform).Should().BeTrue();

            var datFile = new NzbDrone.Core.Dat.DatFile
            {
                Id = 9003,
                Name = "Sony - PlayStation Vita",
                Category = "No-Intro"
            };

            var datEntries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Atelier Escha & Logy Plus - Alchemists of the Dusk Sky (Europe)",
                    RomName = "Atelier Escha & Logy Plus - Alchemists of the Dusk Sky (Europe).vpk",
                    Sha1 = "1111111111111111111111111111111111111112"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Atelier Escha & Logy Plus - Costumes - Happy Wedding & White Knight (Europe) (DLC)",
                    RomName = "Atelier Escha & Logy Plus - Costumes - Happy Wedding & White Knight (Europe) (DLC).vpk",
                    Sha1 = "2222222222222222222222222222222222222223"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Dead or Alive Xtreme 3 - Venus (Asia)",
                    RomName = "Dead or Alive Xtreme 3 - Venus (Asia).vpk",
                    Sha1 = "3333333333333333333333333333333333333334"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Dead or Alive Xtreme 3 - JP0106-PCSG00771_00-00DLC00210000000 (Asia) (DLC)",
                    RomName = "Dead or Alive Xtreme 3 - JP0106-PCSG00771_00-00DLC00210000000 (Asia) (DLC).vpk",
                    Sha1 = "4444444444444444444444444444444444444445"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Dead or Alive Xtreme 3 - Venus - Hatsune Miku Theme (Japan) (Theme)",
                    RomName = "Dead or Alive Xtreme 3 - Venus - Hatsune Miku Theme (Japan) (Theme).vpk",
                    Sha1 = "5555555555555555555555555555555555555556"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Gravity Rush (USA)",
                    RomName = "Gravity Rush (USA).vpk",
                    Sha1 = "6666666666666666666666666666666666666667"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Gravity Rush - Maid Pack (USA) (Unlock Key)",
                    RomName = "Gravity Rush - Maid Pack (USA) (Unlock Key).vpk",
                    Sha1 = "7777777777777777777777777777777777777778"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Lego Batman 2 (USA)",
                    RomName = "Lego Batman 2 (USA).vpk",
                    Sha1 = "8888888888888888888888888888888888888889"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Lego Batman - Villain Pack (USA) (DLC)",
                    RomName = "Lego Batman - Villain Pack (USA) (DLC).vpk",
                    Sha1 = "9999999999999999999999999999999999999990"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Nelke & The Legendary Alchemists - Ateliers of the New World - Pack 1 (Japan) (DLC)",
                    RomName = "Nelke & The Legendary Alchemists - Ateliers of the New World - Pack 1 (Japan) (DLC).vpk",
                    Sha1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 9003,
                    GameName = "Nelke & The Legendary Alchemists - Ateliers of the New World - Pack 2 (Japan) (DLC)",
                    RomName = "Nelke & The Legendary Alchemists - Ateliers of the New World - Pack 2 (Japan) (DLC).vpk",
                    Sha1 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetAllDatFiles())
                .Returns(new List<NzbDrone.Core.Dat.DatFile> { datFile });

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetEntriesForPlatform(vitaPlatform.Slug, vitaPlatform.Name, vitaPlatform.LibretroSystemName))
                .Returns(datEntries);

            var tuple = Subject.GetSeriesInfo(vitaPlatform.TvdbId, Language.English, null);

            var atelierDlc = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Atelier Escha & Logy Plus - Costumes - Happy Wedding & White Knight"));
            atelierDlc.Should().NotBeNull();
            atelierDlc.Title.Should().Be("Atelier Escha & Logy Plus - Alchemists of the Dusk Sky");

            var doaDlc = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("JP0106-PCSG00771_00-00DLC00210000000"));
            doaDlc.Should().NotBeNull();
            doaDlc.Title.Should().Be("Dead or Alive Xtreme 3 - Venus");

            var doaTheme = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Hatsune Miku Theme"));
            doaTheme.Should().NotBeNull();
            doaTheme.Title.Should().Be("Dead or Alive Xtreme 3 - Venus");

            var unlockKey = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Maid Pack"));
            unlockKey.Should().NotBeNull();
            unlockKey.Title.Should().Be("Gravity Rush");
            unlockKey.FinaleType.Should().Be("DLC • Maid Pack");

            var legoDlc = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Villain Pack"));
            legoDlc.Should().NotBeNull();
            legoDlc.Title.Should().Be("Lego Batman");

            var nelke1 = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Pack 1 (Japan) (DLC)"));
            var nelke2 = tuple.Item2.FirstOrDefault(e => e.Overview.Contains("Pack 2 (Japan) (DLC)"));
            nelke1.Should().NotBeNull();
            nelke2.Should().NotBeNull();
            nelke1.Title.Should().Be("Nelke & The Legendary Alchemists - Ateliers of the New World");
            nelke2.Title.Should().Be("Nelke & The Legendary Alchemists - Ateliers of the New World");
        }

        [Test]
        public void should_group_xbox360_addons_by_title_id_and_avatar_marketplace()
        {
            var entries = new List<GameCatalogEntry>
            {
                new GameCatalogEntry
                {
                    Title = "Halo 3",
                    BaseTitle = "Halo 3",
                    NoIntroName = "Halo 3 (USA) (4D5307E6)",
                    RevisionTag = "Retail (v1.0)"
                },
                new GameCatalogEntry
                {
                    Title = "Heroic Map Pack",
                    BaseTitle = "Heroic Map Pack",
                    NoIntroName = "Heroic Map Pack (World) (4D5307E6) (Addon)",
                    RevisionTag = "Addon"
                },
                new GameCatalogEntry
                {
                    Title = "2016 Penguins Stanley Cup Tee",
                    BaseTitle = "2016 Penguins Stanley Cup Tee",
                    NoIntroName = "2016 Penguins Stanley Cup Tee (World) (373907D1) (Addon)",
                    RevisionTag = "Addon"
                },
                new GameCatalogEntry
                {
                    Title = "2017 Air Force Cap",
                    BaseTitle = "2017 Air Force Cap",
                    NoIntroName = "2017 Air Force Cap (World) (373907D1) (Addon)",
                    RevisionTag = "Addon"
                },
                new GameCatalogEntry
                {
                    Title = "2017 St Patricks Day Grn Chr Lep",
                    BaseTitle = "2017 St Patricks Day Grn Chr Lep",
                    NoIntroName = "2017 St Patricks Day Grn Chr Lep (World) (373907D1) (Addon)",
                    RevisionTag = "Addon"
                },
                new GameCatalogEntry
                {
                    Title = "Map Pack Alpha",
                    BaseTitle = "Map Pack Alpha",
                    NoIntroName = "Map Pack Alpha (World) (58410AAA) (Addon)",
                    RevisionTag = "Addon"
                },
                new GameCatalogEntry
                {
                    Title = "Map Pack Beta",
                    BaseTitle = "Map Pack Beta",
                    NoIntroName = "Map Pack Beta (World) (58410AAA) (Addon)",
                    RevisionTag = "Addon"
                }
            };

            GameCatalogProxy.PairDlcWithBaseGames(entries);

            entries[1].Title.Should().Be("Halo 3");
            entries[1].RevisionTag.Should().Be("DLC • Heroic Map Pack");

            entries[2].Title.Should().Be("Xbox 360 Avatar & Marketplace Items");
            entries[3].Title.Should().Be("Xbox 360 Avatar & Marketplace Items");
            entries[4].Title.Should().Be("Xbox 360 Avatar & Marketplace Items");
            entries[2].RevisionTag.Should().Be("DLC • 2016 Penguins Stanley Cup Tee");

            entries[5].Title.Should().Be("Xbox 360 Addons (58410AAA)");
            entries[6].Title.Should().Be("Xbox 360 Addons (58410AAA)");
        }

        [Test]
        public void should_not_classify_game_boy_color_smdb_retail_entries_as_rom_hacks()
        {
            Subject.TryGetPlatformEntryBySlug("gbc", out var gbcPlatform).Should().BeTrue();

            var noIntroDat = new NzbDrone.Core.Dat.DatFile
            {
                Id = 10,
                Name = "Nintendo - Game Boy Color.dat",
                Category = "No-Intro"
            };
            var smdbDat = new NzbDrone.Core.Dat.DatFile
            {
                Id = 11,
                Name = "Game Boy Color SMDB.txt",
                Category = "SmokeMonster HTGDB / SMDB"
            };

            var datEntries = new List<NzbDrone.Core.Dat.DatEntry>
            {
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 10,
                    GameName = "Shantae (USA)",
                    RomName = "Shantae (USA).gbc",
                    Sha1 = "1111111111111111111111111111111111111111"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 11,
                    GameName = "Shantae (USA)",
                    RomName = "Shantae (USA).gbc",
                    SmdbPath = "Game Boy Color/1 USA - A-Z/Shantae (USA).gbc",
                    Sha1 = "1111111111111111111111111111111111111111"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 11,
                    GameName = "Wario Land 3 (World) (En,Ja)",
                    RomName = "Wario Land 3 (World) (En,Ja).gbc",
                    SmdbPath = "Game Boy Color/1 World/Wario Land 3 (World) (En,Ja).gbc",
                    Sha1 = "2222222222222222222222222222222222222222"
                },
                new NzbDrone.Core.Dat.DatEntry
                {
                    DatFileId = 11,
                    GameName = "Super Mario Land DX (World) (Color)",
                    RomName = "Super Mario Land DX (World) (Color).gbc",
                    SmdbPath = "Game Boy Color/4 Color Hacks/Super Mario Land DX (World) (Color).gbc",
                    Sha1 = "3333333333333333333333333333333333333333"
                }
            };

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetAllDatFiles())
                .Returns(new List<NzbDrone.Core.Dat.DatFile> { noIntroDat, smdbDat });

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Setup(s => s.GetEntriesForPlatform(gbcPlatform.Slug, gbcPlatform.Name, gbcPlatform.LibretroSystemName))
                .Returns(datEntries);

            var tuple = Subject.GetSeriesInfo(gbcPlatform.TvdbId, Language.English, null);

            tuple.Item2.Should().Contain(e => e.Title == "Shantae" && e.SeasonNumber == GameCatalogProxy.RegionUsa);
            tuple.Item2.Should().NotContain(e => e.Title == "Shantae" && e.SeasonNumber == GameCatalogProxy.RegionRomHacks);
            tuple.Item2.Should().Contain(e => e.Title == "Wario Land 3" && e.SeasonNumber == GameCatalogProxy.RegionWorld);
            tuple.Item2.Should().Contain(e => e.Title.Contains("Super Mario Land DX") && e.SeasonNumber == GameCatalogProxy.RegionRomHacks);
        }

        [Test]
        public void should_not_confuse_gb_gbc_and_gba_platforms()
        {
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Tetris (USA).gb", "gb", "Game Boy").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Pokemon Crystal (USA).gbc", "gb", "Game Boy").Should().BeFalse();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Metroid Fusion (USA).gba", "gb", "Game Boy").Should().BeFalse();

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Pokemon Crystal (USA).gbc", "gbc", "Game Boy Color").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Tetris (USA).gb", "gbc", "Game Boy Color").Should().BeFalse();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Metroid Fusion (USA).gba", "gbc", "Game Boy Color").Should().BeFalse();

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Metroid Fusion (USA).gba", "gba", "Game Boy Advance").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Tetris (USA).gb", "gba", "Game Boy Advance").Should().BeFalse();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Pokemon Crystal (USA).gbc", "gba", "Game Boy Advance").Should().BeFalse();
        }

        [Test]
        public void should_expose_separate_nintendo_eshop_platforms()
        {
            var platforms = Subject.GetAllPlatforms(Language.English);

            platforms.Should().Contain(p => p.TitleSlug == "wiiware" && p.Title == "Nintendo WiiWare");
            platforms.Should().Contain(p => p.TitleSlug == "wiiueshop" && p.Title == "Nintendo Wii U eShop");
            platforms.Should().Contain(p => p.TitleSlug == "dsiware" && p.Title == "Nintendo DSi Shop");
            platforms.Should().Contain(p => p.TitleSlug == "3dseshop" && p.Title == "Nintendo 3DS eShop");
            platforms.Should().Contain(p => p.TitleSlug == "switcheshop" && p.Title == "Nintendo Switch eShop");
        }

        [Test]
        public void should_separate_eshop_titles_from_retail_in_gametdb_and_compatibility_validator()
        {
            Subject.TryGetPlatformEntry(1014, out var switchPlatform).Should().BeTrue();
            Subject.TryGetPlatformEntry(1055, out var switchEshopPlatform).Should().BeTrue();

            var sampleXml = @"<xml>
  <game name=""Super Mario Odyssey (USA)"">
    <id>AAACA</id>
    <type>Switch</type>
    <region>USA</region>
    <locale lang=""EN""><title>Super Mario Odyssey</title></locale>
  </game>
  <game name=""Golf Story (USA)"">
    <id>ABABA</id>
    <type>eShop</type>
    <region>USA</region>
    <locale lang=""EN""><title>Golf Story</title></locale>
  </game>
</xml>";

            var retailGames = Subject.ParseGameTdbXmlContent(switchPlatform, sampleXml, new HashSet<string>());
            retailGames.Should().ContainSingle(g => g.Title == "Super Mario Odyssey");
            retailGames.Should().NotContain(g => g.Title == "Golf Story");

            var eshopGames = Subject.ParseGameTdbXmlContent(switchEshopPlatform, sampleXml, new HashSet<string>());
            eshopGames.Should().ContainSingle(g => g.Title == "Golf Story");
            eshopGames.Should().NotContain(g => g.Title == "Super Mario Odyssey");

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Golf Story (eShop).nsp", "switcheshop", "Nintendo Switch eShop").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Golf Story (eShop).nsp", "switch", "Nintendo Switch").Should().BeFalse();

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Cave Story (WiiWare).wad", "wiiware", "Nintendo WiiWare").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Cave Story (WiiWare).wad", "wii", "Nintendo Wii").Should().BeFalse();

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Shantae (DSiWare).nds", "dsiware", "Nintendo DSi Shop").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Shantae (DSiWare).nds", "nds", "Nintendo DS").Should().BeFalse();

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Pushmo (3DS eShop).cia", "3dseshop", "Nintendo 3DS eShop").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Pushmo (3DS eShop).cia", "3ds", "Nintendo 3DS").Should().BeFalse();

            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Fast Racing NEO (Wii U eShop).wux", "wiiueshop", "Nintendo Wii U eShop").Should().BeTrue();
            NzbDrone.Core.Parser.PlatformCompatibilityValidator.IsCompatible("Fast Racing NEO (Wii U eShop).wux", "wiiu", "Nintendo Wii U").Should().BeFalse();
        }

        [Test]
        public void should_not_classify_switch_or_atlas_games_as_magazines_and_books()
        {
            NzbDrone.Core.Games.RegionHelper.IsMagazineOrBook("Starlink - Battle for Atlas (USA)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsMagazineOrBook("Storybook Brawl (USA)").Should().BeFalse();

            Subject.TryGetPlatformEntry(1055, out var switchEshopPlatform).Should().BeTrue();
            var entry = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(switchEshopPlatform, "Pocket Dictionary (USA)", 2020, "GameTDB");
            entry.Should().NotBeNull();
            entry.RegionNumber.Should().Be(GameCatalogProxy.RegionUsa);
        }

        [Test]
        public void should_still_load_gametdb_switch_eshop_catalog_when_nswdb_times_out()
        {
            Subject.InvalidatePlatformCache();
            Subject.TryGetPlatformEntry(1055, out var switchEshopPlatform).Should().BeTrue();

            using var zipStream = new System.IO.MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("switchtdb.xml");
                using var writer = new System.IO.StreamWriter(entry.Open());
                writer.Write(@"<xml>
  <game name=""Golf Story (USA)"">
    <id>ABABA</id>
    <type>eShop</type>
    <region>USA</region>
    <locale lang=""EN""><title>Golf Story</title></locale>
  </game>
  <game name=""Hollow Knight (USA)"">
    <id>ABABB</id>
    <type>eShop</type>
    <region>USA</region>
    <locale lang=""EN""><title>Hollow Knight</title></locale>
  </game>
</xml>");
            }

            var zipBytes = zipStream.ToArray();

            Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>()
                .Setup(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.ToString().Contains("nswdb.com"))))
                .Throws(new System.TimeoutException("NSWDB timed out"));

            Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>()
                .SetupSequence(c => c.Execute(Moq.It.Is<NzbDrone.Common.Http.HttpRequest>(r => r.Url.ToString().Contains("switchtdb.zip"))))
                .Throws(new System.TimeoutException("Transient timeout"))
                .Returns(new NzbDrone.Common.Http.HttpResponse(
                    new NzbDrone.Common.Http.HttpRequest("https://www.gametdb.com/switchtdb.zip"),
                    new NzbDrone.Common.Http.HttpHeader(),
                    zipBytes));

            var tuple = Subject.GetSeriesInfo(switchEshopPlatform.TvdbId, Language.English, null);

            tuple.Item2.Should().Contain(e => e.Title == "Golf Story");
            tuple.Item2.Should().Contain(e => e.Title == "Hollow Knight");
        }

        [Test]
        public void should_route_3dsware_and_new3dsware_to_3ds_eshop_and_exclude_custom_types()
        {
            Subject.TryGetPlatformEntry(1013, out var retail3ds).Should().BeTrue();
            Subject.TryGetPlatformEntry(1054, out var eshop3ds).Should().BeTrue();

            var xml = @"<xml>
  <game name=""Pokemon X (USA)""><id>EKJA</id><type>3DS</type><region>USA</region><locale lang=""EN""><title>Pokemon X</title></locale></game>
  <game name=""Pushmo (USA)""><id>JCAA</id><type>3DSWare</type><region>USA</region><locale lang=""EN""><title>Pushmo</title></locale></game>
  <game name=""Binding of Isaac Rebirth (USA)""><id>FNBA</id><type>New3DSWare</type><region>USA</region><locale lang=""EN""><title>The Binding of Isaac: Rebirth</title></locale></game>
  <game name=""Shantae DSiWare (USA)""><id>KSHA</id><type>DSiWare</type><region>USA</region><locale lang=""EN""><title>Shantae DSiWare</title></locale></game>
  <game name=""Custom Theme (USA)""><id>CUST</id><type>CUSTOM</type><region>USA</region><locale lang=""EN""><title>Custom Theme</title></locale></game>
</xml>";

            var retailGames = Subject.ParseGameTdbXmlContent(retail3ds, xml, new HashSet<string>());
            retailGames.Should().ContainSingle(g => g.Title == "Pokemon X");
            retailGames.Should().NotContain(g => g.Title == "Pushmo");

            var eshopGames = Subject.ParseGameTdbXmlContent(eshop3ds, xml, new HashSet<string>());
            eshopGames.Should().Contain(g => g.Title == "Pushmo");
            eshopGames.Should().Contain(g => g.Title == "Binding of Isaac Rebirth");
            eshopGames.Should().NotContain(g => g.Title == "Pokemon X");
            eshopGames.Should().NotContain(g => g.Title == "Shantae DSiWare");
            eshopGames.Should().NotContain(g => g.Title == "Custom Theme");
        }

        [Test]
        public void should_not_misclassify_retail_eshop_titles_by_title_words()
        {
            NzbDrone.Core.Games.RegionHelper.IsUnlicensed("Shantae and the Pirates Curse (World)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsUnlicensed("Family Pirate Party (USA)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsUnlicensed("Action 52 (USA) (Pirate)").Should().BeTrue();

            NzbDrone.Core.Games.RegionHelper.IsRomHackOrTranslation("Rogue Trooper Redux (World)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsRomHackOrTranslation("Rigid Force Redux (World)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsRomHackOrTranslation("Super Metroid (USA) (Redux)").Should().BeTrue();

            NzbDrone.Core.Games.RegionHelper.IsMagazineOrBook("Simple Series Vol. 1 - The Block Kuzushi neo (Japan)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsMagazineOrBook("Hellokids - Vol. 1 - Coloring and Painting! (Europe)").Should().BeFalse();

            NzbDrone.Core.Games.RegionHelper.IsTestOrUtility("Codebreaker Puzzle 1000! ENG & JAN (World)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsTestOrUtility("Just 1 Minute! Memory Test with Masterpieces (Japan)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsTestOrUtility("Photographic Memory Test (World)").Should().BeFalse();
            NzbDrone.Core.Games.RegionHelper.IsTestOrUtility("Memory Test").Should().BeTrue();
        }

        [Test]
        public void should_provide_gametdb_urls_for_gamecube_nds_and_ps3_and_filter_gamecube_from_wiitdb()
        {
            GameCatalogProxy.GetGameTdbUrl("gamecube").Should().Be("https://www.gametdb.com/wiitdb.zip");
            GameCatalogProxy.GetGameTdbUrl("nds").Should().Be("https://www.gametdb.com/dstdb.zip");
            GameCatalogProxy.GetGameTdbUrl("dsiware").Should().Be("https://www.gametdb.com/dstdb.zip");
            GameCatalogProxy.GetGameTdbUrl("ps3").Should().Be("https://www.gametdb.com/ps3tdb.zip");

            Subject.TryGetPlatformEntry(1007, out var gamecubePlatform).Should().BeTrue();
            Subject.TryGetPlatformEntry(1008, out var wiiPlatform).Should().BeTrue();

            const string wiiAndGcXml = @"<xml>
  <game name=""Super Mario Sunshine (USA)"">
    <id>GMSE01</id>
    <type>GameCube</type>
    <region>USA</region>
    <locale lang=""EN""><title>Super Mario Sunshine</title></locale>
  </game>
  <game name=""Super Mario Galaxy (USA)"">
    <id>RMGE01</id>
    <type>Wii</type>
    <region>USA</region>
    <locale lang=""EN""><title>Super Mario Galaxy</title></locale>
  </game>
</xml>";

            var gcGames = Subject.ParseGameTdbXmlContent(gamecubePlatform, wiiAndGcXml, new HashSet<string>());
            gcGames.Should().ContainSingle(g => g.Title == "Super Mario Sunshine");
            gcGames.Should().NotContain(g => g.Title == "Super Mario Galaxy");

            var wiiGames = Subject.ParseGameTdbXmlContent(wiiPlatform, wiiAndGcXml, new HashSet<string>());
            wiiGames.Should().ContainSingle(g => g.Title == "Super Mario Galaxy");
            wiiGames.Should().NotContain(g => g.Title == "Super Mario Sunshine");

            Mocker.GetMock<NzbDrone.Core.Dat.IDatCacheService>()
                .Verify(d => d.Insert(Moq.It.IsAny<NzbDrone.Core.Dat.DatFile>(), Moq.It.IsAny<List<NzbDrone.Core.Dat.DatEntry>>()), Moq.Times.Never());
        }

        [Test]
        public void should_bypass_remote_archive_cache_when_force_refresh_is_requested()
        {
            Subject.InvalidatePlatformCache();
            Subject.TryGetPlatformEntry(1014, out var switchPlatform).Should().BeTrue();

            var nswdbXmlV1 = @"<releases><release><name>Test Switch Game One</name><region>USA</region><imgcrc>11223344</imgcrc><type>1</type></release></releases>";
            var nswdbXmlV2 = @"<releases><release><name>Test Switch Game Two</name><region>USA</region><imgcrc>55667788</imgcrc><type>1</type></release></releases>";

            var nswdbFetches = 0;
            Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>()
                .Setup(c => c.Execute(Moq.It.IsAny<NzbDrone.Common.Http.HttpRequest>()))
                .Returns((NzbDrone.Common.Http.HttpRequest req) =>
                {
                    if (req.Url.ToString().Contains("nswdb.com"))
                    {
                        nswdbFetches++;
                        var payload = nswdbFetches == 1 ? nswdbXmlV1 : nswdbXmlV2;
                        return new NzbDrone.Common.Http.HttpResponse(req, new NzbDrone.Common.Http.HttpHeader(), System.Text.Encoding.UTF8.GetBytes(payload));
                    }

                    return new NzbDrone.Common.Http.HttpResponse(req, new NzbDrone.Common.Http.HttpHeader(), System.Array.Empty<byte>(), System.Net.HttpStatusCode.NotFound);
                });

            Subject.SyncPlatformDatCatalogs(switchPlatform.TvdbId, forceRefresh: false);
            Subject.SyncPlatformDatCatalogs(switchPlatform.TvdbId, forceRefresh: true);

            nswdbFetches.Should().Be(2);
            var refreshed = Subject.GetSeriesInfo(switchPlatform.TvdbId, Language.English, null);
            refreshed.Item2.Should().Contain(g => g.Title == "Test Switch Game Two");
        }

        [Test]
        public void should_include_all_new_console_handheld_and_addon_platforms_with_dat_feeds()
        {
            var expectedNewSlugs = new[]
            {
                "neogeocd", "supergrafx", "ws", "ngp",
                "sg1000", "cd32", "pico", "beena", "arcadia2001", "scv", "cassettevision",
                "loopy", "pv1000", "superacan", "vc4000", "studio2", "gx4000", "videopacplus",
                "creativision", "vsmile", "zeebo", "picno",
                "xbox360digital", "ps3psn", "psppsn",
                "supervision", "gamecom", "adventurevision", "gamemaster", "gp32",
                "arduboy", "gameandwatch", "leapster", "pocketchallengev2",
                "sufamiturbo", "ereader"
            };

            var proxySlugs = Subject.GetAllPlatforms(Language.English).Select(p => p.TitleSlug).ToHashSet();
            var catalogServiceSlugs = new NzbDrone.Core.Games.PlatformCatalogService().GetAll().Select(p => p.Slug).ToHashSet();

            foreach (var slug in expectedNewSlugs)
            {
                proxySlugs.Should().Contain(slug, $"GameCatalogProxy should define '{slug}'");
                catalogServiceSlugs.Should().Contain(slug, $"PlatformCatalogService should define '{slug}'");
                NzbDrone.Core.Games.PlatformCatalogService.KnownPlatformSlugs.Should().Contain(slug);
            }

            Subject.InferPlatformForText("Sony - PlayStation 3 (PSN)")?.Slug.Should().Be("ps3psn");
            Subject.InferPlatformForText("SNK - Neo Geo CD")?.Slug.Should().Be("neogeocd");
            Subject.InferPlatformForText("Microsoft - Xbox 360 (Digital)")?.Slug.Should().Be("xbox360digital");
            Subject.InferPlatformForText("NEC - PC Engine SuperGrafx")?.Slug.Should().Be("supergrafx");
            Subject.InferPlatformForText("Epoch - Super Cassette Vision")?.Slug.Should().Be("scv");
            Subject.InferPlatformForText("Philips - Videopac+")?.Slug.Should().Be("videopacplus");

            var requestedUrls = new List<string>();
            Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>()
                .Setup(c => c.Execute(Moq.It.IsAny<NzbDrone.Common.Http.HttpRequest>()))
                .Returns((NzbDrone.Common.Http.HttpRequest req) =>
                {
                    requestedUrls.Add(req.Url.ToString());
                    return new NzbDrone.Common.Http.HttpResponse(req, new NzbDrone.Common.Http.HttpHeader(), System.Array.Empty<byte>(), System.Net.HttpStatusCode.NotFound);
                });

            Subject.TryGetPlatformEntryBySlug("xbox360digital", out var x360d).Should().BeTrue();
            Subject.SyncPlatformDatCatalogs(x360d.TvdbId, forceRefresh: true);
            requestedUrls.Should().Contain(u => u.Contains("Microsoft%20-%20Xbox%20360%20(Digital).dat"));
            requestedUrls.Should().Contain(u => u.Contains("Games%20on%20Demand).dat"));

            requestedUrls.Clear();
            Subject.TryGetPlatformEntryBySlug("neogeocd", out var ngcd).Should().BeTrue();
            Subject.SyncPlatformDatCatalogs(ngcd.TvdbId, forceRefresh: true);
            requestedUrls.Should().Contain(u => u.Contains("metadat/redump/SNK%20-%20Neo%20Geo%20CD.dat"));

            requestedUrls.Clear();
            Subject.TryGetPlatformEntryBySlug("gameandwatch", out var gw).Should().BeTrue();
            Subject.SyncPlatformDatCatalogs(gw.TvdbId, forceRefresh: true);
            requestedUrls.Should().Contain(u => u.Contains("dat/Handheld%20Electronic%20Game.dat"));

            requestedUrls.Clear();
            Subject.TryGetPlatformEntryBySlug("gx4000", out var gx).Should().BeTrue();
            Subject.SyncPlatformDatCatalogs(gx.TvdbId, forceRefresh: true);
            requestedUrls.Should().Contain(u => u.Contains("metadat/tosec/Amstrad%20-%20GX4000.dat"));
        }

        [Test]
        public void should_normalize_base_v1_versions_and_preserve_alt_or_edition_qualifiers()
        {
            Subject.TryGetPlatformEntryBySlug("gb", out var gbPlatform).Should().BeTrue();

            var v10 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Tetris (World) (v1.0)", 1989, "No-Intro");
            var v100 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Tetris (World) (v1.00)", 1989, "No-Intro");
            var ver10 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Tetris (World) (Version 1.0)", 1989, "No-Intro");
            var altV1 = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Tetris (USA) (v1.0) (Alt)", 1989, "No-Intro");
            var sgb = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Donkey Kong (World) (SGB Enhanced)", 1994, "No-Intro");
            var playersChoice = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Tetris (World) (Player's Choice)", 1989, "No-Intro");
            var revAlt = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(gbPlatform, "Link's Awakening (USA) (Rev 1) (Alt 2)", 1993, "No-Intro");

            v10.RevisionTag.Should().Be("Retail (v1.0)");
            v100.RevisionTag.Should().Be("Retail (v1.0)");
            ver10.RevisionTag.Should().Be("Retail (v1.0)");
            altV1.RevisionTag.Should().Be("v1.0 • Alt");
            sgb.RevisionTag.Should().Be("SGB Enhanced • ROM Hack");
            playersChoice.RevisionTag.Should().Be("Player's Choice");
            revAlt.RevisionTag.Should().Be("Rev 1 • Alt 2");
        }

        [Test]
        public void should_disambiguate_distinct_hash_entries_colliding_on_same_region_and_base_version()
        {
            var existing = new GameCatalogEntry
            {
                Title = "Rayman",
                BaseTitle = "Rayman",
                NoIntroName = "Rayman (Europe) (En,Fr,De)",
                RegionNumber = GameCatalogProxy.RegionEurope,
                RevisionTag = "Retail (v1.0)",
                ExpectedSha1 = "1111111111111111111111111111111111111111"
            };

            var candidate = new GameCatalogEntry
            {
                Title = "Rayman",
                BaseTitle = "Rayman",
                NoIntroName = "Rayman (Europe) (En,Es,It)",
                RegionNumber = GameCatalogProxy.RegionEurope,
                RevisionTag = "Retail (v1.0)",
                ExpectedSha1 = "2222222222222222222222222222222222222222"
            };

            var list = new List<GameCatalogEntry> { existing };
            GameCatalogProxy.DisambiguateCollidingEntries(existing, candidate, list, "rayman");

            existing.RevisionTag.Should().Be("Retail (v1.0) • En,Fr,De");
            candidate.RevisionTag.Should().Be("Retail (v1.0) • En,Es,It");
        }

        [Test]
        public void should_have_canonical_console_image_and_png_asset_for_every_built_in_platform()
        {
            var platforms = GameCatalogProxy.GetBuiltInPlatforms();
            platforms.Should().HaveCount(91);

            var dir = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            string consoleAssetDir = null;
            while (dir != null)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "frontend", "public", "Content", "Images", "Consoles");
                if (System.IO.Directory.Exists(candidate))
                {
                    consoleAssetDir = candidate;
                    break;
                }

                dir = dir.Parent;
            }

            consoleAssetDir.Should().NotBeNull("expected to locate frontend/public/Content/Images/Consoles asset folder");

            foreach (var platform in platforms)
            {
                var expectedUrl = $"/Content/Images/Consoles/{platform.Slug.ToLowerInvariant()}.png";
                var info = Subject.GetSeriesInfo(platform.TvdbId, Language.English, "standard");

                info.Item1.Images.Should().Contain(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Poster && i.Url == expectedUrl && i.RemoteUrl == expectedUrl);
                info.Item1.Images.Should().Contain(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Banner && i.Url == expectedUrl && i.RemoteUrl == expectedUrl);

                var assetPath = System.IO.Path.Combine(consoleAssetDir, $"{platform.Slug.ToLowerInvariant()}.png");
                System.IO.File.Exists(assetPath).Should().BeTrue($"missing console image PNG for {platform.Name} ({platform.Slug})");
            }
        }

        [Test]
        public void should_heal_missing_external_uppercase_and_legacy_slug_platform_images()
        {
            var emptyImages = new List<NzbDrone.Core.MediaCover.MediaCover>();
            GameCatalogProxy.EnsureCanonicalPlatformImages(1003, "snes", "Super Nintendo Entertainment System", emptyImages).Should().BeTrue();
            emptyImages.Should().Contain(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Poster && i.RemoteUrl == "/Content/Images/Consoles/snes.png");
            emptyImages.Should().Contain(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Banner && i.RemoteUrl == "/Content/Images/Consoles/snes.png");

            var staleImages = new List<NzbDrone.Core.MediaCover.MediaCover>
            {
                new(NzbDrone.Core.MediaCover.MediaCoverTypes.Poster, "/MediaCover/1/poster.jpg") { RemoteUrl = "https://example.com/old.jpg" },
                new(NzbDrone.Core.MediaCover.MediaCoverTypes.Fanart, "/MediaCover/1/fanart.jpg") { RemoteUrl = "https://example.com/fanart.jpg" }
            };
            GameCatalogProxy.EnsureCanonicalPlatformImages(1022, "ps1", "Sony PlayStation", staleImages).Should().BeTrue();
            staleImages.Should().NotContain(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Fanart);
            staleImages.Should().ContainSingle(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Poster && i.Url == "/Content/Images/Consoles/ps1.png" && i.RemoteUrl == "/Content/Images/Consoles/ps1.png");

            var legacySlugImages = new List<NzbDrone.Core.MediaCover.MediaCover>();
            GameCatalogProxy.EnsureCanonicalPlatformImages(0, "super-nintendo-entertainment-system", "Super Nintendo Entertainment System", legacySlugImages).Should().BeTrue();
            legacySlugImages.Should().Contain(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Poster && i.RemoteUrl == "/Content/Images/Consoles/snes.png");

            var uppercaseImages = new List<NzbDrone.Core.MediaCover.MediaCover>
            {
                new(NzbDrone.Core.MediaCover.MediaCoverTypes.Poster, "/Content/Images/Consoles/NES.png") { RemoteUrl = "/Content/Images/Consoles/NES.png" },
                new(NzbDrone.Core.MediaCover.MediaCoverTypes.Banner, "/Content/Images/Consoles/NES.png") { RemoteUrl = "/Content/Images/Consoles/NES.png" }
            };
            GameCatalogProxy.EnsureCanonicalPlatformImages(1001, "nes", "Nintendo Entertainment System", uppercaseImages).Should().BeTrue();
            uppercaseImages.Should().OnlyContain(i => i.Url == "/Content/Images/Consoles/nes.png" && i.RemoteUrl == "/Content/Images/Consoles/nes.png");
            GameCatalogProxy.EnsureCanonicalPlatformImages(1001, "nes", "Nintendo Entertainment System", uppercaseImages).Should().BeFalse();
        }
    }
}
