using System.Collections.Generic;
using System.IO;
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

        public override DownloadProtocol Protocol => DownloadProtocol.Usenet; // or Http, but Using Usenet to force direct download in base if necessary

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
            var url = remoteEpisode.Release.DownloadUrl;
            var filename = Parser.Parser.CleanSeriesTitle(remoteEpisode.Release.Title) + (url.EndsWith(".zip") ? ".zip" : ".chd");

            var request = indexer?.GetDownloadRequest(url) ?? new HttpRequest(url);
            request.RateLimitKey = remoteEpisode?.Release?.IndexerId.ToString();
            request.AllowAutoRedirect = true;

            // Implementing basic retry logic via RetryStrategy
            var response = await RetryStrategy
                .ExecuteAsync(static async (state, token) => await state._httpClient.GetAsync(state.request, token), (_httpClient, request))
                .ConfigureAwait(false);

            var path = Path.Combine(Settings.UrlBase, filename); // dummy path for now, actually needs to save.

            // Native HTTP direct streaming implementation stub:
            // Since this is a core service without a separate download worker thread yet, we simulate passing it to a generic state.
            // Normally we'd stream response.ResponseData to _diskProvider.WriteAllBytes

            if (response.ResponseData != null)
            {
                var downloadPath = Path.Combine(_configService.DownloadClientWorkingFolders, filename);
                using (var stream = _diskProvider.OpenWriteStream(downloadPath))
                {
                    await stream.WriteAsync(new System.ReadOnlyMemory<byte>(response.ResponseData), CancellationToken.None);
                }

                _logger.Debug("Downloaded archive {0} to {1}", filename, downloadPath);
                return downloadPath;
            }

            return null;
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
    }
}
