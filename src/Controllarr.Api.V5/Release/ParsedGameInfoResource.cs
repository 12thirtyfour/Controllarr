using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace Sonarr.Api.V5.Release;

public class ParsedGameInfoResource
{
    public string? ReleaseTitle { get; set; }
    public string? PlatformTitle { get; set; }
    public SeriesTitleInfo? PlatformTitleInfo { get; set; }
    public QualityModel? Quality { get; set; }
    public string? ReleaseGroup { get; set; }
    public string? ReleaseHash { get; set; }
    public bool FullSeason { get; set; }
    public bool IsPartialSeason { get; set; }
    public bool IsMultiSeason { get; set; }
    public bool IsSeasonExtra { get; set; }
    public int? SeasonNumber { get; set; }
    public List<int> SeasonNumbers { get; set; } = [];
    public int SeasonPart { get; set; }
    public string? AirDate { get; set; }
    public int[] GameNumbers { get; set; } = [];
    public int[] AbsoluteGameNumbers { get; set; } = [];
    public decimal[] SpecialAbsoluteGameNumbers { get; set; } = [];
    public List<Language> Languages { get; set; } = [];
    public string? ReleaseTokens { get; set; }
    public bool IsDaily { get; set; }
    public bool IsAbsoluteNumbering { get; set; }
    public bool IsPossibleSpecialGame { get; set; }
    public bool IsPossibleSceneSeasonSpecial { get; set; }
    public bool Special { get; set; }
}

public class ParsedEpisodeInfoResource : ParsedGameInfoResource
{
}

public static class ParsedGameInfoResourceMapper
{
    public static ParsedEpisodeInfoResource ToResource(this ParsedEpisodeInfo parsedEpisodeInfo)
    {
        return new ParsedEpisodeInfoResource
        {
            ReleaseTitle = parsedEpisodeInfo.ReleaseTitle,
            PlatformTitle = parsedEpisodeInfo.SeriesTitle,
            PlatformTitleInfo = parsedEpisodeInfo.SeriesTitleInfo,
            Quality = parsedEpisodeInfo.Quality,
            ReleaseGroup = parsedEpisodeInfo.ReleaseGroup,
            ReleaseHash = parsedEpisodeInfo.ReleaseHash,
            FullSeason = parsedEpisodeInfo.FullSeason,
            IsPartialSeason = parsedEpisodeInfo.IsPartialSeason,
            IsMultiSeason = parsedEpisodeInfo.IsMultiSeason,
            IsSeasonExtra = parsedEpisodeInfo.IsSeasonExtra,
            SeasonNumber = parsedEpisodeInfo.SeasonNumber,
            SeasonNumbers = parsedEpisodeInfo.SeasonNumbers,
            SeasonPart = parsedEpisodeInfo.SeasonPart,
            AirDate = parsedEpisodeInfo.AirDate,
            GameNumbers = parsedEpisodeInfo.EpisodeNumbers,
            AbsoluteGameNumbers = parsedEpisodeInfo.AbsoluteEpisodeNumbers,
            SpecialAbsoluteGameNumbers = parsedEpisodeInfo.SpecialAbsoluteEpisodeNumbers,
            Languages = parsedEpisodeInfo.Languages,
            ReleaseTokens = parsedEpisodeInfo.ReleaseTokens,
            IsDaily = parsedEpisodeInfo.IsDaily,
            IsAbsoluteNumbering = parsedEpisodeInfo.IsAbsoluteNumbering,
            IsPossibleSpecialGame = parsedEpisodeInfo.IsPossibleSpecialEpisode,
            IsPossibleSceneSeasonSpecial = parsedEpisodeInfo.IsPossibleSceneSeasonSpecial,
            Special = parsedEpisodeInfo.Special,
        };
    }
}
