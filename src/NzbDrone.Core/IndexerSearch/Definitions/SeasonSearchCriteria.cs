using NzbDrone.Core.Games;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class SeasonSearchCriteria : SearchCriteriaBase
    {
        public int SeasonNumber { get; set; }

        public override string ToString()
        {
            var region = RegionHelper.GetRegionName(SeasonNumber);
            return string.Format("[{0} : {1}]", Series.Title, region);
        }
    }
}
