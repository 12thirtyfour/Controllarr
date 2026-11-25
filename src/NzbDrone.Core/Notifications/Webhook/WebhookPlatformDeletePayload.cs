namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookPlatformDeletePayload : WebhookPayload
    {
        public WebhookPlatform Platform { get; set; }
        public bool DeletedFiles { get; set; }
    }
}
