using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Tv;
using Sonarr.Api.V5.CustomFormats;
using Sonarr.Api.V5.Platforms;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Release;

public class ReleaseResource : RestResource
{
    public ParsedEpisodeInfoResource? ParsedInfo { get; set; }
    public ReleaseInfoResource? Release { get; set; }
    public ReleaseDecisionResource? Decision { get; set; }
    public ReleaseHistoryResource? History { get; set; }
    public int QualityWeight { get; set; }
    public string? Format { get; set; }
    public List<Language> Languages { get; set; } = [];
    public int? MappedSeasonNumber { get; set; }
    public int[] MappedGameNumbers { get; set; } = [];
    public int[] MappedAbsoluteGameNumbers { get; set; } = [];
    public int? MappedSeriesId { get; set; }
    public IEnumerable<ReleaseEpisodeResource> MappedGameInfo { get; set; } = [];
    public bool GameRequested { get; set; }
    public bool DownloadAllowed { get; set; }
    public int ReleaseWeight { get; set; }
    public List<CustomFormatResource>? CustomFormats { get; set; }
    public int CustomFormatScore { get; set; }
}

public static class ReleaseResourceMapper
{
    public static ReleaseResource ToResource(this DownloadDecision model)
    {
        var releaseInfo = model.RemoteEpisode?.Release;
        var parsedEpisodeInfo = model.RemoteEpisode?.ParsedEpisodeInfo;
        var remoteEpisode = model.RemoteEpisode;

        var format = releaseInfo?.ParsedRomInfo?.Format ??
                     releaseInfo?.Container ??
                     (!string.IsNullOrWhiteSpace(releaseInfo?.Title) ? Path.GetExtension(releaseInfo.Title)?.TrimStart('.').ToUpperInvariant() : null);

        var episodes = remoteEpisode?.Episodes ?? new List<Episode>();

        return new ReleaseResource
        {
            Format = format?.ToUpperInvariant(),
            ParsedInfo = parsedEpisodeInfo?.ToResource(),
            Release = releaseInfo?.ToResource(),
            Decision = new ReleaseDecisionResource(model),

            Languages = remoteEpisode?.Languages ?? new List<Language>(),
            MappedSeriesId = remoteEpisode?.Series?.Id,
            MappedSeasonNumber = episodes.FirstOrDefault()?.SeasonNumber,
            MappedGameNumbers = episodes.Select(v => v.EpisodeNumber).ToArray(),
            MappedAbsoluteGameNumbers = episodes.Where(v => v.AbsoluteEpisodeNumber.HasValue).Select(v => v.AbsoluteEpisodeNumber!.Value).ToArray(),
            MappedGameInfo = episodes.Select(v => new ReleaseEpisodeResource(v)),
            GameRequested = remoteEpisode?.EpisodeRequested ?? false,
            DownloadAllowed = (remoteEpisode?.DownloadAllowed ?? false) && (releaseInfo != null && !releaseInfo.MissingCredentials),
            CustomFormatScore = remoteEpisode?.CustomFormatScore ?? 0,
            CustomFormats = remoteEpisode?.CustomFormats?.ToResource(false),
        };
    }

    public static ReleaseResource MapDecision(this DownloadDecision decision, int initialWeight, QualityProfile profile)
    {
        var release = decision.ToResource();

        release.ReleaseWeight = initialWeight;

        if (release.ParsedInfo?.Quality == null)
        {
            release.QualityWeight = 0;
        }
        else
        {
            var qualityId = release.ParsedInfo.Quality.Quality?.Id ?? 0;
            var index = profile?.GetIndex(release.ParsedInfo.Quality.Quality).Index ?? qualityId;
            release.QualityWeight = index * 100;
            release.QualityWeight += release.ParsedInfo.Quality.Revision.Real * 10;
            release.QualityWeight += release.ParsedInfo.Quality.Revision.Version;
        }

        return release;
    }
}

public class ReleaseEpisodeResource
{
    public int Id { get; set; }
    public int RomFileId { get; set; }
    public int SeasonNumber { get; set; }
    public int GameNumber { get; set; }
    public int? AbsoluteGameNumber { get; set; }
    public string? Title { get; set; }

    public ReleaseEpisodeResource()
    {
    }

    public ReleaseEpisodeResource(Episode episode)
    {
        Id = episode.Id;
        RomFileId = episode.EpisodeFileId;
        SeasonNumber = episode.SeasonNumber;
        GameNumber = episode.EpisodeNumber;
        AbsoluteGameNumber = episode.AbsoluteEpisodeNumber;
        Title = episode.Title;
    }
}
