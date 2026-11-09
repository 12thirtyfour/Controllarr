using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Settings;

public class NamingSettingsResource : RestResource
{
    public bool RenameGames { get; set; }

    public bool ReplaceIllegalCharacters { get; set; }

    public int ColonReplacementFormat { get; set; }

    public string? CustomColonReplacementFormat { get; set; }

    public int MultiGameStyle { get; set; }

    public string? StandardGameFormat { get; set; }

    public string? DiscGameFormat
    {
        get => DailyGameFormat;
        set => DailyGameFormat = value;
    }

    public string? DailyGameFormat { get; set; }

    public string? AnimeGameFormat { get; set; }

    public string? PlatformFolderFormat
    {
        get => SeriesFolderFormat;
        set => SeriesFolderFormat = value;
    }

    public string? RegionFolderFormat
    {
        get => SeasonFolderFormat;
        set => SeasonFolderFormat = value;
    }

    public string? UnlicensedFolderFormat
    {
        get => SpecialsFolderFormat;
        set => SpecialsFolderFormat = value;
    }

    public string? SeriesFolderFormat { get; set; }

    public string? SeasonFolderFormat { get; set; }

    public string? SpecialsFolderFormat { get; set; }
}
