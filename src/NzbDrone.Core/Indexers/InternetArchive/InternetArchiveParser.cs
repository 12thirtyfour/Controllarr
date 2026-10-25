using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveSearchResponse
    {
        [JsonProperty("response")]
        public InternetArchiveSearchResponseBody Response { get; set; }
    }

    public class InternetArchiveSearchResponseBody
    {
        [JsonProperty("numFound")]
        public int NumFound { get; set; }

        [JsonProperty("start")]
        public int Start { get; set; }

        [JsonProperty("docs")]
        public List<InternetArchiveDoc> Docs { get; set; }
    }

    public class InternetArchiveDoc
    {
        [JsonProperty("identifier")]
        public string Identifier { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("mediatype")]
        public string Mediatype { get; set; }

        [JsonProperty("date")]
        public DateTime? Date { get; set; }

        [JsonProperty("publicdate")]
        public DateTime? PublicDate { get; set; }

        [JsonProperty("item_size")]
        public long? ItemSize { get; set; }

        [JsonProperty("downloads")]
        public int? Downloads { get; set; }

        [JsonProperty("access-restricted-item")]
        public object AccessRestrictedItem { get; set; }

        public bool IsAccessRestricted()
        {
            if (AccessRestrictedItem == null)
            {
                return false;
            }

            var str = AccessRestrictedItem.ToString();
            return string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) || str == "1";
        }
    }

    public class InternetArchiveParser : IParseIndexerResponse
    {
        private readonly InternetArchiveIndexerSettings _settings;

        public InternetArchiveParser(InternetArchiveIndexerSettings settings)
        {
            _settings = settings;
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var httpResponse = indexerResponse.HttpResponse;

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new IndexerException(
                    indexerResponse,
                    "Unexpected response status code {0} from Internet Archive",
                    httpResponse.StatusCode);
            }

            if (indexerResponse.Content.IsNullOrWhiteSpace())
            {
                return new List<ReleaseInfo>();
            }

            InternetArchiveSearchResponse searchResponse;

            try
            {
                searchResponse = JsonConvert.DeserializeObject<InternetArchiveSearchResponse>(indexerResponse.Content);
            }
            catch (Exception ex)
            {
                throw new IndexerException(indexerResponse, "Failed to parse JSON response from Internet Archive", ex);
            }

            if (searchResponse?.Response?.Docs == null)
            {
                return new List<ReleaseInfo>();
            }

            var baseUrl = _settings?.BaseUrl?.TrimEnd('/') ?? "https://archive.org";
            var results = new List<ReleaseInfo>();

            foreach (var doc in searchResponse.Response.Docs)
            {
                if (doc.Identifier.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var title = doc.Title.IsNotNullOrWhiteSpace() ? doc.Title : doc.Identifier;
                var publishDate = doc.PublicDate ?? doc.Date ?? DateTime.UtcNow;
                var requiresCredentials = doc.IsAccessRestricted() || (_settings != null && _settings.RequireCredentials);

                var releaseInfo = new ReleaseInfo
                {
                    Guid = $"IA-{doc.Identifier}",
                    Title = title,
                    Size = doc.ItemSize ?? 0,
                    DownloadUrl = $"{baseUrl}/compress/{doc.Identifier}",
                    InfoUrl = $"{baseUrl}/details/{doc.Identifier}",
                    DownloadProtocol = DownloadProtocol.Http,
                    PublishDate = publishDate.ToUniversalTime(),
                    RequiresCredentials = requiresCredentials
                };

                results.Add(releaseInfo);
            }

            return results;
        }
    }
}
