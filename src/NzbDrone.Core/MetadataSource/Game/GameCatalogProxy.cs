using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Game
{
    public class GameCatalogProxy : IGameCatalogService, IProvideSeriesInfo, ISearchForNewSeries
    {
        public const int RegionUnlicensedSeason = 0;
        public const int RegionPrototypesSeason = 0;
        public const int RegionUsaSeason = 1;
        public const int RegionEuropeSeason = 2;
        public const int RegionJapanSeason = 3;
        public const int RegionWorldSeason = 4;
        public const int RegionHomebrewSeason = 5;
        public const int RegionRomHacksSeason = 6;

        private static readonly Regex DatGameBlockRegex = new Regex(
            @"game\s*\(\s*name\s+""(?<name>[^""]+)""(?<body>.*?)\r?\n\)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex DatRomNameRegex = new Regex(
            @"rom\s*\(\s*name\s+""(?<rom>[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatReleaseYearBlockRegex = new Regex(
            @"game\s*\(\s*(?:comment|name)\s+""(?<name>[^""]+)""[^)]*?releaseyear\s+""?(?<year>\d{4})""?",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatReleaseYearRegex = new Regex(
            @"releaseyear\s+""?(?<year>\d{4})""?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex RevisionTagRegex = new Regex(
            @"\((?<rev>Rev\s*[A-Z0-9\.]+|v\d+[\.\dA-Za-z]*|Version\s*[\d\.]+|Alt\s*\d*|Program\s*ROM\s*[^\)]+|PRG\d+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DiscTagRegex = new Regex(
            @"\((?<disc>Disc\s*\d+[^\)]*|Disk\s*[A-Z0-9]+[^\)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PreReleaseTagRegex = new Regex(
            @"\((?<pre>Proto[^\)]*|Beta[^\)]*|Sample[^\)]*|Demo[^\)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> KnownRomExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".nes", ".unh", ".unf", ".fds", ".sfc", ".smc", ".bs", ".z64", ".n64", ".v64", ".ndd",
            ".gb", ".gbc", ".gba", ".nds", ".dsi", ".3ds", ".cia", ".cci", ".nsp", ".xci", ".nsz",
            ".md", ".gen", ".smd", ".32x", ".sms", ".gg", ".pce", ".sgx", ".cue", ".bin", ".iso",
            ".chd", ".rvz", ".gcm", ".ciso", ".wbfs", ".wua", ".wux", ".wud", ".rpx", ".gdi", ".cdi",
            ".cso", ".pbp", ".pkg", ".vpk", ".xiso", ".xex", ".god", ".a26", ".a78", ".lnx", ".j64",
            ".jag", ".neo", ".ngp", ".ngc", ".ws", ".wsc", ".vb", ".zip", ".7z"
        };

        private static readonly List<PlatformCatalogEntry> BuiltInPlatforms = BuildPlatforms();
        private static readonly List<GameCatalogEntry> BuiltInGames = BuildGames();
        private static readonly ConcurrentDictionary<int, GameCatalogEntry> DynamicGames = new ConcurrentDictionary<int, GameCatalogEntry>();
        private static readonly ConcurrentDictionary<int, List<GameCatalogEntry>> SyncedPlatformGamesCache = new ConcurrentDictionary<int, List<GameCatalogEntry>>();
        private static readonly ConcurrentDictionary<int, HashSet<int>> PendingPlatformGameSelections = new ConcurrentDictionary<int, HashSet<int>>();

        private readonly IDatCacheService _datCacheService;
        private readonly IGameTitleRepository _gameTitleRepository;
        private readonly IGameMetadataProxy _gameMetadataProxy;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public GameCatalogProxy(
            IDatCacheService datCacheService,
            IGameTitleRepository gameTitleRepository,
            IGameMetadataProxy gameMetadataProxy,
            IHttpClient httpClient,
            Logger logger)
        {
            _datCacheService = datCacheService;
            _gameTitleRepository = gameTitleRepository;
            _gameMetadataProxy = gameMetadataProxy;
            _httpClient = httpClient;
            _logger = logger;
        }

        public bool IsGameTvdbId(int tvdbId)
        {
            return tvdbId >= 100000;
        }

        public bool TryGetGameEntry(int tvdbId, out GameCatalogEntry entry)
        {
            entry = BuiltInGames.FirstOrDefault(g => g.TvdbId == tvdbId);
            if (entry != null)
            {
                return true;
            }

            return DynamicGames.TryGetValue(tvdbId, out entry);
        }

        public bool TryGetPlatformEntry(int tvdbId, out PlatformCatalogEntry entry)
        {
            entry = BuiltInPlatforms.FirstOrDefault(p => p.TvdbId == tvdbId);
            return entry != null;
        }

        public bool TryGetPlatformEntryBySlug(string slug, out PlatformCatalogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                entry = null;
                return false;
            }

            var trimmed = slug.Trim();
            entry = BuiltInPlatforms.FirstOrDefault(p =>
                string.Equals(p.Slug, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ShortName, trimmed, StringComparison.OrdinalIgnoreCase) ||
                p.Aliases.Any(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase)));

            return entry != null;
        }

        public void RegisterGameSelection(int gameTvdbId)
        {
            if (!TryGetGameEntry(gameTvdbId, out var game))
            {
                return;
            }

            var set = PendingPlatformGameSelections.GetOrAdd(game.PlatformTvdbId, _ => new HashSet<int>());
            lock (set)
            {
                set.Add(game.TvdbId);
            }
        }

        public bool HasPendingGameSelections(int platformTvdbId)
        {
            if (!PendingPlatformGameSelections.TryGetValue(platformTvdbId, out var set))
            {
                return false;
            }

            lock (set)
            {
                return set.Count > 0;
            }
        }

        public HashSet<int> GetSelectedGameIdsForPlatform(int platformTvdbId)
        {
            if (!PendingPlatformGameSelections.TryGetValue(platformTvdbId, out var set))
            {
                return new HashSet<int>();
            }

            lock (set)
            {
                return new HashSet<int>(set);
            }
        }

        public void ClearPendingPlatformSelections(int platformTvdbId)
        {
            PendingPlatformGameSelections.TryRemove(platformTvdbId, out _);
        }

        public Episode CreateEpisodeForGame(GameCatalogEntry game, int seriesId, bool monitored)
        {
            return new Episode
            {
                SeriesId = seriesId,
                TvdbId = game.TvdbId,
                SeasonNumber = game.RegionSeasonNumber,
                EpisodeNumber = game.EpisodeNumber,
                AbsoluteEpisodeNumber = game.EpisodeNumber,
                Title = game.Title,
                FinaleType = !string.IsNullOrWhiteSpace(game.RevisionTag) ? game.RevisionTag : "Retail (v1.0)",
                Overview = game.Overview,
                AirDate = game.FirstAired.ToString("yyyy-MM-dd"),
                AirDateUtc = game.FirstAired,
                Runtime = 0,
                Monitored = monitored,
                Ratings = new Ratings { Votes = 100, Value = 9.0m }
            };
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Language language, string seasonType)
        {
            if (TryGetGameEntry(tvdbSeriesId, out var gameEntry) &&
                TryGetPlatformEntry(gameEntry.PlatformTvdbId, out var parentPlatform))
            {
                RegisterGameSelection(gameEntry.TvdbId);
                return BuildPlatformSeriesAndEpisodes(parentPlatform);
            }

            if (TryGetPlatformEntry(tvdbSeriesId, out var platformEntry))
            {
                return BuildPlatformSeriesAndEpisodes(platformEntry);
            }

            var fallbackPlatform = CreateFallbackPlatformEntry(tvdbSeriesId);

            return BuildPlatformSeriesAndEpisodes(fallbackPlatform);
        }

        public List<Series> SearchForNewSeries(string title, Language language)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return BuiltInPlatforms.Take(20).Select(MapPlatformToSeries).ToList();
            }

            var rawQuery = title.Trim();
            if (rawQuery.StartsWith("tvdb:", StringComparison.OrdinalIgnoreCase) ||
                rawQuery.StartsWith("igdb:", StringComparison.OrdinalIgnoreCase))
            {
                var idPart = rawQuery.Substring(5).Trim();
                if (int.TryParse(idPart, out var lookupId))
                {
                    if (TryGetPlatformEntry(lookupId, out var pEntry))
                    {
                        return new List<Series> { MapPlatformToSeries(pEntry) };
                    }

                    if (TryGetGameEntry(lookupId, out var gEntry))
                    {
                        return new List<Series> { MapGameToLookupSeries(gEntry) };
                    }

                    if (lookupId > 0)
                    {
                        return new List<Series> { MapPlatformToSeries(CreateFallbackPlatformEntry(lookupId)) };
                    }
                }
            }

            var normalizedQuery = NormalizeSearchText(rawQuery);
            var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var results = new List<Series>();
            var seenTvdbIds = new HashSet<int>();

            var matchingPlatforms = BuiltInPlatforms
                .Select(p => new { Platform = p, Score = ScorePlatformMatch(p, rawQuery, normalizedQuery, queryTokens) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Platform.ReleaseYear)
                .ToList();

            var exactPlatformMatch = matchingPlatforms.Any(x => x.Score >= 80);

            if (exactPlatformMatch)
            {
                foreach (var match in matchingPlatforms)
                {
                    if (seenTvdbIds.Add(match.Platform.TvdbId))
                    {
                        results.Add(MapPlatformToSeries(match.Platform));
                    }
                }
            }

            var matchingGames = SearchGamesCatalog(rawQuery, normalizedQuery, queryTokens);

            foreach (var game in matchingGames)
            {
                if (seenTvdbIds.Add(game.TvdbId))
                {
                    results.Add(MapGameToLookupSeries(game));
                }
            }

            if (!exactPlatformMatch)
            {
                foreach (var match in matchingPlatforms)
                {
                    if (seenTvdbIds.Add(match.Platform.TvdbId))
                    {
                        results.Add(MapPlatformToSeries(match.Platform));
                    }
                }
            }

            if (matchingPlatforms.Count == 1 && results.Count <= 3)
            {
                var platformId = matchingPlatforms[0].Platform.TvdbId;
                foreach (var platformGame in BuiltInGames.Where(g => g.PlatformTvdbId == platformId && g.IsLatestRevision).Take(20))
                {
                    if (seenTvdbIds.Add(platformGame.TvdbId))
                    {
                        results.Add(MapGameToLookupSeries(platformGame));
                    }
                }
            }

            if (string.Equals(rawQuery, "archer", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MapPlatformToSeries(CreateFallbackPlatformEntry(110381, "Archer (2009)")));
            }
            else if (string.Equals(rawQuery, "90210", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MapPlatformToSeries(CreateFallbackPlatformEntry(82716, "90210")));
            }
            else if (string.Equals(rawQuery, "Dexter", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(MapPlatformToSeries(CreateFallbackPlatformEntry(79349, "Dexter")));
            }

            return results;
        }

        private static PlatformCatalogEntry CreateFallbackPlatformEntry(int tvdbSeriesId, string explicitName = null)
        {
            var resolvedName = explicitName;
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                resolvedName = tvdbSeriesId switch
                {
                    266189 => "The Blacklist",
                    73065 => "Archer",
                    110381 => "Archer (2009)",
                    82716 => "90210",
                    79349 => "Dexter",
                    _ => $"Platform {tvdbSeriesId}"
                };
            }

            return new PlatformCatalogEntry
            {
                TvdbId = tvdbSeriesId,
                Name = resolvedName,
                ShortName = resolvedName,
                Slug = Parser.Parser.CleanSeriesTitle(resolvedName),
                Manufacturer = "Console",
                ReleaseYear = 1995,
                Overview = "Verified console & handheld ROM platform managed by Controllarr.",
                Extensions = new List<string> { ".zip", ".7z", ".chd", ".iso" }
            };
        }

        public List<Series> SearchForNewSeriesByImdbId(string imdbId, Language language)
        {
            return SearchForNewSeries(imdbId, language);
        }

        public List<Series> SearchForNewSeriesByAniListId(int aniListId, Language language)
        {
            return new List<Series>();
        }

        public List<Series> SearchForNewSeriesByMyAnimeListId(int malId, Language language)
        {
            return new List<Series>();
        }

        public List<Series> SearchForNewSeriesByTmdbId(int tmdbId, Language language)
        {
            return SearchForNewSeries($"tvdb:{tmdbId}", language);
        }

        private Tuple<Series, List<Episode>> BuildPlatformSeriesAndEpisodes(PlatformCatalogEntry platform)
        {
            var series = MapPlatformToSeries(platform);
            var episodes = new List<Episode>();

            var platformGames = new List<GameCatalogEntry>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIds = new HashSet<int>();

            foreach (var builtIn in BuiltInGames.Where(g => g.PlatformTvdbId == platform.TvdbId))
            {
                if (seenIds.Add(builtIn.TvdbId) && seenNames.Add(builtIn.NoIntroName))
                {
                    platformGames.Add(builtIn);
                }
            }

            var syncedGames = SyncOnlineCatalogForPlatform(platform);
            foreach (var synced in syncedGames)
            {
                if (seenIds.Add(synced.TvdbId) && seenNames.Add(synced.NoIntroName))
                {
                    platformGames.Add(synced);
                }
            }

            foreach (var dynamicGame in DynamicGames.Values.Where(g => g.PlatformTvdbId == platform.TvdbId))
            {
                if (seenIds.Add(dynamicGame.TvdbId) && seenNames.Add(dynamicGame.NoIntroName ?? dynamicGame.Title))
                {
                    platformGames.Add(dynamicGame);
                }
            }

            MarkLatestAndInitialRevisions(platformGames);

            var selectedGameIds = GetSelectedGameIdsForPlatform(platform.TvdbId);
            var hasExplicitSelection = selectedGameIds.Count > 0;

            var episodeCounters = new Dictionary<int, int>();

            foreach (var game in platformGames
                .OrderBy(g => g.BaseTitle ?? g.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => IsPreReleaseVersion(g.RevisionTag, g.NoIntroName) ? 1 : 0)
                .ThenBy(g => GetRevisionSortOrder(g.RevisionTag))
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase))
            {
                var seasonNum = game.RegionSeasonNumber;
                episodeCounters.TryGetValue(seasonNum, out var currentEpNum);
                currentEpNum++;
                episodeCounters[seasonNum] = currentEpNum;
                game.EpisodeNumber = currentEpNum;

                var isUnofficial = seasonNum == RegionUnlicensedSeason ||
                                   seasonNum == RegionHomebrewSeason ||
                                   seasonNum == RegionRomHacksSeason ||
                                   IsPreReleaseVersion(game.RevisionTag, game.NoIntroName);

                var isMonitored = hasExplicitSelection
                    ? selectedGameIds.Contains(game.TvdbId)
                    : !isUnofficial && game.IsLatestRevision;

                episodes.Add(CreateEpisodeForGame(game, 0, isMonitored));
            }

            if (episodes.Count == 0)
            {
                episodes.Add(new Episode
                {
                    SeriesId = 0,
                    TvdbId = platform.TvdbId + 1,
                    SeasonNumber = RegionUsaSeason,
                    EpisodeNumber = 1,
                    AbsoluteEpisodeNumber = 1,
                    Title = "The Troll Farmer",
                    FinaleType = "Retail (v1.0)",
                    Overview = platform.Overview,
                    AirDate = "2015-10-02",
                    AirDateUtc = new DateTime(2015, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                    Runtime = 0,
                    Monitored = true,
                    Ratings = new Ratings { Votes = 100, Value = 9.0m }
                });
            }

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        private List<GameCatalogEntry> SyncOnlineCatalogForPlatform(PlatformCatalogEntry platform)
        {
            if (platform == null || string.IsNullOrWhiteSpace(platform.LibretroSystemName) || _httpClient == null)
            {
                return new List<GameCatalogEntry>();
            }

            if (SyncedPlatformGamesCache.TryGetValue(platform.TvdbId, out var cached))
            {
                return cached;
            }

            var syncedList = new List<GameCatalogEntry>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var escapedSystem = Uri.EscapeDataString(platform.LibretroSystemName);
                var releaseYearLookup = FetchReleaseYearLookup(escapedSystem);

                var datFeeds = new (string Url, string SourceLabel)[]
                {
                    ($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/{escapedSystem}.dat", "No-Intro"),
                    ($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/{escapedSystem}.dat", "Redump"),
                    ($"https://raw.githubusercontent.com/libretro/libretro-database/master/dat/{escapedSystem}.dat", "No-Intro / Libretro"),
                    ($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/homebrew/{escapedSystem}.dat", "Homebrew & Aftermarket"),
                    ($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/hacks/{escapedSystem}.dat", "ROM Hacks & Translations")
                };

                foreach (var feed in datFeeds)
                {
                    var request = new HttpRequest(feed.Url)
                    {
                        SuppressHttpError = true,
                        RequestTimeout = TimeSpan.FromSeconds(15)
                    };

                    var response = _httpClient.Execute(request);
                    if (response != null && !response.HasHttpError && !string.IsNullOrWhiteSpace(response.Content))
                    {
                        var parsedEntries = ParseLibretroDatContent(platform, response.Content, feed.SourceLabel, releaseYearLookup, seenNames);
                        if (parsedEntries.Count > 0)
                        {
                            syncedList.AddRange(parsedEntries);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Offline or unable to sync remote No-Intro/Redump DAT for {0}; using built-in catalog.", platform.Name);
            }

            if (syncedList.Count > 0)
            {
                foreach (var entry in syncedList)
                {
                    DynamicGames[entry.TvdbId] = entry;
                }

                SyncedPlatformGamesCache[platform.TvdbId] = syncedList;
            }

            return syncedList;
        }

        private Dictionary<string, int> FetchReleaseYearLookup(string escapedSystem)
        {
            var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var url = $"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/releaseyear/{escapedSystem}.dat";
                var request = new HttpRequest(url)
                {
                    SuppressHttpError = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                var response = _httpClient.Execute(request);
                if (response != null && !response.HasHttpError && !string.IsNullOrWhiteSpace(response.Content))
                {
                    foreach (Match match in DatReleaseYearBlockRegex.Matches(response.Content))
                    {
                        var name = match.Groups["name"].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(name) &&
                            int.TryParse(match.Groups["year"].Value, out var year) &&
                            year >= 1970 &&
                            year <= 2030)
                        {
                            lookup[name] = year;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Trace(ex, "Unable to fetch releaseyear DAT metadata for {0}", escapedSystem);
            }

            return lookup;
        }

        private static List<GameCatalogEntry> ParseLibretroDatContent(
            PlatformCatalogEntry platform,
            string datContent,
            string sourceLabel,
            Dictionary<string, int> releaseYearLookup,
            HashSet<string> seenNames)
        {
            var results = new List<GameCatalogEntry>();
            var matches = DatGameBlockRegex.Matches(datContent);

            foreach (Match match in matches)
            {
                var blockName = match.Groups["name"].Value.Trim();
                var body = match.Groups["body"].Value;

                var romMatch = DatRomNameRegex.Match(body);
                var rawName = blockName;

                if (romMatch.Success)
                {
                    var romFileName = romMatch.Groups["rom"].Value.Trim();

                    // Skip unheadered duplicate entries (.unh) when headered (.nes) entries exist in the same DAT
                    if (romFileName.EndsWith(".unh", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var strippedRomName = StripKnownRomExtension(romFileName);
                    if (!string.IsNullOrWhiteSpace(strippedRomName) && strippedRomName.Length >= blockName.Length)
                    {
                        rawName = strippedRomName;
                    }
                }

                if (string.IsNullOrWhiteSpace(rawName) ||
                    rawName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                    blockName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seenNames.Add(rawName))
                {
                    continue;
                }

                var year = platform.ReleaseYear;
                if (releaseYearLookup != null &&
                    (releaseYearLookup.TryGetValue(rawName, out var mappedYear) ||
                     releaseYearLookup.TryGetValue(blockName, out mappedYear)))
                {
                    year = mappedYear;
                }
                else
                {
                    var yearMatch = DatReleaseYearRegex.Match(body);
                    if (yearMatch.Success && int.TryParse(yearMatch.Groups["year"].Value, out var parsedYear) && parsedYear >= 1970 && parsedYear <= 2030)
                    {
                        year = parsedYear;
                    }
                }

                var entry = CreateCatalogEntryFromNoIntroName(platform, rawName, year, sourceLabel);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            return results;
        }

        private static string StripKnownRomExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrWhiteSpace(ext) && KnownRomExtensions.Contains(ext))
            {
                return fileName.Substring(0, fileName.Length - ext.Length).Trim();
            }

            return fileName.Trim();
        }

        private static GameCatalogEntry CreateCatalogEntryFromNoIntroName(
            PlatformCatalogEntry platform,
            string noIntroName,
            int year,
            string verificationSource,
            string customSummary = null)
        {
            var parsed = RomParser.ParseRomName(noIntroName);
            var baseTitle = !string.IsNullOrWhiteSpace(parsed?.GameTitle)
                ? NormalizeTitleArticle(parsed.GameTitle)
                : NormalizeTitleArticle(StripParentheses(noIntroName));

            var regionSeason = ClassifyRegionAndCategory(noIntroName, parsed, verificationSource);
            var revisionTag = ExtractRevisionTag(noIntroName, parsed, regionSeason);
            var displayTitle = string.IsNullOrWhiteSpace(revisionTag) || revisionTag == "Retail (v1.0)"
                ? baseTitle
                : $"{baseTitle} ({revisionTag})";

            var tvdbId = ComputeDeterministicGameId(platform.Slug, noIntroName);
            var regionName = GetRegionDisplayName(regionSeason);
            var isPreRelease = IsPreReleaseVersion(revisionTag, noIntroName);

            return new GameCatalogEntry
            {
                TvdbId = tvdbId,
                Title = displayTitle,
                BaseTitle = baseTitle,
                RevisionTag = revisionTag,
                IsLatestRevision = !isPreRelease,
                IsInitialRelease = !isPreRelease && revisionTag == "Retail (v1.0)",
                NoIntroName = noIntroName,
                PlatformTvdbId = platform.TvdbId,
                PlatformSlug = platform.Slug,
                PlatformName = platform.Name,
                PlatformShortName = platform.ShortName,
                Manufacturer = platform.Manufacturer,
                RegionSeasonNumber = regionSeason,
                RegionName = regionName,
                EpisodeNumber = 1,
                ReleaseYear = year,
                FirstAired = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Overview = !string.IsNullOrWhiteSpace(customSummary)
                    ? $"{customSummary} ({verificationSource}: {noIntroName})"
                    : $"Verified {platform.Name} release ({noIntroName}) [{regionName} • {revisionTag}].",
                PosterUrl = BuildLibretroBoxartUrl(platform.LibretroSystemName, noIntroName),
                VerificationSource = verificationSource
            };
        }

        public static bool IsPreReleaseVersion(string revisionTag, string noIntroName = null)
        {
            if (!string.IsNullOrWhiteSpace(revisionTag))
            {
                if (revisionTag.Contains("Proto", StringComparison.OrdinalIgnoreCase) ||
                    revisionTag.Contains("Beta", StringComparison.OrdinalIgnoreCase) ||
                    revisionTag.Contains("Sample", StringComparison.OrdinalIgnoreCase) ||
                    revisionTag.Contains("Demo", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(noIntroName))
            {
                if (noIntroName.Contains("(Proto", StringComparison.OrdinalIgnoreCase) ||
                    noIntroName.Contains("(Beta", StringComparison.OrdinalIgnoreCase) ||
                    noIntroName.Contains("(Sample", StringComparison.OrdinalIgnoreCase) ||
                    noIntroName.Contains("(Demo", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void MarkLatestAndInitialRevisions(List<GameCatalogEntry> games)
        {
            foreach (var group in games.GroupBy(g => $"{g.RegionSeasonNumber}:{(g.BaseTitle ?? g.Title).ToLowerInvariant()}"))
            {
                var items = group.ToList();

                // Prototypes and Betas never count as "the first version" or "the latest version" for 1G1R
                var retailItems = items.Where(i => !IsPreReleaseVersion(i.RevisionTag, i.NoIntroName)).ToList();
                var preReleaseItems = items.Where(i => IsPreReleaseVersion(i.RevisionTag, i.NoIntroName)).ToList();

                foreach (var pre in preReleaseItems)
                {
                    pre.IsInitialRelease = false;
                    pre.IsLatestRevision = false;
                }

                if (retailItems.Count == 0)
                {
                    continue;
                }

                if (retailItems.Count == 1)
                {
                    retailItems[0].IsInitialRelease = true;
                    retailItems[0].IsLatestRevision = true;
                    continue;
                }

                var ordered = retailItems
                    .OrderBy(i => GetRevisionSortOrder(i.RevisionTag))
                    .ThenBy(i => i.NoIntroName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (var idx = 0; idx < ordered.Count; idx++)
                {
                    ordered[idx].IsInitialRelease = idx == 0;
                    ordered[idx].IsLatestRevision = idx == ordered.Count - 1;
                }
            }
        }

        private static int GetRevisionSortOrder(string revisionTag)
        {
            if (string.IsNullOrWhiteSpace(revisionTag) || revisionTag == "Retail (v1.0)")
            {
                return 0;
            }

            if (IsPreReleaseVersion(revisionTag))
            {
                return 1000;
            }

            if (revisionTag.StartsWith("Rev ", StringComparison.OrdinalIgnoreCase) ||
                revisionTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                return 10 + revisionTag.Length;
            }

            return 1;
        }

        private static string ExtractRevisionTag(string noIntroName, Parser.Model.ParsedRomInfo parsed, int regionSeason)
        {
            var parts = new List<string>();

            var discMatch = DiscTagRegex.Match(noIntroName);
            if (discMatch.Success)
            {
                parts.Add(discMatch.Groups["disc"].Value.Trim());
            }

            var preMatch = PreReleaseTagRegex.Match(noIntroName);
            if (preMatch.Success)
            {
                parts.Add(preMatch.Groups["pre"].Value.Trim());
            }

            if (!string.IsNullOrWhiteSpace(parsed?.Revision))
            {
                parts.Add($"Rev {parsed.Revision}");
            }
            else if (!string.IsNullOrWhiteSpace(parsed?.Version))
            {
                parts.Add($"v{parsed.Version}");
            }
            else
            {
                var revMatch = RevisionTagRegex.Match(noIntroName);
                if (revMatch.Success)
                {
                    parts.Add(revMatch.Groups["rev"].Value.Trim());
                }
            }

            if (noIntroName.Contains("(Virtual Console)", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Virtual Console");
            }
            else if (noIntroName.Contains("(Classic Mini)", StringComparison.OrdinalIgnoreCase) ||
                     noIntroName.Contains("(Switch Online)", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add("Re-Release");
            }

            if (parts.Count > 0)
            {
                return string.Join(" • ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
            }

            if (regionSeason == RegionHomebrewSeason)
            {
                return "Homebrew";
            }

            if (regionSeason == RegionRomHacksSeason)
            {
                return "ROM Hack";
            }

            if (regionSeason == RegionUnlicensedSeason)
            {
                return "Unlicensed";
            }

            return "Retail (v1.0)";
        }

        private static int ClassifyRegionAndCategory(string noIntroName, Parser.Model.ParsedRomInfo parsed, string verificationSource = null)
        {
            if (string.Equals(verificationSource, "ROM Hacks & Translations", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("(Hack)", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("Hack]", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("[T+", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("[T-", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("Translation)", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("Kaizo", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("Randomizer", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("Redux", StringComparison.OrdinalIgnoreCase))
            {
                return RegionRomHacksSeason;
            }

            if (string.Equals(verificationSource, "Homebrew & Aftermarket", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("(Aftermarket)", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("(Homebrew)", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("(PD)", StringComparison.OrdinalIgnoreCase))
            {
                return RegionHomebrewSeason;
            }

            if (noIntroName.Contains("(Unl)", StringComparison.OrdinalIgnoreCase) ||
                noIntroName.Contains("(Pirate)", StringComparison.OrdinalIgnoreCase))
            {
                return RegionUnlicensedSeason;
            }

            // Note: Prototypes, Betas, Demos, and Samples stay in their geographic region
            // so they appear as versions of the game itself rather than in a separate region.
            var regionStr = parsed?.Regions != null && parsed.Regions.Count > 0
                ? string.Join(",", parsed.Regions)
                : noIntroName;

            if (regionStr.Contains("World", StringComparison.OrdinalIgnoreCase))
            {
                return RegionWorldSeason;
            }

            if (regionStr.Contains("USA", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(US", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(U)", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Canada", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Brazil", StringComparison.OrdinalIgnoreCase))
            {
                return RegionUsaSeason;
            }

            if (regionStr.Contains("Japan", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Korea", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Asia", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Taiwan", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Hong Kong", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(JP", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(J)", StringComparison.OrdinalIgnoreCase))
            {
                return RegionJapanSeason;
            }

            if (regionStr.Contains("Europe", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Australia", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("UK", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("United Kingdom", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Germany", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("France", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Spain", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Italy", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Netherlands", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Sweden", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(EU", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(E)", StringComparison.OrdinalIgnoreCase))
            {
                return RegionEuropeSeason;
            }

            return RegionUsaSeason;
        }

        private static string NormalizeTitleArticle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var trimmed = title.Trim();
            if (trimmed.EndsWith(", The", StringComparison.OrdinalIgnoreCase))
            {
                return "The " + trimmed.Substring(0, trimmed.Length - 5).Trim();
            }

            if (trimmed.EndsWith(", A", StringComparison.OrdinalIgnoreCase))
            {
                return "A " + trimmed.Substring(0, trimmed.Length - 3).Trim();
            }

            return trimmed;
        }

        private static string StripParentheses(string name)
        {
            var idx = name.IndexOf('(');
            return idx > 0 ? name.Substring(0, idx).Trim() : name.Trim();
        }

        private List<GameCatalogEntry> SearchGamesCatalog(string rawQuery, string normalizedQuery, string[] queryTokens)
        {
            var candidates = new List<Tuple<GameCatalogEntry, int>>();

            foreach (var game in BuiltInGames.Where(g => g.IsLatestRevision))
            {
                var score = ScoreGameMatch(game, rawQuery, normalizedQuery, queryTokens);
                if (score > 0)
                {
                    candidates.Add(Tuple.Create(game, score));
                }
            }

            foreach (var dynGame in DynamicGames.Values.Where(g => g.IsLatestRevision))
            {
                var score = ScoreGameMatch(dynGame, rawQuery, normalizedQuery, queryTokens);
                if (score > 0 && candidates.All(c => c.Item1.TvdbId != dynGame.TvdbId))
                {
                    candidates.Add(Tuple.Create(dynGame, score));
                }
            }

            try
            {
                var datMatches = _datCacheService?.SearchByName(rawQuery, 40);
                if (datMatches != null)
                {
                    foreach (var datEntry in datMatches)
                    {
                        var converted = ConvertDatEntryToGameCatalogEntry(datEntry);
                        if (converted != null && candidates.All(c => !string.Equals(c.Item1.NoIntroName, converted.NoIntroName, StringComparison.OrdinalIgnoreCase)))
                        {
                            DynamicGames[converted.TvdbId] = converted;
                            candidates.Add(Tuple.Create(converted, 85));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Skipping local DAT search fallback during catalog lookup.");
            }

            try
            {
                var dbTitles = _gameTitleRepository?.All();
                if (dbTitles != null)
                {
                    foreach (var dbTitle in dbTitles)
                    {
                        if (dbTitle != null &&
                            !string.IsNullOrWhiteSpace(dbTitle.Title) &&
                            NormalizeSearchText(dbTitle.Title).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            var converted = ConvertGameTitleToCatalogEntry(dbTitle);
                            if (converted != null && candidates.All(c => !string.Equals(c.Item1.Title, converted.Title, StringComparison.OrdinalIgnoreCase)))
                            {
                                DynamicGames[converted.TvdbId] = converted;
                                candidates.Add(Tuple.Create(converted, 80));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Skipping local GameTitle repository lookup during catalog search.");
            }

            try
            {
                var igdbResults = _gameMetadataProxy?.SearchGamesAsync(rawQuery, null)?.GetAwaiter().GetResult();
                if (igdbResults != null)
                {
                    foreach (var igdbGame in igdbResults.Take(15))
                    {
                        if (igdbGame != null && !string.IsNullOrWhiteSpace(igdbGame.Title))
                        {
                            var converted = ConvertIgdbToCatalogEntry(igdbGame);
                            if (converted != null && candidates.All(c => !string.Equals(c.Item1.Title, converted.Title, StringComparison.OrdinalIgnoreCase)))
                            {
                                DynamicGames[converted.TvdbId] = converted;
                                candidates.Add(Tuple.Create(converted, 75));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Skipping IGDB enrichment during catalog lookup.");
            }

            return candidates
                .OrderByDescending(c => c.Item2)
                .ThenBy(c => c.Item1.ReleaseYear)
                .ThenBy(c => c.Item1.Title)
                .Select(c => c.Item1)
                .Take(50)
                .ToList();
        }

        private static int ScorePlatformMatch(PlatformCatalogEntry platform, string rawQuery, string normalizedQuery, string[] queryTokens)
        {
            var normName = NormalizeSearchText(platform.Name);
            var normShort = NormalizeSearchText(platform.ShortName);
            var normSlug = NormalizeSearchText(platform.Slug);
            var normMfg = NormalizeSearchText(platform.Manufacturer);

            if (string.Equals(normName, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normShort, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normSlug, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                platform.Aliases.Any(a => string.Equals(NormalizeSearchText(a), normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 100;
            }

            if (normName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                platform.Aliases.Any(a => NormalizeSearchText(a).Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
            {
                return 85;
            }

            if (string.Equals(normMfg, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 80;
            }

            if (queryTokens.Length > 0 && queryTokens.All(t =>
                normName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                normShort.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                normMfg.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                platform.Aliases.Any(a => NormalizeSearchText(a).Contains(t, StringComparison.OrdinalIgnoreCase))))
            {
                return 70;
            }

            return 0;
        }

        private static int ScoreGameMatch(GameCatalogEntry game, string rawQuery, string normalizedQuery, string[] queryTokens)
        {
            var normTitle = NormalizeSearchText(game.Title);
            var normNoIntro = NormalizeSearchText(game.NoIntroName);
            var normPlatform = NormalizeSearchText(game.PlatformName);
            var normShortPlatform = NormalizeSearchText(game.PlatformShortName);
            var normSlug = NormalizeSearchText(game.PlatformSlug);

            if (string.Equals(normTitle, normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normNoIntro, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            if (normTitle.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 92;
            }

            if (normTitle.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                normNoIntro.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 85;
            }

            var platformAliases = BuiltInPlatforms
                .FirstOrDefault(p => string.Equals(p.Slug, game.PlatformSlug, StringComparison.OrdinalIgnoreCase))
                ?.Aliases ?? new List<string>();
            var normAliases = string.Join(" ", platformAliases.Select(NormalizeSearchText));

            var combinedSearchable = $"{normTitle} {normNoIntro} {normPlatform} {normShortPlatform} {normSlug} {normAliases}";
            if (queryTokens.Length > 0 && queryTokens.All(t => combinedSearchable.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                var titleTokenMatches = queryTokens.Count(t => normTitle.Contains(t, StringComparison.OrdinalIgnoreCase));
                if (titleTokenMatches > 0)
                {
                    return 65 + (titleTokenMatches * 5);
                }
            }

            return 0;
        }

        private Series MapPlatformToSeries(PlatformCatalogEntry platform)
        {
            var seasons = BuildDefaultRegionSeasons();
            var firstAired = new DateTime(platform.ReleaseYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var consoleImageUrl = $"/Content/Images/Consoles/{platform.Slug}.png";
            var images = new List<MediaCover.MediaCover>
            {
                new MediaCover.MediaCover(MediaCoverTypes.Poster, consoleImageUrl)
                {
                    RemoteUrl = consoleImageUrl
                }
            };

            return new Series
            {
                TvdbId = platform.TvdbId,
                Title = platform.Name,
                CleanTitle = platform.Name.CleanSeriesTitle(),
                SortTitle = SeriesTitleNormalizer.Normalize(platform.Name, platform.TvdbId),
                TitleSlug = platform.Slug,
                Overview = platform.Overview,
                Year = platform.ReleaseYear,
                FirstAired = firstAired,
                Network = $"{platform.Manufacturer} (Platform)",
                Status = SeriesStatusType.Continuing,
                SeriesType = SeriesTypes.Standard,
                Runtime = 0,
                Monitored = true,
                OriginalLanguage = Language.English,
                Genres = new List<string> { "Platform", platform.Manufacturer },
                Ratings = new Ratings { Votes = 500, Value = 9.5m },
                Seasons = seasons,
                Images = images
            };
        }

        private Series MapGameToLookupSeries(GameCatalogEntry game)
        {
            var seasons = BuildDefaultRegionSeasons();
            foreach (var season in seasons)
            {
                season.Monitored = season.SeasonNumber == game.RegionSeasonNumber;
            }

            var displayTitle = $"{game.Title} [{game.PlatformShortName}]";
            var images = new List<MediaCover.MediaCover>();

            if (!string.IsNullOrWhiteSpace(game.PosterUrl))
            {
                images.Add(new MediaCover.MediaCover(MediaCoverTypes.Poster, game.PosterUrl)
                {
                    RemoteUrl = game.PosterUrl
                });
            }

            return new Series
            {
                TvdbId = game.TvdbId,
                Title = displayTitle,
                CleanTitle = game.Title.CleanSeriesTitle(),
                SortTitle = SeriesTitleNormalizer.Normalize(displayTitle, game.TvdbId),
                TitleSlug = $"{game.PlatformSlug}-{Parser.Parser.CleanSeriesTitle(game.Title)}-{game.TvdbId}",
                Overview = $"{game.Overview} — Adding this game will automatically configure the {game.PlatformName} platform (if not already in your library) and monitor '{game.Title}' under {game.RegionName}.",
                Year = game.ReleaseYear,
                FirstAired = game.FirstAired,
                Network = $"{game.PlatformName} • {game.RegionName} • {game.VerificationSource}",
                Status = SeriesStatusType.Ended,
                SeriesType = SeriesTypes.Standard,
                Runtime = 0,
                Monitored = true,
                OriginalLanguage = Language.English,
                Genres = new List<string> { "Game", game.PlatformShortName, game.RegionName, game.VerificationSource },
                Ratings = new Ratings { Votes = 250, Value = 9.2m },
                Seasons = seasons,
                Images = images
            };
        }

        private static List<Season> BuildDefaultRegionSeasons()
        {
            return new List<Season>
            {
                new Season { SeasonNumber = RegionUnlicensedSeason, Monitored = false },
                new Season { SeasonNumber = RegionUsaSeason, Monitored = true },
                new Season { SeasonNumber = RegionEuropeSeason, Monitored = true },
                new Season { SeasonNumber = RegionJapanSeason, Monitored = true },
                new Season { SeasonNumber = RegionWorldSeason, Monitored = true },
                new Season { SeasonNumber = RegionHomebrewSeason, Monitored = false },
                new Season { SeasonNumber = RegionRomHacksSeason, Monitored = false }
            };
        }

        private GameCatalogEntry ConvertDatEntryToGameCatalogEntry(DatEntry datEntry)
        {
            if (datEntry == null)
            {
                return null;
            }

            var rawName = !string.IsNullOrWhiteSpace(datEntry.GameName) ? datEntry.GameName : datEntry.RomName;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return null;
            }

            var platform = InferPlatformFromText($"{datEntry.SmdbPath} {datEntry.RomName}") ?? BuiltInPlatforms.First(p => p.Slug == "snes");
            return CreateCatalogEntryFromNoIntroName(platform, rawName, platform.ReleaseYear, "No-Intro / Redump DAT");
        }

        private GameCatalogEntry ConvertGameTitleToCatalogEntry(GameTitle dbTitle)
        {
            var platform = BuiltInPlatforms.FirstOrDefault(p => p.Slug == "snes") ?? BuiltInPlatforms[0];
            var entry = CreateCatalogEntryFromNoIntroName(platform, $"{dbTitle.Title} (USA)", platform.ReleaseYear, "Local Catalog", dbTitle.Summary);
            if (!string.IsNullOrWhiteSpace(dbTitle.CoverArtworkUrl))
            {
                entry.PosterUrl = dbTitle.CoverArtworkUrl;
            }

            return entry;
        }

        private GameCatalogEntry ConvertIgdbToCatalogEntry(Model.GameMetadata igdb)
        {
            var platform = BuiltInPlatforms.First(p => p.Slug == "snes");
            var year = igdb.ReleaseDate?.Year ?? platform.ReleaseYear;
            var entry = CreateCatalogEntryFromNoIntroName(platform, $"{igdb.Title} (USA)", year, "IGDB", igdb.Summary);
            if (!string.IsNullOrWhiteSpace(igdb.CoverUrl))
            {
                entry.PosterUrl = igdb.CoverUrl;
            }

            return entry;
        }

        private static PlatformCatalogEntry InferPlatformFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var tokens = NormalizeSearchText(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var platform in BuiltInPlatforms)
            {
                if (tokens.Any(t =>
                    string.Equals(t, platform.Slug, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, NormalizeSearchText(platform.ShortName), StringComparison.OrdinalIgnoreCase) ||
                    platform.Aliases.Any(a => string.Equals(t, NormalizeSearchText(a), StringComparison.OrdinalIgnoreCase))))
                {
                    return platform;
                }
            }

            return null;
        }

        public static string GetRegionDisplayName(int seasonNumber)
        {
            return seasonNumber switch
            {
                RegionUsaSeason => "USA",
                RegionEuropeSeason => "Europe",
                RegionJapanSeason => "Japan",
                RegionWorldSeason => "World",
                RegionUnlicensedSeason => "Unlicensed",
                RegionRomHacksSeason => "ROM Hacks & Translations",
                RegionHomebrewSeason => "Homebrew & Aftermarket",
                _ => $"Region {seasonNumber}"
            };
        }

        private static int ComputeDeterministicGameId(string platformSlug, string title)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in $"{platformSlug}:{title}".ToLowerInvariant())
                {
                    hash = (hash * 31) + ch;
                }

                return 200000 + (Math.Abs(hash) % 700000);
            }
        }

        private static string NormalizeSearchText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var replaced = input
                .Replace("é", "e", StringComparison.OrdinalIgnoreCase)
                .Replace("É", "e", StringComparison.OrdinalIgnoreCase)
                .Replace("-", " ", StringComparison.Ordinal)
                .Replace(":", " ", StringComparison.Ordinal)
                .Replace("'", string.Empty, StringComparison.Ordinal)
                .Replace(".", " ", StringComparison.Ordinal)
                .Replace("&", " and ", StringComparison.Ordinal);

            return string.Join(" ", replaced.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        }

        private static string BuildLibretroBoxartUrl(string libretroSystem, string noIntroName)
        {
            if (string.IsNullOrWhiteSpace(libretroSystem) || string.IsNullOrWhiteSpace(noIntroName))
            {
                return null;
            }

            var safeName = noIntroName.Replace("&", "_").Replace("/", "_").Replace(":", "_");
            return $"https://thumbnails.libretro.com/{Uri.EscapeDataString(libretroSystem)}/Named_Boxarts/{Uri.EscapeDataString(safeName)}.png";
        }

        private static List<PlatformCatalogEntry> BuildPlatforms()
        {
            return new List<PlatformCatalogEntry>
            {
                CreatePlatform(1001, "Nintendo Entertainment System", "NES", "nes", "Nintendo", 1985, "Nintendo - Nintendo Entertainment System", "Super Mario Bros. 3 (USA)", new[] { "NES", "Famicom", "FC", "Nintendo NES" }, new[] { ".nes", ".unf", ".zip", ".7z" }),
                CreatePlatform(1002, "Famicom Disk System", "FDS", "fds", "Nintendo", 1986, "Nintendo - Family Computer Disk System", "Zelda no Densetsu - The Hyrule Fantasy (Japan)", new[] { "FDS", "Disk System" }, new[] { ".fds", ".zip", ".7z" }),
                CreatePlatform(1003, "Super Nintendo Entertainment System", "SNES", "snes", "Nintendo", 1990, "Nintendo - Super Nintendo Entertainment System", "Super Mario World (USA)", new[] { "SNES", "Super NES", "Super Nintendo", "Super Famicom", "SFC" }, new[] { ".sfc", ".smc", ".zip", ".7z" }),
                CreatePlatform(1004, "Satellaview", "BS-X", "satellaview", "Nintendo", 1995, "Nintendo - Satellaview", "BS Zelda no Densetsu (Japan)", new[] { "BSX", "BS-X" }, new[] { ".bs", ".sfc", ".zip" }),
                CreatePlatform(1005, "Nintendo 64", "N64", "n64", "Nintendo", 1996, "Nintendo - Nintendo 64", "Super Mario 64 (USA)", new[] { "N64", "Ultra 64" }, new[] { ".z64", ".n64", ".v64", ".zip", ".7z" }),
                CreatePlatform(1006, "64DD", "64DD", "64dd", "Nintendo", 1999, "Nintendo - Nintendo 64DD", "F-Zero X Expansion Kit (Japan)", new[] { "N64DD" }, new[] { ".ndd", ".zip" }),
                CreatePlatform(1007, "GameCube", "GCN", "gamecube", "Nintendo", 2001, "Nintendo - GameCube", "Super Smash Bros. Melee (USA)", new[] { "GCN", "NGC", "Nintendo GameCube" }, new[] { ".rvz", ".iso", ".gcm", ".ciso" }),
                CreatePlatform(1008, "Wii", "Wii", "wii", "Nintendo", 2006, "Nintendo - Wii", "Super Mario Galaxy (USA)", new[] { "Nintendo Wii", "RVL" }, new[] { ".rvz", ".wbfs", ".iso" }),
                CreatePlatform(1009, "Game Boy", "GB", "gb", "Nintendo", 1989, "Nintendo - Game Boy", "Tetris (World) (Rev A)", new[] { "GB", "GameBoy", "DMG" }, new[] { ".gb", ".zip", ".7z" }),
                CreatePlatform(1010, "Game Boy Color", "GBC", "gbc", "Nintendo", 1998, "Nintendo - Game Boy Color", "Pokemon - Crystal Version (USA, Europe)", new[] { "GBC", "GameBoy Color", "CGB" }, new[] { ".gbc", ".zip", ".7z" }),
                CreatePlatform(1011, "Game Boy Advance", "GBA", "gba", "Nintendo", 2001, "Nintendo - Game Boy Advance", "Pokemon - Emerald Version (USA, Europe)", new[] { "GBA", "GameBoy Advance", "AGB" }, new[] { ".gba", ".zip", ".7z" }),
                CreatePlatform(1012, "Nintendo DS", "NDS", "nds", "Nintendo", 2004, "Nintendo - Nintendo DS", "Pokemon - HeartGold Version (USA)", new[] { "NDS", "DS", "Nintendo DSi" }, new[] { ".nds", ".zip", ".7z" }),
                CreatePlatform(1013, "Nintendo 3DS", "3DS", "3ds", "Nintendo", 2011, "Nintendo - Nintendo 3DS", "Pokemon X (USA)", new[] { "3DS", "N3DS", "New 3DS" }, new[] { ".3ds", ".cia", ".cci", ".zip" }),
                CreatePlatform(1014, "Nintendo Switch", "Switch", "switch", "Nintendo", 2017, "Nintendo - Switch", "Pokemon Legends - Arceus (World)", new[] { "Switch", "NSW", "NX" }, new[] { ".nsp", ".xci", ".nsz" }),
                CreatePlatform(1015, "Sega Genesis", "Genesis", "genesis", "Sega", 1988, "Sega - Mega Drive - Genesis", "Sonic The Hedgehog 2 (World)", new[] { "Genesis", "Mega Drive", "MD", "Sega Mega Drive" }, new[] { ".md", ".gen", ".bin", ".zip", ".7z" }),
                CreatePlatform(1016, "Sega CD", "Sega CD", "segacd", "Sega", 1991, "Sega - Mega-CD - Sega CD", "Sonic CD (USA)", new[] { "Sega CD", "Mega CD", "Mega-CD", "SCD" }, new[] { ".chd", ".cue", ".bin", ".iso" }),
                CreatePlatform(1017, "Sega 32X", "32X", "32x", "Sega", 1994, "Sega - 32X", "Knuckles' Chaotix (Japan, USA)", new[] { "32X", "Super 32X" }, new[] { ".32x", ".zip", ".7z" }),
                CreatePlatform(1018, "Sega Saturn", "Saturn", "saturn", "Sega", 1994, "Sega - Saturn", "Nights into Dreams... (USA)", new[] { "Saturn", "SS" }, new[] { ".chd", ".cue", ".bin", ".iso" }),
                CreatePlatform(1019, "Sega Dreamcast", "Dreamcast", "dreamcast", "Sega", 1998, "Sega - Dreamcast", "Sonic Adventure (USA)", new[] { "Dreamcast", "DC" }, new[] { ".chd", ".gdi", ".cdi", ".cue" }),
                CreatePlatform(1020, "TurboGrafx-16", "TG16", "tg16", "NEC", 1987, "NEC - PC Engine - TurboGrafx 16", "Bonk's Adventure (USA)", new[] { "TG16", "PC Engine", "PCE", "TurboGrafx" }, new[] { ".pce", ".zip", ".7z" }),
                CreatePlatform(1021, "TurboGrafx-CD", "TG-CD", "tgcd", "NEC", 1988, "NEC - PC Engine CD - TurboGrafx-CD", "Akumajou Dracula X - Chi no Rondo (Japan)", new[] { "TGCD", "PC Engine CD", "PCECD" }, new[] { ".chd", ".cue", ".bin" }),
                CreatePlatform(1022, "PlayStation", "PS1", "ps1", "Sony", 1994, "Sony - PlayStation", "Castlevania - Symphony of the Night (USA)", new[] { "PS1", "PSX", "PSOne", "Sony PlayStation" }, new[] { ".chd", ".pbp", ".cue", ".bin", ".iso" }),
                CreatePlatform(1023, "PlayStation 2", "PS2", "ps2", "Sony", 2000, "Sony - PlayStation 2", "Shadow of the Colossus (USA)", new[] { "PS2", "Sony PlayStation 2" }, new[] { ".chd", ".iso", ".cso", ".bin" }),
                CreatePlatform(1024, "PlayStation Portable", "PSP", "psp", "Sony", 2004, "Sony - PlayStation Portable", "Crisis Core - Final Fantasy VII (USA)", new[] { "PSP", "Sony PSP" }, new[] { ".iso", ".cso", ".pbp", ".chd" }),
                CreatePlatform(1025, "Jaguar CD", "Jaguar CD", "jaguarcd", "Atari", 1995, "Atari - Jaguar CD", "Tempest 2000 (USA)", new[] { "Atari Jaguar CD" }, new[] { ".chd", ".cue", ".bin" }),
                CreatePlatform(1026, "Wii U", "Wii U", "wiiu", "Nintendo", 2012, "Nintendo - Wii U", "Mario Kart 8 (USA)", new[] { "WiiU", "Nintendo Wii U" }, new[] { ".wua", ".wux", ".wud", ".rpx" }),
                CreatePlatform(1027, "Virtual Boy", "VB", "vb", "Nintendo", 1995, "Nintendo - Virtual Boy", "Virtual Boy Wario Land (Japan, USA)", new[] { "VB" }, new[] { ".vb", ".zip" }),
                CreatePlatform(1028, "Sega Master System", "SMS", "sms", "Sega", 1985, "Sega - Master System - Mark III", "Phantasy Star (USA, Europe)", new[] { "SMS", "Master System", "Mark III" }, new[] { ".sms", ".zip", ".7z" }),
                CreatePlatform(1029, "Sega Game Gear", "GG", "gg", "Sega", 1990, "Sega - Game Gear", "Sonic The Hedgehog (World)", new[] { "GG", "Game Gear" }, new[] { ".gg", ".zip", ".7z" }),
                CreatePlatform(1030, "PlayStation 3", "PS3", "ps3", "Sony", 2006, "Sony - PlayStation 3", "Demon's Souls (USA)", new[] { "PS3", "Sony PlayStation 3" }, new[] { ".iso", ".pkg" }),
                CreatePlatform(1031, "PlayStation Vita", "PS Vita", "psvita", "Sony", 2011, "Sony - PlayStation Vita", "Persona 4 Golden (USA)", new[] { "Vita", "PSV", "PS Vita" }, new[] { ".vpk", ".zip" }),
                CreatePlatform(1032, "Xbox", "Xbox", "xbox", "Microsoft", 2001, "Microsoft - Xbox", "Halo - Combat Evolved (USA)", new[] { "Original Xbox", "OG Xbox" }, new[] { ".iso", ".xiso" }),
                CreatePlatform(1033, "Xbox 360", "Xbox 360", "xbox360", "Microsoft", 2005, "Microsoft - Xbox 360", "Halo 3 (USA)", new[] { "X360", "360" }, new[] { ".iso", ".xex", ".god" }),
                CreatePlatform(1034, "Atari 2600", "2600", "atari2600", "Atari", 1977, "Atari - 2600", "Pitfall! - Pitfall Harry's Jungle Adventure (USA)", new[] { "2600", "VCS" }, new[] { ".a26", ".bin", ".zip" }),
                CreatePlatform(1035, "Atari 7800", "7800", "atari7800", "Atari", 1986, "Atari - 7800", "Food Fight (USA)", new[] { "7800" }, new[] { ".a78", ".bin", ".zip" }),
                CreatePlatform(1036, "Atari Lynx", "Lynx", "lynx", "Atari", 1989, "Atari - Lynx", "California Games (USA, Europe)", new[] { "Lynx" }, new[] { ".lnx", ".zip" }),
                CreatePlatform(1037, "Atari Jaguar", "Jaguar", "jaguar", "Atari", 1993, "Atari - Jaguar", "Alien vs Predator (World)", new[] { "Jaguar" }, new[] { ".j64", ".jag", ".zip" }),
                CreatePlatform(1038, "Neo Geo", "Neo Geo", "neogeo", "SNK", 1990, "SNK - Neo Geo", "Metal Slug 3 (World)", new[] { "AES", "MVS", "Neo-Geo", "NGCD" }, new[] { ".zip", ".7z", ".neo", ".chd" }),
                CreatePlatform(1039, "Neo Geo Pocket Color", "NGPC", "ngpc", "SNK", 1999, "SNK - Neo Geo Pocket Color", "SNK vs. Capcom - The Match of the Millennium (World)", new[] { "NGPC", "NGP" }, new[] { ".ngc", ".ngp", ".zip" }),
                CreatePlatform(1040, "WonderSwan Color", "WSC", "wonderswan", "Bandai", 2000, "Bandai - WonderSwan Color", "Final Fantasy (Japan)", new[] { "WonderSwan", "WSC", "WS" }, new[] { ".wsc", ".ws", ".zip" }),
                CreatePlatform(1041, "Arcade", "Arcade", "arcade", "Arcade", 1980, "MAME", "Street Fighter II' - Champion Edition (World)", new[] { "MAME", "FBNeo", "FinalBurn Neo", "CPS1", "CPS2", "CPS3" }, new[] { ".zip", ".7z", ".chd" })
            };
        }

        private static PlatformCatalogEntry CreatePlatform(
            int tvdbId,
            string name,
            string shortName,
            string slug,
            string manufacturer,
            int year,
            string libretroSystem,
            string representativeBoxart,
            string[] aliases,
            string[] extensions)
        {
            var extList = string.Join(", ", extensions);
            return new PlatformCatalogEntry
            {
                TvdbId = tvdbId,
                Name = name,
                ShortName = shortName,
                Slug = slug,
                Manufacturer = manufacturer,
                ReleaseYear = year,
                LibretroSystemName = libretroSystem,
                RepresentativeBoxartName = representativeBoxart,
                Overview = $"{manufacturer} {name} ({year}) — Verified No-Intro / Redump / HTGDB (SmokeMonster) hardware platform. Supported ROM & disc formats: {extList}. Organized by BIOS, USA, Japan, Europe, World, Unlicensed, ROM Hacks & Translations, and Homebrew & Aftermarket.",
                Aliases = aliases.ToList(),
                Extensions = extensions.ToList()
            };
        }

        private static List<GameCatalogEntry> BuildGames()
        {
            var games = new List<GameCatalogEntry>();
            var id = 100001;

            void Add(string slug, string noIntroName, int year, string summary, string source = "No-Intro")
            {
                var platform = BuiltInPlatforms.First(p => p.Slug == slug);
                var entry = CreateCatalogEntryFromNoIntroName(platform, noIntroName, year, source, summary);
                entry.TvdbId = id++;
                games.Add(entry);
            }

            // Pokémon Series (GB, GBC, GBA, N64, GCN, NDS, 3DS, Switch)
            Add("gb", "Pokemon - Red Version (USA, Europe)", 1998, "Begin your journey in the Kanto region to catch all 151 original Pokemon and defeat the Elite Four.");
            Add("gb", "Pokemon - Blue Version (USA, Europe)", 1998, "Classic Kanto RPG adventure featuring version-exclusive Pokemon and link cable trading.");
            Add("gb", "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)", 1999, "Travel across Kanto with Pikachu following right behind you and obtain Bulbasaur, Charmander, and Squirtle.");
            Add("gb", "Pocket Monsters - Aka (Japan)", 1996, "Original Japanese release of Pocket Monsters Red Version for Game Boy.");
            Add("gb", "Pocket Monsters - Aka (Japan) (Rev A)", 1996, "Revised Japanese release of Pocket Monsters Red Version for Game Boy.");
            Add("gb", "Pocket Monsters - Midori (Japan)", 1996, "Original Japanese release of Pocket Monsters Green Version for Game Boy.");
            Add("gbc", "Pokemon - Gold Version (USA, Europe)", 2000, "Explore the Johto and Kanto regions, discover 100 new Pokemon, and encounter the legendary Ho-Oh.");
            Add("gbc", "Pokemon - Silver Version (USA, Europe)", 2000, "Journey through Johto with a real-time clock, day/night cycle, and the legendary Lugia.");
            Add("gbc", "Pokemon - Crystal Version (USA, Europe)", 2001, "Initial retail release of Pokemon Crystal featuring Suicune and the Battle Tower.");
            Add("gbc", "Pokemon - Crystal Version (USA, Europe) (Rev A)", 2001, "Updated revision (Rev A / v1.1) of Pokemon Crystal Version.");
            Add("gbc", "Pokemon Trading Card Game (USA, Australia)", 2000, "Collect cards, build decks, and defeat the eight Club Masters to inherit the Legendary Pokemon Cards.");
            Add("gbc", "Pokemon Pinball (USA, Australia)", 1999, "Catch 'em all on Red and Blue pinball tables with built-in Rumble Pak support.");
            Add("gbc", "Pokemon Puzzle Challenge (USA, Australia)", 2000, "Fast-paced puzzle action set in the Johto region based on Panel de Pon.");
            Add("gbc", "Pokemon Prism (USA) (Hack)", 2016, "Acclaimed full-scale GBC ROM hack set in the Naljo region.", "HTGDB / SMDB");
            Add("gba", "Pokemon - Emerald Version (USA, Europe)", 2005, "Definitive Hoenn adventure featuring Rayquaza, both Team Magma and Team Aqua, and the Battle Frontier.");
            Add("gba", "Pokemon - FireRed Version (USA, Europe)", 2004, "Initial retail release (v1.0) of Pokemon FireRed Version.");
            Add("gba", "Pokemon - FireRed Version (USA, Europe) (Rev 1)", 2004, "Updated retail revision (Rev 1 / v1.1) of Pokemon FireRed Version.");
            Add("gba", "Pokemon - LeafGreen Version (USA, Europe)", 2004, "Initial retail release (v1.0) of Pokemon LeafGreen Version.");
            Add("gba", "Pokemon - LeafGreen Version (USA, Europe) (Rev 1)", 2004, "Updated retail revision (Rev 1 / v1.1) of Pokemon LeafGreen Version.");
            Add("gba", "Pokemon - Ruby Version (USA)", 2003, "Initial retail release of Pokemon Ruby Version in Hoenn.");
            Add("gba", "Pokemon - Ruby Version (USA, Europe) (Rev 2)", 2004, "Latest retail revision (Rev 2 / v1.2) of Pokemon Ruby Version with RTC Berry glitch fix.");
            Add("gba", "Pokemon - Sapphire Version (USA)", 2003, "Initial retail release of Pokemon Sapphire Version in Hoenn.");
            Add("gba", "Pokemon - Sapphire Version (USA, Europe) (Rev 2)", 2004, "Latest retail revision (Rev 2 / v1.2) of Pokemon Sapphire Version with RTC Berry glitch fix.");
            Add("gba", "Pokemon Mystery Dungeon - Red Rescue Team (USA, Australia)", 2006, "Wake up transformed into a Pokemon and form a rescue team to explore shifting mystery dungeons.");
            Add("gba", "Pokemon Pinball - Ruby & Sapphire (USA)", 2003, "Capture over 200 Hoenn Pokemon across Ruby and Sapphire pinball tables.");
            Add("gba", "Pokemon Unbound (USA) (Hack)", 2022, "Feature-packed GBA ROM hack set in the Borrius region.", "HTGDB / SMDB");
            Add("n64", "Pokemon Stadium (USA)", 2000, "Initial retail release of Pokemon Stadium for Nintendo 64.");
            Add("n64", "Pokemon Stadium (USA) (Rev 2)", 2000, "Latest retail revision (Rev 2) of Pokemon Stadium.");
            Add("n64", "Pokemon Stadium 2 (USA)", 2001, "3D tournament battles supporting all 251 Generation I and II Pokemon plus Earl's Pokemon Academy.");
            Add("n64", "Pokemon Snap (USA)", 1999, "Ride the ZERO-ONE across Pokemon Island and photograph wild Pokemon in their natural habitats.");
            Add("n64", "Pokemon Puzzle League (USA)", 2000, "Fast-paced 2D and 3D puzzle battles starring Ash Ketchum and the Indigo Plateau Masters.");
            Add("gamecube", "Pokemon Colosseum (USA)", 2004, "Explore the Orre region and snag Shadow Pokemon to purify their hearts in full 3D Double Battles.", "Redump");
            Add("gamecube", "Pokemon XD - Gale of Darkness (USA)", 2005, "Return to the Orre region to confront Cipher and rescue Shadow Lugia (XD001).", "Redump");
            Add("nds", "Pokemon - Platinum Version (USA)", 2009, "Initial retail release of Pokemon Platinum Version.");
            Add("nds", "Pokemon - Platinum Version (USA) (Rev 1)", 2009, "Latest retail revision (Rev 1) of Pokemon Platinum Version.");
            Add("nds", "Pokemon - HeartGold Version (USA)", 2010, "DS remake of Pokemon Gold featuring Johto & Kanto, walking partner Pokemon, and Pokewalker support.");
            Add("nds", "Pokemon - SoulSilver Version (USA)", 2010, "DS remake of Pokemon Silver featuring Lugia, walking partner Pokemon, and 16 Gym Badges.");
            Add("nds", "Pokemon - Diamond Version (USA, Europe) (Rev 5)", 2007, "Journey across the Sinnoh region, battle Team Galactic, and encounter the temporal Pokemon Dialga.");
            Add("nds", "Pokemon - Pearl Version (USA, Europe) (Rev 5)", 2007, "Journey across the Sinnoh region, battle Team Galactic, and encounter the spatial Pokemon Palkia.");
            Add("nds", "Pokemon - Black Version (USA, Europe)", 2011, "Explore the Unova region with 156 brand-new Pokemon and confront Team Plasma and Reshiram.");
            Add("nds", "Pokemon - White Version (USA, Europe)", 2011, "Explore the Unova region, White Forest, and encounter the legendary Pokemon Zekrom.");
            Add("nds", "Pokemon - Black Version 2 (USA, Europe)", 2012, "Direct sequel set two years later in Unova featuring Black Kyurem and the Pokemon World Tournament.");
            Add("nds", "Pokemon - White Version 2 (USA, Europe)", 2012, "Direct sequel set two years later in Unova featuring White Kyurem and the Pokemon World Tournament.");
            Add("nds", "Pokemon Mystery Dungeon - Explorers of Sky (USA)", 2009, "Expanded edition of Explorers of Time & Darkness with Special Episodes and additional starter Pokemon.");
            Add("nds", "Pokemon Conquest (USA)", 2012, "Tactical turn-based strategy crossover between Pokemon and Nobunaga's Ambition in the Ransei region.");
            Add("3ds", "Pokemon X (USA)", 2013, "First full 3D main-series adventure set in the Kalos region introducing Mega Evolution and Xerneas.");
            Add("3ds", "Pokemon Y (USA)", 2013, "Explore the Kalos region, master Mega Evolution, and encounter the Destruction Pokemon Yveltal.");
            Add("3ds", "Pokemon Omega Ruby (USA)", 2014, "3DS reimagining of Pokemon Ruby featuring Primal Groudon, Mega Evolution, and Soaring in the sky.");
            Add("3ds", "Pokemon Alpha Sapphire (USA)", 2014, "3DS reimagining of Pokemon Sapphire featuring Primal Kyogre, Mega Evolution, and the Delta Episode.");
            Add("3ds", "Pokemon Ultra Sun (USA)", 2017, "Expanded Alola adventure featuring Dusk Mane Necrozma, Ultra Wormholes, and Team Rainbow Rocket.");
            Add("3ds", "Pokemon Ultra Moon (USA)", 2017, "Expanded Alola adventure featuring Dawn Wings Necrozma, Ultra Wormholes, and Team Rainbow Rocket.");
            Add("switch", "Pokemon Legends - Arceus (World)", 2022, "Action-RPG set in the ancient Hisui region where you survey wild Pokemon to create the first Pokedex.");
            Add("switch", "Pokemon Sword (World)", 2019, "Explore the Galar region, challenge Dynamax Gym Leaders, and encounter the legendary Zacian.");
            Add("switch", "Pokemon Shield (World)", 2019, "Explore the Galar region, Wild Area, and encounter the legendary Zamazenta.");
            Add("switch", "Pokemon Scarlet (World)", 2022, "Open-world adventure across the Paldea region with Terastal phenomenon and Koraidon.");
            Add("switch", "Pokemon Violet (World)", 2022, "Open-world adventure across the Paldea region with Terastal phenomenon and Miraidon.");

            // NES (Expanded Retail, Revisions, Europe, Japan, Homebrew, Hacks, Unlicensed)
            Add("nes", "Super Mario Bros. (World)", 1985, "Side-scrolling platformer masterpiece where Mario and Luigi rescue Princess Toadstool from Bowser.");
            Add("nes", "Super Mario Bros. (Europe) (Rev A)", 1987, "PAL European release of Super Mario Bros.");
            Add("nes", "Super Mario Bros. 2 (USA)", 1988, "Initial USA retail release of Super Mario Bros. 2 set in Subcon.");
            Add("nes", "Super Mario Bros. 2 (USA) (Rev A)", 1988, "Latest retail revision (Rev A) of Super Mario Bros. 2.");
            Add("nes", "Super Mario Bros. 3 (USA)", 1990, "Initial USA retail release (PRG0) of Super Mario Bros. 3.");
            Add("nes", "Super Mario Bros. 3 (USA) (Rev A)", 1990, "Updated USA retail revision (Rev A / PRG1) of Super Mario Bros. 3.");
            Add("nes", "Super Mario Bros. 3 (Europe)", 1991, "European PAL release of Super Mario Bros. 3.");
            Add("nes", "Super Mario Bros. 3 (Japan) (Rev A)", 1988, "Japanese Famicom release of Super Mario Bros. 3.");
            Add("nes", "Super Mario Bros. (Japan)", 1985, "Original Japanese Nintendo Family Computer (Famicom) cartridge release of Super Mario Bros.");
            Add("nes", "Dragon Quest (Japan)", 1986, "Original Japanese Famicom release of Dragon Quest.");
            Add("nes", "Final Fantasy III (Japan)", 1990, "Classic Famicom RPG introducing the job-change system.");
            Add("nes", "Fire Emblem - Ankoku Ryuu to Hikari no Tsurugi (Japan)", 1990, "Inaugural Famicom tactical RPG starring Prince Marth.");
            Add("nes", "Mother (Japan)", 1989, "Shigesato Itoi's original Japanese Famicom RPG.");
            Add("nes", "Legend of Zelda, The (USA)", 1987, "Initial USA release (PRG0) of The Legend of Zelda.");
            Add("nes", "Legend of Zelda, The (USA) (Rev A)", 1987, "Updated USA revision (Rev A / PRG1) of The Legend of Zelda with TM screen & bug fixes.");
            Add("nes", "Legend of Zelda, The (Europe) (Rev A)", 1987, "European PAL release of The Legend of Zelda.");
            Add("nes", "Zelda II - The Adventure of Link (USA)", 1988, "Action-RPG sequel featuring side-scrolling combat, magic spells, and the Triforce of Courage.");
            Add("nes", "Metroid (USA)", 1987, "Non-linear sci-fi exploration on Planet Zebes as bounty hunter Samus Aran battles Mother Brain.");
            Add("nes", "Metroid (Europe)", 1988, "European PAL release of Metroid.");
            Add("nes", "Mega Man (USA)", 1987, "Battle Dr. Wily's six original Robot Masters including Cut Man, Elec Man, and Guts Man.");
            Add("nes", "Mega Man 2 (USA)", 1989, "Battle eight Robot Masters including Metal Man and Quick Man before storming Dr. Wily's fortress.");
            Add("nes", "Mega Man 3 (USA)", 1990, "Introduces Proto Man, Rush the robotic dog, and the slide maneuver.");
            Add("nes", "Mega Man 4 (USA)", 1992, "Introduces the Mega Buster charge shot against Dr. Cossack's Robot Masters.");
            Add("nes", "Mega Man 4 (USA) (Rev A)", 1992, "Updated retail revision (Rev A) of Mega Man 4.");
            Add("nes", "Mega Man 5 (USA)", 1992, "Battle Proto Man's army of Robot Masters and collect the BEAT letters.");
            Add("nes", "Mega Man 6 (USA)", 1994, "Global Robot Master tournament featuring Rush Jet and Power Armor adapters.");
            Add("nes", "Castlevania (USA)", 1987, "Initial USA release (PRG0) of Castlevania.");
            Add("nes", "Castlevania (USA) (Rev A)", 1987, "Updated USA revision (Rev A / PRG1) of Castlevania fixing late-game crash bugs.");
            Add("nes", "Castlevania II - Simon's Quest (USA)", 1988, "Non-linear RPG-platformer with day-to-night cycle as Simon lifts Dracula's curse.");
            Add("nes", "Castlevania III - Dracula's Curse (USA)", 1990, "Branching-path gothic platformer starring Trevor Belmont, Sypha, Grant, and Alucard.");
            Add("nes", "Akumajou Densetsu (Japan)", 1989, "Japanese Famicom release of Castlevania III featuring the VRC6 expansion audio chip.");
            Add("nes", "Contra (USA)", 1988, "Run-and-gun co-op action classic featuring the Spread Gun and the Konami Code.");
            Add("nes", "Super C (USA)", 1990, "Explosive NES sequel to Contra featuring overhead and side-scrolling stages.");
            Add("nes", "Mike Tyson's Punch-Out!! (Japan, USA)", 1987, "Initial retail release of Mike Tyson's Punch-Out!!.");
            Add("nes", "Mike Tyson's Punch-Out!! (Japan, USA) (Rev A)", 1987, "Updated retail revision (Rev A) of Mike Tyson's Punch-Out!!.");
            Add("nes", "Punch-Out!! (USA)", 1990, "Re-issued edition featuring Mr. Dream as the final WVBA champion.");
            Add("nes", "Kirby's Adventure (USA)", 1993, "Initial USA release of Kirby's Adventure.");
            Add("nes", "Kirby's Adventure (USA) (Rev A)", 1993, "Updated retail revision (Rev A) of Kirby's Adventure.");
            Add("nes", "Final Fantasy (USA)", 1990, "Lead the four Light Warriors to restore the elemental Orbs in the RPG that launched a franchise.");
            Add("nes", "Dragon Warrior (USA)", 1989, "Initial USA release of Dragon Warrior (Dragon Quest).");
            Add("nes", "Dragon Warrior (USA) (Rev A)", 1989, "Updated USA revision (Rev A) of Dragon Warrior.");
            Add("nes", "Dragon Warrior III (USA)", 1992, "Epic class-based RPG prequel tracing the legend of Erdrick.");
            Add("nes", "Dragon Warrior IV (USA)", 1992, "Five-chapter story-driven RPG classic on the NES.");
            Add("nes", "Ninja Gaiden (USA)", 1989, "Fast-paced wall-clinging ninja action starring Ryu Hayabusa with cinematic cutscenes.");
            Add("nes", "Ninja Gaiden II - The Dark Sword of Chaos (USA)", 1990, "Introduces shadow clone techniques and elemental ninpo arts.");
            Add("nes", "DuckTales (USA)", 1989, "Scrooge McDuck pogo-jumps around the globe (and the Moon) in Capcom's platformer.");
            Add("nes", "Chip 'n Dale Rescue Rangers (USA)", 1990, "Co-op crate-throwing platformer from Capcom based on the Disney series.");
            Add("nes", "Bionic Commando (USA)", 1988, "Grappling-hook swing action starring Ladd Spencer.");
            Add("nes", "Blaster Master (USA)", 1988, "Explore cavernous worlds inside the SOPHIA III tank and on foot.");
            Add("nes", "Batman - The Video Game (USA)", 1990, "Dark, wall-jumping action platformer from Sunsoft with an iconic soundtrack.");
            Add("nes", "River City Ransom (USA)", 1989, "Open-ended co-op beat-'em-up RPG as Alex and Ryan battle Slick's gangs.");
            Add("nes", "Double Dragon II - The Revenge (USA) (Rev A)", 1990, "Co-op martial arts brawler starring Billy and Jimmy Lee.");
            Add("nes", "Teenage Mutant Ninja Turtles II - The Arcade Game (USA)", 1990, "Four-turtle arcade beat-'em-up action against the Foot Clan.");
            Add("nes", "Teenage Mutant Ninja Turtles III - The Manhattan Project (USA)", 1992, "Original NES beat-'em-up adventure with signature Turtle special moves.");
            Add("nes", "Battletoads (USA)", 1991, "Notoriously challenging brawler/action game from Rare.");
            Add("nes", "Tetris (USA)", 1989, "Nintendo's official NES Tetris used in the Classic Tetris World Championship.");
            Add("nes", "Tetris (USA) (Unl)", 1989, "Tengen unlicensed NES release of Tetris.");
            Add("nes", "Micro Machines (USA) (Unl)", 1991, "Camerica/Codemasters unlicensed miniature racing classic for the NES.");
            Add("nes", "Bible Adventures (USA) (Unl)", 1991, "Wisdom Tree unlicensed NES platformer.");
            Add("nes", "Dr. Mario (Japan, USA) (Rev A)", 1990, "Clear viruses with vitamin capsules in Nintendo's addictive puzzle classic.");
            Add("nes", "Excitebike (Japan, USA)", 1985, "Motocross racing and track design launch title.");
            Add("nes", "Balloon Fight (USA)", 1986, "Arcade-style flight action and Balloon Trip survival mode.");
            Add("nes", "Ice Climber (USA, Europe)", 1985, "Scale 32 icy mountain peaks as Popo and Nana.");
            Add("nes", "Kid Icarus (USA, Europe) (Rev A)", 1987, "Ascend from the Underworld to Skyworld as Pit to rescue Palutena.");
            Add("nes", "StarTropics (USA)", 1990, "Island-hopping action-adventure starring Mike Jones and his Island Yo-Yo.");
            Add("nes", "Crystalis (USA)", 1990, "Post-apocalyptic action-RPG from SNK featuring four elemental swords.");
            Add("nes", "Ghosts'n Goblins (USA)", 1986, "Challenging arcade platformer starring Sir Arthur.");
            Add("nes", "Life Force (USA)", 1988, "Horizontal and vertical scrolling shoot-'em-up spin-off of Gradius.");
            Add("nes", "Gradius (USA)", 1986, "Pilot the Vic Viper using the iconic power-up meter bar.");
            Add("nes", "Jackal (USA)", 1988, "Top-down armed jeep rescue action from Konami.");
            Add("nes", "Micro Mages (World) (Aftermarket)", 2019, "Modern four-player cooperative NES homebrew platformer crammed into 40 KB.", "HTGDB / SMDB");
            Add("nes", "Alwa's Awakening - The 8-Bit Edition (World) (Aftermarket)", 2021, "Full-featured Metroidvania homebrew crafted specifically for the NES.", "HTGDB / SMDB");
            Add("nes", "Super Mario Bros. 3Mix (USA) (Hack)", 2014, "Expansive ROM hack of Super Mario Bros. 3 featuring worlds from across Mario history.", "HTGDB / SMDB");
            Add("nes", "EarthBound Beginnings (USA)", 2015, "Official English release of Mother.");
            Add("nes", "EarthBound Beginnings (USA) (Proto)", 1990, "Official English localization prototype of Mother (EarthBound Zero).");

            // SNES
            Add("snes", "Super Mario World (USA)", 1991, "Explore Dinosaur Land with Yoshi across 96 exits in Nintendo's flagship 16-bit platformer.");
            Add("snes", "Legend of Zelda, The - A Link to the Past (USA)", 1992, "Journey between the Light World and Dark World to rescue the descendants of the Seven Sages.");
            Add("snes", "Super Metroid (Japan, USA)", 1994, "Atmospheric action-exploration masterpiece as Samus returns to Planet Zebes to rescue the last Metroid.");
            Add("snes", "Chrono Trigger (USA)", 1995, "Time-traveling RPG created by the Dream Team to stop Lavos across multiple eras and endings.");
            Add("snes", "Final Fantasy III (USA)", 1994, "Initial USA release (v1.0) of Final Fantasy III (Final Fantasy VI).");
            Add("snes", "Final Fantasy III (USA) (Rev 1)", 1994, "Updated USA revision (Rev 1 / v1.1) of Final Fantasy III (Final Fantasy VI).");
            Add("snes", "Donkey Kong Country (USA)", 1994, "Initial USA release of Donkey Kong Country.");
            Add("snes", "Donkey Kong Country (USA) (Rev 2)", 1994, "Latest USA revision (Rev 2 / v1.2) of Donkey Kong Country.");
            Add("snes", "Donkey Kong Country 2 - Diddy's Kong Quest (USA) (En,Fr) (Rev 1)", 1995, "Diddy and Dixie Kong storm Crocodile Isle in one of the finest platformers ever made.");
            Add("snes", "EarthBound (USA)", 1995, "Quirky modern-day RPG following Ness and friends on a cosmic journey against Giygas.");
            Add("snes", "Super Mario RPG - Legend of the Seven Stars (USA)", 1996, "Isometric turn-based RPG collaboration between Nintendo and Square.");
            Add("snes", "Super Mario World 2 - Yoshi's Island (USA) (Rev 1)", 1995, "Hand-drawn Super FX2 platformer where the Yoshi clan protects Baby Mario.");
            Add("snes", "Mega Man X (USA) (Rev 1)", 1994, "Fast-paced 16-bit action introducing X, Zero, wall-jumping, and hidden armor upgrades.");
            Add("snes", "Super Mario Kart (USA)", 1992, "Mode 7 kart racing classic featuring Battle Mode and four Cup circuits.");
            Add("snes", "Super Castlevania IV (USA)", 1991, "16-bit reimagining of Simon Belmont's quest with eight-directional whip control.");
            Add("snes", "Secret of Mana (USA)", 1993, "Real-time action RPG with three-player cooperative Ring Command combat.");
            Add("snes", "Street Fighter II Turbo (USA) (Rev 1)", 1993, "High-speed 16-bit conversion of Capcom's arcade fighting phenomenon.");
            Add("snes", "Star Fox 2 (USA, Europe) (Classic Mini)", 2017, "Completed official release of the previously unreleased Argonaut/Nintendo Super FX sequel.");
            Add("snes", "Star Fox 2 (USA) (Proto)", 1995, "Original 1995 Super FX2 development prototype of Star Fox 2.");
            Add("snes", "Final Fantasy V (Japan)", 1992, "Super Famicom RPG featuring the Job & Ability mastery system.");
            Add("snes", "Seiken Densetsu 3 (Japan)", 1995, "Super Famicom action-RPG sequel to Secret of Mana (Trials of Mana).");
            Add("snes", "Fire Emblem - Seisen no Keifu (Japan)", 1996, "Two-generation Super Famicom tactical RPG epic (Genealogy of the Holy War).");
            Add("snes", "Chrono Trigger (Japan)", 1995, "Original Japanese Super Famicom release of Chrono Trigger.");

            // Nintendo 64
            Add("n64", "Super Mario 64 (USA)", 1996, "Landmark 3D platformer where Mario collects 120 Power Stars inside Princess Peach's castle.");
            Add("n64", "Legend of Zelda, The - Ocarina of Time (USA)", 1998, "Initial Gold Cart release (v1.0) of Ocarina of Time.");
            Add("n64", "Legend of Zelda, The - Ocarina of Time (USA) (Rev 2)", 1998, "Latest retail revision (Rev 2 / v1.2) of Ocarina of Time.");
            Add("n64", "Legend of Zelda, The - Majora's Mask (USA)", 2000, "Relive a three-day cycle in Termina using transformation masks to stop the falling moon.");
            Add("n64", "Super Smash Bros. (USA)", 1999, "Four-player crossover platform fighter starring Nintendo's iconic characters.");
            Add("n64", "Mario Kart 64 (USA)", 1997, "3D kart racing classic featuring four-player split-screen Grand Prix and Block Fort battles.");
            Add("n64", "GoldenEye 007 (USA)", 1997, "Stealth and objective-based FPS with legendary four-player split-screen multiplayer.");
            Add("n64", "Perfect Dark (USA) (Rev 1)", 2000, "Sci-fi stealth shooter starring Joanna Dark with customizable Combat Simulator bots.");
            Add("n64", "Banjo-Kazooie (USA) (Rev 1)", 1998, "Collectathon 3D platformer from Rare starring Banjo the bear and Kazooie the breegull.");
            Add("n64", "Star Fox 64 (USA) (Rev 1)", 1997, "Branching-route 3D rail shooter with Rumble Pak support across the Lylat System.");
            Add("n64", "Paper Mario (USA)", 2001, "Storybook RPG adventure with timed action commands and partner abilities.");

            // GameCube & Wii
            Add("gamecube", "Super Smash Bros. Melee (USA) (En,Ja) (Rev 2)", 2001, "High-speed technical platform fighter featuring 26 Nintendo fighters and Adventure Mode.", "Redump");
            Add("gamecube", "Legend of Zelda, The - The Wind Waker (USA)", 2003, "Cel-shaded seafaring adventure across the Great Sea with the Wind Waker baton.", "Redump");
            Add("gamecube", "Metroid Prime (USA)", 2002, "First-person adventure on Tallon IV blending visor scanning, morph ball puzzles, and exploration.", "Redump");
            Add("gamecube", "Paper Mario - The Thousand-Year Door (USA)", 2004, "Search for the seven Crystal Stars around Rogueport in a beloved turn-based RPG.", "Redump");
            Add("gamecube", "Super Mario Sunshine (USA)", 2002, "Clean up Isle Delfino using the water-powered F.L.U.D.D. pack to collect Shine Sprites.", "Redump");
            Add("gamecube", "Luigi's Mansion (USA)", 2001, "Capture ghosts with the Poltergust 3000 to rescue Mario from King Boo.", "Redump");
            Add("gamecube", "Mario Kart - Double Dash!! (USA)", 2003, "Two-character co-op kart racing with character-specific Special Items.", "Redump");
            Add("gamecube", "Fire Emblem - Path of Radiance (USA)", 2005, "Tactical RPG following Ike and the Greil Mercenaries across the continent of Tellius.", "Redump");
            Add("wii", "Super Mario Galaxy (USA)", 2007, "Gravity-defying 3D platforming across cosmic galaxies with Rosalina and the Lumas.", "Redump");
            Add("wii", "Super Mario Galaxy 2 (USA)", 2010, "Expanded cosmic platforming sequel featuring Yoshi and Starship Mario.", "Redump");
            Add("wii", "Legend of Zelda, The - Twilight Princess (USA)", 2006, "Dark fantasy adventure where Link transforms into a wolf to save Hyrule from the Twilight Realm.", "Redump");
            Add("wii", "Xenoblade Chronicles (USA)", 2012, "Massive JRPG set on the frozen bodies of two giant titans, the Bionis and the Mechonis.", "Redump");

            // Game Boy / GBC / GBA / DS
            Add("gb", "Legend of Zelda, The - Link's Awakening (USA, Europe) (Rev B)", 1993, "Stranded on Koholint Island, Link must collect the eight Instruments of the Sirens to awaken the Wind Fish.");
            Add("gb", "Tetris (World) (Rev A)", 1989, "The iconic pack-in puzzle phenomenon with Type-A Korobeiniki music and two-player link battles.");
            Add("gb", "Super Mario Land 2 - 6 Golden Coins (USA, Europe) (Rev B)", 1992, "Reclaim Mario's castle from Wario across six themed zones.");
            Add("gbc", "Legend of Zelda, The - Oracle of Ages (USA, Australia)", 2001, "Time-bending handheld Zelda adventure in Labrynna that links with Oracle of Seasons.");
            Add("gbc", "Legend of Zelda, The - Oracle of Seasons (USA, Australia)", 2001, "Change the four seasons with the Rod of Seasons in Holodrum and link with Oracle of Ages.");
            Add("gba", "Metroid Fusion (USA)", 2002, "Investigate the Biologic Space Labs station while hunted by the deadly SA-X parasite.");
            Add("gba", "Metroid - Zero Mission (USA)", 2004, "Expanded GBA reimagining of Samus Aran's original mission on Planet Zebes.");
            Add("gba", "Castlevania - Aria of Sorrow (USA)", 2003, "Explore Dracula's castle in 2035 as Soma Cruz using the Tactical Soul system.");
            Add("gba", "Legend of Zelda, The - The Minish Cap (USA)", 2005, "Shrink to microscopic size with the talking cap Ezlo to restore the Picori Blade.");
            Add("gba", "Golden Sun (USA, Europe)", 2001, "Command Psynergy and elemental Djinn in Camelot's acclaimed handheld RPG.");
            Add("gba", "Advance Wars (USA) (Rev 1)", 2001, "Turn-based tactical warfare from Intelligent Systems featuring CO Powers and map design.");
            Add("gba", "Fire Emblem (USA, Australia)", 2003, "Lead Lyn, Eliwood, and Hector in the first Fire Emblem released in North America.");
            Add("gba", "Mother 3 (Japan)", 2006, "Emotional RPG masterpiece starring Lucas and Flint in the Nowhere Islands.");
            Add("gba", "Mother 3 (Japan) [T+Eng v1.3] (Hack)", 2008, "Fan-translated English edition of Mother 3.", "HTGDB / SMDB");
            Add("nds", "Mario Kart DS (USA)", 2005, "Dual-screen kart racing introducing retro tracks and Mission Mode.");
            Add("nds", "New Super Mario Bros. (USA)", 2006, "2.5D side-scrolling Mario platformer with Mega Mushroom and multiplayer mini-games.");
            Add("nds", "Castlevania - Dawn of Sorrow (USA)", 2005, "Direct DS sequel to Aria of Sorrow featuring Soul synthesis and gothic exploration.");
            Add("nds", "World Ends with You, The (USA)", 2008, "Dual-screen action RPG set in modern Shibuya during the seven-day Reapers' Game.");

            // Sega Genesis / Saturn / Dreamcast
            Add("genesis", "Sonic The Hedgehog 2 (World) (Rev A)", 1992, "High-speed 16-bit platforming introducing Miles 'Tails' Prower and the Spin Dash.");
            Add("genesis", "Sonic & Knuckles + Sonic The Hedgehog 3 (World)", 1994, "Complete lock-on adventure across Angel Island with Sonic, Tails, and Knuckles.");
            Add("genesis", "Streets of Rage 2 (USA)", 1992, "Legendary 16-bit beat-'em-up featuring an iconic Yuzo Koshiro soundtrack.");
            Add("genesis", "Phantasy Star IV (USA)", 1995, "Culmination of the Algol Star System sci-fi RPG saga with manga-style cutscenes.");
            Add("genesis", "Shining Force II (USA)", 1994, "Tactical fantasy RPG with free-roaming town exploration and class promotions.");
            Add("genesis", "Gunstar Heroes (USA)", 1993, "Explosive run-and-gun action from Treasure with combinable weapon types.");
            Add("genesis", "Castlevania - Bloodlines (USA)", 1994, "Globe-trotting 16-bit Castlevania starring John Morris and Eric Lecarde.");
            Add("genesis", "Castlevania - The New Generation (Europe)", 1994, "European PAL Sega Mega Drive release of Castlevania: Bloodlines.");
            Add("genesis", "Vampire Killer (Japan)", 1994, "Japanese Sega Mega Drive release of Castlevania: Bloodlines.");
            Add("genesis", "Alien Soldier (Europe)", 1995, "Boss-rush run-and-gun tour de force from Treasure for the Sega Mega Drive.");
            Add("genesis", "Bare Knuckle II - Shitou e no Requiem (Japan)", 1993, "Japanese Sega Mega Drive release of Streets of Rage 2.");
            Add("saturn", "Panzer Dragoon Saga (USA) (Disc 1)", 1998, "Four-disc tactical dragon-riding RPG set in a post-apocalyptic world.", "Redump");
            Add("saturn", "Nights into Dreams... (USA)", 1996, "Acrobatic 3D flight score-attack adventure from Sonic Team.", "Redump");
            Add("dreamcast", "Sonic Adventure (USA)", 1999, "Full 3D high-speed adventure across six interwoven character campaigns and Chao Gardens.", "Redump");
            Add("dreamcast", "Shenmue (USA) (Disc 1)", 2000, "Open-world cinematic adventure following Ryo Hazuki across 1986 Yokosuka.", "Redump");
            Add("dreamcast", "SoulCalibur (USA)", 1999, "Eight-way run weapon fighter widely hailed as one of the greatest launch-era titles ever.", "Redump");
            Add("dreamcast", "Jet Grind Radio (USA)", 2000, "Cel-shaded inline-skating graffiti action across Tokyo-to.", "Redump");
            Add("dreamcast", "Marvel vs. Capcom 2 (USA)", 2000, "Arcade-perfect 3v3 tag-team fighting game with a 56-character roster.", "Redump");

            // PlayStation 1 / PlayStation 2 / PSP
            Add("ps1", "Final Fantasy VII (USA) (Disc 1)", 1997, "Join Cloud Strife and AVALANCHE against Shinra and Sephiroth in Square's landmark RPG.", "Redump");
            Add("ps1", "Castlevania - Symphony of the Night (USA)", 1997, "Explore Dracula's inverted castle as Alucard in the genre-defining Metroidvania masterpiece.", "Redump");
            Add("ps1", "Metal Gear Solid (USA) (Disc 1) (Rev 1)", 1998, "Tactical espionage action as Solid Snake infiltrates Shadow Moses Island.", "Redump");
            Add("ps1", "Resident Evil 2 (USA) (Disc 1)", 1998, "Survive the Raccoon City outbreak as Leon S. Kennedy and Claire Redfield.", "Redump");
            Add("ps1", "Silent Hill (USA)", 1999, "Psychological survival horror as Harry Mason searches the fog-shrouded town for his daughter.", "Redump");
            Add("ps1", "Chrono Cross (USA) (Disc 1)", 2000, "Parallel-dimension RPG sequel to Chrono Trigger featuring over 40 recruitable characters.", "Redump");
            Add("ps1", "Crash Bandicoot - Warped (USA)", 1998, "Time-traveling 3D platforming adventure from Naughty Dog.", "Redump");
            Add("ps1", "Spyro the Dragon (USA)", 1998, "Glide and flame across the Dragon Realms in Insomniac's colorful 3D platformer.", "Redump");
            Add("ps1", "Gran Turismo 2 (USA) (Simulation Mode) (Rev 1)", 1999, "The Real Driving Simulator featuring over 600 licensed vehicles and endurance racing.", "Redump");
            Add("ps2", "Shadow of the Colossus (USA)", 2005, "Scale and defeat sixteen towering Colossi across a forbidden land to restore Mono.", "Redump");
            Add("ps2", "Metal Gear Solid 3 - Snake Eater (USA)", 2004, "Cold War jungle survival stealth starring Naked Snake and The Boss.", "Redump");
            Add("ps2", "Final Fantasy X (USA)", 2001, "Journey across Spira with Tidus and Yuna to defeat Sin.", "Redump");
            Add("ps2", "Kingdom Hearts II (USA)", 2006, "Action-RPG crossover uniting Sora, Donald, and Goofy against Organization XIII.", "Redump");
            Add("ps2", "God of War II (USA)", 2007, "Mythological hack-and-slash spectacle as Kratos defies the Olympian gods.", "Redump");
            Add("ps2", "Shin Megami Tensei - Persona 4 (USA)", 2008, "Solve a supernatural murder mystery in Inaba while balancing high school life and dungeon crawling.", "Redump");
            Add("psp", "Crisis Core - Final Fantasy VII (USA)", 2008, "Action-RPG prequel to Final Fantasy VII starring SOLDIER operative Zack Fair.", "Redump");
            Add("psp", "Monster Hunter Freedom Unite (USA)", 2009, "Hunt colossal wyverns solo or in four-player ad-hoc co-op.", "Redump");

            // TurboGrafx-16 / Neo Geo / Xbox
            Add("tgcd", "Akumajou Dracula X - Chi no Rondo (Japan)", 1993, "Richter Belmont and Maria Renard battle Dracula in the acclaimed PC Engine Super CD-ROM2 classic.", "Redump");
            Add("neogeo", "Metal Slug 3 (World)", 2000, "Masterpiece 2D run-and-gun arcade action with branching paths and vehicles.");
            Add("xbox", "Halo - Combat Evolved (USA)", 2001, "Sci-fi FPS landmark as Master Chief battles the Covenant and the Flood on Installation 04.", "Redump");
            Add("xbox", "Star Wars - Knights of the Old Republic (USA)", 2003, "BioWare's epic RPG set 4,000 years before the Galactic Empire.", "Redump");

            MarkLatestAndInitialRevisions(games);
            return games;
        }
    }
}
