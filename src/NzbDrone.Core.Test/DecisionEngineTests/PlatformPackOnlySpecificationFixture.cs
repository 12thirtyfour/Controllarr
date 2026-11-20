using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class PlatformPackOnlySpecificationFixture : CoreTest<PlatformPackOnlySpecification>
    {
        private RemoteEpisode _remoteEpisode;

        [SetUp]
        public void Setup()
        {
            var series = Builder<Series>.CreateNew()
                .With(s => s.SeriesType = SeriesTypes.Standard)
                .Build();

            var episode = Builder<Episode>.CreateNew().Build();

            _remoteEpisode = new RemoteEpisode
            {
                Series = series,
                Episodes = new List<Episode> { episode },
                ParsedEpisodeInfo = new ParsedEpisodeInfo { FullSeason = false },
                Release = new ReleaseInfo { Title = "Super Mario 64 (USA).zip" }
            };
        }

        [Test]
        public void should_reject_single_rom_during_platform_collection_search()
        {
            var criteria = new SeasonSearchCriteria
            {
                IsPlatformCollectionSearch = true
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation(false, criteria));

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.NotSeasonPack);
        }

        [Test]
        public void should_accept_full_set_collection_during_platform_collection_search()
        {
            _remoteEpisode.ParsedEpisodeInfo.FullSeason = true;
            var criteria = new SeasonSearchCriteria
            {
                IsPlatformCollectionSearch = true
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation(false, criteria));

            decision.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_collection_package_during_single_game_search()
        {
            _remoteEpisode.ParsedEpisodeInfo.FullSeason = true;
            var criteria = new SingleEpisodeSearchCriteria
            {
                Episodes = _remoteEpisode.Episodes
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation(false, criteria));

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.FullSeason);
        }

        [Test]
        public void should_accept_single_rom_during_single_game_search()
        {
            var criteria = new SingleEpisodeSearchCriteria
            {
                Episodes = _remoteEpisode.Episodes
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation(false, criteria));

            decision.Accepted.Should().BeTrue();
        }
    }
}
