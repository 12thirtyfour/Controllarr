using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.InternetArchive
{
    public class InternetArchiveRequestGenerator : IIndexerRequestGenerator
    {
        public InternetArchiveIndexerSettings Settings { get; set; }
        public int MaxPages { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();
            pageableRequests.Add(GetPagedRequests(null));
            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SingleEpisodeSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SeasonSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(DailyEpisodeSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(DailySeasonSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(AnimeEpisodeSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(AnimeSeasonSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SpecialEpisodeSearchCriteria searchCriteria)
        {
            return GetSearchRequestsForTitles(searchCriteria);
        }

        private IndexerPageableRequestChain GetSearchRequestsForTitles(SearchCriteriaBase searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var titles = searchCriteria.SceneTitles?.Where(t => !t.IsNullOrWhiteSpace()).ToList() ?? new List<string>();

            if (titles.Empty() && searchCriteria.Series?.Title != null)
            {
                titles.Add(searchCriteria.Series.Title);
            }

            foreach (var title in titles)
            {
                pageableRequests.Add(GetPagedRequests(title));
            }

            return pageableRequests;
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term)
        {
            var query = BuildQuery(term);
            var baseUrl = Settings.BaseUrl.TrimEnd('/');

            for (var page = 1; page <= MaxPages; page++)
            {
                var url = $"{baseUrl}/advancedsearch.php?q={Uri.EscapeDataString(query)}&fl[]=identifier&fl[]=title&fl[]=mediatype&fl[]=collection&fl[]=date&fl[]=item_size&fl[]=publicdate&fl[]=downloads&sort[]=publicdate+desc&rows={PageSize}&page={page}&output=json";
                yield return new IndexerRequest(new HttpRequest(url, HttpAccept.Json));
            }
        }

        private string BuildQuery(string term)
        {
            var collections = Settings.Collections?.Where(c => !c.IsNullOrWhiteSpace()).ToList() ?? new List<string>();
            string collectionQuery = null;

            if (collections.Any())
            {
                if (collections.Count == 1)
                {
                    collectionQuery = $"collection:{collections[0]}";
                }
                else
                {
                    collectionQuery = $"({string.Join(" OR ", collections.Select(c => $"collection:{c}"))})";
                }
            }

            if (term.IsNullOrWhiteSpace())
            {
                return collectionQuery ?? "*:*";
            }

            if (collectionQuery != null)
            {
                return $"{collectionQuery} AND ({term})";
            }

            return term;
        }
    }
}
