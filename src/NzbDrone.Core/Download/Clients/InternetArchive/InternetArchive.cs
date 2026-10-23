using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.InternetArchive
{
    public class InternetArchive : DownloadClientBase<InternetArchiveSettings>
    {
        private readonly IHttpClient _httpClient;

        public override string Name => "InternetArchive";

        public override DownloadProtocol Protocol => DownloadProtocol.Http;

        public InternetArchive(IHttpClient httpClient,
                               IConfigService configService,
                               IDiskProvider diskProvider,
                               IRemotePathMappingService remotePathMappingService,
                               Logger logger,
                               ILocalizationService localizationService)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
            _httpClient = httpClient;
        }

        public override async Task<string> Download(RemoteEpisode remoteEpisode, IIndexer indexer)
        {
            var url = ResolveDownloadUrl(remoteEpisode?.Release?.DownloadUrl);
            var extension = Path.GetExtension(url);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = url != null && url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".chd";
            }

            var filename = Parser.Parser.CleanSeriesTitle(remoteEpisode.Release.Title) + extension;

            var request = indexer?.GetDownloadRequest(url) ?? new HttpRequest(url);
            request.RateLimitKey = remoteEpisode?.Release?.IndexerId.ToString();
            request.AllowAutoRedirect = true;
            request.SuppressHttpErrorStatusCodes = new[] { System.Net.HttpStatusCode.Unauthorized, System.Net.HttpStatusCode.Forbidden };

            if (!string.IsNullOrWhiteSpace(Settings.S3AccessKey) && !string.IsNullOrWhiteSpace(Settings.S3SecretKey))
            {
                request.Headers.Add("Authorization", $"LOW {Settings.S3AccessKey.Trim()}:{Settings.S3SecretKey.Trim()}");
            }

            var response = await RetryStrategy
                .ExecuteAsync(static async (state, token) => await state._httpClient.GetAsync(state.request, token), (_httpClient, request))
                .ConfigureAwait(false);

            if (response != null && (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden))
            {
                if (Settings.AllowAnonymousFallback)
                {
                    _logger.Warn("InternetArchive authenticated request failed with {0}, falling back to anonymous.", response.StatusCode);
                    request.Headers.Remove("Authorization");
                    response = await _httpClient.GetAsync(request, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    _logger.Warn("InternetArchive authenticated request failed with {0} and anonymous fallback is disabled.", response.StatusCode);
                    return null;
                }
            }

            if (response?.ResponseData != null)
            {
                var downloadPath = Path.Combine(_configService.DownloadClientWorkingFolders, filename);
                var fullDestinationPath = Path.GetFullPath(downloadPath);

                if (!fullDestinationPath.StartsWith(Path.GetFullPath(_configService.DownloadClientWorkingFolders) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new System.Security.SecurityException("Zip Slip / Path Traversal detected in download destination.");
                }

                using (var stream = _diskProvider.OpenWriteStream(downloadPath))
                using (var memoryStream = new MemoryStream(response.ResponseData))
                {
                    await memoryStream.CopyToAsync(stream, 81920, CancellationToken.None).ConfigureAwait(false);
                }

                _logger.Debug("Downloaded archive {0} to {1}", filename, downloadPath);
                return downloadPath;
            }

            return null;
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
            return new List<DownloadClientItem>();
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (deleteData && item != null && !string.IsNullOrWhiteSpace(item.OutputPath.FullPath))
            {
                _diskProvider.DeleteFile(item.OutputPath.FullPath);
            }
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestFolder(_configService.DownloadClientWorkingFolders, "DownloadClientWorkingFolders"));
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
