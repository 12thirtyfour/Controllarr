using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Dat;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Game;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Patching
{
    public class RomHackSearchResult
    {
        public string Title { get; set; }
        public string PlatformSlug { get; set; }
        public string PlatformHint { get; set; }
        public string BaseGameTitle { get; set; }
        public string BaseRomCrc32 { get; set; }
        public string BaseRomMd5 { get; set; }
        public string BaseRomSha1 { get; set; }
        public string SourceName { get; set; }
        public string DownloadUrl { get; set; }
        public string PageUrl { get; set; }
        public string PatchFormat { get; set; }
        public bool HasDirectDownload { get; set; }
        public long? FileSize { get; set; }
        public int RelevanceScore { get; set; }
        public bool IsHomebrew { get; set; }
    }

    public class RomHackAcquireRequest
    {
        public int SeriesId { get; set; }
        public int? TargetEpisodeId { get; set; }
        public int? BaseEpisodeFileId { get; set; }
        public string BaseRomPath { get; set; }
        public string Title { get; set; }
        public string DownloadUrl { get; set; }
        public string PageUrl { get; set; }
        public string BaseGameTitle { get; set; }
        public string BaseRomCrc32 { get; set; }
        public string BaseRomMd5 { get; set; }
        public string BaseRomSha1 { get; set; }
        public bool IsHomebrew { get; set; }
        public bool SearchMissingBaseRom { get; set; } = true;
    }

    public class RomHackAcquireResult
    {
        public bool Success { get; set; }
        public bool BaseRomMissing { get; set; }
        public bool BaseRomSearchQueued { get; set; }
        public string OutputRomPath { get; set; }
        public string PatchFormat { get; set; }
        public string PatchSource { get; set; }
        public string PatchDownloadUrl { get; set; }
        public string BaseRomName { get; set; }
        public string BaseRomCrc32 { get; set; }
        public string OutputCrc32 { get; set; }
        public string OutputSha1 { get; set; }
        public int? EpisodeId { get; set; }
        public int? EpisodeFileId { get; set; }
        public string Message { get; set; }
    }

    public interface IRomHackAutomationService
    {
        List<RomHackSearchResult> SearchCommunitySources(string query, string platformSlug = null, bool includeHomebrew = true);
        RomHackAcquireResult AcquireAndCreate(RomHackAcquireRequest request);
        RomHackAcquireResult TryAcquirePatchForGame(Series series, Episode episode, bool searchMissingBaseRom = true);
    }

    public class RomHackAutomationService : IRomHackAutomationService
    {
        private static readonly HashSet<string> PatchAndArchiveExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bps",
            ".ips",
            ".xdelta",
            ".vcdiff",
            ".zip",
            ".7z",
            ".rar"
        };

        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "of", "and", "or", "for", "a", "an", "in", "to", "version", "edition", "hack", "patch", "rom", "translation", "english", "usa", "europe", "japan", "world"
        };

        private static readonly Regex AnchorTagRegex = new Regex(
            @"<a[^>]+href=""(?<url>[^""]+)""[^>]*>(?<text>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex HtmlTagRegex = new Regex(
            @"<[^>]+>",
            RegexOptions.Compiled);

        private static readonly Regex BaseRomLabelRegex = new Regex(
            @"(?:Base\s*ROM|Base\s*Game|Original\s*Game|Target\s*ROM|Requires\s*ROM|ROM\s*/\s*ISO)\s*[:\-]\s*(?<val>[^\r\n|<]{2,120})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PlatformLabelRegex = new Regex(
            @"(?:Platform|System|Console)\s*[:\-]\s*(?<val>[^\r\n|<]{2,60})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Crc32Regex = new Regex(
            @"(?:CRC\s*-?32|Source\s*CRC\s*-?32)\s*[:#\-]?\s*(?<hex>[0-9a-fA-F]{8})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Md5Regex = new Regex(
            @"MD5\s*[:#\-]?\s*(?<hex>[0-9a-fA-F]{32})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Sha1Regex = new Regex(
            @"SHA\s*-?1\s*[:#\-]?\s*(?<hex>[0-9a-fA-F]{40})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CleanQueryTagRegex = new Regex(
            @"(\([^)]*\)|\[[^\]]*\])",
            RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly IRomPatchingService _romPatchingService;
        private readonly IDownloadedGamesImportService _downloadedGamesImportService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IDatCacheService _datCacheService;
        private readonly Logger _logger;

        public RomHackAutomationService(
            IHttpClient httpClient,
            IRomPatchingService romPatchingService,
            ISeriesService seriesService,
            IEpisodeService episodeService,
            Logger logger,
            IDownloadedGamesImportService downloadedGamesImportService = null,
            IManageCommandQueue commandQueueManager = null,
            IDatCacheService datCacheService = null)
        {
            _httpClient = httpClient;
            _romPatchingService = romPatchingService;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _downloadedGamesImportService = downloadedGamesImportService;
            _commandQueueManager = commandQueueManager;
            _datCacheService = datCacheService;
            _logger = logger;
        }

        public List<RomHackSearchResult> SearchCommunitySources(string query, string platformSlug = null, bool includeHomebrew = true)
        {
            var results = new List<RomHackSearchResult>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            results.AddRange(QueryRomhackPlaza(query, platformSlug));
            results.AddRange(QueryInternetArchivePatches(query, platformSlug));
            results.AddRange(QueryGitHubPatches(query, platformSlug));

            if (includeHomebrew)
            {
                results.AddRange(QueryGitHubHomebrew(query, platformSlug));
            }

            var normalizedTargetPlatform = PlatformCompatibilityValidator.NormalizePlatformSlug(platformSlug, platformSlug);

            return results
                .Where(r => r != null && IsPlatformCompatible(normalizedTargetPlatform, r.PlatformHint))
                .GroupBy(r => r.DownloadUrl ?? r.PageUrl ?? r.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(r => r.HasDirectDownload)
                .ThenByDescending(r => r.RelevanceScore)
                .ThenByDescending(r => GetFormatPriorityScore(r.PatchFormat))
                .ToList();
        }

        public RomHackAcquireResult TryAcquirePatchForGame(Series series, Episode episode, bool searchMissingBaseRom = true)
        {
            if (series == null || episode == null)
            {
                return new RomHackAcquireResult
                {
                    Success = false,
                    Message = "Platform and game are required for patch-first ROM hack acquisition."
                };
            }

            if (episode.SeasonNumber != GameCatalogProxy.RegionRomHacks)
            {
                return new RomHackAcquireResult
                {
                    Success = false,
                    Message = "Patch-first acquisition is only applicable to ROM Hacks region entries."
                };
            }

            var cleanQuery = BuildPatchSearchQuery(episode.Title);
            if (string.IsNullOrWhiteSpace(cleanQuery))
            {
                return new RomHackAcquireResult
                {
                    Success = false,
                    Message = $"Unable to build a clean search query from ROM hack title '{episode.Title}'."
                };
            }

            _logger?.Debug("Searching community patch sources first for ROM hack '{0}' ({1})", cleanQuery, series.Title);

            var candidates = SearchCommunitySources(cleanQuery, series.TitleSlug, includeHomebrew: false)
                .Where(c => !c.IsHomebrew)
                .Where(c => c.HasDirectDownload || IsResolvableDetailPageUrl(c.PageUrl ?? c.DownloadUrl))
                .Take(3)
                .ToList();

            if (!candidates.Any())
            {
                return new RomHackAcquireResult
                {
                    Success = false,
                    Message = $"No downloadable .bps/.ips patch candidates found for '{cleanQuery}'."
                };
            }

            RomHackAcquireResult missingBaseResult = null;
            RomHackAcquireResult lastFailure = null;

            foreach (var candidate in candidates)
            {
                var attempt = AcquireAndCreate(new RomHackAcquireRequest
                {
                    SeriesId = series.Id,
                    TargetEpisodeId = episode.Id,
                    Title = episode.Title,
                    DownloadUrl = candidate.DownloadUrl,
                    PageUrl = candidate.PageUrl,
                    BaseGameTitle = candidate.BaseGameTitle,
                    BaseRomCrc32 = candidate.BaseRomCrc32,
                    BaseRomMd5 = candidate.BaseRomMd5,
                    BaseRomSha1 = candidate.BaseRomSha1,
                    IsHomebrew = false,
                    SearchMissingBaseRom = false
                });

                if (attempt.Success)
                {
                    attempt.PatchSource ??= candidate.SourceName;
                    return attempt;
                }

                lastFailure = attempt;
                if (attempt.BaseRomMissing && missingBaseResult == null)
                {
                    missingBaseResult = attempt;
                }
            }

            if (missingBaseResult != null)
            {
                if (searchMissingBaseRom)
                {
                    var queued = TryQueueMissingBaseRomSearch(
                        series,
                        missingBaseResult.BaseRomCrc32,
                        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.BaseGameTitle))?.BaseGameTitle,
                        cleanQuery);
                    missingBaseResult.BaseRomSearchQueued = queued;
                }

                return missingBaseResult;
            }

            return lastFailure ?? new RomHackAcquireResult
            {
                Success = false,
                Message = $"Unable to acquire and apply a patch for '{episode.Title}'."
            };
        }

        public RomHackAcquireResult AcquireAndCreate(RomHackAcquireRequest request)
        {
            if (request == null || request.SeriesId <= 0 ||
                (string.IsNullOrWhiteSpace(request.DownloadUrl) && string.IsNullOrWhiteSpace(request.PageUrl)))
            {
                return new RomHackAcquireResult
                {
                    Success = false,
                    Message = "SeriesId and DownloadUrl are required."
                };
            }

            var series = _seriesService.GetSeries(request.SeriesId);
            if (series == null)
            {
                return new RomHackAcquireResult
                {
                    Success = false,
                    Message = $"Platform {request.SeriesId} not found."
                };
            }

            var effectiveDownloadUrl = request.DownloadUrl;
            var effectiveBaseGameTitle = request.BaseGameTitle;
            var effectiveBaseCrc32 = request.BaseRomCrc32;
            var effectiveBaseMd5 = request.BaseRomMd5;
            var effectiveBaseSha1 = request.BaseRomSha1;

            if ((string.IsNullOrWhiteSpace(effectiveDownloadUrl) || IsHtmlDetailPageUrl(effectiveDownloadUrl)) &&
                !request.IsHomebrew)
            {
                var detailUrl = !string.IsNullOrWhiteSpace(request.PageUrl) ? request.PageUrl : effectiveDownloadUrl;
                var resolved = TryResolveRomhackPlazaDetailPage(detailUrl, request.Title, series.TitleSlug);
                if (resolved != null && resolved.HasDirectDownload && !string.IsNullOrWhiteSpace(resolved.DownloadUrl))
                {
                    effectiveDownloadUrl = resolved.DownloadUrl;
                    if (string.IsNullOrWhiteSpace(effectiveBaseGameTitle))
                    {
                        effectiveBaseGameTitle = resolved.BaseGameTitle;
                    }

                    if (string.IsNullOrWhiteSpace(effectiveBaseCrc32))
                    {
                        effectiveBaseCrc32 = resolved.BaseRomCrc32;
                    }

                    if (string.IsNullOrWhiteSpace(effectiveBaseMd5))
                    {
                        effectiveBaseMd5 = resolved.BaseRomMd5;
                    }

                    if (string.IsNullOrWhiteSpace(effectiveBaseSha1))
                    {
                        effectiveBaseSha1 = resolved.BaseRomSha1;
                    }
                }
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_romhack_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var uri = new Uri(effectiveDownloadUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = SanitizeFileName(request.Title ?? "patch") + (request.IsHomebrew ? ".zip" : ".bps");
                }

                var localArtifactPath = Path.Combine(tempDir, fileName);
                _httpClient.DownloadFile(effectiveDownloadUrl, localArtifactPath);

                if (File.Exists(localArtifactPath) && LooksLikeHtmlDocument(localArtifactPath))
                {
                    return new RomHackAcquireResult
                    {
                        Success = false,
                        BaseRomMissing = false,
                        PatchDownloadUrl = effectiveDownloadUrl,
                        Message = "Download URL returned an HTML webpage instead of a binary patch or archive file."
                    };
                }

                if (request.IsHomebrew && _downloadedGamesImportService != null)
                {
                    var importResults = _downloadedGamesImportService.ProcessPath(localArtifactPath, GameImport.ImportMode.Move, series);
                    var imported = importResults?.FirstOrDefault(r => r.Result == GameImport.ImportResultType.Imported);
                    if (imported != null)
                    {
                        return new RomHackAcquireResult
                        {
                            Success = true,
                            PatchFormat = "Homebrew",
                            PatchDownloadUrl = effectiveDownloadUrl,
                            OutputRomPath = imported.EpisodeFile?.RelativePath,
                            EpisodeId = imported.EpisodeFile?.Episodes.Value?.FirstOrDefault()?.Id,
                            EpisodeFileId = imported.EpisodeFile?.Id,
                            Message = $"Imported homebrew '{request.Title}' into {series.Title}."
                        };
                    }
                }

                var patchRequest = new RomPatchRequestModel
                {
                    SeriesId = series.Id,
                    TargetEpisodeId = request.TargetEpisodeId,
                    BaseEpisodeFileId = request.BaseEpisodeFileId,
                    BaseRomPath = request.BaseRomPath,
                    PatchFilePath = localArtifactPath,
                    PatchFileName = fileName,
                    OutputTitle = request.Title,
                    BaseGameTitleHint = effectiveBaseGameTitle,
                    ExpectedBaseRomCrc32 = effectiveBaseCrc32,
                    ExpectedBaseRomMd5 = effectiveBaseMd5,
                    ExpectedBaseRomSha1 = effectiveBaseSha1
                };

                var patchResult = _romPatchingService.ApplyPatchToLibrary(patchRequest)
                    ?? _romPatchingService.TryApplyPatchFileForSeries(localArtifactPath, series);

                if (patchResult != null && patchResult.Success)
                {
                    return new RomHackAcquireResult
                    {
                        Success = true,
                        PatchFormat = patchResult.Format,
                        PatchDownloadUrl = effectiveDownloadUrl,
                        BaseRomName = patchResult.BaseRomName,
                        BaseRomCrc32 = patchResult.BaseRomCrc32,
                        OutputRomPath = patchResult.OutputRomPath,
                        OutputCrc32 = patchResult.OutputCrc32,
                        OutputSha1 = patchResult.OutputSha1,
                        EpisodeId = patchResult.EpisodeId,
                        EpisodeFileId = patchResult.EpisodeFileId,
                        Message = $"Downloaded {patchResult.Format} patch, applied onto '{patchResult.BaseRomName}', and imported '{request.Title}' into {series.Title}."
                    };
                }

                var isMissingBase = DetermineIfBaseRomMissing(patchResult);
                var queuedSearch = false;
                var expectedCrc = patchResult?.ExpectedBaseRomCrc32 ?? effectiveBaseCrc32;

                if (isMissingBase && request.SearchMissingBaseRom)
                {
                    queuedSearch = TryQueueMissingBaseRomSearch(
                        series,
                        expectedCrc,
                        effectiveBaseGameTitle,
                        request.Title);
                }

                return new RomHackAcquireResult
                {
                    Success = false,
                    BaseRomMissing = isMissingBase,
                    BaseRomSearchQueued = queuedSearch,
                    PatchFormat = patchResult?.Format,
                    PatchDownloadUrl = effectiveDownloadUrl,
                    BaseRomCrc32 = expectedCrc,
                    Message = patchResult?.Message ?? $"Matching base retail ROM for '{effectiveBaseGameTitle ?? request.Title}' was not found in your {series.Title} library."
                };
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to acquire/apply ROM hack or homebrew '{0}' from {1}", request.Title, effectiveDownloadUrl);
                return new RomHackAcquireResult
                {
                    Success = false,
                    PatchDownloadUrl = effectiveDownloadUrl,
                    Message = ex.Message
                };
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Unable to clean up temporary ROM hack staging directory {0}", tempDir);
                }
            }
        }

        private List<RomHackSearchResult> QueryRomhackPlaza(string query, string platformSlug)
        {
            var results = new List<RomHackSearchResult>();
            try
            {
                var url = $"https://romhackplaza.org/?s={Uri.EscapeDataString(query)}";
                var response = _httpClient.Get(new HttpRequest(url));
                if (response == null || response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                var candidates = new List<(string Url, string Title, int Score)>();
                foreach (Match match in AnchorTagRegex.Matches(response.Content))
                {
                    var href = WebUtility.HtmlDecode(match.Groups["url"].Value).Trim();
                    var title = CleanHtmlText(match.Groups["text"].Value);
                    if (string.IsNullOrWhiteSpace(title) || !IsValidRomhackPlazaCandidateUrl(href))
                    {
                        continue;
                    }

                    candidates.Add((href, title, ComputeRelevanceScore(query, title)));
                }

                var topCandidates = candidates
                    .GroupBy(c => c.Url, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderByDescending(c => c.Score)
                    .Take(5)
                    .ToList();

                foreach (var candidate in topCandidates)
                {
                    var resolved = TryResolveRomhackPlazaDetailPage(candidate.Url, candidate.Title, platformSlug);
                    if (resolved != null)
                    {
                        resolved.RelevanceScore = Math.Max(resolved.RelevanceScore, ComputeRelevanceScore(query, resolved.Title));
                        results.Add(resolved);
                    }
                    else
                    {
                        results.Add(new RomHackSearchResult
                        {
                            Title = candidate.Title,
                            PlatformSlug = platformSlug,
                            BaseGameTitle = query,
                            SourceName = "Romhack Plaza",
                            DownloadUrl = candidate.Url,
                            PageUrl = candidate.Url,
                            PatchFormat = "Patch",
                            HasDirectDownload = false,
                            RelevanceScore = candidate.Score
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Romhack Plaza query failed for '{0}'", query);
            }

            return results;
        }

        private RomHackSearchResult TryResolveRomhackPlazaDetailPage(string detailUrl, string fallbackTitle, string platformSlug)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(detailUrl) ||
                    !detailUrl.Contains("romhackplaza.org", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var detailResponse = _httpClient.Get(new HttpRequest(detailUrl));
                if (detailResponse == null || detailResponse.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(detailResponse.Content))
                {
                    return null;
                }

                var html = detailResponse.Content;
                var plainText = CleanHtmlMultiline(html);

                var baseRomMatch = BaseRomLabelRegex.Match(plainText);
                var baseGameTitle = baseRomMatch.Success ? baseRomMatch.Groups["val"].Value.Trim() : null;
                var crcMatch = Crc32Regex.Match(plainText);
                var md5Match = Md5Regex.Match(plainText);
                var sha1Match = Sha1Regex.Match(plainText);
                var platformMatch = PlatformLabelRegex.Match(plainText);

                var downloadLinks = new List<(string Url, string Format, int Priority)>();
                foreach (Match link in AnchorTagRegex.Matches(html))
                {
                    var href = WebUtility.HtmlDecode(link.Groups["url"].Value).Trim();
                    if (string.IsNullOrWhiteSpace(href) || href.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resolvedUrl = ResolveAbsoluteUrl(detailUrl, href);
                    if (string.IsNullOrWhiteSpace(resolvedUrl))
                    {
                        continue;
                    }

                    var linkText = CleanHtmlText(link.Groups["text"].Value);
                    var format = DetectPatchFormatFromUrlOrText(resolvedUrl, linkText);
                    var isDownloadEndpoint = IsDirectPatchOrArchiveUrl(resolvedUrl) ||
                                             resolvedUrl.Contains("/download", StringComparison.OrdinalIgnoreCase) ||
                                             (linkText.Contains("download", StringComparison.OrdinalIgnoreCase) &&
                                              !resolvedUrl.Equals(detailUrl, StringComparison.OrdinalIgnoreCase));

                    if (!isDownloadEndpoint)
                    {
                        continue;
                    }

                    downloadLinks.Add((resolvedUrl, format, GetFormatPriorityScore(format)));
                }

                var bestLink = downloadLinks
                    .OrderByDescending(l => l.Priority)
                    .FirstOrDefault();

                var hasDirect = !string.IsNullOrWhiteSpace(bestLink.Url);

                return new RomHackSearchResult
                {
                    Title = fallbackTitle ?? detailUrl,
                    PlatformSlug = platformSlug,
                    PlatformHint = platformMatch.Success ? platformMatch.Groups["val"].Value.Trim() : null,
                    BaseGameTitle = baseGameTitle,
                    BaseRomCrc32 = crcMatch.Success ? crcMatch.Groups["hex"].Value.ToLowerInvariant() : null,
                    BaseRomMd5 = md5Match.Success ? md5Match.Groups["hex"].Value.ToLowerInvariant() : null,
                    BaseRomSha1 = sha1Match.Success ? sha1Match.Groups["hex"].Value.ToLowerInvariant() : null,
                    SourceName = "Romhack Plaza",
                    DownloadUrl = hasDirect ? bestLink.Url : detailUrl,
                    PageUrl = detailUrl,
                    PatchFormat = hasDirect ? bestLink.Format : "Patch",
                    HasDirectDownload = hasDirect,
                    RelevanceScore = ComputeRelevanceScore(fallbackTitle ?? string.Empty, fallbackTitle ?? string.Empty)
                };
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to parse Romhack Plaza detail page '{0}'", detailUrl);
                return null;
            }
        }

        private List<RomHackSearchResult> QueryInternetArchivePatches(string query, string platformSlug)
        {
            var results = new List<RomHackSearchResult>();
            try
            {
                var lucene = $"({query}) AND (romhack OR translation OR \"rom hack\" OR ips OR bps OR homebrew)";
                var url = $"https://archive.org/advancedsearch.php?q={Uri.EscapeDataString(lucene)}&fl[]=identifier,title&rows=15&page=1&output=json";
                var response = _httpClient.Get(new HttpRequest(url, HttpAccept.Json));
                if (response == null || response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                using var doc = JsonDocument.Parse(response.Content);
                if (!doc.RootElement.TryGetProperty("response", out var resp) ||
                    !resp.TryGetProperty("docs", out var docs))
                {
                    return results;
                }

                var items = new List<(string Id, string Title, int Score)>();
                foreach (var item in docs.EnumerateArray())
                {
                    var id = item.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                    var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : id;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    items.Add((id, title ?? id, ComputeRelevanceScore(query, title ?? id)));
                }

                var orderedItems = items.OrderByDescending(i => i.Score).ToList();
                for (var idx = 0; idx < orderedItems.Count; idx++)
                {
                    var (id, title, itemScore) = orderedItems[idx];
                    var addedDirect = false;

                    if (idx < 5)
                    {
                        addedDirect = TryAppendArchivePatchFiles(id, title, query, platformSlug, results);
                    }

                    if (!addedDirect)
                    {
                        results.Add(new RomHackSearchResult
                        {
                            Title = title,
                            PlatformSlug = platformSlug,
                            BaseGameTitle = query,
                            SourceName = "Internet Archive (ROMhacking/Community)",
                            DownloadUrl = $"https://archive.org/download/{id}",
                            PageUrl = $"https://archive.org/details/{id}",
                            PatchFormat = "Archive",
                            HasDirectDownload = false,
                            RelevanceScore = itemScore,
                            IsHomebrew = title.Contains("homebrew", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Internet Archive patch query failed for '{0}'", query);
            }

            return results;
        }

        private bool TryAppendArchivePatchFiles(
            string identifier,
            string itemTitle,
            string query,
            string platformSlug,
            List<RomHackSearchResult> results)
        {
            try
            {
                var metadataUrl = $"https://archive.org/metadata/{Uri.EscapeDataString(identifier)}";
                var metaResponse = _httpClient.Get(new HttpRequest(metadataUrl, HttpAccept.Json));
                if (metaResponse == null || metaResponse.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(metaResponse.Content))
                {
                    return false;
                }

                using var metaDoc = JsonDocument.Parse(metaResponse.Content);
                if (!metaDoc.RootElement.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var candidates = new List<(string Name, string Format, long? Size, int Score)>();
                foreach (var file in files.EnumerateArray())
                {
                    var name = file.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || IsIgnoredArchiveMetadataFile(name))
                    {
                        continue;
                    }

                    var ext = Path.GetExtension(name);
                    if (!PatchAndArchiveExtensions.Contains(ext))
                    {
                        continue;
                    }

                    long? size = null;
                    if (file.TryGetProperty("size", out var sizeProp))
                    {
                        if (sizeProp.ValueKind == JsonValueKind.Number && sizeProp.TryGetInt64(out var numSize))
                        {
                            size = numSize;
                        }
                        else if (sizeProp.ValueKind == JsonValueKind.String && long.TryParse(sizeProp.GetString(), out var strSize))
                        {
                            size = strSize;
                        }
                    }

                    var format = ext.TrimStart('.').ToUpperInvariant();
                    var score = ComputeRelevanceScore(query, $"{itemTitle} {Path.GetFileNameWithoutExtension(name)}");
                    candidates.Add((name, format, size, score));
                }

                var selected = candidates
                    .OrderByDescending(c => c.Score)
                    .ThenByDescending(c => GetFormatPriorityScore(c.Format))
                    .Take(2)
                    .ToList();

                if (!selected.Any())
                {
                    return false;
                }

                foreach (var file in selected)
                {
                    var encodedPath = string.Join("/", file.Name.Split('/').Select(Uri.EscapeDataString));
                    var displayName = Path.GetFileNameWithoutExtension(file.Name);

                    results.Add(new RomHackSearchResult
                    {
                        Title = string.IsNullOrWhiteSpace(displayName) ? itemTitle : displayName,
                        PlatformSlug = platformSlug,
                        BaseGameTitle = query,
                        SourceName = "Internet Archive (ROMhacking/Community)",
                        DownloadUrl = $"https://archive.org/download/{identifier}/{encodedPath}",
                        PageUrl = $"https://archive.org/details/{identifier}",
                        PatchFormat = file.Format,
                        HasDirectDownload = true,
                        FileSize = file.Size,
                        RelevanceScore = file.Score,
                        IsHomebrew = itemTitle.Contains("homebrew", StringComparison.OrdinalIgnoreCase)
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to inspect Internet Archive metadata for item '{0}'", identifier);
                return false;
            }
        }

        private List<RomHackSearchResult> QueryGitHubPatches(string query, string platformSlug)
        {
            var results = new List<RomHackSearchResult>();
            try
            {
                var q = $"{query} romhack OR ips OR bps";
                var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(q)}&per_page=5";
                var request = new HttpRequest(url, HttpAccept.Json);
                request.Headers.Add("User-Agent", "Controllarr");

                var response = _httpClient.Get(request);
                if (response == null || response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                using var doc = JsonDocument.Parse(response.Content);
                if (!doc.RootElement.TryGetProperty("items", out var items))
                {
                    return results;
                }

                foreach (var repo in items.EnumerateArray().Take(3))
                {
                    var fullName = repo.TryGetProperty("full_name", out var nameProp) ? nameProp.GetString() : null;
                    var htmlUrl = repo.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(htmlUrl))
                    {
                        continue;
                    }

                    var releaseUrl = $"https://api.github.com/repos/{fullName}/releases/latest";
                    var relRequest = new HttpRequest(releaseUrl, HttpAccept.Json);
                    relRequest.Headers.Add("User-Agent", "Controllarr");

                    var relResponse = _httpClient.Get(relRequest);
                    if (relResponse == null || relResponse.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(relResponse.Content))
                    {
                        continue;
                    }

                    using var relDoc = JsonDocument.Parse(relResponse.Content);
                    if (!relDoc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var patchAssets = new List<(string Name, string Url, string Format, long? Size)>();
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var assetName = asset.TryGetProperty("name", out var assetNameProp) ? assetNameProp.GetString() : null;
                        var downloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(downloadUrl))
                        {
                            continue;
                        }

                        var ext = Path.GetExtension(assetName);
                        if (!PatchAndArchiveExtensions.Contains(ext))
                        {
                            continue;
                        }

                        long? size = asset.TryGetProperty("size", out var sizeProp) && sizeProp.TryGetInt64(out var parsedSize)
                            ? parsedSize
                            : null;

                        patchAssets.Add((assetName, downloadUrl, ext.TrimStart('.').ToUpperInvariant(), size));
                    }

                    foreach (var asset in patchAssets.OrderByDescending(a => GetFormatPriorityScore(a.Format)).Take(2))
                    {
                        var title = Path.GetFileNameWithoutExtension(asset.Name);
                        results.Add(new RomHackSearchResult
                        {
                            Title = string.IsNullOrWhiteSpace(title) ? fullName : title,
                            PlatformSlug = platformSlug,
                            BaseGameTitle = query,
                            SourceName = "GitHub Releases",
                            DownloadUrl = asset.Url,
                            PageUrl = htmlUrl,
                            PatchFormat = asset.Format,
                            HasDirectDownload = true,
                            FileSize = asset.Size,
                            RelevanceScore = ComputeRelevanceScore(query, $"{fullName} {asset.Name}"),
                            IsHomebrew = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "GitHub release patch query failed for '{0}'", query);
            }

            return results;
        }

        private List<RomHackSearchResult> QueryGitHubHomebrew(string query, string platformSlug)
        {
            var results = new List<RomHackSearchResult>();
            try
            {
                var q = $"{query} homebrew rom";
                var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(q)}&per_page=10";
                var request = new HttpRequest(url, HttpAccept.Json);
                request.Headers.Add("User-Agent", "Controllarr");

                var response = _httpClient.Get(request);
                if (response == null || response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                using var doc = JsonDocument.Parse(response.Content);
                if (doc.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var repo in items.EnumerateArray())
                    {
                        var fullName = repo.TryGetProperty("full_name", out var nameProp) ? nameProp.GetString() : null;
                        var htmlUrl = repo.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(htmlUrl))
                        {
                            continue;
                        }

                        results.Add(new RomHackSearchResult
                        {
                            Title = fullName,
                            PlatformSlug = platformSlug,
                            BaseGameTitle = query,
                            SourceName = "GitHub Homebrew",
                            DownloadUrl = htmlUrl,
                            PageUrl = htmlUrl,
                            PatchFormat = "Homebrew",
                            HasDirectDownload = false,
                            RelevanceScore = ComputeRelevanceScore(query, fullName),
                            IsHomebrew = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "GitHub homebrew query failed for '{0}'", query);
            }

            return results;
        }

        private bool TryQueueMissingBaseRomSearch(
            Series series,
            string expectedBaseCrc32,
            string baseGameTitleHint,
            string fallbackTitle)
        {
            if (_commandQueueManager == null || _episodeService == null || series == null)
            {
                return false;
            }

            var datBaseName = !string.IsNullOrWhiteSpace(expectedBaseCrc32)
                ? _datCacheService?.FindByCrc32(expectedBaseCrc32)?.GameName
                : null;

            var searchTarget = BuildPatchSearchQuery(datBaseName)
                ?? BuildPatchSearchQuery(baseGameTitleHint)
                ?? BuildPatchSearchQuery(fallbackTitle);

            if (string.IsNullOrWhiteSpace(searchTarget))
            {
                return false;
            }

            var tokens = Tokenize(searchTarget);
            var retailEpisodes = (_episodeService.GetEpisodeBySeries(series.Id) ?? new List<Episode>())
                .Where(e => !e.HasFile && e.SeasonNumber >= 1 && e.SeasonNumber <= 4 && !string.IsNullOrWhiteSpace(e.Title))
                .ToList();

            var matchingIds = retailEpisodes
                .Select(e => new
                {
                    Episode = e,
                    Score = ComputeRelevanceScore(searchTarget, e.Title)
                })
                .Where(x => x.Score >= 50 ||
                            x.Episode.Title.Contains(searchTarget, StringComparison.OrdinalIgnoreCase) ||
                            (tokens.Count > 0 && tokens.All(t => x.Episode.Title.Contains(t, StringComparison.OrdinalIgnoreCase))))
                .OrderByDescending(x => x.Score)
                .Select(x => x.Episode.Id)
                .Take(3)
                .ToList();

            if (!matchingIds.Any())
            {
                return false;
            }

            _commandQueueManager.Push(new GameSearchCommand(matchingIds));
            return true;
        }

        private static bool DetermineIfBaseRomMissing(RomPatchResultModel patchResult)
        {
            if (patchResult == null)
            {
                return true;
            }

            if (patchResult.BaseRomMissing || !string.IsNullOrWhiteSpace(patchResult.ExpectedBaseRomCrc32))
            {
                return true;
            }

            var msg = patchResult.Message ?? string.Empty;
            return msg.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   msg.Contains("no matching retail", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidRomhackPlazaCandidateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.Contains("romhackplaza.org", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var lower = url.ToLowerInvariant();
            if (lower == "https://romhackplaza.org" || lower == "https://romhackplaza.org/")
            {
                return false;
            }

            string[] ignoredSegments =
            {
                "/?s=", "/category/", "/tag/", "/author/", "/page/",
                "/wp-login", "/wp-admin", "/feed", "#", "/about", "/contact", "/submit", "/privacy", "/terms"
            };

            return !ignoredSegments.Any(seg => lower.Contains(seg, StringComparison.Ordinal));
        }

        private static bool IsResolvableDetailPageUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   url.Contains("romhackplaza.org", StringComparison.OrdinalIgnoreCase) &&
                   IsValidRomhackPlazaCandidateUrl(url);
        }

        private static bool IsHtmlDetailPageUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   !IsDirectPatchOrArchiveUrl(url) &&
                   url.Contains("romhackplaza.org", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDirectPatchOrArchiveUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var pathOnly = url.Split('?')[0];
            var ext = Path.GetExtension(pathOnly);
            return !string.IsNullOrWhiteSpace(ext) && PatchAndArchiveExtensions.Contains(ext);
        }

        private static string DetectPatchFormatFromUrlOrText(string url, string linkText)
        {
            var pathOnly = (url ?? string.Empty).Split('?')[0];
            var ext = Path.GetExtension(pathOnly).ToLowerInvariant();
            if (ext == ".bps")
            {
                return "BPS";
            }

            if (ext == ".ips")
            {
                return "IPS";
            }

            if (ext == ".xdelta" || ext == ".vcdiff")
            {
                return "XDELTA";
            }

            if (ext == ".zip")
            {
                return "ZIP";
            }

            if (ext == ".7z")
            {
                return "7Z";
            }

            if (ext == ".rar")
            {
                return "RAR";
            }

            if (!string.IsNullOrWhiteSpace(linkText))
            {
                if (linkText.Contains("bps", StringComparison.OrdinalIgnoreCase))
                {
                    return "BPS";
                }

                if (linkText.Contains("ips", StringComparison.OrdinalIgnoreCase))
                {
                    return "IPS";
                }
            }

            return "Patch";
        }

        private static int GetFormatPriorityScore(string format)
        {
            return (format ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "BPS" => 5,
                "IPS" => 4,
                "XDELTA" => 3,
                "VCDIFF" => 3,
                "ZIP" => 2,
                "7Z" => 2,
                "RAR" => 1,
                _ => 0
            };
        }

        private static bool LooksLikeHtmlDocument(string filePath)
        {
            var buf = new byte[512];
            int read;
            using (var fs = File.OpenRead(filePath))
            {
                read = fs.Read(buf, 0, buf.Length);
            }

            if (read <= 0)
            {
                return false;
            }

            var header = System.Text.Encoding.UTF8.GetString(buf, 0, read).TrimStart();
            return header.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                   header.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                   header.StartsWith("<head", StringComparison.OrdinalIgnoreCase) ||
                   header.StartsWith("<body", StringComparison.OrdinalIgnoreCase) ||
                   header.StartsWith("<script", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsIgnoredArchiveMetadataFile(string path)
        {
            var lower = path.ToLowerInvariant();
            return lower.EndsWith("_meta.xml", StringComparison.Ordinal) ||
                   lower.EndsWith("_files.xml", StringComparison.Ordinal) ||
                   lower.EndsWith("_archive.torrent", StringComparison.Ordinal) ||
                   lower.EndsWith("_meta.sqlite", StringComparison.Ordinal) ||
                   lower.Contains("__macosx/", StringComparison.Ordinal);
        }

        private static string ResolveAbsoluteUrl(string pageUrl, string href)
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out var abs))
            {
                return abs.ToString();
            }

            if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, href, out var combined))
            {
                return combined.ToString();
            }

            return null;
        }

        private static bool IsPlatformCompatible(string targetSlug, string hint)
        {
            if (string.IsNullOrWhiteSpace(targetSlug) || string.IsNullOrWhiteSpace(hint))
            {
                return true;
            }

            var normalizedHint = PlatformCompatibilityValidator.NormalizePlatformSlug(hint, hint);
            if (string.IsNullOrWhiteSpace(normalizedHint))
            {
                return true;
            }

            return string.Equals(targetSlug, normalizedHint, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPatchSearchQuery(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            var withoutExt = Path.GetFileNameWithoutExtension(title.Replace('\\', '/'));
            var stripped = CleanQueryTagRegex.Replace(withoutExt, " ");
            stripped = Regex.Replace(stripped, @"\s+", " ").Trim(' ', '-', '_');

            return string.IsNullOrWhiteSpace(stripped) ? title.Trim() : stripped;
        }

        private static string CleanHtmlText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var decoded = WebUtility.HtmlDecode(raw);
            var stripped = HtmlTagRegex.Replace(decoded, " ");
            return Regex.Replace(stripped, @"\s+", " ").Trim();
        }

        private static string CleanHtmlMultiline(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var decoded = WebUtility.HtmlDecode(raw);
            var withBreaks = Regex.Replace(
                decoded,
                @"</?(?:div|p|li|tr|td|th|dt|dd|h[1-6]|br|section|article|header|footer|a)\b[^>]*>",
                "\n",
                RegexOptions.IgnoreCase);
            var stripped = HtmlTagRegex.Replace(withBreaks, " ");
            var normalizedHorizontal = Regex.Replace(stripped, @"[ \t\f\v]+", " ");
            return Regex.Replace(normalizedHorizontal, @"\s*\n+\s*", "\n").Trim();
        }

        private static List<string> Tokenize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return new List<string>();
            }

            return Regex.Split(input.ToLowerInvariant(), @"[^a-z0-9]+")
                .Where(t => t.Length >= 2 && !StopWords.Contains(t))
                .Distinct()
                .ToList();
        }

        private static int ComputeRelevanceScore(string query, string candidateText)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(candidateText))
            {
                return 0;
            }

            var qLower = query.Trim().ToLowerInvariant();
            var cLower = candidateText.Trim().ToLowerInvariant();

            if (cLower.Contains(qLower, StringComparison.Ordinal))
            {
                return 100;
            }

            var queryTokens = Tokenize(qLower);
            if (queryTokens.Count == 0)
            {
                return 0;
            }

            var candidateTokens = Tokenize(cLower).ToHashSet();
            var matched = queryTokens.Count(t => candidateTokens.Contains(t));
            return (int)Math.Round(matched * 80.0 / queryTokens.Count);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        }
    }
}
