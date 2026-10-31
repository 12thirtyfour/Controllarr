using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Episodes;

public class RenameGameResource : RestResource
{
    public int SeriesId { get; set; }
    public int SeasonNumber { get; set; }
    public List<int> GameNumbers { get; set; } = [];
    public int RomFileId { get; set; }
    public string? ExistingPath { get; set; }
    public string? NewPath { get; set; }
}

public class RenameEpisodeResource : RenameGameResource
{
}

public static class RenameGameResourceMapper
{
    public static RenameEpisodeResource ToResource(this NzbDrone.Core.MediaFiles.RenameEpisodeFilePreview model)
    {
        return new RenameEpisodeResource
        {
            Id = model.EpisodeFileId,
            SeriesId = model.SeriesId,
            SeasonNumber = model.SeasonNumber,
            GameNumbers = model.EpisodeNumbers.ToList(),
            RomFileId = model.EpisodeFileId,
            ExistingPath = model.ExistingPath,
            NewPath = model.NewPath
        };
    }

    public static List<RenameEpisodeResource> ToResource(this IEnumerable<NzbDrone.Core.MediaFiles.RenameEpisodeFilePreview> models)
    {
        return models.Select(ToResource).ToList();
    }
}
