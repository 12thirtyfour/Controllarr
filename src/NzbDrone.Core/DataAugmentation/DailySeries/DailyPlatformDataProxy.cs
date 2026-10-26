using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;

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
        private readonly IHttpClient _httpClient;
        private readonly IHttpRequestBuilderFactory _requestBuilder;
        private readonly Logger _logger;

        public DailyPlatformDataProxy(IHttpClient httpClient, ISonarrCloudRequestBuilder requestBuilder, Logger logger)
        {
            _httpClient = httpClient;
            _requestBuilder = requestBuilder.Services;
            _logger = logger;
        }

        public IEnumerable<int> GetDailySeriesIds()
        {
            try
            {
                var dailySeriesRequest = _requestBuilder.Create()
                                                        .Resource("/dailyseries")
                                                        .Build();

                var response = _httpClient.Get<List<DailySeries>>(dailySeriesRequest);
                return response.Resource.Select(c => c.TvdbId);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to get Daily Series");
                return Array.Empty<int>();
            }
        }
    }

    public class DailySeriesDataProxy : DailyPlatformDataProxy
    {
        public DailySeriesDataProxy(IHttpClient httpClient, ISonarrCloudRequestBuilder requestBuilder, Logger logger)
            : base(httpClient, requestBuilder, logger)
        {
        }
    }
}
