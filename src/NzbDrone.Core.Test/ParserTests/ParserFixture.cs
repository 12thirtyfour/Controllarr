using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class ParserFixture : CoreTest
    {
        [TestCase("Super Mario 64 (USA) [!]", "supermario64usa")]
        [TestCase("Chrono Trigger (USA) [!]", "chronotriggerusa")]
        [TestCase("The Legend of Zelda (2006)", "thelegendzelda2006")]
        [TestCase("Metroid Prime (2002)", "metroidprime2002")]
        [TestCase("Pokemon Emerald Version", "pokemonemeraldversion")]
        [TestCase("Nintendo 64 - Super Mario World", "nintendo64supermarioworld")]
        public void should_clean_game_and_platform_names(string rawTitle, string expectedCleanTitle)
        {
            var result = Parser.Parser.ParseSeriesName(rawTitle).CleanSeriesTitle();
            result.Should().Be(expectedCleanTitle);
        }

        [Test]
        public void should_remove_accents_from_title()
        {
            const string title = "Pok\u00E9mon";

            title.CleanSeriesTitle().Should().Be("pokemon");
        }

        [TestCase("Super Mario World : Rev 1 [No-Intro].sfc")]
        public void should_clean_up_invalid_path_characters(string postTitle)
        {
            Parser.Parser.ParseTitle(postTitle);
        }

        [Test]
        public void should_not_misparse_rom_paths_or_indexed_rom_titles_as_tv_episodes()
        {
            Parser.Parser.ParsePath("/roms/Nintendo/Nintendo 64/USA/WWF No Mercy.zip").Should().BeNull();
            Parser.Parser.ParseTitle("1015 Pokemon Diamond Version").Should().BeNull();
            Parser.Parser.ParseTitle("Pokemon Diamond Version r9792").Should().BeNull();
        }

        [TestCase("Super.Mario.64.USA.English.[No-Intro].z64", "English")]
        public void should_parse_language_from_release_title(string postTitle, string expectedLanguage)
        {
            var result = LanguageParser.ParseLanguages(postTitle);

            result.Count.Should().Be(1);
            result.Should().Contain((Language)expectedLanguage);
        }
    }
}
