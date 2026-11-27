using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Blocklisting
{
    [TestFixture]
    public class BlocklistServiceFixture : CoreTest<BlocklistService>
    {
        private DownloadFailedEvent _event;

        [SetUp]
        public void Setup()
        {
            _event = new DownloadFailedEvent
                     {
                         SeriesId = 12345,
                         EpisodeIds = new List<int> { 1 },
                         Quality = new QualityModel(Quality.NoIntroVerified),
                         SourceTitle = "series.title.s01e01",
                         DownloadClient = "SabnzbdClient",
                         DownloadId = "Sabnzbd_nzo_2dfh73k"
                     };

            _event.Data.Add("publishedDate", DateTime.UtcNow.ToString("s") + "Z");
            _event.Data.Add("size", "1000");
            _event.Data.Add("indexer", "nzbs.org");
            _event.Data.Add("protocol", "1");
            _event.Data.Add("message", "Marked as failed");
        }

        [Test]
        public void should_add_to_repository()
        {
            Subject.Handle(_event);

            Mocker.GetMock<IBlocklistRepository>()
                .Verify(v => v.Insert(It.Is<Blocklist>(b => b.EpisodeIds == _event.EpisodeIds)), Times.Once());
        }

        [Test]
        public void should_add_to_repository_missing_size_and_protocol()
        {
            Subject.Handle(_event);

            _event.Data.Remove("size");
            _event.Data.Remove("protocol");

            Mocker.GetMock<IBlocklistRepository>()
                .Verify(v => v.Insert(It.Is<Blocklist>(b => b.EpisodeIds == _event.EpisodeIds)), Times.Once());
        }

        [Test]
        public void should_detect_blocklisted_http_release_even_when_publish_date_differs_if_size_matches()
        {
            var blocklisted = new Blocklist
            {
                SeriesId = 10,
                SourceTitle = "Super Mario Bros. 2 (Japan).fds",
                Indexer = "Internet Archive",
                Protocol = DownloadProtocol.Http,
                Size = 131000,
                PublishedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            Mocker.GetMock<IBlocklistRepository>()
                .Setup(v => v.BlocklistedByTitle(10, "Super Mario Bros. 2 (Japan).fds"))
                .Returns(new List<Blocklist> { blocklisted });

            var release = new ReleaseInfo
            {
                Title = "Super Mario Bros. 2 (Japan).fds",
                Indexer = "Internet Archive",
                DownloadProtocol = DownloadProtocol.Http,
                Size = 131000,
                PublishDate = DateTime.UtcNow
            };

            Subject.Blocklisted(10, release).Should().BeTrue();
        }

        [Test]
        public void should_not_treat_different_sized_http_release_with_different_date_as_blocklisted()
        {
            var blocklisted = new Blocklist
            {
                SeriesId = 10,
                SourceTitle = "Super Mario Bros. 2 (Japan).fds",
                Indexer = "Internet Archive",
                Protocol = DownloadProtocol.Http,
                Size = 131000,
                PublishedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            Mocker.GetMock<IBlocklistRepository>()
                .Setup(v => v.BlocklistedByTitle(10, "Super Mario Bros. 2 (Japan).fds"))
                .Returns(new List<Blocklist> { blocklisted });

            var release = new ReleaseInfo
            {
                Title = "Super Mario Bros. 2 (Japan).fds",
                Indexer = "Internet Archive",
                DownloadProtocol = DownloadProtocol.Http,
                Size = 65500,
                PublishDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            };

            Subject.Blocklisted(10, release).Should().BeFalse();
        }

        [Test]
        public void should_detect_blocklisted_usenet_release_by_title_size_and_indexer()
        {
            var blocklisted = new Blocklist
            {
                SeriesId = 10,
                SourceTitle = "Super.Mario.Bros.2.JPN.FDS-GROUP",
                Indexer = "NZBgeek",
                Protocol = DownloadProtocol.Usenet,
                Size = 131000,
                PublishedDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local)
            };

            Mocker.GetMock<IBlocklistRepository>()
                .Setup(v => v.BlocklistedByTitle(10, "Super.Mario.Bros.2.JPN.FDS-GROUP"))
                .Returns(new List<Blocklist> { blocklisted });

            var release = new ReleaseInfo
            {
                Title = "Super.Mario.Bros.2.JPN.FDS-GROUP",
                Indexer = "NZBgeek",
                DownloadProtocol = DownloadProtocol.Usenet,
                Size = 131000,
                PublishDate = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            };

            Subject.Blocklisted(10, release).Should().BeTrue();
        }

        [Test]
        public void should_not_blocklist_when_skip_blocklist_is_true()
        {
            var downloadFailedEvent = new NzbDrone.Core.Download.DownloadFailedEvent
            {
                SeriesId = 10,
                EpisodeIds = new List<int> { 1 },
                SourceTitle = "Duplicate.Game.ROM",
                SkipBlocklist = true,
                Data = new Dictionary<string, string>
                {
                    { "size", "65500" },
                    { "protocol", ((int)DownloadProtocol.Http).ToString() }
                }
            };

            Subject.Handle(downloadFailedEvent);

            Mocker.GetMock<IBlocklistRepository>()
                .Verify(v => v.Insert(It.IsAny<Blocklist>()), Times.Never());
        }

        [Test]
        public void should_blocklist_when_skip_blocklist_is_false()
        {
            var downloadFailedEvent = new NzbDrone.Core.Download.DownloadFailedEvent
            {
                SeriesId = 10,
                EpisodeIds = new List<int> { 1 },
                SourceTitle = "Bad.Game.ROM",
                SkipBlocklist = false,
                Data = new Dictionary<string, string>
                {
                    { "size", "65500" },
                    { "protocol", ((int)DownloadProtocol.Http).ToString() }
                }
            };

            Subject.Handle(downloadFailedEvent);

            Mocker.GetMock<IBlocklistRepository>()
                .Verify(v => v.Insert(It.Is<Blocklist>(b => b.SourceTitle == "Bad.Game.ROM")), Times.Once());
        }
    }
}
