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
                    pageableRequests.AddTier(primaryRequests);
                }
            }

            pageableRequests.AddTier(GetPagedRequests(null));
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
            var isPlatformCollection = searchCriteria is SeasonSearchCriteria { IsPlatformCollectionSearch: true };

            if (!isPlatformCollection && searchCriteria.Episodes != null && searchCriteria.Episodes.Any())
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

            if (terms.Empty() && searchCriteria.Series != null && !searchCriteria.Series.Title.IsNullOrWhiteSpace())
            {
                var cleanSeriesTitle = CleanGameTitleForSearch(searchCriteria.Series.Title);
                if (!terms.Contains(cleanSeriesTitle))
                {
                    terms.Add(cleanSeriesTitle);
                }
            }

            var platformSlug = searchCriteria.Series?.TitleSlug;
            var platformName = searchCriteria.Series?.Title;
            var platformParam = string.Empty;
            if (!string.IsNullOrWhiteSpace(platformSlug))
            {
                platformParam = $"#_platform={Uri.EscapeDataString(platformSlug)}";
                if (!string.IsNullOrWhiteSpace(platformName))
                {
                    platformParam += $"&_platformName={Uri.EscapeDataString(platformName)}";
                }

                if (isPlatformCollection)
                {
                    platformParam += "&_collection=true";
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
                if (isPlatformCollection)
                {
                    foreach (var archiveId in primaryArchiveIds)
                    {
                        var url = $"{baseUrl}/metadata/{archiveId}/files{platformParam}";
                        primaryRequests.Add(new IndexerRequest(new HttpRequest(url, HttpAccept.Json)));
                    }
                }
                else
                {
                    foreach (var term in terms.Distinct())
                    {
                        foreach (var archiveId in primaryArchiveIds)
                        {
                            var url = $"{baseUrl}/metadata/{archiveId}/files?search={Uri.EscapeDataString(term)}{platformParam}";
                            primaryRequests.Add(new IndexerRequest(new HttpRequest(url, HttpAccept.Json)));
                        }
                    }
                }

                if (primaryRequests.Any() && !searchCriteria.InteractiveSearch)
                {
                    pageableRequests.AddTier(primaryRequests);
                }
            }

            var pagedTerms = isPlatformCollection
                ? BuildCollectionSearchTermsForArchiveOrg(searchCriteria)
                : terms.Distinct().ToList();

            var primaryPagedRequests = new List<IndexerRequest>();
            if (searchCriteria.InteractiveSearch && primaryArchiveIds.Any())
            {
                foreach (var archiveId in primaryArchiveIds)
                {
                    if (isPlatformCollection)
                    {
                        var url = $"{baseUrl}/metadata/{archiveId}/files{platformParam}";
                        primaryPagedRequests.Add(new IndexerRequest(new HttpRequest(url, HttpAccept.Json)));
                    }
                    else
                    {
                        foreach (var term in terms.Distinct())
                        {
                            var url = $"{baseUrl}/metadata/{archiveId}/files?search={Uri.EscapeDataString(term)}{platformParam}";
                            primaryPagedRequests.Add(new IndexerRequest(new HttpRequest(url, HttpAccept.Json)));
                        }
                    }
                }
            }

            foreach (var term in pagedTerms)
            {
                primaryPagedRequests.AddRange(GetPagedRequests(term, platformParam, isCollectionSearch: isPlatformCollection));
            }

            // In interactive search, include global IA search alongside primary archive search so users see all available results
            if (searchCriteria.InteractiveSearch && primaryArchiveIds.Any())
            {
                foreach (var term in pagedTerms)
                {
                    primaryPagedRequests.AddRange(GetPagedRequests(term, platformParam, ignorePrimaryArchives: true, isCollectionSearch: isPlatformCollection));
                }
            }

            if (primaryPagedRequests.Any())
            {
                pageableRequests.AddTier(primaryPagedRequests);
            }

            // If primary archives were configured, provide a global Internet Archive fallback tier when preferred archives return 0 hits
            if (primaryArchiveIds.Any() && !searchCriteria.InteractiveSearch)
            {
                var broadRequests = new List<IndexerRequest>();
                foreach (var term in pagedTerms)
                {
                    broadRequests.AddRange(GetPagedRequests(term, platformParam, ignorePrimaryArchives: true, isCollectionSearch: isPlatformCollection));
                }

                if (broadRequests.Any())
                {
                    pageableRequests.AddTier(broadRequests);
                }
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

        private static List<string> BuildCollectionSearchTermsForArchiveOrg(SearchCriteriaBase searchCriteria)
        {
            var terms = new List<string>();
            var platformName = searchCriteria?.Series?.Title?.Trim();
            var shortName = searchCriteria?.Series?.TitleSlug?.Trim();

            if (!string.IsNullOrWhiteSpace(platformName))
            {
                terms.Add(platformName);
                terms.Add($"{platformName} collection");
                terms.Add($"{platformName} romset");
                terms.Add($"{platformName} no-intro");
                terms.Add($"{platformName} redump");
            }

            if (!string.IsNullOrWhiteSpace(shortName) &&
                !string.Equals(shortName, platformName, StringComparison.OrdinalIgnoreCase))
            {
                terms.Add(shortName);
                terms.Add($"{shortName} collection");
            }

            if (terms.Count == 0 && searchCriteria?.SceneTitles != null)
            {
                foreach (var sceneTitle in searchCriteria.SceneTitles.Take(5))
                {
                    var sanitized = sceneTitle.Trim().TrimStart('@', '[');
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        terms.Add(sanitized);
                    }
                }
            }

            return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term, string platformParam = "", bool ignorePrimaryArchives = false, bool isCollectionSearch = false)
        {
            var query = BuildQuery(term, ignorePrimaryArchives, isCollectionSearch);
            var baseUrl = Settings.BaseUrl.TrimEnd('/');

            for (var page = 1; page <= MaxPages; page++)
            {
                var url = $"{baseUrl}/advancedsearch.php?q={Uri.EscapeDataString(query)}&fl[]=identifier&fl[]=title&fl[]=mediatype&fl[]=collection&fl[]=date&fl[]=item_size&fl[]=publicdate&fl[]=downloads&fl[]=access-restricted-item&rows={PageSize}&page={page}&output=json{platformParam}";
                yield return new IndexerRequest(new HttpRequest(url, HttpAccept.Json));
            }
        }

        private string BuildQuery(string term, bool ignorePrimaryArchives = false, bool isCollectionSearch = false)
        {
            var collections = !ignorePrimaryArchives
                ? Settings?.PrimaryArchiveUrls?
                    .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                    .Where(c => !c.IsNullOrWhiteSpace())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>()
                : new List<string>();
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

            var mediaTypeQuery = isCollectionSearch
                ? "(mediatype:software OR mediatype:data OR mediatype:collection)"
                : "(mediatype:software OR mediatype:data)";

            if (term.IsNullOrWhiteSpace())
            {
                return collectionQuery ?? mediaTypeQuery;
            }

            var cleanTerm = term.Replace("\"", string.Empty)
                .Replace("[", " ")
                .Replace("]", " ")
                .Replace("@", " ")
                .Trim();

            var words = cleanTerm.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !string.Equals(w, "AND", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(w, "OR", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(w, "NOT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var escapedTerm = cleanTerm.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var termClause = words.Count > 1
                ? $"(\"{escapedTerm}\" OR ({string.Join(" AND ", words)}))"
                : $"\"{escapedTerm}\"";

            if (collectionQuery != null)
            {
                return $"{collectionQuery} AND {termClause}";
            }

            var fallbackCollections = Settings?.Collections?.Where(c => !c.IsNullOrWhiteSpace()).ToList();
            if (fallbackCollections != null && fallbackCollections.Any())
            {
                var collectionPart = string.Join(" OR ", fallbackCollections.Select(c => $"collection:{c}"));
                return $"({mediaTypeQuery} OR {collectionPart}) AND {termClause}";
            }

            return $"{mediaTypeQuery} AND {termClause}";
        }
    }
}
