using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class RegionSearchServiceFixture : CoreTest<RegionSearchService>
    {
        private const int SeriesId = 1;
        private const int RegionNumber = 1;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), false))
                  .ReturnsAsync(new List<DownloadDecision>());

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
        }

        [Test]
        public void should_search_each_monitored_missing_game_item_by_item()
        {
            var missingGame1 = new Episode { Id = 1, SeriesId = SeriesId, SeasonNumber = RegionNumber, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };
            var missingGame2 = new Episode { Id = 2, SeriesId = SeriesId, SeasonNumber = RegionNumber, EpisodeNumber = 2, Monitored = true, EpisodeFileId = 0 };
            var existingGame = new Episode { Id = 3, SeriesId = SeriesId, SeasonNumber = RegionNumber, EpisodeNumber = 3, Monitored = true, EpisodeFileId = 10 };
            var unmonitoredGame = new Episode { Id = 4, SeriesId = SeriesId, SeasonNumber = RegionNumber, EpisodeNumber = 4, Monitored = false, EpisodeFileId = 0 };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodesBySeason(SeriesId, RegionNumber))
                  .Returns(new List<Episode> { missingGame1, missingGame2, existingGame, unmonitoredGame });

            Subject.Execute(new RegionSearchCommand { SeriesId = SeriesId, SeasonNumber = RegionNumber, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(missingGame1, true, false), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(missingGame2, true, false), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(existingGame, true, false), Times.Never);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(unmonitoredGame, true, false), Times.Never);
        }
    }
}
