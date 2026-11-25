using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Notifications.Pushover
{
    public class Pushover : NotificationBase<PushoverSettings>
    {
        private readonly IPushoverProxy _proxy;

        public Pushover(IPushoverProxy proxy)
        {
            _proxy = proxy;
        }

        public override string Name => "Pushover";
        public override string Link => "https://pushover.net/";

        public override void OnGrab(GrabMessage grabMessage)
        {
            _proxy.SendNotification(GAME_GRABBED_TITLE_BRANDED, grabMessage.Message, Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            _proxy.SendNotification(GAME_DOWNLOADED_TITLE_BRANDED, message.Message, Settings);
        }

        public override void OnImportComplete(ImportCompleteMessage message)
        {
            _proxy.SendNotification(IMPORT_COMPLETE_TITLE_BRANDED, message.Message, Settings);
        }

        public override void OnEpisodeFileDelete(GameDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(GAME_DELETED_TITLE_BRANDED, deleteMessage.Message, Settings);
        }

        public override void OnSeriesAdd(PlatformAddMessage message)
        {
            _proxy.SendNotification(PLATFORM_ADDED_TITLE_BRANDED, message.Message, Settings);
        }

        public override void OnSeriesDelete(PlatformDeleteMessage deleteMessage)
        {
            _proxy.SendNotification(PLATFORM_DELETED_TITLE_BRANDED, deleteMessage.Message, Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            _proxy.SendNotification(HEALTH_ISSUE_TITLE_BRANDED, healthCheck.Message, Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            _proxy.SendNotification(HEALTH_RESTORED_TITLE_BRANDED, $"The following issue is now resolved: {previousCheck.Message}", Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            _proxy.SendNotification(APPLICATION_UPDATE_TITLE_BRANDED, updateMessage.Message, Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            _proxy.SendNotification(MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED, message.Message, Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }
    }
}
