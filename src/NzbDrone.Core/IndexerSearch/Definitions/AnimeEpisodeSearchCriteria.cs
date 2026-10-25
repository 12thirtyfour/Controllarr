using System.Linq;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class AnimeEpisodeSearchCriteria : SearchCriteriaBase
    {
        public int AbsoluteEpisodeNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public int SeasonNumber { get; set; }
        public bool IsSeasonSearch { get; set; }

        public override string ToString()
        {
            var gameTitle = Episodes?.FirstOrDefault()?.Title;
            var region = RegionHelper.GetRegionName(SeasonNumber);

            if (!string.IsNullOrWhiteSpace(gameTitle))
            {
                return $"[{Series.Title} - {gameTitle} ({region})]";
            }

            return $"[{Series.Title} : {region} #{EpisodeNumber}]";
        }
    }
}
