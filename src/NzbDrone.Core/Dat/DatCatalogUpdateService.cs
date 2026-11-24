using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Bios;
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
        private static int _activeDatUpdates;

        private readonly ISeriesService _seriesService;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IBiosService _biosService;
        private readonly Logger _logger;

        public DatCatalogUpdateService(
            ISeriesService seriesService,
            IGameCatalogService gameCatalogService,
            IManageCommandQueue commandQueueManager,
            Logger logger,
            IBiosService biosService = null)
        {
            _seriesService = seriesService;
            _gameCatalogService = gameCatalogService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
            _biosService = biosService;
        }

        public static bool IsUpdating => Volatile.Read(ref _activeDatUpdates) > 0;

        internal static IDisposable BeginDatUpdateScope()
        {
            Interlocked.Increment(ref _activeDatUpdates);
            return new DatUpdateScope();
        }

        private sealed class DatUpdateScope : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Interlocked.Decrement(ref _activeDatUpdates);
                }
            }
        }

        public DatCatalogSyncResultModel SyncLibraryDatCatalogs(List<int> platformTvdbIds = null)
        {
            var result = new DatCatalogSyncResultModel();
            var refreshedSeriesIds = new List<int>();

            using (BeginDatUpdateScope())
            {
                var allSeries = _seriesService?.GetAllSeries() ?? new List<Series>();

                var targetSeries = platformTvdbIds != null && platformTvdbIds.Count > 0
                    ? allSeries.Where(s => platformTvdbIds.Contains(s.TvdbId)).ToList()
                    : allSeries;

                List<string> explicitPlatformSlugs = null;

                if (targetSeries.Count == 0)
                {
                    var idsToSync = platformTvdbIds != null && platformTvdbIds.Count > 0
                        ? platformTvdbIds.Distinct().ToArray()
                        : new[] { 1001, 1003, 1011, 1012 };

                    if (platformTvdbIds != null && platformTvdbIds.Count > 0)
                    {
                        explicitPlatformSlugs = new List<string>();
                    }

                    foreach (var tvdbId in idsToSync)
                    {
                        var count = _gameCatalogService?.SyncPlatformDatCatalogs(tvdbId, true) ?? 0;
                        result.PlatformsSynced++;
                        result.TotalEntriesSynced += count;
                        if (_gameCatalogService != null && _gameCatalogService.TryGetPlatformEntry(tvdbId, out var p))
                        {
                            result.SyncedPlatformNames.Add(p.Name);
                            if (explicitPlatformSlugs != null && !string.IsNullOrWhiteSpace(p.Slug))
                            {
                                explicitPlatformSlugs.Add(p.Slug);
                            }
                        }
                    }
                }
                else
                {
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

                    if (platformTvdbIds != null && platformTvdbIds.Count > 0)
                    {
                        explicitPlatformSlugs = targetSeries
                            .Select(s => s.TitleSlug)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                }

                if (_biosService != null)
                {
                    var syncedFirmwares = _biosService.SyncFirmwareUpdates(explicitPlatformSlugs);
                    if (syncedFirmwares > 0)
                    {
                        _logger?.ProgressInfo("Checked Darthsternie firmware archive ({0} firmware entries indexed).", syncedFirmwares);
                    }
                }
            }

            if (refreshedSeriesIds.Count > 0 && _commandQueueManager != null)
            {
                _commandQueueManager.Push(new RefreshPlatformCommand(refreshedSeriesIds, false));
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
