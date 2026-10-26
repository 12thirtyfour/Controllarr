using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveIndexer : HttpIndexerBase<InternetArchiveIndexerSettings>
    {
        public override string Name => "InternetArchive";

        public override DownloadProtocol Protocol => DownloadProtocol.Http;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;

        public override IEnumerable<FailDownloads> DefaultFailDownloads =>
            [FailDownloads.Executables, FailDownloads.PotentiallyDangerous, FailDownloads.UserDefinedExtensions];

        public InternetArchiveIndexer(
            IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger,
            ILocalizationService localizationService)
            : base(httpClient, indexerStatusService, configService, parsingService, logger, localizationService)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new InternetArchiveRequestGenerator { Settings = Settings };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new InternetArchiveParser(Settings);
        }

        protected override async Task<ValidationFailure> TestConnection()
        {
            try
            {
                var baseUrl = Settings?.BaseUrl?.TrimEnd('/') ?? "https://archive.org";
                var primaryIds = Settings?.PrimaryArchiveUrls?
                    .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();

                HttpRequest request;
                if (primaryIds != null && primaryIds.Any())
                {
                    request = new HttpRequest($"{baseUrl}/metadata/{primaryIds.First()}", HttpAccept.Json);
                }
                else
                {
                    request = new HttpRequest($"{baseUrl}/advancedsearch.php?q=mediatype%3Asoftware&rows=1&output=json", HttpAccept.Json);
                }

                if (!string.IsNullOrWhiteSpace(Settings?.S3AccessKey) && !string.IsNullOrWhiteSpace(Settings?.S3SecretKey))
                {
                    request.Headers.Add("Authorization", $"LOW {Settings.S3AccessKey.Trim()}:{Settings.S3SecretKey.Trim()}");
                }

                var response = await _httpClient.ExecuteAsync(request);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return new ValidationFailure(string.Empty, "Internet Archive authentication failed. Please verify your S3 Access and Secret keys.");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new ValidationFailure(string.Empty, "Internet Archive returned 404 Not Found. Please verify your primary archive URL or identifier.");
                }

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return new ValidationFailure(string.Empty, $"Failed to connect to Internet Archive: HTTP {(int)response.StatusCode} ({response.StatusCode})");
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Internet Archive test connection failed");
                return new ValidationFailure(string.Empty, $"Unable to connect to Internet Archive: {ex.Message}");
            }
        }
    }
}
