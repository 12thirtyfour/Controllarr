using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.PlatformTests.PlatformServiceTests
{
    [TestFixture]
    public class EnsureMonitoredForGamesFixture : CoreTest<PlatformService>
    {
        [Test]
        public void should_trickle_up_to_unmonitored_region_and_platform_without_trickling_down()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Id = 10)
                .With(s => s.Monitored = false)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = false },
                    new Season { SeasonNumber = 2, Monitored = false }
                })
                .Build();

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(r => r.Get(10))
                  .Returns(series);

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(r => r.Update(It.IsAny<Series>()))
                  .Returns<Series>(s => s);

            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(s => s.GetTagChanges(It.IsAny<Series>()))
                  .Returns(new AutoTaggingChanges());

            var game = new Episode { Id = 101, SeriesId = 10, SeasonNumber = 1, Monitored = true };

            Subject.EnsureMonitoredForGames(new[] { game });

            series.Monitored.Should().BeTrue();
            series.Seasons[0].Monitored.Should().BeTrue();
            series.Seasons[1].Monitored.Should().BeFalse();

            Mocker.GetMock<ISeriesRepository>().Verify(r => r.Update(series), Times.Once());
            Mocker.GetMock<IEpisodeService>().Verify(e => e.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());
            Mocker.GetMock<IEpisodeService>().Verify(e => e.SetEpisodeMonitoredBySeason(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_not_update_when_region_and_platform_are_already_monitored()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Id = 10)
                .With(s => s.Monitored = true)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true }
                })
                .Build();

            Mocker.GetMock<ISeriesRepository>()
                  .Setup(r => r.Get(10))
                  .Returns(series);

            var game = new Episode { Id = 101, SeriesId = 10, SeasonNumber = 1, Monitored = true };

            Subject.EnsureMonitoredForGames(new[] { game });

            Mocker.GetMock<ISeriesRepository>().Verify(r => r.Update(It.IsAny<Series>()), Times.Never());
        }
    }
}
