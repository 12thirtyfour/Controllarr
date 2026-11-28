using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Profiles
{
    [TestFixture]
    public class QualityProfileServiceFixture : CoreTest<QualityProfileService>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(new List<ImportListDefinition>());
        }

        [Test]
        public void init_should_add_default_profiles()
        {
            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat>());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityProfileRepository>()
                .Verify(v => v.Insert(It.IsAny<QualityProfile>()), Times.Exactly(6));
        }

        [Test]

        // This confirms that new profiles are added only if no other profiles exists.
        // We don't want to keep adding them back if a user deleted them on purpose.
        public void Init_should_skip_if_any_profiles_already_exist()
        {
            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(Builder<QualityProfile>.CreateListOfSize(2).Build().ToList());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IQualityProfileRepository>()
                .Verify(v => v.Insert(It.IsAny<QualityProfile>()), Times.Never());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_series()
        {
            var profile = Builder<QualityProfile>.CreateNew()
                                          .With(p => p.Id = 2)
                                          .Build();

            var seriesList = Builder<Series>.CreateListOfSize(3)
                                            .Random(1)
                                            .With(c => c.QualityProfileId = profile.Id)
                                            .Build().ToList();

            Mocker.GetMock<ISeriesService>().Setup(c => c.GetAllSeries()).Returns(seriesList);
            Mocker.GetMock<IQualityProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(profile.Id));

            Mocker.GetMock<IQualityProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_delete_profile_if_not_assigned_to_series()
        {
            var seriesList = Builder<Series>.CreateListOfSize(3)
                                            .All()
                                            .With(c => c.QualityProfileId = 2)
                                            .Build().ToList();

            Mocker.GetMock<ISeriesService>().Setup(c => c.GetAllSeries()).Returns(seriesList);

            Subject.Delete(1);

            Mocker.GetMock<IQualityProfileRepository>().Verify(c => c.Delete(1), Times.Once());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_import_list()
        {
            var profile = Builder<QualityProfile>.CreateNew()
                                                 .With(p => p.Id = 1)
                                                 .Build();

            var seriesList = Builder<Series>.CreateListOfSize(3)
                                            .All()
                                            .With(c => c.QualityProfileId = 2)
                                            .Build().ToList();

            var importLists = Builder<ImportListDefinition>.CreateListOfSize(3)
                                                           .Random(1)
                                                           .Build().ToList();

            Mocker.GetMock<IQualityProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);
            Mocker.GetMock<ISeriesService>().Setup(c => c.GetAllSeries()).Returns(seriesList);

            Mocker.GetMock<IImportListFactory>()
                  .Setup(s => s.All())
                  .Returns(importLists);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(1));

            Mocker.GetMock<IQualityProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_reorder_inverted_gaming_qualities_and_preserve_allowed_flags()
        {
            var invertedProfile = new QualityProfile
            {
                Id = 1,
                Name = "Retail Dumps",
                Cutoff = Quality.VerifiedGoodDump.Id,
                Items = new List<QualityProfileQualityItem>
                {
                    new() { Quality = Quality.Unknown, Allowed = false },
                    new() { Quality = Quality.VerifiedGoodDump, Allowed = true },
                    new() { Quality = Quality.VerifiedRedump, Allowed = true },
                    new() { Quality = Quality.NoIntroVerified, Allowed = true },
                    new() { Quality = Quality.UnverifiedRaw, Allowed = false },
                    new() { Quality = Quality.BetaProto, Allowed = false },
                    new() { Quality = Quality.TranslationPatch, Allowed = false }
                }
            };

            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { invertedProfile });

            Subject.Handle(new ApplicationStartedEvent());

            var orderedIds = invertedProfile.Items.Select(i => i.Quality.Id).ToList();
            orderedIds.Should().Equal(
                Quality.Unknown.Id,
                Quality.BetaProto.Id,
                Quality.TranslationPatch.Id,
                Quality.UnverifiedRaw.Id,
                Quality.VerifiedGoodDump.Id,
                Quality.NoIntroVerified.Id,
                Quality.VerifiedRedump.Id);

            invertedProfile.Items.Single(i => i.Quality == Quality.BetaProto).Allowed.Should().BeFalse();
            invertedProfile.Items.Single(i => i.Quality == Quality.VerifiedGoodDump).Allowed.Should().BeTrue();
            Mocker.GetMock<IQualityProfileRepository>().Verify(r => r.Update(invertedProfile), Times.Once());
        }

        [Test]
        public void should_order_verified_good_dump_above_beta_proto_and_translation_patch_in_default_profile()
        {
            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat>());

            var profile = Subject.GetDefaultProfile(
                "Any",
                Quality.VerifiedGoodDump,
                Quality.VerifiedGoodDump,
                Quality.BetaProto,
                Quality.TranslationPatch);

            var comparer = new QualityModelComparer(profile);
            comparer.Compare(Quality.VerifiedGoodDump, Quality.BetaProto).Should().BeGreaterThan(0);
            comparer.Compare(Quality.VerifiedGoodDump, Quality.TranslationPatch).Should().BeGreaterThan(0);
            comparer.Compare(Quality.VerifiedGoodDump, Quality.UnverifiedRaw).Should().BeGreaterThan(0);
        }

        [Test]
        public void init_should_deduplicate_unknown_entries_and_remove_legacy_groups()
        {
            var profile = new QualityProfile
            {
                Id = 1,
                Name = "Retail Dumps",
                Cutoff = Quality.VerifiedGoodDump.Id,
                Items = new List<QualityProfileQualityItem>
                {
                    new() { Quality = Quality.Unknown, Allowed = false },
                    new() { Quality = Quality.Unknown, Allowed = false },
                    new()
                    {
                        Id = 1000,
                        Name = "Legacy Group",
                        Allowed = false,
                        Items = new List<QualityProfileQualityItem>
                        {
                            new() { Quality = Quality.Unknown, Allowed = false },
                            new() { Quality = Quality.Unknown, Allowed = false }
                        }
                    },
                    new() { Quality = Quality.BetaProto, Allowed = false },
                    new() { Quality = Quality.TranslationPatch, Allowed = false },
                    new() { Quality = Quality.UnverifiedRaw, Allowed = false },
                    new() { Quality = Quality.VerifiedGoodDump, Allowed = true },
                    new() { Quality = Quality.NoIntroVerified, Allowed = true },
                    new() { Quality = Quality.VerifiedRedump, Allowed = true }
                }
            };

            Mocker.GetMock<IQualityProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { profile });

            Subject.Handle(new ApplicationStartedEvent());

            profile.Items.Should().HaveCount(Quality.All.Count);
            profile.Items.Should().NotContain(i => i.Quality == null);
            profile.Items[0].Quality.Should().Be(Quality.Unknown);
            profile.Items.Select(i => i.Quality.Id).Should().OnlyHaveUniqueItems();
            Mocker.GetMock<IQualityProfileRepository>().Verify(r => r.Update(profile), Times.Once());
        }
    }
}
