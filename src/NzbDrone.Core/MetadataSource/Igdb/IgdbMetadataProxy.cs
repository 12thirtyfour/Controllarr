using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
                _logger.Warn("IGDB token or Client-ID is missing. Request will not be successful.");
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
            var requestBuilder = BuildBaseRequest("games");
            if (requestBuilder == null)
            {
                return new List<GameMetadata>();
            }

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
                if (response.HasHttpError)
                {
                    _logger.Error($"Failed to search IGDB games. HTTP Status: {response.StatusCode}");
                    return new List<GameMetadata>();
                }

                var games = JsonConvert.DeserializeObject<List<IgdbGame>>(response.Content);
                return games?.Select(g => new GameMetadata
                {
                    Id = g.Id,
                    Title = g.Name,
                    Summary = g.Summary,
                    CoverUrl = g.Cover != null && !string.IsNullOrWhiteSpace(g.Cover.Url) ? MapCoverUrl(g.Cover.Url) : null,
                    ReleaseDate = g.FirstReleaseDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(g.FirstReleaseDate).UtcDateTime : (DateTime?)null,
                    PlatformId = platformId ?? (g.Platforms != null && g.Platforms.Count > 0 ? g.Platforms.First() : 0)
                }).ToList() ?? new List<GameMetadata>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to search IGDB games.");
                return new List<GameMetadata>();
            }
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
