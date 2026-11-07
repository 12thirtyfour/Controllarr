using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Games;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.PlatformTests
{
    [TestFixture]
    public class RefreshGameServiceFixture : CoreTest<RefreshGameService>
    {
        private List<Episode> _insertedEpisodes;
        private List<Episode> _updatedEpisodes;
        private List<Episode> _deletedEpisodes;
        private Tuple<Series, List<Episode>> _gameOfThrones;

        [OneTimeSetUp]
        public void TestFixture()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.Id = 121361)
                .With(s => s.Title = "Super Mario World")
                .With(s => s.CleanTitle = "supermarioworld")
                .With(s => s.Runtime = 30)
                .With(s => s.Status = SeriesStatusType.Continuing)
                .With(s => s.Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 2, Monitored = true }
                })
                .Build();

            var episodes = new List<Episode>();
            for (var season = 1; season <= 2; season++)
            {
                for (var ep = 1; ep <= 10; ep++)
                {
                    episodes.Add(new Episode
                    {
                        Id = ((season - 1) * 10) + ep,
                        SeriesId = series.Id,
                        SeasonNumber = season,
                        EpisodeNumber = ep,
                        Title = $"Episode {season}x{ep}",
                        AirDateUtc = DateTime.UtcNow.AddDays(-100 + (((season - 1) * 10) + ep)),
                        Monitored = true
                    });
                }
            }

            _gameOfThrones = new Tuple<Series, List<Episode>>(series, episodes);
        }

        private List<Episode> GetEpisodes()
        {
            return _gameOfThrones.Item2.JsonClone();
        }

        private Series GetSeries()
        {
            var series = _gameOfThrones.Item1.JsonClone();

            return series;
        }

        private Series GetAnimeSeries()
        {
            var series = Builder<Series>.CreateNew().Build();
            series.SeriesType = SeriesTypes.Anime;

            return series;
        }

        [SetUp]
        public void Setup()
        {
            _insertedEpisodes = new List<Episode>();
            _updatedEpisodes = new List<Episode>();
            _deletedEpisodes = new List<Episode>();

            Mocker.GetMock<IEpisodeService>().Setup(c => c.InsertMany(It.IsAny<List<Episode>>()))
                .Callback<List<Episode>>(e => _insertedEpisodes = e);

            Mocker.GetMock<IEpisodeService>().Setup(c => c.UpdateMany(It.IsAny<List<Episode>>()))
                .Callback<List<Episode>>(e => _updatedEpisodes = e);

            Mocker.GetMock<IEpisodeService>().Setup(c => c.DeleteMany(It.IsAny<List<Episode>>()))
                .Callback<List<Episode>>(e => _deletedEpisodes = e);
        }

        [Test]
        public void should_create_all_when_no_existing_episodes()
        {
            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Subject.RefreshEpisodeInfo(GetSeries(), GetEpisodes());

            _insertedEpisodes.Should().HaveSameCount(GetEpisodes());
            _updatedEpisodes.Should().BeEmpty();
            _deletedEpisodes.Should().BeEmpty();

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_update_all_when_all_existing_episodes()
        {
            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(GetEpisodes());

            Subject.RefreshEpisodeInfo(GetSeries(), GetEpisodes());

            _insertedEpisodes.Should().BeEmpty();
            _updatedEpisodes.Should().HaveSameCount(GetEpisodes());
            _deletedEpisodes.Should().BeEmpty();
        }

        [Test]
        public void should_delete_all_when_all_existing_episodes_are_gone_from_datasource()
        {
            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(GetEpisodes());

            Subject.RefreshEpisodeInfo(GetSeries(), new List<Episode>());

            _insertedEpisodes.Should().BeEmpty();
            _updatedEpisodes.Should().BeEmpty();
            _deletedEpisodes.Should().HaveSameCount(GetEpisodes());
        }

        [Test]
        public void should_delete_duplicated_episodes_based_on_season_episode_number()
        {
            var duplicateEpisodes = GetEpisodes().Skip(5).Take(2).ToList();

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(GetEpisodes().Union(duplicateEpisodes).ToList());

            Subject.RefreshEpisodeInfo(GetSeries(), GetEpisodes());

            _insertedEpisodes.Should().BeEmpty();
            _updatedEpisodes.Should().HaveSameCount(GetEpisodes());
            _deletedEpisodes.Should().HaveSameCount(duplicateEpisodes);
        }

        [Test]
        public void should_not_change_monitored_status_for_existing_episodes()
        {
            var series = GetSeries();
            series.Seasons = new List<Season>();
            series.Seasons.Add(new Season { SeasonNumber = 1, Monitored = false });

            var episodes = GetEpisodes();

            episodes.ForEach(e => e.Monitored = true);

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(episodes);

            Subject.RefreshEpisodeInfo(series, GetEpisodes());

            _updatedEpisodes.Should().HaveSameCount(GetEpisodes());
            _updatedEpisodes.Should().OnlyContain(e => e.Monitored == true);
        }

        [Test]
        public void should_not_set_monitored_status_for_old_episodes_to_false_if_episodes_existed()
        {
            var series = GetSeries();
            series.Seasons = new List<Season>();
            series.Seasons.Add(new Season { SeasonNumber = 1, Monitored = true });

            var episodes = GetEpisodes().OrderBy(v => v.SeasonNumber).ThenBy(v => v.EpisodeNumber).Take(5).ToList();

            episodes[1].AirDateUtc = DateTime.UtcNow.AddDays(-15);
            episodes[2].AirDateUtc = DateTime.UtcNow.AddDays(-10);
            episodes[3].AirDateUtc = DateTime.UtcNow.AddDays(1);

            var existingEpisodes = episodes.Skip(4).ToList();

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(existingEpisodes);

            Subject.RefreshEpisodeInfo(series, episodes);

            _insertedEpisodes = _insertedEpisodes.OrderBy(v => v.EpisodeNumber).ToList();

            _insertedEpisodes.Should().HaveCount(4);
            _insertedEpisodes[0].Monitored.Should().Be(true);
            _insertedEpisodes[1].Monitored.Should().Be(true);
            _insertedEpisodes[2].Monitored.Should().Be(true);
            _insertedEpisodes[3].Monitored.Should().Be(true);

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_remove_duplicate_remote_episodes_before_processing()
        {
            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            var episodes = Builder<Episode>.CreateListOfSize(5)
                                           .TheFirst(2)
                                           .With(e => e.SeasonNumber = 1)
                                           .With(e => e.EpisodeNumber = 1)
                                           .Build()
                                           .ToList();

            Subject.RefreshEpisodeInfo(GetSeries(), episodes);

            _insertedEpisodes.Should().HaveCount(episodes.Count - 1);
            _updatedEpisodes.Should().BeEmpty();
            _deletedEpisodes.Should().BeEmpty();
        }

        [Test]
        public void should_set_absolute_episode_number_for_anime()
        {
            var episodes = Builder<Episode>.CreateListOfSize(3).Build().ToList();

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Subject.RefreshEpisodeInfo(GetAnimeSeries(), episodes);

            _insertedEpisodes.All(e => e.AbsoluteEpisodeNumber.HasValue).Should().BeTrue();
            _updatedEpisodes.Should().BeEmpty();
            _deletedEpisodes.Should().BeEmpty();
        }

        [Test]
        public void should_set_absolute_episode_number_even_if_not_previously_set_for_anime()
        {
            var episodes = Builder<Episode>.CreateListOfSize(3).Build().ToList();

            var existingEpisodes = episodes.JsonClone();
            existingEpisodes.ForEach(e => e.AbsoluteEpisodeNumber = null);

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(existingEpisodes);

            Subject.RefreshEpisodeInfo(GetAnimeSeries(), episodes);

            _insertedEpisodes.Should().BeEmpty();
            _updatedEpisodes.All(e => e.AbsoluteEpisodeNumber.HasValue).Should().BeTrue();
            _deletedEpisodes.Should().BeEmpty();
        }

        [Test]
        public void should_ignore_episodes_with_no_absolute_episode_in_distinct_by_absolute()
        {
            var episodes = Builder<Episode>.CreateListOfSize(10)
                                           .Build()
                                           .ToList();

            episodes[0].AbsoluteEpisodeNumber = null;
            episodes[1].AbsoluteEpisodeNumber = null;
            episodes[2].AbsoluteEpisodeNumber = null;
            episodes[3].AbsoluteEpisodeNumber = null;
            episodes[4].AbsoluteEpisodeNumber = null;

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Subject.RefreshEpisodeInfo(GetAnimeSeries(), episodes);

            _insertedEpisodes.Should().HaveCount(episodes.Count);
        }

        [Test]
        public void should_use_tba_for_episode_title_when_null()
        {
            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            var episodes = Builder<Episode>.CreateListOfSize(1)
                                           .All()
                                           .With(e => e.Title = null)
                                           .Build()
                                           .ToList();

            Subject.RefreshEpisodeInfo(GetSeries(), episodes);

            _insertedEpisodes.First().Title.Should().Be("TBA");
        }

        [Test]
        public void should_match_anime_episodes_by_season_and_episode_numbers()
        {
            var episodes = Builder<Episode>.CreateListOfSize(2)
                .Build()
                .ToList();

            episodes[0].AbsoluteEpisodeNumber = null;
            episodes[0].SeasonNumber.Should().NotBe(episodes[1].SeasonNumber);
            episodes[0].EpisodeNumber.Should().NotBe(episodes[1].EpisodeNumber);

            var existingEpisode = new Episode
            {
                SeasonNumber = episodes[0].SeasonNumber,
                EpisodeNumber = episodes[0].EpisodeNumber,
                AbsoluteEpisodeNumber = episodes[1].AbsoluteEpisodeNumber
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode> { existingEpisode });

            Subject.RefreshEpisodeInfo(GetAnimeSeries(), episodes);

            _updatedEpisodes.First().SeasonNumber.Should().Be(episodes[0].SeasonNumber);
            _updatedEpisodes.First().EpisodeNumber.Should().Be(episodes[0].EpisodeNumber);
            _updatedEpisodes.First().AbsoluteEpisodeNumber.Should().Be(episodes[0].AbsoluteEpisodeNumber);

            _insertedEpisodes.First().SeasonNumber.Should().Be(episodes[1].SeasonNumber);
            _insertedEpisodes.First().EpisodeNumber.Should().Be(episodes[1].EpisodeNumber);
            _insertedEpisodes.First().AbsoluteEpisodeNumber.Should().Be(episodes[1].AbsoluteEpisodeNumber);
        }

        [Test]
        public void should_mark_updated_episodes_that_have_newly_added_absolute_episode_number()
        {
            var episodes = Builder<Episode>.CreateListOfSize(3)
                .Build()
                .ToList();

            var existingEpisodes = new List<Episode>
            {
                episodes[0],
                episodes[1]
            };

            existingEpisodes[0].AbsoluteEpisodeNumber = null;

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(existingEpisodes);

            Subject.RefreshEpisodeInfo(GetAnimeSeries(), episodes);

            _updatedEpisodes.First().SeasonNumber.Should().Be(episodes[1].SeasonNumber);
            _updatedEpisodes.First().EpisodeNumber.Should().Be(episodes[1].EpisodeNumber);
            _updatedEpisodes.First().AbsoluteEpisodeNumber.Should().NotBeNull();
            _updatedEpisodes.First().AbsoluteEpisodeNumberAdded.Should().BeTrue();

            _insertedEpisodes.Any(e => e.AbsoluteEpisodeNumberAdded).Should().BeFalse();
        }

        [Test]
        public void should_monitor_new_episode_if_season_is_monitored()
        {
            var series = GetSeries();
            series.Seasons = new List<Season>();
            series.Seasons.Add(new Season { SeasonNumber = 1, Monitored = true });

            var episodes = Builder<Episode>.CreateListOfSize(2)
                .All()
                .With(e => e.SeasonNumber = 1)
                .Build()
                .ToList();

            var existingEpisode = new Episode
            {
                SeasonNumber = episodes[0].SeasonNumber,
                EpisodeNumber = episodes[0].EpisodeNumber,
                Monitored = true
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode> { existingEpisode });

            Subject.RefreshEpisodeInfo(series, episodes);

            _updatedEpisodes.Should().HaveCount(1);
            _insertedEpisodes.Should().HaveCount(1);
            _insertedEpisodes.Should().OnlyContain(e => e.Monitored == true);
        }

        [Test]
        public void should_not_monitor_new_episode_if_season_is_not_monitored()
        {
            var series = GetSeries();
            series.Seasons = new List<Season>();
            series.Seasons.Add(new Season { SeasonNumber = 1, Monitored = false });

            var episodes = Builder<Episode>.CreateListOfSize(2)
                .All()
                .With(e => e.SeasonNumber = 1)
                .Build()
                .ToList();

            var existingEpisode = new Episode
            {
                SeasonNumber = episodes[0].SeasonNumber,
                EpisodeNumber = episodes[0].EpisodeNumber,
                Monitored = true
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode> { existingEpisode });

            Subject.RefreshEpisodeInfo(series, episodes);

            _updatedEpisodes.Should().HaveCount(1);
            _insertedEpisodes.Should().HaveCount(1);
            _insertedEpisodes.Should().OnlyContain(e => e.Monitored == false);
        }
        [Test]
        public void should_only_insert_preferred_newest_version_and_keep_all_discs_in_1g1r_mode()
        {
            var series = GetSeries();
            series.CollectionStrategy = CollectionStrategy.OneGameOneRom;

            var episodes = new List<Episode>
            {
                new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Final Fantasy VII (USA) (Disc 1)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 2, Title = "Final Fantasy VII (USA) (Disc 2)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 3, Title = "Final Fantasy VII (USA) (Disc 3)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 4, Title = "Final Fantasy VII (USA) (v1.1) (Disc 1)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 5, Title = "Final Fantasy VII (USA) (v1.1) (Disc 2)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 6, Title = "Final Fantasy VII (USA) (v1.1) (Disc 3)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 7, Title = "Final Fantasy VII (USA) (Update v1.2)" }
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Subject.RefreshEpisodeInfo(series, episodes);

            _insertedEpisodes.Should().HaveCount(4);
            _insertedEpisodes.Select(e => e.Title).Should().BeEquivalentTo(new[]
            {
                "Final Fantasy VII (USA) (v1.1) (Disc 1)",
                "Final Fantasy VII (USA) (v1.1) (Disc 2)",
                "Final Fantasy VII (USA) (v1.1) (Disc 3)",
                "Final Fantasy VII (USA) (Update v1.2)"
            });
        }

        [Test]
        public void should_insert_all_versions_when_in_completionist_mode()
        {
            var series = GetSeries();
            series.CollectionStrategy = CollectionStrategy.Preservationist;

            var episodes = new List<Episode>
            {
                new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Final Fantasy VII (USA) (Disc 1)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 2, Title = "Final Fantasy VII (USA) (Disc 2)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 3, Title = "Final Fantasy VII (USA) (Disc 3)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 4, Title = "Final Fantasy VII (USA) (v1.1) (Disc 1)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 5, Title = "Final Fantasy VII (USA) (v1.1) (Disc 2)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 6, Title = "Final Fantasy VII (USA) (v1.1) (Disc 3)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 7, Title = "Final Fantasy VII (USA) (Update v1.2)" }
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode>());

            Subject.RefreshEpisodeInfo(series, episodes);

            _insertedEpisodes.Should().HaveCount(7);
        }

        [Test]
        public void should_delete_older_versions_when_switching_to_1g1r_mode_unless_file_on_disk()
        {
            var series = GetSeries();
            series.CollectionStrategy = CollectionStrategy.OneGameOneRom;

            var oldVersionWithoutFile = new Episode
            {
                Id = 1,
                SeriesId = series.Id,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Crash Bandicoot (USA) (v1.0)",
                EpisodeFileId = 0
            };

            var oldVersionWithFile = new Episode
            {
                Id = 2,
                SeriesId = series.Id,
                SeasonNumber = 1,
                EpisodeNumber = 2,
                Title = "Crash Bandicoot (USA) (v1.1)",
                EpisodeFileId = 99
            };

            var newestVersion = new Episode
            {
                Id = 3,
                SeriesId = series.Id,
                SeasonNumber = 1,
                EpisodeNumber = 3,
                Title = "Crash Bandicoot (USA) (v1.2)",
                EpisodeFileId = 0
            };

            var existingEpisodes = new List<Episode> { oldVersionWithoutFile, oldVersionWithFile, newestVersion };
            var remoteEpisodes = new List<Episode>
            {
                new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Crash Bandicoot (USA) (v1.0)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 2, Title = "Crash Bandicoot (USA) (v1.1)" },
                new Episode { SeasonNumber = 1, EpisodeNumber = 3, Title = "Crash Bandicoot (USA) (v1.2)" }
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(existingEpisodes);

            Subject.RefreshEpisodeInfo(series, remoteEpisodes);

            _deletedEpisodes.Should().ContainSingle(e => e.Id == 1 && e.Title == "Crash Bandicoot (USA) (v1.0)");
            _updatedEpisodes.Select(e => e.Id).Should().Contain(new[] { 2, 3 });
        }

        [Test]
        public void should_prune_invalid_game_titles_like_en_world_during_refresh()
        {
            var series = GetSeries();
            var invalidGame = new Episode
            {
                Id = 999,
                SeriesId = series.Id,
                SeasonNumber = 4,
                EpisodeNumber = 1,
                Title = "(EN) (WORLD)",
                EpisodeFileId = 0
            };

            var validGame = new Episode
            {
                Id = 1000,
                SeriesId = series.Id,
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Super Mario World",
                EpisodeFileId = 0
            };

            Mocker.GetMock<IEpisodeService>().Setup(c => c.GetEpisodeBySeries(It.IsAny<int>()))
                .Returns(new List<Episode> { invalidGame, validGame });

            var remoteEpisodes = new List<Episode>
            {
                new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Super Mario World" }
            };

            Subject.RefreshEpisodeInfo(series, remoteEpisodes);

            _deletedEpisodes.Should().Contain(e => e.Id == 999 && e.Title == "(EN) (WORLD)");
            _updatedEpisodes.Should().Contain(e => e.Id == 1000 && e.Title == "Super Mario World");
        }
    }
}
