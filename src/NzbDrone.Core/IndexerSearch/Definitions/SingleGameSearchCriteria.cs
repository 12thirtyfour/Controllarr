using System;
using System.Linq;
using NzbDrone.Core.Games;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class SingleGameSearchCriteria : SearchCriteriaBase
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

    public class SingleEpisodeSearchCriteria : SingleGameSearchCriteria
    {
    }

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

    public class SpecialEpisodeSearchCriteria : SearchCriteriaBase
    {
        public string[] EpisodeQueryTitles { get; set; }

        public override string ToString()
        {
            var episodeTitles = EpisodeQueryTitles?.ToList();

            if (episodeTitles != null && episodeTitles.Count > 0)
            {
                return $"[{Series.Title}] Unlicensed / Homebrew: {string.Join(", ", episodeTitles)}";
            }

            return $"[{Series.Title}] Unlicensed / Homebrew";
        }
    }

    public class DailyEpisodeSearchCriteria : SearchCriteriaBase
    {
        public DateTime AirDate { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : {1:yyyy-MM-dd}]", Series.Title, AirDate);
        }
    }
}
