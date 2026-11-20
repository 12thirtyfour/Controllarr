using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.PlatformStats;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.SeriesStatsTests
{
    [TestFixture]
    public class PlatformStatisticsServiceFixture : CoreTest<PlatformStatisticsService>
    {
        [Test]
        public void should_exclude_bios_season_from_rom_counts()
        {
            var regularSeason = new SeasonStatistics
            {
                SeriesId = 1,
                SeasonNumber = 1,
                EpisodeFileCount = 10,
                EpisodeCount = 12,
                TotalEpisodeCount = 15,
                MonitoredEpisodeCount = 12,
                SizeOnDisk = 1024
            };

            var biosSeason = new SeasonStatistics
            {
                SeriesId = 1,
                SeasonNumber = 99,
                EpisodeFileCount = 3,
                EpisodeCount = 3,
                TotalEpisodeCount = 3,
                MonitoredEpisodeCount = 3,
                SizeOnDisk = 512
            };

            Mocker.GetMock<ISeriesStatisticsRepository>()
                .Setup(r => r.SeriesStatistics(1))
                .Returns(new List<SeasonStatistics> { regularSeason, biosSeason });

            Mocker.GetMock<IQualityProfileService>()
                .Setup(p => p.Get(1))
                .Returns(new QualityProfile { Items = new List<QualityProfileQualityItem>() });

            var stats = Subject.SeriesStatistics(1, 1);

            stats.Should().NotBeNull();
            stats.EpisodeFileCount.Should().Be(10, "BIOS season 99 files must be excluded from EpisodeFileCount / RomFileCount");
            stats.EpisodeCount.Should().Be(12);
            stats.TotalEpisodeCount.Should().Be(15);
            stats.MonitoredEpisodeCount.Should().Be(12);
            stats.SizeOnDisk.Should().Be(1536, "SizeOnDisk should still include all files on disk");
        }
    }
}
