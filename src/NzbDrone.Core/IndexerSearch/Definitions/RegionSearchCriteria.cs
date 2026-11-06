using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class RegionSearchCriteria : SearchCriteriaBase
    {
        public int SeasonNumber { get; set; }
        public bool IsPlatformCollectionSearch { get; set; }

        public override string ToString()
        {
            if (IsPlatformCollectionSearch || SeasonNumber <= 0)
            {
                return string.Format("[{0} : Platform Collection]", Series.Title);
            }

            var region = RegionHelper.GetRegionName(SeasonNumber);
            return string.Format("[{0} : {1}]", Series.Title, region);
        }
    }

    public class SeasonSearchCriteria : RegionSearchCriteria
    {
    }

    public class AnimeSeasonSearchCriteria : SearchCriteriaBase
    {
        public int SeasonNumber { get; set; }
        public List<string> SeasonSceneTitles { get; set; } = [];

        public List<string> AllSeasonSceneTitles => SeasonSceneTitles.Concat(CleanSeasonSceneTitles).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
        public List<string> CleanSeasonSceneTitles => SeasonSceneTitles.Select(GetCleanSceneTitle).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

        public override string ToString()
        {
            var region = RegionHelper.GetRegionName(SeasonNumber);
            return $"[{Series.Title} : {region}]";
        }
    }

    public class DailySeasonSearchCriteria : SearchCriteriaBase
    {
        public int Year { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : {1}]", Series.Title, Year);
        }
    }
}
