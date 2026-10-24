using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Game.Model;

namespace NzbDrone.Core.MetadataSource.Igdb
{
    public class IgdbMetadataProxy : Game.IGameMetadataProxy
    {
        private readonly IIgdbTokenProvider _tokenProvider;
        private readonly IConfigService _configService;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public IgdbMetadataProxy(IIgdbTokenProvider tokenProvider, IConfigService configService, IHttpClient httpClient, Logger logger)
        {
            _tokenProvider = tokenProvider;
            _configService = configService;
            _httpClient = httpClient;
            _logger = logger;
        }

        private HttpRequestBuilder BuildBaseRequest(string endpoint)
        {
            var token = _tokenProvider.GetAccessToken();
            var clientId = _configService.IgdbClientId;

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(clientId))
            {
                _logger.Debug("IGDB token or Client-ID is missing. Request will not be successful.");
                return null;
            }

            return new HttpRequestBuilder($"https://api.igdb.com/v4/{endpoint}")
                .SetHeader("Client-ID", clientId)
                .SetHeader("Authorization", $"Bearer {token}")
                .SetHeader("Accept", "application/json");
        }

        public async Task<List<GamePlatform>> SearchPlatformsAsync(string query)
        {
            var requestBuilder = BuildBaseRequest("platforms");
            if (requestBuilder == null)
            {
                return new List<GamePlatform>();
            }

            var request = requestBuilder.Post().Build();
            request.Headers.ContentType = "text/plain";

            var body = "fields id,name,slug,generation,summary; limit 500;";
            if (!string.IsNullOrWhiteSpace(query))
            {
                body = $"search \"{query}\"; {body}";
            }

            request.ContentData = System.Text.Encoding.UTF8.GetBytes(body);

            try
            {
                var response = await _httpClient.ExecuteAsync(request);
                if (response.HasHttpError)
                {
                    _logger.Error($"Failed to search IGDB platforms. HTTP Status: {response.StatusCode}");
                    return new List<GamePlatform>();
                }

                var platforms = JsonConvert.DeserializeObject<List<IgdbPlatform>>(response.Content);
                return platforms?.Select(p => new GamePlatform
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    Generation = p.Generation > 0 ? p.Generation : (int?)null
                }).ToList() ?? new List<GamePlatform>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to search IGDB platforms.");
                return new List<GamePlatform>();
            }
        }

        public async Task<List<GameMetadata>> SearchGamesAsync(string title, int? platformId)
        {
            var combinedResults = new List<GameMetadata>();

            var requestBuilder = BuildBaseRequest("games");
            if (requestBuilder != null)
            {
                var request = requestBuilder.Post().Build();
                request.Headers.ContentType = "text/plain";

                var body = $"search \"{title}\"; fields id,name,summary,cover.url,first_release_date,platforms; limit 50;";
                if (platformId.HasValue)
                {
                    body = $"search \"{title}\"; fields id,name,summary,cover.url,first_release_date,platforms; where platforms = [{platformId.Value}]; limit 50;";
                }

                request.ContentData = System.Text.Encoding.UTF8.GetBytes(body);

                try
                {
                    var response = await _httpClient.ExecuteAsync(request);
                    if (!response.HasHttpError)
                    {
                        var games = JsonConvert.DeserializeObject<List<IgdbGame>>(response.Content);
                        if (games != null)
                        {
                            combinedResults.AddRange(games.Select(g => new GameMetadata
                            {
                                Id = g.Id,
                                Title = g.Name,
                                Summary = g.Summary,
                                CoverUrl = g.Cover != null && !string.IsNullOrWhiteSpace(g.Cover.Url) ? MapCoverUrl(g.Cover.Url) : null,
                                ReleaseDate = g.FirstReleaseDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(g.FirstReleaseDate).UtcDateTime : (DateTime?)null,
                                PlatformId = platformId ?? (g.Platforms != null && g.Platforms.Count > 0 ? g.Platforms.First() : 0)
                            }));
                        }
                    }
                    else
                    {
                        _logger.Error($"Failed to search IGDB games. HTTP Status: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to search IGDB games.");
                }
            }

            if (_configService != null && _configService.EnableTheGamesDb && !string.IsNullOrWhiteSpace(_configService.TheGamesDbApiKey))
            {
                var tgdbResults = await SearchTheGamesDbAsync(title, platformId);
                foreach (var item in tgdbResults)
                {
                    if (combinedResults.All(r => !string.Equals(r.Title, item.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        combinedResults.Add(item);
                    }
                }
            }

            if (_configService != null && _configService.EnableScreenScraper && !string.IsNullOrWhiteSpace(_configService.ScreenScraperUsername))
            {
                var ssResults = await SearchScreenScraperAsync(title, platformId);
                foreach (var item in ssResults)
                {
                    if (combinedResults.All(r => !string.Equals(r.Title, item.Title, StringComparison.OrdinalIgnoreCase)))
                    {
                        combinedResults.Add(item);
                    }
                }
            }

            return combinedResults;
        }

        private async Task<List<GameMetadata>> SearchTheGamesDbAsync(string title, int? platformId)
        {
            var results = new List<GameMetadata>();
            try
            {
                var apiKey = Uri.EscapeDataString(_configService.TheGamesDbApiKey.Trim());
                var query = Uri.EscapeDataString(title.Trim());
                var url = $"https://api.thegamesdb.net/v1/Games/ByGameName?apikey={apiKey}&name={query}&fields=overview,publishers,developers";
                var request = new HttpRequest(url)
                {
                    SuppressHttpError = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                var response = await _httpClient.ExecuteAsync(request);
                if (response == null || response.HasHttpError || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                var root = JObject.Parse(response.Content);
                var gamesArray = root["data"]?["games"] as JArray;
                if (gamesArray == null)
                {
                    return results;
                }

                foreach (var item in gamesArray)
                {
                    var id = item["id"]?.Value<int>() ?? 0;
                    var gameTitle = item["game_title"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(gameTitle))
                    {
                        continue;
                    }

                    DateTime? releaseDate = null;
                    var releaseDateStr = item["release_date"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(releaseDateStr) && DateTime.TryParse(releaseDateStr, out var parsedDate))
                    {
                        releaseDate = parsedDate;
                    }

                    results.Add(new GameMetadata
                    {
                        Id = id > 0 ? id : Math.Abs(gameTitle.GetHashCode()),
                        Title = gameTitle,
                        Summary = item["overview"]?.Value<string>(),
                        ReleaseDate = releaseDate,
                        PlatformId = platformId ?? (item["platform"]?.Value<int>() ?? 0)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to query TheGamesDB API for {0}", title);
            }

            return results;
        }

        private async Task<List<GameMetadata>> SearchScreenScraperAsync(string title, int? platformId)
        {
            var results = new List<GameMetadata>();
            try
            {
                var user = Uri.EscapeDataString(_configService.ScreenScraperUsername.Trim());
                var pass = Uri.EscapeDataString(_configService.ScreenScraperPassword?.Trim() ?? string.Empty);
                var query = Uri.EscapeDataString(title.Trim());
                var url = $"https://api.screenscraper.fr/api2/jeuRecherche.php?ssid={user}&sspassword={pass}&softname=Controllarr&output=json&recherche={query}";
                var request = new HttpRequest(url)
                {
                    SuppressHttpError = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                var response = await _httpClient.ExecuteAsync(request);
                if (response == null || response.HasHttpError || string.IsNullOrWhiteSpace(response.Content))
                {
                    return results;
                }

                var root = JObject.Parse(response.Content);
                var gamesArray = root["response"]?["jeux"] as JArray;
                if (gamesArray == null)
                {
                    return results;
                }

                foreach (var item in gamesArray)
                {
                    var id = item["id"]?.Value<int>() ?? 0;
                    var nomNode = (item["noms"] as JArray)?.FirstOrDefault();
                    var gameTitle = nomNode?["text"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(gameTitle))
                    {
                        continue;
                    }

                    var synopsisNode = (item["synopsis"] as JArray)?.FirstOrDefault();
                    var mediaArray = item["medias"] as JArray;
                    var boxMedia = mediaArray?.FirstOrDefault(m =>
                        string.Equals(m["type"]?.Value<string>(), "box-2D", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m["type"]?.Value<string>(), "box-3D", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m["type"]?.Value<string>(), "ss", StringComparison.OrdinalIgnoreCase));

                    results.Add(new GameMetadata
                    {
                        Id = id > 0 ? id : Math.Abs(gameTitle.GetHashCode()),
                        Title = gameTitle,
                        Summary = synopsisNode?["text"]?.Value<string>(),
                        CoverUrl = boxMedia?["url"]?.Value<string>(),
                        Developer = item["developpeur"]?["text"]?.Value<string>(),
                        Publisher = item["editeur"]?["text"]?.Value<string>(),
                        PlatformId = platformId ?? (item["systeme"]?["id"]?.Value<int>() ?? 0)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to query ScreenScraper API for {0}", title);
            }

            return results;
        }

        public async Task<GameMetadata> GetGameDetailsAsync(int gameId)
        {
            var requestBuilder = BuildBaseRequest("games");
            if (requestBuilder == null)
            {
                return null;
            }

            var request = requestBuilder.Post().Build();
            request.Headers.ContentType = "text/plain";

            var body = $"fields id,name,summary,cover.url,first_release_date,platforms,involved_companies.developer,involved_companies.publisher,involved_companies.company.name; where id = {gameId}; limit 1;";

            request.ContentData = System.Text.Encoding.UTF8.GetBytes(body);

            try
            {
                var response = await _httpClient.ExecuteAsync(request);
                if (response.HasHttpError)
                {
                    _logger.Error($"Failed to get IGDB game details. HTTP Status: {response.StatusCode}");
                    return null;
                }

                var games = JsonConvert.DeserializeObject<List<IgdbGame>>(response.Content);
                var g = games?.FirstOrDefault();

                if (g == null)
                {
                    return null;
                }

                var developer = g.InvolvedCompanies?.FirstOrDefault(c => c.Developer)?.Company?.Name;
                var publisher = g.InvolvedCompanies?.FirstOrDefault(c => c.Publisher)?.Company?.Name;

                return new GameMetadata
                {
                    Id = g.Id,
                    Title = g.Name,
                    Summary = g.Summary,
                    CoverUrl = g.Cover != null && !string.IsNullOrWhiteSpace(g.Cover.Url) ? MapCoverUrl(g.Cover.Url) : null,
                    ReleaseDate = g.FirstReleaseDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(g.FirstReleaseDate).UtcDateTime : (DateTime?)null,
                    Developer = developer,
                    Publisher = publisher,
                    PlatformId = g.Platforms != null && g.Platforms.Count > 0 ? g.Platforms.First() : 0
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to get IGDB game details for {gameId}.");
                return null;
            }
        }

        public Task<GameMetadata> GetGameByHashAsync(string romHash)
        {
            return Task.FromResult<GameMetadata>(null);
        }

        private string MapCoverUrl(string url)
        {
            if (url.StartsWith("//"))
            {
                return "https:" + url;
            }

            return url;
        }

        private class IgdbPlatform
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("slug")]
            public string Slug { get; set; }

            [JsonProperty("summary")]
            public string Summary { get; set; }

            [JsonProperty("generation")]
            public int Generation { get; set; }
        }

        private class IgdbGame
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("summary")]
            public string Summary { get; set; }

            [JsonProperty("cover")]
            public IgdbCover Cover { get; set; }

            [JsonProperty("platforms")]
            public List<int> Platforms { get; set; }

            [JsonProperty("first_release_date")]
            public long FirstReleaseDate { get; set; }

            [JsonProperty("involved_companies")]
            public List<IgdbInvolvedCompany> InvolvedCompanies { get; set; }
        }

        private class IgdbCover
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }
        }

        private class IgdbInvolvedCompany
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("developer")]
            public bool Developer { get; set; }

            [JsonProperty("publisher")]
            public bool Publisher { get; set; }

            [JsonProperty("company")]
            public IgdbCompany Company { get; set; }
        }

        private class IgdbCompany
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }
        }
    }
}
