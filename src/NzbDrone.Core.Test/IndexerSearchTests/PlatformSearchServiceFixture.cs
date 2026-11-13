using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class PlatformSearchServiceFixture : CoreTest<PlatformSearchService>
    {
        private Series _series;
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            _series = new Series
                      {
                          Id = 1,
                          Title = "Title",
                          Seasons = new List<Season>(),
                          QualityProfile = new LazyLoaded<QualityProfile>(Builder<QualityProfile>.CreateNew().With(q => q.UpgradeAllowed = true).Build())
                      };

            _episodes = new List<Episode>();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<int>()))
                  .Returns(_series);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                  .Returns(_episodes);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), false))
                  .ReturnsAsync(new List<DownloadDecision>());

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
        }

        [Test]
        public void should_only_include_monitored_seasons()
        {
            _series.Seasons = new List<Season>
                              {
                                  new Season { SeasonNumber = 0, Monitored = false },
                                  new Season { SeasonNumber = 1, Monitored = true }
                              };

            var gameSeason0 = new Episode { Id = 10, SeriesId = _series.Id, SeasonNumber = 0, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };
            var gameSeason1 = new Episode { Id = 20, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };

            _episodes.AddRange(new[] { gameSeason0, gameSeason1 });

            Subject.Execute(new PlatformSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.EpisodeSearch(gameSeason0, true, false), Times.Never);

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.EpisodeSearch(gameSeason1, true, false), Times.Once);
        }

        [Test]
        public void should_only_search_missing_games()
        {
            _series.Seasons = new List<Season>
            {
                new Season { SeasonNumber = 1, Monitored = true }
            };

            var missingGame = new Episode { Id = 1, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };
            var existingGame = new Episode { Id = 2, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true, EpisodeFileId = 1 };

            _episodes.AddRange(new[] { missingGame, existingGame });

            Subject.Execute(new PlatformSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.EpisodeSearch(missingGame, true, false), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.EpisodeSearch(existingGame, true, false), Times.Never);
        }

        [Test]
        public void should_start_with_lower_seasons_first()
        {
            var searchedGameIds = new List<int>();

            _series.Seasons = new List<Season>
                              {
                                  new Season { SeasonNumber = 3, Monitored = true },
                                  new Season { SeasonNumber = 1, Monitored = true },
                                  new Season { SeasonNumber = 2, Monitored = true }
                              };

            var gameSeason3 = new Episode { Id = 30, SeriesId = _series.Id, SeasonNumber = 3, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };
            var gameSeason1 = new Episode { Id = 10, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };
            var gameSeason2 = new Episode { Id = 20, SeriesId = _series.Id, SeasonNumber = 2, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };

            _episodes.AddRange(new[] { gameSeason3, gameSeason1, gameSeason2 });

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), true, false))
                  .ReturnsAsync(new List<DownloadDecision>())
                  .Callback<Episode, bool, bool>((episode, userInvokedSearch, interactiveSearch) => searchedGameIds.Add(episode.Id));

            Subject.Execute(new PlatformSearchCommand { SeriesId = _series.Id, Trigger = CommandTrigger.Manual });

            searchedGameIds.Should().ContainInOrder(10, 20, 30);
        }

        [Test]
        public void should_skip_single_rom_search_when_collection_first_grabs_release()
        {
            _series.Seasons = new List<Season> { new Season { SeasonNumber = 1, Monitored = true } };
            _episodes.Add(new Episode { Id = 10, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 });

            var collectionDecision = new DownloadDecision(null);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.PlatformCollectionSearch(_series.Id, false, false))
                  .ReturnsAsync(new List<DownloadDecision> { collectionDecision });

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision> { collectionDecision }, new List<DownloadDecision>(), new List<DownloadDecision>()));

            Subject.Execute(new PlatformSearchCommand { SeriesId = _series.Id, SearchCollectionFirst = true });

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.PlatformCollectionSearch(_series.Id, false, false), Times.Once());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_fall_back_to_single_rom_search_when_collection_first_grabs_nothing()
        {
            _series.Seasons = new List<Season> { new Season { SeasonNumber = 1, Monitored = true } };
            _episodes.Add(new Episode { Id = 10, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 });

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.PlatformCollectionSearch(_series.Id, false, false))
                  .ReturnsAsync(new List<DownloadDecision>());

            Subject.Execute(new PlatformSearchCommand { SeriesId = _series.Id, SearchCollectionFirst = true });

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.PlatformCollectionSearch(_series.Id, false, false), Times.Once());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(s => s.EpisodeSearch(It.Is<Episode>(e => e.Id == 10), false, false), Times.Once());
        }
    }
}
