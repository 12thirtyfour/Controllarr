using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Dat;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.MetadataSource.Game
{
    public class DatImportRequestModel
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
    }

    public class DatImportResultModel
    {
        public DatFile DatFile { get; set; }
        public int EntryCount { get; set; }
    }

    public class MetadataScraperConfigModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool RequiresCredentials { get; set; }
        public bool Enabled { get; set; }
        public int Priority { get; set; } = 1;
        public int MaxRequestsPerMinute { get; set; } = 60;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public bool PublishingMetadata { get; set; } = true;
        public bool BoxArt2d { get; set; } = true;
        public bool BoxArt3d { get; set; } = true;
        public bool Screenshots { get; set; } = true;
        public bool TitleScreens { get; set; } = true;
        public bool ClearLogos { get; set; } = true;
        public bool VideoPreviews { get; set; }
        public bool ManualsPdf { get; set; } = true;
    }

    public class UnifiedMetadataSettingsModel
    {
        public bool GenerateGamelistXml { get; set; } = true;
        public List<MetadataScraperConfigModel> Scrapers { get; set; } = new List<MetadataScraperConfigModel>();
    }

    public class MetadataSourceConfigModel
    {
        public bool EnableNoIntroLibretro { get; set; } = true;
        public bool EnableHtgdbSmdb { get; set; } = true;
        public bool EnableIgdb { get; set; }
        public string IgdbClientId { get; set; } = string.Empty;
        public string IgdbClientSecret { get; set; } = string.Empty;
        public bool EnableScreenScraper { get; set; }
        public string ScreenScraperUsername { get; set; } = string.Empty;
        public string ScreenScraperPassword { get; set; } = string.Empty;
        public bool EnableTheGamesDb { get; set; }
        public string TheGamesDbApiKey { get; set; } = string.Empty;
        public bool EnableLaunchBox { get; set; } = true;
    }

    public class GameMediaAssetSettingsModel
    {
        public bool BoxArt2d { get; set; } = true;
        public bool BoxArt3d { get; set; } = true;
        public bool TitleScreens { get; set; } = true;
        public bool Screenshots { get; set; } = true;
        public bool ClearLogos { get; set; } = true;
        public bool MediaScans { get; set; }
        public bool VideoPreviews { get; set; }
        public bool ManualsPdf { get; set; } = true;
        public bool PublishingMetadata { get; set; } = true;
        public bool GenerateGamelistXml { get; set; } = true;
    }

    public interface IGameMediaAssetService
    {
        UnifiedMetadataSettingsModel GetUnifiedSettings();
        UnifiedMetadataSettingsModel SaveUnifiedSettings(UnifiedMetadataSettingsModel model);
        MetadataSourceConfigModel GetMetadataSources();
        MetadataSourceConfigModel SaveMetadataSources(MetadataSourceConfigModel config);
        GameMediaAssetSettingsModel GetMediaAssetSettings();
        GameMediaAssetSettingsModel SaveMediaAssetSettings(GameMediaAssetSettingsModel settings);
        string GeneratePlatformGamelistXml(Series platform);
        void ScrapeMediaAssetsForImportedRom(Series platform, EpisodeFile romFile, Episode game);
        void SyncAllPlatforms();
        List<DatFile> GetDatFiles();
        DatImportResultModel ImportDatOrSmdb(DatImportRequestModel request);
    }

    public class GameMediaAssetService :
        IGameMediaAssetService,
        IHandle<SeriesRefreshCompleteEvent>,
        IHandle<EpisodeImportedEvent>
    {
        private static readonly ConcurrentDictionary<string, Queue<DateTime>> ScraperRequestWindows =
            new ConcurrentDictionary<string, Queue<DateTime>>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, DateTime> ScraperCooldownUntil =
            new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private readonly IConfigService _configService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IHttpClient _httpClient;
        private readonly IDatCacheService _datCacheService;
        private readonly IDatParserService _datParserService;
        private readonly ISmdbParserService _smdbParserService;
        private readonly IGameCatalogService _gameCatalogService;
        private readonly IProvideSeriesInfo _seriesInfo;
        private readonly Logger _logger;

        public GameMediaAssetService(
            IConfigService configService,
            ISeriesService seriesService = null,
            IEpisodeService episodeService = null,
            IMediaFileService mediaFileService = null,
            IDiskProvider diskProvider = null,
            IAppFolderInfo appFolderInfo = null,
            IHttpClient httpClient = null,
            IDatCacheService datCacheService = null,
            IDatParserService datParserService = null,
            ISmdbParserService smdbParserService = null,
            IGameCatalogService gameCatalogService = null,
            IProvideSeriesInfo seriesInfo = null,
            Logger logger = null)
        {
            _configService = configService;
            _seriesService = seriesService;
            _episodeService = episodeService;
            _mediaFileService = mediaFileService;
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
            _httpClient = httpClient;
            _datCacheService = datCacheService;
            _datParserService = datParserService;
            _smdbParserService = smdbParserService;
            _gameCatalogService = gameCatalogService;
            _seriesInfo = seriesInfo;
            _logger = logger;
        }

        public UnifiedMetadataSettingsModel GetUnifiedSettings()
        {
            var defaultScrapers = BuildDefaultScrapers();
            var settingsPath = GetSettingsFilePath();

            if (!string.IsNullOrWhiteSpace(settingsPath) && File.Exists(settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(settingsPath);
                    var loaded = JsonSerializer.Deserialize<UnifiedMetadataSettingsModel>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (loaded?.Scrapers != null && loaded.Scrapers.Count > 0)
                    {
                        foreach (var def in defaultScrapers)
                        {
                            var match = loaded.Scrapers.FirstOrDefault(s => string.Equals(s.Id, def.Id, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                def.Enabled = match.Enabled;
                                def.Priority = match.Priority > 0 ? match.Priority : def.Priority;
                                def.MaxRequestsPerMinute = match.MaxRequestsPerMinute > 0 ? match.MaxRequestsPerMinute : def.MaxRequestsPerMinute;
                                def.Username = match.Username ?? def.Username;
                                def.Password = match.Password ?? def.Password;
                                def.ClientId = match.ClientId ?? def.ClientId;
                                def.ClientSecret = match.ClientSecret ?? def.ClientSecret;
                                def.ApiKey = match.ApiKey ?? def.ApiKey;
                                def.PublishingMetadata = match.PublishingMetadata;
                                def.BoxArt2d = match.BoxArt2d;
                                def.BoxArt3d = match.BoxArt3d;
                                def.Screenshots = match.Screenshots;
                                def.TitleScreens = match.TitleScreens;
                                def.ClearLogos = match.ClearLogos;
                                def.VideoPreviews = match.VideoPreviews;
                                def.ManualsPdf = match.ManualsPdf;
                            }
                        }

                        return new UnifiedMetadataSettingsModel
                        {
                            GenerateGamelistXml = loaded.GenerateGamelistXml,
                            Scrapers = defaultScrapers.OrderBy(s => s.Priority).ToList()
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Debug(ex, "Unable to read metadata_scrapers.json; using defaults.");
                }
            }

            return new UnifiedMetadataSettingsModel
            {
                GenerateGamelistXml = _configService.MetadataGenerateGamelistXml,
                Scrapers = defaultScrapers.OrderBy(s => s.Priority).ToList()
            };
        }

        public UnifiedMetadataSettingsModel SaveUnifiedSettings(UnifiedMetadataSettingsModel model)
        {
            if (model == null)
            {
                return GetUnifiedSettings();
            }

            _configService.MetadataGenerateGamelistXml = model.GenerateGamelistXml;

            if (model.Scrapers != null)
            {
                var ss = model.Scrapers.FirstOrDefault(s => string.Equals(s.Id, "screenscraper", StringComparison.OrdinalIgnoreCase));
                if (ss != null)
                {
                    _configService.EnableScreenScraper = ss.Enabled;
                    _configService.ScreenScraperUsername = ss.Username?.Trim() ?? string.Empty;
                    _configService.ScreenScraperPassword = ss.Password?.Trim() ?? string.Empty;
                }

                var igdb = model.Scrapers.FirstOrDefault(s => string.Equals(s.Id, "igdb", StringComparison.OrdinalIgnoreCase));
                if (igdb != null)
                {
                    _configService.EnableIgdb = igdb.Enabled;
                    _configService.IgdbClientId = igdb.ClientId?.Trim() ?? string.Empty;
                    _configService.IgdbClientSecret = igdb.ClientSecret?.Trim() ?? string.Empty;
                }

                var tgdb = model.Scrapers.FirstOrDefault(s => string.Equals(s.Id, "thegamesdb", StringComparison.OrdinalIgnoreCase));
                if (tgdb != null)
                {
                    _configService.EnableTheGamesDb = tgdb.Enabled;
                    _configService.TheGamesDbApiKey = tgdb.ApiKey?.Trim() ?? string.Empty;
                }

                var libretro = model.Scrapers.FirstOrDefault(s => string.Equals(s.Id, "libretro_launchbox", StringComparison.OrdinalIgnoreCase));
                if (libretro != null)
                {
                    _configService.EnableLaunchBox = libretro.Enabled;
                }

                _configService.MetadataPublishingMetadata = model.Scrapers.Any(s => s.Enabled && s.PublishingMetadata);
                _configService.MetadataBoxArt2d = model.Scrapers.Any(s => s.Enabled && s.BoxArt2d);
                _configService.MetadataBoxArt3d = model.Scrapers.Any(s => s.Enabled && s.BoxArt3d);
                _configService.MetadataScreenshots = model.Scrapers.Any(s => s.Enabled && s.Screenshots);
                _configService.MetadataTitleScreens = model.Scrapers.Any(s => s.Enabled && s.TitleScreens);
                _configService.MetadataClearLogos = model.Scrapers.Any(s => s.Enabled && s.ClearLogos);
                _configService.MetadataVideoPreviews = model.Scrapers.Any(s => s.Enabled && s.VideoPreviews);
                _configService.MetadataManualsPdf = model.Scrapers.Any(s => s.Enabled && s.ManualsPdf);
            }

            var settingsPath = GetSettingsFilePath();
            if (!string.IsNullOrWhiteSpace(settingsPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(settingsPath);
                    if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(settingsPath, json);
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Unable to persist metadata_scrapers.json");
                }
            }

            SyncAllPlatforms();
            return GetUnifiedSettings();
        }

        public MetadataSourceConfigModel GetMetadataSources()
        {
            return new MetadataSourceConfigModel
            {
                EnableNoIntroLibretro = true,
                EnableHtgdbSmdb = true,
                EnableLaunchBox = _configService.EnableLaunchBox,
                EnableIgdb = _configService.EnableIgdb,
                IgdbClientId = _configService.IgdbClientId ?? string.Empty,
                IgdbClientSecret = _configService.IgdbClientSecret ?? string.Empty,
                EnableScreenScraper = _configService.EnableScreenScraper,
                ScreenScraperUsername = _configService.ScreenScraperUsername ?? string.Empty,
                ScreenScraperPassword = _configService.ScreenScraperPassword ?? string.Empty,
                EnableTheGamesDb = _configService.EnableTheGamesDb,
                TheGamesDbApiKey = _configService.TheGamesDbApiKey ?? string.Empty
            };
        }

        public MetadataSourceConfigModel SaveMetadataSources(MetadataSourceConfigModel config)
        {
            if (config == null)
            {
                return GetMetadataSources();
            }

            _configService.EnableLaunchBox = config.EnableLaunchBox;
            _configService.EnableIgdb = config.EnableIgdb;
            _configService.IgdbClientId = config.IgdbClientId?.Trim() ?? string.Empty;
            _configService.IgdbClientSecret = config.IgdbClientSecret?.Trim() ?? string.Empty;
            _configService.EnableScreenScraper = config.EnableScreenScraper;
            _configService.ScreenScraperUsername = config.ScreenScraperUsername?.Trim() ?? string.Empty;
            _configService.ScreenScraperPassword = config.ScreenScraperPassword?.Trim() ?? string.Empty;
            _configService.EnableTheGamesDb = config.EnableTheGamesDb;
            _configService.TheGamesDbApiKey = config.TheGamesDbApiKey?.Trim() ?? string.Empty;

            return GetMetadataSources();
        }

        public GameMediaAssetSettingsModel GetMediaAssetSettings()
        {
            return new GameMediaAssetSettingsModel
            {
                BoxArt2d = _configService.MetadataBoxArt2d,
                BoxArt3d = _configService.MetadataBoxArt3d,
                TitleScreens = _configService.MetadataTitleScreens,
                Screenshots = _configService.MetadataScreenshots,
                ClearLogos = _configService.MetadataClearLogos,
                MediaScans = _configService.MetadataMediaScans,
                VideoPreviews = _configService.MetadataVideoPreviews,
                ManualsPdf = _configService.MetadataManualsPdf,
                PublishingMetadata = _configService.MetadataPublishingMetadata,
                GenerateGamelistXml = _configService.MetadataGenerateGamelistXml
            };
        }

        public GameMediaAssetSettingsModel SaveMediaAssetSettings(GameMediaAssetSettingsModel settings)
        {
            if (settings == null)
            {
                return GetMediaAssetSettings();
            }

            _configService.MetadataBoxArt2d = settings.BoxArt2d;
            _configService.MetadataBoxArt3d = settings.BoxArt3d;
            _configService.MetadataTitleScreens = settings.TitleScreens;
            _configService.MetadataScreenshots = settings.Screenshots;
            _configService.MetadataClearLogos = settings.ClearLogos;
            _configService.MetadataMediaScans = settings.MediaScans;
            _configService.MetadataVideoPreviews = settings.VideoPreviews;
            _configService.MetadataManualsPdf = settings.ManualsPdf;
            _configService.MetadataPublishingMetadata = settings.PublishingMetadata;
            _configService.MetadataGenerateGamelistXml = settings.GenerateGamelistXml;

            SyncAllPlatforms();

            return GetMediaAssetSettings();
        }

        public List<DatFile> GetDatFiles()
        {
            return _datCacheService?.GetAllDatFiles() ?? new List<DatFile>();
        }

        public DatImportResultModel ImportDatOrSmdb(DatImportRequestModel request)
        {
            if (request == null || _datCacheService == null)
            {
                return new DatImportResultModel { EntryCount = 0 };
            }

            var content = request.Content;
            var effectiveName = !string.IsNullOrWhiteSpace(request.FileName)
                ? request.FileName.Trim()
                : (!string.IsNullOrWhiteSpace(request.FilePath) ? Path.GetFileName(request.FilePath.Trim()) : "Imported Catalog");

            if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(request.FilePath))
            {
                var path = request.FilePath.Trim();
                if (_diskProvider != null && _diskProvider.FileExists(path))
                {
                    content = _diskProvider.ReadAllText(path);
                }
                else if (File.Exists(path))
                {
                    content = File.ReadAllText(path);
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new DatImportResultModel { EntryCount = 0 };
            }

            var isSmdb = string.Equals(request.Format, "smdb", StringComparison.OrdinalIgnoreCase) ||
                         effectiveName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                         effectiveName.EndsWith(".smdb", StringComparison.OrdinalIgnoreCase) ||
                         (!content.TrimStart().StartsWith("<", StringComparison.Ordinal) &&
                          !content.Contains("clrmamepro", StringComparison.OrdinalIgnoreCase) &&
                          content.Contains('\t'));

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                var (datFile, entries) = isSmdb && _smdbParserService != null
                    ? _smdbParserService.Parse(stream, Path.GetFileNameWithoutExtension(effectiveName))
                    : (_datParserService != null ? _datParserService.Parse(stream) : (new DatFile { CreatedAt = DateTime.UtcNow }, new List<DatEntry>()));

                if (string.IsNullOrWhiteSpace(datFile.Name))
                {
                    datFile.Name = Path.GetFileNameWithoutExtension(effectiveName);
                }

                var matchedPlatform = _gameCatalogService?.InferPlatformForText($"{datFile.Name} {datFile.Description} {effectiveName}");
                if (matchedPlatform != null)
                {
                    datFile.Description = matchedPlatform.Slug;
                }

                var inserted = _datCacheService.Insert(datFile, entries);
                _gameCatalogService?.InvalidatePlatformCache(matchedPlatform?.TvdbId);

                if (matchedPlatform != null && _seriesService != null && _episodeService != null && _seriesInfo != null)
                {
                    try
                    {
                        var existingSeries = _seriesService.FindByTvdbId(matchedPlatform.TvdbId) ??
                                             _seriesService.FindByTitle(matchedPlatform.Name);
                        if (existingSeries != null)
                        {
                            var tuple = _seriesInfo.GetSeriesInfo(matchedPlatform.TvdbId, Language.English, null);
                            if (tuple?.Item2 != null)
                            {
                                var currentEpisodes = _episodeService.GetEpisodeBySeries(existingSeries.Id) ?? new List<Episode>();
                                var currentTvdbIds = new HashSet<int>(currentEpisodes.Select(e => e.TvdbId));
                                var newEpisodes = tuple.Item2
                                    .Where(e => !currentTvdbIds.Contains(e.TvdbId))
                                    .Select(e =>
                                    {
                                        e.SeriesId = existingSeries.Id;
                                        return e;
                                    })
                                    .ToList();

                                if (newEpisodes.Count > 0)
                                {
                                    _episodeService.InsertMany(newEpisodes);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Debug(ex, "Unable to sync imported DAT entries into platform Games table for {0}", matchedPlatform.Name);
                    }
                }

                return new DatImportResultModel
                {
                    DatFile = inserted,
                    EntryCount = entries.Count
                };
            }
        }

        public void ScrapeMediaAssetsForImportedRom(Series platform, EpisodeFile romFile, Episode game)
        {
            if (platform == null || string.IsNullOrWhiteSpace(platform.Path) || romFile == null || string.IsNullOrWhiteSpace(romFile.RelativePath))
            {
                return;
            }

            var cleanFileStem = Path.GetFileNameWithoutExtension(romFile.RelativePath);
            var gameTitle = game?.Title ?? cleanFileStem;
            var settings = GetUnifiedSettings();
            var enabledScrapers = (settings.Scrapers ?? new List<MetadataScraperConfigModel>())
                .Where(s => s.Enabled)
                .OrderBy(s => s.Priority)
                .ToList();

            if (enabledScrapers.Count == 0 || _httpClient == null)
            {
                return;
            }

            PlatformCatalogEntry platformEntry = null;
            _gameCatalogService?.TryGetPlatformEntry(platform.TvdbId, out platformEntry);
            if (platformEntry == null && !string.IsNullOrWhiteSpace(platform.TitleSlug))
            {
                _gameCatalogService?.TryGetPlatformEntryBySlug(platform.TitleSlug, out platformEntry);
            }

            var mediaRoot = Path.Combine(platform.Path, "media");

            foreach (var scraper in enabledScrapers)
            {
                var needBox2d = scraper.BoxArt2d && !AssetExists(mediaRoot, "box2d", cleanFileStem, ".png");
                var needBox3d = scraper.BoxArt3d && !AssetExists(mediaRoot, "box3d", cleanFileStem, ".png");
                var needScreenshots = scraper.Screenshots && !AssetExists(mediaRoot, "screenshots", cleanFileStem, ".png");
                var needTitleScreens = scraper.TitleScreens && !AssetExists(mediaRoot, "titlescreens", cleanFileStem, ".png");
                var needMarquee = scraper.ClearLogos && !AssetExists(mediaRoot, "marquee", cleanFileStem, ".png");
                var needVideo = scraper.VideoPreviews && !AssetExists(mediaRoot, "videos", cleanFileStem, ".mp4");
                var needManual = scraper.ManualsPdf && !AssetExists(mediaRoot, "manuals", cleanFileStem, ".pdf");

                if (!needBox2d && !needBox3d && !needScreenshots && !needTitleScreens && !needMarquee && !needVideo && !needManual)
                {
                    break;
                }

                if (!TryAcquireRateLimitSlot(scraper.Id, scraper.MaxRequestsPerMinute))
                {
                    _logger?.Debug("Rate-limit fail-safe active for {0}; cascading to next priority scraper.", scraper.Name);
                    continue;
                }

                try
                {
                    var assetUrls = ResolveScraperAssetUrls(scraper, platformEntry, gameTitle, cleanFileStem);

                    if (needBox2d && assetUrls.TryGetValue("box2d", out var box2dUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, box2dUrl, Path.Combine(mediaRoot, "box2d", $"{cleanFileStem}.png"));
                    }

                    if (needBox3d && assetUrls.TryGetValue("box3d", out var box3dUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, box3dUrl, Path.Combine(mediaRoot, "box3d", $"{cleanFileStem}.png"));
                    }

                    if (needScreenshots && assetUrls.TryGetValue("screenshots", out var ssUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, ssUrl, Path.Combine(mediaRoot, "screenshots", $"{cleanFileStem}.png"));
                    }

                    if (needTitleScreens && assetUrls.TryGetValue("titlescreens", out var titleUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, titleUrl, Path.Combine(mediaRoot, "titlescreens", $"{cleanFileStem}.png"));
                    }

                    if (needMarquee && assetUrls.TryGetValue("marquee", out var marqueeUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, marqueeUrl, Path.Combine(mediaRoot, "marquee", $"{cleanFileStem}.png"));
                    }

                    if (needVideo && assetUrls.TryGetValue("videos", out var videoUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, videoUrl, Path.Combine(mediaRoot, "videos", $"{cleanFileStem}.mp4"));
                    }

                    if (needManual && assetUrls.TryGetValue("manuals", out var manualUrl))
                    {
                        DownloadAssetToDisk(scraper.Id, manualUrl, Path.Combine(mediaRoot, "manuals", $"{cleanFileStem}.pdf"));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Debug(ex, "Scraper {0} failed while gathering assets for {1}; moving to next priority scraper.", scraper.Name, gameTitle);
                    RegisterScraperCooldown(scraper.Id, TimeSpan.FromMinutes(1));
                }
            }
        }

        public void SyncAllPlatforms()
        {
            if (_seriesService == null || !_configService.MetadataGenerateGamelistXml)
            {
                return;
            }

            var allPlatforms = _seriesService.GetAllSeries() ?? new List<Series>();
            foreach (var platform in allPlatforms)
            {
                try
                {
                    GeneratePlatformGamelistXml(platform);
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Unable to generate gamelist.xml for platform {0}", platform?.Title);
                }
            }
        }

        public string GeneratePlatformGamelistXml(Series platform)
        {
            if (platform == null || string.IsNullOrWhiteSpace(platform.Path) || !_configService.MetadataGenerateGamelistXml)
            {
                return null;
            }

            if (_diskProvider != null && !_diskProvider.FolderExists(platform.Path))
            {
                return null;
            }

            var romFiles = _mediaFileService?.GetFilesBySeries(platform.Id) ?? new List<EpisodeFile>();
            var games = _episodeService?.GetEpisodeBySeries(platform.Id) ?? new List<Episode>();
            var gamesByFileId = games
                .Where(g => g.EpisodeFileId > 0)
                .GroupBy(g => g.EpisodeFileId)
                .ToDictionary(grp => grp.Key, grp => grp.First());

            var gameListEl = new XElement("gameList");

            foreach (var romFile in romFiles.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(romFile.RelativePath))
                {
                    continue;
                }

                gamesByFileId.TryGetValue(romFile.Id, out var matchedGame);
                var displayName = matchedGame?.Title ?? Path.GetFileNameWithoutExtension(romFile.RelativePath);
                var relativeUnixPath = "./" + romFile.RelativePath.Replace('\\', '/').TrimStart('/');
                var cleanFileStem = Path.GetFileNameWithoutExtension(romFile.RelativePath);

                var gameEl = new XElement(
                    "game",
                    new XElement("path", relativeUnixPath),
                    new XElement("name", displayName));

                if (_configService.MetadataPublishingMetadata)
                {
                    if (!string.IsNullOrWhiteSpace(matchedGame?.Overview))
                    {
                        gameEl.Add(new XElement("desc", matchedGame.Overview));
                    }

                    if (matchedGame?.AirDateUtc.HasValue == true)
                    {
                        gameEl.Add(new XElement("releasedate", matchedGame.AirDateUtc.Value.ToString("yyyyMMddT000000")));
                    }

                    var manufacturer = !string.IsNullOrWhiteSpace(platform.Network)
                        ? platform.Network.Replace(" (Platform)", string.Empty).Trim()
                        : platform.Title;
                    if (!string.IsNullOrWhiteSpace(manufacturer))
                    {
                        gameEl.Add(new XElement("developer", manufacturer));
                        gameEl.Add(new XElement("publisher", manufacturer));
                    }
                }

                if (_configService.MetadataBoxArt2d || _configService.MetadataBoxArt3d)
                {
                    gameEl.Add(new XElement("image", $"./media/box2d/{cleanFileStem}.png"));
                }

                if (_configService.MetadataClearLogos)
                {
                    gameEl.Add(new XElement("marquee", $"./media/marquee/{cleanFileStem}.png"));
                }

                if (_configService.MetadataVideoPreviews)
                {
                    gameEl.Add(new XElement("video", $"./media/videos/{cleanFileStem}.mp4"));
                }

                if (_configService.MetadataManualsPdf)
                {
                    gameEl.Add(new XElement("manual", $"./media/manuals/{cleanFileStem}.pdf"));
                }

                gameListEl.Add(gameEl);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), gameListEl);
            var xmlContent = doc.Declaration + Environment.NewLine + doc.ToString();

            var gamelistPath = Path.Combine(platform.Path, "gamelist.xml");
            if (_diskProvider != null)
            {
                _diskProvider.WriteAllText(gamelistPath, xmlContent);
            }

            return xmlContent;
        }

        public void Handle(SeriesRefreshCompleteEvent message)
        {
            SyncAllPlatforms();
        }

        public void Handle(EpisodeImportedEvent message)
        {
            if (message?.EpisodeInfo?.Series != null)
            {
                try
                {
                    var firstEpisode = message.EpisodeInfo.Episodes?.FirstOrDefault();
                    ScrapeMediaAssetsForImportedRom(message.EpisodeInfo.Series, message.ImportedEpisode, firstEpisode);
                    GeneratePlatformGamelistXml(message.EpisodeInfo.Series);
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Unable to post-process metadata assets after ROM import for {0}", message.EpisodeInfo.Series.Title);
                }
            }
        }

        private List<MetadataScraperConfigModel> BuildDefaultScrapers()
        {
            return new List<MetadataScraperConfigModel>
            {
                new MetadataScraperConfigModel
                {
                    Id = "screenscraper",
                    Name = "ScreenScraper.fr",
                    Description = "Comprehensive retro artwork, 3D box renders, clear logos, gameplay video snaps (.mp4), and PDF manuals",
                    RequiresCredentials = true,
                    Enabled = _configService.EnableScreenScraper,
                    Priority = 1,
                    MaxRequestsPerMinute = 30,
                    Username = _configService.ScreenScraperUsername ?? string.Empty,
                    Password = _configService.ScreenScraperPassword ?? string.Empty,
                    PublishingMetadata = true,
                    BoxArt2d = true,
                    BoxArt3d = true,
                    Screenshots = true,
                    TitleScreens = true,
                    ClearLogos = true,
                    VideoPreviews = true,
                    ManualsPdf = true
                },
                new MetadataScraperConfigModel
                {
                    Id = "igdb",
                    Name = "IGDB (Twitch Developer API)",
                    Description = "Rich game synopses, release dates, developer/publisher credits, cover art, and screenshots",
                    RequiresCredentials = true,
                    Enabled = _configService.EnableIgdb,
                    Priority = 2,
                    MaxRequestsPerMinute = 120,
                    ClientId = _configService.IgdbClientId ?? string.Empty,
                    ClientSecret = _configService.IgdbClientSecret ?? string.Empty,
                    PublishingMetadata = true,
                    BoxArt2d = true,
                    BoxArt3d = false,
                    Screenshots = true,
                    TitleScreens = true,
                    ClearLogos = false,
                    VideoPreviews = false,
                    ManualsPdf = false
                },
                new MetadataScraperConfigModel
                {
                    Id = "thegamesdb",
                    Name = "TheGamesDB (TGDB)",
                    Description = "Community console box art, clear logos, screenshots, and release information",
                    RequiresCredentials = true,
                    Enabled = _configService.EnableTheGamesDb,
                    Priority = 3,
                    MaxRequestsPerMinute = 60,
                    ApiKey = _configService.TheGamesDbApiKey ?? string.Empty,
                    PublishingMetadata = true,
                    BoxArt2d = true,
                    BoxArt3d = false,
                    Screenshots = true,
                    TitleScreens = true,
                    ClearLogos = true,
                    VideoPreviews = false,
                    ManualsPdf = false
                },
                new MetadataScraperConfigModel
                {
                    Id = "libretro_launchbox",
                    Name = "Libretro Thumbnails & LaunchBox Community DB",
                    Description = "Public CDN for 2D box art, in-game snaps, title screens, clear logos, and release years (no account required)",
                    RequiresCredentials = false,
                    Enabled = _configService.EnableLaunchBox,
                    Priority = 4,
                    MaxRequestsPerMinute = 180,
                    PublishingMetadata = true,
                    BoxArt2d = true,
                    BoxArt3d = false,
                    Screenshots = true,
                    TitleScreens = true,
                    ClearLogos = true,
                    VideoPreviews = false,
                    ManualsPdf = false
                }
            };
        }

        private string GetSettingsFilePath()
        {
            if (_appFolderInfo == null)
            {
                return null;
            }

            try
            {
                var appData = _appFolderInfo.AppDataFolder;
                return !string.IsNullOrWhiteSpace(appData)
                    ? Path.Combine(appData, "metadata_scrapers.json")
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private bool AssetExists(string mediaRoot, string subFolder, string fileStem, string extension)
        {
            var path = Path.Combine(mediaRoot, subFolder, $"{fileStem}{extension}");
            if (_diskProvider != null && _diskProvider.FileExists(path))
            {
                return true;
            }

            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        private static bool TryAcquireRateLimitSlot(string scraperId, int maxRequestsPerMinute)
        {
            var now = DateTime.UtcNow;
            if (ScraperCooldownUntil.TryGetValue(scraperId, out var cooldownUntil) && now < cooldownUntil)
            {
                return false;
            }

            var cap = Math.Max(5, maxRequestsPerMinute);
            var queue = ScraperRequestWindows.GetOrAdd(scraperId, _ => new Queue<DateTime>());

            lock (queue)
            {
                while (queue.Count > 0 && (now - queue.Peek()).TotalSeconds >= 60)
                {
                    queue.Dequeue();
                }

                if (queue.Count >= cap)
                {
                    return false;
                }

                queue.Enqueue(now);
                return true;
            }
        }

        private static void RegisterScraperCooldown(string scraperId, TimeSpan duration)
        {
            ScraperCooldownUntil[scraperId] = DateTime.UtcNow.Add(duration);
        }

        private Dictionary<string, string> ResolveScraperAssetUrls(
            MetadataScraperConfigModel scraper,
            PlatformCatalogEntry platformEntry,
            string gameTitle,
            string cleanFileStem)
        {
            var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(scraper.Id, "libretro_launchbox", StringComparison.OrdinalIgnoreCase))
            {
                var sys = platformEntry?.LibretroSystemName;
                if (!string.IsNullOrWhiteSpace(sys))
                {
                    var sanitizedName = cleanFileStem
                        .Replace('&', '_')
                        .Replace('*', '_')
                        .Replace('/', '_')
                        .Replace(':', '_')
                        .Replace('`', '_')
                        .Replace('<', '_')
                        .Replace('>', '_')
                        .Replace('?', '_')
                        .Replace('\\', '_')
                        .Replace('|', '_');

                    var escapedSys = Uri.EscapeDataString(sys);
                    var escapedFile = Uri.EscapeDataString($"{sanitizedName}.png");

                    urls["box2d"] = $"https://thumbnails.libretro.com/{escapedSys}/Named_Boxarts/{escapedFile}";
                    urls["screenshots"] = $"https://thumbnails.libretro.com/{escapedSys}/Named_Snaps/{escapedFile}";
                    urls["titlescreens"] = $"https://thumbnails.libretro.com/{escapedSys}/Named_Titles/{escapedFile}";
                    urls["marquee"] = $"https://thumbnails.libretro.com/{escapedSys}/Named_Logos/{escapedFile}";
                }
            }
            else if (string.Equals(scraper.Id, "screenscraper", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(scraper.Username) &&
                     !string.IsNullOrWhiteSpace(scraper.Password))
            {
                var url = $"https://api.screenscraper.fr/api2/jeuRecherche.php?output=json&ssid={Uri.EscapeDataString(scraper.Username)}&sspassword={Uri.EscapeDataString(scraper.Password)}&softname=Controllarr&recherche={Uri.EscapeDataString(gameTitle)}";
                var response = ExecuteRateLimitedRequest(scraper.Id, url);
                if (response != null && !string.IsNullOrWhiteSpace(response.Content))
                {
                    using (var doc = JsonDocument.Parse(response.Content))
                    {
                        if (doc.RootElement.TryGetProperty("response", out var respEl) &&
                            respEl.TryGetProperty("jeux", out var jeuxEl) &&
                            jeuxEl.ValueKind == JsonValueKind.Array)
                        {
                            var firstGame = jeuxEl.EnumerateArray().FirstOrDefault();
                            if (firstGame.ValueKind == JsonValueKind.Object &&
                                firstGame.TryGetProperty("medias", out var mediasEl) &&
                                mediasEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var media in mediasEl.EnumerateArray())
                                {
                                    var mediaType = media.TryGetProperty("type", out var tEl) ? tEl.GetString() : null;
                                    var mediaUrl = media.TryGetProperty("url", out var uEl) ? uEl.GetString() : null;
                                    if (string.IsNullOrWhiteSpace(mediaType) || string.IsNullOrWhiteSpace(mediaUrl))
                                    {
                                        continue;
                                    }

                                    if (mediaType.StartsWith("box-2D", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("box2d"))
                                    {
                                        urls["box2d"] = mediaUrl;
                                    }
                                    else if (mediaType.StartsWith("box-3D", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("box3d"))
                                    {
                                        urls["box3d"] = mediaUrl;
                                    }
                                    else if (string.Equals(mediaType, "ss", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("screenshots"))
                                    {
                                        urls["screenshots"] = mediaUrl;
                                    }
                                    else if (string.Equals(mediaType, "sstitle", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("titlescreens"))
                                    {
                                        urls["titlescreens"] = mediaUrl;
                                    }
                                    else if ((string.Equals(mediaType, "wheel", StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(mediaType, "wheel-hd", StringComparison.OrdinalIgnoreCase)) &&
                                             !urls.ContainsKey("marquee"))
                                    {
                                        urls["marquee"] = mediaUrl;
                                    }
                                    else if (mediaType.StartsWith("video", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("videos"))
                                    {
                                        urls["videos"] = mediaUrl;
                                    }
                                    else if (string.Equals(mediaType, "manuel", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("manuals"))
                                    {
                                        urls["manuals"] = mediaUrl;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (string.Equals(scraper.Id, "thegamesdb", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(scraper.ApiKey))
            {
                var url = $"https://api.thegamesdb.net/v1/Games/ByGameName?apikey={Uri.EscapeDataString(scraper.ApiKey)}&name={Uri.EscapeDataString(gameTitle)}&include=boxart";
                var response = ExecuteRateLimitedRequest(scraper.Id, url);
                if (response != null && !string.IsNullOrWhiteSpace(response.Content))
                {
                    using (var doc = JsonDocument.Parse(response.Content))
                    {
                        if (doc.RootElement.TryGetProperty("include", out var incEl) &&
                            incEl.TryGetProperty("boxart", out var boxartEl))
                        {
                            var baseUrl = boxartEl.TryGetProperty("base_url", out var baseEl) &&
                                          baseEl.TryGetProperty("original", out var origEl)
                                ? origEl.GetString()
                                : null;

                            if (!string.IsNullOrWhiteSpace(baseUrl) &&
                                boxartEl.TryGetProperty("data", out var dataEl) &&
                                dataEl.ValueKind == JsonValueKind.Object)
                            {
                                var firstProp = dataEl.EnumerateObject().FirstOrDefault();
                                if (firstProp.Value.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var img in firstProp.Value.EnumerateArray())
                                    {
                                        var type = img.TryGetProperty("type", out var tEl) ? tEl.GetString() : null;
                                        var side = img.TryGetProperty("side", out var sEl) ? sEl.GetString() : null;
                                        var filename = img.TryGetProperty("filename", out var fEl) ? fEl.GetString() : null;
                                        if (string.IsNullOrWhiteSpace(filename))
                                        {
                                            continue;
                                        }

                                        var fullUrl = $"{baseUrl.TrimEnd('/')}/{filename.TrimStart('/')}";
                                        if (string.Equals(type, "boxart", StringComparison.OrdinalIgnoreCase) &&
                                            (string.IsNullOrWhiteSpace(side) || string.Equals(side, "front", StringComparison.OrdinalIgnoreCase)) &&
                                            !urls.ContainsKey("box2d"))
                                        {
                                            urls["box2d"] = fullUrl;
                                        }
                                        else if (string.Equals(type, "screenshot", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("screenshots"))
                                        {
                                            urls["screenshots"] = fullUrl;
                                        }
                                        else if (string.Equals(type, "titlescreen", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("titlescreens"))
                                        {
                                            urls["titlescreens"] = fullUrl;
                                        }
                                        else if (string.Equals(type, "clearlogo", StringComparison.OrdinalIgnoreCase) && !urls.ContainsKey("marquee"))
                                        {
                                            urls["marquee"] = fullUrl;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (string.Equals(scraper.Id, "igdb", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(scraper.ClientId) &&
                     !string.IsNullOrWhiteSpace(scraper.ClientSecret))
            {
                var tokenUrl = $"https://id.twitch.tv/oauth2/token?client_id={Uri.EscapeDataString(scraper.ClientId)}&client_secret={Uri.EscapeDataString(scraper.ClientSecret)}&grant_type=client_credentials";
                var tokenReq = new HttpRequest(tokenUrl)
                {
                    Method = HttpMethod.Post,
                    SuppressHttpError = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                var tokenResp = _httpClient?.Execute(tokenReq);
                if (tokenResp != null && !tokenResp.HasHttpError && !string.IsNullOrWhiteSpace(tokenResp.Content))
                {
                    string accessToken = null;
                    using (var tokenDoc = JsonDocument.Parse(tokenResp.Content))
                    {
                        if (tokenDoc.RootElement.TryGetProperty("access_token", out var atEl))
                        {
                            accessToken = atEl.GetString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        var safeQuery = gameTitle.Replace("\"", string.Empty).Trim();
                        var igdbReq = new HttpRequest("https://api.igdb.com/v4/games")
                        {
                            Method = HttpMethod.Post,
                            SuppressHttpError = true,
                            RequestTimeout = TimeSpan.FromSeconds(10)
                        };
                        igdbReq.Headers.Set("Client-ID", scraper.ClientId);
                        igdbReq.Headers.Set("Authorization", $"Bearer {accessToken}");
                        igdbReq.SetContent($"search \"{safeQuery}\"; fields name,cover.image_id,screenshots.image_id,artworks.image_id; limit 1;");

                        var igdbResp = _httpClient.Execute(igdbReq);
                        if (igdbResp != null && !igdbResp.HasHttpError && !string.IsNullOrWhiteSpace(igdbResp.Content))
                        {
                            using (var igdbDoc = JsonDocument.Parse(igdbResp.Content))
                            {
                                if (igdbDoc.RootElement.ValueKind == JsonValueKind.Array)
                                {
                                    var first = igdbDoc.RootElement.EnumerateArray().FirstOrDefault();
                                    if (first.ValueKind == JsonValueKind.Object)
                                    {
                                        if (first.TryGetProperty("cover", out var coverEl) &&
                                            coverEl.ValueKind == JsonValueKind.Object &&
                                            coverEl.TryGetProperty("image_id", out var covId) &&
                                            !string.IsNullOrWhiteSpace(covId.GetString()))
                                        {
                                            urls["box2d"] = $"https://images.igdb.com/igdb/image/upload/t_cover_big/{covId.GetString()}.png";
                                        }

                                        if (first.TryGetProperty("screenshots", out var ssArr) &&
                                            ssArr.ValueKind == JsonValueKind.Array)
                                        {
                                            var firstSs = ssArr.EnumerateArray().FirstOrDefault();
                                            if (firstSs.ValueKind == JsonValueKind.Object &&
                                                firstSs.TryGetProperty("image_id", out var ssId) &&
                                                !string.IsNullOrWhiteSpace(ssId.GetString()))
                                            {
                                                urls["screenshots"] = $"https://images.igdb.com/igdb/image/upload/t_screenshot_big/{ssId.GetString()}.png";
                                            }
                                        }

                                        if (first.TryGetProperty("artworks", out var artArr) &&
                                            artArr.ValueKind == JsonValueKind.Array)
                                        {
                                            var firstArt = artArr.EnumerateArray().FirstOrDefault();
                                            if (firstArt.ValueKind == JsonValueKind.Object &&
                                                firstArt.TryGetProperty("image_id", out var artId) &&
                                                !string.IsNullOrWhiteSpace(artId.GetString()))
                                            {
                                                urls["titlescreens"] = $"https://images.igdb.com/igdb/image/upload/t_screenshot_big/{artId.GetString()}.png";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return urls;
        }

        private HttpResponse ExecuteRateLimitedRequest(string scraperId, string url)
        {
            if (_httpClient == null)
            {
                return null;
            }

            var request = new HttpRequest(url)
            {
                SuppressHttpError = true,
                RequestTimeout = TimeSpan.FromSeconds(12)
            };

            var response = _httpClient.Execute(request);
            if (response != null &&
                (response.StatusCode == HttpStatusCode.TooManyRequests ||
                 response.StatusCode == HttpStatusCode.Forbidden ||
                 response.StatusCode == HttpStatusCode.ServiceUnavailable))
            {
                RegisterScraperCooldown(scraperId, TimeSpan.FromMinutes(2));
                return null;
            }

            return response != null && !response.HasHttpError ? response : null;
        }

        private void DownloadAssetToDisk(string scraperId, string url, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(destinationPath) || _httpClient == null)
            {
                return;
            }

            if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length > 0)
            {
                return;
            }

            var response = ExecuteRateLimitedRequest(scraperId, url);
            if (response?.ResponseData == null || response.ResponseData.Length < 64)
            {
                return;
            }

            var folder = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllBytes(destinationPath, response.ResponseData);
        }
    }
}
