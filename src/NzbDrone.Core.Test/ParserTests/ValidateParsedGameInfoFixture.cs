using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class ValidateParsedGameInfoFixture : CoreTest
    {
        private ParsedEpisodeInfo _parsedGameInfo;
        private Series _platform;

        [SetUp]
        public void Setup()
        {
            _parsedGameInfo = Builder<ParsedEpisodeInfo>.CreateNew()
                                                        .With(p => p.AirDate = null)
                                                        .Build();

            _platform = Builder<Series>.CreateNew()
                                       .With(s => s.SeriesType = SeriesTypes.Standard)
                                       .Build();
        }

        private void GivenDatedParsedGameInfo()
        {
            _parsedGameInfo.AirDate = "2018-05-21";
        }

        [Test]
        public void should_return_true_if_game_info_is_not_dated()
        {
            ValidateParsedGameInfo.ValidateForPlatformType(_parsedGameInfo, _platform).Should().BeTrue();
            ExceptionVerification.ExpectedWarns(0);
        }

        [Test]
        public void should_return_true_if_game_info_is_dated_for_standard_platform()
        {
            GivenDatedParsedGameInfo();

            ValidateParsedGameInfo.ValidateForPlatformType(_parsedGameInfo, _platform).Should().BeTrue();
            ExceptionVerification.ExpectedWarns(0);
        }
    }
}
