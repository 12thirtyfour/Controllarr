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
            var primaryArchiveIds = Settings?.PrimaryArchiveUrls?
                .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var baseUrl = Settings?.BaseUrl?.TrimEnd('/') ?? "https://archive.org";

            if (primaryArchiveIds.Any())
            {
                var primaryRequests = new List<IndexerRequest>();
                foreach (var archiveId in primaryArchiveIds)
                {
                    var url = $"{baseUrl}/metadata/{archiveId}/files";
                    primaryRequests.Add(new IndexerRequest(new HttpRequest(url, HttpAccept.Json)));
                }

                if (primaryRequests.Any())
                {
                    pageableRequests.Add(primaryRequests);
                }
            }

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
            var terms = new List<string>();

            if (searchCriteria.Episodes != null && searchCriteria.Episodes.Any())
            {
                foreach (var episode in searchCriteria.Episodes)
                {
                    if (!episode.Title.IsNullOrWhiteSpace())
                    {
                        var cleanTitle = CleanGameTitleForSearch(episode.Title);
                        if (!terms.Contains(cleanTitle))
                        {
                            terms.Add(cleanTitle);
                        }
                    }
                }
            }

            if (terms.Empty())
            {
                var titles = searchCriteria.SceneTitles?.Where(t => !t.IsNullOrWhiteSpace()).ToList() ?? new List<string>();

                if (titles.Empty() && searchCriteria.Series?.Title != null)
                {
                    titles.Add(searchCriteria.Series.Title);
                }

                terms.AddRange(titles);
            }

            if (searchCriteria.Series != null && !searchCriteria.Series.Title.IsNullOrWhiteSpace())
            {
                var cleanSeriesTitle = CleanGameTitleForSearch(searchCriteria.Series.Title);
                if (!terms.Contains(cleanSeriesTitle))
                {
                    terms.Add(cleanSeriesTitle);
                }
            }

            var primaryArchiveIds = Settings?.PrimaryArchiveUrls?
                .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var baseUrl = Settings?.BaseUrl?.TrimEnd('/') ?? "https://archive.org";

            // Prioritize searching files directly inside Primary Archive URLs
            if (primaryArchiveIds.Any())
            {
                var primaryRequests = new List<IndexerRequest>();
                foreach (var term in terms.Distinct())
                {
                    foreach (var archiveId in primaryArchiveIds)
                    {
                        var url = $"{baseUrl}/metadata/{archiveId}/files?search={Uri.EscapeDataString(term)}";
                        primaryRequests.Add(new IndexerRequest(new HttpRequest(url, HttpAccept.Json)));
                    }
                }

                if (primaryRequests.Any())
                {
                    pageableRequests.Add(primaryRequests);
                }
            }

            foreach (var term in terms.Distinct())
            {
                pageableRequests.Add(GetPagedRequests(term));
            }

            return pageableRequests;
        }

        private static string CleanGameTitleForSearch(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return string.Empty;
            }

            var clean = rawTitle;
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

        private IEnumerable<IndexerRequest> GetPagedRequests(string term)
        {
            var query = BuildQuery(term);
            var baseUrl = Settings.BaseUrl.TrimEnd('/');

            for (var page = 1; page <= MaxPages; page++)
            {
                var url = $"{baseUrl}/advancedsearch.php?q={Uri.EscapeDataString(query)}&fl[]=identifier&fl[]=title&fl[]=mediatype&fl[]=collection&fl[]=date&fl[]=item_size&fl[]=publicdate&fl[]=downloads&fl[]=access-restricted-item&sort[]=publicdate+desc&rows={PageSize}&page={page}&output=json";
                yield return new IndexerRequest(new HttpRequest(url, HttpAccept.Json));
            }
        }

        private string BuildQuery(string term)
        {
            var collections = Settings?.PrimaryArchiveUrls?
                .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                .Where(c => !c.IsNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            string collectionQuery = null;

            if (collections.Any())
            {
                if (collections.Count == 1)
                {
                    collectionQuery = $"(collection:{collections[0]} OR identifier:{collections[0]})";
                }
                else
                {
                    collectionQuery = $"({string.Join(" OR ", collections.Select(c => $"(collection:{c} OR identifier:{c})"))})";
                }
            }

            if (term.IsNullOrWhiteSpace())
            {
                return collectionQuery ?? "mediatype:software";
            }

            var escapedTerm = term.Replace("\"", "\\\"");

            if (collectionQuery != null)
            {
                return $"({collectionQuery} OR mediatype:software) AND (\"{escapedTerm}\" OR {term})";
            }

            return $"mediatype:software AND (\"{escapedTerm}\" OR {term})";
        }
    }
}
