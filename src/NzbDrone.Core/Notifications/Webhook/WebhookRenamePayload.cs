using System.Collections.Generic;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookRenamePayload : WebhookPayload
    {
        public WebhookPlatform Series { get; set; }
        public List<WebhookRenamedRomFile> RenamedEpisodeFiles { get; set; }
    }
}
