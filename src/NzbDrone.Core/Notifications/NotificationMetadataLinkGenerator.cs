using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications;

public static class NotificationMetadataLinkGenerator
{
    public static List<NotificationMetadataLink> GenerateLinks(Series series, IEnumerable<int> metadataLinks)
    {
        var links = new List<NotificationMetadataLink>();

        if (series == null)
        {
            return links;
        }

        foreach (var link in metadataLinks)
        {
            var linkType = (MetadataLinkType)link;

            if (linkType == MetadataLinkType.Imdb && series.ImdbId.IsNotNullOrWhiteSpace())
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.Imdb, "IMDb", $"https://www.imdb.com/title/{series.ImdbId}"));
            }

            if ((linkType == MetadataLinkType.GameCatalog || linkType == MetadataLinkType.Tvdb) && (series.TvdbId > 0 || series.TitleSlug.IsNotNullOrWhiteSpace()))
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.GameCatalog, "Game Catalog", "https://www.igdb.com/"));
            }

            if ((linkType == MetadataLinkType.TheGamesDb || linkType == MetadataLinkType.Tvmaze) && (series.TvMazeId > 0 || series.TitleSlug.IsNotNullOrWhiteSpace()))
            {
                links.Add(new NotificationMetadataLink(MetadataLinkType.TheGamesDb, "TheGamesDB", "https://thegamesdb.net/"));
            }
        }

        return links;
    }
}
