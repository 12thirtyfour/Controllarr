using System.Collections.Generic;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookImportPayload : WebhookPayload
    {
        public WebhookPlatform Series { get; set; }
        public List<WebhookGame> Episodes { get; set; }
        public WebhookRomFile EpisodeFile { get; set; }
        public bool IsUpgrade { get; set; }
        public string DownloadClient { get; set; }
        public string DownloadClientType { get; set; }
        public string DownloadId { get; set; }
        public List<WebhookRomFile> DeletedFiles { get; set; }
        public WebhookCustomFormatInfo CustomFormatInfo { get; set; }
        public WebhookGrabbedRelease Release { get; set; }
    }
}
