using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Queue;
using Sonarr.Http;
using Sonarr.Http.REST;
using InternetArchiveClient = NzbDrone.Core.Download.Clients.InternetArchive.InternetArchive;

namespace Sonarr.Api.V5.Queue
{
    [V5ApiController("queue")]
    public class QueueActionController : Controller
    {
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IDownloadService _downloadService;
        private readonly IQueueService _queueService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IProvideDownloadClient _downloadClientProvider;

        public QueueActionController(IPendingReleaseService pendingReleaseService,
                                     IDownloadService downloadService,
                                     IQueueService queueService,
                                     ITrackedDownloadService trackedDownloadService,
                                     IProvideDownloadClient downloadClientProvider)
        {
            _pendingReleaseService = pendingReleaseService;
            _downloadService = downloadService;
            _queueService = queueService;
            _trackedDownloadService = trackedDownloadService;
            _downloadClientProvider = downloadClientProvider;
        }

        [HttpPost("reorder")]
        [Consumes("application/json")]
        public Results<NoContent, NotFound> Reorder([FromBody] QueueReorderResource resource)
        {
            if (resource?.Ids == null || resource.Ids.Count == 0)
            {
                throw new BadRequestException("At least one queue item ID must be provided.");
            }

            var queuedDownloadIds = new List<string>();

            foreach (var id in resource.Ids.Distinct())
            {
                var queueItem = _queueService.Find(id);
                if (queueItem == null)
                {
                    throw new NotFoundException();
                }

                var trackedDownload = _trackedDownloadService.Find(queueItem.DownloadId);
                if (trackedDownload == null)
                {
                    throw new NotFoundException();
                }

                var client = _downloadClientProvider.Get(trackedDownload.DownloadClient);
                var isInternetArchive = client is InternetArchiveClient ||
                                        string.Equals(trackedDownload.DownloadItem?.DownloadClientInfo?.Type, "InternetArchive", StringComparison.OrdinalIgnoreCase);

                if (!isInternetArchive)
                {
                    throw new BadRequestException("Queue reordering is only supported for Internet Archive downloads.");
                }

                if (trackedDownload.DownloadItem?.Status == DownloadItemStatus.Queued &&
                    !string.IsNullOrWhiteSpace(trackedDownload.DownloadItem.DownloadId))
                {
                    queuedDownloadIds.Add(trackedDownload.DownloadItem.DownloadId);
                }
            }

            if (queuedDownloadIds.Count == 0)
            {
                return TypedResults.NoContent();
            }

            var positions = InternetArchiveClient.ReorderQueuedDownloads(queuedDownloadIds, resource.Action);

            foreach (var trackedDownload in _trackedDownloadService.GetTrackedDownloads())
            {
                if (trackedDownload.DownloadItem != null &&
                    !string.IsNullOrWhiteSpace(trackedDownload.DownloadItem.DownloadId) &&
                    positions.TryGetValue(trackedDownload.DownloadItem.DownloadId, out var newPos))
                {
                    trackedDownload.DownloadItem.QueuePosition = newPos;
                    trackedDownload.DownloadItem.CanReorder = newPos.HasValue && trackedDownload.DownloadItem.Status == DownloadItemStatus.Queued;
                }
            }

            _queueService.UpdateQueuePositions(positions);

            return TypedResults.NoContent();
        }

        [HttpPost("grab/{id:int}")]
        public async Task<Results<NoContent, NotFound>> Grab([FromRoute] int id)
        {
            var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

            if (pendingRelease == null)
            {
                throw new NotFoundException();
            }

            if (pendingRelease.RemoteEpisode?.Release != null)
            {
                pendingRelease.RemoteEpisode.Release.PendingReleaseReason = null;
            }

            await _downloadService.DownloadReport(pendingRelease.RemoteEpisode, null);

            return TypedResults.NoContent();
        }

        [HttpPost("grab/bulk")]
        [Consumes("application/json")]
        public async Task<Results<NoContent, NotFound>> Grab([FromBody] QueueBulkResource resource)
        {
            foreach (var id in resource.Ids)
            {
                var pendingRelease = _pendingReleaseService.FindPendingQueueItem(id);

                if (pendingRelease == null)
                {
                    throw new NotFoundException();
                }

                if (pendingRelease.RemoteEpisode?.Release != null)
                {
                    pendingRelease.RemoteEpisode.Release.PendingReleaseReason = null;
                }

                await _downloadService.DownloadReport(pendingRelease.RemoteEpisode, null);
            }

            return TypedResults.NoContent();
        }
    }
}
