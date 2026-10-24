using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.EpisodeMonitoredServiceTests
{
    [TestFixture]
    public class SetEpisodeMontitoredFixture : CoreTest<EpisodeMonitoredService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Status = SeriesStatusType.Continuing)
                                     .With(s => s.Seasons = new List<Season>
                                                            {
                                                                new Season { SeasonNumber = 0, Monitored = true },
                                                                new Season { SeasonNumber = 1, Monitored = true },
                                                                new Season { SeasonNumber = 2, Monitored = true }
                                                            })
                                     .Build();

            GivenMonitoredSeasons(_series.Seasons.Where(s => s.SeasonNumber > 0 && s.Monitored)
                                                 .Select(s => s.SeasonNumber)
                                                 .ToArray());
        }

        private void GivenMonitoredSeasons(params int[] seasonNumbers)
        {
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(seasonNumbers.ToList());
        }

        [Test]
        public void should_update_series_without_changing_episodes_when_options_are_null()
        {
            Subject.SetEpisodeMonitoredStatus(_series, null);

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.IsAny<Series>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_when_skip()
        {
            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Skip });

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.IsAny<Series>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_apply_monitored_status_with_first_and_last_season()
        {
            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Missing });

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.SetEpisodeMonitoredBySeries(_series.Id, MonitorTypes.Missing, 1, 2), Times.Once());
        }

        [Test]
        public void should_monitor_last_season_when_monitoring_all()
        {
            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.All });

            VerifySeasonMonitored(2);
        }

        [Test]
        public void should_monitor_last_season_when_monitoring_future_and_series_continuing()
        {
            _series.Status = SeriesStatusType.Continuing;

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Future });

            VerifySeasonMonitored(2);
        }

        [Test]
        public void should_monitor_last_season_when_monitoring_future_and_series_upcoming()
        {
            _series.Status = SeriesStatusType.Upcoming;

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Future });

            VerifySeasonMonitored(2);
        }

        [Test]
        public void should_not_monitor_last_season_when_monitoring_future_and_series_ended()
        {
            _series.Status = SeriesStatusType.Ended;

            GivenMonitoredSeasons();

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Future });

            VerifySeasonNotMonitored(2);
        }

        [Test]
        public void should_not_monitor_first_season_when_monitoring_pilot()
        {
            GivenMonitoredSeasons(1);

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Pilot });

            VerifySeasonNotMonitored(1);
        }

        [Test]
        public void should_monitor_season_with_monitored_episodes()
        {
            GivenMonitoredSeasons(1);

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.Missing });

            VerifySeasonMonitored(1);
            VerifySeasonNotMonitored(2);
        }

        [Test]
        public void should_unmonitor_seasons_without_monitored_episodes()
        {
            GivenMonitoredSeasons();

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.None });

            VerifySeasonNotMonitored(0);
            VerifySeasonNotMonitored(1);
            VerifySeasonNotMonitored(2);
        }

        [Test]
        public void should_monitor_specials_season_when_specials_have_monitored_episodes()
        {
            GivenMonitoredSeasons(0);

            Subject.SetEpisodeMonitoredStatus(_series, new MonitoringOptions { Monitor = MonitorTypes.MonitorSpecials });

            VerifySeasonMonitored(0);
        }

        [Test]
        public void should_monitor_only_latest_revision_for_catalog_platform_when_1g1r_and_latest_revision_preference()
        {
            var catalogSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90001)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGameOneRom)
                .With(s => s.RevisionPreference = RevisionPreference.LatestRevision)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            var ep1 = new Episode { Id = 101, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Super Mario Bros. (USA) (v1.0)", Monitored = true };
            var ep2 = new Episode { Id = 102, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Super Mario Bros. (USA) (v1.1)", Monitored = true };
            var ep3 = new Episode { Id = 103, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "The Legend of Zelda (USA)", Monitored = true };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(catalogSeries.Id))
                  .Returns(new List<Episode> { ep1, ep2, ep3 });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(catalogSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            ep1.Monitored.Should().BeFalse();
            ep2.Monitored.Should().BeTrue();
            ep3.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_only_initial_release_for_catalog_platform_when_1g1r_and_initial_release_preference()
        {
            var catalogSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90001)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGameOneRom)
                .With(s => s.RevisionPreference = RevisionPreference.InitialRelease)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            var ep1 = new Episode { Id = 101, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Super Mario Bros. (USA) (v1.0)", Monitored = true };
            var ep2 = new Episode { Id = 102, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Super Mario Bros. (USA) (v1.1)", Monitored = true };
            var ep3 = new Episode { Id = 103, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "The Legend of Zelda (USA)", Monitored = true };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(catalogSeries.Id))
                  .Returns(new List<Episode> { ep1, ep2, ep3 });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(catalogSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            ep1.Monitored.Should().BeTrue();
            ep2.Monitored.Should().BeFalse();
            ep3.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_all_revisions_for_catalog_platform_when_preservationist_strategy()
        {
            var catalogSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90001)
                .With(s => s.CollectionStrategy = CollectionStrategy.Preservationist)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            var ep1 = new Episode { Id = 101, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Super Mario Bros. (USA) (v1.0)", Monitored = true };
            var ep2 = new Episode { Id = 102, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Super Mario Bros. (USA) (v1.1)", Monitored = true };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(catalogSeries.Id))
                  .Returns(new List<Episode> { ep1, ep2 });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(catalogSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            ep1.Monitored.Should().BeTrue();
            ep2.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_preserve_unmonitored_status_for_catalog_special_seasons_when_monitoring_all()
        {
            var catalogSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90001)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGameOneRom)
                .With(s => s.RevisionPreference = RevisionPreference.LatestRevision)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            var epPrototypes = new Episode { Id = 200, SeriesId = catalogSeries.Id, SeasonNumber = 0, Title = "Proto Game (USA)", Monitored = true };
            var epHomebrew = new Episode { Id = 205, SeriesId = catalogSeries.Id, SeasonNumber = 5, Title = "Homebrew Game (World)", Monitored = true };
            var epRomHacks = new Episode { Id = 206, SeriesId = catalogSeries.Id, SeasonNumber = 6, Title = "Rom Hack (World)", Monitored = true };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(catalogSeries.Id))
                  .Returns(new List<Episode> { epPrototypes, epHomebrew, epRomHacks });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(catalogSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            catalogSeries.Seasons.Single(s => s.SeasonNumber == 0).Monitored.Should().BeFalse();
            catalogSeries.Seasons.Single(s => s.SeasonNumber == 5).Monitored.Should().BeFalse();
            catalogSeries.Seasons.Single(s => s.SeasonNumber == 6).Monitored.Should().BeFalse();

            epPrototypes.Monitored.Should().BeFalse();
            epHomebrew.Monitored.Should().BeFalse();
            epRomHacks.Monitored.Should().BeFalse();
        }

        private void VerifySeasonMonitored(int seasonNumber)
        {
            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Single(n => n.SeasonNumber == seasonNumber).Monitored), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        private void VerifySeasonNotMonitored(int seasonNumber)
        {
            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => !s.Seasons.Single(n => n.SeasonNumber == seasonNumber).Monitored), It.IsAny<bool>(), It.IsAny<bool>()));
        }
    }
}
