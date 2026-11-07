using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    public class SingleOrArrayStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var token = JToken.Load(reader);
                if (token is JArray array && array.Count > 0)
                {
                    return array[0]?.ToString();
                }

                return null;
            }

            return reader.Value?.ToString();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    public class FlexibleDateTimeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(DateTime) || objectType == typeof(DateTime?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.Date)
            {
                return reader.Value as DateTime?;
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var token = JToken.Load(reader);
                if (token is JArray array && array.Count > 0)
                {
                    return ParseDateTimeString(array[0]?.ToString());
                }

                return null;
            }

            if (reader.TokenType == JsonToken.String || reader.TokenType == JsonToken.Integer)
            {
                return ParseDateTimeString(reader.Value?.ToString());
            }

            return null;
        }

        private static DateTime? ParseDateTimeString(string val)
        {
            if (string.IsNullOrWhiteSpace(val))
            {
                return null;
            }

            if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt;
            }

            if (int.TryParse(val, out var year) && year >= 1970 && year <= 2100)
            {
                return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    public class FlexibleLongConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(long) || objectType == typeof(long?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.Integer)
            {
                return Convert.ToInt64(reader.Value);
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var token = JToken.Load(reader);
                if (token is JArray array && array.Count > 0)
                {
                    var str = array[0]?.ToString();
                    return long.TryParse(str, out var v) ? v : (long?)null;
                }

                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                var str = reader.Value?.ToString();
                return long.TryParse(str, out var v) ? v : (long?)null;
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    public class FlexibleIntConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(int) || objectType == typeof(int?);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.Integer)
            {
                return Convert.ToInt32(reader.Value);
            }

            if (reader.TokenType == JsonToken.StartArray)
            {
                var token = JToken.Load(reader);
                if (token is JArray array && array.Count > 0)
                {
                    var str = array[0]?.ToString();
                    return int.TryParse(str, out var v) ? v : (int?)null;
                }

                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                var str = reader.Value?.ToString();
                return int.TryParse(str, out var v) ? v : (int?)null;
            }

            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }

    public class InternetArchiveDoc
    {
        [JsonProperty("identifier")]
        [JsonConverter(typeof(SingleOrArrayStringConverter))]
        public string Identifier { get; set; }

        [JsonProperty("title")]
        [JsonConverter(typeof(SingleOrArrayStringConverter))]
        public string Title { get; set; }

        [JsonProperty("mediatype")]
        [JsonConverter(typeof(SingleOrArrayStringConverter))]
        public string Mediatype { get; set; }

        [JsonProperty("date")]
        [JsonConverter(typeof(FlexibleDateTimeConverter))]
        public DateTime? Date { get; set; }

        [JsonProperty("publicdate")]
        [JsonConverter(typeof(FlexibleDateTimeConverter))]
        public DateTime? PublicDate { get; set; }

        [JsonProperty("item_size")]
        [JsonConverter(typeof(FlexibleLongConverter))]
        public long? ItemSize { get; set; }

        [JsonProperty("downloads")]
        [JsonConverter(typeof(FlexibleIntConverter))]
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

        private static bool MatchesSearchTerm(string fileNameOnly, string fullName, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return true;
            }

            var cleanName = RomParser.CleanIndexPrefix(fileNameOnly);

            if (fileNameOnly.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                fullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                cleanName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedSearch = NormalizeForSearchMatching(searchTerm);
            var normalizedFile = NormalizeForSearchMatching(cleanName);
            var normalizedFull = NormalizeForSearchMatching(fullName);

            if (normalizedFile.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                normalizedFull.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var searchWords = searchTerm.Split(new[] { ' ', '_', '-', '.', ':', '(', ')', '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1 && !string.Equals(w, "the", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (searchWords.Count > 0 && searchWords.All(w => cleanName.Contains(w, StringComparison.OrdinalIgnoreCase) || fullName.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeForSearchMatching(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var chars = input.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
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
                var endIdx = requestUrl.IndexOfAny(new[] { '&', '#' }, platformIdx + 10);
                var rawSlug = endIdx >= 0
                    ? requestUrl.Substring(platformIdx + 10, endIdx - (platformIdx + 10))
                    : requestUrl.Substring(platformIdx + 10);
                platformSlug = Uri.UnescapeDataString(rawSlug);
            }

            var platformNameIdx = requestUrl.IndexOf("_platformName=", StringComparison.OrdinalIgnoreCase);
            if (platformNameIdx >= 0)
            {
                var endIdx = requestUrl.IndexOfAny(new[] { '&', '#' }, platformNameIdx + 14);
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
                    var endIdx = searchSub.IndexOfAny(new[] { '&', '#' });
                    searchTerm = Uri.UnescapeDataString(endIdx >= 0 ? searchSub[..endIdx] : searchSub);
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
                    var isCollectionSearch = requestUrl.IndexOf("_collection=true", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isCollectionSearch)
                    {
                        var isArchiveFile = ext is ".zip" or ".7z" or ".rar" or ".tar" or ".gz";
                        if (!isArchiveFile)
                        {
                            continue;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        if (!MatchesSearchTerm(fileNameOnly, file.Name, searchTerm))
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
                    var parsedRomInfo = RomParser.ParseRomName(file.Name) ?? new ParsedRomInfo { ReleaseTitle = fileNameOnly };
                    if (!string.IsNullOrWhiteSpace(container))
                    {
                        parsedRomInfo.Format = container.ToLowerInvariant();
                    }

                    if (isCollectionSearch)
                    {
                        parsedRomInfo.IsFullSetArchive = true;
                    }

                    var escapedPath = string.Join("/", file.Name.TrimStart('/').Split('/').Select(Uri.EscapeDataString));

                    var releaseInfo = new ReleaseInfo
                    {
                        Guid = $"IA-{archiveId}-{file.Name}",
                        Title = fileNameOnly,
                        Size = size,
                        DownloadUrl = $"{baseUrl}/download/{archiveId}/{escapedPath}",
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

            var primaryArchiveIds = _settings?.PrimaryArchiveUrls?
                .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            foreach (var doc in searchResponse.Response.Docs)
            {
                if (doc.Identifier.IsNullOrWhiteSpace() || primaryArchiveIds.Contains(doc.Identifier))
                {
                    continue;
                }

                var isCollectionSearch = requestUrl.IndexOf("_collection=true", StringComparison.OrdinalIgnoreCase) >= 0;

                // If this is an entire multi-game collection (e.g. 472GB archive pack), reject it during single-game search
                if (!isCollectionSearch && doc.ItemSize.HasValue && doc.ItemSize.Value > 25_000_000_000L)
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
                var docRomInfo = RomParser.ParseRomName(title) ?? new ParsedRomInfo { ReleaseTitle = title };
                if (!string.IsNullOrWhiteSpace(docContainer))
                {
                    docRomInfo.Format = docContainer.ToLowerInvariant();
                }

                if (isCollectionSearch)
                {
                    docRomInfo.IsFullSetArchive = true;
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
