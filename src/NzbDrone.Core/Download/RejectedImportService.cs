using System.Linq;
using NLog;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.MediaFiles.GameImport;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Download;

public interface IRejectedImportService
{
    bool Process(TrackedDownload trackedDownload, ImportResult importResult);
}

public class RejectedImportService : IRejectedImportService
{
    private readonly ICachedIndexerSettingsProvider _cachedIndexerSettingsProvider;
    private readonly IManageCommandQueue _commandQueueManager;
    private readonly Logger _logger;

    public RejectedImportService(ICachedIndexerSettingsProvider cachedIndexerSettingsProvider,
                                 Logger logger,
                                 IManageCommandQueue commandQueueManager = null)
    {
        _cachedIndexerSettingsProvider = cachedIndexerSettingsProvider;
        _commandQueueManager = commandQueueManager;
        _logger = logger;
    }

    public bool Process(TrackedDownload trackedDownload, ImportResult importResult)
    {
        if (importResult.Result != ImportResultType.Rejected || trackedDownload.RemoteEpisode?.Release == null)
        {
            return false;
        }

        var indexerSettings = _cachedIndexerSettingsProvider.GetSettings(trackedDownload.RemoteEpisode.Release.IndexerId);
        var rejectionReason = importResult.ImportDecision.Rejections.FirstOrDefault()?.Reason;

        if (rejectionReason == ImportRejectionReason.UnmonitoredDatMatch)
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));

            var originalEpisodeIds = trackedDownload.RemoteEpisode?.Episodes?.Select(e => e.Id).Where(id => id > 0).ToList();
            if (_commandQueueManager != null && originalEpisodeIds != null && originalEpisodeIds.Any())
            {
                _logger.Info("Holding unmonitored DAT match '{0}' for manual verification while re-searching original ROM(s).", trackedDownload.DownloadItem.Title);
                _commandQueueManager.Push(new GameSearchCommand(originalEpisodeIds));
            }

            return true;
        }

        if (rejectionReason == ImportRejectionReason.CrossPlatformMatchImported ||
            rejectionReason == ImportRejectionReason.UnverifiedRomHash ||
            rejectionReason == ImportRejectionReason.PlatformFormatRejected ||
            rejectionReason == ImportRejectionReason.UnsupportedExtension)
        {
            _logger.Trace("Download '{0}' rejected ({1}), marking as failed to continue searching for requested ROM", trackedDownload.DownloadItem.Title, rejectionReason);
            trackedDownload.Fail();
            return true;
        }

        if (indexerSettings == null)
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));
            return true;
        }

        if (rejectionReason == ImportRejectionReason.DangerousFile &&
            indexerSettings.FailDownloads.Contains(FailDownloads.PotentiallyDangerous))
        {
            _logger.Trace("Download '{0}' contains potentially dangerous file, marking as failed", trackedDownload.DownloadItem.Title);
            trackedDownload.Fail();
        }
        else if (rejectionReason == ImportRejectionReason.ExecutableFile &&
            indexerSettings.FailDownloads.Contains(FailDownloads.Executables))
        {
            _logger.Trace("Download '{0}' contains executable file, marking as failed", trackedDownload.DownloadItem.Title);
            trackedDownload.Fail();
        }
        else if (rejectionReason == ImportRejectionReason.UserRejectedExtension &&
                 indexerSettings.FailDownloads.Contains(FailDownloads.UserDefinedExtensions))
        {
            _logger.Trace("Download '{0}' contains user defined rejected file extension, marking as failed", trackedDownload.DownloadItem.Title);
            trackedDownload.Fail();
        }
        else
        {
            trackedDownload.Warn(new TrackedDownloadStatusMessage(trackedDownload.DownloadItem.Title, importResult.Errors));
        }

        return true;
    }
}
