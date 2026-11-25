using System.Collections.Generic;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookRenamePayload : WebhookPayload
    {
        public WebhookPlatform Platform { get; set; }
        public List<WebhookRenamedRomFile> RenamedRomFiles { get; set; }
    }
}
