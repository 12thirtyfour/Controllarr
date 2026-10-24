using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
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
        public const int RegionUsaSeason = 1;
        public const int RegionEuropeSeason = 2;
        public const int RegionJapanSeason = 3;
        public const int RegionWorldSeason = 4;
        public const int RegionUnofficialSeason = 0;

        private static readonly List<PlatformCatalogEntry> BuiltInPlatforms = BuildPlatforms();
        private static readonly List<GameCatalogEntry> BuiltInGames = BuildGames();
        private static readonly ConcurrentDictionary<int, GameCatalogEntry> DynamicGames = new ConcurrentDictionary<int, GameCatalogEntry>();
        private static readonly ConcurrentDictionary<int, HashSet<int>> PendingPlatformGameSelections = new ConcurrentDictionary<int, HashSet<int>>();

        private readonly IDatCacheService _datCacheService;
        private readonly IGameTitleRepository _gameTitleRepository;
        private readonly IGameMetadataProxy _gameMetadataProxy;
        private readonly Logger _logger;

        public GameCatalogProxy(
            IDatCacheService datCacheService,
            IGameTitleRepository gameTitleRepository,
            IGameMetadataProxy gameMetadataProxy,
            Logger logger)
        {
            _datCacheService = datCacheService;
            _gameTitleRepository = gameTitleRepository;
            _gameMetadataProxy = gameMetadataProxy;
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

            var fallbackPlatform = new PlatformCatalogEntry
            {
                TvdbId = tvdbSeriesId,
                Name = $"Platform {tvdbSeriesId}",
                ShortName = $"Platform {tvdbSeriesId}",
                Slug = $"platform-{tvdbSeriesId}",
                Manufacturer = "Console",
                ReleaseYear = 1995,
                Overview = "Verified console & handheld ROM platform managed by Controllarr.",
                Extensions = new List<string> { ".zip", ".7z", ".chd", ".iso" }
            };

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
                foreach (var platformGame in BuiltInGames.Where(g => g.PlatformTvdbId == platformId).Take(15))
                {
                    if (seenTvdbIds.Add(platformGame.TvdbId))
                    {
                        results.Add(MapGameToLookupSeries(platformGame));
                    }
                }
            }

            if (results.Count == 0)
            {
                var dynamicFallbacks = CreateDynamicFallbackGames(rawQuery);
                foreach (var fallback in dynamicFallbacks)
                {
                    if (seenTvdbIds.Add(fallback.TvdbId))
                    {
                        results.Add(MapGameToLookupSeries(fallback));
                    }
                }
            }

            return results;
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

            var platformGames = BuiltInGames
                .Where(g => g.PlatformTvdbId == platform.TvdbId)
                .ToList();

            foreach (var dynamicGame in DynamicGames.Values.Where(g => g.PlatformTvdbId == platform.TvdbId))
            {
                if (platformGames.All(g => g.TvdbId != dynamicGame.TvdbId && !string.Equals(g.Title, dynamicGame.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    platformGames.Add(dynamicGame);
                }
            }

            var selectedGameIds = GetSelectedGameIdsForPlatform(platform.TvdbId);
            var hasExplicitSelection = selectedGameIds.Count > 0;

            var episodeCounters = new Dictionary<int, int>();

            foreach (var game in platformGames.OrderBy(g => g.Title))
            {
                var seasonNum = game.RegionSeasonNumber;
                episodeCounters.TryGetValue(seasonNum, out var currentEpNum);
                currentEpNum++;
                episodeCounters[seasonNum] = currentEpNum;
                game.EpisodeNumber = currentEpNum;

                var isMonitored = hasExplicitSelection
                    ? selectedGameIds.Contains(game.TvdbId)
                    : seasonNum != RegionUnofficialSeason;

                episodes.Add(CreateEpisodeForGame(game, 0, isMonitored));
            }

            return new Tuple<Series, List<Episode>>(series, episodes);
        }

        private List<GameCatalogEntry> SearchGamesCatalog(string rawQuery, string normalizedQuery, string[] queryTokens)
        {
            var candidates = new List<Tuple<GameCatalogEntry, int>>();

            foreach (var game in BuiltInGames)
            {
                var score = ScoreGameMatch(game, rawQuery, normalizedQuery, queryTokens);
                if (score > 0)
                {
                    candidates.Add(Tuple.Create(game, score));
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
                .Take(40)
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

            var combinedSearchable = $"{normTitle} {normNoIntro} {normPlatform} {normShortPlatform} {normSlug}";
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

            var series = new Series
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
                OriginalLanguage = Language.English,
                Genres = new List<string> { "Platform", platform.Manufacturer },
                Ratings = new Ratings { Votes = 500, Value = 9.5m },
                Seasons = seasons,
                Images = new List<MediaCover.MediaCover>()
            };

            return series;
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
                OriginalLanguage = Language.English,
                Genres = new List<string> { game.PlatformShortName, game.RegionName, game.VerificationSource },
                Ratings = new Ratings { Votes = 250, Value = 9.2m },
                Seasons = seasons,
                Images = images
            };
        }

        private static List<Season> BuildDefaultRegionSeasons()
        {
            return new List<Season>
            {
                new Season { SeasonNumber = RegionUnofficialSeason, Monitored = false },
                new Season { SeasonNumber = RegionUsaSeason, Monitored = true },
                new Season { SeasonNumber = RegionEuropeSeason, Monitored = true },
                new Season { SeasonNumber = RegionJapanSeason, Monitored = true },
                new Season { SeasonNumber = RegionWorldSeason, Monitored = true }
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

            var parsed = RomParser.ParseRomName(rawName);
            var cleanGameTitle = !string.IsNullOrWhiteSpace(parsed?.GameTitle) ? parsed.GameTitle : rawName;
            var platform = InferPlatformFromText($"{datEntry.SmdbPath} {datEntry.RomName}") ?? BuiltInPlatforms.First(p => p.Slug == "snes");
            var regionSeason = MapRegionToSeasonNumber(datEntry.Region ?? parsed?.Regions?.FirstOrDefault());
            var regionName = GetRegionDisplayName(regionSeason);
            var tvdbId = ComputeDeterministicGameId(platform.Slug, rawName);

            return new GameCatalogEntry
            {
                TvdbId = tvdbId,
                Title = cleanGameTitle,
                NoIntroName = rawName,
                PlatformTvdbId = platform.TvdbId,
                PlatformSlug = platform.Slug,
                PlatformName = platform.Name,
                PlatformShortName = platform.ShortName,
                Manufacturer = platform.Manufacturer,
                RegionSeasonNumber = regionSeason,
                RegionName = regionName,
                EpisodeNumber = 1,
                ReleaseYear = platform.ReleaseYear,
                FirstAired = new DateTime(platform.ReleaseYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Overview = $"Verified DAT entry ({rawName}) for {platform.Name}.",
                PosterUrl = BuildLibretroBoxartUrl(platform.LibretroSystemName, rawName),
                VerificationSource = "No-Intro / Redump DAT"
            };
        }

        private GameCatalogEntry ConvertGameTitleToCatalogEntry(GameTitle dbTitle)
        {
            var platform = BuiltInPlatforms.FirstOrDefault(p => p.Slug == "snes") ?? BuiltInPlatforms[0];
            var tvdbId = ComputeDeterministicGameId(platform.Slug, dbTitle.Title);

            return new GameCatalogEntry
            {
                TvdbId = tvdbId,
                Title = dbTitle.Title,
                NoIntroName = $"{dbTitle.Title} (USA)",
                PlatformTvdbId = platform.TvdbId,
                PlatformSlug = platform.Slug,
                PlatformName = platform.Name,
                PlatformShortName = platform.ShortName,
                Manufacturer = platform.Manufacturer,
                RegionSeasonNumber = RegionUsaSeason,
                RegionName = GetRegionDisplayName(RegionUsaSeason),
                EpisodeNumber = 1,
                ReleaseYear = platform.ReleaseYear,
                FirstAired = new DateTime(platform.ReleaseYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Overview = !string.IsNullOrWhiteSpace(dbTitle.Summary) ? dbTitle.Summary : $"Catalog entry for {dbTitle.Title}.",
                PosterUrl = dbTitle.CoverArtworkUrl,
                VerificationSource = "Local Catalog"
            };
        }

        private GameCatalogEntry ConvertIgdbToCatalogEntry(Model.GameMetadata igdb)
        {
            var platform = BuiltInPlatforms.First(p => p.Slug == "snes");
            var year = igdb.ReleaseDate?.Year ?? platform.ReleaseYear;
            var tvdbId = ComputeDeterministicGameId(platform.Slug, igdb.Title);
            var poster = igdb.CoverUrl;

            return new GameCatalogEntry
            {
                TvdbId = tvdbId,
                Title = igdb.Title,
                NoIntroName = $"{igdb.Title} (USA)",
                PlatformTvdbId = platform.TvdbId,
                PlatformSlug = platform.Slug,
                PlatformName = platform.Name,
                PlatformShortName = platform.ShortName,
                Manufacturer = platform.Manufacturer,
                RegionSeasonNumber = RegionUsaSeason,
                RegionName = GetRegionDisplayName(RegionUsaSeason),
                EpisodeNumber = 1,
                ReleaseYear = year,
                FirstAired = igdb.ReleaseDate ?? new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Overview = !string.IsNullOrWhiteSpace(igdb.Summary) ? igdb.Summary : $"IGDB verified game entry for {igdb.Title} on {platform.Name}.",
                PosterUrl = poster,
                VerificationSource = "IGDB"
            };
        }

        private List<GameCatalogEntry> CreateDynamicFallbackGames(string rawQuery)
        {
            var parsed = RomParser.ParseRomName(rawQuery);
            var cleanTitle = !string.IsNullOrWhiteSpace(parsed?.GameTitle) ? parsed.GameTitle : rawQuery.Trim();
            var hintedPlatform = InferPlatformFromText(rawQuery);

            var targetPlatforms = hintedPlatform != null
                ? new List<PlatformCatalogEntry> { hintedPlatform }
                : new List<PlatformCatalogEntry>
                {
                    BuiltInPlatforms.First(p => p.Slug == "snes"),
                    BuiltInPlatforms.First(p => p.Slug == "gba"),
                    BuiltInPlatforms.First(p => p.Slug == "ps1"),
                    BuiltInPlatforms.First(p => p.Slug == "n64"),
                    BuiltInPlatforms.First(p => p.Slug == "nds")
                };

            var regionSeason = MapRegionToSeasonNumber(parsed?.Regions?.FirstOrDefault());
            var list = new List<GameCatalogEntry>();

            foreach (var platform in targetPlatforms)
            {
                var tvdbId = ComputeDeterministicGameId(platform.Slug, cleanTitle);
                var noIntroName = $"{cleanTitle} (USA)";
                var entry = new GameCatalogEntry
                {
                    TvdbId = tvdbId,
                    Title = cleanTitle,
                    NoIntroName = noIntroName,
                    PlatformTvdbId = platform.TvdbId,
                    PlatformSlug = platform.Slug,
                    PlatformName = platform.Name,
                    PlatformShortName = platform.ShortName,
                    Manufacturer = platform.Manufacturer,
                    RegionSeasonNumber = regionSeason,
                    RegionName = GetRegionDisplayName(regionSeason),
                    EpisodeNumber = 1,
                    ReleaseYear = platform.ReleaseYear,
                    FirstAired = new DateTime(platform.ReleaseYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Overview = $"Custom No-Intro / Redump target for '{cleanTitle}' on {platform.Name}.",
                    PosterUrl = BuildLibretroBoxartUrl(platform.LibretroSystemName, noIntroName),
                    VerificationSource = "No-Intro / Redump"
                };

                DynamicGames[entry.TvdbId] = entry;
                list.Add(entry);
            }

            return list;
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

        private static int MapRegionToSeasonNumber(string region)
        {
            if (string.IsNullOrWhiteSpace(region))
            {
                return RegionUsaSeason;
            }

            if (region.Contains("USA", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("US", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("U", StringComparison.OrdinalIgnoreCase))
            {
                return RegionUsaSeason;
            }

            if (region.Contains("Europe", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("EU", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("E", StringComparison.OrdinalIgnoreCase))
            {
                return RegionEuropeSeason;
            }

            if (region.Contains("Japan", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("JP", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("J", StringComparison.OrdinalIgnoreCase))
            {
                return RegionJapanSeason;
            }

            if (region.Contains("World", StringComparison.OrdinalIgnoreCase) ||
                region.Equals("W", StringComparison.OrdinalIgnoreCase))
            {
                return RegionWorldSeason;
            }

            return RegionUsaSeason;
        }

        public static string GetRegionDisplayName(int seasonNumber)
        {
            return seasonNumber switch
            {
                RegionUsaSeason => "USA (NTSC-U)",
                RegionEuropeSeason => "Europe (PAL)",
                RegionJapanSeason => "Japan (NTSC-J)",
                RegionWorldSeason => "World / Multi-Region",
                RegionUnofficialSeason => "Unofficial / Homebrew",
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
                CreatePlatform(1001, "Nintendo Entertainment System", "NES", "nes", "Nintendo", 1985, "Nintendo - Nintendo Entertainment System", new[] { "NES", "Famicom", "FC", "Nintendo NES" }, new[] { ".nes", ".unf", ".zip", ".7z" }),
                CreatePlatform(1002, "Famicom Disk System", "FDS", "fds", "Nintendo", 1986, "Nintendo - Family Computer Disk System", new[] { "FDS", "Disk System" }, new[] { ".fds", ".zip", ".7z" }),
                CreatePlatform(1003, "Super Nintendo Entertainment System", "SNES", "snes", "Nintendo", 1990, "Nintendo - Super Nintendo Entertainment System", new[] { "SNES", "Super NES", "Super Nintendo", "Super Famicom", "SFC" }, new[] { ".sfc", ".smc", ".zip", ".7z" }),
                CreatePlatform(1004, "Satellaview", "BS-X", "satellaview", "Nintendo", 1995, "Nintendo - Satellaview", new[] { "BSX", "BS-X" }, new[] { ".bs", ".sfc", ".zip" }),
                CreatePlatform(1005, "Nintendo 64", "N64", "n64", "Nintendo", 1996, "Nintendo - Nintendo 64", new[] { "N64", "Ultra 64" }, new[] { ".z64", ".n64", ".v64", ".zip", ".7z" }),
                CreatePlatform(1006, "64DD", "64DD", "64dd", "Nintendo", 1999, "Nintendo - Nintendo 64DD", new[] { "N64DD" }, new[] { ".ndd", ".zip" }),
                CreatePlatform(1007, "GameCube", "GCN", "gamecube", "Nintendo", 2001, "Nintendo - GameCube", new[] { "GCN", "NGC", "Nintendo GameCube" }, new[] { ".rvz", ".iso", ".gcm", ".ciso" }),
                CreatePlatform(1008, "Wii", "Wii", "wii", "Nintendo", 2006, "Nintendo - Wii", new[] { "Nintendo Wii", "RVL" }, new[] { ".rvz", ".wbfs", ".iso" }),
                CreatePlatform(1009, "Game Boy", "GB", "gb", "Nintendo", 1989, "Nintendo - Game Boy", new[] { "GB", "GameBoy", "DMG" }, new[] { ".gb", ".zip", ".7z" }),
                CreatePlatform(1010, "Game Boy Color", "GBC", "gbc", "Nintendo", 1998, "Nintendo - Game Boy Color", new[] { "GBC", "GameBoy Color", "CGB" }, new[] { ".gbc", ".zip", ".7z" }),
                CreatePlatform(1011, "Game Boy Advance", "GBA", "gba", "Nintendo", 2001, "Nintendo - Game Boy Advance", new[] { "GBA", "GameBoy Advance", "AGB" }, new[] { ".gba", ".zip", ".7z" }),
                CreatePlatform(1012, "Nintendo DS", "NDS", "nds", "Nintendo", 2004, "Nintendo - Nintendo DS", new[] { "NDS", "DS", "Nintendo DSi" }, new[] { ".nds", ".zip", ".7z" }),
                CreatePlatform(1013, "Nintendo 3DS", "3DS", "3ds", "Nintendo", 2011, "Nintendo - Nintendo 3DS", new[] { "3DS", "N3DS", "New 3DS" }, new[] { ".3ds", ".cia", ".cci", ".zip" }),
                CreatePlatform(1014, "Nintendo Switch", "Switch", "switch", "Nintendo", 2017, "Nintendo - Switch", new[] { "Switch", "NSW", "NX" }, new[] { ".nsp", ".xci", ".nsz" }),
                CreatePlatform(1015, "Sega Genesis", "Genesis", "genesis", "Sega", 1988, "Sega - Mega Drive - Genesis", new[] { "Genesis", "Mega Drive", "MD", "Sega Mega Drive" }, new[] { ".md", ".gen", ".bin", ".zip", ".7z" }),
                CreatePlatform(1016, "Sega CD", "Sega CD", "segacd", "Sega", 1991, "Sega - Mega-CD - Sega CD", new[] { "Sega CD", "Mega CD", "Mega-CD", "SCD" }, new[] { ".chd", ".cue", ".bin", ".iso" }),
                CreatePlatform(1017, "Sega 32X", "32X", "32x", "Sega", 1994, "Sega - 32X", new[] { "32X", "Super 32X" }, new[] { ".32x", ".zip", ".7z" }),
                CreatePlatform(1018, "Sega Saturn", "Saturn", "saturn", "Sega", 1994, "Sega - Saturn", new[] { "Saturn", "SS" }, new[] { ".chd", ".cue", ".bin", ".iso" }),
                CreatePlatform(1019, "Sega Dreamcast", "Dreamcast", "dreamcast", "Sega", 1998, "Sega - Dreamcast", new[] { "Dreamcast", "DC" }, new[] { ".chd", ".gdi", ".cdi", ".cue" }),
                CreatePlatform(1020, "TurboGrafx-16", "TG16", "tg16", "NEC", 1987, "NEC - PC Engine - TurboGrafx 16", new[] { "TG16", "PC Engine", "PCE", "TurboGrafx" }, new[] { ".pce", ".zip", ".7z" }),
                CreatePlatform(1021, "TurboGrafx-CD", "TG-CD", "tgcd", "NEC", 1988, "NEC - PC Engine CD - TurboGrafx-CD", new[] { "TGCD", "PC Engine CD", "PCECD" }, new[] { ".chd", ".cue", ".bin" }),
                CreatePlatform(1022, "PlayStation", "PS1", "ps1", "Sony", 1994, "Sony - PlayStation", new[] { "PS1", "PSX", "PSOne", "Sony PlayStation" }, new[] { ".chd", ".pbp", ".cue", ".bin", ".iso" }),
                CreatePlatform(1023, "PlayStation 2", "PS2", "ps2", "Sony", 2000, "Sony - PlayStation 2", new[] { "PS2", "Sony PlayStation 2" }, new[] { ".chd", ".iso", ".cso", ".bin" }),
                CreatePlatform(1024, "PlayStation Portable", "PSP", "psp", "Sony", 2004, "Sony - PlayStation Portable", new[] { "PSP", "Sony PSP" }, new[] { ".iso", ".cso", ".pbp", ".chd" }),
                CreatePlatform(1025, "Jaguar CD", "Jaguar CD", "jaguarcd", "Atari", 1995, "Atari - Jaguar CD", new[] { "Atari Jaguar CD" }, new[] { ".chd", ".cue", ".bin" }),
                CreatePlatform(1026, "Wii U", "Wii U", "wiiu", "Nintendo", 2012, "Nintendo - Wii U", new[] { "WiiU", "Nintendo Wii U" }, new[] { ".wua", ".wux", ".wud", ".rpx" }),
                CreatePlatform(1027, "Virtual Boy", "VB", "vb", "Nintendo", 1995, "Nintendo - Virtual Boy", new[] { "VB" }, new[] { ".vb", ".zip" }),
                CreatePlatform(1028, "Sega Master System", "SMS", "sms", "Sega", 1985, "Sega - Master System - Mark III", new[] { "SMS", "Master System", "Mark III" }, new[] { ".sms", ".zip", ".7z" }),
                CreatePlatform(1029, "Sega Game Gear", "GG", "gg", "Sega", 1990, "Sega - Game Gear", new[] { "GG", "Game Gear" }, new[] { ".gg", ".zip", ".7z" }),
                CreatePlatform(1030, "PlayStation 3", "PS3", "ps3", "Sony", 2006, "Sony - PlayStation 3", new[] { "PS3", "Sony PlayStation 3" }, new[] { ".iso", ".pkg" }),
                CreatePlatform(1031, "PlayStation Vita", "PS Vita", "psvita", "Sony", 2011, "Sony - PlayStation Vita", new[] { "Vita", "PSV", "PS Vita" }, new[] { ".vpk", ".zip" }),
                CreatePlatform(1032, "Xbox", "Xbox", "xbox", "Microsoft", 2001, "Microsoft - Xbox", new[] { "Original Xbox", "OG Xbox" }, new[] { ".iso", ".xiso" }),
                CreatePlatform(1033, "Xbox 360", "Xbox 360", "xbox360", "Microsoft", 2005, "Microsoft - Xbox 360", new[] { "X360", "360" }, new[] { ".iso", ".xex", ".god" }),
                CreatePlatform(1034, "Atari 2600", "2600", "atari2600", "Atari", 1977, "Atari - 2600", new[] { "2600", "VCS" }, new[] { ".a26", ".bin", ".zip" }),
                CreatePlatform(1035, "Atari 7800", "7800", "atari7800", "Atari", 1986, "Atari - 7800", new[] { "7800" }, new[] { ".a78", ".bin", ".zip" }),
                CreatePlatform(1036, "Atari Lynx", "Lynx", "lynx", "Atari", 1989, "Atari - Lynx", new[] { "Lynx" }, new[] { ".lnx", ".zip" }),
                CreatePlatform(1037, "Atari Jaguar", "Jaguar", "jaguar", "Atari", 1993, "Atari - Jaguar", new[] { "Jaguar" }, new[] { ".j64", ".jag", ".zip" }),
                CreatePlatform(1038, "Neo Geo", "Neo Geo", "neogeo", "SNK", 1990, "SNK - Neo Geo", new[] { "AES", "MVS", "Neo-Geo", "NGCD" }, new[] { ".zip", ".7z", ".neo", ".chd" }),
                CreatePlatform(1039, "Neo Geo Pocket Color", "NGPC", "ngpc", "SNK", 1999, "SNK - Neo Geo Pocket Color", new[] { "NGPC", "NGP" }, new[] { ".ngc", ".ngp", ".zip" }),
                CreatePlatform(1040, "WonderSwan Color", "WSC", "wonderswan", "Bandai", 2000, "Bandai - WonderSwan Color", new[] { "WonderSwan", "WSC", "WS" }, new[] { ".wsc", ".ws", ".zip" }),
                CreatePlatform(1041, "Arcade", "Arcade", "arcade", "Arcade", 1980, "MAME", new[] { "MAME", "FBNeo", "FinalBurn Neo", "CPS1", "CPS2", "CPS3" }, new[] { ".zip", ".7z", ".chd" })
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
                Overview = $"{manufacturer} {name} ({year}) — Verified No-Intro / Redump / HTGDB hardware platform. Supported ROM & disc formats: {extList}. Organized by regional releases (USA, Europe, Japan, World, and Unofficial/Homebrew).",
                Aliases = aliases.ToList(),
                Extensions = extensions.ToList()
            };
        }

        private static List<GameCatalogEntry> BuildGames()
        {
            var games = new List<GameCatalogEntry>();
            var id = 100001;

            void Add(string slug, string title, string noIntroName, int year, int regionSeason, string summary, string source = "No-Intro")
            {
                var platform = BuiltInPlatforms.First(p => p.Slug == slug);
                games.Add(new GameCatalogEntry
                {
                    TvdbId = id++,
                    Title = title,
                    NoIntroName = noIntroName,
                    PlatformTvdbId = platform.TvdbId,
                    PlatformSlug = platform.Slug,
                    PlatformName = platform.Name,
                    PlatformShortName = platform.ShortName,
                    Manufacturer = platform.Manufacturer,
                    RegionSeasonNumber = regionSeason,
                    RegionName = GetRegionDisplayName(regionSeason),
                    EpisodeNumber = 1,
                    ReleaseYear = year,
                    FirstAired = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Overview = $"{summary} ({source}: {noIntroName})",
                    PosterUrl = BuildLibretroBoxartUrl(platform.LibretroSystemName, noIntroName),
                    VerificationSource = source
                });
            }

            // Pokémon Series (GB, GBC, GBA, N64, GCN, NDS, 3DS, Switch)
            Add("gb", "Pokemon Red Version", "Pokemon - Red Version (USA, Europe)", 1998, RegionUsaSeason, "Begin your journey in the Kanto region to catch all 151 original Pokemon and defeat the Elite Four.");
            Add("gb", "Pokemon Blue Version", "Pokemon - Blue Version (USA, Europe)", 1998, RegionUsaSeason, "Classic Kanto RPG adventure featuring version-exclusive Pokemon and link cable trading.");
            Add("gb", "Pokemon Yellow Version: Special Pikachu Edition", "Pokemon - Yellow Version - Special Pikachu Edition (USA, Europe)", 1999, RegionUsaSeason, "Travel across Kanto with Pikachu following right behind you and obtain Bulbasaur, Charmander, and Squirtle.");
            Add("gb", "Pocket Monsters Midori (Pokemon Green)", "Pocket Monsters - Midori (Japan)", 1996, RegionJapanSeason, "Original Japanese release of Pocket Monsters Green Version for Game Boy.");
            Add("gbc", "Pokemon Gold Version", "Pokemon - Gold Version (USA, Europe)", 2000, RegionUsaSeason, "Explore the Johto and Kanto regions, discover 100 new Pokemon, and encounter the legendary Ho-Oh.");
            Add("gbc", "Pokemon Silver Version", "Pokemon - Silver Version (USA, Europe)", 2000, RegionUsaSeason, "Journey through Johto with a real-time clock, day/night cycle, and the legendary Lugia.");
            Add("gbc", "Pokemon Crystal Version", "Pokemon - Crystal Version (USA, Europe)", 2001, RegionUsaSeason, "Enhanced Johto adventure featuring Suicune, animated Pokemon sprites, and the Battle Tower.");
            Add("gbc", "Pokemon Trading Card Game", "Pokemon Trading Card Game (USA, Australia)", 2000, RegionUsaSeason, "Collect cards, build decks, and defeat the eight Club Masters to inherit the Legendary Pokemon Cards.");
            Add("gbc", "Pokemon Pinball", "Pokemon Pinball (USA, Australia)", 1999, RegionUsaSeason, "Catch 'em all on Red and Blue pinball tables with built-in Rumble Pak support.");
            Add("gbc", "Pokemon Puzzle Challenge", "Pokemon Puzzle Challenge (USA, Australia)", 2000, RegionUsaSeason, "Fast-paced puzzle action set in the Johto region based on Panel de Pon.");
            Add("gba", "Pokemon Emerald Version", "Pokemon - Emerald Version (USA, Europe)", 2005, RegionUsaSeason, "Definitive Hoenn adventure featuring Rayquaza, both Team Magma and Team Aqua, and the Battle Frontier.");
            Add("gba", "Pokemon FireRed Version", "Pokemon - FireRed Version (USA, Europe)", 2004, RegionUsaSeason, "Full GBA remake of Pokemon Red featuring the Sevii Islands and wireless adapter support.");
            Add("gba", "Pokemon LeafGreen Version", "Pokemon - LeafGreen Version (USA, Europe)", 2004, RegionUsaSeason, "Full GBA remake of Pokemon Blue featuring the Sevii Islands and updated Generation III mechanics.");
            Add("gba", "Pokemon Ruby Version", "Pokemon - Ruby Version (USA, Europe)", 2003, RegionUsaSeason, "Explore the tropical Hoenn region, battle Team Magma, and awaken the continent Pokemon Groudon.");
            Add("gba", "Pokemon Sapphire Version", "Pokemon - Sapphire Version (USA, Europe)", 2003, RegionUsaSeason, "Explore the Hoenn region, battle Team Aqua, and encounter the sea basin Pokemon Kyogre.");
            Add("gba", "Pokemon Mystery Dungeon: Red Rescue Team", "Pokemon Mystery Dungeon - Red Rescue Team (USA, Australia)", 2006, RegionUsaSeason, "Wake up transformed into a Pokemon and form a rescue team to explore shifting mystery dungeons.");
            Add("gba", "Pokemon Pinball: Ruby & Sapphire", "Pokemon Pinball - Ruby & Sapphire (USA)", 2003, RegionUsaSeason, "Capture over 200 Hoenn Pokemon across Ruby and Sapphire pinball tables.");
            Add("n64", "Pokemon Stadium", "Pokemon Stadium (USA)", 2000, RegionUsaSeason, "Bring your Game Boy Pokemon into full 3D arena tournaments, Gym Leader Castle, and mini-games.");
            Add("n64", "Pokemon Stadium 2", "Pokemon Stadium 2 (USA)", 2001, RegionUsaSeason, "3D tournament battles supporting all 251 Generation I and II Pokemon plus Earl's Pokemon Academy.");
            Add("n64", "Pokemon Snap", "Pokemon Snap (USA)", 1999, RegionUsaSeason, "Ride the ZERO-ONE across Pokemon Island and photograph wild Pokemon in their natural habitats.");
            Add("n64", "Pokemon Puzzle League", "Pokemon Puzzle League (USA)", 2000, RegionUsaSeason, "Fast-paced 2D and 3D puzzle battles starring Ash Ketchum and the Indigo Plateau Masters.");
            Add("gamecube", "Pokemon Colosseum", "Pokemon Colosseum (USA)", 2004, RegionUsaSeason, "Explore the Orre region and snag Shadow Pokemon to purify their hearts in full 3D Double Battles.", "Redump");
            Add("gamecube", "Pokemon XD: Gale of Darkness", "Pokemon XD - Gale of Darkness (USA)", 2005, RegionUsaSeason, "Return to the Orre region to confront Cipher and rescue Shadow Lugia (XD001).", "Redump");
            Add("nds", "Pokemon Platinum Version", "Pokemon - Platinum Version (USA)", 2009, RegionUsaSeason, "Definitive Sinnoh adventure featuring Giratina, the Distortion World, and the Battle Frontier.");
            Add("nds", "Pokemon HeartGold Version", "Pokemon - HeartGold Version (USA)", 2010, RegionUsaSeason, "DS remake of Pokemon Gold featuring Johto & Kanto, walking partner Pokemon, and Pokewalker support.");
            Add("nds", "Pokemon SoulSilver Version", "Pokemon - SoulSilver Version (USA)", 2010, RegionUsaSeason, "DS remake of Pokemon Silver featuring Lugia, walking partner Pokemon, and 16 Gym Badges.");
            Add("nds", "Pokemon Diamond Version", "Pokemon - Diamond Version (USA, Europe)", 2007, RegionUsaSeason, "Journey across the Sinnoh region, battle Team Galactic, and encounter the temporal Pokemon Dialga.");
            Add("nds", "Pokemon Pearl Version", "Pokemon - Pearl Version (USA, Europe)", 2007, RegionUsaSeason, "Journey across the Sinnoh region, battle Team Galactic, and encounter the spatial Pokemon Palkia.");
            Add("nds", "Pokemon Black Version", "Pokemon - Black Version (USA, Europe)", 2011, RegionUsaSeason, "Explore the Unova region with 156 brand-new Pokemon and confront Team Plasma and Reshiram.");
            Add("nds", "Pokemon White Version", "Pokemon - White Version (USA, Europe)", 2011, RegionUsaSeason, "Explore the Unova region, White Forest, and encounter the legendary Pokemon Zekrom.");
            Add("nds", "Pokemon Black Version 2", "Pokemon - Black Version 2 (USA, Europe)", 2012, RegionUsaSeason, "Direct sequel set two years later in Unova featuring Black Kyurem and the Pokemon World Tournament.");
            Add("nds", "Pokemon White Version 2", "Pokemon - White Version 2 (USA, Europe)", 2012, RegionUsaSeason, "Direct sequel set two years later in Unova featuring White Kyurem and the Pokemon World Tournament.");
            Add("nds", "Pokemon Mystery Dungeon: Explorers of Sky", "Pokemon Mystery Dungeon - Explorers of Sky (USA)", 2009, RegionUsaSeason, "Expanded edition of Explorers of Time & Darkness with Special Episodes and additional starter Pokemon.");
            Add("nds", "Pokemon Conquest", "Pokemon Conquest (USA)", 2012, RegionUsaSeason, "Tactical turn-based strategy crossover between Pokemon and Nobunaga's Ambition in the Ransei region.");
            Add("3ds", "Pokemon X", "Pokemon X (USA)", 2013, RegionUsaSeason, "First full 3D main-series adventure set in the Kalos region introducing Mega Evolution and Xerneas.");
            Add("3ds", "Pokemon Y", "Pokemon Y (USA)", 2013, RegionUsaSeason, "Explore the Kalos region, master Mega Evolution, and encounter the Destruction Pokemon Yveltal.");
            Add("3ds", "Pokemon Omega Ruby", "Pokemon Omega Ruby (USA)", 2014, RegionUsaSeason, "3DS reimagining of Pokemon Ruby featuring Primal Groudon, Mega Evolution, and Soaring in the sky.");
            Add("3ds", "Pokemon Alpha Sapphire", "Pokemon Alpha Sapphire (USA)", 2014, RegionUsaSeason, "3DS reimagining of Pokemon Sapphire featuring Primal Kyogre, Mega Evolution, and the Delta Episode.");
            Add("3ds", "Pokemon Ultra Sun", "Pokemon Ultra Sun (USA)", 2017, RegionUsaSeason, "Expanded Alola adventure featuring Dusk Mane Necrozma, Ultra Wormholes, and Team Rainbow Rocket.");
            Add("3ds", "Pokemon Ultra Moon", "Pokemon Ultra Moon (USA)", 2017, RegionUsaSeason, "Expanded Alola adventure featuring Dawn Wings Necrozma, Ultra Wormholes, and Team Rainbow Rocket.");
            Add("switch", "Pokemon Legends: Arceus", "Pokemon Legends - Arceus (World)", 2022, RegionWorldSeason, "Action-RPG set in the ancient Hisui region where you survey wild Pokemon to create the first Pokedex.");
            Add("switch", "Pokemon Sword", "Pokemon Sword (World)", 2019, RegionWorldSeason, "Explore the Galar region, challenge Dynamax Gym Leaders, and encounter the legendary Zacian.");
            Add("switch", "Pokemon Shield", "Pokemon Shield (World)", 2019, RegionWorldSeason, "Explore the Galar region, Wild Area, and encounter the legendary Zamazenta.");
            Add("switch", "Pokemon Scarlet", "Pokemon Scarlet (World)", 2022, RegionWorldSeason, "Open-world adventure across the Paldea region with Terastal phenomenon and Koraidon.");
            Add("switch", "Pokemon Violet", "Pokemon Violet (World)", 2022, RegionWorldSeason, "Open-world adventure across the Paldea region with Terastal phenomenon and Miraidon.");

            // NES
            Add("nes", "Super Mario Bros.", "Super Mario Bros. (World)", 1985, RegionWorldSeason, "Side-scrolling platformer masterpiece where Mario and Luigi rescue Princess Toadstool from Bowser.");
            Add("nes", "Super Mario Bros. 3", "Super Mario Bros. 3 (USA)", 1990, RegionUsaSeason, "Travel across eight kingdoms using iconic power-ups like the Super Leaf and Tanooki Suit.");
            Add("nes", "The Legend of Zelda", "Legend of Zelda, The (USA)", 1987, RegionUsaSeason, "Explore Hyrule, conquer nine dungeons, and collect the Triforce of Wisdom to defeat Ganon.");
            Add("nes", "Zelda II: The Adventure of Link", "Zelda II - The Adventure of Link (USA)", 1988, RegionUsaSeason, "Action-RPG sequel featuring side-scrolling combat, magic spells, and the Triforce of Courage.");
            Add("nes", "Metroid", "Metroid (USA)", 1987, RegionUsaSeason, "Non-linear sci-fi exploration on Planet Zebes as bounty hunter Samus Aran battles Mother Brain.");
            Add("nes", "Mega Man 2", "Mega Man 2 (USA)", 1989, RegionUsaSeason, "Battle eight Robot Masters including Metal Man and Quick Man before storming Dr. Wily's fortress.");
            Add("nes", "Castlevania", "Castlevania (USA)", 1987, RegionUsaSeason, "Wield the Vampire Killer whip as Simon Belmont to defeat Count Dracula.");
            Add("nes", "Castlevania III: Dracula's Curse", "Castlevania III - Dracula's Curse (USA)", 1990, RegionUsaSeason, "Branching-path gothic platformer starring Trevor Belmont, Sypha, Grant, and Alucard.");
            Add("nes", "Contra", "Contra (USA)", 1988, RegionUsaSeason, "Run-and-gun co-op action classic featuring the Spread Gun and the Konami Code.");
            Add("nes", "Punch-Out!!", "Mike Tyson's Punch-Out!! (Japan, USA)", 1987, RegionUsaSeason, "Guide Little Mac through the WVBA boxing circuits with timing and pattern mastery.");
            Add("nes", "Kirby's Adventure", "Kirby's Adventure (USA)", 1993, RegionUsaSeason, "Late-era NES platformer introducing Kirby's signature Copy Ability across Dream Land.");
            Add("nes", "Final Fantasy", "Final Fantasy (USA)", 1990, RegionUsaSeason, "Lead the four Light Warriors to restore the elemental Orbs in the RPG that launched a franchise.");

            // SNES
            Add("snes", "Super Mario World", "Super Mario World (USA)", 1991, RegionUsaSeason, "Explore Dinosaur Land with Yoshi across 96 exits in Nintendo's flagship 16-bit platformer.");
            Add("snes", "The Legend of Zelda: A Link to the Past", "Legend of Zelda, The - A Link to the Past (USA)", 1992, RegionUsaSeason, "Journey between the Light World and Dark World to rescue the descendants of the Seven Sages.");
            Add("snes", "Super Metroid", "Super Metroid (Japan, USA)", 1994, RegionUsaSeason, "Atmospheric action-exploration masterpiece as Samus returns to Planet Zebes to rescue the last Metroid.");
            Add("snes", "Chrono Trigger", "Chrono Trigger (USA)", 1995, RegionUsaSeason, "Time-traveling RPG created by the Dream Team to stop Lavos across multiple eras and endings.");
            Add("snes", "Final Fantasy III (Final Fantasy VI)", "Final Fantasy III (USA)", 1994, RegionUsaSeason, "Epic steampunk RPG where an ensemble cast confronts the rogue general Kefka Palazzo.");
            Add("snes", "Donkey Kong Country", "Donkey Kong Country (USA)", 1994, RegionUsaSeason, "Pre-rendered 16-bit platforming with Donkey and Diddy Kong reclaiming their banana hoard from King K. Rool.");
            Add("snes", "Donkey Kong Country 2: Diddy's Kong Quest", "Donkey Kong Country 2 - Diddy's Kong Quest (USA)", 1995, RegionUsaSeason, "Diddy and Dixie Kong storm Crocodile Isle in one of the finest platformers ever made.");
            Add("snes", "EarthBound", "EarthBound (USA)", 1995, RegionUsaSeason, "Quirky modern-day RPG following Ness and friends on a cosmic journey against Giygas.");
            Add("snes", "Super Mario RPG: Legend of the Seven Stars", "Super Mario RPG - Legend of the Seven Stars (USA)", 1996, RegionUsaSeason, "Isometric turn-based RPG collaboration between Nintendo and Square.");
            Add("snes", "Super Mario World 2: Yoshi's Island", "Super Mario World 2 - Yoshi's Island (USA)", 1995, RegionUsaSeason, "Hand-drawn Super FX2 platformer where the Yoshi clan protects Baby Mario.");
            Add("snes", "Mega Man X", "Mega Man X (USA)", 1994, RegionUsaSeason, "Fast-paced 16-bit action introducing X, Zero, wall-jumping, and hidden armor upgrades.");
            Add("snes", "Super Mario Kart", "Super Mario Kart (USA)", 1992, RegionUsaSeason, "Mode 7 kart racing classic featuring Battle Mode and four Cup circuits.");
            Add("snes", "Super Castlevania IV", "Super Castlevania IV (USA)", 1991, RegionUsaSeason, "16-bit reimagining of Simon Belmont's quest with eight-directional whip control.");
            Add("snes", "Secret of Mana", "Secret of Mana (USA)", 1993, RegionUsaSeason, "Real-time action RPG with three-player cooperative Ring Command combat.");
            Add("snes", "Street Fighter II Turbo", "Street Fighter II Turbo (USA)", 1993, RegionUsaSeason, "High-speed 16-bit conversion of Capcom's arcade fighting phenomenon.");

            // Nintendo 64
            Add("n64", "Super Mario 64", "Super Mario 64 (USA)", 1996, RegionUsaSeason, "Landmark 3D platformer where Mario collects 120 Power Stars inside Princess Peach's castle.");
            Add("n64", "The Legend of Zelda: Ocarina of Time", "Legend of Zelda, The - Ocarina of Time (USA)", 1998, RegionUsaSeason, "Travel across seven years with the Master Sword and Ocarina of Time to stop Ganondorf.");
            Add("n64", "The Legend of Zelda: Majora's Mask", "Legend of Zelda, The - Majora's Mask (USA)", 2000, RegionUsaSeason, "Relive a three-day cycle in Termina using transformation masks to stop the falling moon.");
            Add("n64", "Super Smash Bros.", "Super Smash Bros. (USA)", 1999, RegionUsaSeason, "Four-player crossover platform fighter starring Nintendo's iconic characters.");
            Add("n64", "Mario Kart 64", "Mario Kart 64 (USA)", 1997, RegionUsaSeason, "3D kart racing classic featuring four-player split-screen Grand Prix and Block Fort battles.");
            Add("n64", "GoldenEye 007", "GoldenEye 007 (USA)", 1997, RegionUsaSeason, "Stealth and objective-based FPS with legendary four-player split-screen multiplayer.");
            Add("n64", "Perfect Dark", "Perfect Dark (USA)", 2000, RegionUsaSeason, "Sci-fi stealth shooter starring Joanna Dark with customizable Combat Simulator bots.");
            Add("n64", "Banjo-Kazooie", "Banjo-Kazooie (USA)", 1998, RegionUsaSeason, "Collectathon 3D platformer from Rare starring Banjo the bear and Kazooie the breegull.");
            Add("n64", "Star Fox 64", "Star Fox 64 (USA)", 1997, RegionUsaSeason, "Branching-route 3D rail shooter with Rumble Pak support across the Lylat System.");
            Add("n64", "Paper Mario", "Paper Mario (USA)", 2001, RegionUsaSeason, "Storybook RPG adventure with timed action commands and partner abilities.");

            // GameCube & Wii
            Add("gamecube", "Super Smash Bros. Melee", "Super Smash Bros. Melee (USA)", 2001, RegionUsaSeason, "High-speed technical platform fighter featuring 26 Nintendo fighters and Adventure Mode.", "Redump");
            Add("gamecube", "The Legend of Zelda: The Wind Waker", "Legend of Zelda, The - The Wind Waker (USA)", 2003, RegionUsaSeason, "Cel-shaded seafaring adventure across the Great Sea with the Wind Waker baton.", "Redump");
            Add("gamecube", "Metroid Prime", "Metroid Prime (USA)", 2002, RegionUsaSeason, "First-person adventure on Tallon IV blending visor scanning, morph ball puzzles, and exploration.", "Redump");
            Add("gamecube", "Paper Mario: The Thousand-Year Door", "Paper Mario - The Thousand-Year Door (USA)", 2004, RegionUsaSeason, "Search for the seven Crystal Stars around Rogueport in a beloved turn-based RPG.", "Redump");
            Add("gamecube", "Super Mario Sunshine", "Super Mario Sunshine (USA)", 2002, RegionUsaSeason, "Clean up Isle Delfino using the water-powered F.L.U.D.D. pack to collect Shine Sprites.", "Redump");
            Add("gamecube", "Luigi's Mansion", "Luigi's Mansion (USA)", 2001, RegionUsaSeason, "Capture ghosts with the Poltergust 3000 to rescue Mario from King Boo.", "Redump");
            Add("gamecube", "Mario Kart: Double Dash!!", "Mario Kart - Double Dash!! (USA)", 2003, RegionUsaSeason, "Two-character co-op kart racing with character-specific Special Items.", "Redump");
            Add("gamecube", "Fire Emblem: Path of Radiance", "Fire Emblem - Path of Radiance (USA)", 2005, RegionUsaSeason, "Tactical RPG following Ike and the Greil Mercenaries across the continent of Tellius.", "Redump");
            Add("wii", "Super Mario Galaxy", "Super Mario Galaxy (USA)", 2007, RegionUsaSeason, "Gravity-defying 3D platforming across cosmic galaxies with Rosalina and the Lumas.", "Redump");
            Add("wii", "Super Mario Galaxy 2", "Super Mario Galaxy 2 (USA)", 2010, RegionUsaSeason, "Expanded cosmic platforming sequel featuring Yoshi and Starship Mario.", "Redump");
            Add("wii", "The Legend of Zelda: Twilight Princess", "Legend of Zelda, The - Twilight Princess (USA)", 2006, RegionUsaSeason, "Dark fantasy adventure where Link transforms into a wolf to save Hyrule from the Twilight Realm.", "Redump");
            Add("wii", "Xenoblade Chronicles", "Xenoblade Chronicles (USA)", 2012, RegionUsaSeason, "Massive JRPG set on the frozen bodies of two giant titans, the Bionis and the Mechonis.", "Redump");

            // Game Boy / GBC / GBA / DS
            Add("gb", "The Legend of Zelda: Link's Awakening", "Legend of Zelda, The - Link's Awakening (USA, Europe)", 1993, RegionUsaSeason, "Stranded on Koholint Island, Link must collect the eight Instruments of the Sirens to awaken the Wind Fish.");
            Add("gb", "Tetris", "Tetris (World)", 1989, RegionWorldSeason, "The iconic pack-in puzzle phenomenon with Type-A Korobeiniki music and two-player link battles.");
            Add("gb", "Super Mario Land 2: 6 Golden Coins", "Super Mario Land 2 - 6 Golden Coins (USA, Europe)", 1992, RegionUsaSeason, "Reclaim Mario's castle from Wario across six themed zones.");
            Add("gbc", "The Legend of Zelda: Oracle of Ages", "Legend of Zelda, The - Oracle of Ages (USA, Australia)", 2001, RegionUsaSeason, "Time-bending handheld Zelda adventure in Labrynna that links with Oracle of Seasons.");
            Add("gbc", "The Legend of Zelda: Oracle of Seasons", "Legend of Zelda, The - Oracle of Seasons (USA, Australia)", 2001, RegionUsaSeason, "Change the four seasons with the Rod of Seasons in Holodrum and link with Oracle of Ages.");
            Add("gba", "Metroid Fusion", "Metroid Fusion (USA)", 2002, RegionUsaSeason, "Investigate the Biologic Space Labs station while hunted by the deadly SA-X parasite.");
            Add("gba", "Metroid: Zero Mission", "Metroid - Zero Mission (USA)", 2004, RegionUsaSeason, "Expanded GBA reimagining of Samus Aran's original mission on Planet Zebes.");
            Add("gba", "Castlevania: Aria of Sorrow", "Castlevania - Aria of Sorrow (USA)", 2003, RegionUsaSeason, "Explore Dracula's castle in 2035 as Soma Cruz using the Tactical Soul system.");
            Add("gba", "The Legend of Zelda: The Minish Cap", "Legend of Zelda, The - The Minish Cap (USA)", 2005, RegionUsaSeason, "Shrink to microscopic size with the talking cap Ezlo to restore the Picori Blade.");
            Add("gba", "Golden Sun", "Golden Sun (USA, Europe)", 2001, RegionUsaSeason, "Command Psynergy and elemental Djinn in Camelot's acclaimed handheld RPG.");
            Add("gba", "Advance Wars", "Advance Wars (USA)", 2001, RegionUsaSeason, "Turn-based tactical warfare from Intelligent Systems featuring CO Powers and map design.");
            Add("gba", "Fire Emblem: The Blazing Blade", "Fire Emblem (USA, Australia)", 2003, RegionUsaSeason, "Lead Lyn, Eliwood, and Hector in the first Fire Emblem released in North America.");
            Add("gba", "Mother 3", "Mother 3 (Japan)", 2006, RegionJapanSeason, "Emotional RPG masterpiece starring Lucas and Flint in the Nowhere Islands.");
            Add("nds", "Mario Kart DS", "Mario Kart DS (USA)", 2005, RegionUsaSeason, "Dual-screen kart racing introducing retro tracks and Mission Mode.");
            Add("nds", "New Super Mario Bros.", "New Super Mario Bros. (USA)", 2006, RegionUsaSeason, "2.5D side-scrolling Mario platformer with Mega Mushroom and multiplayer mini-games.");
            Add("nds", "Castlevania: Dawn of Sorrow", "Castlevania - Dawn of Sorrow (USA)", 2005, RegionUsaSeason, "Direct DS sequel to Aria of Sorrow featuring Soul synthesis and gothic exploration.");
            Add("nds", "The World Ends with You", "World Ends with You, The (USA)", 2008, RegionUsaSeason, "Dual-screen action RPG set in modern Shibuya during the seven-day Reapers' Game.");

            // Sega Genesis / Saturn / Dreamcast
            Add("genesis", "Sonic the Hedgehog 2", "Sonic the Hedgehog 2 (World)", 1992, RegionWorldSeason, "High-speed 16-bit platforming introducing Miles 'Tails' Prower and the Spin Dash.");
            Add("genesis", "Sonic the Hedgehog 3 & Knuckles", "Sonic & Knuckles + Sonic the Hedgehog 3 (World)", 1994, RegionWorldSeason, "Complete lock-on adventure across Angel Island with Sonic, Tails, and Knuckles.");
            Add("genesis", "Streets of Rage 2", "Streets of Rage 2 (USA)", 1992, RegionUsaSeason, "Legendary 16-bit beat-'em-up featuring an iconic Yuzo Koshiro soundtrack.");
            Add("genesis", "Chrono / Phantasy Star IV", "Phantasy Star IV (USA)", 1995, RegionUsaSeason, "Culmination of the Algol Star System sci-fi RPG saga with manga-style cutscenes.");
            Add("genesis", "Shining Force II", "Shining Force II (USA)", 1994, RegionUsaSeason, "Tactical fantasy RPG with free-roaming town exploration and class promotions.");
            Add("genesis", "Gunstar Heroes", "Gunstar Heroes (USA)", 1993, RegionUsaSeason, "Explosive run-and-gun action from Treasure with combinable weapon types.");
            Add("genesis", "Castlevania: Bloodlines", "Castlevania - Bloodlines (USA)", 1994, RegionUsaSeason, "Globe-trotting 16-bit Castlevania starring John Morris and Eric Lecarde.");
            Add("saturn", "Panzer Dragoon Saga", "Panzer Dragoon Saga (USA)", 1998, RegionUsaSeason, "Four-disc tactical dragon-riding RPG set in a post-apocalyptic world.", "Redump");
            Add("saturn", "Nights into Dreams...", "Nights into Dreams... (USA)", 1996, RegionUsaSeason, "Acrobatic 3D flight score-attack adventure from Sonic Team.", "Redump");
            Add("dreamcast", "Sonic Adventure", "Sonic Adventure (USA)", 1999, RegionUsaSeason, "Full 3D high-speed adventure across six interwoven character campaigns and Chao Gardens.", "Redump");
            Add("dreamcast", "Shenmue", "Shenmue (USA)", 2000, RegionUsaSeason, "Open-world cinematic adventure following Ryo Hazuki across 1986 Yokosuka.", "Redump");
            Add("dreamcast", "SoulCalibur", "SoulCalibur (USA)", 1999, RegionUsaSeason, "Eight-way run weapon fighter widely hailed as one of the greatest launch-era titles ever.", "Redump");
            Add("dreamcast", "Jet Grind Radio (Jet Set Radio)", "Jet Grind Radio (USA)", 2000, RegionUsaSeason, "Cel-shaded inline-skating graffiti action across Tokyo-to.", "Redump");
            Add("dreamcast", "Marvel vs. Capcom 2: New Age of Heroes", "Marvel vs. Capcom 2 (USA)", 2000, RegionUsaSeason, "Arcade-perfect 3v3 tag-team fighting game with a 56-character roster.", "Redump");

            // PlayStation 1 / PlayStation 2 / PSP
            Add("ps1", "Final Fantasy VII", "Final Fantasy VII (USA) (Disc 1)", 1997, RegionUsaSeason, "Join Cloud Strife and AVALANCHE against Shinra and Sephiroth in Square's landmark RPG.", "Redump");
            Add("ps1", "Castlevania: Symphony of the Night", "Castlevania - Symphony of the Night (USA)", 1997, RegionUsaSeason, "Explore Dracula's inverted castle as Alucard in the genre-defining Metroidvania masterpiece.", "Redump");
            Add("ps1", "Metal Gear Solid", "Metal Gear Solid (USA) (Disc 1)", 1998, RegionUsaSeason, "Tactical espionage action as Solid Snake infiltrates Shadow Moses Island.", "Redump");
            Add("ps1", "Resident Evil 2", "Resident Evil 2 (USA) (Disc 1)", 1998, RegionUsaSeason, "Survive the Raccoon City outbreak as Leon S. Kennedy and Claire Redfield.", "Redump");
            Add("ps1", "Silent Hill", "Silent Hill (USA)", 1999, RegionUsaSeason, "Psychological survival horror as Harry Mason searches the fog-shrouded town for his daughter.", "Redump");
            Add("ps1", "Chrono Cross", "Chrono Cross (USA) (Disc 1)", 2000, RegionUsaSeason, "Parallel-dimension RPG sequel to Chrono Trigger featuring over 40 recruitable characters.", "Redump");
            Add("ps1", "Crash Bandicoot: Warped", "Crash Bandicoot - Warped (USA)", 1998, RegionUsaSeason, "Time-traveling 3D platforming adventure from Naughty Dog.", "Redump");
            Add("ps1", "Spyro the Dragon", "Spyro the Dragon (USA)", 1998, RegionUsaSeason, "Glide and flame across the Dragon Realms in Insomniac's colorful 3D platformer.", "Redump");
            Add("ps1", "Gran Turismo 2", "Gran Turismo 2 (USA) (Simulation Mode)", 1999, RegionUsaSeason, "The Real Driving Simulator featuring over 600 licensed vehicles and endurance racing.", "Redump");
            Add("ps2", "Shadow of the Colossus", "Shadow of the Colossus (USA)", 2005, RegionUsaSeason, "Scale and defeat sixteen towering Colossi across a forbidden land to restore Mono.", "Redump");
            Add("ps2", "Metal Gear Solid 3: Snake Eater", "Metal Gear Solid 3 - Snake Eater (USA)", 2004, RegionUsaSeason, "Cold War jungle survival stealth starring Naked Snake and The Boss.", "Redump");
            Add("ps2", "Final Fantasy X", "Final Fantasy X (USA)", 2001, RegionUsaSeason, "Journey across Spira with Tidus and Yuna to defeat Sin.", "Redump");
            Add("ps2", "Kingdom Hearts II", "Kingdom Hearts II (USA)", 2006, RegionUsaSeason, "Action-RPG crossover uniting Sora, Donald, and Goofy against Organization XIII.", "Redump");
            Add("ps2", "God of War II", "God of War II (USA)", 2007, RegionUsaSeason, "Mythological hack-and-slash spectacle as Kratos defies the Olympian gods.", "Redump");
            Add("ps2", "Persona 4", "Shin Megami Tensei - Persona 4 (USA)", 2008, RegionUsaSeason, "Solve a supernatural murder mystery in Inaba while balancing high school life and dungeon crawling.", "Redump");
            Add("psp", "Crisis Core: Final Fantasy VII", "Crisis Core - Final Fantasy VII (USA)", 2008, RegionUsaSeason, "Action-RPG prequel to Final Fantasy VII starring SOLDIER operative Zack Fair.", "Redump");
            Add("psp", "Monster Hunter Freedom Unite", "Monster Hunter Freedom Unite (USA)", 2009, RegionUsaSeason, "Hunt colossal wyverns solo or in four-player ad-hoc co-op.", "Redump");

            // TurboGrafx-16 / Neo Geo / Xbox
            Add("tgcd", "Castlevania: Rondo of Blood", "Akumajou Dracula X - Chi no Rondo (Japan)", 1993, RegionJapanSeason, "Richter Belmont and Maria Renard battle Dracula in the acclaimed PC Engine Super CD-ROM2 classic.", "Redump");
            Add("neogeo", "Metal Slug 3", "Metal Slug 3 (World)", 2000, RegionWorldSeason, "Masterpiece 2D run-and-gun arcade action with branching paths and vehicles.");
            Add("xbox", "Halo: Combat Evolved", "Halo - Combat Evolved (USA)", 2001, RegionUsaSeason, "Sci-fi FPS landmark as Master Chief battles the Covenant and the Flood on Installation 04.", "Redump");
            Add("xbox", "Star Wars: Knights of the Old Republic", "Star Wars - Knights of the Old Republic (USA)", 2003, RegionUsaSeason, "BioWare's epic RPG set 4,000 years before the Galactic Empire.", "Redump");

            return games;
        }
    }
}
