using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class LanguageParserFixture : CoreTest
    {
        [TestCase("Sonic the Hedgehog (USA) [!].md")]
        [TestCase("Chrono Trigger (USA) [No-Intro].sfc")]
        [TestCase("Final Fantasy VII (USA) (Disc 1) [Redump].chd")]
        public void should_parse_language_unknown(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.Unknown);
        }

        [TestCase("Super Mario 64 (USA) (English) [No-Intro].z64")]
        [TestCase("Chrono Trigger (World) (English) [!].sfc")]
        public void should_parse_language_english(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.English);
        }

        [TestCase("Final Fantasy VII (France) (French) [Redump].chd")]
        [TestCase("The Legend of Zelda (France) (FR) [No-Intro].nes")]
        public void should_parse_language_french(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.French);
        }

        [TestCase("Final Fantasy VI (France) (Eng) (Fra) [No-Intro].sfc")]
        [TestCase("Gran Turismo (Europe) (ENG) (FR) [Redump].chd")]
        public void should_parse_language_french_english(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);

            result.Should().Contain(Language.French);
            result.Should().Contain(Language.English);
        }

        [TestCase("The Legend of Zelda (Germany) (German) [No-Intro].nes")]
        [TestCase("Secret of Mana (Germany) (DE) [!].sfc")]
        public void should_parse_language_german(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.German);
        }

        [TestCase("Pokemon Emerald (Spain) (Spanish) [No-Intro].gba")]
        [TestCase("Castlevania (Spain) (ESP) [!].nes")]
        public void should_parse_language_spanish(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.Spanish);
        }

        [TestCase("Super Metroid (Italy) (Italian) [No-Intro].sfc")]
        [TestCase("Resident Evil (Italy) (ITA) [Redump].chd")]
        public void should_parse_language_italian(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.Italian);
        }

        [TestCase("Chrono Trigger (Japan) (Japanese) [!].sfc")]
        [TestCase("Mother 3 (Japan) (JA) [No-Intro].gba")]
        public void should_parse_language_japanese(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.Japanese);
        }

        [TestCase("Tetris (Russia) (Russian) [!].nes")]
        [TestCase("Battletoads (Russia) (RUS) [No-Intro].nes")]
        public void should_parse_language_russian(string releaseTitle)
        {
            var result = LanguageParser.ParseLanguages(releaseTitle);
            result.Should().Contain(Language.Russian);
        }
    }
}
