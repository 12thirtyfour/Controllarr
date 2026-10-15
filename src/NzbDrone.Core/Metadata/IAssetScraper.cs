using System.Threading.Tasks;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.Metadata
{
    public interface IAssetScraper
    {
        Task FetchMissingAssetsAsync(GameRelease release, string destinationPath);
    }
}
