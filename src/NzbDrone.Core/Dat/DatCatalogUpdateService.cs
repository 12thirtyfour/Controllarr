using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Dat.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;

namespace NzbDrone.Core.Dat
{
    public class DatCatalogSyncResultModel
    {
        public int PlatformsSynced { get; set; }
        public int TotalEntriesSynced { get; set; }
        public List<string> SyncedPlatformNames { get; set; } = new List<string>();
    }

    public interface IDatCatalogUpdateService
    {
        DatCatalogSyncResultModel SyncLibraryDatCatalogs(List<int> platformTvdbIds = null);
    }

    public class DatCatalogUpdateService : IDatCatalogUpdateService, IExecute<UpdateDatCatalogsCommand>
    {
        private readonly ISeriesService _seriesService;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public DatCatalogUpdateService(
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

        public DatCatalogSyncResultModel SyncLibraryDatCatalogs(List<int> platformTvdbIds = null)
        {
            var result = new DatCatalogSyncResultModel();
            var allSeries = _seriesService?.GetAllSeries() ?? new List<Series>();

            var targetSeries = platformTvdbIds != null && platformTvdbIds.Count > 0
                ? allSeries.Where(s => platformTvdbIds.Contains(s.TvdbId)).ToList()
                : allSeries;

            if (targetSeries.Count == 0 && platformTvdbIds != null && platformTvdbIds.Count > 0)
            {
                foreach (var tvdbId in platformTvdbIds.Distinct())
                {
                    var count = _gameCatalogService?.SyncPlatformDatCatalogs(tvdbId, true) ?? 0;
                    result.PlatformsSynced++;
                    result.TotalEntriesSynced += count;
                    if (_gameCatalogService != null && _gameCatalogService.TryGetPlatformEntry(tvdbId, out var p))
                    {
                        result.SyncedPlatformNames.Add(p.Name);
                    }
                }

                return result;
            }

            // If no platforms have been added to the library yet, sync default flagship platforms (NES, SNES, GBA, NDS)
            if (targetSeries.Count == 0)
            {
                foreach (var defaultTvdbId in new[] { 1001, 1003, 1011, 1012 })
                {
                    var count = _gameCatalogService?.SyncPlatformDatCatalogs(defaultTvdbId, true) ?? 0;
                    result.PlatformsSynced++;
                    result.TotalEntriesSynced += count;
                    if (_gameCatalogService != null && _gameCatalogService.TryGetPlatformEntry(defaultTvdbId, out var p))
                    {
                        result.SyncedPlatformNames.Add(p.Name);
                    }
                }

                return result;
            }

            var refreshedSeriesIds = new List<int>();

            foreach (var series in targetSeries)
            {
                _logger?.ProgressInfo("Updating No-Intro / Redump / TOSEC / SMDB DAT catalogs for {0}", series.Title);
                var syncedCount = _gameCatalogService?.SyncPlatformDatCatalogs(series.TvdbId, true, series.SeasonType) ?? 0;
                result.PlatformsSynced++;
                result.TotalEntriesSynced += syncedCount;
                result.SyncedPlatformNames.Add(series.Title);

                if (series.Id > 0)
                {
                    refreshedSeriesIds.Add(series.Id);
                }
            }

            if (refreshedSeriesIds.Count > 0 && _commandQueueManager != null)
            {
                _commandQueueManager.Push(new RefreshSeriesCommand(refreshedSeriesIds, false));
            }

            return result;
        }

        public void Execute(UpdateDatCatalogsCommand message)
        {
            var summary = SyncLibraryDatCatalogs(message?.PlatformTvdbIds);
            _logger?.ProgressInfo(
                "Completed scheduled DAT catalog update across {0} platform(s) ({1} verified entries synced).",
                summary.PlatformsSynced,
                summary.TotalEntriesSynced);
        }
    }
}
