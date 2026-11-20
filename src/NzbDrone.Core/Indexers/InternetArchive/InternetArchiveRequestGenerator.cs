using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;

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
                foreach (var archiveId in primaryArchiveIds)
                {
                    var url = $"{baseUrl}/metadata/{archiveId}/files";
                    pageableRequests.Add(new[] { new IndexerRequest(new HttpRequest(url, HttpAccept.Json)) });
                }

                pageableRequests.AddTier();
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
                else if (terms.Any())
                {
                    platformParam += $"&_searchTerm={Uri.EscapeDataString(terms[0])}";
                }
            }
            else if (!isPlatformCollection && terms.Any())
            {
                platformParam = $"#_searchTerm={Uri.EscapeDataString(terms[0])}";
            }

            var primaryArchiveIds = Settings?.PrimaryArchiveUrls?
                .Select(InternetArchiveIndexerSettings.ExtractIdentifier)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var baseUrl = Settings?.BaseUrl?.TrimEnd('/') ?? "https://archive.org";

            var pagedTerms = isPlatformCollection
                ? BuildCollectionSearchTermsForArchiveOrg(searchCriteria)
                : ExpandSingleGameSearchTerms(terms);

            var platformLuceneClause = !isPlatformCollection
                ? BuildPlatformLuceneClause(platformSlug, platformName)
                : null;

            // Tier 1: Search preferred archives first (direct file metadata + collection-scoped search)
            if (primaryArchiveIds.Any())
            {
                if (isPlatformCollection)
                {
                    foreach (var archiveId in primaryArchiveIds)
                    {
                        var url = $"{baseUrl}/metadata/{archiveId}/files{platformParam}";
                        pageableRequests.Add(new[] { new IndexerRequest(new HttpRequest(url, HttpAccept.Json)) });
                    }
                }
                else
                {
                    foreach (var term in terms)
                    {
                        foreach (var archiveId in primaryArchiveIds)
                        {
                            var url = $"{baseUrl}/metadata/{archiveId}/files?search={Uri.EscapeDataString(term)}{platformParam}";
                            pageableRequests.Add(new[] { new IndexerRequest(new HttpRequest(url, HttpAccept.Json)) });
                        }
                    }
                }

                if (!searchCriteria.InteractiveSearch)
                {
                    pageableRequests.AddTier();
                }

                foreach (var term in pagedTerms)
                {
                    pageableRequests.Add(GetPagedRequests(term, platformParam, ignorePrimaryArchives: false, isCollectionSearch: isPlatformCollection, maxPagesOverride: 1));
                }

                // For automated searches, advance to a third tier so preferred archives are evaluated first;
                // for interactive searches, keep global requests in the same tier so all results are returned together.
                if (!searchCriteria.InteractiveSearch)
                {
                    pageableRequests.AddTier();
                }
            }

            // Tier 2 (or remainder of Tier 1 when interactive or no preferred archives): Broad global Internet Archive search
            if (isPlatformCollection)
            {
                foreach (var term in pagedTerms)
                {
                    pageableRequests.Add(GetPagedRequests(term, platformParam, ignorePrimaryArchives: true, isCollectionSearch: true, maxPagesOverride: 1));
                }
            }
            else
            {
                foreach (var term in pagedTerms)
                {
                    if (!string.IsNullOrWhiteSpace(platformLuceneClause))
                    {
                        pageableRequests.Add(GetPagedRequests(term, platformParam, ignorePrimaryArchives: true, isCollectionSearch: false, maxPagesOverride: 1, platformLuceneClause: platformLuceneClause));
                    }

                    pageableRequests.Add(GetPagedRequests(term, platformParam, ignorePrimaryArchives: true, isCollectionSearch: false, maxPagesOverride: 1, titleScoped: true));
                }
            }

            return pageableRequests;
        }

        private static List<string> ExpandSingleGameSearchTerms(List<string> rawTerms)
        {
            var result = new List<string>();
            foreach (var term in rawTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                result.Add(term.Trim());
                var withoutVersion = Regex.Replace(term, @"\bVersion\b", string.Empty, RegexOptions.IgnoreCase).Trim();
                withoutVersion = Regex.Replace(withoutVersion, @"\s{2,}", " ").Trim(' ', '-', ':');
                if (!string.IsNullOrWhiteSpace(withoutVersion) &&
                    !string.Equals(withoutVersion, term.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(withoutVersion);
                }
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string BuildPlatformLuceneClause(string platformSlug, string platformName)
        {
            var targetSlug = PlatformCompatibilityValidator.NormalizePlatformSlug(platformSlug, platformName);
            if (string.IsNullOrWhiteSpace(targetSlug))
            {
                return null;
            }

            var clauses = targetSlug switch
            {
                "gba" => new[] { "gba", "game boy advance", "gameboy advance" },
                "gbc" => new[] { "gbc", "game boy color", "gameboy color" },
                "gb" => new[] { "gb", "game boy", "gameboy" },
                "snes" => new[] { "snes", "super nintendo", "super famicom", "sfc" },
                "nes" => new[] { "nes", "nintendo entertainment system", "famicom" },
                "n64" => new[] { "n64", "nintendo 64" },
                "64dd" => new[] { "64dd", "nintendo 64dd" },
                "gamecube" => new[] { "gamecube", "game cube", "gcn", "ngc" },
                "wii" => new[] { "wii", "nintendo wii" },
                "wiiware" => new[] { "wiiware", "wii ware", "nintendo wii digital", "wii virtual console" },
                "wiiu" => new[] { "wii u", "wiiu", "nintendo wii u" },
                "wiiueshop" => new[] { "wii u eshop", "wii u digital", "nintendo wii u digital" },
                "switch" => new[] { "switch", "nintendo switch", "nsw" },
                "switcheshop" => new[] { "switch eshop", "nintendo switch eshop", "nsw eshop" },
                "nds" => new[] { "nds", "nintendo ds" },
                "dsiware" => new[] { "dsiware", "dsi ware", "nintendo dsi", "dsi shop" },
                "3ds" => new[] { "3ds", "nintendo 3ds" },
                "3dseshop" => new[] { "3ds eshop", "nintendo 3ds digital", "3ds digital" },
                "vb" => new[] { "virtual boy", "vb" },
                "fds" => new[] { "famicom disk system", "fds" },
                "genesis" => new[] { "genesis", "sega genesis", "mega drive", "megadrive" },
                "sms" => new[] { "master system", "sega master system", "sms" },
                "gg" => new[] { "game gear", "sega game gear", "gamegear" },
                "segacd" => new[] { "sega cd", "mega cd", "segacd", "megacd" },
                "32x" => new[] { "32x", "sega 32x" },
                "saturn" => new[] { "saturn", "sega saturn" },
                "dreamcast" => new[] { "dreamcast", "sega dreamcast" },
                "ps1" => new[] { "ps1", "psx", "playstation", "sony playstation" },
                "ps2" => new[] { "ps2", "playstation 2", "sony playstation 2" },
                "ps3" => new[] { "ps3", "playstation 3", "sony playstation 3" },
                "psp" => new[] { "psp", "playstation portable", "sony psp" },
                "psvita" => new[] { "ps vita", "psvita", "playstation vita" },
                "xbox" => new[] { "xbox", "microsoft xbox" },
                "xbox360" => new[] { "xbox 360", "xbox360" },
                "tg16" => new[] { "turbografx-16", "turbografx", "pc engine", "pce", "tg16" },
                "tgcd" => new[] { "turbografx-cd", "pc engine cd", "pcecd", "tgcd" },
                "neogeo" => new[] { "neo geo", "neogeo" },
                "ngpc" => new[] { "neo geo pocket", "ngpc", "ngp" },
                "lynx" => new[] { "atari lynx", "lynx" },
                "jaguar" => new[] { "atari jaguar", "jaguar" },
                "atari2600" => new[] { "atari 2600", "a2600" },
                "atari5200" => new[] { "atari 5200", "a5200" },
                "atari7800" => new[] { "atari 7800", "a7800" },
                "wonderswan" => new[] { "wonderswan", "wsc" },
                _ => PlatformCompatibilityValidator.GetAliasesForPlatform(targetSlug).Take(4).ToArray()
            };

            if (clauses.Length == 0)
            {
                return null;
            }

            return $"({string.Join(" OR ", clauses.Select(c => $"\"{c}\""))})";
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

                var strippedPlatform = Regex.Replace(
                    platformName,
                    @"^(?:Nintendo|Sega|Sony|Atari|NEC|SNK|Microsoft|Bandai|Commodore)\s+",
                    string.Empty,
                    RegexOptions.IgnoreCase).Trim();

                if (!string.IsNullOrWhiteSpace(strippedPlatform) &&
                    strippedPlatform.Length >= 4 &&
                    !string.Equals(strippedPlatform, platformName, StringComparison.OrdinalIgnoreCase))
                {
                    terms.Add(strippedPlatform);
                }
            }

            if (!string.IsNullOrWhiteSpace(shortName) &&
                shortName.Length >= 3 &&
                !string.Equals(shortName, platformName, StringComparison.OrdinalIgnoreCase))
            {
                terms.Add(shortName);
            }

            if (terms.Count == 0 && searchCriteria?.SceneTitles != null)
            {
                foreach (var sceneTitle in searchCriteria.SceneTitles.Take(3))
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

        private IEnumerable<IndexerRequest> GetPagedRequests(
            string term,
            string platformParam = "",
            bool ignorePrimaryArchives = false,
            bool isCollectionSearch = false,
            int? maxPagesOverride = null,
            string platformLuceneClause = null,
            bool titleScoped = false)
        {
            var query = BuildQuery(term, ignorePrimaryArchives, isCollectionSearch, platformLuceneClause, titleScoped);
            var baseUrl = Settings.BaseUrl.TrimEnd('/');
            var totalPages = maxPagesOverride ?? MaxPages;

            for (var page = 1; page <= totalPages; page++)
            {
                var url = $"{baseUrl}/advancedsearch.php?q={Uri.EscapeDataString(query)}&fl[]=identifier&fl[]=title&fl[]=mediatype&fl[]=collection&fl[]=subject&fl[]=description&fl[]=date&fl[]=item_size&fl[]=publicdate&fl[]=downloads&fl[]=access-restricted-item&rows={PageSize}&page={page}&output=json{platformParam}";
                yield return new IndexerRequest(new HttpRequest(url, HttpAccept.Json));
            }
        }

        private string BuildQuery(
            string term,
            bool ignorePrimaryArchives = false,
            bool isCollectionSearch = false,
            string platformLuceneClause = null,
            bool titleScoped = false)
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

            var cleanTerm = Regex.Replace(term, @"[\""\[\]\(\)\{\}\@\:\-\+\!\?\~\^\/\\]+", " ");
            cleanTerm = Regex.Replace(cleanTerm, @"\s{2,}", " ").Trim();

            var words = cleanTerm.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Any(char.IsLetterOrDigit) &&
                            !string.Equals(w, "AND", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(w, "OR", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(w, "NOT", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var escapedTerm = cleanTerm.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string termClause;

            if (words.Count == 0)
            {
                return collectionQuery ?? mediaTypeQuery;
            }

            var andWords = string.Join(" AND ", words);
            if (!string.IsNullOrWhiteSpace(platformLuceneClause))
            {
                termClause = words.Count > 1
                    ? $"(title:({andWords}) OR ({andWords})) AND {platformLuceneClause}"
                    : $"(title:\"{escapedTerm}\" OR \"{escapedTerm}\") AND {platformLuceneClause}";
            }
            else if (titleScoped)
            {
                termClause = words.Count > 1
                    ? $"title:({andWords})"
                    : $"title:\"{escapedTerm}\"";
            }
            else
            {
                termClause = words.Count > 1
                    ? $"(\"{escapedTerm}\" OR ({andWords}))"
                    : $"\"{escapedTerm}\"";
            }

            if (collectionQuery != null)
            {
                return $"{collectionQuery} AND {termClause}";
            }

            return $"{mediaTypeQuery} AND {termClause}";
        }
    }
}
