# Changelog

All notable changes to **Controllarr** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Fixed
- **Canonical BIOS Catalog Cryptographic Hash Verification & Sega CD JP BIOS Support**:
  - Audited and corrected `ExpectedMd5` and `ExpectedSha1` checksums across all 57 entries in `CanonicalBiosCatalog` in `src/NzbDrone.Core/Bios/BiosService.cs` (including `bios_CD_J.bin` for Mega-CD JP v1.00, which previously had a mismatched `ExpectedSha1` that caused valid files to fail verification and be discarded).
  - Added multi-revision verification support (`AlternateBiosHashesByFileName`) and 16-bit byte-swapped ROM detection for Sega CD / Mega-CD (`bios_CD_J.bin`, `bios_CD_U.bin`, `bios_CD_E.bin`) and official regional BIOS revisions.
  - Preserved `.zip`-packaged arcade/system BIOS archives (`neogeo.zip`, `naomi.zip`, `awbios.zip`, `cdimono1.zip`) intact on disk during verification and auto-download, and updated `BuildCandidateDownloadUrls` to target current `Abdess/retroarch_system` (`main/bios/...`) and `archtaurus/RetroPieBIOS` directory paths.

### Removed
- **Comprehensive Purge of Dead Video/TV Subsystems & TV Providers (Evaluation Audit)**:
  - **Video Subtitles Subsystem (100% Dead)**: Completely removed the video subtitle subsystem (`src/NzbDrone.Core/Extras/Subtitles/`, `SubtitleFile.cs`, `SubtitleFileRepository.cs`, `SubtitleService.cs`, `SubtitleFileService.cs`, `ExistingSubtitleImporter.cs`, `AggregateSubtitleInfo.cs`, and `CleanupOrphanedSubtitleFiles.cs`) along with tests, removing all `.srt`, `.sub`, `.ass`, `.vtt` video subtitle processing irrelevant to console video game ROMs.
  - **SkyHook Sonarr Upstream Proxy & Models**: Purged dead `SkyHookProxy.cs`, exceptions, and all TV show DTOs under `src/NzbDrone.Core/MetadataSource/SkyHook/`.
  - **Daily Broadcast TV Series Services**: Removed talk-show daily series proxies and services (`DailyPlatformDataProxy.cs`, `DailyPlatformService.cs`) and test fixtures.
  - **TV-Only Import Lists**: Purged all TV and video streaming import list providers (`Trakt`, `Simkl`, `Plex`, `Plex RSS`, `Sonarr`) and their unit test fixtures.
  - **TV Media Server Notifications**: Purged video media server notification agents (`Trakt Scrobbler`, `Plex Media Server`, `Kodi / XBMC`, and `Emby / Jellyfin MediaBrowser`) and their test fixtures.
  - **TV Metadata Consumers**: Removed `PlexMetadata` (`.plexmatch`) and `XbmcMetadata` (`tvshow.nfo` and `<episodedetails>` XML sidecars) from `src/NzbDrone.Core/Extras/Metadata/Consumers/`.
  - **Unlinked TV Calendar Feature & Route**: Completely deleted the orphaned 21-file `frontend/src/Calendar/` feature and its route from `AppRoutes.tsx`.
  - **Orphaned Frontend Utilities & Dead UI Components**: Deleted legacy Flux directory `frontend/src/Utilities/State/`, dead consumer settings UI in `frontend/src/Settings/Metadata/Metadata/`, dead TheTVDB attribution (`TheTvdb.tsx`), unreferenced `PageSidebarStatus.css`, and dead QualityProfile editor components (`QualityProfiles.tsx`, `EditQualityProfileModal*.tsx`, etc.).
  - **TV Table Columns & Video MediaInfo Properties**: Removed `network`, `nextAiring`, and `previousAiring` default table columns and filter builder rules from `platformOptionsStore.ts` and `usePlatform.ts`; removed video codec/stream columns (`videoCodec`, `videoDynamicRangeType`, `audioInfo`, `audioLanguages`, `subtitleLanguages`, `runtime`) from `gameOptionsStore.ts` and `GameRow.tsx`; removed TV season folder and season pack controls from platform edit modals and media management settings.
- **Defunct TV-Only Trackers & Anime Import Lists**:
  - Removed TV-exclusive tracker BroadcastheNet (`BroadcastheNet.cs` and fixtures) and movies/TV documentary tracker HDBits (`HDBits.cs` and fixtures).
  - Removed TV anime import lists `AniList` and `MyAnimeList` (`src/NzbDrone.Core/ImportLists/AniList/` and `src/NzbDrone.Core/ImportLists/MyAnimeList/`).
  - Removed TV scene numbering column (`useSceneNumbering`) from platform tables, options stores, and filter builders, and removed TV scene numbering warning badges.
