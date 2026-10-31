using NzbDrone.Core.Organizer;

namespace Sonarr.Api.V5.Settings;

public class NamingExampleResource
{
    public string? SingleGameExample { get; set; }

    public string? MultiGameExample { get; set; }

    public string? DailyGameExample { get; set; }

    public string? AnimeGameExample { get; set; }

    public string? AnimeMultiGameExample { get; set; }

    public string? SeriesFolderExample { get; set; }

    public string? SeasonFolderExample { get; set; }

    public string? SpecialsFolderExample { get; set; }
}

public static class NamingConfigResourceMapper
{
    public static NamingSettingsResource ToResource(this NamingConfig model)
    {
        return new NamingSettingsResource
        {
            Id = model.Id,
            RenameGames = model.RenameEpisodes,
            ReplaceIllegalCharacters = model.ReplaceIllegalCharacters,
            ColonReplacementFormat = (int)model.ColonReplacementFormat,
            CustomColonReplacementFormat = model.CustomColonReplacementFormat,
            MultiGameStyle = (int)model.MultiEpisodeStyle,
            StandardGameFormat = model.StandardEpisodeFormat,
            DailyGameFormat = model.DailyEpisodeFormat,
            AnimeGameFormat = model.AnimeEpisodeFormat,
            SeriesFolderFormat = model.SeriesFolderFormat,
            SeasonFolderFormat = model.SeasonFolderFormat,
            SpecialsFolderFormat = model.SpecialsFolderFormat
        };
    }

    public static NamingConfig ToModel(this NamingSettingsResource resource)
    {
        return new NamingConfig
        {
            Id = resource.Id,
            RenameEpisodes = resource.RenameGames,
            ReplaceIllegalCharacters = resource.ReplaceIllegalCharacters,
            MultiEpisodeStyle = (MultiEpisodeStyle)resource.MultiGameStyle,
            ColonReplacementFormat = (ColonReplacementFormat)resource.ColonReplacementFormat,
            CustomColonReplacementFormat = resource.CustomColonReplacementFormat ?? "",
            StandardEpisodeFormat = resource.StandardGameFormat,
            DailyEpisodeFormat = resource.DailyGameFormat,
            AnimeEpisodeFormat = resource.AnimeGameFormat,
            SeriesFolderFormat = resource.SeriesFolderFormat,
            SeasonFolderFormat = resource.SeasonFolderFormat,
            SpecialsFolderFormat = resource.SpecialsFolderFormat
        };
    }
}
