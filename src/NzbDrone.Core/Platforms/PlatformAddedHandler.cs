using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Bios;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Tv
{
    public class PlatformAddedHandler : IHandle<PlatformAddedEvent>,
                                      IHandle<PlatformImportedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IConfigService _configService;
        private readonly IBiosService _biosService;
        private readonly ISeriesService _seriesService;
        private readonly Logger _logger;

        public PlatformAddedHandler(IManageCommandQueue commandQueueManager,
                                    IConfigService configService = null,
                                    IBiosService biosService = null,
                                    ISeriesService seriesService = null,
                                    Logger logger = null)
        {
            _commandQueueManager = commandQueueManager;
            _configService = configService;
            _biosService = biosService;
            _seriesService = seriesService;
            _logger = logger;
        }

        public void Handle(PlatformAddedEvent message)
        {
            _commandQueueManager.Push(new RefreshPlatformCommand(new List<int> { message.Series.Id }, true));
            TriggerAutoDownloadRequiredBios(message.Series?.TitleSlug);
        }

        public void Handle(PlatformImportedEvent message)
        {
            _commandQueueManager.PushMany(message.SeriesIds.Select(s => new RefreshPlatformCommand(new List<int> { s }, true)).ToList());

            if (_configService?.AutoDownloadRequiredBios == true && _seriesService != null)
            {
                foreach (var seriesId in message.SeriesIds)
                {
                    var series = _seriesService.GetSeries(seriesId);
                    TriggerAutoDownloadRequiredBios(series?.TitleSlug);
                }
            }
        }

        private void TriggerAutoDownloadRequiredBios(string platformSlug)
        {
            if (_configService?.AutoDownloadRequiredBios != true ||
                _biosService == null ||
                string.IsNullOrWhiteSpace(platformSlug))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    _biosService.DownloadMissingBios(platformSlug, null, onlyRequired: true);
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Failed to auto-download required BIOS files for platform '{0}'.", platformSlug);
                }
            });
        }
    }
}
