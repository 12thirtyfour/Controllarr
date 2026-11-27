using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.GameImport.Aggregation.Aggregators;
using NzbDrone.Core.MediaFiles.GameImport.Aggregation.Aggregators.Augmenters.Quality;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    [TestFixture]
    public class AggregateQualityFixture : CoreTest<AggregateQuality>
    {
        private Mock<IAugmentQuality> _mediaInfoAugmenter;
        private Mock<IAugmentQuality> _fileExtensionAugmenter;
        private Mock<IAugmentQuality> _nameAugmenter;
        private Mock<IAugmentQuality> _releaseNameAugmenter;

        [SetUp]
        public void Setup()
        {
            _mediaInfoAugmenter = new Mock<IAugmentQuality>();
            _fileExtensionAugmenter = new Mock<IAugmentQuality>();
            _nameAugmenter = new Mock<IAugmentQuality>();
            _releaseNameAugmenter = new Mock<IAugmentQuality>();

            _fileExtensionAugmenter.SetupGet(s => s.Order).Returns(1);
            _nameAugmenter.SetupGet(s => s.Order).Returns(2);
            _mediaInfoAugmenter.SetupGet(s => s.Order).Returns(4);
            _releaseNameAugmenter.SetupGet(s => s.Order).Returns(5);

            _mediaInfoAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                               .Returns(AugmentQualityResult.ResolutionOnly(0, Confidence.Default));

            _fileExtensionAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                                   .Returns(new AugmentQualityResult(QualitySource.Unverified, Confidence.Fallback, 0, Confidence.Fallback, new Revision(), Confidence.Fallback));

            _nameAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                          .Returns(new AugmentQualityResult(QualitySource.Prototype, Confidence.Default, 0, Confidence.Default, new Revision(), Confidence.Default));

            _releaseNameAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                                 .Returns(AugmentQualityResult.SourceOnly(QualitySource.Verified, Confidence.MediaInfo));
        }

        private void GivenAugmenters(params Mock<IAugmentQuality>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IAugmentQuality>>(mocks.Select(c => c.Object));
        }

        [Test]
        public void should_return_UnverifiedRaw_from_extension_when_other_augments_are_null()
        {
            var nullMock = new Mock<IAugmentQuality>();
            nullMock.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                    .Returns<LocalEpisode, DownloadClientItem>((l, d) => null);

            GivenAugmenters(_fileExtensionAugmenter, nullMock);

            var result = Subject.Aggregate(new LocalEpisode(), null);

            result.Quality.SourceDetectionSource.Should().Be(QualityDetectionSource.Extension);
            result.Quality.Quality.Should().Be(Quality.UnverifiedRaw);
        }

        [Test]
        public void should_return_BetaProto_when_name_overrides_extension_fallback()
        {
            GivenAugmenters(_fileExtensionAugmenter, _nameAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), null);

            result.Quality.SourceDetectionSource.Should().Be(QualityDetectionSource.Name);
            result.Quality.Quality.Should().Be(Quality.BetaProto);
        }

        [Test]
        public void should_return_VerifiedGoodDump_when_release_name_indicates_verified_source()
        {
            GivenAugmenters(_nameAugmenter, _releaseNameAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), new DownloadClientItem());

            result.Quality.SourceDetectionSource.Should().Be(QualityDetectionSource.Name);
            result.Quality.Quality.Should().Be(Quality.VerifiedGoodDump);
        }

        [Test]
        public void should_return_version_1_when_no_version_specified()
        {
            GivenAugmenters(_nameAugmenter, _releaseNameAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), new DownloadClientItem());

            result.Quality.Revision.Version.Should().Be(1);
            result.Quality.RevisionDetectionSource.Should().Be(QualityDetectionSource.Unknown);
        }

        [Test]
        public void should_return_version_2_when_name_indicates_proper()
        {
            _nameAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                          .Returns(new AugmentQualityResult(QualitySource.Prototype, Confidence.Default, 0, Confidence.Default, new Revision(2), Confidence.Tag));

            GivenAugmenters(_nameAugmenter, _releaseNameAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), new DownloadClientItem());

            result.Quality.Revision.Version.Should().Be(2);
            result.Quality.RevisionDetectionSource.Should().Be(QualityDetectionSource.Name);
        }

        [Test]
        public void should_return_version_0_when_file_name_indicates_v0()
        {
            _nameAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                          .Returns(new AugmentQualityResult(QualitySource.Prototype, Confidence.Default, 0, Confidence.Default, new Revision(0), Confidence.Tag));

            GivenAugmenters(_nameAugmenter, _releaseNameAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), new DownloadClientItem());

            result.Quality.Revision.Version.Should().Be(0);
            result.Quality.RevisionDetectionSource.Should().Be(QualityDetectionSource.Name);
        }

        [Test]
        public void should_return_version_2_when_file_name_indicates_v0_and_release_name_indicates_v2()
        {
            _nameAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                          .Returns(new AugmentQualityResult(QualitySource.Prototype, Confidence.Default, 0, Confidence.Default, new Revision(0), Confidence.Tag));

            _releaseNameAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                                 .Returns(new AugmentQualityResult(QualitySource.Prototype, Confidence.Default, 0, Confidence.Default, new Revision(2), Confidence.Tag));

            GivenAugmenters(_nameAugmenter, _releaseNameAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), new DownloadClientItem());

            result.Quality.Revision.Version.Should().Be(2);
            result.Quality.RevisionDetectionSource.Should().Be(QualityDetectionSource.Name);
        }

        [Test]
        public void should_return_Unknown_for_rom_when_source_is_unknown_and_resolution_is_zero()
        {
            var romAugmenter = new Mock<IAugmentQuality>();
            romAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                        .Returns(new AugmentQualityResult(QualitySource.Unknown, Confidence.Fallback, 0, Confidence.Fallback, new Revision(), Confidence.Fallback));

            GivenAugmenters(romAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), null);

            result.Quality.Quality.Should().Be(Quality.Unknown);
        }

        [Test]
        public void should_preserve_gaming_quality_such_as_VerifiedGoodDump()
        {
            var romAugmenter = new Mock<IAugmentQuality>();
            var augmentResult = new AugmentQualityResult(QualitySource.Unknown, Confidence.Tag, 0, Confidence.Tag, new Revision(), Confidence.Tag)
            {
                Quality = Quality.VerifiedGoodDump,
                QualityConfidence = Confidence.Tag
            };
            romAugmenter.Setup(s => s.AugmentQuality(It.IsAny<LocalEpisode>(), It.IsAny<DownloadClientItem>()))
                        .Returns(augmentResult);

            GivenAugmenters(romAugmenter);

            var result = Subject.Aggregate(new LocalEpisode(), null);

            result.Quality.Quality.Should().Be(Quality.VerifiedGoodDump);
        }
    }
}