- **Legacy Sonarr Remnants & Obsolete TV Services (Category 3 Consolidation)**:
  - **Scene Mapping Scheduled Task & Cloud TV Proxy**: Removed periodic 3-hour `UpdateSceneMappingCommand` background task from `TaskManager.cs` and neutralized `SceneMappingProxy.Fetch()` to return an empty list, completely eliminating recurring network calls to `services.sonarr.tv`.
  - **TheXEM TV Numbering Proxy & Specification**: Neutralized `UnverifiedSceneNumberingSpecification.cs` to unconditionally accept ROM files without requiring TV episode numbering confirmation, and neutralized `XemProxy.cs` endpoints to eliminate outbound network traffic to the blocked/defunct `thexem.info`.
  - **SkyHook TV Proxy Decoupling & Offline Unit Testing**: Removed live HTTP requests and `SkyHookProxy` calls from `RefreshGameServiceFixture.cs`, generating in-memory test series and episode data fixtures to ensure 100% offline unit test isolation in compliance with architecture directives. Neutralized `DailyPlatformDataProxy.cs` to return empty collections immediately.
  - **Obsolete TV Metadata Consumers**: Removed legacy Western Digital TV Live and Roku XML metadata consumers (`Roksbox` and `Wdtv`) along with their test fixtures from `src/NzbDrone.Core/Extras/Metadata/Consumers/`.
  - **Defunct TV Anime Indexer (`Fanzub`)**: Removed defunct anime TV fansub indexer implementation and test fixtures (`Fanzub.cs`, `FanzubRequestGenerator.cs`, `FanzubSettings.cs`, `FanzubFixture.cs`).
  - **Duplicate Localization String Keys**: Pruned redundant duplicate TV keys in `src/NzbDrone.Core/Localization/Core/en.json` (`AllowSeasonPackWithoutAllEpisodesAired*`, `AnEpisodeIsDownloading`, `CountSeasonsCountEpisodes`, `CountSeasonsOneEpisode`, `OneSeasonCountEpisodes`, `OneSeasonOneEpisode`) that shadowed game-native keys.

### Changed
- **Indexer Category Search Alignment & Elimination of Hardcoded Background Categories**:
  - Audited all indexer request generators and settings (`Newznab`, `Torznab`, `FileList`, `Nyaa`, `IPTorrents`, `Torrentleech`, `TorrentRss`, `InternetArchive`) to guarantee search queries strictly search only categories defined by the user.
  - Removed `NonGameCategoryHundreds` and the hardcoded 25-category fallback list from `NewznabRequestGenerator.cs`, ensuring user-configured categories (standard or custom tracker categories) are never stripped or modified in the background, and omitting the `&cat=` query parameter when empty without injecting fallback categories.
  - Aligned `NewznabRssParser.cs` and `TorznabRssParser.cs` with `AllowedCategories` so items matching categories explicitly selected by the user are never discarded by background filtering heuristics.
  - Replaced legacy TV category enum in `FileListSettings.cs` (`TV_SD`, `TV_HD`, `TV_4K`, `Anime`, `Sport`, etc.) with game categories (`GamesConsole = 10`, `GamesPC = 9`, `Software = 25`), defaulting to console and PC game categories, and removed TV `&season=&episode=` query parameters from `FileListRequestGenerator.cs` to enable full game title searches.
  - Updated `NyaaSettings.cs` default query parameters to target games (`&cats=6_2&filter=1` instead of anime `&cats=1_0`), and updated `NyaaRequestGenerator.cs` to search game titles directly rather than requiring TV season/episode numbers.
- **Total TV Vocabulary, Terminology & Function Elimination**:
  - **Platform Types & Descriptions**: Replaced TV "Anime" and "Daily" options in `PlatformTypeSelectInput.tsx`, `PlatformTypePopoverContent.tsx`, and `PlatformTypeFilterBuilderRowValue.tsx` with game-native terminology (`Standard (Regional)` with regional catalog hints, and `Single Collection (Flat)` with direct catalog hints; eliminated `Daily` talk shows completely).
  - **Scheduled Tasks & Command Presentation**: Aligned `TaskController.cs` (V3 and V5) and `CommandResource.cs` (V3 and V5) to display "Refresh Platforms" instead of "Refresh Series" across the System Tasks table, command queue, and activity progress toasts.
  - **Game Numbering & Alternate Title Display**: Refactored `GameNumber.tsx` and `SceneInfo.tsx` to display clean regional alternate titles without TV season/episode numbers and without TV `S01E02` suffixes, renaming "Scene Information" to "Release Information".
  - **Statistics Page Cleanliness**: Aligned `Statistics.tsx` header scope to "Platforms" instead of "Media", and removed zero-count TV `Daily` and `Anime` slices from the Platform Types donut chart.
  - **Localization & String Alignment**: Updated `en.json` to replace TV and anime strings with gaming equivalents ("Flat / Direct", "Flat Game Format", "Release Info", "Catalog Numbering").
