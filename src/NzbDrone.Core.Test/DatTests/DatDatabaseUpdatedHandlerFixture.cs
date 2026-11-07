using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Dat.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Test.DatTests
{
    [TestFixture]
    public class DatDatabaseUpdatedHandlerFixture
    {
        private Mock<ISeriesService> _seriesServiceMock;
        private Mock<IGameCatalogService> _gameCatalogServiceMock;
        private Mock<IManageCommandQueue> _commandQueueMock;
        private DatDatabaseUpdatedHandler _subject;

        [SetUp]
        public void Setup()
        {
            _seriesServiceMock = new Mock<ISeriesService>();
            _gameCatalogServiceMock = new Mock<IGameCatalogService>();
            _commandQueueMock = new Mock<IManageCommandQueue>();

            _subject = new DatDatabaseUpdatedHandler(
                _seriesServiceMock.Object,
                _gameCatalogServiceMock.Object,
                _commandQueueMock.Object,
                NLog.LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void should_refresh_matching_platform_when_dat_database_is_updated()
        {
            _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 42, TvdbId = 1003, Title = "Super Nintendo Entertainment System", TitleSlug = "snes" },
                new Series { Id = 43, TvdbId = 1014, Title = "Nintendo Switch", TitleSlug = "switch" }
            });

            var datFile = new DatFile
            {
                Id = 1,
                Name = "Nintendo - Super Nintendo Entertainment System (20261106-120000).dat",
                Description = "snes"
            };

            _subject.Handle(new DatDatabaseUpdatedEvent(datFile, 150));

            _gameCatalogServiceMock.Verify(g => g.InvalidatePlatformCache(1003), Times.Once);
            _commandQueueMock.Verify(
                q => q.Push(
                    It.Is<RefreshPlatformCommand>(c => c.SeriesIds.Contains(42)),
                    It.IsAny<CommandPriority>(),
                    It.IsAny<CommandTrigger>()),
                Times.Once);
        }

        [Test]
        public void should_not_refresh_when_no_matching_platform_in_library()
        {
            _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 42, TvdbId = 1003, Title = "Super Nintendo Entertainment System", TitleSlug = "snes" }
            });

            var datFile = new DatFile
            {
                Id = 2,
                Name = "Sega - Mega Drive - Genesis",
                Description = "genesis"
            };

            _subject.Handle(new DatDatabaseUpdatedEvent(datFile, 80));

            _gameCatalogServiceMock.Verify(g => g.InvalidatePlatformCache(It.IsAny<int?>()), Times.Never);
            _commandQueueMock.Verify(
                q => q.Push(
                    It.IsAny<RefreshPlatformCommand>(),
                    It.IsAny<CommandPriority>(),
                    It.IsAny<CommandTrigger>()),
                Times.Never);
        }
    }
}
