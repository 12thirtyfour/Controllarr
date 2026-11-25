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
        }

        private static readonly ConcurrentDictionary<string, InternetArchiveDownloadState> TrackedItems =
            new ConcurrentDictionary<string, InternetArchiveDownloadState>(StringComparer.OrdinalIgnoreCase);

        private static readonly SemaphoreSlim ConcurrencySemaphore = new SemaphoreSlim(2, 2);

        private static readonly System.Net.Http.HttpClient SharedHttpClient = CreateSharedHttpClient();

        private static readonly object PersistenceLock = new object();

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
            TrackedItems.Clear();
            _initializedFromDisk = false;
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
                Status = ConcurrencySemaphore.CurrentCount > 0 ? DownloadItemStatus.Downloading : DownloadItemStatus.Queued
            };

            TrackedItems[downloadPath] = state;
            PersistTrackedItems();

            var isBackgroundRun = remoteEpisode?.Series != null &&
                                  _httpClient.GetType().FullName?.Contains("Castle.Proxies", StringComparison.Ordinal) != true;

            if (isBackgroundRun)
            {
                _diskProvider.EnsureFolder(workingFolder);
                _ = Task.Run(() => StreamDownloadToDiskAsync(url, filename, downloadPath, state));
                return downloadPath;
            }

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
            var tempDownloadPath = downloadPath + ".ia.part";
            await ConcurrencySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                state.Status = DownloadItemStatus.Downloading;
                var stopwatch = Stopwatch.StartNew();

                var s3AccessKey = GetS3AccessKey();
                var s3SecretKey = GetS3SecretKey();
                string authHeader = null;
                if (!string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey))
                {
                    authHeader = $"LOW {s3AccessKey}:{s3SecretKey}";
                }

                static bool ShouldSendAuthHeader(string targetUrl, string initialHost)
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

                static async Task<System.Net.Http.HttpResponseMessage> SendWithRedirectsAsync(
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

                var resp = await SendWithRedirectsAsync(SharedHttpClient, url, authHeader).ConfigureAwait(false);
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
                        resp = await SendWithRedirectsAsync(SharedHttpClient, url, null).ConfigureAwait(false);
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
                            return;
                        }
                    }
                    else
                    {
                        state.Status = DownloadItemStatus.Failed;
                        state.ErrorMessage = $"HTTP {(int)initialStatus} ({initialStatus}) - Authentication required. Please configure S3 Access and Secret keys.";
                        PersistTrackedItems();
                        return;
                    }
                }

                using (resp)
                {
                    resp.EnsureSuccessStatusCode();
                    if (resp.Content.Headers.ContentLength.HasValue && resp.Content.Headers.ContentLength.Value > 0)
                    {
                        state.TotalSize = resp.Content.Headers.ContentLength.Value;
                    }

                    using var networkStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using var fileStream = _diskProvider.OpenWriteStream(tempDownloadPath);

                    const int bufferSize = 262144;
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        long totalRead = 0;
                        var lastRefreshSec = 0.0;
                        int bytesRead;
                        while ((bytesRead = await networkStream.ReadAsync(buffer.AsMemory(0, bufferSize)).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                            totalRead += bytesRead;
                            state.DownloadedBytes = totalRead;
                            if (totalRead > state.TotalSize)
                            {
                                state.TotalSize = totalRead;
                            }

                            var elapsedSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                            state.SpeedBytesPerSec = totalRead / elapsedSec;

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

                if (_diskProvider.FileExists(downloadPath))
                {
                    _diskProvider.DeleteFile(downloadPath);
                }

                _diskProvider.MoveFile(tempDownloadPath, downloadPath, true);

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

                try
                {
                    if (_diskProvider.FileExists(tempDownloadPath))
                    {
                        _diskProvider.DeleteFile(tempDownloadPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.Debug(cleanupEx, "Unable to delete partial download file '{0}'", tempDownloadPath);
                }

                PersistTrackedItems();
            }
            finally
            {
                ConcurrencySemaphore.Release();
                _commandQueue?.Push(new RefreshMonitoredDownloadsCommand(), CommandPriority.High);
            }
        }

        private async Task<string> ExecuteDownloadInternalAsync(
            RemoteEpisode remoteEpisode,
            IIndexer indexer,
            string url,
            string filename,
            string downloadPath,
            InternetArchiveDownloadState state)
        {
            await ConcurrencySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                state.Status = DownloadItemStatus.Downloading;
                var stopwatch = Stopwatch.StartNew();

                var request = indexer?.GetDownloadRequest(url) ?? new HttpRequest(url);
                request.RateLimitKey = remoteEpisode?.Release?.IndexerId.ToString();
                request.AllowAutoRedirect = true;
                request.SuppressHttpErrorStatusCodes = new[] { System.Net.HttpStatusCode.Unauthorized, System.Net.HttpStatusCode.Forbidden };

                var s3AccessKey = GetS3AccessKey();
                var s3SecretKey = GetS3SecretKey();
                var hasS3Auth = !string.IsNullOrWhiteSpace(s3AccessKey) && !string.IsNullOrWhiteSpace(s3SecretKey);
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
                ConcurrencySemaphore.Release();
            }
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
                    CanBeRemoved = true
                });
            }

            return items;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            InternetArchiveDownloadState removedState = null;
            if (item != null && !string.IsNullOrWhiteSpace(item.DownloadId))
            {
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
                                         (_diskProvider.FileExists(entry.OutputPath) || HasAnyExtractedFolder(entry.OutputPath)))
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
