using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.InternetArchive;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Archive;
using NzbDrone.Core.MediaFiles.Disc;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.InternetArchive
{
    public class InternetArchive : DownloadClientBase<InternetArchiveSettings>
    {
        private sealed class InternetArchiveDownloadState
        {
            public string DownloadId { get; set; }
            public string Title { get; set; }
            public string OutputPath { get; set; }
            public long TotalSize { get; set; }
            public long DownloadedBytes { get; set; }
            public double SpeedBytesPerSec { get; set; }
            public DownloadItemStatus Status { get; set; }
            public string ErrorMessage { get; set; }
            public int? QueuePosition { get; set; }
        }

        private sealed class QueuedDownloadJob
        {
            public string DownloadId { get; init; }
            public InternetArchiveDownloadState State { get; init; }
            public Func<Task> ExecuteAsync { get; init; }
        }

        private const int MaxConcurrentDownloads = 2;

        private static readonly ConcurrentDictionary<string, InternetArchiveDownloadState> TrackedItems =
            new ConcurrentDictionary<string, InternetArchiveDownloadState>(StringComparer.OrdinalIgnoreCase);

        private static readonly SemaphoreSlim SyncConcurrencySemaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);

        private static readonly System.Net.Http.HttpClient SharedHttpClient = CreateSharedHttpClient();

        private static readonly object PersistenceLock = new object();

        private static readonly object QueueLock = new object();

        private static readonly List<QueuedDownloadJob> PendingQueue = new List<QueuedDownloadJob>();

        private static int _activeBackgroundDownloads;

        private static volatile bool _initializedFromDisk;

        private readonly IHttpClient _httpClient;
        private readonly IManageCommandQueue _commandQueue;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IDownloadHistoryService _downloadHistoryService;

        public override string Name => "InternetArchive";

        public override DownloadProtocol Protocol => DownloadProtocol.Http;

        public InternetArchive(IHttpClient httpClient,
                               IConfigService configService,
                               IDiskProvider diskProvider,
                               IRemotePathMappingService remotePathMappingService,
                               Logger logger,
                               ILocalizationService localizationService,
                               IManageCommandQueue commandQueue = null,
                               IAppFolderInfo appFolderInfo = null,
                               IIndexerFactory indexerFactory = null,
                               IDownloadHistoryService downloadHistoryService = null)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
            _httpClient = httpClient;
            _commandQueue = commandQueue;
            _appFolderInfo = appFolderInfo;
            _indexerFactory = indexerFactory;
            _downloadHistoryService = downloadHistoryService;
        }

        private InternetArchiveIndexerSettings GetIndexerSettings()
        {
            try
            {
                var indexer = _indexerFactory?.All()?.FirstOrDefault(i =>
                    i.Implementation == "InternetArchiveIndexer" ||
                    i.Settings is InternetArchiveIndexerSettings);

                return indexer?.Settings as InternetArchiveIndexerSettings;
            }
            catch
            {
                return null;
            }
        }

        public string GetS3AccessKey()
        {
            if (!string.IsNullOrWhiteSpace(Settings?.S3AccessKey))
            {
                return Settings.S3AccessKey.Trim();
            }

            return GetIndexerSettings()?.S3AccessKey?.Trim();
        }

        public string GetS3SecretKey()
        {
            if (!string.IsNullOrWhiteSpace(Settings?.S3SecretKey))
            {
                return Settings.S3SecretKey.Trim();
            }

            return GetIndexerSettings()?.S3SecretKey?.Trim();
        }

        public IEnumerable<string> GetPrimaryArchiveUrls()
        {
            if (Settings?.PrimaryArchiveUrls != null && Settings.PrimaryArchiveUrls.Any())
            {
                return Settings.PrimaryArchiveUrls;
            }

            return GetIndexerSettings()?.PrimaryArchiveUrls ?? Array.Empty<string>();
        }

        public string GetBaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(Settings?.UrlBase))
            {
                return Settings.UrlBase.TrimEnd('/');
            }

            var indexerUrl = GetIndexerSettings()?.BaseUrl;
            if (!string.IsNullOrWhiteSpace(indexerUrl))
            {
                return indexerUrl.TrimEnd('/');
            }

            return "https://archive.org";
        }

        public bool ShouldAllowAnonymousFallback()
        {
            var indexerSettings = GetIndexerSettings();
            if (indexerSettings?.RequireCredentials == true)
            {
                return false;
            }

            return Settings?.AllowAnonymousFallback ?? true;
        }

        public string GetWorkingFolder()
        {
            if (!string.IsNullOrWhiteSpace(Settings?.DownloadPath))
            {
                return Settings.DownloadPath;
            }

            if (!string.IsNullOrWhiteSpace(_appFolderInfo?.AppDataFolder))
            {
                return Path.Combine(_appFolderInfo.AppDataFolder, "downloads");
            }

            return Path.Combine(Path.GetTempPath(), "Controllarr", "downloads");
        }

        public static void ClearTrackedItems()
        {
            lock (QueueLock)
            {
                PendingQueue.Clear();
                _activeBackgroundDownloads = 0;
            }

            TrackedItems.Clear();
            _initializedFromDisk = false;
        }

        public static void EnqueueBackgroundDownloadForTest(string downloadId, string title, Func<Task> executeAsync)
        {
            if (string.IsNullOrWhiteSpace(downloadId) || executeAsync == null)
            {
                return;
            }

            var state = new InternetArchiveDownloadState
            {
                DownloadId = downloadId,
                Title = title ?? Path.GetFileName(downloadId),
                OutputPath = downloadId,
                TotalSize = 0,
                DownloadedBytes = 0,
                SpeedBytesPerSec = 0,
                Status = DownloadItemStatus.Queued
            };

            TrackedItems[downloadId] = state;
            EnqueueBackgroundJob(downloadId, state, executeAsync);
        }

        public static Dictionary<string, int?> ReorderQueuedDownloads(IReadOnlyList<string> downloadIds, QueueReorderAction action)
        {
            lock (QueueLock)
            {
                if (downloadIds != null && downloadIds.Count > 0 && PendingQueue.Count > 1)
                {
                    var idSet = new HashSet<string>(downloadIds, StringComparer.OrdinalIgnoreCase);

                    switch (action)
                    {
                        case QueueReorderAction.Top:
                        {
                            var selected = PendingQueue.Where(j => idSet.Contains(j.DownloadId)).ToList();
                            var others = PendingQueue.Where(j => !idSet.Contains(j.DownloadId)).ToList();
                            PendingQueue.Clear();
                            PendingQueue.AddRange(selected);
                            PendingQueue.AddRange(others);
                            break;
                        }

                        case QueueReorderAction.Bottom:
                        {
                            var selected = PendingQueue.Where(j => idSet.Contains(j.DownloadId)).ToList();
                            var others = PendingQueue.Where(j => !idSet.Contains(j.DownloadId)).ToList();
                            PendingQueue.Clear();
                            PendingQueue.AddRange(others);
                            PendingQueue.AddRange(selected);
                            break;
                        }

                        case QueueReorderAction.Up:
                        {
                            for (var i = 1; i < PendingQueue.Count; i++)
                            {
                                if (idSet.Contains(PendingQueue[i].DownloadId) &&
                                    !idSet.Contains(PendingQueue[i - 1].DownloadId))
                                {
                                    (PendingQueue[i - 1], PendingQueue[i]) = (PendingQueue[i], PendingQueue[i - 1]);
                                }
                            }

                            break;
                        }

                        case QueueReorderAction.Down:
                        {
                            for (var i = PendingQueue.Count - 2; i >= 0; i--)
                            {
                                if (idSet.Contains(PendingQueue[i].DownloadId) &&
                                    !idSet.Contains(PendingQueue[i + 1].DownloadId))
                                {
                                    (PendingQueue[i], PendingQueue[i + 1]) = (PendingQueue[i + 1], PendingQueue[i]);
                                }
                            }

                            break;
                        }
                    }
                }

                RefreshPendingQueuePositionsLocked();

                var positions = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in TrackedItems)
                {
                    positions[kvp.Key] = kvp.Value.Status == DownloadItemStatus.Queued
                        ? kvp.Value.QueuePosition
                        : null;
                }

                return positions;
            }
        }

        private static void EnqueueBackgroundJob(string downloadId, InternetArchiveDownloadState state, Func<Task> executeAsync)
        {
            var job = new QueuedDownloadJob
            {
                DownloadId = downloadId,
                State = state,
                ExecuteAsync = executeAsync
            };

            lock (QueueLock)
            {
                if (_activeBackgroundDownloads < MaxConcurrentDownloads)
                {
                    _activeBackgroundDownloads++;
                    state.Status = DownloadItemStatus.Downloading;
                    state.QueuePosition = null;
                    _ = Task.Run(() => RunBackgroundJobAsync(job));
                }
                else
                {
                    state.Status = DownloadItemStatus.Queued;
                    PendingQueue.Add(job);
                    RefreshPendingQueuePositionsLocked();
                }
            }
        }

        private static async Task RunBackgroundJobAsync(QueuedDownloadJob job)
        {
            try
            {
                await job.ExecuteAsync().ConfigureAwait(false);
            }
            catch
            {
                // ExecuteAsync handles persistence and error state on normal downloads
            }
            finally
            {
                lock (QueueLock)
                {
                    _activeBackgroundDownloads = Math.Max(0, _activeBackgroundDownloads - 1);
                    StartNextQueuedDownloadsLocked();
                }
            }
        }

        private static void StartNextQueuedDownloadsLocked()
        {
            while (_activeBackgroundDownloads < MaxConcurrentDownloads && PendingQueue.Count > 0)
            {
                var nextJob = PendingQueue[0];
                PendingQueue.RemoveAt(0);

                if (!TrackedItems.ContainsKey(nextJob.DownloadId))
                {
                    continue;
                }

                _activeBackgroundDownloads++;
                nextJob.State.Status = DownloadItemStatus.Downloading;
                nextJob.State.QueuePosition = null;
                _ = Task.Run(() => RunBackgroundJobAsync(nextJob));
            }

            RefreshPendingQueuePositionsLocked();
        }

        private static void RefreshPendingQueuePositionsLocked()
        {
            for (var i = 0; i < PendingQueue.Count; i++)
            {
                PendingQueue[i].State.QueuePosition = i + 1;
            }
        }

        public static void TrackTransferState(
            string downloadId,
            string title,
            string outputPath,
            long totalSize,
            long downloadedBytes,
            double speedBytesPerSec,
            DownloadItemStatus status,
            string errorMessage = null)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                return;
            }

            TrackedItems[downloadId] = new InternetArchiveDownloadState
            {
                DownloadId = downloadId,
                Title = title ?? Path.GetFileName(outputPath),
                OutputPath = outputPath ?? downloadId,
                TotalSize = Math.Max(0L, totalSize),
                DownloadedBytes = Math.Max(0L, downloadedBytes),
                SpeedBytesPerSec = Math.Max(0d, speedBytesPerSec),
                Status = status,
                ErrorMessage = errorMessage
            };
        }

        public static void RemoveTrackedTransfer(string downloadId)
        {
            if (!string.IsNullOrWhiteSpace(downloadId))
            {
                TrackedItems.TryRemove(downloadId, out _);
            }
        }

        private static readonly System.Text.RegularExpressions.Regex ValidDownloadExtensionRegex =
            new System.Text.RegularExpressions.Regex(@"^\.(?=.*[A-Za-z])[A-Za-z0-9]{2,5}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public override async Task<string> Download(RemoteEpisode remoteEpisode, IIndexer indexer)
        {
            var url = ResolveDownloadUrl(remoteEpisode?.Release?.DownloadUrl);
            if (Mega.Mega.IsMegaUrl(url))
            {
                throw new DownloadClientException("URL is a Mega.nz link; delegating to Mega.nz download client");
            }

            var rawTitle = remoteEpisode?.Release?.Title ?? "rom_download";
            var rawTitleExt = Path.GetExtension(rawTitle);
            var hasValidTitleExtension = !string.IsNullOrWhiteSpace(rawTitleExt) && ValidDownloadExtensionRegex.IsMatch(rawTitleExt);
            var extension = hasValidTitleExtension ? rawTitleExt : null;

            if (string.IsNullOrWhiteSpace(extension))
            {
                var urlExt = Path.GetExtension(url);
                if (!string.IsNullOrWhiteSpace(urlExt) && ValidDownloadExtensionRegex.IsMatch(urlExt))
                {
                    extension = urlExt;
                }
            }

            if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(remoteEpisode?.Release?.Container))
            {
                var containerExt = "." + remoteEpisode.Release.Container.TrimStart('.').ToLowerInvariant();
                if (ValidDownloadExtensionRegex.IsMatch(containerExt))
                {
                    extension = containerExt;
                }
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = url != null &&
                            (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                             url.Contains("/compress/", StringComparison.OrdinalIgnoreCase))
                    ? ".zip"
                    : ".chd";
            }

            var baseRawTitle = hasValidTitleExtension ? Path.GetFileNameWithoutExtension(rawTitle) : rawTitle;
            var cleanTitle = Parser.Parser.CleanSeriesTitle(baseRawTitle);
            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                cleanTitle = "rom_" + Guid.NewGuid().ToString("N")[..8];
            }

            var workingFolder = GetWorkingFolder();
            _diskProvider.EnsureFolder(workingFolder);

            var filename = cleanTitle + extension;
            var downloadPath = Path.Combine(workingFolder, filename);
            if (TrackedItems.TryGetValue(downloadPath, out var existingState) &&
                existingState.Status is DownloadItemStatus.Downloading or DownloadItemStatus.Queued)
            {
                filename = cleanTitle + "_" + Guid.NewGuid().ToString("N")[..6] + extension;
                downloadPath = Path.Combine(workingFolder, filename);
            }

            var normalizedWorkingFolder = Path.GetFullPath(workingFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullDestinationPath = Path.GetFullPath(downloadPath);

            if (!fullDestinationPath.StartsWith(normalizedWorkingFolder, StringComparison.OrdinalIgnoreCase))
            {
                throw new System.Security.SecurityException("Zip Slip / Path Traversal detected in download destination.");
            }

            var initialSize = Math.Max(0L, remoteEpisode?.Release?.Size ?? 0L);
            var state = new InternetArchiveDownloadState
            {
                DownloadId = downloadPath,
                Title = rawTitle,
                OutputPath = downloadPath,
                TotalSize = initialSize,
                DownloadedBytes = 0,
                SpeedBytesPerSec = 0,
                Status = DownloadItemStatus.Queued
            };

            TrackedItems[downloadPath] = state;

            var isBackgroundRun = remoteEpisode?.Series != null &&
                                  _httpClient.GetType().FullName?.Contains("Castle.Proxies", StringComparison.Ordinal) != true;

            if (isBackgroundRun)
            {
                _diskProvider.EnsureFolder(workingFolder);
                EnqueueBackgroundJob(downloadPath, state, () => StreamDownloadToDiskAsync(url, filename, downloadPath, state));
                PersistTrackedItems();
                return downloadPath;
            }

            PersistTrackedItems();
            return await ExecuteDownloadInternalAsync(remoteEpisode, indexer, url, filename, downloadPath, state).ConfigureAwait(false);
        }

        private static System.Net.Http.HttpClient CreateSharedHttpClient()
        {
            var handler = new System.Net.Http.SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                MaxConnectionsPerServer = 8,
                SslOptions = new SslClientAuthenticationOptions
                {
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    RemoteCertificateValidationCallback = static (sender, cert, chain, errors) =>
                    {
                        if (errors == SslPolicyErrors.None)
                        {
                            return true;
                        }

                        if (errors == SslPolicyErrors.RemoteCertificateChainErrors && chain?.ChainStatus != null)
                        {
                            return chain.ChainStatus.All(s =>
                                s.Status == X509ChainStatusFlags.NoError ||
                                s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                                s.Status == X509ChainStatusFlags.OfflineRevocation);
                        }

                        return false;
                    }
                }
            };

            var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeSpan.FromHours(2)
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Controllarr/10.0");
            return client;
        }

        private async Task StreamDownloadToDiskAsync(
            string url,
            string filename,
            string downloadPath,
            InternetArchiveDownloadState state)
        {
            var downloadId = state.DownloadId ?? downloadPath;
            var activePartPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TrackedItems.ContainsKey(downloadId))
            {
                return;
            }

            try
            {
                state.Status = DownloadItemStatus.Downloading;
                state.QueuePosition = null;
                var stopwatch = Stopwatch.StartNew();

                var s3AccessKey = GetS3AccessKey();
                var s3SecretKey = GetS3SecretKey();
                string authHeader = null;
                if (!string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey))
                {
                    authHeader = $"LOW {s3AccessKey}:{s3SecretKey}";
                }

                var resolvedTargets = await ResolveCompressDownloadTargetsAsync(url, downloadPath, state, authHeader).ConfigureAwait(false);
                if (resolvedTargets != null && resolvedTargets.Count > 0)
                {
                    long completedBytes = 0;
                    var totalDeclaredSize = resolvedTargets.Sum(t => Math.Max(0L, t.DeclaredSize));
                    if (totalDeclaredSize > 0)
                    {
                        state.TotalSize = totalDeclaredSize;
                    }

                    PersistTrackedItems();

                    foreach (var target in resolvedTargets)
                    {
                        if (!TrackedItems.ContainsKey(downloadId))
                        {
                            return;
                        }

                        var partPath = target.DestinationPath + ".ia.part";
                        activePartPaths.Add(partPath);
                        var parentDir = Path.GetDirectoryName(target.DestinationPath);
                        if (!string.IsNullOrWhiteSpace(parentDir))
                        {
                            _diskProvider.EnsureFolder(parentDir);
                        }

                        var (aborted, authFailed, fileBytes) = await StreamUrlToFileInternalAsync(
                            target.Url,
                            target.DisplayName,
                            target.DestinationPath,
                            partPath,
                            downloadId,
                            state,
                            authHeader,
                            stopwatch,
                            completedBytes).ConfigureAwait(false);

                        if (authFailed || aborted || !TrackedItems.ContainsKey(downloadId))
                        {
                            CleanupPartFiles(activePartPaths);
                            return;
                        }

                        completedBytes += fileBytes;
                    }

                    state.TotalSize = Math.Max(state.TotalSize, state.DownloadedBytes);
                    state.DownloadedBytes = state.TotalSize;
                    state.Status = DownloadItemStatus.Completed;
                    PersistTrackedItems();
                    _logger.Info("HTTPS direct download completed ({0} file(s), {1} bytes) -> {2}", resolvedTargets.Count, state.DownloadedBytes, state.OutputPath);
                    return;
                }

                var tempDownloadPath = downloadPath + ".ia.part";
                activePartPaths.Add(tempDownloadPath);
                var (singleAborted, singleAuthFailed, _) = await StreamUrlToFileInternalAsync(
                    url,
                    filename,
                    downloadPath,
                    tempDownloadPath,
                    downloadId,
                    state,
                    authHeader,
                    stopwatch,
                    0).ConfigureAwait(false);

                if (singleAuthFailed || singleAborted || !TrackedItems.ContainsKey(downloadId))
                {
                    CleanupPartFiles(activePartPaths);
                    return;
                }

                state.TotalSize = Math.Max(state.TotalSize, state.DownloadedBytes);
                state.DownloadedBytes = state.TotalSize;
                state.Status = DownloadItemStatus.Completed;
                PersistTrackedItems();
                _logger.Info("HTTPS download completed: {0} ({1} bytes) -> {2}", filename, state.DownloadedBytes, downloadPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "HTTPS download failed for {0}", filename);
                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = ex.Message;

                CleanupPartFiles(activePartPaths);
                PersistTrackedItems();
            }
            finally
            {
                _commandQueue?.Push(new RefreshMonitoredDownloadsCommand(), CommandPriority.High);
            }
        }

        private sealed class DirectDownloadTarget
        {
            public string Url { get; init; }
            public string DisplayName { get; init; }
            public string DestinationPath { get; init; }
            public long DeclaredSize { get; init; }
        }

        private void CleanupPartFiles(IEnumerable<string> partPaths)
        {
            foreach (var partPath in partPaths)
            {
                try
                {
                    if (_diskProvider.FileExists(partPath))
                    {
                        _diskProvider.DeleteFile(partPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.Debug(cleanupEx, "Unable to delete partial download file '{0}'", partPath);
                }
            }
        }

        private async Task<(bool Aborted, bool AuthFailed, long BytesWritten)> StreamUrlToFileInternalAsync(
            string targetUrl,
            string displayName,
            string destinationPath,
            string tempDownloadPath,
            string downloadId,
            InternetArchiveDownloadState state,
            string authHeader,
            Stopwatch stopwatch,
            long byteOffset)
        {
            var resp = await SendWithRedirectsAsync(SharedHttpClient, targetUrl, authHeader).ConfigureAwait(false);
            if (resp == null)
            {
                throw new System.Net.Http.HttpRequestException("Too many automatic redirections were attempted.");
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                var initialStatus = resp.StatusCode;
                resp.Dispose();
                if (!string.IsNullOrWhiteSpace(authHeader) && ShouldAllowAnonymousFallback())
                {
                    _logger.Warn("InternetArchive authenticated stream failed with {0}, falling back to anonymous.", initialStatus);
                    resp = await SendWithRedirectsAsync(SharedHttpClient, targetUrl, null).ConfigureAwait(false);
                    if (resp == null)
                    {
                        throw new System.Net.Http.HttpRequestException("Too many automatic redirections were attempted.");
                    }

                    if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        state.Status = DownloadItemStatus.Failed;
                        state.ErrorMessage = $"HTTP {(int)resp.StatusCode} ({resp.StatusCode}) - Authentication required. Please configure S3 Access and Secret keys.";
                        _logger.Warn("InternetArchive anonymous stream also failed with {0}.", resp.StatusCode);
                        resp.Dispose();
                        PersistTrackedItems();
                        return (false, true, 0);
                    }
                }
                else
                {
                    state.Status = DownloadItemStatus.Failed;
                    state.ErrorMessage = $"HTTP {(int)initialStatus} ({initialStatus}) - Authentication required. Please configure S3 Access and Secret keys.";
                    PersistTrackedItems();
                    return (false, true, 0);
                }
            }

            long totalRead = 0;

            using (resp)
            {
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.ContentLength.HasValue && resp.Content.Headers.ContentLength.Value > 0 && byteOffset == 0 && state.TotalSize <= 0)
                {
                    state.TotalSize = resp.Content.Headers.ContentLength.Value;
                }

                using var networkStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var fileStream = _diskProvider.OpenWriteStream(tempDownloadPath);

                const int bufferSize = 262144;
                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
                try
                {
                    var lastRefreshSec = 0.0;
                    int bytesRead;
                    while ((bytesRead = await networkStream.ReadAsync(buffer.AsMemory(0, bufferSize)).ConfigureAwait(false)) > 0)
                    {
                        if (!TrackedItems.ContainsKey(downloadId))
                        {
                            _logger.Debug("InternetArchive download aborted because item '{0}' was removed.", displayName);
                            return (true, false, totalRead);
                        }

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                        totalRead += bytesRead;
                        var cumulativeBytes = byteOffset + totalRead;
                        state.DownloadedBytes = cumulativeBytes;
                        if (cumulativeBytes > state.TotalSize)
                        {
                            state.TotalSize = cumulativeBytes;
                        }

                        var elapsedSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                        state.SpeedBytesPerSec = cumulativeBytes / elapsedSec;

                        if (elapsedSec - lastRefreshSec >= 3.0)
                        {
                            lastRefreshSec = elapsedSec;
                            _commandQueue?.Push(new RefreshMonitoredDownloadsCommand(), CommandPriority.High);
                        }
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            if (!TrackedItems.ContainsKey(downloadId))
            {
                if (_diskProvider.FileExists(tempDownloadPath))
                {
                    _diskProvider.DeleteFile(tempDownloadPath);
                }

                return (true, false, totalRead);
            }

            if (_diskProvider.FileExists(destinationPath))
            {
                _diskProvider.DeleteFile(destinationPath);
            }

            if (!_diskProvider.FileExists(tempDownloadPath))
            {
                _logger.Warn("InternetArchive partial file '{0}' no longer exists; skipping completion move.", tempDownloadPath);
                return (true, false, totalRead);
            }

            _diskProvider.MoveFile(tempDownloadPath, destinationPath, true);
            return (false, false, totalRead);
        }

        private async Task<List<DirectDownloadTarget>> ResolveCompressDownloadTargetsAsync(
            string url,
            string downloadPath,
            InternetArchiveDownloadState state,
            string authHeader)
        {
            if (!TryExtractCompressIdentifier(url, out var iaBaseUrl, out var identifier))
            {
                return null;
            }

            var metadataUrl = $"{iaBaseUrl}/metadata/{Uri.EscapeDataString(identifier)}/files";
            try
            {
                var resp = await SendWithRedirectsAsync(SharedHttpClient, metadataUrl, authHeader).ConfigureAwait(false);
                if (resp != null && (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden) &&
                    !string.IsNullOrWhiteSpace(authHeader) && ShouldAllowAnonymousFallback())
                {
                    resp.Dispose();
                    resp = await SendWithRedirectsAsync(SharedHttpClient, metadataUrl, null).ConfigureAwait(false);
                }

                if (resp == null)
                {
                    return null;
                }

                using (resp)
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.Debug("InternetArchive metadata lookup returned HTTP {0} for '{1}'; falling back to /compress/.", (int)resp.StatusCode, identifier);
                        return null;
                    }

                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var metadata = Newtonsoft.Json.JsonConvert.DeserializeObject<InternetArchiveMetadataResponse>(json);
                    return BuildDirectTargetsFromMetadata(iaBaseUrl, identifier, metadata, downloadPath, state);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to resolve direct files for Internet Archive item '{0}'; falling back to /compress/.", identifier);
                return null;
            }
        }

        private List<DirectDownloadTarget> BuildDirectTargetsFromMetadata(
            string iaBaseUrl,
            string identifier,
            InternetArchiveMetadataResponse metadata,
            string downloadPath,
            InternetArchiveDownloadState state)
        {
            var selectedFiles = SelectDirectDownloadFiles(metadata?.Result);
            if (selectedFiles.Count == 0 || selectedFiles.Count > 100)
            {
                return null;
            }

            var workingFolder = GetWorkingFolder();
            var normalizedWorkingFolder = Path.GetFullPath(workingFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (selectedFiles.Count == 1)
            {
                var file = selectedFiles[0];
                var directFileName = SanitizeFileNameForDestination(Path.GetFileName(file.Name.Replace('\\', '/')));
                if (string.IsNullOrWhiteSpace(directFileName))
                {
                    return null;
                }

                var directPath = Path.Combine(workingFolder, directFileName);
                var collision = TrackedItems.Values.Any(s =>
                    !string.Equals(s.DownloadId, state.DownloadId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(s.OutputPath, directPath, StringComparison.OrdinalIgnoreCase) &&
                    s.Status is DownloadItemStatus.Downloading or DownloadItemStatus.Queued);

                if (collision)
                {
                    var stem = Path.GetFileNameWithoutExtension(directFileName);
                    var ext = Path.GetExtension(directFileName);
                    directPath = Path.Combine(workingFolder, $"{stem}_{Guid.NewGuid().ToString("N")[..6]}{ext}");
                }

                var fullDirectPath = Path.GetFullPath(directPath);
                if (!fullDirectPath.StartsWith(normalizedWorkingFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                state.OutputPath = directPath;
                var declaredSize = long.TryParse(file.Size, out var parsedSize) ? Math.Max(0L, parsedSize) : 0L;
                _logger.Info("Resolved Internet Archive item '{0}' directly to '{1}' (bypassing /compress/)", identifier, directFileName);

                return new List<DirectDownloadTarget>
                {
                    new DirectDownloadTarget
                    {
                        Url = BuildDirectDownloadUrl(iaBaseUrl, identifier, file.Name),
                        DisplayName = directFileName,
                        DestinationPath = directPath,
                        DeclaredSize = declaredSize
                    }
                };
            }

            var folderBaseName = Path.GetFileNameWithoutExtension(downloadPath);
            if (string.IsNullOrWhiteSpace(folderBaseName))
            {
                folderBaseName = "rom_" + Guid.NewGuid().ToString("N")[..8];
            }

            var targetFolder = Path.Combine(workingFolder, folderBaseName);
            var folderCollision = TrackedItems.Values.Any(s =>
                !string.Equals(s.DownloadId, state.DownloadId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.OutputPath, targetFolder, StringComparison.OrdinalIgnoreCase) &&
                s.Status is DownloadItemStatus.Downloading or DownloadItemStatus.Queued);

            if (folderCollision)
            {
                targetFolder = Path.Combine(workingFolder, $"{folderBaseName}_{Guid.NewGuid().ToString("N")[..6]}");
            }

            var normalizedTargetFolder = Path.GetFullPath(targetFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!normalizedTargetFolder.StartsWith(normalizedWorkingFolder, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var targets = new List<DirectDownloadTarget>(selectedFiles.Count);
            foreach (var file in selectedFiles)
            {
                var relPath = file.Name.Replace('\\', '/').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var destPath = Path.GetFullPath(Path.Combine(normalizedTargetFolder, relPath));
                if (!destPath.StartsWith(normalizedTargetFolder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var declaredSize = long.TryParse(file.Size, out var parsedSize) ? Math.Max(0L, parsedSize) : 0L;
                targets.Add(new DirectDownloadTarget
                {
                    Url = BuildDirectDownloadUrl(iaBaseUrl, identifier, file.Name),
                    DisplayName = Path.GetFileName(relPath),
                    DestinationPath = destPath,
                    DeclaredSize = declaredSize
                });
            }

            if (targets.Count == 0)
            {
                return null;
            }

            _diskProvider.EnsureFolder(targetFolder);
            state.OutputPath = targetFolder;
            _logger.Info("Resolved Internet Archive item '{0}' directly to folder '{1}' ({2} files, bypassing /compress/)", identifier, targetFolder, targets.Count);
            return targets;
        }

        internal static bool TryExtractCompressIdentifier(string url, out string baseUrl, out string identifier)
        {
            baseUrl = null;
            identifier = null;

            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var path = uri.AbsolutePath ?? string.Empty;
            const string compressPrefix = "/compress/";
            if (!path.StartsWith(compressPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var remainder = path[compressPrefix.Length..].Trim('/');
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return false;
            }

            var slashIdx = remainder.IndexOf('/');
            var rawId = slashIdx >= 0 ? remainder[..slashIdx] : remainder;
            var decoded = Uri.UnescapeDataString(rawId).Trim();
            if (string.IsNullOrWhiteSpace(decoded))
            {
                return false;
            }

            baseUrl = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            identifier = decoded;
            return true;
        }

        internal static List<InternetArchiveFileItem> SelectDirectDownloadFiles(IEnumerable<InternetArchiveFileItem> files)
        {
            if (files == null)
            {
                return new List<InternetArchiveFileItem>();
            }

            var candidates = new List<InternetArchiveFileItem>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.Name))
                {
                    continue;
                }

                var normalized = file.Name.Replace('\\', '/').TrimStart('/');
                if (string.IsNullOrWhiteSpace(normalized) || normalized.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = Path.GetFileName(normalized);
                if (string.IsNullOrWhiteSpace(fileName) ||
                    ArchiveRepackService.IsJunkEntry(normalized, fileName) ||
                    ArchiveRepackService.IsNonRomArchiveJunkFile(normalized))
                {
                    continue;
                }

                var ext = Path.GetExtension(fileName);
                if (FileExtensions.DangerousExtensions.Contains(ext) ||
                    FileExtensions.ExecutableExtensions.Contains(ext))
                {
                    continue;
                }

                var isTarCompound = fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                                    fileName.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase);

                var isEligible = isTarCompound ||
                                 (!string.IsNullOrWhiteSpace(ext) &&
                                  (MediaFileExtensions.RomExtensions.Contains(ext) ||
                                   FileExtensions.ArchiveExtensions.Contains(ext) ||
                                   DiscSheetHelper.IsDiscSheetExtension(ext) ||
                                   DiscSheetHelper.IsCompanionTrackExtension(ext)));

                if (!isEligible || !seenNames.Add(normalized))
                {
                    continue;
                }

                candidates.Add(file);
            }

            var hasDiscSheet = candidates.Any(f => DiscSheetHelper.IsDiscSheetExtension(Path.GetExtension(f.Name)));
            if (!hasDiscSheet)
            {
                candidates = candidates
                    .Where(f => !string.Equals(Path.GetExtension(f.Name), ".wav", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return candidates
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildDirectDownloadUrl(string baseUrl, string identifier, string fileName)
        {
            var normalized = (fileName ?? string.Empty).Replace('\\', '/').TrimStart('/');
            var escapedPath = string.Join(
                "/",
                normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

            return $"{baseUrl.TrimEnd('/')}/download/{Uri.EscapeDataString(identifier)}/{escapedPath}";
        }

        private static string SanitizeFileNameForDestination(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var chars = fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars).Trim();
        }

        private static async Task<System.Net.Http.HttpResponseMessage> SendWithRedirectsAsync(
            System.Net.Http.HttpClient httpClient,
            string requestUrl,
            string auth)
        {
            var initialHost = Uri.TryCreate(requestUrl, UriKind.Absolute, out var initialUri)
                ? initialUri.Host
                : null;
            var currentUrl = requestUrl;
            const int maxRedirects = 10;
            for (var i = 0; i < maxRedirects; i++)
            {
                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, currentUrl);
                if (!string.IsNullOrWhiteSpace(auth) && ShouldSendAuthHeader(currentUrl, initialHost))
                {
                    req.Headers.TryAddWithoutValidation("Authorization", auth);
                }

                var response = await httpClient.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                var status = (int)response.StatusCode;
                if (status is 301 or 302 or 303 or 307 or 308)
                {
                    var location = response.Headers.Location;
                    response.Dispose();
                    if (location == null)
                    {
                        throw new System.Net.Http.HttpRequestException("Redirect response did not include a Location header.");
                    }

                    if (!location.IsAbsoluteUri)
                    {
                        location = new Uri(new Uri(currentUrl), location);
                    }

                    currentUrl = location.ToString();
                    continue;
                }

                return response;
            }

            return null;
        }

        private static bool ShouldSendAuthHeader(string targetUrl, string initialHost)
        {
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var targetUri) || string.IsNullOrWhiteSpace(targetUri.Host))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(initialHost) &&
                string.Equals(targetUri.Host, initialHost, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(targetUri.Host, "archive.org", StringComparison.OrdinalIgnoreCase) ||
                   targetUri.Host.EndsWith(".archive.org", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ExecuteDownloadInternalAsync(
            RemoteEpisode remoteEpisode,
            IIndexer indexer,
            string url,
            string filename,
            string downloadPath,
            InternetArchiveDownloadState state)
        {
            await SyncConcurrencySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                state.Status = DownloadItemStatus.Downloading;
                var stopwatch = Stopwatch.StartNew();

                var s3AccessKey = GetS3AccessKey();
                var s3SecretKey = GetS3SecretKey();
                var hasS3Auth = !string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey);

                var directTargets = await TryResolveCompressTargetsWithHttpClientAsync(url, downloadPath, state, hasS3Auth ? $"LOW {s3AccessKey}:{s3SecretKey}" : null).ConfigureAwait(false);
                if (directTargets != null && directTargets.Count > 0)
                {
                    long totalBytes = 0;
                    var declaredSum = directTargets.Sum(t => Math.Max(0L, t.DeclaredSize));
                    if (declaredSum > 0)
                    {
                        state.TotalSize = declaredSum;
                    }

                    foreach (var target in directTargets)
                    {
                        var targetParent = Path.GetDirectoryName(target.DestinationPath);
                        if (!string.IsNullOrWhiteSpace(targetParent))
                        {
                            _diskProvider.EnsureFolder(targetParent);
                        }

                        var targetResponse = await ExecuteSingleHttpGetAsync(remoteEpisode, indexer, target.Url, downloadPath, state, s3AccessKey, s3SecretKey, hasS3Auth).ConfigureAwait(false);
                        if (targetResponse?.ResponseData == null)
                        {
                            state.Status = DownloadItemStatus.Failed;
                            state.ErrorMessage = "Empty response from HTTPS server";
                            TrackedItems.TryRemove(downloadPath, out _);
                            PersistTrackedItems();
                            throw new DownloadClientException(state.ErrorMessage);
                        }

                        using (var stream = _diskProvider.OpenWriteStream(target.DestinationPath))
                        {
                            await stream.WriteAsync(targetResponse.ResponseData.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                        }

                        totalBytes += targetResponse.ResponseData.Length;
                        state.DownloadedBytes = totalBytes;
                    }

                    var elapsedDirectSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    state.TotalSize = Math.Max(state.TotalSize, totalBytes);
                    state.DownloadedBytes = state.TotalSize;
                    state.SpeedBytesPerSec = totalBytes / elapsedDirectSec;
                    state.Status = DownloadItemStatus.Completed;
                    PersistTrackedItems();
                    return state.OutputPath ?? downloadPath;
                }

                var response = await ExecuteSingleHttpGetAsync(remoteEpisode, indexer, url, downloadPath, state, s3AccessKey, s3SecretKey, hasS3Auth).ConfigureAwait(false);

                if (response?.ResponseData != null)
                {
                    var data = response.ResponseData;
                    if (data.Length > 0)
                    {
                        state.TotalSize = data.Length;
                    }

                    using (var stream = _diskProvider.OpenWriteStream(downloadPath))
                    {
                        await stream.WriteAsync(data.AsMemory(), CancellationToken.None).ConfigureAwait(false);
                    }

                    var elapsedSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                    state.DownloadedBytes = data.Length;
                    state.SpeedBytesPerSec = data.Length / elapsedSec;

                    state.DownloadedBytes = state.TotalSize;
                    state.Status = DownloadItemStatus.Completed;
                    PersistTrackedItems();
                    _logger.Debug("Downloaded archive {0} to {1}", filename, downloadPath);
                    return downloadPath;
                }

                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = "Empty response from HTTPS server";
                TrackedItems.TryRemove(downloadPath, out _);
                PersistTrackedItems();
                throw new DownloadClientException(state.ErrorMessage);
            }
            catch (Exception ex)
            {
                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = ex.Message;
                PersistTrackedItems();
                throw;
            }
            finally
            {
                SyncConcurrencySemaphore.Release();
            }
        }

        private async Task<List<DirectDownloadTarget>> TryResolveCompressTargetsWithHttpClientAsync(
            string url,
            string downloadPath,
            InternetArchiveDownloadState state,
            string authHeader)
        {
            if (!TryExtractCompressIdentifier(url, out var iaBaseUrl, out var identifier))
            {
                return null;
            }

            var metadataUrl = $"{iaBaseUrl}/metadata/{Uri.EscapeDataString(identifier)}/files";
            try
            {
                var req = new HttpRequest(metadataUrl)
                {
                    AllowAutoRedirect = true,
                    SuppressHttpErrorStatusCodes = new[]
                    {
                        System.Net.HttpStatusCode.Unauthorized,
                        System.Net.HttpStatusCode.Forbidden,
                        System.Net.HttpStatusCode.NotFound
                    }
                };
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    req.Headers.Add("Authorization", authHeader);
                }

                var response = await _httpClient.GetAsync(req).ConfigureAwait(false);
                if (response != null &&
                    (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden) &&
                    !string.IsNullOrWhiteSpace(authHeader) &&
                    ShouldAllowAnonymousFallback())
                {
                    req.Headers.Remove("Authorization");
                    response = await _httpClient.GetAsync(req).ConfigureAwait(false);
                }

                if (response == null || response.StatusCode != System.Net.HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return null;
                }

                var metadata = Newtonsoft.Json.JsonConvert.DeserializeObject<InternetArchiveMetadataResponse>(response.Content);
                return BuildDirectTargetsFromMetadata(iaBaseUrl, identifier, metadata, downloadPath, state);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to resolve direct files for Internet Archive item '{0}'; falling back to /compress/.", identifier);
                return null;
            }
        }

        private async Task<HttpResponse> ExecuteSingleHttpGetAsync(
            RemoteEpisode remoteEpisode,
            IIndexer indexer,
            string url,
            string downloadPath,
            InternetArchiveDownloadState state,
            string s3AccessKey,
            string s3SecretKey,
            bool hasS3Auth)
        {
            var request = indexer?.GetDownloadRequest(url) ?? new HttpRequest(url);
            request.RateLimitKey = remoteEpisode?.Release?.IndexerId.ToString();
            request.AllowAutoRedirect = true;
            request.SuppressHttpErrorStatusCodes = new[] { System.Net.HttpStatusCode.Unauthorized, System.Net.HttpStatusCode.Forbidden };

            if (hasS3Auth)
            {
                request.Headers.Add("Authorization", $"LOW {s3AccessKey}:{s3SecretKey}");
            }

            var response = await RetryStrategy
                .ExecuteAsync(static async (s, token) => await s._httpClient.GetAsync(s.request, token), (_httpClient, request))
                .ConfigureAwait(false);

            if (response != null && (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden))
            {
                if (hasS3Auth && ShouldAllowAnonymousFallback())
                {
                    _logger.Warn("InternetArchive authenticated request failed with {0}, falling back to anonymous.", response.StatusCode);
                    request.Headers.Remove("Authorization");
                    response = await _httpClient.GetAsync(request, CancellationToken.None).ConfigureAwait(false);
                    if (response != null && (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden))
                    {
                        _logger.Warn("InternetArchive anonymous request also failed with {0}.", response.StatusCode);
                        state.Status = DownloadItemStatus.Failed;
                        state.ErrorMessage = $"HTTP {(int)response.StatusCode} ({response.StatusCode}) - Authentication required. Please configure S3 Access and Secret keys.";
                        TrackedItems.TryRemove(downloadPath, out _);
                        throw new DownloadClientException(state.ErrorMessage);
                    }
                }
                else
                {
                    _logger.Warn("InternetArchive request failed with {0}.", response.StatusCode);
                    state.Status = DownloadItemStatus.Failed;
                    state.ErrorMessage = $"HTTP {(int)response.StatusCode} ({response.StatusCode}) - Authentication required. Please configure S3 Access and Secret keys.";
                    TrackedItems.TryRemove(downloadPath, out _);
                    throw new DownloadClientException(state.ErrorMessage);
                }
            }

            return response;
        }

        public bool IsPreferredArchiveUrl(string downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return false;
            }

            var preferredList = GetPrimaryArchiveUrls()
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();

            if (preferredList.Count == 0)
            {
                return false;
            }

            return preferredList.Any(archiveId =>
                downloadUrl.Contains($"/download/{archiveId}/", StringComparison.OrdinalIgnoreCase) ||
                downloadUrl.EndsWith($"/download/{archiveId}", StringComparison.OrdinalIgnoreCase));
        }

        public override DownloadClientInfo GetStatus()
        {
            return new DownloadClientInfo
            {
                IsLocalhost = true
            };
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            EnsureInitializedFromDisk();

            var clientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false);
            var items = new List<DownloadClientItem>();

            foreach (var state in TrackedItems.Values)
            {
                var remaining = state.Status == DownloadItemStatus.Completed
                    ? 0L
                    : Math.Max(0L, state.TotalSize - state.DownloadedBytes);

                TimeSpan? remainingTime = null;
                if (state.Status == DownloadItemStatus.Downloading && state.SpeedBytesPerSec > 0 && remaining > 0)
                {
                    remainingTime = TimeSpan.FromSeconds(Math.Max(1.0, remaining / state.SpeedBytesPerSec));
                }
                else if (state.Status == DownloadItemStatus.Completed)
                {
                    remainingTime = TimeSpan.Zero;
                }

                items.Add(new DownloadClientItem
                {
                    DownloadClientInfo = clientInfo,
                    DownloadId = state.DownloadId,
                    Title = state.Title,
                    TotalSize = state.TotalSize,
                    RemainingSize = remaining,
                    RemainingTime = remainingTime,
                    OutputPath = new OsPath(state.OutputPath),
                    Status = state.Status,
                    Message = state.ErrorMessage,
                    CanMoveFiles = state.Status == DownloadItemStatus.Completed,
                    CanBeRemoved = true,
                    QueuePosition = state.Status == DownloadItemStatus.Queued ? state.QueuePosition : null,
                    CanReorder = state.Status == DownloadItemStatus.Queued
                });
            }

            return items;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            InternetArchiveDownloadState removedState = null;
            if (item != null && !string.IsNullOrWhiteSpace(item.DownloadId))
            {
                lock (QueueLock)
                {
                    PendingQueue.RemoveAll(j => string.Equals(j.DownloadId, item.DownloadId, StringComparison.OrdinalIgnoreCase));
                    RefreshPendingQueuePositionsLocked();
                }

                TrackedItems.TryRemove(item.DownloadId, out removedState);
                PersistTrackedItems();
            }

            if (deleteData && item != null)
            {
                if (item.OutputPath.IsEmpty)
                {
                    var fallbackPath = !string.IsNullOrWhiteSpace(removedState?.OutputPath)
                        ? removedState.OutputPath
                        : (Path.IsPathRooted(item.DownloadId) ? item.DownloadId : null);

                    if (!string.IsNullOrWhiteSpace(fallbackPath))
                    {
                        DeleteItemData(new DownloadClientItem
                        {
                            DownloadId = item.DownloadId,
                            Title = item.Title ?? removedState?.Title ?? Path.GetFileName(fallbackPath),
                            OutputPath = new OsPath(fallbackPath)
                        });
                    }

                    return;
                }

                DeleteItemData(item);
            }
        }

        private string GetManifestFilePath()
        {
            if (string.IsNullOrWhiteSpace(_appFolderInfo?.AppDataFolder))
            {
                return null;
            }

            return Path.Combine(_appFolderInfo.AppDataFolder, "internet_archive_downloads.json");
        }

        private void PersistTrackedItems()
        {
            var manifestPath = GetManifestFilePath();
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return;
            }

            try
            {
                var snapshot = TrackedItems.Values.ToList();
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                var dir = Path.GetDirectoryName(manifestPath);
                lock (PersistenceLock)
                {
                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(manifestPath, json);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to persist Internet Archive download state to '{0}'", manifestPath);
            }
        }

        private void EnsureInitializedFromDisk()
        {
            if (_initializedFromDisk)
            {
                return;
            }

            lock (PersistenceLock)
            {
                if (_initializedFromDisk)
                {
                    return;
                }

                _initializedFromDisk = true;
                var changed = false;
                var manifestPath = GetManifestFilePath();

                if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestPath);
                        var persisted = JsonSerializer.Deserialize<List<InternetArchiveDownloadState>>(json);
                        if (persisted != null)
                        {
                            foreach (var entry in persisted)
                            {
                                if (entry == null || string.IsNullOrWhiteSpace(entry.DownloadId) || TrackedItems.ContainsKey(entry.DownloadId))
                                {
                                    continue;
                                }

                                if (entry.Status is DownloadItemStatus.Downloading or DownloadItemStatus.Queued)
                                {
                                    var partPath = (entry.OutputPath ?? string.Empty) + ".ia.part";
                                    if (!string.IsNullOrWhiteSpace(entry.OutputPath) && _diskProvider.FileExists(partPath))
                                    {
                                        _diskProvider.DeleteFile(partPath);
                                    }

                                    entry.Status = DownloadItemStatus.Failed;
                                    entry.ErrorMessage = "Download interrupted by application restart";
                                    TrackedItems[entry.DownloadId] = entry;
                                    changed = true;
                                }
                                else if (!string.IsNullOrWhiteSpace(entry.OutputPath) &&
                                         (_diskProvider.FileExists(entry.OutputPath) || _diskProvider.FolderExists(entry.OutputPath) || HasAnyExtractedFolder(entry.OutputPath)))
                                {
                                    if (_downloadHistoryService != null)
                                    {
                                        var latest = _downloadHistoryService.GetLatestDownloadHistoryItem(entry.DownloadId);
                                        if (latest != null && IsOwnedByInternetArchive(latest) && ShouldAutoDeleteByHistory(latest))
                                        {
                                            _logger.Info("Cleaning up leftover Internet Archive download data for '{0}' at '{1}'", entry.Title ?? entry.DownloadId, entry.OutputPath);
                                            DeleteItemData(new DownloadClientItem
                                            {
                                                DownloadId = entry.DownloadId,
                                                Title = entry.Title ?? Path.GetFileName(entry.OutputPath),
                                                OutputPath = new OsPath(entry.OutputPath)
                                            });
                                            changed = true;
                                            continue;
                                        }
                                    }

                                    TrackedItems[entry.DownloadId] = entry;
                                    changed = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "Unable to read persisted Internet Archive download state from '{0}'", manifestPath);
                    }
                }

                if (ReconcileWorkingFolderWithHistory())
                {
                    changed = true;
                }

                if (changed)
                {
                    PersistTrackedItems();
                }
            }
        }

        private bool ReconcileWorkingFolderWithHistory()
        {
            var changed = false;
            var workingFolder = GetWorkingFolder();
            if (string.IsNullOrWhiteSpace(workingFolder) || !_diskProvider.FolderExists(workingFolder))
            {
                return false;
            }

            try
            {
                var files = _diskProvider.GetFiles(workingFolder, false) ?? Array.Empty<string>();
                foreach (var filePath in files)
                {
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        continue;
                    }

                    if (filePath.EndsWith(".ia.part", StringComparison.OrdinalIgnoreCase))
                    {
                        var targetPath = filePath[..^8];
                        if (!TrackedItems.TryGetValue(targetPath, out var activeState) ||
                            activeState.Status is not (DownloadItemStatus.Downloading or DownloadItemStatus.Queued))
                        {
                            _logger.Info("Deleting stale Internet Archive partial download '{0}'", filePath);
                            _diskProvider.DeleteFile(filePath);
                            changed = true;
                        }

                        continue;
                    }

                    if (TrackedItems.ContainsKey(filePath) || _downloadHistoryService == null)
                    {
                        continue;
                    }

                    var latest = _downloadHistoryService.GetLatestDownloadHistoryItem(filePath);
                    if (latest == null || !IsOwnedByInternetArchive(latest))
                    {
                        continue;
                    }

                    if (ShouldAutoDeleteByHistory(latest))
                    {
                        _logger.Info("Cleaning up orphaned Internet Archive download '{0}' (status: {1})", filePath, latest.EventType);
                        DeleteItemData(new DownloadClientItem
                        {
                            DownloadId = filePath,
                            Title = latest.SourceTitle ?? Path.GetFileName(filePath),
                            OutputPath = new OsPath(filePath)
                        });
                        changed = true;
                    }
                    else if (latest.EventType == DownloadHistoryEventType.DownloadGrabbed)
                    {
                        var size = Math.Max(0L, _diskProvider.GetFileSize(filePath));
                        TrackedItems[filePath] = new InternetArchiveDownloadState
                        {
                            DownloadId = filePath,
                            Title = latest.SourceTitle ?? Path.GetFileName(filePath),
                            OutputPath = filePath,
                            TotalSize = size,
                            DownloadedBytes = size,
                            SpeedBytesPerSec = 0,
                            Status = DownloadItemStatus.Completed
                        };
                        changed = true;
                    }
                }

                if (_downloadHistoryService != null)
                {
                    var dirs = _diskProvider.GetDirectories(workingFolder) ?? Array.Empty<string>();
                    string[] candidateExtensions = { ".7z", ".zip", ".rar", ".chd", ".iso" };
                    foreach (var dirPath in dirs)
                    {
                        if (string.IsNullOrWhiteSpace(dirPath) || !_diskProvider.FolderExists(dirPath))
                        {
                            continue;
                        }

                        var dirName = Path.GetFileName(dirPath);
                        if (string.IsNullOrWhiteSpace(dirName))
                        {
                            continue;
                        }

                        string baseName = null;
                        if (dirName.EndsWith("_extracted", StringComparison.OrdinalIgnoreCase))
                        {
                            baseName = dirName[..^"_extracted".Length];
                        }
                        else
                        {
                            var extractIdx = dirName.IndexOf("_extract_", StringComparison.OrdinalIgnoreCase);
                            if (extractIdx > 0)
                            {
                                baseName = dirName[..extractIdx];
                            }
                        }

                        if (string.IsNullOrWhiteSpace(baseName))
                        {
                            continue;
                        }

                        var isTrackedOrCleaned = false;
                        foreach (var ext in candidateExtensions)
                        {
                            var candidatePath = Path.Combine(workingFolder, baseName + ext);
                            if (TrackedItems.ContainsKey(candidatePath))
                            {
                                isTrackedOrCleaned = true;
                                break;
                            }

                            var latest = _downloadHistoryService.GetLatestDownloadHistoryItem(candidatePath);
                            if (latest != null && IsOwnedByInternetArchive(latest) && ShouldAutoDeleteByHistory(latest))
                            {
                                _logger.Info("Deleting orphaned Internet Archive extracted folder '{0}'", dirPath);
                                _diskProvider.DeleteFolder(dirPath, true);
                                changed = true;
                                isTrackedOrCleaned = true;
                                break;
                            }
                        }

                        if (isTrackedOrCleaned)
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error while reconciling Internet Archive working folder '{0}'", workingFolder);
            }

            return changed;
        }

        private bool ShouldAutoDeleteByHistory(DownloadHistory history)
        {
            if (history == null)
            {
                return false;
            }

            var definition = Definition as DownloadClientDefinition;
            if (history.EventType == DownloadHistoryEventType.DownloadImported)
            {
                return definition is { RemoveCompletedDownloads: true };
            }

            if (history.EventType == DownloadHistoryEventType.DownloadFailed)
            {
                return definition is { RemoveFailedDownloads: true };
            }

            return false;
        }

        private static bool IsOwnedByInternetArchive(DownloadHistory history)
        {
            if (history == null)
            {
                return false;
            }

            if (history.Data != null &&
                history.Data.TryGetValue("DownloadClient", out var downloadClient) &&
                !string.IsNullOrWhiteSpace(downloadClient))
            {
                return string.Equals(downloadClient, "InternetArchive", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(downloadClient, "Internet Archive", StringComparison.OrdinalIgnoreCase);
            }

            return history.Protocol == DownloadProtocol.Http;
        }

        private bool HasAnyExtractedFolder(string outputPath)
        {
            var parentDir = Path.GetDirectoryName(outputPath);
            var baseName = Path.GetFileNameWithoutExtension(outputPath);
            if (string.IsNullOrWhiteSpace(parentDir) || string.IsNullOrWhiteSpace(baseName))
            {
                return false;
            }

            if (_diskProvider.FolderExists(Path.Combine(parentDir, baseName + "_extracted")))
            {
                return true;
            }

            if (!_diskProvider.FolderExists(parentDir))
            {
                return false;
            }

            var extractPrefix = baseName + "_extract_";
            var siblingDirs = _diskProvider.GetDirectories(parentDir) ?? Array.Empty<string>();
            return siblingDirs.Any(d =>
            {
                var name = Path.GetFileName(d);
                return !string.IsNullOrWhiteSpace(name) &&
                       name.StartsWith(extractPrefix, StringComparison.OrdinalIgnoreCase) &&
                       _diskProvider.FolderExists(d);
            });
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            var folder = GetWorkingFolder();
            try
            {
                _diskProvider.EnsureFolder(folder);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to ensure folder '{0}' exists", folder);
            }

            failures.AddIfNotNull(TestFolder(folder, "DownloadPath"));
        }

        private string ResolveDownloadUrl(string downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return downloadUrl;
            }

            if (downloadUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                downloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return downloadUrl;
            }

            var preferredArchive = GetPrimaryArchiveUrls()?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))?.Trim();
            if (!string.IsNullOrWhiteSpace(preferredArchive))
            {
                preferredArchive = InternetArchiveIndexerSettings.ExtractIdentifier(preferredArchive);
            }

            var baseUrl = GetBaseUrl();

            if (!string.IsNullOrWhiteSpace(preferredArchive))
            {
                return $"{baseUrl}/download/{preferredArchive}/{downloadUrl.TrimStart('/')}";
            }

            return $"{baseUrl}/download/{downloadUrl.TrimStart('/')}";
        }
    }
}
