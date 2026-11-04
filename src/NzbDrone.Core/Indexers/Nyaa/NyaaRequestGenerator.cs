using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Nyaa
{
    public class NyaaRequestGenerator : IIndexerRequestGenerator
    {
        public NyaaSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests(null));

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SingleEpisodeSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var queryTitles = new List<string>();

            if (searchCriteria.SceneTitles != null)
            {
                queryTitles.AddRange(searchCriteria.SceneTitles);
            }

            if (searchCriteria.Episodes != null)
            {
                queryTitles.AddRange(searchCriteria.Episodes.Select(e => e.Title));
            }

            if (!queryTitles.Any() && searchCriteria.Series != null && !string.IsNullOrWhiteSpace(searchCriteria.Series.Title))
            {
                queryTitles.Add(searchCriteria.Series.Title);
            }

            foreach (var searchTitle in queryTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).Select(PrepareQuery))
            {
                pageableRequests.Add(GetPagedRequests(searchTitle));
            }

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SeasonSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var queryTitles = new List<string>();

            if (searchCriteria.SceneTitles != null)
            {
                queryTitles.AddRange(searchCriteria.SceneTitles);
            }

            if (!queryTitles.Any() && searchCriteria.Series != null && !string.IsNullOrWhiteSpace(searchCriteria.Series.Title))
            {
                queryTitles.Add(searchCriteria.Series.Title);
            }

            foreach (var searchTitle in queryTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).Select(PrepareQuery))
            {
                pageableRequests.Add(GetPagedRequests(searchTitle));
            }

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(DailyEpisodeSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(DailySeasonSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(AnimeEpisodeSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(AnimeSeasonSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SpecialEpisodeSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term)
        {
            var baseUrl = $"{Settings.BaseUrl.TrimEnd('/')}/?page=rss{Settings.AdditionalParameters}";

            if (term != null)
            {
                baseUrl += "&term=" + term;
            }

            yield return new IndexerRequest(baseUrl, HttpAccept.Rss);
        }

        private string PrepareQuery(string query)
        {
            return query.Replace(' ', '+');
        }
    }
}
