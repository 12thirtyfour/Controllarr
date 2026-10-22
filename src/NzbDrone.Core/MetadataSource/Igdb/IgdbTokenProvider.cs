using System;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource.Igdb
{
    public class IgdbTokenProvider : IIgdbTokenProvider
    {
        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;
        private readonly object _lock = new object();

        private string _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public IgdbTokenProvider(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public string GetAccessToken()
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _accessToken;
            }

            lock (_lock)
            {
                if (!string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _tokenExpiration)
                {
                    return _accessToken;
                }

                var clientId = _configService.IgdbClientId;
                var clientSecret = _configService.IgdbClientSecret;

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    _logger.Warn("IGDB ClientId or ClientSecret is not configured.");
                    return null;
                }

                try
                {
                    var request = new HttpRequestBuilder("https://id.twitch.tv/oauth2/token")
                        .Post()
                        .AddQueryParam("client_id", clientId)
                        .AddQueryParam("client_secret", clientSecret)
                        .AddQueryParam("grant_type", "client_credentials")
                        .Build();

                    var response = _httpClient.Execute(request);
                    if (response.HasHttpError)
                    {
                        _logger.Error($"Failed to authenticate with Twitch OAuth. HTTP Status: {response.StatusCode}");
                        return null;
                    }

                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(response.Content);

                    if (tokenResponse != null && !string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                    {
                        _accessToken = tokenResponse.AccessToken;
                        _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);
                        return _accessToken;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to authenticate with Twitch OAuth.");
                }

                return null;
            }
        }

        private class TokenResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty("token_type")]
            public string TokenType { get; set; }
        }
    }
}
