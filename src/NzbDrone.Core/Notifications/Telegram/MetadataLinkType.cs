using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Notifications.Telegram
{
    // Maintain the same values as MetadataLinkType

    public enum MetadataLinkPreviewType
    {
        [FieldOption(Label = "None")]
        None = -1,

        [FieldOption(Label = "IMDb")]
        Imdb = 0,

        [FieldOption(Label = "Game Catalog")]
        GameCatalog = 1,

        [FieldOption(Label = "TheGamesDb")]
        TheGamesDb = 2,

        // Legacy compatibility aliases
        Tvdb = 1,
        Tvmaze = 2,
        Trakt = 3
    }
}