- **Category 2 Redundant & Duplicated Code Consolidation**:
  - **Dead jQuery Dependency Elimination**: Migrated `createAjaxRequest.js` and `requestAction.js` to modern standard `fetch`, `AbortController`, and `URLSearchParams` with promise `.done()`, `.fail()`, and `.always()` backwards compatibility shims, removing `jquery` from `package.json` and saving ~90KB minified.
  - **Platform Metadata, Alias & Extension Centralization**: Consolidated duplicate platform slug dictionaries, cartridge type sets, exclusive ROM file extensions, ODE/flash-cart raw extensions, and canonical platform display names into `PlatformCatalogService.cs`. `DatCacheService.cs`, `PlatformCompatibilityValidator.cs`, and `SdCardExporterService.cs` now consume the unified authoritative catalog directly, removing >150 lines of duplicate dictionaries.
  - **Game-Native Controllers Migration to V5 & API Route Alignment**: Relocated all game-native controllers (`BiosController.cs`, `GameLauncherController.cs`, `GameMetadataSettingsController.cs`, `RetroAchievementsController.cs`, `SdCardExportController.cs`, and `VirtualLibraryController.cs`) to `Controllarr.Api.V5/Games/` with nullable reference annotations, updated `GameLauncherService.cs` default stream URLs to `/api/v5/gamelauncher`, and aligned frontend `/api/v3` references across `gameAchievementsHelper.ts`, `WebPlayerPage.tsx`, and `useProviderOptions.ts` directly to `/api/v5`.
- **Category 1 Performance & Memory Optimizations**:
  - **Frontend Table Virtualization (`PlatformDetailsRegion.tsx`)**: Implemented windowed row slicing with dynamic scroll parent detection and spacer rows for region game lists exceeding 50 games. Reduces mounted DOM nodes and React hook execution by >99% for massive platforms (e.g., Nintendo Switch with 21,000+ entries) while maintaining smooth 60 FPS scrolling and full table responsiveness.
  - **Route-Based Frontend Code Splitting (`React.lazy`)**: Split non-initial frontend routes (`WebPlayerPage`, `RomPatcherPage`, `Statistics`, settings pages, and system utilities) into asynchronous dynamic import chunks wrapped in `<Suspense fallback={<LoadingIndicator />}>` in `App.tsx` and `AppRoutes.tsx`, isolating heavy components and reducing initial page load overhead.
  - **Streaming GameTDB XML Catalog Parser (`GameCatalogProxy.cs`)**: Replaced full in-memory string buffering and `XDocument.Parse` with forward-only `XmlReader` streaming and element-by-element `XNode.ReadFrom(xmlReader) as XElement` parsing, eliminating 300MB-500MB transient memory allocation spikes during multi-platform catalog synchronizations.
  - **High-Throughput ROM Hashing & Fast-Path CRC32 Cache Lookup (`UnmanagedDiskScanner.cs`)**: Upgraded streaming ROM hash buffer to 128KB pooled byte arrays via `ArrayPool<byte>.Shared.Rent(128 * 1024)` and introduced an $O(1)$ CRC32 fast-path lookup against `IDatCacheService.FindByCrc32` to retrieve pre-computed cryptographic hashes (`SHA-1`, `MD5`, `SHA-256`) from archive headers before decompressing inner streams.
- **Platform View Search Dropdown Button & Sidebar "Platforms" Navigation**:
  - Replaced the standalone "Search Monitored" and "Search for Collection" toolbar buttons on the platform details page (`frontend/src/Platform/Details/PlatformDetails.tsx`) with a single "Search" dropdown button positioned to the left of "Refresh & Scan ROMs" to declutter the top toolbar.
  - Renamed the primary sidebar navigation group label from "Media" to "Platforms" in `frontend/src/Components/Page/Sidebar/PageSidebar.tsx`.
- **Direct API V5, Command, Configuration & Localization Naming Alignment (Zero Aliasing)**:
  - Removed all code-level token aliasing in `frontend/src/Utilities/String/translate.ts` and updated localization templates in `src/NzbDrone.Core/Localization/Core/en.json` and `PlatformIndexProgressBar.tsx` to use `{romFileCount}`, `{gameCount}`, `{totalGameCount}`, `{gameFormat}`, and `{game}` directly.
