using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.CustomFormats;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class CustomFormatAllowedByProfileSpecificationFixture : CoreTest<CustomFormatAllowedbyProfileSpecification>
    {
        private RemoteEpisode _remoteEpisode;
        private CustomFormat _format1;
        private CustomFormat _format2;

        [SetUp]
        public void Setup()
        {
            _remoteEpisode = new RemoteEpisode
            {
                Series = new Series
                {
                    QualityProfile = new QualityProfile
                    {
                        MinFormatScore = 10,
                        FormatItems = new List<ProfileFormatItem>()
                    }
                }
            };

            _format1 = new CustomFormat("Format 1") { Id = 1 };
            _format2 = new CustomFormat("Format 2") { Id = 2 };

            CustomFormatsTestHelpers.GivenCustomFormats(_format1, _format2);
        }

        [Test]
        public void should_allow_if_format_score_greater_than_min()
        {
            _remoteEpisode.CustomFormats = new List<CustomFormat> { _format1 };
            _remoteEpisode.Series.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(_format1.Name);
            _remoteEpisode.CustomFormatScore = _remoteEpisode.Series.QualityProfile.Value.CalculateCustomFormatScore(_remoteEpisode.CustomFormats);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_allow_all_game_releases_even_when_format_score_not_greater_than_min()
        {
            _remoteEpisode.CustomFormats = new List<CustomFormat> { _format2 };
            _remoteEpisode.Series.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(_format1.Name);
            _remoteEpisode.CustomFormatScore = _remoteEpisode.Series.QualityProfile.Value.CalculateCustomFormatScore(_remoteEpisode.CustomFormats);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_allow_all_game_releases_when_multiple_formats_present()
        {
            _remoteEpisode.CustomFormats = new List<CustomFormat> { _format2, _format1 };
            _remoteEpisode.Series.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(_format1.Name);
            _remoteEpisode.CustomFormatScore = _remoteEpisode.Series.QualityProfile.Value.CalculateCustomFormatScore(_remoteEpisode.CustomFormats);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_allow_if_all_format_is_defined_in_profile()
        {
            _remoteEpisode.CustomFormats = new List<CustomFormat> { _format2, _format1 };
            _remoteEpisode.Series.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(_format1.Name, _format2.Name);
            _remoteEpisode.CustomFormatScore = _remoteEpisode.Series.QualityProfile.Value.CalculateCustomFormatScore(_remoteEpisode.CustomFormats);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_allow_game_releases_even_if_no_format_was_parsed()
        {
            _remoteEpisode.CustomFormats = new List<CustomFormat>();
            _remoteEpisode.Series.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(_format1.Name, _format2.Name);
            _remoteEpisode.CustomFormatScore = _remoteEpisode.Series.QualityProfile.Value.CalculateCustomFormatScore(_remoteEpisode.CustomFormats);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_allow_if_no_format_was_parsed_min_score_is_zero()
        {
            _remoteEpisode.CustomFormats = new List<CustomFormat>();
            _remoteEpisode.Series.QualityProfile.Value.FormatItems = CustomFormatsTestHelpers.GetSampleFormatItems(_format1.Name, _format2.Name);
            _remoteEpisode.Series.QualityProfile.Value.MinFormatScore = 0;
            _remoteEpisode.CustomFormatScore = _remoteEpisode.Series.QualityProfile.Value.CalculateCustomFormatScore(_remoteEpisode.CustomFormats);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }
    }
}
