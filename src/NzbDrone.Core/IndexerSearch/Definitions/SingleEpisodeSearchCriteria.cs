using System.Linq;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class SingleEpisodeSearchCriteria : SearchCriteriaBase
    {
        public int EpisodeNumber { get; set; }
        public int SeasonNumber { get; set; }

        public override string ToString()
        {
            var gameTitle = Episodes?.FirstOrDefault()?.Title;
            var region = RegionHelper.GetRegionName(SeasonNumber);

            if (!string.IsNullOrWhiteSpace(gameTitle))
            {
                return string.Format("[{0} - {1} ({2})]", Series.Title, gameTitle, region);
            }

            return string.Format("[{0} : {1} #{2}]", Series.Title, region, EpisodeNumber);
        }
    }
}
