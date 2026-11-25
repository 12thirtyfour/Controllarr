using NzbDrone.Core.Games;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookGame
    {
        public WebhookGame()
        {
        }

        public WebhookGame(Episode game)
        {
            Id = game.Id;
            PlatformId = game.SeriesId;
            RegionNumber = game.SeasonNumber;
            Region = RegionHelper.GetRegionName(game.SeasonNumber);
            GameNumber = game.EpisodeNumber;
            Title = game.Title;
            Overview = game.Overview;
        }

        public int Id { get; set; }
        public int PlatformId { get; set; }
        public int RegionNumber { get; set; }
        public string Region { get; set; }
        public int GameNumber { get; set; }
        public string Title { get; set; }
        public string Overview { get; set; }
    }
}
