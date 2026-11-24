using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Dat.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Test.DatTests
{
    [TestFixture]
    public class DatCatalogUpdateServiceFixture
    {
        private Mock<ISeriesService> _seriesServiceMock;
        private Mock<IGameCatalogService> _gameCatalogServiceMock;
        private Mock<IManageCommandQueue> _commandQueueMock;
        private Mock<IBiosService> _biosServiceMock;
        private DatCatalogUpdateService _subject;

        [SetUp]
        public void Setup()
        {
            _seriesServiceMock = new Mock<ISeriesService>();
            _gameCatalogServiceMock = new Mock<IGameCatalogService>();
            _commandQueueMock = new Mock<IManageCommandQueue>();
            _biosServiceMock = new Mock<IBiosService>();

            _subject = new DatCatalogUpdateService(
                _seriesServiceMock.Object,
                _gameCatalogServiceMock.Object,
                _commandQueueMock.Object,
                null,
                _biosServiceMock.Object);
        }

        [Test]
        public void should_sync_dat_catalogs_for_library_platforms_and_queue_series_refresh()
        {
            _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, TvdbId = 1001, Title = "Nintendo Entertainment System", SeasonType = "official" },
                new Series { Id = 2, TvdbId = 1012, Title = "Nintendo DS", SeasonType = "both" }
            });

            _gameCatalogServiceMock.Setup(g => g.SyncPlatformDatCatalogs(1001, true, "official")).Returns(120);
            _gameCatalogServiceMock.Setup(g => g.SyncPlatformDatCatalogs(1012, true, "both")).Returns(240);

            _subject.Execute(new UpdateDatCatalogsCommand());

            _gameCatalogServiceMock.Verify(g => g.SyncPlatformDatCatalogs(1001, true, "official"), Times.Once);
            _gameCatalogServiceMock.Verify(g => g.SyncPlatformDatCatalogs(1012, true, "both"), Times.Once);
            _commandQueueMock.Verify(
                q => q.Push(
                    It.Is<RefreshPlatformCommand>(c => c.SeriesIds.Contains(1) && c.SeriesIds.Contains(2)),
                    It.IsAny<CommandPriority>(),
                    It.IsAny<CommandTrigger>()),
                Times.Once);
            _biosServiceMock.Verify(b => b.SyncFirmwareUpdates(null), Times.Once);
        }

        [Test]
        public void should_mark_is_updating_during_dat_and_bios_sync_and_queue_refresh_afterward()
        {
            var updatingDuringDatSync = false;
            var updatingDuringBiosSync = false;
            bool? updatingWhenRefreshQueued = null;

            _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, TvdbId = 1001, Title = "Nintendo Entertainment System", SeasonType = "official" }
            });

            _gameCatalogServiceMock
                .Setup(g => g.SyncPlatformDatCatalogs(1001, true, "official"))
                .Callback(() => updatingDuringDatSync = DatCatalogUpdateService.IsUpdating)
                .Returns(120);

            _biosServiceMock
                .Setup(b => b.SyncFirmwareUpdates(null))
                .Callback(() => updatingDuringBiosSync = DatCatalogUpdateService.IsUpdating)
                .Returns(5);

            _commandQueueMock
                .Setup(q => q.Push(It.IsAny<RefreshPlatformCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()))
                .Callback(() => updatingWhenRefreshQueued = DatCatalogUpdateService.IsUpdating);

            _subject.Execute(new UpdateDatCatalogsCommand());

            updatingDuringDatSync.Should().BeTrue();
            updatingDuringBiosSync.Should().BeTrue();
            updatingWhenRefreshQueued.Should().BeFalse();
            DatCatalogUpdateService.IsUpdating.Should().BeFalse();
        }

        [Test]
        public void should_clear_is_updating_if_dat_sync_throws()
        {
            _seriesServiceMock.Setup(s => s.GetAllSeries()).Returns(new List<Series>
            {
                new Series { Id = 1, TvdbId = 1001, Title = "Nintendo Entertainment System", SeasonType = "official" }
            });

            _gameCatalogServiceMock
                .Setup(g => g.SyncPlatformDatCatalogs(1001, true, "official"))
                .Throws(new InvalidOperationException("Network error"));

            Assert.Throws<InvalidOperationException>(() => _subject.Execute(new UpdateDatCatalogsCommand()));

            DatCatalogUpdateService.IsUpdating.Should().BeFalse();
            _commandQueueMock.Verify(
                q => q.Push(It.IsAny<RefreshPlatformCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()),
                Times.Never);
        }
    }
}
