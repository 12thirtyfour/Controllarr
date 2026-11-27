using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class QueueServiceFixture : CoreTest<QueueService>
    {
        private List<TrackedDownload> _trackedDownloads;

        [SetUp]
        public void SetUp()
        {
            var downloadClientInfo = Builder<DownloadClientItemClientInfo>.CreateNew().Build();

            var downloadItem = Builder<NzbDrone.Core.Download.DownloadClientItem>.CreateNew()
                                        .With(v => v.RemainingTime = TimeSpan.FromSeconds(10))
                                        .With(v => v.DownloadClientInfo = downloadClientInfo)
                                        .Build();

            var series = Builder<Series>.CreateNew()
                                        .Build();

            var episodes = Builder<Episode>.CreateListOfSize(3)
                                          .All()
                                          .With(e => e.SeriesId = series.Id)
                                          .Build();

            var remoteEpisode = Builder<RemoteEpisode>.CreateNew()
                                                   .With(r => r.Series = series)
                                                   .With(r => r.Episodes = new List<Episode>(episodes))
                                                   .With(r => r.ParsedEpisodeInfo = new ParsedEpisodeInfo())
                                                   .Build();

            _trackedDownloads = Builder<TrackedDownload>.CreateListOfSize(1)
                .All()
                .With(v => v.IsTrackable = true)
                .With(v => v.DownloadItem = downloadItem)
                .With(v => v.RemoteEpisode = remoteEpisode)
                .Build()
                .ToList();
        }

        [Test]
        public void null_download_client_info_should_not_throw()
        {
            _trackedDownloads[0].DownloadItem.DownloadClientInfo = null;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();
            queue.Should().HaveCount(1);
            queue[0].DownloadClient.Should().BeEmpty();
            queue[0].DownloadClientHasPostImportCategory.Should().BeFalse();
        }

        [Test]
        public void queue_items_should_have_id()
        {
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();

            queue.Should().HaveCount(1);

            queue.All(v => v.Id > 0).Should().BeTrue();
        }

        [Test]
        public void should_map_queue_position_can_reorder_and_download_client_type()
        {
            _trackedDownloads[0].DownloadItem.DownloadClientInfo.Type = "InternetArchive";
            _trackedDownloads[0].DownloadItem.Status = DownloadItemStatus.Queued;
            _trackedDownloads[0].DownloadItem.QueuePosition = 2;
            _trackedDownloads[0].DownloadItem.CanReorder = true;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();
            queue.Should().ContainSingle();
            queue[0].DownloadClientType.Should().Be("InternetArchive");
            queue[0].QueuePosition.Should().Be(2);
            queue[0].CanReorder.Should().BeTrue();
        }

        [Test]
        public void update_queue_positions_should_update_matching_items_and_publish_event()
        {
            _trackedDownloads[0].DownloadItem.Status = DownloadItemStatus.Queued;
            _trackedDownloads[0].DownloadItem.QueuePosition = 3;
            _trackedDownloads[0].DownloadItem.CanReorder = true;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));
            Mocker.GetMock<IEventAggregator>().Invocations.Clear();

            var positions = new Dictionary<string, int?>
            {
                [_trackedDownloads[0].DownloadItem.DownloadId] = 1
            };

            Subject.UpdateQueuePositions(positions);

            var queue = Subject.GetQueue();
            queue[0].QueuePosition.Should().Be(1);
            queue[0].CanReorder.Should().BeTrue();
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<QueueUpdatedEvent>()), Times.Once());
        }
    }
}
