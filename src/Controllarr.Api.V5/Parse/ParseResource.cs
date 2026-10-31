using NzbDrone.Core.Languages;
using Sonarr.Api.V5.CustomFormats;
using Sonarr.Api.V5.Episodes;
using Sonarr.Api.V5.Release;
using Sonarr.Api.V5.Series;
using Sonarr.Http.REST;

namespace Sonarr.Api.V5.Parse;

public class ParseResource : RestResource
{
    public string? Title { get; set; }
    public ParsedEpisodeInfoResource? ParsedGameInfo { get; set; }
    public SeriesResource? Series { get; set; }
    public List<EpisodeResource>? Games { get; set; }
    public List<Language>? Languages { get; set; }
    public List<CustomFormatResource>? CustomFormats { get; set; }
    public int CustomFormatScore { get; set; }
}
