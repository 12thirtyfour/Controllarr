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
            tuple.Item2.Should().Contain(e => e.Title.Contains("Pokemon - Emerald Version") && e.SeasonNumber == GameCatalogProxy.RegionUsa);
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

            platforms.Should().HaveCount(40);
            platforms.Select(p => p.TvdbId).Should().OnlyHaveUniqueItems();
            platforms.Select(p => p.Title).Should().OnlyHaveUniqueItems();
            platforms.Select(p => p.TitleSlug).Should().OnlyHaveUniqueItems();
            platforms.Should().OnlyContain(p => p.TvdbId > 0 && !string.IsNullOrWhiteSpace(p.Title) && !string.IsNullOrWhiteSpace(p.TitleSlug));
            platforms.Should().OnlyContain(p => p.Seasons != null && p.Seasons.Count > 0);
        }

        [Test]
        public void should_classify_240p_test_suite_as_homebrew_and_underscore_regions_correctly()
        {
            Subject.TryGetPlatformEntry(1022, out var segaCdPlatform).Should().BeTrue();

            var megaCd240pEu = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "240p_MegaCD_EU", 2014, "Redump");
            megaCd240pEu.Should().NotBeNull();
            megaCd240pEu.RegionNumber.Should().Be(GameCatalogProxy.RegionHomebrew);

            var suite240p = GameCatalogProxy.CreateCatalogEntryFromNoIntroName(segaCdPlatform, "240p Test Suite (USA)", 2014, "Redump");
            suite240p.Should().NotBeNull();
            suite240p.RegionNumber.Should().Be(GameCatalogProxy.RegionHomebrew);

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
                new object[] { vbPlatform, smdbContent, null, seenNames, false });

            entries.Should().HaveCount(1);
            entries[0].Title.Should().Be("3-D Tetris");
            entries[0].NoIntroName.Should().Be("3-D Tetris (USA)");
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
                new object[] { vbPlatform, datContent, "No-Intro", lookup, seenNames });

            entries.Should().HaveCount(2);
            entries[0].ReleaseYear.Should().Be(1996);
            entries[0].ReleaseDateString.Should().Be("1996");
            entries[1].ReleaseYear.Should().Be(1996);
            entries[1].ReleaseDateString.Should().Be("1996");
        }
    }
}
