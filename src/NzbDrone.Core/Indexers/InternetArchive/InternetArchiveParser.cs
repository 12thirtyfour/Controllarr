using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveMetadataResponse
    {
        [JsonProperty("result")]
        public List<InternetArchiveFileItem> Result { get; set; }
    }

    public class InternetArchiveFileItem
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }

        [JsonProperty("format")]
        public string Format { get; set; }

        [JsonProperty("mtime")]
        public long? Mtime { get; set; }

        [JsonProperty("crc32")]
        public string Crc32 { get; set; }

        [JsonProperty("sha1")]
        public string Sha1 { get; set; }

        [JsonProperty("md5")]
        public string Md5 { get; set; }

        [JsonProperty("private")]
        public object Private { get; set; }

        public bool IsPrivate()
        {
            if (Private == null)
            {
                return false;
            }

            var str = Private.ToString();
            return string.Equals(str, "true", StringComparison.OrdinalIgnoreCase) || str == "1";
        }
    }

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
        private static readonly Regex MetadataIdentifierRegex = new Regex(@"/metadata/(?<id>[^/?#]+)/files", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly HashSet<string> IgnoredArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".xml", ".sqlite", ".torrent", ".json", ".txt", ".png", ".jpg", ".jpeg"
        };

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

            var requestUrl = indexerResponse.HttpRequest?.Url?.FullUri ?? string.Empty;
            var baseUrl = _settings?.BaseUrl?.TrimEnd('/') ?? "https://archive.org";
            var results = new List<ReleaseInfo>();

            string platformSlug = null;
            string platformName = null;

            var platformIdx = requestUrl.IndexOf("_platform=", StringComparison.OrdinalIgnoreCase);
            if (platformIdx >= 0)
            {
                var endIdx = requestUrl.IndexOf('&', platformIdx);
                var rawSlug = endIdx >= 0
                    ? requestUrl.Substring(platformIdx + 10, endIdx - (platformIdx + 10))
                    : requestUrl.Substring(platformIdx + 10);
                platformSlug = Uri.UnescapeDataString(rawSlug);
            }

            var platformNameIdx = requestUrl.IndexOf("_platformName=", StringComparison.OrdinalIgnoreCase);
            if (platformNameIdx >= 0)
            {
                var endIdx = requestUrl.IndexOf('&', platformNameIdx);
                var rawName = endIdx >= 0
                    ? requestUrl.Substring(platformNameIdx + 14, endIdx - (platformNameIdx + 14))
                    : requestUrl.Substring(platformNameIdx + 14);
                platformName = Uri.UnescapeDataString(rawName);
            }

            if (requestUrl.Contains("/metadata/", StringComparison.OrdinalIgnoreCase))
            {
                var match = MetadataIdentifierRegex.Match(requestUrl);
                var archiveId = match.Success ? match.Groups["id"].Value : "archive";

                string searchTerm = null;
                var queryIdx = requestUrl.IndexOf("?search=", StringComparison.OrdinalIgnoreCase);
                if (queryIdx >= 0)
                {
                    var searchSub = requestUrl[(queryIdx + 8)..];
                    var nextParamIdx = searchSub.IndexOf('&');
                    searchTerm = Uri.UnescapeDataString(nextParamIdx >= 0 ? searchSub[..nextParamIdx] : searchSub);
                }

                InternetArchiveMetadataResponse metadataResponse;
                try
                {
                    metadataResponse = JsonConvert.DeserializeObject<InternetArchiveMetadataResponse>(indexerResponse.Content);
                }
                catch (Exception ex)
                {
                    throw new IndexerException(indexerResponse, "Failed to parse metadata JSON response from Internet Archive", ex);
                }

                if (metadataResponse?.Result == null)
                {
                    return results;
                }

                foreach (var file in metadataResponse.Result)
                {
                    if (string.IsNullOrWhiteSpace(file.Name))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(file.Name);
                    if (IgnoredArchiveExtensions.Contains(ext) || PlatformCompatibilityValidator.IsTvOrVideoRelease(file.Name, ext))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(platformSlug) &&
                        !PlatformCompatibilityValidator.IsCompatible(file.Name, platformSlug, platformName, file.Format, archiveId))
                    {
                        continue;
                    }

                    var fileNameOnly = Path.GetFileName(file.Name);
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var matchesName = fileNameOnly.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          file.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                          RomParser.CleanIndexPrefix(fileNameOnly).Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
                        if (!matchesName)
                        {
                            continue;
                        }
                    }

                    var size = long.TryParse(file.Size, out var parsedSize) ? parsedSize : 0;
                    var publishDate = file.Mtime.HasValue
                        ? DateTimeOffset.FromUnixTimeSeconds(file.Mtime.Value).UtcDateTime
                        : DateTime.UtcNow;
                    var requiresCredentials = file.IsPrivate() || (_settings != null && _settings.RequireCredentials);
                    var container = !string.IsNullOrWhiteSpace(ext) ? ext.TrimStart('.').ToUpperInvariant() : null;
                    var parsedRomInfo = RomParser.ParseRomName(file.Name);
                    if (parsedRomInfo != null && !string.IsNullOrWhiteSpace(container))
                    {
                        parsedRomInfo.Format = container.ToLowerInvariant();
                    }

                    var releaseInfo = new ReleaseInfo
                    {
                        Guid = $"IA-{archiveId}-{file.Name}",
                        Title = fileNameOnly,
                        Size = size,
                        DownloadUrl = $"{baseUrl}/download/{archiveId}/{Uri.EscapeDataString(file.Name.TrimStart('/'))}",
                        InfoUrl = $"{baseUrl}/details/{archiveId}",
                        DownloadProtocol = DownloadProtocol.Http,
                        PublishDate = publishDate,
                        RequiresCredentials = requiresCredentials,
                        Origin = file.Name,
                        Container = container,
                        ParsedRomInfo = parsedRomInfo
                    };

                    results.Add(releaseInfo);
                }

                return results;
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

            foreach (var doc in searchResponse.Response.Docs)
            {
                if (doc.Identifier.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var title = doc.Title.IsNotNullOrWhiteSpace() ? doc.Title : doc.Identifier;
                if (PlatformCompatibilityValidator.IsTvOrVideoRelease(title))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(platformSlug) &&
                    !PlatformCompatibilityValidator.IsCompatible(title, platformSlug, platformName, doc.Mediatype, doc.Identifier))
                {
                    continue;
                }

                var publishDate = doc.PublicDate ?? doc.Date ?? DateTime.UtcNow;
                var requiresCredentials = doc.IsAccessRestricted() || (_settings != null && _settings.RequireCredentials);
                var docExt = Path.GetExtension(title);
                var docContainer = !string.IsNullOrWhiteSpace(docExt) && docExt.Length <= 6 ? docExt.TrimStart('.').ToUpperInvariant() : null;
                var docRomInfo = RomParser.ParseRomName(title);
                if (docRomInfo != null && !string.IsNullOrWhiteSpace(docContainer))
                {
                    docRomInfo.Format = docContainer.ToLowerInvariant();
                }

                var releaseInfo = new ReleaseInfo
                {
                    Guid = $"IA-{doc.Identifier}",
                    Title = title,
                    Size = doc.ItemSize ?? 0,
                    DownloadUrl = $"{baseUrl}/compress/{doc.Identifier}",
                    InfoUrl = $"{baseUrl}/details/{doc.Identifier}",
                    DownloadProtocol = DownloadProtocol.Http,
                    PublishDate = publishDate.ToUniversalTime(),
                    RequiresCredentials = requiresCredentials,
                    Container = docContainer,
                    ParsedRomInfo = docRomInfo
                };

                results.Add(releaseInfo);
            }

            return results;
        }
    }
}
