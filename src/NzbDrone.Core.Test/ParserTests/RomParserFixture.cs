using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class RomParserFixture
    {
        [Test]
        public void Should_parse_simple_title()
        {
            var result = RomParser.ParseRomName("Super Mario World (USA).sfc");
            result.GameTitle.Should().Be("Super Mario World");
            result.Regions.Should().Contain("USA");
            result.Format.Should().Be("sfc");
        }

        [TestCase("Castlevania (USA) (Rev 1).nes", "Castlevania", "USA", "1", "nes")]
        [TestCase("Zelda II - The Adventure of Link (Europe).nes", "Zelda II - The Adventure of Link", "Europe", null, "nes")]
        [TestCase("Chrono Trigger (Japan) (v1.1).sfc", "Chrono Trigger", "Japan", "1.1", "sfc")]
        [TestCase("Super Mario 64 (USA) (Rev A).z64", "Super Mario 64", "USA", "A", "z64")]
        [TestCase("NES/A-F/Castlevania (USA) (Rev 1).nes", "Castlevania", "USA", "1", "nes")]
        [TestCase("001 Legend of Zelda, The (USA).sfc", "Legend of Zelda, The", "USA", null, "sfc")]
        [TestCase("002 Chrono Trigger (USA).sfc", "Chrono Trigger", "USA", null, "sfc")]
        [TestCase("001-Super Mario World (USA).sfc", "Super Mario World", "USA", null, "sfc")]
        [TestCase("[001] Super Mario World (USA).sfc", "Super Mario World", "USA", null, "sfc")]
        [TestCase("#001 Super Mario World (USA).sfc", "Super Mario World", "USA", null, "sfc")]
        [TestCase("01 - Metroid (USA).nes", "Metroid", "USA", null, "nes")]
        [TestCase("Final Fantasy III (USA) (Rev 1).sfc", "Final Fantasy III", "USA", "1", "sfc")]
        [TestCase("Donkey Kong Country (USA) (Rev. 1).sfc", "Donkey Kong Country", "USA", "1", "sfc")]
        [TestCase("Super Metroid (Japan, USA).sfc", "Super Metroid", "USA", null, "sfc")]
        [TestCase("1942 (USA).nes", "1942", "USA", null, "nes")]
        [TestCase("2020 Super Baseball (USA).sfc", "2020 Super Baseball", "USA", null, "sfc")]
        [TestCase("3 Ninjas Kick Back (USA).sfc", "3 Ninjas Kick Back", "USA", null, "sfc")]
        [TestCase("7th Saga, The (USA).sfc", "7th Saga, The", "USA", null, "sfc")]
        public void Should_parse_real_world_roms(string title, string expectedTitle, string expectedRegion, string expectedRevision, string expectedFormat)
        {
            var result = RomParser.ParseRomName(title);
            result.GameTitle.Should().Be(expectedTitle);
            result.Regions.Should().Contain(expectedRegion);
            if (expectedRevision != null)
            {
                if (result.Revision != null)
                    result.Revision.Should().Be(expectedRevision);
                else
                    result.Version.Should().Be(expectedRevision);
            }
            result.Format.Should().Be(expectedFormat);
        }

        [Test]
        public void Should_parse_full_set_archive()
        {
            var result = RomParser.ParseRomName("Nintendo - Super Nintendo Entertainment System (20240501-120000).zip");
            result.IsFullSetArchive.Should().BeTrue();
            result.GameTitle.Should().Be("Nintendo - Super Nintendo Entertainment System");
            result.Format.Should().Be("zip");
        }

        [Test]
        public void Should_parse_quality_tags()
        {
            var result = RomParser.ParseRomName("Some Game [!].nes");
            result.Quality.Quality.Should().Be(Quality.VerifiedGoodDump);

            result = RomParser.ParseRomName("Bad Game [b].nes");
            result.Quality.Quality.Should().Be(Quality.UnverifiedRaw);

            result = RomParser.ParseRomName("Translation [T+Eng].nes");
            result.Quality.Quality.Should().Be(Quality.TranslationPatch);
        }

        [TestCase("Some Game (Headered).smc", "Headered")]
        [TestCase("Another Game (NES 2.0).nes", "NES 2.0")]
        public void Should_parse_hardware_tags(string title, string expectedTag)
        {
            var result = RomParser.ParseRomName(title);
            result.HardwareTags.Should().Contain(expectedTag);
        }

        [TestCase("Game (Disc 1).iso", "1", null)]
        [TestCase("Game (Track 02).bin", null, "02")]
        public void Should_parse_disc_and_track_numbers(string title, string expectedDisc, string expectedTrack)
        {
            var result = RomParser.ParseRomName(title);
            result.DiscNumber.Should().Be(expectedDisc);
            result.TrackNumber.Should().Be(expectedTrack);
        }

        [TestCase("_Top/Game.nes", "Game")]
        [TestCase("A-F/Another.nes", "Another")]
        [TestCase("1 USA/Third.nes", "Third")]
        [TestCase("Translations/Translated.nes", "Translated")]
        public void Should_strip_ignored_prefixes(string title, string expectedTitle)
        {
            var result = RomParser.ParseRomName(title);
            result.GameTitle.Should().Be(expectedTitle);
        }

        [Test]
        public void Should_return_null_for_empty_title()
        {
            var result = RomParser.ParseRomName("  ");
            result.Should().BeNull();
        }

        [Test]
        public void Should_not_treat_language_codes_as_geographic_regions()
        {
            var result = RomParser.ParseRomName("Monster Fruit Academy (World) (EN,FR,JA,KO,ZHCN).nsp");
            result.GameTitle.Should().Be("Monster Fruit Academy");
            result.Regions.Should().Contain("World");
            result.Regions.Should().NotContain("EN");
            result.Regions.Should().NotContain("JA");
        }
    }
}
