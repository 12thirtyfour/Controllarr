using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles.Patching
{
    public class RomHackSearchResult
    {
        public string Title { get; set; }
        public string PlatformSlug { get; set; }
        public string BaseGameTitle { get; set; }
        public string SourceName { get; set; }
        public string DownloadUrl { get; set; }
        public string PatchFormat { get; set; }
        public bool IsHomebrew { get; set; }
    }

    public class RomHackAcquireRequest
    {
        public int SeriesId { get; set; }
        public string Title { get; set; }
        public string DownloadUrl { get; set; }
        public string BaseGameTitle { get; set; }
        public bool IsHomebrew { get; set; }
        public bool SearchMissingBaseRom { get; set; } = true;
    }

    public class RomHackAcquireResult
    {
        public bool Success { get; set; }
        public bool BaseRomMissing { get; set; }
        public string OutputRomPath { get; set; }
        public string Message { get; set; }
    }

    public interface IRomHackAutomationService
    {
        List<RomHackSearchResult> SearchCommunitySources(string query, string platformSlug = null);
        RomHackAcquireResult AcquireAndCreate(RomHackAcquireRequest request);
    }

    public class RomHackAutomationService : IRomHackAutomationService
    {
        private static readonly Regex RomhackPlazaLinkRegex = new Regex(
            @"<a[^>]+href=""(?<url>https?://[^""]*romhackplaza\.org/[^""]+)""[^>]*>(?<title>[^<]+)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly IRomPatchingService _romPatchingService;
        private readonly IDownloadedGamesImportService _downloadedGamesImportService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public RomHackAutomationService(
            IHttpClient httpClient,
            IRomPatchingService romPatchingService,
            ISeriesService seriesService,
            IEpisodeService episodeService,
            Logger logger,
            IDownloadedGamesImportService downloadedGamesImportService = null,
            IManageCommandQueue commandQueueManager = null)
        {
            _httpClient = httpClient;
            _romPatchingService = romPatchingService;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _downloadedGamesImportService = downloadedGamesImportService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        public List<RomHackSearchResult> SearchCommunitySources(string query, string platformSlug = null)
        {
            var results = new List<RomHackSearchResult>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            results.AddRange(QueryRomhackPlaza(query, platformSlug));
            results.AddRange(QueryInternetArchivePatches(query, platformSlug));
            results.AddRange(QueryGitHubHomebrew(query, platformSlug));

            return results
                .GroupBy(r => r.DownloadUrl ?? r.Title, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        public RomHackAcquireResult AcquireAndCreate(RomHackAcquireRequest request)
        {
            if (request == null || request.SeriesId <= 0 || string.IsNullOrWhiteSpace(request.DownloadUrl))
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

            var tempDir = Path.Combine(Path.GetTempPath(), "controllarr_romhack_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var uri = new Uri(request.DownloadUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = (request.Title ?? "patch") + (request.IsHomebrew ? ".zip" : ".bps");
                }

                var localArtifactPath = Path.Combine(tempDir, fileName);
                _httpClient.DownloadFile(request.DownloadUrl, localArtifactPath);

                if (request.IsHomebrew && _downloadedGamesImportService != null)
                {
                    var importResults = _downloadedGamesImportService.ProcessPath(localArtifactPath, GameImport.ImportMode.Move, series);
                    var imported = importResults?.FirstOrDefault(r => r.Result == GameImport.ImportResultType.Imported);
                    if (imported != null)
                    {
                        return new RomHackAcquireResult
                        {
                            Success = true,
                            OutputRomPath = imported.EpisodeFile?.RelativePath,
                            Message = $"Imported homebrew '{request.Title}' into {series.Title}."
                        };
                    }
                }

                var patchResult = _romPatchingService.TryApplyPatchFileForSeries(localArtifactPath, series);
                if (patchResult != null && patchResult.Success)
                {
                    return new RomHackAcquireResult
                    {
                        Success = true,
                        OutputRomPath = patchResult.OutputRomPath,
                        Message = $"Patched and imported '{request.Title}' into {series.Title}."
                    };
                }

                var missingBaseTitle = request.BaseGameTitle ?? request.Title;
                if (request.SearchMissingBaseRom && _commandQueueManager != null && !string.IsNullOrWhiteSpace(missingBaseTitle))
                {
                    var baseGames = _episodeService.GetEpisodeBySeries(series.Id)
                        .Where(e => !e.HasFile && e.Title != null && e.Title.Contains(missingBaseTitle, StringComparison.OrdinalIgnoreCase))
                        .Select(e => e.Id)
                        .Take(3)
                        .ToList();

                    if (baseGames.Any())
                    {
                        _commandQueueManager.Push(new GameSearchCommand(baseGames));
                    }
                }

                return new RomHackAcquireResult
                {
                    Success = false,
                    BaseRomMissing = true,
                    Message = patchResult?.Message ?? $"Matching base retail ROM for '{missingBaseTitle}' was not found in your {series.Title} library."
                };
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to acquire/apply ROM hack or homebrew '{0}' from {1}", request.Title, request.DownloadUrl);
                return new RomHackAcquireResult
                {
                    Success = false,
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
                if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                foreach (Match match in RomhackPlazaLinkRegex.Matches(response.Content))
                {
                    var href = match.Groups["url"].Value;
                    var title = WebUtility.HtmlDecode(match.Groups["title"].Value).Trim();
                    if (string.IsNullOrWhiteSpace(title) || href.Contains("/?s=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results.Add(new RomHackSearchResult
                    {
                        Title = title,
                        PlatformSlug = platformSlug,
                        BaseGameTitle = query,
                        SourceName = "Romhack Plaza",
                        DownloadUrl = href,
                        PatchFormat = "Patch"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Romhack Plaza query failed for '{0}'", query);
            }

            return results;
        }

        private List<RomHackSearchResult> QueryInternetArchivePatches(string query, string platformSlug)
        {
            var results = new List<RomHackSearchResult>();
            try
            {
                var lucene = $"({query}) AND (romhack OR translation OR \"rom hack\" OR ips OR bps OR homebrew)";
                var url = $"https://archive.org/advancedsearch.php?q={Uri.EscapeDataString(lucene)}&fl[]=identifier,title&rows=15&page=1&output=json";
                var response = _httpClient.Get(new HttpRequest(url, HttpAccept.Json));
                if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                using var doc = JsonDocument.Parse(response.Content);
                if (doc.RootElement.TryGetProperty("response", out var resp) &&
                    resp.TryGetProperty("docs", out var docs))
                {
                    foreach (var item in docs.EnumerateArray())
                    {
                        var id = item.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : id;
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        results.Add(new RomHackSearchResult
                        {
                            Title = title ?? id,
                            PlatformSlug = platformSlug,
                            BaseGameTitle = query,
                            SourceName = "Internet Archive (ROMhacking/Community)",
                            DownloadUrl = $"https://archive.org/download/{id}",
                            PatchFormat = "Archive",
                            IsHomebrew = (title ?? string.Empty).Contains("homebrew", StringComparison.OrdinalIgnoreCase)
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
                if (response.StatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(response.Content))
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
                            PatchFormat = "Homebrew",
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
    }
}
