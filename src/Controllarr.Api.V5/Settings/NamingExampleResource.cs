using NzbDrone.Core.Organizer;

namespace Sonarr.Api.V5.Settings;

public class NamingExampleResource
{
    public string? SingleGameExample { get; set; }

    public string? MultiGameExample { get; set; }

    public string? DiscGameExample { get; set; }

    public string? DailyGameExample
    {
        get => DiscGameExample;
        set => DiscGameExample = value;
    }

    public string? AnimeGameExample { get; set; }

    public string? AnimeMultiGameExample { get; set; }

    public string? PlatformFolderExample { get; set; }

    public string? RegionFolderExample { get; set; }

    public string? UnlicensedFolderExample { get; set; }

    public string? SeriesFolderExample
    {
        get => PlatformFolderExample;
        set => PlatformFolderExample = value;
    }

    public string? SeasonFolderExample
    {
        get => RegionFolderExample;
        set => RegionFolderExample = value;
    }

    public string? SpecialsFolderExample
    {
        get => UnlicensedFolderExample;
        set => UnlicensedFolderExample = value;
    }
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
            StandardGameFormat = model.StandardGameFormat,
            DiscGameFormat = model.DiscGameFormat,
            DailyGameFormat = model.DiscGameFormat,
            AnimeGameFormat = model.AnimeGameFormat,
            PlatformFolderFormat = model.PlatformFolderFormat,
            RegionFolderFormat = model.RegionFolderFormat,
            UnlicensedFolderFormat = model.UnlicensedFolderFormat
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
            StandardGameFormat = resource.StandardGameFormat,
            DiscGameFormat = resource.DiscGameFormat,
            AnimeGameFormat = resource.AnimeGameFormat,
            PlatformFolderFormat = resource.PlatformFolderFormat,
            RegionFolderFormat = resource.RegionFolderFormat,
            UnlicensedFolderFormat = resource.UnlicensedFolderFormat
        };
    }
}
