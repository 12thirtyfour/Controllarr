using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.Patching;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    [TestFixture]
    public class GameSearchServiceFixture : CoreTest<GameSearchService>
    {
        private const int SeriesId = 1;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), false))
                  .ReturnsAsync(new List<DownloadDecision>());

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<int>(), It.IsAny<bool>(), false))
                  .ReturnsAsync(new List<DownloadDecision>());

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        [Test]
        public void should_search_bulk_missing_games_item_by_item()
        {
            var game1 = new Episode { Id = 101, SeriesId = SeriesId, SeasonNumber = 1, EpisodeNumber = 1, Monitored = true, EpisodeFileId = 0 };
            var game2 = new Episode { Id = 102, SeriesId = SeriesId, SeasonNumber = 1, EpisodeNumber = 2, Monitored = true, EpisodeFileId = 0 };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(SeriesId))
                  .Returns(new List<Episode> { game1, game2 });

            Subject.Execute(new MissingGameSearchCommand(SeriesId) { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(game1, true, false), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(game2, true, false), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.SeasonSearch(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<List<Episode>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);

            Mocker.GetMock<IRomHackAutomationService>()
                  .Verify(v => v.TryAcquirePatchForGame(It.IsAny<Series>(), It.IsAny<Episode>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void should_search_game_by_ids()
        {
            var command = new GameSearchCommand(new List<int> { 1, 2 }) { Trigger = CommandTrigger.Manual };

            Subject.Execute(command);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(1, true, false), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(2, true, false), Times.Once);
        }

        [Test]
        public void should_use_patch_first_acquisition_for_rom_hack_and_skip_indexer_on_success()
        {
            var series = new Series { Id = SeriesId, Title = "Game Boy Advance", TitleSlug = "gba" };
            var hackGame = new Episode
            {
                Id = 601,
                SeriesId = SeriesId,
                SeasonNumber = GameCatalogProxy.RegionRomHacks,
                Title = "Pokemon Orange Islands (Hack)"
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(SeriesId))
                  .Returns(series);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(e => e.GetEpisode(601))
                  .Returns(hackGame);

            Mocker.GetMock<IRomHackAutomationService>()
                  .Setup(p => p.TryAcquirePatchForGame(series, hackGame, true))
                  .Returns(new RomHackAcquireResult
                  {
                      Success = true,
                      PatchFormat = "BPS",
                      Message = "Patched and imported."
                  });

            Subject.Execute(new GameSearchCommand(new List<int> { 601 }) { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<IRomHackAutomationService>()
                  .Verify(p => p.TryAcquirePatchForGame(series, hackGame, true), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(601, true, false), Times.Never);
        }

        [Test]
        public void should_fallback_to_indexer_search_when_rom_hack_patch_acquisition_fails()
        {
            var series = new Series { Id = SeriesId, Title = "Game Boy Advance", TitleSlug = "gba" };
            var hackGame = new Episode
            {
                Id = 602,
                SeriesId = SeriesId,
                SeasonNumber = GameCatalogProxy.RegionRomHacks,
                Title = "Pokemon Glazed (Hack)"
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(SeriesId))
                  .Returns(series);

            Mocker.GetMock<IEpisodeService>()
                  .Setup(e => e.GetEpisode(602))
                  .Returns(hackGame);

            Mocker.GetMock<IRomHackAutomationService>()
                  .Setup(p => p.TryAcquirePatchForGame(series, hackGame, true))
                  .Returns(new RomHackAcquireResult
                  {
                      Success = false,
                      Message = "No matching patch found."
                  });

            Subject.Execute(new GameSearchCommand(new List<int> { 602 }) { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<IRomHackAutomationService>()
                  .Verify(p => p.TryAcquirePatchForGame(series, hackGame, true), Times.Once);

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(602, true, false), Times.Once);
        }
    }
}
