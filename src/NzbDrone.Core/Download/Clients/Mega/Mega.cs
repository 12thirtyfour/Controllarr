using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CG.Web.MegaApiClient;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.Mega
{
    public class Mega : DownloadClientBase<MegaSettings>
    {
        private sealed class MegaDownloadState
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

        private static readonly ConcurrentDictionary<string, MegaDownloadState> TrackedItems =
            new ConcurrentDictionary<string, MegaDownloadState>(StringComparer.OrdinalIgnoreCase);

        private static readonly SemaphoreSlim ConcurrencySemaphore = new SemaphoreSlim(2, 2);

        private readonly IManageCommandQueue _commandQueue;
        private readonly IAppFolderInfo _appFolderInfo;

        public override string Name => "Mega.nz";

        public override DownloadProtocol Protocol => DownloadProtocol.Http;

        public Mega(
            IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            Logger logger,
            ILocalizationService localizationService,
            IManageCommandQueue commandQueue = null,
            IAppFolderInfo appFolderInfo = null)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
            _commandQueue = commandQueue;
            _appFolderInfo = appFolderInfo;
        }

        public static bool IsMegaUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            var host = uri.Host;
            return host.Equals("mega.nz", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".mega.nz", StringComparison.OrdinalIgnoreCase) ||
                   host.Equals("mega.co.nz", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".mega.co.nz", StringComparison.OrdinalIgnoreCase);
        }

        private string GetWorkingFolder()
        {
            if (!string.IsNullOrWhiteSpace(Settings?.DownloadPath))
            {
                return Settings.DownloadPath.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_appFolderInfo?.AppDataFolder))
            {
                return Path.Combine(_appFolderInfo.AppDataFolder, "downloads");
            }

            return Path.Combine(Path.GetTempPath(), "Controllarr", "downloads");
        }

        public override async Task<string> Download(RemoteEpisode remoteEpisode, IIndexer indexer)
        {
            var url = remoteEpisode?.Release?.DownloadUrl;
            if (!IsMegaUrl(url))
            {
                throw new DownloadClientException("URL is not a Mega.nz link");
            }

            var rawTitle = remoteEpisode?.Release?.Title ?? "mega_download";
            var cleanTitle = Parser.Parser.CleanSeriesTitle(Path.GetFileNameWithoutExtension(rawTitle));
            if (string.IsNullOrWhiteSpace(cleanTitle))
            {
                cleanTitle = "mega_" + Guid.NewGuid().ToString("N")[..8];
            }

            var workingFolder = GetWorkingFolder();
            _diskProvider.EnsureFolder(workingFolder);

            var initialPath = Path.Combine(workingFolder, cleanTitle);
            var state = new MegaDownloadState
            {
                DownloadId = initialPath,
                Title = rawTitle,
                OutputPath = initialPath,
                TotalSize = Math.Max(0L, remoteEpisode?.Release?.Size ?? 0L),
                DownloadedBytes = 0,
                SpeedBytesPerSec = 0,
                Status = ConcurrencySemaphore.CurrentCount > 0 ? DownloadItemStatus.Downloading : DownloadItemStatus.Queued
            };

            TrackedItems[initialPath] = state;

            _ = Task.Run(() => ExecuteMegaDownloadAsync(url, workingFolder, cleanTitle, state));
            return await Task.FromResult(initialPath).ConfigureAwait(false);
        }

        private async Task ExecuteMegaDownloadAsync(string url, string workingFolder, string fallbackName, MegaDownloadState state)
        {
            await ConcurrencySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                state.Status = DownloadItemStatus.Downloading;
                var stopwatch = Stopwatch.StartNew();
                var lastRefreshSec = 0.0;

                var downloadedPath = await DownloadFromMegaAsync(
                    url,
                    workingFolder,
                    fallbackName,
                    Settings?.Email,
                    Settings?.Password,
                    (bytes, total, speed) =>
                    {
                        if (total > 0)
                        {
                            state.TotalSize = total;
                        }

                        state.DownloadedBytes = bytes;
                        state.SpeedBytesPerSec = speed;

                        var elapsedSec = stopwatch.Elapsed.TotalSeconds;
                        if (elapsedSec - lastRefreshSec >= 4.0)
                        {
                            lastRefreshSec = elapsedSec;
                            _commandQueue?.Push(new RefreshMonitoredDownloadsCommand(), CommandPriority.High);
                        }
                    },
                    _logger,
                    CancellationToken.None).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(downloadedPath))
                {
                    state.Status = DownloadItemStatus.Failed;
                    state.ErrorMessage = "Mega.nz download returned no output file";
                    return;
                }

                state.OutputPath = downloadedPath;
                if (File.Exists(downloadedPath))
                {
                    var finalSize = new FileInfo(downloadedPath).Length;
                    state.TotalSize = Math.Max(state.TotalSize, finalSize);
                    state.DownloadedBytes = state.TotalSize;
                }

                state.SpeedBytesPerSec = stopwatch.Elapsed.TotalSeconds > 0
                    ? state.DownloadedBytes / Math.Max(0.05d, stopwatch.Elapsed.TotalSeconds)
                    : 0d;
                state.Status = DownloadItemStatus.Completed;
                _logger.Info("Mega.nz download completed: {0} -> {1}", state.Title, downloadedPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Mega.nz download failed for {0}", state.Title);
                state.Status = DownloadItemStatus.Failed;
                state.ErrorMessage = ex.Message;
            }
            finally
            {
                ConcurrencySemaphore.Release();
                _commandQueue?.Push(new RefreshMonitoredDownloadsCommand(), CommandPriority.High);
            }
        }

        public static async Task<string> DownloadFromMegaAsync(
            string url,
            string destinationFolder,
            string fallbackFileName = null,
            string email = null,
            string password = null,
            Action<long, long, double> progressCallback = null,
            Logger logger = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsMegaUrl(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            {
                return null;
            }

            Directory.CreateDirectory(destinationFolder);
            var client = new MegaApiClient();
            var loggedIn = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
                {
                    await client.LoginAsync(email.Trim(), password).ConfigureAwait(false);
                }
                else
                {
                    await client.LoginAnonymousAsync().ConfigureAwait(false);
                }

                loggedIn = true;

                // Always download directly to local disk; never import/add to the user's Mega account.
                var isFolderLink = url.Contains("/folder/", StringComparison.OrdinalIgnoreCase) ||
                                   url.Contains("#F!", StringComparison.OrdinalIgnoreCase);

                if (isFolderLink)
                {
                    var folderNodes = await client.GetNodesFromLinkAsync(uri).ConfigureAwait(false);
                    var nodes = folderNodes?.Where(n => n.Type == NodeType.File).ToList();
                    if (nodes == null || nodes.Count == 0)
                    {
                        return null;
                    }

                    if (!string.IsNullOrWhiteSpace(fallbackFileName))
                    {
                        var fallbackBase = Path.GetFileNameWithoutExtension(fallbackFileName);
                        var matchingNodes = nodes.Where(n =>
                            string.Equals(Path.GetFileName(n.Name), fallbackFileName, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrWhiteSpace(fallbackBase) &&
                             string.Equals(Path.GetFileNameWithoutExtension(n.Name), fallbackBase, StringComparison.OrdinalIgnoreCase)))
                            .ToList();

                        if (matchingNodes.Count > 0)
                        {
                            nodes = matchingNodes.Take(1).ToList();
                        }
                        else if (!string.IsNullOrWhiteSpace(Path.GetExtension(fallbackFileName)))
                        {
                            return null;
                        }
                    }

                    var targetDir = string.IsNullOrWhiteSpace(fallbackFileName)
                        ? destinationFolder
                        : Path.Combine(destinationFolder, fallbackFileName);
                    Directory.CreateDirectory(targetDir);

                    string firstFile = null;
                    foreach (var node in nodes)
                    {
                        var safeName = Path.GetFileName(node.Name);
                        if (string.IsNullOrWhiteSpace(safeName))
                        {
                            continue;
                        }

                        var outPath = Path.Combine(targetDir, safeName);
                        await DownloadSingleNodeAsync(client, node, outPath, progressCallback, cancellationToken).ConfigureAwait(false);
                        firstFile ??= outPath;
                    }

                    return nodes.Count == 1 ? firstFile : targetDir;
                }
                else
                {
                    var node = await client.GetNodeFromLinkAsync(uri).ConfigureAwait(false);
                    if (node == null)
                    {
                        return null;
                    }

                    var safeName = Path.GetFileName(node.Name);
                    if (string.IsNullOrWhiteSpace(safeName))
                    {
                        safeName = string.IsNullOrWhiteSpace(fallbackFileName) ? $"mega_{Guid.NewGuid():N}.bin" : fallbackFileName;
                    }

                    var outPath = Path.Combine(destinationFolder, safeName);
                    await DownloadSingleNodeAsync(client, node, outPath, progressCallback, cancellationToken).ConfigureAwait(false);
                    return outPath;
                }
            }
            catch (Exception ex)
            {
                logger?.Warn(ex, "Mega.nz download failed for {0}", url);
                throw;
            }
            finally
            {
                if (loggedIn)
                {
                    try
                    {
                        await client.LogoutAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static async Task DownloadSingleNodeAsync(
            MegaApiClient client,
            INode node,
            string outPath,
            Action<long, long, double> progressCallback,
            CancellationToken cancellationToken)
        {
            var totalSize = Math.Max(0L, node.Size);
            var stopwatch = Stopwatch.StartNew();

            if (File.Exists(outPath))
            {
                File.Delete(outPath);
            }

            var progress = new Progress<double>(pct =>
            {
                if (progressCallback == null || totalSize <= 0)
                {
                    return;
                }

                var downloaded = (long)Math.Round(totalSize * Math.Clamp(pct, 0d, 100d) / 100d);
                var speed = stopwatch.Elapsed.TotalSeconds > 0
                    ? downloaded / Math.Max(0.05d, stopwatch.Elapsed.TotalSeconds)
                    : 0d;
                progressCallback(downloaded, totalSize, speed);
            });

            await client.DownloadFileAsync(node, outPath, progress, cancellationToken).ConfigureAwait(false);
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

            if (deleteData && item != null && !string.IsNullOrWhiteSpace(item.OutputPath.FullPath))
            {
                if (_diskProvider.FileExists(item.OutputPath.FullPath))
                {
                    _diskProvider.DeleteFile(item.OutputPath.FullPath);
                }
                else if (_diskProvider.FolderExists(item.OutputPath.FullPath))
                {
                    _diskProvider.DeleteFolder(item.OutputPath.FullPath, true);
                }
            }
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

            if (!string.IsNullOrWhiteSpace(Settings?.Email) && !string.IsNullOrWhiteSpace(Settings?.Password))
            {
                var client = new MegaApiClient();
                try
                {
                    client.Login(Settings.Email.Trim(), Settings.Password);
                    client.Logout();
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Mega.nz authentication test failed");
                    failures.Add(new ValidationFailure("Email", $"Mega.nz login failed: {ex.Message}"));
                }
            }
        }
    }
}
