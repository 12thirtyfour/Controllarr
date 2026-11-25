namespace NzbDrone.Core.Notifications.Discord
{
    public enum DiscordGrabFieldType
    {
        Overview,
        Rating,
        Genres,
        Quality,
        Group,
        Size,
        Links,
        Release,
        Poster,
        Fanart,
        Indexer,
        CustomFormats,
        CustomFormatScore
    }

    public enum DiscordImportFieldType
    {
        Overview = 0,
        Rating = 1,
        Genres = 2,
        Quality = 3,
        Group = 5,
        Size = 6,
        Languages = 7,
        Links = 9,
        Release = 10,
        Poster = 11,
        Fanart = 12,
        CustomFormats = 13,
        CustomFormatScore = 14
    }

    public enum DiscordManualInteractionFieldType
    {
        Overview,
        Rating,
        Genres,
        Quality,
        Group,
        Size,
        Links,
        DownloadTitle,
        Poster,
        Fanart
    }
}
