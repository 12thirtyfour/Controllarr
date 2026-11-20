namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookPlatformDeletePayload : WebhookPayload
    {
        public WebhookPlatform Series { get; set; }
        public bool DeletedFiles { get; set; }
    }
}
