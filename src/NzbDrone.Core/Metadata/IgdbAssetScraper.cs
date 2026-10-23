using System;
using System.IO;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Core.Games;
using NzbDrone.Core.MetadataSource.Game;

namespace NzbDrone.Core.Metadata
{
    public class IgdbAssetScraper : IAssetScraper
    {
        private readonly IGameMetadataProxy _metadataProxy;
        private readonly Logger _logger;

        public IgdbAssetScraper(IGameMetadataProxy metadataProxy, Logger logger)
        {
            _metadataProxy = metadataProxy;
            _logger = logger;
        }

        public async Task FetchMissingAssetsAsync(GameRelease release, string destinationPath)
        {
            if (string.IsNullOrEmpty(destinationPath) || !Directory.Exists(destinationPath))
            {
                _logger.Warn($"Cannot scrape assets to an invalid directory: {destinationPath}");
                return;
            }

            _logger.Debug($"Initiating asset scraping for {release.Title}");

            // Mock Implementation representing external HTTP client fetching image blobs
            var assetsToFetch = new[]
            {
                "boxart-front.png",
                "boxart-back.png",
                "boxart-3d.png",
                "snap.png",
                "title.png",
                "wheel.png",
                "manual.pdf",
                "video.mp4",
                "metadata.json"
            };

            foreach (var assetName in assetsToFetch)
            {
                var fullAssetPath = Path.Combine(destinationPath, assetName);
                if (!File.Exists(fullAssetPath))
                {
                    _logger.Trace($"Mock downloading {assetName} to {fullAssetPath}");

                    // Here we'd utilize an HttpClient wired through IGameMetadataProxy
                    // to stream the bytes from IGDB/Twitch CDN. We simulate this by writing empty bytes.
                    await File.WriteAllBytesAsync(fullAssetPath, Array.Empty<byte>());
                }
            }
        }
    }
}
