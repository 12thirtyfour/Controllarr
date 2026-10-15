using System;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource.Igdb
{
    public class IgdbTokenManager : IIgdbTokenManager
    {
        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        private string _accessToken;
        private DateTime _tokenExpiration = DateTime.MinValue;

        public IgdbTokenManager(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public string GetToken()
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return _accessToken;
            }

            var clientId = _configService.IgdbClientId;
            var clientSecret = _configService.IgdbClientSecret;

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                // _logger.Warn("IGDB ClientId or ClientSecret is not configured.");
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

                var response = _httpClient.Post<TokenResponse>(request);

                if (response.Resource != null && !string.IsNullOrWhiteSpace(response.Resource.AccessToken))
                {
                    _accessToken = response.Resource.AccessToken;
                    _tokenExpiration = DateTime.UtcNow.AddSeconds(response.Resource.ExpiresIn - 60); // Refresh 1 minute early
                    return _accessToken;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to authenticate with Twitch OAuth.");
            }

            return null;
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
