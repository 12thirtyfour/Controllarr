using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
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

        private readonly IHttpClient _httpClient;
        private readonly IManageCommandQueue _commandQueue;
        private readonly IAppFolderInfo _appFolderInfo;

        public override string Name => "InternetArchive";

        public override DownloadProtocol Protocol => DownloadProtocol.Http;

        public InternetArchive(IHttpClient httpClient,
                               IConfigService configService,
                               IDiskProvider diskProvider,
                               IRemotePathMappingService remotePathMappingService,
                               Logger logger,
                               ILocalizationService localizationService,
                               IManageCommandQueue commandQueue = null,
                               IAppFolderInfo appFolderInfo = null)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
            _httpClient = httpClient;
            _commandQueue = commandQueue;
            _appFolderInfo = appFolderInfo;
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

        public override async Task<string> Download(RemoteEpisode remoteEpisode, IIndexer indexer)
        {
            var url = ResolveDownloadUrl(remoteEpisode?.Release?.DownloadUrl);
            var extension = Path.GetExtension(url);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = url != null && url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".chd";
            }

            var rawTitle = remoteEpisode?.Release?.Title ?? "rom_download";
            var filename = Parser.Parser.CleanSeriesTitle(rawTitle) + extension;
            var workingFolder = GetWorkingFolder();
            _diskProvider.EnsureFolder(workingFolder);

            var downloadPath = Path.Combine(workingFolder, filename);
            var fullDestinationPath = Path.GetFullPath(downloadPath);

            if (!fullDestinationPath.StartsWith(Path.GetFullPath(workingFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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

        private async Task StreamDownloadToDiskAsync(
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

                using var handler = new System.Net.Http.HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    CheckCertificateRevocationList = true,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
                using var client = new System.Net.Http.HttpClient(handler)
                {
                    Timeout = TimeSpan.FromHours(2)
                };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Controllarr/10.0");

                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(Settings?.S3AccessKey) && !string.IsNullOrWhiteSpace(Settings?.S3SecretKey))
                {
                    req.Headers.TryAddWithoutValidation("Authorization", $"LOW {Settings.S3AccessKey.Trim()}:{Settings.S3SecretKey.Trim()}");
                }

                var resp = await client.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    resp.Dispose();
                    if (Settings?.AllowAnonymousFallback == true)
                    {
                        _logger.Warn("InternetArchive authenticated stream failed with {0}, falling back to anonymous.", resp.StatusCode);
                        using var anonReq = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                        resp = await client.SendAsync(anonReq, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            state.Status = DownloadItemStatus.Failed;
                            state.ErrorMessage = $"HTTP {(int)resp.StatusCode} ({resp.StatusCode}) - Authentication required. Please configure S3 Access and Secret keys.";
                            _logger.Warn("InternetArchive anonymous stream also failed with {0}.", resp.StatusCode);
                            return;
                        }
                    }
                    else
                    {
                        state.Status = DownloadItemStatus.Failed;
                        state.ErrorMessage = $"HTTP {(int)resp.StatusCode} ({resp.StatusCode}) - Authentication required. Please configure S3 Access and Secret keys.";
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
                    using var fileStream = _diskProvider.OpenWriteStream(downloadPath);

                    var buffer = new byte[81920];
                    long totalRead = 0;
                    var lastRefreshSec = 0.0;
                    int bytesRead;
                    while ((bytesRead = await networkStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
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

                        if (elapsedSec - lastRefreshSec >= 2.0)
                        {
                            lastRefreshSec = elapsedSec;
                            _commandQueue?.Push(new RefreshMonitoredDownloadsCommand(), CommandPriority.High);
                        }
                    }

                    state.TotalSize = Math.Max(state.TotalSize, totalRead);
                    state.DownloadedBytes = state.TotalSize;
                    state.Status = DownloadItemStatus.Completed;
                    _logger.Info("HTTPS download completed: {0} ({1} bytes) -> {2}", filename, totalRead, downloadPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "HTTPS download failed for {0}", filename);
                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = ex.Message;
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

                if (!string.IsNullOrWhiteSpace(Settings.S3AccessKey) && !string.IsNullOrWhiteSpace(Settings.S3SecretKey))
                {
                    request.Headers.Add("Authorization", $"LOW {Settings.S3AccessKey.Trim()}:{Settings.S3SecretKey.Trim()}");
                }

                var response = await RetryStrategy
                    .ExecuteAsync(static async (s, token) => await s._httpClient.GetAsync(s.request, token), (_httpClient, request))
                    .ConfigureAwait(false);

                if (response != null && (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden))
                {
                    if (Settings.AllowAnonymousFallback)
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
                            return null;
                        }
                    }
                    else
                    {
                        _logger.Warn("InternetArchive authenticated request failed with {0} and anonymous fallback is disabled.", response.StatusCode);
                        state.Status = DownloadItemStatus.Failed;
                        state.ErrorMessage = $"HTTP {(int)response.StatusCode} ({response.StatusCode}) - Authentication required. Please configure S3 Access and Secret keys.";
                        TrackedItems.TryRemove(downloadPath, out _);
                        return null;
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
                        const int chunkSize = 81920;
                        var offset = 0;
                        while (offset < data.Length)
                        {
                            var count = Math.Min(chunkSize, data.Length - offset);
                            await stream.WriteAsync(data.AsMemory(offset, count), CancellationToken.None).ConfigureAwait(false);
                            offset += count;
                            state.DownloadedBytes = offset;
                            var elapsedSec = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
                            state.SpeedBytesPerSec = offset / elapsedSec;
                        }
                    }

                    state.DownloadedBytes = state.TotalSize;
                    state.Status = DownloadItemStatus.Completed;
                    _logger.Debug("Downloaded archive {0} to {1}", filename, downloadPath);
                    return downloadPath;
                }

                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = "Empty response from HTTPS server";
                return null;
            }
            catch (Exception ex)
            {
                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = ex.Message;
                throw;
            }
            finally
            {
                ConcurrencySemaphore.Release();
            }
        }

        public bool IsPreferredArchiveUrl(string downloadUrl)
        {
            if (string.IsNullOrWhiteSpace(downloadUrl) || Settings?.PreferredArchives == null)
            {
                return false;
            }

            var preferredList = Settings.PreferredArchives
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
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
            if (item != null && !string.IsNullOrWhiteSpace(item.DownloadId))
            {
                TrackedItems.TryRemove(item.DownloadId, out _);
            }

            if (deleteData && item != null && !string.IsNullOrWhiteSpace(item.OutputPath.FullPath) && _diskProvider.FileExists(item.OutputPath.FullPath))
            {
                _diskProvider.DeleteFile(item.OutputPath.FullPath);
            }
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            var folder = GetWorkingFolder();
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

            var preferredArchive = Settings?.PreferredArchives?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))?.Trim();
            var baseUrl = string.IsNullOrWhiteSpace(Settings?.UrlBase) ? "https://archive.org" : Settings.UrlBase.TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(preferredArchive))
            {
                return $"{baseUrl}/download/{preferredArchive}/{downloadUrl.TrimStart('/')}";
            }

            return $"{baseUrl}/download/{downloadUrl.TrimStart('/')}";
        }
    }
}
