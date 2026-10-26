using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Cache;

namespace NzbDrone.Core.DataAugmentation.DailySeries
{
    public interface IDailyPlatformService
    {
        bool IsDailySeries(int tvdbid);
    }

    public interface IDailySeriesService : IDailyPlatformService
    {
    }

    public class DailyPlatformService : IDailySeriesService
    {
        private readonly IDailySeriesDataProxy _proxy;
        private readonly ICached<List<int>> _cache;

        public DailyPlatformService(IDailySeriesDataProxy proxy, ICacheManager cacheManager)
        {
            _proxy = proxy;
            _cache = cacheManager.GetCache<List<int>>(GetType());
        }

        public bool IsDailySeries(int tvdbid)
        {
            var dailySeries = _cache.Get("all", () => _proxy.GetDailySeriesIds().ToList(), TimeSpan.FromHours(1));
            return dailySeries.Any(i => i == tvdbid);
        }
    }

    public class DailySeriesService : DailyPlatformService
    {
        public DailySeriesService(IDailySeriesDataProxy proxy, ICacheManager cacheManager)
            : base(proxy, cacheManager)
        {
        }
    }
}
