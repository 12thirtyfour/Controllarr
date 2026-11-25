using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookPlatform
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string TitleSlug { get; set; }
        public string Path { get; set; }
        public int CatalogId { get; set; }
        public int Year { get; set; }
        public List<string> Genres { get; set; }
        public List<WebhookImage> Images { get; set; }
        public List<string> Tags { get; set; }

        public WebhookPlatform()
        {
        }

        public WebhookPlatform(Series platform, List<string> tags)
        {
            Id = platform.Id;
            Title = platform.Title;
            TitleSlug = platform.TitleSlug;
            Path = platform.Path;
            CatalogId = platform.TvdbId;
            Year = platform.Year;
            Genres = platform.Genres;
            Images = platform.Images.Select(i => new WebhookImage(i)).ToList();
            Tags = tags;
        }
    }
}
