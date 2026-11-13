using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Games;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.PlatformTests.GameMonitoredServiceTests
{
    [TestFixture]
    public class SetGameMonitoredFixture : CoreTest<GameMonitoredService>
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
        public void should_mark_catalog_special_seasons_monitored_when_monitoring_all()
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

            catalogSeries.Seasons.Single(s => s.SeasonNumber == 0).Monitored.Should().BeTrue();
            catalogSeries.Seasons.Single(s => s.SeasonNumber == 5).Monitored.Should().BeTrue();
            catalogSeries.Seasons.Single(s => s.SeasonNumber == 6).Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_all_discs_for_selected_version_when_adding_multi_disc_game_with_1g1r()
        {
            var catalogSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90001)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGamePerRegionPerConsole)
                .With(s => s.RevisionPreference = RevisionPreference.LatestRevision)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            var epDisc1 = new Episode { Id = 301, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Sewer Shark", FinaleType = "Disc 1", Monitored = false };
            var epDisc2 = new Episode { Id = 302, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Sewer Shark", FinaleType = "Disc 2", Monitored = false };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(catalogSeries.Id))
                  .Returns(new List<Episode> { epDisc1, epDisc2 });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(catalogSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            epDisc1.Monitored.Should().BeTrue();
            epDisc2.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_all_discs_of_preferred_version_and_unmonitor_older_version_discs_for_multi_disc_game()
        {
            var catalogSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90001)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGamePerRegionPerConsole)
                .With(s => s.RevisionPreference = RevisionPreference.LatestRevision)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            var v1Disc1 = new Episode { Id = 401, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Night Trap", FinaleType = "Disc 1", Monitored = true };
            var v1Disc2 = new Episode { Id = 402, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Night Trap", FinaleType = "Disc 2", Monitored = true };
            var v2Disc1 = new Episode { Id = 403, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Night Trap", FinaleType = "Disc 1 • v1.1", Monitored = false };
            var v2Disc2 = new Episode { Id = 404, SeriesId = catalogSeries.Id, SeasonNumber = 1, Title = "Night Trap", FinaleType = "Disc 2 • v1.1", Monitored = false };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(catalogSeries.Id))
                  .Returns(new List<Episode> { v1Disc1, v1Disc2, v2Disc1, v2Disc2 });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(catalogSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            v1Disc1.Monitored.Should().BeFalse();
            v1Disc2.Monitored.Should().BeFalse();
            v2Disc1.Monitored.Should().BeTrue();
            v2Disc2.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_apply_1g1r_monitoring_for_retail_only_platforms_on_add()
        {
            var retailSeries = Builder<Series>.CreateNew()
                .With(s => s.TvdbId = 90002)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGameOneRom)
                .With(s => s.RevisionPreference = RevisionPreference.LatestRevision)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 2, Monitored = true }
                })
                .Build();

            var usaV1 = new Episode { Id = 501, SeriesId = retailSeries.Id, SeasonNumber = 1, Title = "Sonic The Hedgehog (USA)", FinaleType = "Retail (v1.0)", Monitored = true };
            var usaV2 = new Episode { Id = 502, SeriesId = retailSeries.Id, SeasonNumber = 1, Title = "Sonic The Hedgehog (USA) (Rev 1)", FinaleType = "Rev 1", Monitored = false };
            var eurV1 = new Episode { Id = 503, SeriesId = retailSeries.Id, SeasonNumber = 2, Title = "Sonic The Hedgehog (Europe)", FinaleType = "Retail (v1.0)", Monitored = true };

            var episodes = new List<Episode> { usaV1, usaV2, eurV1 };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(retailSeries.Id))
                  .Returns(episodes);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1, 2 });

            Subject.SetEpisodeMonitoredStatus(retailSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            usaV1.Monitored.Should().BeFalse();
            usaV2.Monitored.Should().BeTrue();
            eurV1.Monitored.Should().BeFalse();
            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(episodes), Times.Once());
        }

        [Test]
        public void should_monitor_both_preferred_base_game_and_preferred_update_in_1g1r()
        {
            var retailSeries = Builder<Series>.CreateNew()
                .With(s => s.Id = 77)
                .With(s => s.CollectionStrategy = CollectionStrategy.OneGameOneRom)
                .With(s => s.RevisionPreference = RevisionPreference.LatestRevision)
                .With(s => s.EncryptionPreference = EncryptionPreference.NotEncrypted)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true }
                })
                .Build();

            var baseV1 = new Episode { Id = 601, SeriesId = retailSeries.Id, SeasonNumber = 1, Title = "Pokemon Moon (USA)", FinaleType = "Retail (v1.0)", Monitored = true };
            var baseV2 = new Episode { Id = 602, SeriesId = retailSeries.Id, SeasonNumber = 1, Title = "Pokemon Moon (USA) (Rev 1)", FinaleType = "Rev 1", Monitored = false };
            var updV1 = new Episode { Id = 603, SeriesId = retailSeries.Id, SeasonNumber = 1, Title = "Pokemon Moon (USA) (Update v1.1)", FinaleType = "Update v1.1", Monitored = false };
            var updV2 = new Episode { Id = 604, SeriesId = retailSeries.Id, SeasonNumber = 1, Title = "Pokemon Moon (USA) (Update v1.2)", FinaleType = "Update v1.2", Monitored = false };

            var episodes = new List<Episode> { baseV1, baseV2, updV1, updV2 };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(retailSeries.Id))
                  .Returns(episodes);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(It.IsAny<int>(), It.IsAny<MonitorTypes>(), It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1 });

            Subject.SetEpisodeMonitoredStatus(retailSeries, new MonitoringOptions { Monitor = MonitorTypes.All });

            baseV1.Monitored.Should().BeFalse();
            baseV2.Monitored.Should().BeTrue();
            updV1.Monitored.Should().BeFalse();
            updV2.Monitored.Should().BeTrue();

            Mocker.GetMock<IEpisodeService>()
                  .Verify(v => v.UpdateEpisodes(episodes), Times.Once());
        }

        [Test]
        public void should_monitor_only_default_region_from_ui_settings()
        {
            var platform = Builder<Series>.CreateNew()
                .With(s => s.CollectionStrategy = CollectionStrategy.Preservationist)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = false },
                    new Season { SeasonNumber = 2, Monitored = false },
                    new Season { SeasonNumber = 3, Monitored = false },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            Mocker.GetMock<NzbDrone.Core.Configuration.IConfigService>()
                  .SetupGet(c => c.ConsoleRegion)
                  .Returns("EU");

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(platform.Id, MonitorTypes.DefaultRegion, 2, It.IsAny<int>()))
                  .Returns(new List<int> { 2 });

            Subject.SetEpisodeMonitoredStatus(platform, new MonitoringOptions { Monitor = MonitorTypes.DefaultRegion });

            VerifySeasonNotMonitored(0);
            VerifySeasonNotMonitored(1);
            VerifySeasonMonitored(2);
            VerifySeasonNotMonitored(3);
            VerifySeasonNotMonitored(5);
            VerifySeasonNotMonitored(6);
        }

        [Test]
        public void should_monitor_only_retail_regions_for_all_retail()
        {
            var platform = Builder<Series>.CreateNew()
                .With(s => s.CollectionStrategy = CollectionStrategy.Preservationist)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = true },
                    new Season { SeasonNumber = 1, Monitored = false },
                    new Season { SeasonNumber = 2, Monitored = false },
                    new Season { SeasonNumber = 3, Monitored = false },
                    new Season { SeasonNumber = 4, Monitored = false },
                    new Season { SeasonNumber = 5, Monitored = true },
                    new Season { SeasonNumber = 6, Monitored = true }
                })
                .Build();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(platform.Id, MonitorTypes.AllRetail, It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 1, 2, 3, 4 });

            Subject.SetEpisodeMonitoredStatus(platform, new MonitoringOptions { Monitor = MonitorTypes.AllRetail });

            VerifySeasonNotMonitored(0);
            VerifySeasonMonitored(1);
            VerifySeasonMonitored(2);
            VerifySeasonMonitored(3);
            VerifySeasonMonitored(4);
            VerifySeasonNotMonitored(5);
            VerifySeasonNotMonitored(6);
        }

        [Test]
        public void should_monitor_only_non_retail_regions_for_non_retail()
        {
            var platform = Builder<Series>.CreateNew()
                .With(s => s.CollectionStrategy = CollectionStrategy.Preservationist)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 0, Monitored = false },
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 2, Monitored = true },
                    new Season { SeasonNumber = 3, Monitored = true },
                    new Season { SeasonNumber = 4, Monitored = true },
                    new Season { SeasonNumber = 5, Monitored = false },
                    new Season { SeasonNumber = 6, Monitored = false }
                })
                .Build();

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.SetEpisodeMonitoredBySeries(platform.Id, MonitorTypes.NonRetail, It.IsAny<int>(), It.IsAny<int>()))
                  .Returns(new List<int> { 0, 5, 6 });

            Subject.SetEpisodeMonitoredStatus(platform, new MonitoringOptions { Monitor = MonitorTypes.NonRetail });

            VerifySeasonMonitored(0);
            VerifySeasonNotMonitored(1);
            VerifySeasonNotMonitored(2);
            VerifySeasonNotMonitored(3);
            VerifySeasonNotMonitored(4);
            VerifySeasonMonitored(5);
            VerifySeasonMonitored(6);
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
