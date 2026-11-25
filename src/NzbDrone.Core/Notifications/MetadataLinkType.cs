using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Notifications
{
    public enum MetadataLinkType
    {
        [FieldOption(Label = "Game Catalog")]
        GameCatalog = 1,

        [FieldOption(Label = "TheGamesDB")]
        TheGamesDb = 2
    }
}
