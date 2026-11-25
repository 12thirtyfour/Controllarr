namespace NzbDrone.Core.Notifications.Webhook
{
    public class WebhookPlatformAddPayload : WebhookPayload
    {
        public WebhookPlatform Platform { get; set; }
    }
}
