using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.FileList
{
    public class FileListRequestGenerator : IIndexerRequestGenerator
    {
        public FileListSettings Settings { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetRequest("latest-torrents", Settings.Categories, ""));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(SingleEpisodeSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            if (searchCriteria.SearchMode.HasFlag(SearchMode.SearchID) || searchCriteria.SearchMode == SearchMode.Default)
            {
                AddImdbRequests(pageableRequests, searchCriteria, "search-torrents", Settings.Categories, string.Empty);
            }

            if (searchCriteria.SearchMode.HasFlag(SearchMode.SearchTitle))
            {
                AddNameRequests(pageableRequests, searchCriteria, "search-torrents", Settings.Categories, string.Empty);
            }

            pageableRequests.AddTier();

            if (searchCriteria.SearchMode == SearchMode.Default)
            {
                AddNameRequests(pageableRequests, searchCriteria, "search-torrents", Settings.Categories, string.Empty);
            }

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(SeasonSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            if (searchCriteria.SearchMode.HasFlag(SearchMode.SearchID) || searchCriteria.SearchMode == SearchMode.Default)
            {
                AddImdbRequests(pageableRequests, searchCriteria, "search-torrents", Settings.Categories, string.Empty);
            }

            if (searchCriteria.SearchMode.HasFlag(SearchMode.SearchTitle))
            {
                AddNameRequests(pageableRequests, searchCriteria, "search-torrents", Settings.Categories, string.Empty);
            }

            pageableRequests.AddTier();

            if (searchCriteria.SearchMode == SearchMode.Default)
            {
                AddNameRequests(pageableRequests, searchCriteria, "search-torrents", Settings.Categories, string.Empty);
            }

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(DailyEpisodeSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(DailySeasonSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(AnimeEpisodeSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(AnimeSeasonSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(SpecialEpisodeSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        private void AddImdbRequests(IndexerPageableRequestChain chain, SearchCriteriaBase searchCriteria, string searchType, IEnumerable<int> categories, string parameters)
        {
            if (searchCriteria.Series.ImdbId.IsNotNullOrWhiteSpace())
            {
                chain.Add(GetRequest(searchType, categories, string.Format("&type=imdb&query={0}{1}", searchCriteria.Series.ImdbId, parameters)));
            }
        }

        private void AddNameRequests(IndexerPageableRequestChain chain, SearchCriteriaBase searchCriteria, string searchType, IEnumerable<int> categories, string parameters)
        {
            var queryTitles = new List<string>();
            if (searchCriteria.SceneTitles != null)
            {
                queryTitles.AddRange(searchCriteria.SceneTitles);
            }

            if (searchCriteria.Series != null && !string.IsNullOrWhiteSpace(searchCriteria.Series.Title))
            {
                queryTitles.Add(searchCriteria.Series.Title);
            }

            foreach (var sceneTitle in queryTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(GetRequest(searchType, categories, string.Format("&type=name&query={0}{1}", Uri.EscapeDataString(sceneTitle.Trim()), parameters)));
            }
        }

        private IEnumerable<IndexerRequest> GetRequest(string searchType, IEnumerable<int> categories, string parameters)
        {
            var catList = categories?.Distinct().ToList() ?? new List<int>();
            var categoriesParam = catList.Any() ? $"&category={string.Join(",", catList)}" : string.Empty;

            var baseUrl = string.Format("{0}/api.php?action={1}{2}{3}", Settings.BaseUrl.TrimEnd('/'), searchType, categoriesParam, parameters);

            var request = new IndexerRequest(baseUrl, HttpAccept.Json);
            request.HttpRequest.Credentials = new BasicNetworkCredential(Settings.Username.Trim(), Settings.Passkey.Trim());

            yield return request;
        }
    }
}
