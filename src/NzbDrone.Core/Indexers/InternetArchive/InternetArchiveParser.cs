using System;
using System.Collections.Concurrent;
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

    public class JoinedStringConverter : JsonConverter
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
                    var items = array
                        .Select(t => t?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Trim().StartsWith("fav-", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    return items.Count > 0 ? string.Join(", ", items) : null;
                }

                return null;
            }

            var single = reader.Value?.ToString();
            return !string.IsNullOrWhiteSpace(single) && single.Trim().StartsWith("fav-", StringComparison.OrdinalIgnoreCase)
                ? null
                : single;
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

        [JsonProperty("collection")]
        [JsonConverter(typeof(JoinedStringConverter))]
        public string Collection { get; set; }

        [JsonProperty("subject")]
        [JsonConverter(typeof(JoinedStringConverter))]
        public string Subject { get; set; }

        [JsonProperty("description")]
        [JsonConverter(typeof(JoinedStringConverter))]
        public string Description { get; set; }

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
            ".xml", ".sqlite", ".torrent", ".json", ".txt", ".png", ".jpg", ".jpeg",
            ".pdf", ".gif", ".bmp", ".webp", ".nfo", ".sfv", ".md5", ".sha1", ".dat",
            ".html", ".htm", ".db", ".ini", ".log", ".mp3", ".flac", ".ogg", ".wav"
        };

        private static readonly HashSet<string> IgnoredFileFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Metadata", "Item Tile", "Archive BitTorrent", "Text PDF", "Thumbnail",
            "Animated GIF", "JPEG", "PNG", "MARC", "Dublin Core", "JSON", "Log", "Checksums"
        };

        private static readonly Regex NonRomContentRegex = new Regex(
            @"(?:^(?:github\.com-|thingiverse-|wiki-))|\b(?:manual|manuals|magazine|magazines|strategy\s+guide|walkthrough|soundtrack|ost|original\s+sound\s+track|longplay|playthrough|box\s*art|scans|cover\s+art|flyers|promo\s+video|commercials|podcast|retroarch\s+cores?|screensaver|save\s*files?|roblox|bloxcards?wiki|wikidump|wikitext|wikipedia|fandom|wikia)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ValidDocExtensionRegex = new Regex(
            @"^\.(?=.*[A-Za-z])[A-Za-z0-9]{2,5}$",
            RegexOptions.Compiled);

        private static readonly Regex PcComputerContextRegex = new Regex(
            @"\b(?:ms-?dos|dosbox|win3\.?1|win95|win98|windows\s+3\.1|windows\s+95|windows\s+98|apple\s*ii|commodore\s*64|zx\s*spectrum|amstrad\s+cpc|pc-98)\b|softwarelibrary_msdos|softwarelibrary_win|softwarelibrary_mac|softwarelibrary_c64|softwarelibrary_amiga",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CollectionPackIndicatorRegex = new Regex(
            @"(?:\b(?:htgdb|smdb|gamepacks?|game\s*packs?|flashboy|everdrive|ever\s*drive|sd2snes|fxpak|powerpak|retrohq|darksoft|neosd|harmony|atarimax|lto\s*flash|vecmulti|flash\s*masta|bennvenn|mcwill|elcheaposd|full\s*set|rom\s*set|romset|1g1r|no-?intro|redump|tosec|collection|complete|all\s+roms|megapack|mega\s+pack|champion\s+collection)\b)|^@",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ArchivePackIdRegex = new Regex(
            @"\b(?:htgdb|smdb|gamepacks?|everdrive)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly ConcurrentDictionary<string, Regex> AliasWordBoundaryRegexCache =
            new ConcurrentDictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);

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

            var cleanFile = (fileNameOnly ?? string.Empty).RemoveDiacritics();
            var cleanFull = (fullName ?? string.Empty).RemoveDiacritics();
            var cleanSearch = searchTerm.RemoveDiacritics();
            var cleanName = RomParser.CleanIndexPrefix(cleanFile);

            if (cleanFile.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                cleanFull.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase) ||
                cleanName.Contains(cleanSearch, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedSearch = NormalizeForSearchMatching(cleanSearch);
            var normalizedFile = NormalizeForSearchMatching(cleanName);
            var normalizedFull = NormalizeForSearchMatching(cleanFull);

            if (!string.IsNullOrWhiteSpace(normalizedSearch) &&
                (normalizedFile.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                 normalizedFull.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var searchWords = cleanSearch.Split(new[] { ' ', '_', '-', '.', ':', '(', ')', '[', ']', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 1 &&
                            !string.Equals(w, "the", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(w, "version", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (searchWords.Count > 0 &&
                searchWords.All(w => cleanName.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                                     cleanFull.Contains(w, StringComparison.OrdinalIgnoreCase)))
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

            var requestUrl = indexerResponse.Request?.Url?.FullUri ?? indexerResponse.HttpRequest?.Url?.FullUri ?? string.Empty;
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

            string fragmentSearchTerm = null;
            var searchTermIdx = requestUrl.IndexOf("_searchTerm=", StringComparison.OrdinalIgnoreCase);
            if (searchTermIdx >= 0)
            {
                var endIdx = requestUrl.IndexOfAny(new[] { '&', '#' }, searchTermIdx + 12);
                var rawSearchTerm = endIdx >= 0
                    ? requestUrl.Substring(searchTermIdx + 12, endIdx - (searchTermIdx + 12))
                    : requestUrl.Substring(searchTermIdx + 12);
                fragmentSearchTerm = Uri.UnescapeDataString(rawSearchTerm);
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

                    if (!string.IsNullOrWhiteSpace(file.Format) && IgnoredFileFormats.Contains(file.Format.Trim()))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(file.Name);
                    if (IgnoredArchiveExtensions.Contains(ext) ||
                        PlatformCompatibilityValidator.IsTvOrVideoRelease(file.Name, ext) ||
                        PlatformCompatibilityValidator.IsPcOrNonConsoleRelease(file.Name) ||
                        IsNonRomContent(file.Name))
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
                    var isPackFile = IsCollectionPackFile(fileNameOnly, file.Name, archiveId, platformSlug, platformName);

                    if (isCollectionSearch)
                    {
                        var isArchiveFile = ext is ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz";
                        if (!isArchiveFile)
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(platformSlug) &&
                            !MatchesPlatformInMultiPlatformArchive(file.Name, file.Format, archiveId, platformSlug, platformName))
                        {
                            continue;
                        }

                        if (!isPackFile)
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

                    if (isCollectionSearch || CollectionPackIndicatorRegex.IsMatch(fileNameOnly))
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

            var isCollectionSearchQuery = requestUrl.IndexOf("_collection=true", StringComparison.OrdinalIgnoreCase) >= 0;

            foreach (var doc in searchResponse.Response.Docs)
            {
                if (doc.Identifier.IsNullOrWhiteSpace())
                {
                    continue;
                }

                // Skip primary archives in advancedsearch unless this is a collection search and the primary archive itself is a single-platform archive for the target platform
                if (primaryArchiveIds.Contains(doc.Identifier))
                {
                    if (!isCollectionSearchQuery ||
                        string.IsNullOrWhiteSpace(platformSlug) ||
                        !IsSinglePlatformArchiveMatch(doc.Identifier, doc.Title, platformSlug, platformName))
                    {
                        continue;
                    }
                }

                // Reject non-ROM mediatypes (books/manuals, audio/OSTs, video, images)
                if (!string.IsNullOrWhiteSpace(doc.Mediatype) &&
                    doc.Mediatype.Trim() is "texts" or "audio" or "movies" or "image" or "etree" or "web" or "account")
                {
                    continue;
                }

                // If this is an entire multi-game collection (e.g. >25GB archive pack), reject it during single-game search
                if (!isCollectionSearchQuery && doc.ItemSize.HasValue && doc.ItemSize.Value > 25_000_000_000L)
                {
                    continue;
                }

                var title = doc.Title.IsNotNullOrWhiteSpace() ? doc.Title : doc.Identifier;
                var archiveContext = BuildArchiveContext(doc);

                if (PlatformCompatibilityValidator.IsTvOrVideoRelease(title) ||
                    IsNonRomContent(title) ||
                    IsNonRomContent(doc.Identifier) ||
                    PcComputerContextRegex.IsMatch(archiveContext) ||
                    PlatformCompatibilityValidator.IsPcOrNonConsoleRelease(archiveContext))
                {
                    continue;
                }

                if (!isCollectionSearchQuery &&
                    !string.IsNullOrWhiteSpace(fragmentSearchTerm) &&
                    !MatchesSearchTerm(title, doc.Identifier, fragmentSearchTerm))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(platformSlug))
                {
                    if (!PlatformCompatibilityValidator.IsCompatible(title, platformSlug, platformName, doc.Mediatype, doc.Identifier))
                    {
                        continue;
                    }

                    // Scrutinize collection and subject tags for conflicting console platforms
                    if (!IsArchiveMetadataCompatible(doc, platformSlug, platformName))
                    {
                        continue;
                    }

                    // For collection searches, require positive platform identification in archive ID, title, collection, subject, or description
                    if (isCollectionSearchQuery &&
                        !MatchesPlatformInMultiPlatformArchive(title, $"{doc.Collection} {doc.Subject} {doc.Description}", doc.Identifier, platformSlug, platformName))
                    {
                        continue;
                    }
                }

                var publishDate = doc.PublicDate ?? doc.Date ?? DateTime.UtcNow;
                var requiresCredentials = doc.IsAccessRestricted() || (_settings != null && _settings.RequireCredentials);
                var docExt = Path.GetExtension(title);
                var isValidDocExt = !string.IsNullOrWhiteSpace(docExt) && ValidDocExtensionRegex.IsMatch(docExt);
                var docContainer = isValidDocExt ? docExt.TrimStart('.').ToUpperInvariant() : "ZIP";
                var docRomInfo = RomParser.ParseRomName(title) ?? new ParsedRomInfo { ReleaseTitle = title };
                if (isValidDocExt)
                {
                    docRomInfo.Format = docContainer.ToLowerInvariant();
                }
                else if (string.IsNullOrWhiteSpace(docRomInfo.Format))
                {
                    docRomInfo.Format = "zip";
                }

                if (isCollectionSearchQuery || CollectionPackIndicatorRegex.IsMatch(title) || CollectionPackIndicatorRegex.IsMatch(doc.Identifier))
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
                    Origin = BuildArchiveOrigin(doc),
                    Container = docContainer,
                    ParsedRomInfo = docRomInfo
                };

                results.Add(releaseInfo);
            }

            return results;
        }

        private static bool IsNonRomContent(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return NonRomContentRegex.IsMatch(text);
        }

        private static string BuildArchiveContext(InternetArchiveDoc doc)
        {
            return string.Join(
                " ",
                new[] { doc.Identifier, doc.Title, doc.Collection, doc.Subject }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static string BuildArchiveOrigin(InternetArchiveDoc doc)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(doc.Identifier))
            {
                parts.Add(doc.Identifier);
            }

            if (!string.IsNullOrWhiteSpace(doc.Collection))
            {
                parts.Add(doc.Collection.Replace('/', ' ').Replace('\\', ' '));
            }

            if (!string.IsNullOrWhiteSpace(doc.Subject))
            {
                parts.Add(doc.Subject.Replace('/', ' ').Replace('\\', ' '));
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : null;
        }

        private static bool IsArchiveMetadataCompatible(InternetArchiveDoc doc, string platformSlug, string platformName)
        {
            var targetSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(platformSlug, platformName);
            if (string.IsNullOrWhiteSpace(targetSlug))
            {
                return true;
            }

            foreach (var metadataField in new[] { doc.Collection, doc.Subject })
            {
                if (string.IsNullOrWhiteSpace(metadataField))
                {
                    continue;
                }

                var detected = PlatformCompatibilityValidator.DetectPlatforms(metadataField);
                if (detected.Count > 0 && !detected.Any(p => PlatformCompatibilityValidator.IsPlatformMatch(p, targetSlug)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSinglePlatformArchiveMatch(string archiveId, string title, string platformSlug, string platformName)
        {
            var targetSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(platformSlug, platformName);
            var detected = PlatformCompatibilityValidator.DetectPlatforms($"{archiveId} {title}");
            return detected.Count == 1 && detected.All(p => PlatformCompatibilityValidator.IsPlatformMatch(p, targetSlug));
        }

        private static bool IsCollectionPackFile(string fileNameOnly, string fullPath, string archiveId, string platformSlug, string platformName)
        {
            if (CollectionPackIndicatorRegex.IsMatch(fileNameOnly) ||
                CollectionPackIndicatorRegex.IsMatch(fullPath) ||
                (!string.IsNullOrWhiteSpace(archiveId) &&
                 ArchivePackIdRegex.IsMatch(archiveId)))
            {
                return true;
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileNameOnly);
            if (!string.IsNullOrWhiteSpace(platformSlug) &&
                !string.IsNullOrWhiteSpace(nameWithoutExt) &&
                MatchesPlatformInMultiPlatformArchive(nameWithoutExt, null, null, platformSlug, platformName))
            {
                return true;
            }

            return false;
        }

        private static bool MatchesPlatformInMultiPlatformArchive(string fileName, string fileFormat, string archiveId, string platformSlug, string platformName)
        {
            var targetSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(platformSlug, platformName);

            if (!string.IsNullOrWhiteSpace(archiveId) &&
                PlatformCompatibilityValidator.DetectPlatforms(archiveId).Any(p => PlatformCompatibilityValidator.IsPlatformMatch(p, targetSlug)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileName) &&
                PlatformCompatibilityValidator.DetectPlatforms(fileName).Any(p => PlatformCompatibilityValidator.IsPlatformMatch(p, targetSlug)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(fileFormat) &&
                PlatformCompatibilityValidator.DetectPlatforms(fileFormat).Any(p => PlatformCompatibilityValidator.IsPlatformMatch(p, targetSlug)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(platformName) &&
                !string.IsNullOrWhiteSpace(fileName) &&
                fileName.Contains(platformName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var aliases = PlatformCompatibilityValidator.GetAliasesForPlatform(targetSlug);
            if (aliases.Length > 0)
            {
                var combined = $"{archiveId} {fileName} {fileFormat}";
                foreach (var alias in aliases)
                {
                    if (alias.Length >= 2)
                    {
                        var aliasRegex = AliasWordBoundaryRegexCache.GetOrAdd(
                            alias,
                            static a => new Regex($@"(?<![A-Za-z0-9]){Regex.Escape(a)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled));
                        if (aliasRegex.IsMatch(combined))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
