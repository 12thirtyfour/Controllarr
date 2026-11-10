using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Games;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Game
{
    public class GameCatalogProxy : IGameCatalogService, IProvidePlatformInfo, ISearchForNewPlatforms
    {
        public const int RegionUnlicensed = 0;
        public const int RegionPrototypes = 0;
        public const int RegionUsa = 1;
        public const int RegionEurope = 2;
        public const int RegionJapan = 3;
        public const int RegionWorld = 4;
        public const int RegionHomebrew = 5;
        public const int RegionRomHacks = 6;
        public const int RegionBios = 99;

        private static readonly Regex DatGameBlockRegex = new Regex(
            @"game\s*\(\s*name\s+""(?<name>[^""]+)""(?<body>.*?)\r?\n\)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex DatRomNameRegex = new Regex(
            @"rom\s*\(\s*name\s+""(?<rom>[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatReleaseYearBlockRegex = new Regex(
            @"game\s*\((?<body>[\s\S]*?)\n\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatNameOrCommentRegex = new Regex(
            @"(?:comment|name)\s+""(?<name>[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatReleaseYearRegex = new Regex(
            @"releaseyear\s+""?(?<year>\d{4})""?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatReleaseMonthRegex = new Regex(
            @"releasemonth\s+""?(?<month>\d{1,2})""?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatReleaseDayRegex = new Regex(
            @"releaseday\s+""?(?<day>\d{1,2})""?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex DatCrcRegex = new Regex(
            @"crc\s+([0-9a-fA-F]{8})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ResidualRevisionRegex = new Regex(
            @"(?:\s*[\(\[]\s*(?:Rev\.?\s*[0-9A-Za-z\.]+|v\d+(?:\.\d+)*[A-Za-z]?|Version\s*[\d\.]+|Alt\s*\d+|PRG\d+|(?:(?:Update|Patch|DLC)(?:\s*v?[\d.]+)?|v?[\d.]+\s*(?:Update|Patch)|UPD))\s*[\)\]]|\s+-\s+(?:Rev\.?\s*[0-9A-Za-z]+|v\d+(?:\.\d+)*[A-Za-z]?|(?:(?:Update|Patch|DLC)(?:\s*v?[\d.]+)?|UPD))$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static readonly Regex RevisionTagRegex = new Regex(
            @"\((?<rev>Rev\s*[A-Z0-9\.]+|v\d+[\.\dA-Za-z]*|Version\s*[\d\.]+|Alt\s*\d*|Program\s*ROM\s*[^\)]+|PRG\d+)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static readonly Regex UpdateTagRegex = new Regex(
            @"(?:\(|[\s\[])?\b(?<upd>(?:(?:Update|Patch|DLC)(?:\s*v?[\d.]+)?|v?[\d.]+\s*(?:Update|Patch)|UPD))\b(?:\)|\])?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static readonly Regex DiscTagRegex = new Regex(
            @"(?:\(|\[)?\b(?<disc>Disc\s*(?<num>\d+(?:\s*of\s*\d+)?)|Disk\s*(?<num>[A-Z0-9]+(?:\s*of\s*\d+)?))\b(?:\)|\])?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PreReleaseTagRegex = new Regex(
            @"\((?<pre>Proto[^\)]*|Beta[^\)]*|Sample[^\)]*|Demo[^\)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex GeographicRegionTagRegex = new Regex(
            @"\((?:USA|Europe|Japan|World|Asia|Australia|Brazil|Canada|China|Denmark|Finland|France|Germany|Greece|Hong Kong|India|Italy|Korea|Latin America|Mexico|Netherlands|Norway|Poland|Portugal|Russia|Scandinavia|Spain|Sweden|Taiwan|UK|United Kingdom)(?:\s*,\s*(?:USA|Europe|Japan|World|Asia|Australia|Brazil|Canada|China|Denmark|Finland|France|Germany|Greece|Hong Kong|India|Italy|Korea|Latin America|Mexico|Netherlands|Norway|Poland|Portugal|Russia|Scandinavia|Spain|Sweden|Taiwan|UK|United Kingdom))*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex TrailingLanguageTagRegex = new Regex(
            @"\((?<langs>(?:En|Fr|De|Es|It|Nl|Pt|Sv|Se|No|Da|Dk|Fi|Ja|Jp|Ko|Kr|Zh|Zhcn|Zhtw|Zhhk|Ru|Pl|Ar|Tr|Cs|Hu|El|Ro|Th|Id|Ms|Vi|Hi|He)(?:\s*[,+]\s*(?:En|Fr|De|Es|It|Nl|Pt|Sv|Se|No|Da|Dk|Fi|Ja|Jp|Ko|Kr|Zh|Zhcn|Zhtw|Zhhk|Ru|Pl|Ar|Tr|Cs|Hu|El|Ro|Th|Id|Ms|Vi|Hi|He))*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> UsaRegionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USA", "US", "NTSC-U", "NTSC", "CAN", "CANADA", "BRA", "BRAZIL", "MEX", "MEXICO", "LATIN AMERICA"
        };

        private static readonly HashSet<string> EuropeRegionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EUR", "EU", "EUROPE", "PAL", "PAL-R", "GBR", "UK", "UKV", "UNITED KINGDOM", "AUS", "AUSTRALIA",
            "GER", "DEU", "GERMANY", "FRA", "FRANCE", "SPA", "ESP", "SPAIN", "ITA", "ITALY", "HOL", "NLD",
            "NETHERLANDS", "RUS", "RUSSIA", "SWE", "SWEDEN", "NOR", "NORWAY", "DAN", "DEN", "DENMARK",
            "FIN", "FINLAND", "POL", "POLAND", "POR", "PRT", "PORTUGAL", "SCAN", "SCANDINAVIA", "GRE", "GREECE"
        };

        private static readonly HashSet<string> JapanRegionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "JPN", "JP", "JAPAN", "NTSC-J", "KOR", "KR", "KOREA", "NTSC-K", "TWN", "TW", "TAIWAN", "NTSC-T",
            "CHN", "CN", "CHINA", "HKG", "HK", "HONG KONG", "ASI", "AS", "ASIA"
        };

        private static readonly HashSet<string> WorldRegionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WLD", "WORLD", "ALL"
        };

        private static readonly HashSet<string> AsianLanguageTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "JA", "JP", "KO", "KR", "ZH", "ZHCN", "ZHTW", "ZHHK", "CH", "TW", "HK"
        };

        private static readonly HashSet<string> EuropeanNonEnglishLanguageTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FR", "DE", "ES", "IT", "NL", "PT", "RU", "PL", "SV", "SE", "DA", "DK", "NO", "FI", "CS", "HU", "EL", "RO", "TR"
        };

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
        private static readonly ConcurrentDictionary<string, List<GameCatalogEntry>> SyncedPlatformGamesCache = new ConcurrentDictionary<string, List<GameCatalogEntry>>();
        private static readonly ConcurrentDictionary<int, HashSet<int>> PendingPlatformGameSelections = new ConcurrentDictionary<int, HashSet<int>>();

        private readonly IDatCacheService _datCacheService;
        private readonly IDatParserService _datParserService;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public GameCatalogProxy(
            IDatCacheService datCacheService,
            IHttpClient httpClient,
            Logger logger,
            IDatParserService datParserService = null)
        {
            _datCacheService = datCacheService;
            _httpClient = httpClient;
            _logger = logger;
            _datParserService = datParserService;
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

        public void InvalidatePlatformCache(int? platformTvdbId = null)
        {
            if (platformTvdbId.HasValue)
            {
                var prefix = $"{platformTvdbId.Value}:";
                foreach (var key in SyncedPlatformGamesCache.Keys.Where(k => k.StartsWith(prefix)).ToList())
                {
                    SyncedPlatformGamesCache.TryRemove(key, out _);
                }

                foreach (var kvp in DynamicGames.Where(kv => kv.Value.PlatformTvdbId == platformTvdbId.Value).ToList())
                {
                    DynamicGames.TryRemove(kvp.Key, out _);
                }
            }
            else
            {
                SyncedPlatformGamesCache.Clear();
                DynamicGames.Clear();
            }
        }

        public int SyncPlatformDatCatalogs(int platformTvdbId, bool forceRefresh = false, string seasonType = null)
        {
            if (!TryGetPlatformEntry(platformTvdbId, out var platform))
            {
                return 0;
            }

            if (forceRefresh)
            {
                InvalidatePlatformCache(platformTvdbId);
            }

            var encryptionPref = ParseEncryptionPreference(seasonType);
            var synced = SyncOnlineCatalogForPlatform(
                platform,
                encryptionPref,
                forceRefresh);

            return synced?.Count ?? 0;
        }

        public PlatformCatalogEntry InferPlatformForText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (TryGetPlatformEntryBySlug(text, out var exact))
            {
                return exact;
            }

            foreach (var platform in BuiltInPlatforms)
            {
                if ((!string.IsNullOrWhiteSpace(platform.LibretroSystemName) &&
                     text.Contains(platform.LibretroSystemName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(platform.Name) &&
                     text.Contains(platform.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return platform;
                }
            }

            return InferPlatformFromText(text);
        }

        public Episode CreateEpisodeForGame(GameCatalogEntry game, int seriesId, bool monitored)
        {
            var hasValidYear = game.ReleaseYear >= 1970 && game.ReleaseYear <= 2035 && game.FirstAired.Year >= 1970;
            var airDateString = !string.IsNullOrWhiteSpace(game.ReleaseDateString)
                ? game.ReleaseDateString
                : (hasValidYear ? game.ReleaseYear.ToString() : null);

            return new Episode
            {
                SeriesId = seriesId,
                TvdbId = game.TvdbId,
                SeasonNumber = game.RegionNumber,
                EpisodeNumber = game.GameNumber,
                AbsoluteEpisodeNumber = game.AbsoluteGameNumber > 0 ? game.AbsoluteGameNumber : game.GameNumber,
                Title = game.Title,
                FinaleType = !string.IsNullOrWhiteSpace(game.RevisionTag) ? game.RevisionTag : "Retail (v1.0)",
                Overview = game.Overview,
                AirDate = airDateString,
                AirDateUtc = hasValidYear ? game.FirstAired : null,
                Runtime = 0,
                Monitored = monitored,
                Ratings = new Ratings { Votes = 100, Value = 9.0m }
            };
        }

        public Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Language language, string seasonType)
        {
            var encryptionPreference = ParseEncryptionPreference(seasonType);

            if (TryGetGameEntry(tvdbSeriesId, out var gameEntry) &&
                TryGetPlatformEntry(gameEntry.PlatformTvdbId, out var parentPlatform))
            {
                RegisterGameSelection(gameEntry.TvdbId);
                return BuildPlatformAndGames(parentPlatform, encryptionPreference);
            }

            if (TryGetPlatformEntry(tvdbSeriesId, out var platformEntry))
            {
                return BuildPlatformAndGames(platformEntry, encryptionPreference);
            }

            var fallbackPlatform = CreateFallbackPlatformEntry(tvdbSeriesId);

            return BuildPlatformAndGames(fallbackPlatform, encryptionPreference);
        }

        public static Games.EncryptionPreference ParseEncryptionPreference(string seasonType)
        {
            if (string.Equals(seasonType, "both", StringComparison.OrdinalIgnoreCase))
            {
                return Games.EncryptionPreference.Both;
            }

            if (string.Equals(seasonType, "encrypted", StringComparison.OrdinalIgnoreCase))
            {
                return Games.EncryptionPreference.Encrypted;
            }

            return Games.EncryptionPreference.NotEncrypted;
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
                Overview = "Console & handheld hardware platform (1995).",
                Extensions = new List<string> { ".zip", ".7z", ".chd", ".iso" }
            };
        }

        public List<Series> SearchForNewSeriesByImdbId(string imdbId, Language language)
        {
            return SearchForNewSeries(imdbId, language);
        }

        public List<Series> SearchForNewSeriesByTmdbId(int tmdbId, Language language)
        {
            return SearchForNewSeries($"tvdb:{tmdbId}", language);
        }

        private Tuple<Series, List<Episode>> BuildPlatformAndGames(
            PlatformCatalogEntry platform,
            Games.EncryptionPreference encryptionPreference = Games.EncryptionPreference.NotEncrypted)
        {
            var series = MapPlatformToSeries(platform);
            var episodes = new List<Episode>();

            var platformGames = new List<GameCatalogEntry>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIds = new HashSet<int>();
            var seenCanonicalReleases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSpecialCategoryTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool TryRegisterCrossReferencedGame(GameCatalogEntry candidate)
            {
                if (candidate == null)
                {
                    return false;
                }

                var canonicalTitle = NormalizeCanonicalCrossRefTitle(candidate.BaseTitle ?? candidate.Title);
                var rawRev = candidate.RevisionTag ?? "Retail (v1.0)";
                var canonicalRev = rawRev.Equals("v1.0", StringComparison.OrdinalIgnoreCase) ||
                                   rawRev.Equals("Retail", StringComparison.OrdinalIgnoreCase)
                    ? "Retail (v1.0)"
                    : rawRev.Trim();
                var canonicalKey = $"{candidate.RegionNumber}:{canonicalTitle}:{canonicalRev}";

                if ((candidate.RegionNumber == RegionUsa || candidate.RegionNumber == RegionWorld || candidate.RegionNumber == RegionEurope) &&
                    seenSpecialCategoryTitles.Contains(canonicalTitle))
                {
                    return false;
                }

                if (seenCanonicalReleases.Contains(canonicalKey) ||
                    seenIds.Contains(candidate.TvdbId) ||
                    seenNames.Contains(candidate.NoIntroName ?? candidate.Title))
                {
                    return false;
                }

                if (candidate.RegionNumber == RegionHomebrew ||
                    candidate.RegionNumber == RegionUnlicensed ||
                    candidate.RegionNumber == RegionRomHacks)
                {
                    seenSpecialCategoryTitles.Add(canonicalTitle);
                }

                seenCanonicalReleases.Add(canonicalKey);
                seenIds.Add(candidate.TvdbId);
                seenNames.Add(candidate.NoIntroName ?? candidate.Title);

                if (!string.IsNullOrWhiteSpace(candidate.ExpectedSha256))
                {
                    seenHashes.Add($"sha256:{candidate.ExpectedSha256.ToLowerInvariant()}");
                }

                if (!string.IsNullOrWhiteSpace(candidate.ExpectedSha1))
                {
                    seenHashes.Add($"sha1:{candidate.ExpectedSha1.ToLowerInvariant()}");
                }

                if (!string.IsNullOrWhiteSpace(candidate.ExpectedMd5))
                {
                    seenHashes.Add($"md5:{candidate.ExpectedMd5.ToLowerInvariant()}");
                }

                if (!string.IsNullOrWhiteSpace(candidate.ExpectedCrc32))
                {
                    seenHashes.Add($"crc:{candidate.ExpectedCrc32.ToLowerInvariant()}");
                }

                return true;
            }

            foreach (var builtIn in BuiltInGames.Where(g => g.PlatformTvdbId == platform.TvdbId))
            {
                if (TryRegisterCrossReferencedGame(builtIn))
                {
                    platformGames.Add(builtIn);
                }
            }

            var syncedGames = SyncOnlineCatalogForPlatform(
                platform,
                encryptionPreference);
            foreach (var synced in syncedGames)
            {
                if (TryRegisterCrossReferencedGame(synced))
                {
                    platformGames.Add(synced);
                }
            }

            foreach (var dynamicGame in DynamicGames.Values.Where(g => g.PlatformTvdbId == platform.TvdbId))
            {
                if (TryRegisterCrossReferencedGame(dynamicGame))
                {
                    platformGames.Add(dynamicGame);
                }
            }

            try
            {
                var cachedDatEntries = _datCacheService?.GetEntriesForPlatform(platform.Slug, platform.Name, platform.LibretroSystemName);
                if (cachedDatEntries != null)
                {
                    var datFileMap = _datCacheService?.GetAllDatFiles()?.ToDictionary(d => d.Id) ?? new Dictionary<int, DatFile>();
                    foreach (var datEntry in cachedDatEntries)
                    {
                        if (datEntry == null)
                        {
                            continue;
                        }

                        var hasDuplicateHash =
                            (!string.IsNullOrWhiteSpace(datEntry.Sha256) && !seenHashes.Add($"sha256:{datEntry.Sha256}")) ||
                            (!string.IsNullOrWhiteSpace(datEntry.Sha1) && !seenHashes.Add($"sha1:{datEntry.Sha1}")) ||
                            (!string.IsNullOrWhiteSpace(datEntry.Md5) && !seenHashes.Add($"md5:{datEntry.Md5}")) ||
                            (!string.IsNullOrWhiteSpace(datEntry.Crc32) && !seenHashes.Add($"crc:{datEntry.Crc32}"));

                        if (hasDuplicateHash)
                        {
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(datEntry.RomName) &&
                            datEntry.RomName.EndsWith(".unh", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var rawName = datEntry.GameName;
                        if (!string.IsNullOrWhiteSpace(datEntry.RomName))
                        {
                            var romClean = StripKnownRomExtension(Path.GetFileName(datEntry.RomName.Replace('\\', '/')));
                            if (string.IsNullOrWhiteSpace(rawName) ||
                                (!rawName.Contains("(Aftermarket)", StringComparison.OrdinalIgnoreCase) &&
                                 (romClean.Contains("(Aftermarket)", StringComparison.OrdinalIgnoreCase) ||
                                  romClean.Contains("(Unl)", StringComparison.OrdinalIgnoreCase) ||
                                  romClean.Contains("(Pirate)", StringComparison.OrdinalIgnoreCase) ||
                                  romClean.Contains("(Homebrew)", StringComparison.OrdinalIgnoreCase))))
                            {
                                rawName = romClean;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(rawName))
                        {
                            rawName = StripKnownRomExtension(Path.GetFileName(rawName.Replace('\\', '/')));
                            rawName = RomParser.CleanIndexPrefix(rawName);
                            if (!string.IsNullOrWhiteSpace(datEntry.Region))
                            {
                                var resolvedDatRegion = ResolveCatalogRegionLabel(datEntry.Region, null, null, rawName);
                                rawName = EnsureGeographicRegionTag(rawName, rawName, resolvedDatRegion);
                            }
                        }

                        if (string.IsNullOrWhiteSpace(rawName) ||
                            rawName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                            !IsValidGameTitle(rawName) ||
                            !IsValidGameTitle(datEntry.GameName) ||
                            !IsValidGameTitle(datEntry.RomName))
                        {
                            continue;
                        }

                        var effectiveCategory = "Cross-Referenced DAT / SMDB Catalog";
                        if (datFileMap.TryGetValue(datEntry.DatFileId, out var datFile))
                        {
                            effectiveCategory = !string.IsNullOrWhiteSpace(datFile.Category) ? datFile.Category : datFile.Name;
                        }

                        if (!string.IsNullOrWhiteSpace(datEntry.SmdbPath))
                        {
                            var smdbParts = datEntry.SmdbPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                            if (smdbParts.Any(s => s.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Contains("Hack", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Contains("Enhancement", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Contains("Color", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                                                   s.Contains("Fix", StringComparison.OrdinalIgnoreCase)))
                            {
                                effectiveCategory = "ROM Hacks & Translations";
                            }
                            else if (smdbParts.Any(s => s.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
                                                       s.Contains("Aftermarket", StringComparison.OrdinalIgnoreCase)))
                            {
                                effectiveCategory = "Homebrew & Aftermarket";
                            }
                            else if (smdbParts.Any(s => s.Contains("Unlicensed", StringComparison.OrdinalIgnoreCase) ||
                                                       s.Contains("Pirate", StringComparison.OrdinalIgnoreCase) ||
                                                       s.Contains("Bootleg", StringComparison.OrdinalIgnoreCase)))
                            {
                                effectiveCategory = "Unlicensed";
                            }
                        }

                        var converted = CreateCatalogEntryFromNoIntroName(platform, rawName, platform.ReleaseYear, effectiveCategory);
                        if (converted != null && TryRegisterCrossReferencedGame(converted))
                        {
                            DynamicGames[converted.TvdbId] = converted;
                            platformGames.Add(converted);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Unable to cross-reference DAT entries for platform {0}", platform.Name);
            }

            MarkLatestAndInitialRevisions(platformGames);

            var selectedGameIds = GetSelectedGameIdsForPlatform(platform.TvdbId);
            var hasExplicitSelection = selectedGameIds.Count > 0;
            var defaultMonitoredGameIds = GetDefaultMonitoredGameIds(platformGames, encryptionPreference);

            var gameCounters = new Dictionary<int, int>();
            var absoluteGameCounter = 0;

            foreach (var game in platformGames
                .OrderBy(g => StripArticle(g.BaseTitle ?? g.Title), StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => IsPreReleaseVersion(g.RevisionTag, g.NoIntroName) ? 2 : (IsEncryptedVersion(g.RevisionTag, g.NoIntroName) ? 1 : 0))
                .ThenBy(g => GetRevisionSortOrder(g.RevisionTag))
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase))
            {
                var seasonNum = game.RegionNumber;
                gameCounters.TryGetValue(seasonNum, out var currentGameNum);
                currentGameNum++;
                gameCounters[seasonNum] = currentGameNum;
                game.GameNumber = currentGameNum;

                absoluteGameCounter++;
                game.AbsoluteGameNumber = absoluteGameCounter;

                var isUnofficial = seasonNum == RegionUnlicensed ||
                                   seasonNum == RegionHomebrew ||
                                   seasonNum == RegionRomHacks ||
                                   IsPreReleaseVersion(game.RevisionTag, game.NoIntroName);

                var isMonitored = hasExplicitSelection
                    ? selectedGameIds.Contains(game.TvdbId)
                    : !isUnofficial && defaultMonitoredGameIds.Contains(game.TvdbId);

                episodes.Add(CreateEpisodeForGame(game, 0, isMonitored));
            }

            if (episodes.Count > 0)
            {
                var presentSeasonNumbers = episodes.Select(e => e.SeasonNumber).Distinct().ToHashSet();
                series.Seasons = series.Seasons.Where(s => presentSeasonNumbers.Contains(s.SeasonNumber)).ToList();
            }
            else
            {
                episodes.Add(new Episode
                {
                    SeriesId = 0,
                    TvdbId = platform.TvdbId + 1,
                    SeasonNumber = RegionUsa,
                    EpisodeNumber = 1,
                    AbsoluteEpisodeNumber = 1,
                    Title = $"{platform.Name} Launch Title",
                    FinaleType = "Retail (v1.0)",
                    Overview = platform.Overview,
                    AirDate = $"{platform.ReleaseYear}-01-01",
                    AirDateUtc = new DateTime(platform.ReleaseYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Runtime = 0,
                    Monitored = true,
                    Ratings = new Ratings { Votes = 100, Value = 9.0m }
                });

                series.Seasons = series.Seasons.Where(s => s.SeasonNumber == RegionUsa).ToList();
            }

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        private List<GameCatalogEntry> SyncOnlineCatalogForPlatform(
            PlatformCatalogEntry platform,
            Games.EncryptionPreference encryptionPreference = Games.EncryptionPreference.NotEncrypted,
            bool forceRefresh = false)
        {
            if (platform == null || string.IsNullOrWhiteSpace(platform.LibretroSystemName) || _httpClient == null)
            {
                return new List<GameCatalogEntry>();
            }

            var cacheKey = $"{platform.TvdbId}:{(int)encryptionPreference}";
            if (!forceRefresh && SyncedPlatformGamesCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var syncedList = new List<GameCatalogEntry>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var escapedSystem = Uri.EscapeDataString(platform.LibretroSystemName);
                var releaseYearLookup = FetchReleaseYearLookup(escapedSystem);

                var datFeeds = BuildDatFeedsForPlatform(platform, escapedSystem, encryptionPreference);

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
                            TryCacheRemoteDatContent(platform, feed.SourceLabel, response.Content, forceRefresh);
                        }
                    }
                }

                var smdbUrl = GetSmokeMonsterSmdbUrl(platform.Slug);
                if (!string.IsNullOrWhiteSpace(smdbUrl))
                {
                    var smdbRequest = new HttpRequest(smdbUrl)
                    {
                        SuppressHttpError = true,
                        RequestTimeout = TimeSpan.FromSeconds(15)
                    };

                    var smdbResponse = _httpClient.Execute(smdbRequest);
                    if (smdbResponse != null && !smdbResponse.HasHttpError && !string.IsNullOrWhiteSpace(smdbResponse.Content))
                    {
                        var smdbEntries = ParseSmokeMonsterSmdbContent(platform, smdbResponse.Content, releaseYearLookup, seenNames, forceRefresh);
                        if (smdbEntries.Count > 0)
                        {
                            syncedList.AddRange(smdbEntries);
                        }
                    }
                }

                if (string.Equals(platform.Slug, "switch", StringComparison.OrdinalIgnoreCase))
                {
                    var nswdbRequest = new HttpRequest("https://nswdb.com/xml.php")
                    {
                        SuppressHttpError = true,
                        RequestTimeout = TimeSpan.FromSeconds(15)
                    };

                    var nswdbResponse = _httpClient.Execute(nswdbRequest);
                    if (nswdbResponse != null && !nswdbResponse.HasHttpError && !string.IsNullOrWhiteSpace(nswdbResponse.Content))
                    {
                        var nswdbEntries = ParseNswdbXmlContent(platform, nswdbResponse.Content, seenNames, forceRefresh);
                        if (nswdbEntries.Count > 0)
                        {
                            syncedList.AddRange(nswdbEntries);
                        }
                    }
                }

                var gameTdbUrl = GetGameTdbUrl(platform.Slug);
                if (!string.IsNullOrWhiteSpace(gameTdbUrl))
                {
                    var tdbRequest = new HttpRequest(gameTdbUrl)
                    {
                        SuppressHttpError = true,
                        RequestTimeout = TimeSpan.FromSeconds(30)
                    };

                    var tdbResponse = _httpClient.Execute(tdbRequest);
                    if (tdbResponse != null && !tdbResponse.HasHttpError && tdbResponse.ResponseData != null && tdbResponse.ResponseData.Length > 0)
                    {
                        var tdbEntries = ParseGameTdbZipContent(platform, tdbResponse.ResponseData, seenNames, forceRefresh);
                        if (tdbEntries.Count > 0)
                        {
                            syncedList.AddRange(tdbEntries);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Offline or unable to sync remote No-Intro/Redump/SMDB DAT for {0}; using built-in catalog.", platform.Name);
            }

            if (syncedList.Count > 0)
            {
                foreach (var entry in syncedList)
                {
                    DynamicGames[entry.TvdbId] = entry;
                }

                SyncedPlatformGamesCache[cacheKey] = syncedList;
            }

            return syncedList;
        }

        private static List<(string Url, string SourceLabel)> BuildDatFeedsForPlatform(
            PlatformCatalogEntry platform,
            string escapedSystem,
            Games.EncryptionPreference encryptionPreference)
        {
            var feeds = new List<(string Url, string SourceLabel)>();
            var slug = (platform?.Slug ?? string.Empty).ToLowerInvariant();

            if (slug == "nds")
            {
                if (encryptionPreference == Games.EncryptionPreference.NotEncrypted ||
                    encryptionPreference == Games.EncryptionPreference.Both)
                {
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Nintendo%20DS%20(Decrypted).dat", "No-Intro (Decrypted)"));
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Nintendo%20DSi%20(Decrypted).dat", "No-Intro DSi (Decrypted)"));
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Nintendo%20DSi%20(Digital).dat", "No-Intro DSiWare"));
                }

                if (encryptionPreference == Games.EncryptionPreference.Encrypted ||
                    encryptionPreference == Games.EncryptionPreference.Both)
                {
                    feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/{escapedSystem}.dat", "No-Intro (Encrypted)"));
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Nintendo%20DSi.dat", "No-Intro DSi (Encrypted)"));
                }
            }
            else if (slug == "3ds")
            {
                if (encryptionPreference == Games.EncryptionPreference.NotEncrypted ||
                    encryptionPreference == Games.EncryptionPreference.Both)
                {
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Nintendo%203DS%20(Decrypted).dat", "No-Intro (Decrypted)"));
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20New%20Nintendo%203DS%20(Decrypted).dat", "No-Intro New 3DS (Decrypted)"));
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Nintendo%203DS%20(Digital).dat", "No-Intro 3DS Digital"));
                }

                if (encryptionPreference == Games.EncryptionPreference.Encrypted ||
                    encryptionPreference == Games.EncryptionPreference.Both)
                {
                    feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/{escapedSystem}.dat", "No-Intro (Encrypted)"));
                    feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20New%20Nintendo%203DS.dat", "No-Intro New 3DS (Encrypted)"));
                }
            }
            else if (slug == "neogeo")
            {
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/dat/SNK%20-%20Neo%20Geo.dat", "FBNeo / Neo Geo AES"));
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/SNK%20-%20Neo%20Geo%20CD.dat", "Redump Neo Geo CD"));
                feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/{escapedSystem}.dat", "No-Intro"));
            }
            else if (slug == "wiiu")
            {
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Wii%20U%20(Digital).dat", "No-Intro Wii U Digital"));
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/dat/Nintendo%20-%20Wii%20U.dat", "Wii U Catalog"));
                feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/{escapedSystem}.dat", "Redump Wii U"));
            }
            else
            {
                feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/{escapedSystem}.dat", "No-Intro"));
                feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/{escapedSystem}.dat", "Redump"));
            }

            if (slug == "ngpc")
            {
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/SNK%20-%20Neo%20Geo%20Pocket.dat", "No-Intro Neo Geo Pocket"));
            }
            else if (slug == "wonderswan")
            {
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Bandai%20-%20WonderSwan.dat", "No-Intro WonderSwan"));
            }
            else if (slug == "tg16")
            {
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/NEC%20-%20PC%20Engine%20SuperGrafx.dat", "No-Intro SuperGrafx"));
            }
            else if (slug == "wii")
            {
                feeds.Add(("https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/no-intro/Nintendo%20-%20Wii%20(Digital).dat", "No-Intro WiiWare & VC"));
            }

            feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/homebrew/{escapedSystem}.dat", "Homebrew & Aftermarket"));
            feeds.Add(($"https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/hacks/{escapedSystem}.dat", "ROM Hacks & Translations"));

            return feeds;
        }

        private static string GetSmokeMonsterSmdbUrl(string platformSlug)
        {
            if (string.IsNullOrWhiteSpace(platformSlug))
            {
                return null;
            }

            const string baseUrl = "https://raw.githubusercontent.com/SmokeMonsterPacks/EverDrive-Packs-Lists-Database/master/EverDrive%20Pack%20SMDBs/";

            switch (platformSlug.ToLowerInvariant())
            {
                case "nes":
                    return baseUrl + "EverDrive%20N8%20%26%20PowerPak%20SMDB.txt";
                case "fds":
                    return baseUrl + "Famicom%20Disk%20System%20SMDB.txt";
                case "snes":
                    return baseUrl + "Super%20EverDrive%20%26%20SD2SNES%20SMDB.txt";
                case "n64":
                    return baseUrl + "EverDrive%2064%20%26%2064drive%20SMDB.txt";
                case "gb":
                    return baseUrl + "EverDrive%20GB%20SMDB.txt";
                case "gbc":
                    return baseUrl + "Game%20Boy%20Color%20SMDB.txt";
                case "gba":
                    return baseUrl + "EverDrive%20GBA%20SMDB.txt";
                case "genesis":
                    return baseUrl + "Mega%20EverDrive%20SMDB.txt";
                case "segacd":
                    return baseUrl + "MegaSD%20SMDB.txt";
                case "32x":
                    return baseUrl + "Sega%2032X%20SMDB.txt";
                case "sms":
                    return baseUrl + "Master%20EverDrive%20SMDB.txt";
                case "gg":
                    return baseUrl + "EverDrive%20GG%20SMDB.txt";
                case "tg16":
                    return baseUrl + "Turbo%20EverDrive%20%26%20Super%20SD%20System%203%20SMDB.txt";
                case "tgcd":
                    return baseUrl + "PC%20Engine%20CD%20Redump%20Supplement.txt";
                case "neogeo":
                    return baseUrl + "Darksoft%20Neo%20Geo%20SMDB.txt";
                case "atari2600":
                    return baseUrl + "Atari%202600%20SMDB.txt";
                case "atari7800":
                    return baseUrl + "Atari%207800%20SMDB.txt";
                case "lynx":
                    return baseUrl + "Atari%20Lynx%20SMDB.txt";
                case "jaguar":
                    return baseUrl + "Atari%20Jaguar%20SMDB.txt";
                case "vb":
                    return baseUrl + "Virtual%20Boy%20SMDB.txt";
                case "ngpc":
                    return baseUrl + "NGPC%20RetroHQ%20SMDB.txt";
                case "wonderswan":
                    return baseUrl + "Wonderswan%20SMDB.txt";
                default:
                    return null;
            }
        }

        private class ReleaseDateInfo
        {
            public int Year { get; set; }
            public string ReleaseDate { get; set; }
        }

        private List<GameCatalogEntry> ParseSmokeMonsterSmdbContent(
            PlatformCatalogEntry platform,
            string smdbContent,
            Dictionary<string, ReleaseDateInfo> releaseYearLookup,
            HashSet<string> seenNames,
            bool forceRefresh = false)
        {
            var results = new List<GameCatalogEntry>();
            var datEntries = new List<DatEntry>();
            var lines = smdbContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts.Length < 2)
                {
                    continue;
                }

                var sha256 = parts[0].Trim();
                var smdbPath = parts[1].Trim();
                var sha1 = parts.Length > 2 ? parts[2].Trim() : string.Empty;
                var md5 = parts.Length > 3 ? parts[3].Trim() : string.Empty;
                var crc32 = parts.Length > 4 ? parts[4].Trim() : string.Empty;

                var normalizedSmdbPath = smdbPath.Replace('\\', '/');
                var pathSegments = normalizedSmdbPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // Skip EverDrive duplicate playlist / collection sets (Best-Of, Top 100, Franchise, Genre, etc.)
                if (pathSegments.Any(s => s.Contains("Game Series Collections", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Best-Of", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Top 100", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var fileName = Path.GetFileName(normalizedSmdbPath);
                var rawName = StripKnownRomExtension(fileName);
                rawName = RomParser.CleanIndexPrefix(rawName);

                if (string.IsNullOrWhiteSpace(rawName) ||
                    rawName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                    !IsValidGameTitle(fileName) ||
                    !IsValidGameTitle(rawName) ||
                    !IsValidGameTitle(smdbPath))
                {
                    continue;
                }

                datEntries.Add(new DatEntry
                {
                    GameName = rawName,
                    RomName = fileName,
                    SmdbPath = smdbPath,
                    Sha256 = sha256,
                    Sha1 = sha1,
                    Md5 = md5,
                    Crc32 = crc32
                });

                if (!seenNames.Add(rawName))
                {
                    continue;
                }

                var year = platform.ReleaseYear;
                string releaseDate = null;
                if (!string.IsNullOrWhiteSpace(crc32) &&
                    releaseYearLookup != null &&
                    releaseYearLookup.TryGetValue($"crc:{crc32.ToUpperInvariant()}", out var crcDateInfo))
                {
                    year = crcDateInfo.Year;
                    releaseDate = crcDateInfo.ReleaseDate;
                }
                else if (releaseYearLookup != null &&
                         (releaseYearLookup.TryGetValue(rawName, out var mappedDateInfo) ||
                          releaseYearLookup.TryGetValue(RomParser.CleanIndexPrefix(rawName), out mappedDateInfo)))
                {
                    year = mappedDateInfo.Year;
                    releaseDate = mappedDateInfo.ReleaseDate;
                }

                var smdbCategory = "SmokeMonster HTGDB / SMDB";

                if (pathSegments.Any(s => s.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Hack", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Enhancement", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Color", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                                          s.Contains("Fix", StringComparison.OrdinalIgnoreCase)))
                {
                    smdbCategory = "ROM Hacks & Translations";
                }
                else if (pathSegments.Any(s => s.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
                                               s.Contains("Aftermarket", StringComparison.OrdinalIgnoreCase)))
                {
                    smdbCategory = "Homebrew & Aftermarket";
                }
                else if (pathSegments.Any(s => s.Contains("Unlicensed", StringComparison.OrdinalIgnoreCase) ||
                                               s.Contains("Pirate", StringComparison.OrdinalIgnoreCase) ||
                                               s.Contains("Bootleg", StringComparison.OrdinalIgnoreCase)))
                {
                    smdbCategory = "Unlicensed";
                }

                var entry = CreateCatalogEntryFromNoIntroName(platform, rawName, year, smdbCategory, customReleaseDate: releaseDate);
                if (entry != null)
                {
                    entry.ExpectedSha256 = sha256;
                    entry.ExpectedSha1 = sha1;
                    entry.ExpectedMd5 = md5;
                    entry.ExpectedCrc32 = crc32;
                    results.Add(entry);
                }
            }

            if (datEntries.Count > 0 && _datCacheService != null)
            {
                try
                {
                    var catalogName = $"{platform.LibretroSystemName} (SmokeMonster SMDB)";
                    var existing = _datCacheService.GetAllDatFiles();
                    if (forceRefresh || existing == null || !existing.Any(d => string.Equals(d.Name, catalogName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _datCacheService.Insert(
                            new DatFile
                            {
                                Name = catalogName,
                                Description = platform.Slug,
                                Category = "SmokeMonster SMDB",
                                CreatedAt = DateTime.UtcNow
                            },
                            datEntries);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Trace(ex, "Unable to cache remote SmokeMonster SMDB entries for {0}", platform?.Name);
                }
            }

            return results;
        }

        private List<GameCatalogEntry> ParseNswdbXmlContent(
            PlatformCatalogEntry platform,
            string xmlContent,
            HashSet<string> seenNames,
            bool forceRefresh = false)
        {
            var results = new List<GameCatalogEntry>();
            var datEntries = new List<DatEntry>();

            try
            {
                var doc = XDocument.Parse(xmlContent);
                foreach (var releaseEl in doc.Descendants("release"))
                {
                    var name = releaseEl.Element("name")?.Value?.Trim();
                    var releaseName = releaseEl.Element("releasename")?.Value?.Trim() ?? releaseEl.Element("filename")?.Value?.Trim();

                    if (string.IsNullOrWhiteSpace(name) || !IsValidGameTitle(name))
                    {
                        var resolvedTitle = CleanSceneReleaseTitle(releaseName);
                        if (!string.IsNullOrWhiteSpace(resolvedTitle) && IsValidGameTitle(resolvedTitle))
                        {
                            name = resolvedTitle;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(name) || !IsValidGameTitle(name))
                    {
                        continue;
                    }

                    var region = releaseEl.Element("region")?.Value?.Trim();
                    var languages = releaseEl.Element("languages")?.Value?.Trim();
                    var gameId = releaseEl.Element("titleid")?.Value?.Trim() ?? releaseEl.Element("serial")?.Value?.Trim();
                    var crc = releaseEl.Element("imgcrc")?.Value?.Trim();
                    var revision = releaseEl.Element("revision")?.Value?.Trim();

                    var regionLabel = ResolveCatalogRegionLabel(region, languages, gameId, name);

                    var revSuffix = !string.IsNullOrWhiteSpace(revision) && revision != "0" && revision != "000"
                        ? $" (Rev {revision})"
                        : string.Empty;
                    var baseWithRegion = EnsureGeographicRegionTag(name, name, regionLabel);
                    var noIntroName = !string.IsNullOrEmpty(revSuffix) && !baseWithRegion.Contains("(Rev ", StringComparison.OrdinalIgnoreCase)
                        ? $"{baseWithRegion}{revSuffix}"
                        : baseWithRegion;

                    if (!string.IsNullOrWhiteSpace(crc))
                    {
                        datEntries.Add(new DatEntry
                        {
                            GameName = noIntroName,
                            RomName = $"{noIntroName}.xci",
                            Crc32 = crc.ToLowerInvariant(),
                            Region = regionLabel
                        });
                    }

                    if (!seenNames.Add(noIntroName))
                    {
                        continue;
                    }

                    var entry = CreateCatalogEntryFromNoIntroName(platform, noIntroName, platform.ReleaseYear, "NSWDB Switch Catalog");
                    if (entry != null)
                    {
                        entry.ExpectedCrc32 = crc;
                        results.Add(entry);
                    }
                }

                if (datEntries.Count > 0 && _datCacheService != null)
                {
                    var catalogName = $"{platform.LibretroSystemName} (NSWDB)";
                    var existing = _datCacheService.GetAllDatFiles();
                    var existingFile = existing?.FirstOrDefault(d => string.Equals(d.Name, catalogName, StringComparison.OrdinalIgnoreCase));
                    if (forceRefresh || existingFile == null || !string.Equals(existingFile.Version, "v2", StringComparison.OrdinalIgnoreCase))
                    {
                        _datCacheService.Insert(
                            new DatFile
                            {
                                Name = catalogName,
                                Description = platform.Slug,
                                Version = "v2",
                                Category = "NSWDB",
                                CreatedAt = DateTime.UtcNow
                            },
                            datEntries);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Trace(ex, "Unable to parse NSWDB XML catalog for Nintendo Switch");
            }

            return results;
        }

        public static string GetGameTdbUrl(string slug)
        {
            return slug?.ToLowerInvariant() switch
            {
                "switch" => "https://www.gametdb.com/switchtdb.zip",
                "wii" => "https://www.gametdb.com/wiitdb.zip",
                "wiiu" => "https://www.gametdb.com/wiiutdb.zip",
                "3ds" => "https://www.gametdb.com/3dstdb.zip",
                _ => null
            };
        }

        public List<GameCatalogEntry> ParseGameTdbZipContent(
            PlatformCatalogEntry platform,
            byte[] zipBytes,
            HashSet<string> seenNames,
            bool forceRefresh = false)
        {
            try
            {
                using var memStream = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(memStream, ZipArchiveMode.Read);
                var xmlEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
                if (xmlEntry == null)
                {
                    return new List<GameCatalogEntry>();
                }

                using var entryStream = xmlEntry.Open();
                return ParseGameTdbXmlStream(platform, entryStream, seenNames, forceRefresh);
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Failed to unpack GameTDB archive for {0}", platform.Name);
                return new List<GameCatalogEntry>();
            }
        }

        public List<GameCatalogEntry> ParseGameTdbXmlStream(
            PlatformCatalogEntry platform,
            Stream stream,
            HashSet<string> seenNames,
            bool forceRefresh = false)
        {
            if (stream == null)
            {
                return new List<GameCatalogEntry>();
            }

            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore
            });

            return ParseGameTdbXmlReader(platform, xmlReader, seenNames, forceRefresh);
        }

        public List<GameCatalogEntry> ParseGameTdbXmlContent(
            PlatformCatalogEntry platform,
            string xmlContent,
            HashSet<string> seenNames,
            bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return new List<GameCatalogEntry>();
            }

            using var textReader = new StringReader(xmlContent);
            using var xmlReader = XmlReader.Create(textReader, new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore
            });

            return ParseGameTdbXmlReader(platform, xmlReader, seenNames, forceRefresh);
        }

        private List<GameCatalogEntry> ParseGameTdbXmlReader(
            PlatformCatalogEntry platform,
            XmlReader xmlReader,
            HashSet<string> seenNames,
            bool forceRefresh = false)
        {
            var results = new List<GameCatalogEntry>();
            var datEntries = new List<DatEntry>();

            try
            {
                while (!xmlReader.EOF)
                {
                    if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.LocalName == "game")
                    {
                        if (XNode.ReadFrom(xmlReader) is not XElement gameEl)
                        {
                            xmlReader.Read();
                            continue;
                        }

                        var rawName = gameEl.Attribute("name")?.Value?.Trim();
                        var localeEn = gameEl.Elements("locale").FirstOrDefault(l => string.Equals((string)l.Attribute("lang"), "EN", StringComparison.OrdinalIgnoreCase)) ??
                                       gameEl.Elements("locale").FirstOrDefault();
                        var localeTitle = localeEn?.Element("title")?.Value?.Trim();
                        var synopsis = localeEn?.Element("synopsis")?.Value?.Trim();

                        var id = gameEl.Element("id")?.Value?.Trim();
                        var region = gameEl.Element("region")?.Value?.Trim();
                        var languages = gameEl.Element("languages")?.Value?.Trim();

                        var title = !string.IsNullOrWhiteSpace(localeTitle) && IsValidGameTitle(localeTitle)
                            ? localeTitle
                            : (!string.IsNullOrWhiteSpace(rawName) && IsValidGameTitle(rawName) ? rawName : null);

                        if (string.IsNullOrWhiteSpace(title))
                        {
                            var altLocale = gameEl.Elements("locale")
                                .Select(l => l.Element("title")?.Value?.Trim())
                                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t) && IsValidGameTitle(t));
                            if (!string.IsNullOrWhiteSpace(altLocale))
                            {
                                title = altLocale;
                            }
                            else
                            {
                                var romName = (string)gameEl.Elements("rom").FirstOrDefault()?.Attribute("name");
                                var cleanedRom = CleanSceneReleaseTitle(romName);
                                if (!string.IsNullOrWhiteSpace(cleanedRom) && IsValidGameTitle(cleanedRom))
                                {
                                    title = cleanedRom;
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(title) || !IsValidGameTitle(title))
                        {
                            continue;
                        }

                        var regionLabel = ResolveCatalogRegionLabel(region, languages, id, rawName);
                        var formattedName = EnsureGeographicRegionTag(rawName, title, regionLabel);

                        var releaseYear = platform.ReleaseYear;
                        var dateEl = gameEl.Element("date");
                        if (dateEl != null && int.TryParse((string)dateEl.Attribute("year"), out var parsedYear) && parsedYear >= 1970 && parsedYear <= 2035)
                        {
                            releaseYear = parsedYear;
                        }

                        var romElements = gameEl.Elements("rom").ToList();
                        string primaryCrc = null;
                        string primarySha1 = null;
                        string primaryMd5 = null;

                        if (romElements.Count > 0)
                        {
                            foreach (var romEl in romElements)
                            {
                                var romName = (string)romEl.Attribute("name");
                                var crc = (string)romEl.Attribute("crc");
                                var md5 = (string)romEl.Attribute("md5");
                                var sha1 = (string)romEl.Attribute("sha1");
                                var sizeStr = (string)romEl.Attribute("size");
                                long.TryParse(sizeStr, out var size);

                                if (string.IsNullOrWhiteSpace(romName))
                                {
                                    var defaultExt = platform.Extensions?.FirstOrDefault() ?? ".bin";
                                    romName = $"{formattedName}{defaultExt}";
                                }

                                if (primaryCrc == null && !string.IsNullOrWhiteSpace(crc))
                                {
                                    primaryCrc = crc;
                                    primarySha1 = sha1;
                                    primaryMd5 = md5;
                                }

                                if (!string.IsNullOrWhiteSpace(crc) || !string.IsNullOrWhiteSpace(sha1) || !string.IsNullOrWhiteSpace(md5))
                                {
                                    datEntries.Add(new DatEntry
                                    {
                                        GameName = formattedName,
                                        RomName = romName,
                                        Crc32 = crc?.ToLowerInvariant(),
                                        Md5 = md5?.ToLowerInvariant(),
                                        Sha1 = sha1?.ToLowerInvariant(),
                                        Size = size > 0 ? size : 0,
                                        Region = regionLabel
                                    });
                                }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(id))
                        {
                            var defaultExt = platform.Extensions?.FirstOrDefault() ?? ".bin";
                            datEntries.Add(new DatEntry
                            {
                                GameName = formattedName,
                                RomName = $"{formattedName}{defaultExt}",
                                Region = regionLabel
                            });
                        }

                        if (!seenNames.Add(formattedName))
                        {
                            continue;
                        }

                        var entry = CreateCatalogEntryFromNoIntroName(platform, formattedName, releaseYear, $"GameTDB {platform.Name}");
                        if (entry != null)
                        {
                            if (!string.IsNullOrWhiteSpace(synopsis))
                            {
                                entry.Overview = synopsis;
                            }

                            entry.ExpectedCrc32 = primaryCrc;
                            entry.ExpectedSha1 = primarySha1;
                            entry.ExpectedMd5 = primaryMd5;
                            results.Add(entry);
                        }
                    }
                    else
                    {
                        xmlReader.Read();
                    }
                }

                if (datEntries.Count > 0 && _datCacheService != null)
                {
                    var catalogName = $"{platform.LibretroSystemName} (GameTDB)";
                    var existing = _datCacheService.GetAllDatFiles();
                    var existingFile = existing?.FirstOrDefault(d => string.Equals(d.Name, catalogName, StringComparison.OrdinalIgnoreCase));
                    if (forceRefresh || existingFile == null || !string.Equals(existingFile.Version, "v2", StringComparison.OrdinalIgnoreCase))
                    {
                        _datCacheService.Insert(
                            new DatFile
                            {
                                Name = catalogName,
                                Description = platform.Slug,
                                Version = "v2",
                                Category = "GameTDB",
                                CreatedAt = DateTime.UtcNow
                            },
                            datEntries);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug(ex, "Unable to parse GameTDB XML catalog for {0}", platform.Name);
            }

            return results;
        }

        private Dictionary<string, ReleaseDateInfo> FetchReleaseYearLookup(string escapedSystem)
        {
            var lookup = new Dictionary<string, ReleaseDateInfo>(StringComparer.OrdinalIgnoreCase);
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
                        var body = match.Groups["body"].Value;
                        var yearMatch = DatReleaseYearRegex.Match(body);
                        if (!yearMatch.Success ||
                            !int.TryParse(yearMatch.Groups["year"].Value, out var year) ||
                            year < 1970 ||
                            year > 2035)
                        {
                            continue;
                        }

                        var releaseDate = $"{year:D4}";
                        var monthMatch = DatReleaseMonthRegex.Match(body);
                        if (monthMatch.Success &&
                            int.TryParse(monthMatch.Groups["month"].Value, out var month) &&
                            month >= 1 && month <= 12)
                        {
                            var dayMatch = DatReleaseDayRegex.Match(body);
                            if (dayMatch.Success &&
                                int.TryParse(dayMatch.Groups["day"].Value, out var day) &&
                                day >= 1 && day <= 31)
                            {
                                releaseDate = $"{year:D4}-{month:D2}-{day:D2}";
                            }
                            else
                            {
                                releaseDate = $"{year:D4}-{month:D2}";
                            }
                        }

                        var dateInfo = new ReleaseDateInfo { Year = year, ReleaseDate = releaseDate };

                        var nameMatch = DatNameOrCommentRegex.Match(body);
                        if (nameMatch.Success)
                        {
                            var name = nameMatch.Groups["name"].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                lookup[name] = dateInfo;
                                var cleanName = RomParser.CleanIndexPrefix(name);
                                lookup[cleanName] = dateInfo;
                                var normalized = RomParser.ParseRomName(cleanName)?.GameTitle;
                                if (!string.IsNullOrWhiteSpace(normalized))
                                {
                                    lookup[normalized] = dateInfo;
                                }
                            }
                        }

                        var crcMatch = DatCrcRegex.Match(body);
                        if (crcMatch.Success)
                        {
                            lookup[$"crc:{crcMatch.Groups[1].Value.ToUpperInvariant()}"] = dateInfo;
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

        private void TryCacheRemoteDatContent(PlatformCatalogEntry platform, string sourceLabel, string datContent, bool forceRefresh = false)
        {
            if (_datCacheService == null || _datParserService == null || string.IsNullOrWhiteSpace(datContent))
            {
                return;
            }

            try
            {
                var catalogName = $"{platform.LibretroSystemName} ({sourceLabel})";
                var existing = _datCacheService.GetAllDatFiles();
                if (!forceRefresh && existing != null && existing.Any(d => string.Equals(d.Name, catalogName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(datContent)))
                {
                    var (datFile, entries) = _datParserService.Parse(stream);
                    if (entries != null && entries.Count > 0)
                    {
                        datFile.Name = catalogName;
                        datFile.Description = platform.Slug;
                        datFile.Category = sourceLabel;
                        _datCacheService.Insert(datFile, entries);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Trace(ex, "Unable to persist remote DAT hash cache for {0}", platform?.Name);
            }
        }

        private static List<GameCatalogEntry> ParseLibretroDatContent(
            PlatformCatalogEntry platform,
            string datContent,
            string sourceLabel,
            Dictionary<string, ReleaseDateInfo> releaseYearLookup,
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

                var isHackOrHomebrewFeed =
                    sourceLabel != null &&
                    (sourceLabel.Contains("ROM Hacks", StringComparison.OrdinalIgnoreCase) ||
                     sourceLabel.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                     sourceLabel.Contains("Hacks", StringComparison.OrdinalIgnoreCase) ||
                     sourceLabel.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
                     sourceLabel.Contains("Aftermarket", StringComparison.OrdinalIgnoreCase));

                if (romMatch.Success && !isHackOrHomebrewFeed)
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

                rawName = RomParser.CleanIndexPrefix(rawName);

                if (string.IsNullOrWhiteSpace(rawName) ||
                    rawName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                    blockName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase) ||
                    !IsValidGameTitle(rawName) ||
                    !IsValidGameTitle(blockName))
                {
                    continue;
                }

                if (sourceLabel != null &&
                    sourceLabel.Contains("(Encrypted)", StringComparison.OrdinalIgnoreCase) &&
                    !rawName.Contains("(Encrypted)", StringComparison.OrdinalIgnoreCase))
                {
                    rawName = $"{rawName} (Encrypted)";
                }

                if (!seenNames.Add(rawName))
                {
                    continue;
                }

                var year = platform.ReleaseYear;
                string releaseDate = null;
                if (releaseYearLookup != null &&
                    (releaseYearLookup.TryGetValue(rawName, out var mappedDateInfo) ||
                     releaseYearLookup.TryGetValue(blockName, out mappedDateInfo)))
                {
                    year = mappedDateInfo.Year;
                    releaseDate = mappedDateInfo.ReleaseDate;
                }
                else
                {
                    var yearMatch = DatReleaseYearRegex.Match(body);
                    if (yearMatch.Success && int.TryParse(yearMatch.Groups["year"].Value, out var parsedYear) && parsedYear >= 1970 && parsedYear <= 2030)
                    {
                        year = parsedYear;
                        releaseDate = $"{parsedYear:D4}";
                        var monthMatch = DatReleaseMonthRegex.Match(body);
                        if (monthMatch.Success && int.TryParse(monthMatch.Groups["month"].Value, out var month) && month >= 1 && month <= 12)
                        {
                            var dayMatch = DatReleaseDayRegex.Match(body);
                            if (dayMatch.Success && int.TryParse(dayMatch.Groups["day"].Value, out var day) && day >= 1 && day <= 31)
                            {
                                releaseDate = $"{parsedYear:D4}-{month:D2}-{day:D2}";
                            }
                            else
                            {
                                releaseDate = $"{parsedYear:D4}-{month:D2}";
                            }
                        }
                    }
                }

                var entry = CreateCatalogEntryFromNoIntroName(platform, rawName, year, sourceLabel, customReleaseDate: releaseDate);
                if (entry != null)
                {
                    var crcMatch = DatCrcRegex.Match(body);
                    if (crcMatch.Success)
                    {
                        entry.ExpectedCrc32 = crcMatch.Groups[1].Value.ToLowerInvariant();
                    }

                    var md5Match = Regex.Match(body, @"\bmd5\s+([0-9a-fA-F]{32})", RegexOptions.IgnoreCase);
                    if (md5Match.Success)
                    {
                        entry.ExpectedMd5 = md5Match.Groups[1].Value.ToLowerInvariant();
                    }

                    var sha1Match = Regex.Match(body, @"\bsha1\s+([0-9a-fA-F]{40})", RegexOptions.IgnoreCase);
                    if (sha1Match.Success)
                    {
                        entry.ExpectedSha1 = sha1Match.Groups[1].Value.ToLowerInvariant();
                    }

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

        public static bool IsValidGameTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            var trimmed = title.Trim();
            if (trimmed.StartsWith("@", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("_", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmed.IndexOf("Pack Here", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (trimmed.IndexOf("Place ", StringComparison.OrdinalIgnoreCase) >= 0 && trimmed.IndexOf(" Here", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return false;
            }

            var textOutsideBrackets = Regex.Replace(trimmed, @"\([^\)]*\)|\[[^\]]*\]", " ").Trim();
            if (string.IsNullOrWhiteSpace(textOutsideBrackets) || !textOutsideBrackets.Any(char.IsLetterOrDigit))
            {
                return false;
            }

            if (Regex.IsMatch(trimmed, @"^[\(\[\s]*(?:EN|JA|FR|DE|ES|IT|ZH|KO|NL|RU|PT|USA|EUR|JPN|WORLD|ASIA|REV\s*\d+|V[\d.]+)+[\)\]\s]*$", RegexOptions.IgnoreCase))
            {
                return false;
            }

            return true;
        }

        internal static GameCatalogEntry CreateCatalogEntryFromNoIntroName(
            PlatformCatalogEntry platform,
            string noIntroName,
            int year,
            string verificationSource,
            string customSummary = null,
            string customReleaseDate = null)
        {
            if (!IsValidGameTitle(noIntroName))
            {
                return null;
            }

            var cleanNoIntroForParse = RomParser.CleanIndexPrefix(noIntroName.Replace(" (Encrypted)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim());
            if (!IsValidGameTitle(cleanNoIntroForParse))
            {
                return null;
            }

            var parsed = RomParser.ParseRomName(cleanNoIntroForParse);
            var baseTitle = !string.IsNullOrWhiteSpace(parsed?.GameTitle)
                ? NormalizeTitleArticle(parsed.GameTitle)
                : NormalizeTitleArticle(StripParentheses(cleanNoIntroForParse));

            baseTitle = RomParser.CleanIndexPrefix(baseTitle);
            baseTitle = CleanResidualRevisions(baseTitle);

            if (!IsValidGameTitle(baseTitle))
            {
                return null;
            }

            var regionNumber = ClassifyRegionAndCategory(cleanNoIntroForParse, parsed, verificationSource);
            var revisionTag = ExtractRevisionTag(noIntroName, parsed, regionNumber, verificationSource);

            var tvdbId = ComputeDeterministicGameId(platform.Slug, noIntroName);
            var regionName = GetRegionDisplayName(regionNumber);
            var isPreRelease = IsPreReleaseVersion(revisionTag, noIntroName);
            var isEncrypted = IsEncryptedVersion(revisionTag, noIntroName);
            var isValidYear = year >= 1970 && year <= 2035;
            var effectiveYear = isValidYear ? year : platform.ReleaseYear;
            var releaseDateString = !string.IsNullOrWhiteSpace(customReleaseDate)
                ? customReleaseDate
                : (isValidYear ? year.ToString() : (platform.ReleaseYear >= 1970 ? platform.ReleaseYear.ToString() : null));

            DateTime firstAired;
            if (!string.IsNullOrWhiteSpace(releaseDateString) && DateTime.TryParse(releaseDateString, out var parsedDate))
            {
                firstAired = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, 12, 0, 0, DateTimeKind.Utc);
            }
            else if (isValidYear)
            {
                firstAired = new DateTime(year, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            }
            else if (platform.ReleaseYear >= 1970)
            {
                firstAired = new DateTime(platform.ReleaseYear, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                firstAired = DateTime.MinValue;
            }

            return new GameCatalogEntry
            {
                TvdbId = tvdbId,
                Title = baseTitle,
                BaseTitle = baseTitle,
                RevisionTag = revisionTag,
                IsLatestRevision = !isPreRelease && !isEncrypted,
                IsInitialRelease = !isPreRelease && !isEncrypted && revisionTag == "Retail (v1.0)",
                NoIntroName = noIntroName,
                PlatformTvdbId = platform.TvdbId,
                PlatformSlug = platform.Slug,
                PlatformName = platform.Name,
                PlatformShortName = platform.ShortName,
                Manufacturer = platform.Manufacturer,
                RegionNumber = regionNumber,
                RegionName = regionName,
                GameNumber = 1,
                ReleaseYear = effectiveYear,
                ReleaseDateString = releaseDateString,
                FirstAired = firstAired,
                Overview = !string.IsNullOrWhiteSpace(customSummary)
                    ? $"{customSummary} ({verificationSource}: {noIntroName})"
                    : $"Verified {platform.Name} release ({noIntroName}) [{regionName} • {revisionTag}].",
                PosterUrl = BuildLibretroBoxartUrl(platform.LibretroSystemName, cleanNoIntroForParse),
                VerificationSource = verificationSource
            };
        }

        public static bool IsEncryptedVersion(string revisionTag, string noIntroNameOrTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(revisionTag) &&
                revisionTag.Contains("Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(noIntroNameOrTitle) &&
                noIntroNameOrTitle.Contains("(Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool IsUpdateVersion(string revisionTag, string noIntroNameOrTitle = null)
        {
            if (!string.IsNullOrWhiteSpace(revisionTag) && UpdateTagRegex.IsMatch(revisionTag))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(noIntroNameOrTitle) && UpdateTagRegex.IsMatch(noIntroNameOrTitle))
            {
                return true;
            }

            return false;
        }

        private static int? ExtractDiscNumber(string revisionTag, string noIntroName)
        {
            var match = DiscTagRegex.Match(noIntroName ?? string.Empty);
            if (!match.Success && !string.IsNullOrWhiteSpace(revisionTag))
            {
                match = DiscTagRegex.Match(revisionTag);
            }

            if (match.Success)
            {
                var numStr = match.Groups["num"].Value;
                if (int.TryParse(numStr, out var num))
                {
                    return num;
                }

                if (!string.IsNullOrWhiteSpace(numStr) && numStr.Length == 1 && char.IsLetter(numStr[0]))
                {
                    return char.ToUpperInvariant(numStr[0]) - 'A' + 1;
                }

                return 1;
            }

            return null;
        }

        internal static string ExtractVersionOnly(string revisionTag, string noIntroName = null)
        {
            var raw = revisionTag ?? string.Empty;
            var withoutDisc = DiscTagRegex.Replace(raw, string.Empty).Trim(' ', ',', '-', '•');
            if (string.IsNullOrWhiteSpace(withoutDisc))
            {
                if (!string.IsNullOrWhiteSpace(noIntroName))
                {
                    var cleanNoIntro = DiscTagRegex.Replace(noIntroName, string.Empty);
                    var updMatch = UpdateTagRegex.Match(cleanNoIntro);
                    if (updMatch.Success)
                    {
                        return updMatch.Groups["upd"].Value.Trim();
                    }

                    var revMatch = RevisionTagRegex.Match(cleanNoIntro);
                    if (revMatch.Success)
                    {
                        return revMatch.Groups["rev"].Value.Trim();
                    }
                }

                return "Retail (v1.0)";
            }

            return withoutDisc;
        }

        private static HashSet<int> GetDefaultMonitoredGameIds(
            List<GameCatalogEntry> games,
            Games.EncryptionPreference encryptionPreference)
        {
            var monitoredIds = new HashSet<int>();

            foreach (var group in games.GroupBy(g => $"{g.RegionNumber}:{DiscTagRegex.Replace(g.BaseTitle ?? g.Title, string.Empty).Trim(' ', '-', ':').ToLowerInvariant()}"))
            {
                var retailItems = group
                    .Where(i => !IsPreReleaseVersion(i.RevisionTag, i.NoIntroName))
                    .ToList();

                if (retailItems.Count == 0)
                {
                    continue;
                }

                void MonitorPreferredVersion(List<GameCatalogEntry> items)
                {
                    if (items.Count == 0)
                    {
                        return;
                    }

                    var versionGroups = items
                        .GroupBy(i => ExtractVersionOnly(i.RevisionTag, i.NoIntroName))
                        .OrderBy(vg => GetRevisionSortOrder(vg.Key))
                        .ToList();

                    var preferredVersionItems = versionGroups.Last();
                    foreach (var game in preferredVersionItems)
                    {
                        monitoredIds.Add(game.TvdbId);
                    }
                }

                void ProcessPool(List<GameCatalogEntry> poolItems)
                {
                    if (poolItems.Count == 0)
                    {
                        return;
                    }

                    var unencryptedRetail = poolItems
                        .Where(i => !IsEncryptedVersion(i.RevisionTag, i.NoIntroName))
                        .ToList();
                    var encryptedRetail = poolItems
                        .Where(i => IsEncryptedVersion(i.RevisionTag, i.NoIntroName))
                        .ToList();

                    if (encryptionPreference == Games.EncryptionPreference.NotEncrypted ||
                        encryptionPreference == Games.EncryptionPreference.Both)
                    {
                        if (unencryptedRetail.Count > 0)
                        {
                            MonitorPreferredVersion(unencryptedRetail);
                        }
                        else if (encryptionPreference == Games.EncryptionPreference.NotEncrypted && encryptedRetail.Count > 0)
                        {
                            MonitorPreferredVersion(encryptedRetail);
                        }
                    }

                    if (encryptionPreference == Games.EncryptionPreference.Encrypted ||
                        encryptionPreference == Games.EncryptionPreference.Both)
                    {
                        if (encryptedRetail.Count > 0)
                        {
                            MonitorPreferredVersion(encryptedRetail);
                        }
                        else if (encryptionPreference == Games.EncryptionPreference.Encrypted && unencryptedRetail.Count > 0)
                        {
                            MonitorPreferredVersion(unencryptedRetail);
                        }
                    }
                }

                var baseGamePool = retailItems.Where(i => !IsUpdateVersion(i.RevisionTag, i.NoIntroName)).ToList();
                var updatePool = retailItems.Where(i => IsUpdateVersion(i.RevisionTag, i.NoIntroName)).ToList();

                if (baseGamePool.Count > 0)
                {
                    ProcessPool(baseGamePool);
                }
                else if (updatePool.Count == 0)
                {
                    ProcessPool(retailItems);
                }

                if (updatePool.Count > 0)
                {
                    ProcessPool(updatePool);
                }
            }

            return monitoredIds;
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
            foreach (var group in games.GroupBy(g => $"{g.RegionNumber}:{DiscTagRegex.Replace(g.BaseTitle ?? g.Title, string.Empty).Trim(' ', '-', ':').ToLowerInvariant()}"))
            {
                var items = group.ToList();

                // Prototypes, Betas, Encrypted variants, and Updates do not replace the primary unencrypted retail entry in search
                var retailItems = items
                    .Where(i => !IsPreReleaseVersion(i.RevisionTag, i.NoIntroName) &&
                                !IsEncryptedVersion(i.RevisionTag, i.NoIntroName) &&
                                !IsUpdateVersion(i.RevisionTag, i.NoIntroName))
                    .ToList();
                var nonPrimaryItems = items
                    .Where(i => IsPreReleaseVersion(i.RevisionTag, i.NoIntroName) ||
                                IsEncryptedVersion(i.RevisionTag, i.NoIntroName) ||
                                IsUpdateVersion(i.RevisionTag, i.NoIntroName))
                    .ToList();

                foreach (var nonPrimary in nonPrimaryItems)
                {
                    nonPrimary.IsInitialRelease = false;
                    nonPrimary.IsLatestRevision = false;
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

                var versionGroups = retailItems
                    .GroupBy(i => ExtractVersionOnly(i.RevisionTag, i.NoIntroName))
                    .OrderBy(vg => GetRevisionSortOrder(vg.Key))
                    .ToList();

                for (var vIdx = 0; vIdx < versionGroups.Count; vIdx++)
                {
                    var isLatestVersion = vIdx == versionGroups.Count - 1;
                    var isInitialVersion = vIdx == 0;
                    var vItems = versionGroups[vIdx].ToList();

                    var orderedDiscs = vItems
                        .OrderBy(i => ExtractDiscNumber(i.RevisionTag, i.NoIntroName) ?? 1)
                        .ThenBy(i => i.NoIntroName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    for (var dIdx = 0; dIdx < orderedDiscs.Count; dIdx++)
                    {
                        var isFirstDisc = dIdx == 0;
                        orderedDiscs[dIdx].IsLatestRevision = isLatestVersion && isFirstDisc;
                        orderedDiscs[dIdx].IsInitialRelease = isInitialVersion && isFirstDisc;
                    }
                }
            }
        }

        internal static int GetRevisionSortOrder(string revisionTag)
        {
            if (string.IsNullOrWhiteSpace(revisionTag))
            {
                return 0;
            }

            var clean = DiscTagRegex.Replace(revisionTag, string.Empty).Trim(' ', ',', '-', '•');
            if (string.IsNullOrWhiteSpace(clean) || clean == "Retail (v1.0)")
            {
                return 0;
            }

            if (IsPreReleaseVersion(clean))
            {
                return 10000;
            }

            var match = Regex.Match(clean, @"(?:Rev\.?\s*([A-Za-z0-9.]+)|v([0-9.]+)|Update\s*v?([0-9.]+)|Patch\s*v?([0-9.]+)|DLC\s*v?([0-9.]+))", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var val = match.Groups[1].Success ? match.Groups[1].Value :
                          match.Groups[2].Success ? match.Groups[2].Value :
                          match.Groups[3].Success ? match.Groups[3].Value :
                          match.Groups[4].Success ? match.Groups[4].Value :
                          match.Groups[5].Value;

                if (Version.TryParse(val, out var semVer))
                {
                    return 1000 + (semVer.Major * 1000000) + (Math.Max(0, semVer.Minor) * 10000) + (Math.Max(0, semVer.Build) * 100) + Math.Max(0, semVer.Revision);
                }

                if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var num))
                {
                    return 100 + (int)(num * 100);
                }

                if (val.Length == 1 && char.IsLetter(val[0]))
                {
                    return 200 + (char.ToUpperInvariant(val[0]) - 'A');
                }
            }

            if (clean.StartsWith("Rev ", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("v", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("Update", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("Patch", StringComparison.OrdinalIgnoreCase))
            {
                return 10 + clean.Length;
            }

            return 1;
        }

        internal static string NormalizeCanonicalCrossRefTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var unaccented = value.RemoveDiacritics();
            var articleNormalized = NormalizeTitleArticle(StripParentheses(unaccented));
            var sb = new System.Text.StringBuilder(articleNormalized.Length);
            foreach (var ch in articleNormalized)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        private static string ExtractRevisionTag(
            string noIntroName,
            Parser.Model.ParsedRomInfo parsed,
            int regionNumber,
            string verificationSource = null)
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

            var updMatch = UpdateTagRegex.Match(noIntroName);
            if (updMatch.Success)
            {
                parts.Add(updMatch.Groups["upd"].Value.Trim());
            }

            if (!string.IsNullOrWhiteSpace(parsed?.Revision))
            {
                if (parsed.Revision.StartsWith("Rev", StringComparison.OrdinalIgnoreCase) ||
                    IsUpdateVersion(parsed.Revision))
                {
                    parts.Add(parsed.Revision);
                }
                else
                {
                    parts.Add($"Rev {parsed.Revision}");
                }
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

            if (noIntroName.Contains("(Encrypted)", StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(verificationSource) &&
                 verificationSource.Contains("(Encrypted)", StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add("Encrypted");
            }

            if (parts.Count > 0)
            {
                if (regionNumber == RegionRomHacks)
                {
                    var isTranslation = noIntroName.Contains("[T+", StringComparison.OrdinalIgnoreCase) ||
                                        noIntroName.Contains("[T-", StringComparison.OrdinalIgnoreCase) ||
                                        noIntroName.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                                        noIntroName.Contains("Translated", StringComparison.OrdinalIgnoreCase);
                    parts.Add(isTranslation ? "Translation" : "ROM Hack");
                }

                return string.Join(" • ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
            }

            if (regionNumber == RegionHomebrew)
            {
                return "Homebrew";
            }

            if (regionNumber == RegionRomHacks)
            {
                var isTranslation = noIntroName.Contains("[T+", StringComparison.OrdinalIgnoreCase) ||
                                    noIntroName.Contains("[T-", StringComparison.OrdinalIgnoreCase) ||
                                    noIntroName.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                                    noIntroName.Contains("Translated", StringComparison.OrdinalIgnoreCase);
                return isTranslation ? "Translation" : "ROM Hack";
            }

            if (regionNumber == RegionUnlicensed)
            {
                return "Unlicensed";
            }

            return "Retail (v1.0)";
        }

        private static int ClassifyRegionAndCategory(string noIntroName, Parser.Model.ParsedRomInfo parsed, string verificationSource = null)
        {
            if (RegionHelper.IsRomHackOrTranslation(noIntroName, parsed?.ReleaseTitle, parsed?.DumpTags, verificationSource))
            {
                return RegionRomHacks;
            }

            if (RegionHelper.IsHomebrewOrAftermarket(noIntroName, parsed?.ReleaseTitle, parsed?.DumpTags, verificationSource))
            {
                return RegionHomebrew;
            }

            if (RegionHelper.IsUnlicensed(noIntroName, parsed?.ReleaseTitle, parsed?.DumpTags, verificationSource))
            {
                return RegionUnlicensed;
            }

            // Note: Prototypes, Betas, Demos, and Samples stay in their geographic region
            // so they appear as versions of the game itself rather than in a separate region.
            var regionStr = parsed?.Regions != null && parsed.Regions.Count > 0
                ? string.Join(",", parsed.Regions)
                : noIntroName;

            if (regionStr.Contains("World", StringComparison.OrdinalIgnoreCase))
            {
                return RegionWorld;
            }

            if (regionStr.Contains("USA", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(US", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(U)", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Canada", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Brazil", StringComparison.OrdinalIgnoreCase))
            {
                return RegionUsa;
            }

            if (regionStr.Contains("Japan", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Korea", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Asia", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Taiwan", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("China", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Hong Kong", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("NTSC-J", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("NTSC-K", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(JP", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(J)", StringComparison.OrdinalIgnoreCase))
            {
                return RegionJapan;
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
                regionStr.Contains("Russia", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Poland", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Denmark", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Finland", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Norway", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Portugal", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Greece", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("Scandinavia", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("PAL", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(EU", StringComparison.OrdinalIgnoreCase) ||
                regionStr.Contains("(E)", StringComparison.OrdinalIgnoreCase))
            {
                return RegionEurope;
            }

            var langTokens = ExtractLanguageTokens(null, noIntroName);
            if (langTokens.Count > 0)
            {
                if (langTokens.All(l => AsianLanguageTokens.Contains(l)))
                {
                    return RegionJapan;
                }

                if (!langTokens.Contains("EN", StringComparer.OrdinalIgnoreCase) &&
                    langTokens.All(l => EuropeanNonEnglishLanguageTokens.Contains(l)))
                {
                    return RegionEurope;
                }

                if (langTokens.Count >= 2)
                {
                    return RegionWorld;
                }
            }

            return RegionUsa;
        }

        public static string ResolveCatalogRegionLabel(string region, string languages = null, string gameId = null, string rawName = null)
        {
            var tokens = string.IsNullOrWhiteSpace(region)
                ? Array.Empty<string>()
                : region.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToArray();

            var specificTokens = tokens.Where(t => !WorldRegionTokens.Contains(t)).ToList();

            var hasUsa = specificTokens.Any(t => UsaRegionTokens.Contains(t));
            var hasEur = specificTokens.Any(t => EuropeRegionTokens.Contains(t));
            var hasJpn = specificTokens.Any(t => JapanRegionTokens.Contains(t));

            if (hasUsa && hasEur && hasJpn)
            {
                return "World";
            }

            foreach (var token in specificTokens)
            {
                if (UsaRegionTokens.Contains(token))
                {
                    return "USA";
                }

                if (EuropeRegionTokens.Contains(token))
                {
                    return "Europe";
                }

                if (JapanRegionTokens.Contains(token))
                {
                    return "Japan";
                }
            }

            if (tokens.Length == 0 && !string.IsNullOrWhiteSpace(gameId))
            {
                var trimmedId = gameId.Trim();
                if (trimmedId.Length == 4 || trimmedId.Length == 6)
                {
                    var regionChar = char.ToUpperInvariant(trimmedId[3]);
                    if (regionChar == 'E' || regionChar == 'N')
                    {
                        return "USA";
                    }

                    if (regionChar == 'J' || regionChar == 'K' || regionChar == 'W' || regionChar == 'C')
                    {
                        return "Japan";
                    }

                    if (regionChar == 'P' || regionChar == 'D' || regionChar == 'F' || regionChar == 'S' ||
                        regionChar == 'I' || regionChar == 'H' || regionChar == 'U' || regionChar == 'X' ||
                        regionChar == 'Y' || regionChar == 'Z' || regionChar == 'R')
                    {
                        return "Europe";
                    }
                }
            }

            var langTokens = ExtractLanguageTokens(languages, rawName);
            if (langTokens.Count > 0)
            {
                if (langTokens.All(l => AsianLanguageTokens.Contains(l)))
                {
                    return "Japan";
                }

                if (!langTokens.Contains("EN", StringComparer.OrdinalIgnoreCase) &&
                    langTokens.All(l => EuropeanNonEnglishLanguageTokens.Contains(l)))
                {
                    return "Europe";
                }
            }

            return "World";
        }

        public static string EnsureGeographicRegionTag(string rawName, string fallbackTitle, string regionLabel)
        {
            var candidate = !string.IsNullOrWhiteSpace(rawName) ? rawName.Trim() : fallbackTitle?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            if (GeographicRegionTagRegex.IsMatch(candidate))
            {
                return candidate;
            }

            var effectiveRegion = string.IsNullOrWhiteSpace(regionLabel) ? "World" : regionLabel.Trim();
            var openParenIndex = candidate.IndexOf('(');
            if (openParenIndex > 0)
            {
                var prefix = candidate.Substring(0, openParenIndex).TrimEnd();
                var suffix = candidate.Substring(openParenIndex).TrimStart();
                return $"{prefix} ({effectiveRegion}) {suffix}";
            }

            return $"{candidate} ({effectiveRegion})";
        }

        private static List<string> ExtractLanguageTokens(string languages, string rawName)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(languages))
            {
                foreach (var part in languages.Split(new[] { ',', ';', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        list.Add(trimmed.ToUpperInvariant());
                    }
                }
            }

            if (list.Count == 0 && !string.IsNullOrWhiteSpace(rawName))
            {
                var match = TrailingLanguageTagRegex.Match(rawName);
                if (match.Success)
                {
                    foreach (var part in match.Groups["langs"].Value.Split(new[] { ',', '+' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = part.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            list.Add(trimmed.ToUpperInvariant());
                        }
                    }
                }
            }

            return list;
        }

        private static string NormalizeTitleArticle(string title)
        {
            return RomParser.NormalizeTitleArticle(title);
        }

        public static string CleanSceneReleaseTitle(string releaseName)
        {
            if (string.IsNullOrWhiteSpace(releaseName))
            {
                return string.Empty;
            }

            var clean = releaseName.Trim();
            var ext = Path.GetExtension(clean);
            if (!string.IsNullOrWhiteSpace(ext) && Regex.IsMatch(ext, @"^\.[A-Za-z0-9]{1,5}$"))
            {
                clean = Path.GetFileNameWithoutExtension(clean);
            }

            clean = Regex.Replace(clean, @"-[A-Za-z0-9_]+$", string.Empty);
            clean = Regex.Replace(clean, @"[._-](?:NSW|SWITCH|NINTENDO[._-]SWITCH|INTERNAL)(?=[._-]|$)", string.Empty, RegexOptions.IgnoreCase);
            clean = clean.Replace('_', ' ').Replace('.', ' ');
            clean = Regex.Replace(clean, @"\b(?:USA|EUR|JPN|JAP|ASIA|WORLD|NSW|GLOBAL|MULTI\d*)\b", string.Empty, RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\bv?\d+(?:\.\d+)+\b", string.Empty, RegexOptions.IgnoreCase);
            clean = Regex.Replace(clean, @"\([^\)]*\)|\[[^\]]*\]", " ");
            clean = Regex.Replace(clean, @"\s+", " ").Trim();

            return clean;
        }

        private static string StripParentheses(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var stripped = Regex.Replace(name, @"\([^\)]*\)|\[[^\]]*\]", " ").Trim();
            stripped = Regex.Replace(stripped, @"\s+", " ");
            return stripped;
        }

        public static string ExtractBaseTitle(string title, string finaleType = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var clean = title;
            if (!string.IsNullOrWhiteSpace(finaleType) &&
                !string.Equals(finaleType, "Retail (v1.0)", StringComparison.OrdinalIgnoreCase) &&
                clean.EndsWith($"({finaleType})", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - finaleType.Length - 2).Trim();
            }

            var parenIdx = clean.IndexOf(" (", StringComparison.Ordinal);
            var bracketIdx = clean.IndexOf(" [", StringComparison.Ordinal);
            var cutIdx = -1;
            if (parenIdx > 0 && bracketIdx > 0)
            {
                cutIdx = Math.Min(parenIdx, bracketIdx);
            }
            else if (parenIdx > 0)
            {
                cutIdx = parenIdx;
            }
            else if (bracketIdx > 0)
            {
                cutIdx = bracketIdx;
            }

            if (cutIdx > 0)
            {
                clean = clean.Substring(0, cutIdx).Trim();
            }

            clean = DiscTagRegex.Replace(clean, string.Empty).Trim(' ', '-', ':');
            return clean;
        }

        private static string CleanResidualRevisions(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var cleaned = ResidualRevisionRegex.Replace(title, string.Empty).Trim();
            cleaned = RomParser.CleanIndexPrefix(cleaned);
            return cleaned.Trim().TrimStart('-', ':', '.', '\'', '"', '!');
        }

        private static string StripArticle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var trimmed = title.Trim();
            if (trimmed.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(4).Trim();
            }

            if (trimmed.StartsWith("A ", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(2).Trim();
            }

            if (trimmed.StartsWith("An ", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(3).Trim();
            }

            return trimmed;
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
            var seasons = BuildDefaultRegions();
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
                SeasonFolder = true,
                PreferredArchiveFormat = "auto",
                OriginalLanguage = Language.English,
                Genres = new List<string> { "Platform", platform.Manufacturer },
                Ratings = new Ratings { Votes = 500, Value = 9.5m },
                Seasons = seasons,
                Images = images
            };
        }

        private Series MapGameToLookupSeries(GameCatalogEntry game)
        {
            var seasons = BuildDefaultRegions();
            foreach (var season in seasons)
            {
                season.Monitored = season.SeasonNumber == game.RegionNumber;
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
                SeasonFolder = true,
                PreferredArchiveFormat = "auto",
                OriginalLanguage = Language.English,
                Genres = new List<string> { "Game", game.PlatformShortName, game.RegionName, game.VerificationSource },
                Ratings = new Ratings { Votes = 250, Value = 9.2m },
                Seasons = seasons,
                Images = images
            };
        }

        private static List<Season> BuildDefaultRegions()
        {
            return new List<Season>
            {
                new Season { SeasonNumber = RegionUnlicensed, Title = "Unlicensed", Monitored = false },
                new Season { SeasonNumber = RegionUsa, Title = "USA", Monitored = true },
                new Season { SeasonNumber = RegionEurope, Title = "Europe", Monitored = true },
                new Season { SeasonNumber = RegionJapan, Title = "Japan", Monitored = true },
                new Season { SeasonNumber = RegionWorld, Title = "World", Monitored = true },
                new Season { SeasonNumber = RegionHomebrew, Title = "Homebrew", Monitored = false },
                new Season { SeasonNumber = RegionRomHacks, Title = "ROM Hacks", Monitored = false }
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
            var sourceLabel = "No-Intro / Redump DAT";
            if (!string.IsNullOrWhiteSpace(datEntry.SmdbPath))
            {
                var smdbParts = datEntry.SmdbPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (smdbParts.Any(s => s.Contains("Translation", StringComparison.OrdinalIgnoreCase) ||
                                       s.Contains("Hack", StringComparison.OrdinalIgnoreCase) ||
                                       s.Contains("Enhancement", StringComparison.OrdinalIgnoreCase) ||
                                       s.Contains("Color", StringComparison.OrdinalIgnoreCase) ||
                                       s.Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                                       s.Contains("Fix", StringComparison.OrdinalIgnoreCase)))
                {
                    sourceLabel = "ROM Hacks & Translations";
                }
                else if (smdbParts.Any(s => s.Contains("Homebrew", StringComparison.OrdinalIgnoreCase) ||
                                           s.Contains("Aftermarket", StringComparison.OrdinalIgnoreCase)))
                {
                    sourceLabel = "Homebrew & Aftermarket";
                }
                else if (smdbParts.Any(s => s.Contains("Unlicensed", StringComparison.OrdinalIgnoreCase) ||
                                           s.Contains("Pirate", StringComparison.OrdinalIgnoreCase) ||
                                           s.Contains("Bootleg", StringComparison.OrdinalIgnoreCase)))
                {
                    sourceLabel = "Unlicensed";
                }
            }

            var entry = CreateCatalogEntryFromNoIntroName(platform, rawName, platform.ReleaseYear, sourceLabel);
            if (entry != null)
            {
                entry.ExpectedCrc32 = datEntry.Crc32;
                entry.ExpectedMd5 = datEntry.Md5;
                entry.ExpectedSha1 = datEntry.Sha1;
                entry.ExpectedSha256 = datEntry.Sha256;
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
                RegionUsa => "USA",
                RegionEurope => "Europe",
                RegionJapan => "Japan",
                RegionWorld => "World",
                RegionUnlicensed => "Unlicensed",
                RegionRomHacks => "ROM Hacks & Translations",
                RegionHomebrew => "Homebrew & Aftermarket",
                _ => $"Region {seasonNumber}"
            };
        }

        public static int GetRegionPriority(int seasonNumber)
        {
            return seasonNumber switch
            {
                RegionUsa => 1,
                RegionEurope => 2,
                RegionJapan => 3,
                RegionWorld => 4,
                _ => 10 + seasonNumber
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

        public static string GetRegionFolderName(int regionNumber)
        {
            return regionNumber switch
            {
                RegionUnlicensed => "Unlicensed",
                RegionUsa => "USA",
                RegionEurope => "Europe",
                RegionJapan => "Japan",
                RegionWorld => "World",
                RegionHomebrew => "Homebrew",
                RegionRomHacks => "ROM Hacks",
                RegionBios => "BIOS",
                _ => "USA"
            };
        }

        public static string ResolveCanonicalRegionFolderForRom(string relativeOrFullPath, string fileName)
        {
            var effectiveFileName = !string.IsNullOrWhiteSpace(fileName)
                ? fileName
                : Path.GetFileName(relativeOrFullPath ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(effectiveFileName) &&
                effectiveFileName.StartsWith("[BIOS]", StringComparison.OrdinalIgnoreCase))
            {
                return "BIOS";
            }

            if (!string.IsNullOrWhiteSpace(relativeOrFullPath))
            {
                var segments = relativeOrFullPath
                    .Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments.Any(s => s.Equals("BIOS", StringComparison.OrdinalIgnoreCase)))
                {
                    return "BIOS";
                }
            }

            var noIntroBase = Path.GetFileNameWithoutExtension(effectiveFileName ?? string.Empty);
            var parsed = RomParser.ParseRomName(effectiveFileName ?? string.Empty);
            var regionNumber = ClassifyRegionAndCategory(noIntroBase, parsed, relativeOrFullPath);

            return GetRegionFolderName(regionNumber);
        }

        public static string GetPlatformOverview(string slug, int tvdbId = 0)
        {
            var entry = BuiltInPlatforms.FirstOrDefault(p =>
                (tvdbId > 0 && p.TvdbId == tvdbId) ||
                (!string.IsNullOrWhiteSpace(slug) && string.Equals(p.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase)));

            return entry?.Overview;
        }

        private static List<PlatformCatalogEntry> BuildPlatforms()
        {
            return new List<PlatformCatalogEntry>
            {
                CreatePlatform(
                    1001,
                    "Nintendo Entertainment System",
                    "NES",
                    "nes",
                    "Nintendo",
                    1985,
                    "Nintendo - Nintendo Entertainment System",
                    "Super Mario Bros. 3 (USA)",
                    new[] { "NES", "Famicom", "FC", "Nintendo NES" },
                    new[] { ".nes", ".unf", ".zip", ".7z" },
                    "Nintendo's landmark 8-bit home console (released in Japan as the Family Computer in 1983 and North America in 1985) revitalized the video game industry. Powered by the Ricoh 2A03 processor and custom cartridge mapper chips (MMC1–MMC5), it introduced foundational franchises including Super Mario Bros., The Legend of Zelda, Metroid, Castlevania, and Mega Man."),
                CreatePlatform(
                    1002,
                    "Famicom Disk System",
                    "FDS",
                    "fds",
                    "Nintendo",
                    1986,
                    "Nintendo - Family Computer Disk System",
                    "Zelda no Densetsu - The Hyrule Fantasy (Japan)",
                    new[] { "FDS", "Disk System" },
                    new[] { ".fds", ".zip", ".7z" },
                    "Released exclusively in Japan in 1986 as a peripheral for the Family Computer, the Famicom Disk System used proprietary Quick Disk media with 112 KB of rewritable storage and an extra wavetable audio channel. It debuted The Legend of Zelda, Metroid, Kid Icarus, and Super Mario Bros.: The Lost Levels with native save functionality andDisk Writer kiosk support."),
                CreatePlatform(
                    1003,
                    "Super Nintendo Entertainment System",
                    "SNES",
                    "snes",
                    "Nintendo",
                    1990,
                    "Nintendo - Super Nintendo Entertainment System",
                    "Super Mario World (USA)",
                    new[] { "SNES", "Super NES", "Super Nintendo", "Super Famicom", "SFC" },
                    new[] { ".sfc", ".smc", ".zip", ".7z" },
                    "Nintendo's 16-bit flagship console (known as the Super Famicom in Japan) defined an era of pixel art and orchestral chip music via its Ricoh 5A22 CPU, Mode 7 affine background scaling, and Sony SPC700 8-channel ADPCM sound processor. Enhancement chips like the Super FX and SA-1 powered classics such as Super Mario World, Chrono Trigger, Super Metroid, and Star Fox."),
                CreatePlatform(
                    1004,
                    "Satellaview",
                    "BS-X",
                    "satellaview",
                    "Nintendo",
                    1995,
                    "Nintendo - Satellaview",
                    "BS Zelda no Densetsu (Japan)",
                    new[] { "BSX", "BS-X" },
                    new[] { ".bs", ".sfc", ".zip" },
                    "A satellite modem peripheral for the Japanese Super Famicom operated by Nintendo and St.GIGA between 1995 and 2000. Players tuned into live 'SoundLink' broadcasts through the BS-X town hub to download episodic remakes and exclusives such as BS Zelda no Densetsu, Radical Dreamers, and BS F-Zero Grand Prix onto rewritable 8M Memory Packs."),
                CreatePlatform(
                    1005,
                    "Nintendo 64",
                    "N64",
                    "n64",
                    "Nintendo",
                    1996,
                    "Nintendo - Nintendo 64",
                    "Super Mario 64 (USA)",
                    new[] { "N64", "Ultra 64" },
                    new[] { ".z64", ".n64", ".v64", ".zip", ".7z" },
                    "Engineered with Silicon Graphics around a 64-bit NEC VR4300 CPU and Reality Coprocessor, the Nintendo 64 pioneered 3D camera control, analog thumbstick movement, force-feedback Rumble Pak peripherals, and four-player built-in multiplayer. Its cartridge library features all-time classics including Super Mario 64, The Legend of Zelda: Ocarina of Time, and GoldenEye 007."),
                CreatePlatform(
                    1006,
                    "64DD",
                    "64DD",
                    "64dd",
                    "Nintendo",
                    1999,
                    "Nintendo - Nintendo 64DD",
                    "F-Zero X Expansion Kit (Japan)",
                    new[] { "N64DD" },
                    new[] { ".ndd", ".zip" },
                    "A magnetic disk drive attachment for the Nintendo 64 released in Japan in 1999 using 64 MB rewritable magneto-optical disks and a built-in real-time clock. Celebrated by preservationists for creative suites and expansions like the Mario Artist series, Doshin the Giant, SimCity 64, and the F-Zero X Expansion Kit."),
                CreatePlatform(
                    1007,
                    "GameCube",
                    "GCN",
                    "gamecube",
                    "Nintendo",
                    2001,
                    "Nintendo - GameCube",
                    "Super Smash Bros. Melee (USA)",
                    new[] { "GCN", "NGC", "Nintendo GameCube" },
                    new[] { ".rvz", ".iso", ".gcm", ".ciso" },
                    "Nintendo's sixth-generation compact console combined an IBM PowerPC 'Gekko' processor and ATI 'Flipper' GPU with 1.5 GB miniDVD optical discs and the iconic WaveBird wireless controller. Home to Super Smash Bros. Melee, Metroid Prime, The Legend of Zelda: The Wind Waker, and Luigi's Mansion."),
                CreatePlatform(
                    1008,
                    "Wii",
                    "Wii",
                    "wii",
                    "Nintendo",
                    2006,
                    "Nintendo - Wii",
                    "Super Mario Galaxy (USA)",
                    new[] { "Nintendo Wii", "RVL" },
                    new[] { ".rvz", ".wbfs", ".iso" },
                    "Revolutionizing home gaming with the motion-sensing Wii Remote and Nunchuk, the Nintendo Wii brought accessible multiplayer and innovative 3D design to over 100 million households while maintaining full hardware backward compatibility with the GameCube. Highlights include Super Mario Galaxy, Wii Sports, Xenoblade Chronicles, and Metroid Prime Trilogy."),
                CreatePlatform(
                    1009,
                    "Game Boy",
                    "GB",
                    "gb",
                    "Nintendo",
                    1989,
                    "Nintendo - Game Boy",
                    "Tetris (World) (Rev A)",
                    new[] { "GB", "GameBoy", "DMG" },
                    new[] { ".gb", ".zip", ".7z" },
                    "Designed by Gunpei Yokoi and Nintendo R&D1, the original dot-matrix Game Boy (DMG-01) paired a Sharp LR35902 8-bit processor and four-shade reflective LCD with legendary battery life. Bundled with Tetris and later igniting the global Pokémon phenomenon, it established portable gaming as a cornerstone of the medium."),
                CreatePlatform(
                    1010,
                    "Game Boy Color",
                    "GBC",
                    "gbc",
                    "Nintendo",
                    1998,
                    "Nintendo - Game Boy Color",
                    "Pokemon - Crystal Version (USA, Europe)",
                    new[] { "GBC", "GameBoy Color", "CGB" },
                    new[] { ".gbc", ".zip", ".7z" },
                    "Doubling the clock speed and memory of the original Game Boy while adding a 56-color simultaneous palette from 32,768 colors and an infrared communications port, the Game Boy Color bridged Nintendo's 8-bit handheld era with titles like Pokémon Gold, Silver & Crystal, The Legend of Zelda: Oracle of Ages & Seasons, and Shantae."),
                CreatePlatform(
                    1011,
                    "Game Boy Advance",
                    "GBA",
                    "gba",
                    "Nintendo",
                    2001,
                    "Nintendo - Game Boy Advance",
                    "Pokemon - Emerald Version (USA, Europe)",
                    new[] { "GBA", "GameBoy Advance", "AGB" },
                    new[] { ".gba", ".zip", ".7z" },
                    "Powered by a 32-bit ARM7TDMI processor alongside a Z80 coprocessor for backward compatibility, the Game Boy Advance delivered supercharged 2D sprite scaling and affine effects in the palm of your hand. Its library represents a golden age of 2D craftsmanship including Metroid Fusion, Castlevania: Aria of Sorrow, Pokémon Emerald, and Mother 3."),
                CreatePlatform(
                    1012,
                    "Nintendo DS",
                    "NDS",
                    "nds",
                    "Nintendo",
                    2004,
                    "Nintendo - Nintendo DS",
                    "Pokemon - HeartGold Version (USA)",
                    new[] { "NDS", "DS", "Nintendo DSi" },
                    new[] { ".nds", ".zip", ".7z" },
                    "Featuring a clamshell dual-screen layout with a resistive lower touchscreen, built-in microphone, and IEEE 802.11 wireless connectivity, the Nintendo DS became the best-selling handheld console in history. Its diverse library spans Pokémon HeartGold & SoulSilver, Mario Kart DS, The World Ends with You, and Chrono Trigger."),
                CreatePlatform(
                    1013,
                    "Nintendo 3DS",
                    "3DS",
                    "3ds",
                    "Nintendo",
                    2011,
                    "Nintendo - Nintendo 3DS",
                    "Pokemon X (USA)",
                    new[] { "3DS", "N3DS", "New 3DS" },
                    new[] { ".3ds", ".cia", ".cci", ".zip" },
                    "Nintendo's glasses-free autostereoscopic 3D handheld introduced the Circle Pad, StreetPass/SpotPass social connectivity, and a custom PICA200 GPU. Its catalog blends landmark first-party entries and RPGs such as The Legend of Zelda: A Link Between Worlds, Fire Emblem: Awakening, Super Mario 3D Land, and Pokémon X & Y."),
                CreatePlatform(
                    1014,
                    "Nintendo Switch",
                    "Switch",
                    "switch",
                    "Nintendo",
                    2017,
                    "Nintendo - Switch",
                    "Pokemon Legends - Arceus (World)",
                    new[] { "Switch", "NSW", "NX" },
                    new[] { ".nsp", ".xci", ".nsz" },
                    "Combining home console dock output with portable handheld play and detachable Joy-Con controllers around a custom NVIDIA Tegra processor, the Nintendo Switch unified Nintendo's console and portable software pipelines. Acclaimed for The Legend of Zelda: Breath of the Wild, Super Mario Odyssey, Metroid Dread, and Super Smash Bros. Ultimate."),
                CreatePlatform(
                    1015,
                    "Sega Genesis",
                    "Genesis",
                    "genesis",
                    "Sega",
                    1988,
                    "Sega - Mega Drive - Genesis",
                    "Sonic The Hedgehog 2 (World)",
                    new[] { "Genesis", "Mega Drive", "MD", "Sega Mega Drive" },
                    new[] { ".md", ".gen", ".bin", ".zip", ".7z" },
                    "Known internationally as the Mega Drive, Sega's 16-bit powerhouse paired a 7.6 MHz Motorola 68000 CPU and Zilog Z80 sound controller with the unmistakable grit of the Yamaha YM2612 FM synthesizer. Famous for 'Blast Processing' speed, arcade conversions, Sonic the Hedgehog, Streets of Rage 2, Gunstar Heroes, and Phantasy Star IV."),
                CreatePlatform(
                    1016,
                    "Sega CD",
                    "Sega CD",
                    "segacd",
                    "Sega",
                    1991,
                    "Sega - Mega-CD - Sega CD",
                    "Sonic CD (USA)",
                    new[] { "Sega CD", "Mega CD", "Mega-CD", "SCD" },
                    new[] { ".chd", ".cue", ".bin", ".iso" },
                    "Sega's CD-ROM expansion for the Genesis / Mega Drive added a faster 12.5 MHz Motorola 68000 coprocessor, hardware sprite scaling and rotation, Red Book CD audio, and 8-channel Ricoh PCM sound. Best known for Sonic CD, Snatcher, Lunar: Eternal Blue, Final Fight CD, and Popful Mail."),
                CreatePlatform(
                    1017,
                    "Sega 32X",
                    "32X",
                    "32x",
                    "Sega",
                    1994,
                    "Sega - 32X",
                    "Knuckles' Chaotix (Japan, USA)",
                    new[] { "32X", "Super 32X" },
                    new[] { ".32x", ".zip", ".7z" },
                    "Codenamed Project Mars, the Sega 32X cartridge add-on upgraded the Genesis with dual 32-bit Hitachi SH-2 RISC processors, a 32,768-color palette, and PWM digital audio. Though short-lived, it delivered standout 32-bit arcade ports including Virtua Racing Deluxe, Star Wars Arcade, Doom, and Knuckles' Chaotix."),
                CreatePlatform(
                    1018,
                    "Sega Saturn",
                    "Saturn",
                    "saturn",
                    "Sega",
                    1994,
                    "Sega - Saturn",
                    "Nights into Dreams... (USA)",
                    new[] { "Saturn", "SS" },
                    new[] { ".chd", ".cue", ".bin", ".iso" },
                    "Engineered with a complex dual-SH-2 CPU and dual-VDP video architecture alongside RAM expansion cartridge slot support, the Sega Saturn became an unmatched powerhouse for 2D fighting games, shoot-'em-ups, and distinctive 3D classics like Panzer Dragoon Saga, Radiant Silvergun, Nights into Dreams, and Saturn Bomberman."),
                CreatePlatform(
                    1019,
                    "Sega Dreamcast",
                    "Dreamcast",
                    "dreamcast",
                    "Sega",
                    1998,
                    "Sega - Dreamcast",
                    "Sonic Adventure (USA)",
                    new[] { "Dreamcast", "DC" },
                    new[] { ".chd", ".gdi", ".cdi", ".cue" },
                    "Sega's final home console was ahead of its time, introducing a built-in modem for online play, 1 GB GD-ROM optical discs, 480p VGA crispness via its Hitachi SH-4 CPU and PowerVR2 GPU, and the interactive Visual Memory Unit (VMU). Beloved for Soulcalibur, Jet Set Radio, Shenmue, Phantasy Star Online, and Marvel vs. Capcom 2."),
                CreatePlatform(
                    1020,
                    "TurboGrafx-16",
                    "TG16",
                    "tg16",
                    "NEC",
                    1987,
                    "NEC - PC Engine - TurboGrafx 16",
                    "Bonk's Adventure (USA)",
                    new[] { "TG16", "PC Engine", "PCE", "TurboGrafx" },
                    new[] { ".pce", ".zip", ".7z" },
                    "Co-developed by Hudson Soft and NEC (released in Japan as the PC Engine), the TurboGrafx-16 combined an efficient 8-bit HuC6280 CPU with dual 16-bit graphics processors and credit-card-sized HuCard ROM media. Celebrated for its vibrant color palettes and legendary scrolling shooters like Blazing Lazers, R-Type, Soldier Blade, and Bonk's Adventure."),
                CreatePlatform(
                    1021,
                    "TurboGrafx-CD",
                    "TG-CD",
                    "tgcd",
                    "NEC",
                    1988,
                    "NEC - PC Engine CD - TurboGrafx-CD",
                    "Akumajou Dracula X - Chi no Rondo (Japan)",
                    new[] { "TGCD", "PC Engine CD", "PCECD" },
                    new[] { ".chd", ".cue", ".bin" },
                    "The world's first CD-ROM video game console peripheral (later integrated into the TurboDuo and PC Engine Duo), evolving across System Card 1.0, 2.0, 3.0, and Arcade Card RAM upgrades. Home to Castlevania: Rondo of Blood, Ys Book I & II, Lords of Thunder, and Gate of Thunder."),
                CreatePlatform(
                    1022,
                    "PlayStation",
                    "PS1",
                    "ps1",
                    "Sony",
                    1994,
                    "Sony - PlayStation",
                    "Castlevania - Symphony of the Night (USA)",
                    new[] { "PS1", "PSX", "PSOne", "Sony PlayStation" },
                    new[] { ".chd", ".pbp", ".cue", ".bin", ".iso" },
                    "Sony's debut home console ushered in the 3D CD-ROM revolution with its 32-bit MIPS R3000A CPU, Geometry Transformation Engine, and DualShock analog controller. Becoming the first console to ship 100 million units, it hosted Final Fantasy VII, Metal Gear Solid, Castlevania: Symphony of the Night, Gran Turismo, and Resident Evil 2."),
                CreatePlatform(
                    1023,
                    "PlayStation 2",
                    "PS2",
                    "ps2",
                    "Sony",
                    2000,
                    "Sony - PlayStation 2",
                    "Shadow of the Colossus (USA)",
                    new[] { "PS2", "Sony PlayStation 2" },
                    new[] { ".chd", ".iso", ".cso", ".bin" },
                    "Powered by the custom 128-bit 'Emotion Engine' CPU, 'Graphics Synthesizer' GPU, and DVD-ROM drive with hardware PS1 backward compatibility, the PlayStation 2 stands as the best-selling home console in gaming history. Its vast catalog spans Shadow of the Colossus, Metal Gear Solid 3: Snake Eater, God of War II, Final Fantasy X, and Silent Hill 2."),
                CreatePlatform(
                    1024,
                    "PlayStation Portable",
                    "PSP",
                    "psp",
                    "Sony",
                    2004,
                    "Sony - PlayStation Portable",
                    "Crisis Core - Final Fantasy VII (USA)",
                    new[] { "PSP", "Sony PSP" },
                    new[] { ".iso", ".cso", ".pbp", ".chd" },
                    "Sony's first handheld system brought console-grade 3D graphics to a 4.3-inch widescreen LCD using Universal Media Disc (UMD) optical media and Memory Stick PRO Duo storage. Standout releases include Crisis Core: Final Fantasy VII, Monster Hunter Freedom Unite, Metal Gear Solid: Peace Walker, God of War: Ghost of Sparta, and Tactics Ogre."),
                CreatePlatform(
                    1025,
                    "Jaguar CD",
                    "Jaguar CD",
                    "jaguarcd",
                    "Atari",
                    1995,
                    "Atari - Jaguar CD",
                    "Tempest 2000 (USA)",
                    new[] { "Atari Jaguar CD" },
                    new[] { ".chd", ".cue", ".bin" },
                    "Atari's top-mounted 2x CD-ROM attachment for the Atari Jaguar featured a built-in cartridge pass-through port, Jeff Minter's Virtual Light Machine (VLM) audio-visual synthesizer, and Memory Track save cartridge support for titles such as Battlemorph, Primal Rage, and Myst."),
                CreatePlatform(
                    1026,
                    "Wii U",
                    "Wii U",
                    "wiiu",
                    "Nintendo",
                    2012,
                    "Nintendo - Wii U",
                    "Mario Kart 8 (USA)",
                    new[] { "WiiU", "Nintendo Wii U" },
                    new[] { ".wua", ".wux", ".wud", ".rpx" },
                    "Nintendo's first high-definition console introduced asymmetric second-screen gameplay and off-TV streaming via the Wii U GamePad tablet controller. Its first-party library laid the groundwork for the modern era with Mario Kart 8, Splatoon, Super Mario Maker, Xenoblade Chronicles X, and The Legend of Zelda: The Wind Waker HD."),
                CreatePlatform(
                    1027,
                    "Virtual Boy",
                    "VB",
                    "vb",
                    "Nintendo",
                    1995,
                    "Nintendo - Virtual Boy",
                    "Virtual Boy Wario Land (Japan, USA)",
                    new[] { "VB" },
                    new[] { ".vb", ".zip" },
                    "Gunpei Yokoi's tabletop 32-bit stereoscopic 3D system used oscillating dual red LED linear arrays and a dual-D-pad controller to render true parallax depth around an NEC V810 RISC processor. Though only 22 official titles were released, Virtual Boy Wario Land, Jack Bros., and Galactic Pinball remain cult favorites."),
                CreatePlatform(
                    1028,
                    "Sega Master System",
                    "SMS",
                    "sms",
                    "Sega",
                    1985,
                    "Sega - Master System - Mark III",
                    "Phantasy Star (USA, Europe)",
                    new[] { "SMS", "Master System", "Mark III" },
                    new[] { ".sms", ".zip", ".7z" },
                    "Evolved from the Japanese Sega Mark III, the 8-bit Master System boasted a Zilog Z80 CPU, rich color hardware, both Sega Card and Mega Cartridge slots, and optional FM Synthesis audio (YM2413). A massive hit across Europe and Brazil, it debuted Phantasy Star, Alex Kidd in Miracle World, Wonder Boy III: The Dragon's Trap, and Master of Darkness."),
                CreatePlatform(
                    1029,
                    "Sega Game Gear",
                    "GG",
                    "gg",
                    "Sega",
                    1990,
                    "Sega - Game Gear",
                    "Sonic The Hedgehog (World)",
                    new[] { "GG", "Game Gear" },
                    new[] { ".gg", ".zip", ".7z" },
                    "Sega's full-color backlit 8-bit handheld shared core architecture with the Master System while expanding the color palette to 4,096 colors in a comfortable landscape grip. Its portable library includes Shinobi, Sonic Chaos, Defenders of Oasis, Ristar, and Tails Adventure."),
                CreatePlatform(
                    1030,
                    "PlayStation 3",
                    "PS3",
                    "ps3",
                    "Sony",
                    2006,
                    "Sony - PlayStation 3",
                    "Demon's Souls (USA)",
                    new[] { "PS3", "Sony PlayStation 3" },
                    new[] { ".iso", ".pkg" },
                    "Built around the multi-core Cell Broadband Engine, NVIDIA RSX 'Reality Synthesizer' GPU, and high-capacity Blu-ray Disc drive, the PlayStation 3 ushered Sony into the HD era and free PlayStation Network online play. Iconic titles include The Last of Us, Demon's Souls, Metal Gear Solid 4: Guns of the Patriots, and Uncharted 2: Among Thieves."),
                CreatePlatform(
                    1031,
                    "PlayStation Vita",
                    "PS Vita",
                    "psvita",
                    "Sony",
                    2011,
                    "Sony - PlayStation Vita",
                    "Persona 4 Golden (USA)",
                    new[] { "Vita", "PSV", "PS Vita" },
                    new[] { ".vpk", ".zip" },
                    "Featuring a vibrant 5-inch OLED display (on original PCH-1000 models), dual analog sticks, front touchscreen, and rear touchpad powered by a quad-core ARM Cortex-A9 CPU, the PlayStation Vita became a beloved haven for JRPGs and indie classics led by Persona 4 Golden, Gravity Rush, Tearaway, and Uncharted: Golden Abyss."),
                CreatePlatform(
                    1032,
                    "Xbox",
                    "Xbox",
                    "xbox",
                    "Microsoft",
                    2001,
                    "Microsoft - Xbox",
                    "Halo - Combat Evolved (USA)",
                    new[] { "Original Xbox", "OG Xbox" },
                    new[] { ".iso", ".xiso" },
                    "Microsoft's debut sixth-generation console brought PC-class architecture to the living room with a 733 MHz Intel Pentium III CPU, custom NVIDIA NV2A GPU, built-in internal hard drive for save data and custom soundtracks, and broadband Ethernet for Xbox Live. Home to Halo: Combat Evolved, Ninja Gaiden Black, Panzer Dragoon Orta, and Star Wars: Knights of the Old Republic."),
                CreatePlatform(
                    1033,
                    "Xbox 360",
                    "Xbox 360",
                    "xbox360",
                    "Microsoft",
                    2005,
                    "Microsoft - Xbox 360",
                    "Halo 3 (USA)",
                    new[] { "X360", "360" },
                    new[] { ".iso", ".xex", ".god" },
                    "Powered by a triple-core IBM PowerPC 'Xenon' CPU and ATI 'Xenos' GPU with unified shaders, the Xbox 360 defined modern online multiplayer, Achievements, and digital indie distribution via Xbox Live Arcade. Its acclaim spans Halo 3, Gears of War, Forza Horizon, Lost Odyssey, and Red Dead Redemption."),
                CreatePlatform(
                    1034,
                    "Atari 2600",
                    "2600",
                    "atari2600",
                    "Atari",
                    1977,
                    "Atari - 2600",
                    "Pitfall! - Pitfall Harry's Jungle Adventure (USA)",
                    new[] { "2600", "VCS" },
                    new[] { ".a26", ".bin", ".zip" },
                    "Originally launched in 1977 as the Atari Video Computer System (VCS), the Atari 2600 popularized microprocessor-based ROM cartridges and the iconic single-button CX40 joystick. With just 128 bytes of RAM on its MOS 6507/TIA chipset, developers crafted enduring classics like Pitfall!, Adventure, Yars' Revenge, River Raid, and Space Invaders."),
                CreatePlatform(
                    1035,
                    "Atari 7800",
                    "7800",
                    "atari7800",
                    "Atari",
                    1986,
                    "Atari - 7800",
                    "Food Fight (USA)",
                    new[] { "7800" },
                    new[] { ".a78", ".bin", ".zip" },
                    "Designed by General Computer Corporation with full hardware backward compatibility for Atari 2600 cartridges, the Atari 7800 ProSystem used its custom MARIA graphics chip to move dozens of colorful sprites simultaneously without flicker in arcade hits like Food Fight, Centipede, Ballblazer, and Ninja Golf."),
                CreatePlatform(
                    1036,
                    "Atari Lynx",
                    "Lynx",
                    "lynx",
                    "Atari",
                    1989,
                    "Atari - Lynx",
                    "California Games (USA, Europe)",
                    new[] { "Lynx" },
                    new[] { ".lnx", ".zip" },
                    "Originally engineered by Epyx as the 'Handy', the Atari Lynx was the world's first handheld console with a color backlit LCD, hardware sprite scaling and distortion, ambidextrous flip-screen play, and up to 17-player ComLynx networking. Highlights include California Games, Chip's Challenge, Todd's Adventures in Slime World, and Blue Lightning."),
                CreatePlatform(
                    1037,
                    "Atari Jaguar",
                    "Jaguar",
                    "jaguar",
                    "Atari",
                    1993,
                    "Atari - Jaguar",
                    "Alien vs Predator (World)",
                    new[] { "Jaguar" },
                    new[] { ".j64", ".jag", ".zip" },
                    "Marketed as the first 64-bit home console, the Atari Jaguar utilized a multi-chip architecture featuring the custom 'Tom' and 'Jerry' RISC processors alongside a Motorola 68000 controller. Its library is headlined by Jeff Minter's Tempest 2000, Rebellion's Alien vs Predator, Rayman, and Iron Soldier."),
                CreatePlatform(
                    1038,
                    "Neo Geo",
                    "Neo Geo",
                    "neogeo",
                    "SNK",
                    1990,
                    "SNK - Neo Geo",
                    "Metal Slug 3 (World)",
                    new[] { "AES", "MVS", "Neo-Geo", "NGCD" },
                    new[] { ".zip", ".7z", ".neo", ".chd" },
                    "SNK's legendary 24-bit arcade and home console platform (spanning MVS arcade cabinets, AES home cartridges, and Neo Geo CD) paired a Motorola 68000 CPU and Z80 audio processor with massive multi-hundred-megabit ROM cartridges. Revered for peerless 2D pixel art in Metal Slug, The King of Fighters '98, Garou: Mark of the Wolves, and Samurai Shodown II."),
                CreatePlatform(
                    1039,
                    "Neo Geo Pocket Color",
                    "NGPC",
                    "ngpc",
                    "SNK",
                    1999,
                    "SNK - Neo Geo Pocket Color",
                    "SNK vs. Capcom - The Match of the Millennium (World)",
                    new[] { "NGPC", "NGP" },
                    new[] { ".ngc", ".ngp", ".zip" },
                    "SNK's 16-bit color handheld (powered by a Toshiba TLCS-900/H CPU) is celebrated for its crisp reflective screen, 40-hour battery life, and tactile microswitch 8-way thumbstick. Its compact library boasts masterpieces like SNK vs. Capcom: The Match of the Millennium, Sonic the Hedgehog Pocket Adventure, and Metal Slug: 1st & 2nd Mission."),
                CreatePlatform(
                    1040,
                    "WonderSwan Color",
                    "WSC",
                    "wonderswan",
                    "Bandai",
                    2000,
                    "Bandai - WonderSwan Color",
                    "Final Fantasy (Japan)",
                    new[] { "WonderSwan", "WSC", "WS" },
                    new[] { ".wsc", ".ws", ".zip" },
                    "Conceived by Game Boy creator Gunpei Yokoi's company Koto Laboratory and published by Bandai in Japan, the WonderSwan and WonderSwan Color feature dual directional button layouts for both horizontal and vertical play on a single AA battery. Famous for Squaresoft's remakes of Final Fantasy I, II, and IV, Judgement Silversword, and Gunpey.")
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
            string[] extensions,
            string overview = null)
        {
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
                Overview = !string.IsNullOrWhiteSpace(overview) ? overview : $"{manufacturer} hardware platform ({year}).",
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
                var isDualEncryptionPlatform = slug == "nds" || slug == "3ds";
                var effectiveSource = isDualEncryptionPlatform && source == "No-Intro" ? "No-Intro (Decrypted)" : source;
                var entry = CreateCatalogEntryFromNoIntroName(platform, noIntroName, year, effectiveSource, summary);
                if (entry != null)
                {
                    entry.TvdbId = id++;
                    games.Add(entry);
                }

                if (isDualEncryptionPlatform && !noIntroName.Contains("(Encrypted)", StringComparison.OrdinalIgnoreCase))
                {
                    var encryptedEntry = CreateCatalogEntryFromNoIntroName(
                        platform,
                        $"{noIntroName} (Encrypted)",
                        year,
                        "No-Intro (Encrypted)",
                        $"{summary} (Encrypted ROM dump)");

                    if (encryptedEntry != null)
                    {
                        encryptedEntry.TvdbId = id++;
                        games.Add(encryptedEntry);
                    }
                }
            }

            // Pokémon Series (GB, GBC, GBA, N64, GCN, NDS, 3DS, Switch)
            Add("gb", "Pokemon - Red Version (USA, Europe)", 1998, "Begin your journey in the Kanto region to catch all 151 original Pokemon and defeat the Elite Four.");
            Add("gb", "Pokemon - Blue Version (USA, Europe)", 1998, "Classic Kanto RPG adventure featuring version-exclusive Pokemon and link cable trading.");
            Add("gb", "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)", 1999, "Travel across Kanto with Pikachu following right behind you and obtain Bulbasaur, Charmander, and Squirtle.");
            Add("gb", "Pocket Monsters - Aka (Japan)", 1996, "Original Japanese release of Pocket Monsters Red Version for Game Boy.");
            Add("gb", "Pocket Monsters - Aka (Japan) (Rev A)", 1996, "Revised Japanese release of Pocket Monsters Red Version for Game Boy.");
            Add("gb", "Pocket Monsters - Midori (Japan)", 1996, "Original Japanese release of Pocket Monsters Green Version for Game Boy.");
            Add("gb", "Pokemon - Brown Version (USA) (Hack)", 2014, "Acclaimed full-scale Game Boy ROM hack by Koolboyman set in the Rijon region with new Pokemon, types, and Gym Badges.", "SmokeMonster HTGDB / SMDB");
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
            Add("3ds", "Pokemon Sun (USA)", 2016, "Explore the tropical Alola region with legendary Solgaleo and regional Alolan variants.");
            Add("3ds", "Pokemon Moon (USA)", 2016, "Explore the tropical Alola region with legendary Lunala and island challenge trials.");
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
            Add("gb", "Super Mario Land 2 DX (USA) (Hack)", 2017, "Full color DX hack by toruzz of Super Mario Land 2 with enhanced Luigi gameplay and color graphics.", "SmokeMonster HTGDB / SMDB");
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
