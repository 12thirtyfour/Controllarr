using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Games;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class GameCollectionStrategyFixture
    {
        private DownloadDecisionComparer _comparer;
        private DownloadDecision _decisionPinned;
        private DownloadDecision _decisionLatestRev;
        private DownloadDecision _decisionInitialRelease;
        private Mock<IDelayProfileService> _delayProfileServiceMock;

        [SetUp]
        public void Setup()
        {
            var configServiceMock = new Mock<IConfigService>();
            configServiceMock.Setup(c => c.DownloadPropersAndRepacks).Returns(ProperDownloadTypes.PreferAndUpgrade);

            _delayProfileServiceMock = new Mock<IDelayProfileService>();
            _delayProfileServiceMock.Setup(c => c.BestForTags(It.IsAny<HashSet<int>>())).Returns(new DelayProfile { PreferredProtocol = DownloadProtocol.Usenet });

            _comparer = new DownloadDecisionComparer(configServiceMock.Object, _delayProfileServiceMock.Object);

            var profile = new QualityProfile
            {
                Items = new List<QualityProfileQualityItem>
                {
                    new QualityProfileQualityItem { Quality = Quality.Unknown, Allowed = true }
                }
            };

            var lazyProfile = new LazyLoaded<QualityProfile>(profile);

            var seriesPinned = new Series { CollectionStrategy = CollectionStrategy.OneGameOneRom, RevisionPreference = RevisionPreference.LatestRevision, PinnedVersion = "PinnedRev", QualityProfile = lazyProfile };

            var seriesLatest = new Series { CollectionStrategy = CollectionStrategy.OneGameOneRom, RevisionPreference = RevisionPreference.LatestRevision, QualityProfile = lazyProfile };

            var seriesInitial = new Series { CollectionStrategy = CollectionStrategy.OneGameOneRom, RevisionPreference = RevisionPreference.InitialRelease, QualityProfile = lazyProfile };

            _decisionPinned = new DownloadDecision(new RemoteEpisode
            {
                Series = seriesPinned,
                ParsedRomInfo = new ParsedRomInfo { Revision = "PinnedRev", Version = "1.0" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            _decisionLatestRev = new DownloadDecision(new RemoteEpisode
            {
                Series = seriesLatest,
                ParsedRomInfo = new ParsedRomInfo { Revision = "RevB" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            _decisionInitialRelease = new DownloadDecision(new RemoteEpisode
            {
                Series = seriesInitial,
                ParsedRomInfo = new ParsedRomInfo { Revision = null }, // no revision means initial release
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });
        }

        [Test]
        public void should_prioritize_pinned_version_over_others()
        {
            var otherDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = _decisionPinned.RemoteEpisode.Series,
                ParsedRomInfo = new ParsedRomInfo { Revision = "OtherRev" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            _comparer.Compare(_decisionPinned, otherDecision).Should().BeGreaterThan(0); // Pinned wins under OrderByDescending
            _comparer.Compare(otherDecision, _decisionPinned).Should().BeLessThan(0);
        }

        [Test]
        public void should_prioritize_latest_revision_when_preference_is_latest()
        {
            var olderDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = _decisionLatestRev.RemoteEpisode.Series,
                ParsedRomInfo = new ParsedRomInfo { Revision = "RevA" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            _comparer.Compare(_decisionLatestRev, olderDecision).Should().BeGreaterThan(0); // RevB wins over RevA under OrderByDescending
        }

        [Test]
        public void should_prioritize_initial_release_when_preference_is_initial()
        {
            var laterDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = _decisionInitialRelease.RemoteEpisode.Series,
                ParsedRomInfo = new ParsedRomInfo { Revision = "RevA" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            _comparer.Compare(_decisionInitialRelease, laterDecision).Should().BeGreaterThan(0); // Initial (null) wins over RevA under OrderByDescending
        }

        [Test]
        public void should_prioritize_retail_over_prerelease_and_sort_winner_first_in_prioritization_service()
        {
            var configServiceMock = new Mock<IConfigService>();
            configServiceMock.Setup(c => c.DownloadPropersAndRepacks).Returns(ProperDownloadTypes.PreferAndUpgrade);
            var prioritizationService = new DownloadDecisionPriorizationService(configServiceMock.Object, _delayProfileServiceMock.Object);

            var series = new Series
            {
                Id = 1,
                CollectionStrategy = CollectionStrategy.OneGameOneRom,
                RevisionPreference = RevisionPreference.LatestRevision,
                PinnedVersion = "1.1",
                QualityProfile = _decisionLatestRev.RemoteEpisode.Series.QualityProfile
            };

            var betaDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = series,
                ParsedRomInfo = new ParsedRomInfo { DumpTags = new List<string> { "Beta" }, ReleaseTitle = "Game (USA) (Beta).sfc" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            var unpinnedRev0Decision = new DownloadDecision(new RemoteEpisode
            {
                Series = series,
                ParsedRomInfo = new ParsedRomInfo { ReleaseTitle = "Game (USA).sfc" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            var pinnedDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = series,
                ParsedRomInfo = new ParsedRomInfo { Version = "1.1", ReleaseTitle = "Game (USA) (v1.1).sfc" },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            var sorted = prioritizationService.PrioritizeDecisions(new List<DownloadDecision>
            {
                betaDecision,
                unpinnedRev0Decision,
                pinnedDecision
            });

            sorted[0].Should().BeSameAs(pinnedDecision);
            sorted[1].Should().BeSameAs(unpinnedRev0Decision);
            sorted[2].Should().BeSameAs(betaDecision);
        }

        [Test]
        public void should_rank_protocols_by_protocol_priority_list()
        {
            _delayProfileServiceMock
                .Setup(c => c.BestForTags(It.IsAny<HashSet<int>>()))
                .Returns(new DelayProfile
                {
                    EnableHttp = true,
                    EnableUsenet = true,
                    EnableTorrent = true,
                    ProtocolPriority = new List<DownloadProtocol>
                    {
                        DownloadProtocol.Http,
                        DownloadProtocol.Usenet,
                        DownloadProtocol.Torrent
                    }
                });

            var httpDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = _decisionLatestRev.RemoteEpisode.Series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Http }
            });

            var usenetDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = _decisionLatestRev.RemoteEpisode.Series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Usenet }
            });

            var torrentDecision = new DownloadDecision(new RemoteEpisode
            {
                Series = _decisionLatestRev.RemoteEpisode.Series,
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.Unknown) },
                Release = new ReleaseInfo { DownloadProtocol = DownloadProtocol.Torrent }
            });

            _comparer.Compare(httpDecision, usenetDecision).Should().BeGreaterThan(0);
            _comparer.Compare(usenetDecision, torrentDecision).Should().BeGreaterThan(0);
            _comparer.Compare(httpDecision, torrentDecision).Should().BeGreaterThan(0);
        }
    }
}
