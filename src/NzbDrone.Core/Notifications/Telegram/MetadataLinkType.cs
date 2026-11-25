using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Notifications.Telegram
{
    // Maintain the same values as MetadataLinkType

    public enum MetadataLinkPreviewType
    {
        [FieldOption(Label = "None")]
        None = -1,

        [FieldOption(Label = "Game Catalog")]
        GameCatalog = 1,

        [FieldOption(Label = "TheGamesDB")]
        TheGamesDb = 2
    }
}
