using System.Collections.Generic;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookGameDeletePayload : WebhookPayload
    {
        public WebhookPlatform Platform { get; set; }
        public List<WebhookGame> Games { get; set; }
        public WebhookRomFile RomFile { get; set; }
        public DeleteMediaFileReason DeleteReason { get; set; }
    }
}
