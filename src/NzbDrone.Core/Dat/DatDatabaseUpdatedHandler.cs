using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Dat.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Dat
{
    public class DatDatabaseUpdatedHandler : IHandle<DatDatabaseUpdatedEvent>
    {
        private readonly ISeriesService _seriesService;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public DatDatabaseUpdatedHandler(
            ISeriesService seriesService,
            IGameCatalogService gameCatalogService,
            IManageCommandQueue commandQueueManager,
            Logger logger)
        {
            _seriesService = seriesService;
            _gameCatalogService = gameCatalogService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public void Handle(DatDatabaseUpdatedEvent message)
        {
            if (message?.DatFile == null)
            {
                return;
            }

            var allSeries = _seriesService.GetAllSeries();
            if (allSeries == null || allSeries.Count == 0)
            {
                return;
            }

            var datFile = message.DatFile;
            var datSlug = (datFile.Description ?? string.Empty).Trim().ToLowerInvariant();
            var datName = (datFile.Name ?? string.Empty).Trim();

            var matchedSeries = allSeries.Where(s =>
            {
                if (!string.IsNullOrWhiteSpace(datSlug) && string.Equals(s.TitleSlug, datSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(datName) &&
                    (datName.Contains(s.Title, StringComparison.OrdinalIgnoreCase) ||
                     s.Title.Contains(datName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (_gameCatalogService != null &&
                    _gameCatalogService.InferPlatformForText($"{datName} {datSlug}") is var inferred &&
                    inferred != null &&
                    (inferred.TvdbId == s.TvdbId || string.Equals(inferred.Slug, s.TitleSlug, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                return false;
            }).DistinctBy(s => s.Id).ToList();

            if (matchedSeries.Count == 0)
            {
                _logger.Debug("DAT database '{0}' was updated ({1} entries), but no matching platform is currently in the library.", datName, message.EntryCount);
                return;
            }

            foreach (var series in matchedSeries)
            {
                _logger.Info("DAT database '{0}' was updated ({1} entries) for platform '{2}'; automatically refreshing library with new additions.", datName, message.EntryCount, series.Title);
                _gameCatalogService?.InvalidatePlatformCache(series.TvdbId);
                _commandQueueManager.Push(new RefreshPlatformCommand(series.Id, false));
            }
        }
    }
}
