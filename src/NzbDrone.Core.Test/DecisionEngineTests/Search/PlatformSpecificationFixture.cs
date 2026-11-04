using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications.Search;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests.Search
{
    [TestFixture]
    public class PlatformSpecificationFixture : TestBase<PlatformSpecification>
    {
        private Series _series1;
        private Series _series2;
        private RemoteEpisode _remoteEpisode;
        private SearchCriteriaBase _searchCriteria = new SingleEpisodeSearchCriteria();
        private ReleaseDecisionInformation _information;

        [SetUp]
        public void Setup()
        {
            _series1 = Builder<Series>.CreateNew().With(s => s.Id = 1).Build();
            _series2 = Builder<Series>.CreateNew().With(s => s.Id = 2).Build();

            _remoteEpisode = new RemoteEpisode { Series = _series1 };
            _information = new ReleaseDecisionInformation(false, _searchCriteria);
        }

        [Test]
        public void should_return_false_if_series_doesnt_match()
        {
            _searchCriteria.Series = _series2;

            Subject.IsSatisfiedBy(_remoteEpisode, _information).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_when_series_ids_match()
        {
            _searchCriteria.Series = _series1;

            Subject.IsSatisfiedBy(_remoteEpisode, _information).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_release_when_title_has_incorrect_platform()
        {
            _series1.Title = "Nintendo Switch";
            _series1.TitleSlug = "switch";
            _searchCriteria.Series = _series1;

            _remoteEpisode.Release = new ReleaseInfo
            {
                Title = "Super Mario Galaxy (Wii).iso"
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, _information);
            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.WrongSeries);
        }

        [Test]
        public void should_accept_release_when_title_has_no_platform_detected()
        {
            _series1.Title = "Nintendo Switch";
            _series1.TitleSlug = "switch";
            _searchCriteria.Series = _series1;

            _remoteEpisode.Release = new ReleaseInfo
            {
                Title = "Super Mario Galaxy (USA).zip"
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, _information);
            decision.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_release_when_title_matches_target_platform()
        {
            _series1.Title = "Nintendo Switch";
            _series1.TitleSlug = "switch";
            _searchCriteria.Series = _series1;

            _remoteEpisode.Release = new ReleaseInfo
            {
                Title = "Super Mario Galaxy [Nintendo Switch].nsp"
            };

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, _information);
            decision.Accepted.Should().BeTrue();
        }
    }
}
