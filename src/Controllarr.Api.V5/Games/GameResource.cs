using System.Text.Json.Serialization;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Tv;
using Sonarr.Api.V5.Platforms;
using Sonarr.Api.V5.RomFiles;
using Sonarr.Http.REST;
using Swashbuckle.AspNetCore.Annotations;

namespace Sonarr.Api.V5.Games
{
    public class GameResource : RestResource
    {
        public int SeriesId { get; set; }
        public int TvdbId { get; set; }
        public int RomFileId { get; set; }
        public int SeasonNumber { get; set; }
        public int PlatformId { get => SeriesId; set => SeriesId = value; }
        public int EpisodeFileId { get => RomFileId; set => RomFileId = value; }
        public int RegionNumber { get => SeasonNumber; set => SeasonNumber = value; }
        public int EpisodeNumber { get => GameNumber; set => GameNumber = value; }
        public int GameNumber { get; set; }
        public string? Title { get; set; }
        public string? AirDate { get; set; }
        public string? ReleaseDate { get => AirDate; set => AirDate = value; }
        public DateTime? AirDateUtc { get; set; }
        public DateTime? ReleaseDateUtc { get => AirDateUtc; set => AirDateUtc = value; }
        public int? AbsoluteEpisodeNumber { get => AbsoluteGameNumber; set => AbsoluteGameNumber = value; }
        public DateTime? LastSearchTime { get; set; }
        public int Runtime { get; set; }
        public string? FinaleType { get; set; }
        public string? Overview { get; set; }
        public EpisodeFileResource? RomFile { get; set; }
        public bool HasFile { get; set; }
        public bool Monitored { get; set; }
        public int? AbsoluteGameNumber { get; set; }
        public int? SceneAbsoluteGameNumber { get; set; }
        public int? SceneGameNumber { get; set; }
        public int? SceneSeasonNumber { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? GrabDate { get; set; }
        public SeriesResource? Series { get; set; }
        public List<MediaCover>? Images { get; set; }

        // Hiding this so people don't think its usable (only used to set the initial state)
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [SwaggerIgnore]
        public bool Grabbed { get; set; }
    }

    public class EpisodeResource : GameResource
    {
    }

    public static class GameResourceMapper
    {
        public static EpisodeResource ToResource(this Episode model)
        {
            return new EpisodeResource
            {
                Id = model.Id,

                SeriesId = model.SeriesId,
                TvdbId = model.TvdbId,
                RomFileId = model.EpisodeFileId,
                SeasonNumber = model.SeasonNumber,
                GameNumber = model.EpisodeNumber,
                Title = model.Title,
                AirDate = model.AirDate,
                AirDateUtc = model.AirDateUtc,
                Runtime = model.Runtime,
                FinaleType = model.FinaleType,
                Overview = model.Overview,
                LastSearchTime = model.LastSearchTime,

                // RomFile

                HasFile = model.HasFile,
                Monitored = model.Monitored,
                AbsoluteGameNumber = model.AbsoluteEpisodeNumber,
                SceneAbsoluteGameNumber = model.SceneAbsoluteEpisodeNumber,
                SceneGameNumber = model.SceneEpisodeNumber,
                SceneSeasonNumber = model.SceneSeasonNumber,

                // Series = model.Series.MapToResource(),
            };
        }

        public static List<EpisodeResource> ToResource(this IEnumerable<Episode> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
