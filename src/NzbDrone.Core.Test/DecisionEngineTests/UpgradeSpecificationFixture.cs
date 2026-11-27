using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]

    public class UpgradeSpecificationFixture : CoreTest<UpgradableSpecification>
    {
        public static object[] IsUpgradeTestCases =
        {
            new object[] { Quality.BetaProto, 1, Quality.BetaProto, 2, Quality.BetaProto, UpgradeableRejectReason.None },
            new object[] { Quality.VerifiedGoodDump, 1, Quality.VerifiedGoodDump, 2, Quality.VerifiedGoodDump, UpgradeableRejectReason.None },
            new object[] { Quality.BetaProto, 1, Quality.BetaProto, 1, Quality.BetaProto, UpgradeableRejectReason.CustomFormatScore },
            new object[] { Quality.VerifiedGoodDump, 1, Quality.UnverifiedRaw, 2, Quality.NoIntroVerified, UpgradeableRejectReason.BetterQuality },
            new object[] { Quality.VerifiedGoodDump, 1, Quality.UnverifiedRaw, 2, Quality.VerifiedGoodDump, UpgradeableRejectReason.BetterQuality },
            new object[] { Quality.VerifiedGoodDump, 1, Quality.VerifiedGoodDump, 1, Quality.VerifiedGoodDump, UpgradeableRejectReason.CustomFormatScore },
            new object[] { Quality.VerifiedGoodDump, 1, Quality.VerifiedGoodDump, 1, Quality.VerifiedGoodDump, UpgradeableRejectReason.CustomFormatScore }
        };

        private void GivenAutoDownloadPropers(ProperDownloadTypes type)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.DownloadPropersAndRepacks)
                  .Returns(type);
        }

        [Test]
        [TestCaseSource(nameof(IsUpgradeTestCases))]
        public void IsUpgradeTest(Quality current, int currentVersion, Quality newQuality, int newVersion, Quality cutoff, UpgradeableRejectReason expected)
        {
            GivenAutoDownloadPropers(ProperDownloadTypes.PreferAndUpgrade);

            var profile = new QualityProfile
            {
                UpgradeAllowed = true,
                Items = Qualities.QualityFixture.GetDefaultQualities()
            };

            Subject.IsUpgradable(
                       profile,
                       new QualityModel(current, new Revision(version: currentVersion)),
                       new List<CustomFormat>(),
                       new QualityModel(newQuality, new Revision(version: newVersion)),
                       new List<CustomFormat>())
                    .Should().Be(expected);
        }

        [Test]
        public void should_return_true_if_proper_and_download_propers_is_do_not_download()
        {
            GivenAutoDownloadPropers(ProperDownloadTypes.DoNotUpgrade);

            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(),
            };

            Subject.IsUpgradable(
                       profile,
                       new QualityModel(Quality.TranslationPatch, new Revision(version: 1)),
                       new List<CustomFormat>(),
                       new QualityModel(Quality.TranslationPatch, new Revision(version: 2)),
                       new List<CustomFormat>())
                    .Should().Be(UpgradeableRejectReason.None);
        }

        [Test]
        public void should_return_false_if_proper_and_autoDownloadPropers_is_do_not_prefer()
        {
            GivenAutoDownloadPropers(ProperDownloadTypes.DoNotPrefer);

            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(),
            };

            Subject.IsUpgradable(
                       profile,
                       new QualityModel(Quality.TranslationPatch, new Revision(version: 1)),
                       new List<CustomFormat>(),
                       new QualityModel(Quality.TranslationPatch, new Revision(version: 2)),
                       new List<CustomFormat>())
                   .Should().Be(UpgradeableRejectReason.UpgradesNotAllowed);
        }

        [Test]
        public void should_return_false_if_release_and_existing_file_are_the_same()
        {
            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities()
            };

            Subject.IsUpgradable(
                       profile,
                       new QualityModel(Quality.UnverifiedRaw, new Revision(version: 1)),
                       new List<CustomFormat>(),
                       new QualityModel(Quality.UnverifiedRaw, new Revision(version: 1)),
                       new List<CustomFormat>())
                   .Should().Be(UpgradeableRejectReason.UpgradesNotAllowed);
        }

        [Test]
        public void should_return_true_if_release_has_higher_quality_and_cutoff_is_not_already_met()
        {
            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                UpgradeAllowed = true,
                Cutoff = Quality.VerifiedGoodDump.Id
            };

            Subject.IsUpgradable(
                    profile,
                    new QualityModel(Quality.UnverifiedRaw, new Revision(version: 1)),
                    new List<CustomFormat>(),
                    new QualityModel(Quality.NoIntroVerified, new Revision(version: 1)),
                    new List<CustomFormat>())
                .Should().Be(UpgradeableRejectReason.None);
        }

        [Test]
        public void should_return_false_if_release_has_higher_quality_and_cutoff_is_already_met()
        {
            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                UpgradeAllowed = true,
                Cutoff = Quality.UnverifiedRaw.Id
            };

            Subject.IsUpgradable(
                    profile,
                    new QualityModel(Quality.VerifiedGoodDump, new Revision(version: 1)),
                    new List<CustomFormat>(),
                    new QualityModel(Quality.NoIntroVerified, new Revision(version: 1)),
                    new List<CustomFormat>())
                .Should().Be(UpgradeableRejectReason.QualityCutoff);
        }

        [Test]
        public void should_return_false_if_minimum_custom_score_is_not_met()
        {
            var customFormatOne = new CustomFormat
            {
                Id = 1,
                Name = "One"
            };

            var customFormatTwo = new CustomFormat
            {
                Id = 2,
                Name = "Two"
            };

            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                UpgradeAllowed = true,
                MinUpgradeFormatScore = 11,
                CutoffFormatScore = 100,
                FormatItems = new List<ProfileFormatItem>
                {
                    new ProfileFormatItem
                    {
                        Format = customFormatOne,
                        Score = 10
                    },
                    new ProfileFormatItem
                    {
                        Format = customFormatTwo,
                        Score = 20
                    }
                }
            };

            Subject.IsUpgradable(
                    profile,
                    new QualityModel(Quality.TranslationPatch),
                    new List<CustomFormat> { customFormatOne },
                    new QualityModel(Quality.TranslationPatch),
                    new List<CustomFormat> { customFormatTwo })
                .Should().Be(UpgradeableRejectReason.MinCustomFormatScore);
        }

        [Test]
        public void should_return_true_if_minimum_custom_score_is_met()
        {
            var customFormatOne = new CustomFormat
            {
                Id = 1,
                Name = "One"
            };

            var customFormatTwo = new CustomFormat
            {
                Id = 2,
                Name = "Two"
            };

            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(),
                UpgradeAllowed = true,
                MinUpgradeFormatScore = 10,
                CutoffFormatScore = 100,
                FormatItems = new List<ProfileFormatItem>
                {
                    new ProfileFormatItem
                    {
                        Format = customFormatOne,
                        Score = 10
                    },
                    new ProfileFormatItem
                    {
                        Format = customFormatTwo,
                        Score = 20
                    }
                }
            };

            Subject.IsUpgradable(
                    profile,
                    new QualityModel(Quality.TranslationPatch),
                    new List<CustomFormat> { customFormatOne },
                    new QualityModel(Quality.TranslationPatch),
                    new List<CustomFormat> { customFormatTwo })
                .Should().Be(UpgradeableRejectReason.None);
        }
    }
}
