using System;
using System.Collections.Generic;

namespace NzbDrone.Core.DataAugmentation.DailySeries
{
    public interface IDailyPlatformDataProxy
    {
        IEnumerable<int> GetDailySeriesIds();
    }

    public interface IDailySeriesDataProxy : IDailyPlatformDataProxy
    {
    }

    public class DailyPlatformDataProxy : IDailySeriesDataProxy
    {
        public IEnumerable<int> GetDailySeriesIds()
        {
            return Array.Empty<int>();
        }
    }

    public class DailySeriesDataProxy : DailyPlatformDataProxy
    {
    }
}
