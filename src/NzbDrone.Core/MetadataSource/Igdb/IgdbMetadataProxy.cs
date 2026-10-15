using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Games;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.MetadataSource.Igdb
{
    public class IgdbMetadataProxy : IGameMetadataProxy
    {
        private readonly IIgdbTokenManager _tokenManager;
        private readonly IConfigService _configService;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public IgdbMetadataProxy(IIgdbTokenManager tokenManager, IConfigService configService, IHttpClient httpClient, Logger logger)
        {
            _tokenManager = tokenManager;
            _configService = configService;
            _httpClient = httpClient;
            _logger = logger;
        }

        private HttpRequestBuilder BuildBaseRequest(string endpoint)
        {
            var token = _tokenManager.GetToken();
            var clientId = _configService.IgdbClientId;

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(clientId))
            {
                throw new Exception("IGDB token or Client-ID is missing.");
            }

            return new HttpRequestBuilder($"https://api.igdb.com/v4/{endpoint}")
                .SetHeader("Client-ID", clientId)
                .SetHeader("Authorization", $"Bearer {token}");
        }

        public List<GamePlatform> GetPlatforms()
        {
            var request = BuildBaseRequest("platforms")
                .Post()
                .SetHeader("Accept", "application/json")
                .Build();

            request.ContentData = System.Text.Encoding.UTF8.GetBytes("fields id,name,slug,generation,summary; limit 500;");
            request.Headers.ContentType = "text/plain";

            var response = _httpClient.Post<List<IgdbPlatform>>(request);

            return response.Resource.Select(p => new GamePlatform
            {
                IgdbId = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Overview = p.Summary
            }).ToList();
        }

        public Tuple<GameTitle, List<GameRelease>> GetGameInfo(string providerId, Language language)
        {
            if (!providerId.StartsWith("igdb:"))
            {
                throw new ArgumentException("Provider ID must start with 'igdb:'", nameof(providerId));
            }

            var igdbIdStr = providerId.Substring(5);
            if (!int.TryParse(igdbIdStr, out var igdbId))
            {
                throw new ArgumentException("Invalid IGDB ID", nameof(providerId));
            }

            // Get Game Info
            var gameRequest = BuildBaseRequest("games")
                .Post()
                .SetHeader("Accept", "application/json")
                .Build();

            gameRequest.ContentData = System.Text.Encoding.UTF8.GetBytes($"fields id,name,summary,cover.url,release_dates.region,release_dates.y,release_dates.m,release_dates.d,platforms; where id = {igdbId};");
            gameRequest.Headers.ContentType = "text/plain";

            var gameResponse = _httpClient.Post<List<IgdbGame>>(gameRequest);
            var igdbGame = gameResponse.Resource.FirstOrDefault();

            if (igdbGame == null)
            {
                return null;
            }

            var title = new GameTitle
            {
                IgdbId = igdbGame.Id,
                Title = igdbGame.Name,
                CleanTitle = igdbGame.Name,
                ProviderId = providerId,
                Summary = igdbGame.Summary,
                CoverArtworkUrl = igdbGame.Cover?.Url
            };

            var releases = new List<GameRelease>();

            if (igdbGame.ReleaseDates != null)
            {
                foreach (var releaseDate in igdbGame.ReleaseDates)
                {
                    releases.Add(new GameRelease
                    {
                        Region = releaseDate.Region.ToString(), // Or map to region name if needed
                        ReleaseName = $"{igdbGame.Name} ({releaseDate.Region})",
                        Title = igdbGame.Name
                    });
                }
            }

            return new Tuple<GameTitle, List<GameRelease>>(title, releases);
        }

        public List<GameTitle> SearchForNewGames(string title, Language language)
        {
            var request = BuildBaseRequest("games")
                .Post()
                .SetHeader("Accept", "application/json")
                .Build();

            request.ContentData = System.Text.Encoding.UTF8.GetBytes($"search \"{title}\"; fields id,name,summary,cover.url; limit 50;");
            request.Headers.ContentType = "text/plain";

            var response = _httpClient.Post<List<IgdbGame>>(request);

            return response.Resource.Select(g => new GameTitle
            {
                IgdbId = g.Id,
                Title = g.Name,
                CleanTitle = g.Name,
                ProviderId = $"igdb:{g.Id}",
                Summary = g.Summary,
                CoverArtworkUrl = g.Cover?.Url
            }).ToList();
        }

        public class IgdbPlatform
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

        public class IgdbGame
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("summary")]
            public string Summary { get; set; }

            [JsonProperty("cover")]
            public IgdbCover Cover { get; set; }

            [JsonProperty("release_dates")]
            public List<IgdbReleaseDate> ReleaseDates { get; set; }
        }

        public class IgdbCover
        {
            [JsonProperty("url")]
            public string Url { get; set; }
        }

        public class IgdbReleaseDate
        {
            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("region")]
            public int Region { get; set; }
        }
    }
}
