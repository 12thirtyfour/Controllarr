using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.PlatformTests.PlatformServiceTests
{
    [TestFixture]
    public class FindByTitleInexactFixture : CoreTest<SeriesService>
    {
        [Test]
        public void should_not_return_parent_platform_for_addon_release_title()
        {
            var jaguar = new Series
            {
                Id = 1,
                Title = "Atari Jaguar",
                CleanTitle = "atarijaguar",
                TitleSlug = "jaguar"
            };

            Mocker.GetMock<ISeriesRepository>()
                .Setup(r => r.FindByTitleInexact(It.IsAny<string>()))
                .Returns(new List<Series> { jaguar });

            var result = Subject.FindByTitleInexact("Atari Jaguar CD - Battlemorph (USA)");

            result.Should().BeNull();
        }

        [Test]
        public void should_return_matching_addon_platform_when_both_parent_and_addon_exist()
        {
            var jaguar = new Series
            {
                Id = 1,
                Title = "Atari Jaguar",
                CleanTitle = "atarijaguar",
                TitleSlug = "jaguar"
            };

            var jaguarCd = new Series
            {
                Id = 2,
                Title = "Atari Jaguar CD",
                CleanTitle = "atarijaguarcd",
                TitleSlug = "jaguarcd"
            };

            Mocker.GetMock<ISeriesRepository>()
                .Setup(r => r.FindByTitleInexact(It.IsAny<string>()))
                .Returns(new List<Series> { jaguar, jaguarCd });

            var result = Subject.FindByTitleInexact("Atari Jaguar CD - Battlemorph (USA)");

            result.Should().NotBeNull();
            result.Id.Should().Be(2);
            result.TitleSlug.Should().Be("jaguarcd");
        }
    }
}
