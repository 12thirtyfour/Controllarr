using System.Collections.Generic;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookGameDeletePayload : WebhookPayload
    {
        public WebhookPlatform Series { get; set; }
        public List<WebhookGame> Episodes { get; set; }
        public WebhookRomFile EpisodeFile { get; set; }
        public DeleteMediaFileReason DeleteReason { get; set; }
    }
}
