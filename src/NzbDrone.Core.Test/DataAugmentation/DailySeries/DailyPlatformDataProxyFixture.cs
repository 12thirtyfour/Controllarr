using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.DailySeries;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DataAugmentation.DailySeries
{
    [TestFixture]
    public class DailyPlatformDataProxyFixture : CoreTest<DailySeriesDataProxy>
    {
        [Test]
        public void should_get_empty_list_of_daily_series()
        {
            var list = Subject.GetDailySeriesIds();
            list.Should().BeEmpty();
        }
    }
}
