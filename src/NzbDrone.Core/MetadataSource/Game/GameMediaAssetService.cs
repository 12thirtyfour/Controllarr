using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common.Disk;
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
        MetadataSourceConfigModel GetMetadataSources();
        MetadataSourceConfigModel SaveMetadataSources(MetadataSourceConfigModel config);
        GameMediaAssetSettingsModel GetMediaAssetSettings();
        GameMediaAssetSettingsModel SaveMediaAssetSettings(GameMediaAssetSettingsModel settings);
        string GeneratePlatformGamelistXml(Series platform);
        void SyncAllPlatforms();
        List<DatFile> GetDatFiles();
        DatImportResultModel ImportDatOrSmdb(DatImportRequestModel request);
    }

    public class GameMediaAssetService :
        IGameMediaAssetService,
        IHandle<SeriesRefreshCompleteEvent>,
        IHandle<EpisodeImportedEvent>
    {
        private readonly IConfigService _configService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IDiskProvider _diskProvider;
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
            _datCacheService = datCacheService;
            _datParserService = datParserService;
            _smdbParserService = smdbParserService;
            _gameCatalogService = gameCatalogService;
            _seriesInfo = seriesInfo;
            _logger = logger;
        }

        public MetadataSourceConfigModel GetMetadataSources()
        {
            return new MetadataSourceConfigModel
            {
                EnableNoIntroLibretro = _configService.EnableNoIntroLibretro,
                EnableHtgdbSmdb = _configService.EnableHtgdbSmdb,
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

            _configService.EnableNoIntroLibretro = config.EnableNoIntroLibretro;
            _configService.EnableHtgdbSmdb = config.EnableHtgdbSmdb;
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
                    GeneratePlatformGamelistXml(message.EpisodeInfo.Series);
                }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Unable to update gamelist.xml after ROM import for {0}", message.EpisodeInfo.Series.Title);
                }
            }
        }
    }
}
