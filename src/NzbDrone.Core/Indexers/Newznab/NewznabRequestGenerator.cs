using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Indexers.Newznab
{
    public class NewznabRequestGenerator : IIndexerRequestGenerator
    {
        private readonly Logger _logger;
        private readonly INewznabCapabilitiesProvider _capabilitiesProvider;

        public ProviderDefinition Definition { get; set; }
        public int MaxPages { get; set; }
        public int PageSize { get; set; }
        public NewznabSettings Settings { get; set; }

        public NewznabRequestGenerator(INewznabCapabilitiesProvider capabilitiesProvider)
        {
            _logger = NzbDroneLogger.GetLogger(GetType());
            _capabilitiesProvider = capabilitiesProvider;

            MaxPages = 30;
            PageSize = 100;
        }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var capabilities = _capabilitiesProvider.GetCapabilities(Settings);
            var categories = GetSearchCategories(null);

            pageableRequests.Add(GetPagedRequests(MaxPages, categories, "search", ""));

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SingleEpisodeSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var capabilities = _capabilitiesProvider.GetCapabilities(Settings);
            var categories = GetSearchCategories(searchCriteria);
            var queryTitles = new List<string>();

            if (searchCriteria.Episodes != null)
            {
                foreach (var ep in searchCriteria.Episodes)
                {
                    if (!string.IsNullOrWhiteSpace(ep.Title))
                    {
                        var cleanBase = CleanGameTitleForSearch(ep.Title);
                        if (!string.IsNullOrWhiteSpace(cleanBase))
                        {
                            queryTitles.Add(cleanBase);
                        }

                        var unaccented = ep.Title.RemoveDiacritics();
                        if (!string.IsNullOrWhiteSpace(unaccented))
                        {
                            queryTitles.Add(unaccented);
                        }

                        queryTitles.Add(ep.Title);
                    }
                }
            }

            if (searchCriteria.SceneTitles != null)
            {
                foreach (var sceneTitle in searchCriteria.SceneTitles.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    var cleanBase = CleanGameTitleForSearch(sceneTitle);
                    if (!string.IsNullOrWhiteSpace(cleanBase))
                    {
                        queryTitles.Add(cleanBase);
                    }

                    queryTitles.Add(sceneTitle);
                }
            }

            if (queryTitles.Empty() && searchCriteria.Series != null && !string.IsNullOrWhiteSpace(searchCriteria.Series.Title))
            {
                queryTitles.Add(searchCriteria.Series.Title);
            }

            var distinctTerms = new List<string>();
            foreach (var title in queryTitles)
            {
                var clean = title.Trim();
                if (!string.IsNullOrWhiteSpace(clean) && !distinctTerms.Contains(clean, StringComparer.OrdinalIgnoreCase))
                {
                    distinctTerms.Add(clean);
                }

                var sceneClean = SearchCriteriaBase.GetCleanSceneTitle(clean).Replace("+", " ").Trim();
                if (!string.IsNullOrWhiteSpace(sceneClean) && !distinctTerms.Contains(sceneClean, StringComparer.OrdinalIgnoreCase))
                {
                    distinctTerms.Add(sceneClean);
                }
            }

            foreach (var term in distinctTerms)
            {
                var query = NewsnabifyTitle(term);
                pageableRequests.Add(GetPagedRequests(MaxPages, categories, "search", $"&q={query}"));
            }

            return pageableRequests;
        }

        public virtual IndexerPageableRequestChain GetSearchRequests(SeasonSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var capabilities = _capabilitiesProvider.GetCapabilities(Settings);
            var categories = GetSearchCategories(searchCriteria);
            var terms = searchCriteria.SceneTitles?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

            if (terms.Empty() && searchCriteria.Series != null && !string.IsNullOrWhiteSpace(searchCriteria.Series.Title))
            {
                terms.Add(searchCriteria.Series.Title);
            }

            foreach (var term in terms)
            {
                var query = NewsnabifyTitle(term);
                pageableRequests.Add(GetPagedRequests(MaxPages, categories, "search", $"&q={query}"));
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

        private IEnumerable<IndexerRequest> GetPagedRequests(int maxPages, IEnumerable<int> categories, string searchType, string parameters)
        {
            var catList = categories?.Distinct().ToList() ?? new List<int>();
            var categoriesParam = catList.Any() ? $"&cat={string.Join(",", catList)}" : string.Empty;

            var baseUrl =
                $"{Settings.BaseUrl.TrimEnd('/')}{Settings.ApiPath.TrimEnd('/')}?t={searchType}{categoriesParam}&extended=1{Settings.AdditionalParameters}";

            if (Settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                baseUrl += "&apikey=" + Settings.ApiKey;
            }

            if (PageSize == 0)
            {
                yield return new IndexerRequest($"{baseUrl}{parameters}", HttpAccept.Rss);
            }
            else
            {
                for (var page = 0; page < maxPages; page++)
                {
                    yield return new IndexerRequest($"{baseUrl}&offset={page * PageSize}&limit={PageSize}{parameters}", HttpAccept.Rss);
                }
            }
        }

        private static string NewsnabifyTitle(string title)
        {
            title = title.Replace("+", " ");
            return Uri.EscapeDataString(title);
        }

        private IList<int> GetSearchCategories(SearchCriteriaBase searchCriteria)
        {
            return Settings?.Categories?.Distinct().ToList() ?? new List<int>();
        }

        private static string CleanGameTitleForSearch(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return string.Empty;
            }

            var clean = rawTitle.RemoveDiacritics();
            var parenIdx = clean.IndexOf('(');
            if (parenIdx > 0)
            {
                clean = clean.Substring(0, parenIdx);
            }

            var bracketIdx = clean.IndexOf('[');
            if (bracketIdx > 0)
            {
                clean = clean.Substring(0, bracketIdx);
            }

            return clean.Trim();
        }
    }
}
