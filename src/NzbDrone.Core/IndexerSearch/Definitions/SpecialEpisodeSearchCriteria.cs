using System.Linq;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
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
}
