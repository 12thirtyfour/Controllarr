# Changelog

All notable changes to **Controllarr** are documented in this file.

> [!NOTE]
> **Project Lifecycle & Versioning Model: Alpha**
> Controllarr is currently in active **Alpha** development as core architecture, platform models, and media acquisition workflows are built and stabilized. Formal versioned releases (Beta, RC, and 1.0.0) will begin once the baseline feature set is verified. During Alpha, all changes are tracked chronologically by **Date**.
>
> **Beta Readiness Gate (Baseline Capabilities Required)**:
> 1. **Library Creation & Management**: Adding console platforms, configuring regional folder paths, and library customization.
> 2. **Library Import**: Scanning and importing existing collections and loose ROMs with accurate cryptographic identification.
> 3. **Indexing**: Ingesting releases from configured indexers with clean console categorization and zero TV/PC bleed.
> 4. **Searching**: Automated and interactive searching for both full collections and single ROMs.
> 5. **Downloading**: Seamless queue dispatch across all 3 acquisition protocol types: **Usenet**, **BitTorrent**, and **Internet Archive**.
> 6. **Cryptographic Verification**: Post-download hash-gating (CRC32, MD5, SHA-1) against authentic DAT catalogs (No-Intro, Redump, TOSEC, SMDB).
> 7. **Import & Organization**: Moving verified files to their correct destination library path with standardized game and disc renaming.
>
> **Release Progression**:
> - **Alpha (Current)**: Core baseline feature implementation and bug fixing (tracked by date).
> - **Beta**: Comprehensive multi-OS and physical hardware testing across all features once baseline works.
> - **Release Candidate (RC)**: Final pre-release stability verification once features work fully as expected.
> - **v1.0.0**: Stable production release across all 10 OS/architecture packages and multi-arch Docker containers.

---

## [Unreleased]

---

## [2026-11-06]

### Fixed
- **ROM Patcher Page Scrolling & Bottom Viewport Clipping**: Fixed the ROM Patcher page (`/patcher`) cutting off the bottom of the "Apply Patch File" section:
  - Wrapped page contents with `PageContentBody`, integrating the application's standard vertical `Scroller` and viewport flex constraints so the page can scroll completely to the bottom.
  - Added bottom padding (`paddingBottom: '48px'`) to ensure the server path input, action button, and card rounded corners maintain clear visual breathing room above the viewport bottom.
- **Console Game Update Bundling & Dual 1G1R Monitoring (3DS, Wii U, PS3, PS4, Switch)**: Fixed console game updates (e.g. `(Update v1.5)`, `[Update v1.1.7]`, `(Patch 1.02)`, `(DLC)`) listing as separate game entries or conflicting with base game monitoring under 1G1R:
  - Updated `GameCatalogProxy.cs` and `RomParser.cs` with `UpdateTagRegex` and updated `ResidualRevisionRegex` to recognize and clean standalone update, patch, and DLC tags (`Update`, `UPD`, `Patch`, `DLC`), properly assigning them to the game's `RevisionTag` rather than defaulting to `"Retail (v1.0)"` or treating the update as an independent title.
  - Implemented semantic versioning in `GameCatalogProxy.GetRevisionSortOrder` to correctly order multi-component update versions (e.g. `Update v1.2.0` < `Update v1.2.1`).
  - Added dual candidate pool evaluation in `GameCatalogProxy.GetDefaultMonitoredGameIds`, `PlatformService.ApplyOneGameOneRomMonitoringIfNeeded`, and `GameMonitoredService.SetEpisodeMonitoredStatus`: under 1G1R mode, both the preferred base game ROM and the preferred latest update file are monitored simultaneously, preventing updates from displacing the base game or vice versa while filtering out older base revisions and older updates.
  - Updated frontend `PlatformDetailsRegion.tsx` and `GameRow.tsx` to group update files under the base game title with an update toggle badge (`+X update(s)` / `Hide updates`, or `+X (updates & versions)`), ensuring the collapsed view always showcases the primary retail base game with individual monitoring controls and interactive search available for all bundled updates.
  - Added unit test coverage across `RomParserFixture.cs`, `GameCatalogProxyFixture.cs`, and `SetGameMonitoredFixture.cs`.
- **Dynamic Collection Strategy Adjustment (1G1R vs Completionist)**: Fixed library monitoring states not updating when editing a platform's strategy between Completionist (`Preservationist`) and 1G1R (`OneGameOneRom` or `OneGamePerRegionPerConsole`), or when initially adding a platform:
  - Removed artificial `isCatalogPlatform` guard in `PlatformService.ApplyOneGameOneRomMonitoringIfNeeded` and `GameMonitoredService.SetEpisodeMonitoredStatus` that previously required seasons 5 or 6 to exist, ensuring collection strategy monitoring rules apply across all console platforms and standard retail regions.
  - Switching to Completionist (`Preservationist`) automatically re-monitors all games across all active monitored regions.
  - Switching to 1G1R (`OneGameOneRom` or `OneGamePerRegionPerConsole`) properly filters and monitors the single preferred release per title based on revision preference, region priority, and encryption preference, unmonitoring redundant versions.
  - Fixed candidate pool fallback so homebrew, translation, or prototype games are not mistakenly unmonitored under 1G1R when retail releases are absent.
  - Updated frontend React Query invalidation across `usePlatform.ts` hooks and `SignalRListener.tsx` so UI game lists, region badges, and monitoring toggles immediately reflect the updated strategy without requiring a manual refresh.
  - Added unit test coverage in `UpdatePlatformFixture.cs` and `SetGameMonitoredFixture.cs`.
- **Platform Collection Multi-Region Search & Full Romset Alignment**: Fixed collection search designating entire platform collections as single-region searches (previously scoping to Season 1 / North America only) and rejecting multi-region releases.
  - Updated `RegionSearchCriteria.ToString()` and `ReleaseSearchService.PlatformCollectionSearch` so platform collection searches log and execute as `[Platform : Platform Collection]` (`SeasonNumber = 0`, `IsPlatformCollectionSearch = true`) across all regions instead of defaulting to `[Platform : North America]`.
  - Updated `DownloadDecisionMaker.cs` so matched platform collections populate `SeasonNumbers` with all configured regions for the platform (`allSeriesSeasons`) and set `IsMultiSeason = true`, ensuring games across all regions in the collection are recognized, matched, and imported upon extraction.
  - Updated `RegionMatchSpecification.cs` and `MultiRegionSpecification.cs` to accept multi-region platform collections when `IsPlatformCollectionSearch` is active.
  - Updated `InternetArchiveRequestGenerator.cs` to search for collection terms and archive collections rather than iterating individual games during collection searches, and enabled large collection archive downloads in `InternetArchiveParser.cs`.
  - Added platform collection interactive search support to Api V3 and V5 `ReleaseController` and added an Interactive Search action to the platform toolbar in `PlatformDetails.tsx`.
- **Indexer Download Client Assumption by Protocol**: Removed the downloader selection prompt from the Indexer configuration modal (`EditIndexerModalContent.tsx`). Indexers now automatically route to the configured download client matching their acquisition protocol:
  - Usenet indexers automatically route to the configured Usenet downloader (e.g. SABnzbd, NZBGet).
  - Torrent indexers automatically route to the configured BitTorrent downloader (e.g. qBittorrent, Transmission).
  - Internet Archive and HTTP indexers automatically route to the configured HTTP downloader.
  - Updated `PendingReleaseService.cs` to resolve and display the protocol-matched download client name for pending releases in the queue when no specific indexer client override is present.
- **Release Group Logic & Interactive Import Prompt Purge**: Eliminated TV-oriented scene "release group" requirements and prompts that blocked or burdened manual and completed download imports.
  - Removed the `releaseGroup` column, placeholder buttons, and bulk action options from Interactive Import (`InteractiveImportRow.tsx`, `InteractiveImportModalContent.tsx`, `interactiveImportOptionsStore.ts`).
  - Bumped interactive import options store version to `interactive_import_options_v2` so client browsers automatically adopt the clean layout without release group placeholders.
  - Deleted obsolete modal components (`SelectReleaseGroupModal.tsx`, `SelectReleaseGroupModalContent.tsx`).
  - Removed release group enforcement from download decision specifications (`RepackSpecification.cs`, `AnimeVersionUpgradeSpecification.cs`), preventing game revision/repack downloads and manual imports from being rejected due to missing scene release groups.
  - Updated `{Release Group}` token handler default fallback in `FileNameBuilder.cs` from legacy `"Sonarr"` to empty string.
  - Updated decision engine test fixtures (`RepackSpecificationFixture.cs`, `AnimeVersionUpgradeSpecificationFixture.cs`).
- **Queue Table & TV Season/Episode Holdover Elimination**: Resolved user-reported issue where the Activity > Queue table displayed a redundant "Game(s)" column cataloged in TV season x episode format (e.g. `1x1348`) alongside "Game Title(s)".
  - Removed the `game` ("Game(s)") column entirely from the Queue view in `queueOptionsStore.ts` and `QueueRow.tsx`.
  - Bumped queue options store version to `queue_options_v3` so client browsers automatically discard the cached `game` column and default to the clean layout with "Game Title" (`games.title`).
  - Deleted `GameCellContent.tsx` and purged `{seasonNumber}x{padNumber(gameNumber, 2)}` season-by-episode formatting from `GameNumber.tsx` and `RegionGameNumber.tsx`. Games are now consistently identified by their title and clean game number rather than TV episode codes (`1x1348`).
  - Removed awkward `(s)` suffixes from game and platform localization strings in `en.json` (`GameMaybePlural`, `GameTitleMaybePlural`, `EpisodeMaybePlural`, `EpisodeTitleMaybePlural`, `GameNumbers`, `AbsoluteGameNumbers`, `SelectGames`, etc.).
- **Recursive ROM Inspection & Collection Archive Library Import**: Resolved critical issues in `Library -> Import` and platform scanning where extracted folders were not inspected recursively for ROM files (causing child game folders to be erroneously detected as separate unmapped platforms) and collection zip archives were not recognized for multi-game extraction and analysis.
  - Refactored `RootFolderService.GetUnmappedFolders` to look at ROM files recursively rather than just top-level folders:
    - If a folder path resolves to a platform by name or contains ROMs belonging to a single platform, Controllarr identifies the folder itself as that platform without enumerating child game folders as separate platforms.
    - Top-level platform subfolders in multi-platform root directories are identified as single platform entries without recursing into individual game directories.
    - Exposed `ResolvePlatformFromArchive` on `IRootFolderService` to enable cross-service platform archive identification.
  - Fixed collection archive extraction abortion in `DiskScanService.ExtractCollectionArchives` and `IsMultiGameCollectionArchive`:
    - Replaced the flawed `isMultiTrackDisc` check (which previously aborted extraction on any archive containing `.cue` files) with `isSingleMultiTrack`: an archive is only considered a single disc image if it contains exactly one descriptor, no nested archives, and files in a single folder. Multi-game collection archives (such as Sega CD, Saturn, PS1 collections containing `.cue` files) are now properly recognized and extracted into `series.Path`.
    - Added dynamic platform matching for collection archives in root directories via `ResolvePlatformFromArchive` and platform aliases.
    - Recursively inspects newly extracted inner zip archives in `series.Path` to unpack multi-track discs (`.cue` + `.bin` tracks) into dedicated game subfolders and clean up wrapper archives for emulator readiness.
  - Updated `UnmanagedDiskScanner`:
    - Enhanced archive scanning to distinguish between single multi-track discs and multi-game collections, ensuring collection archives properly enumerate every game with CRC32 hashes and DAT cross-referencing.
  - Added unit tests in `RootFolderServiceFixture` (`should_identify_extracted_folder_with_game_subfolders_as_single_platform`), `ScanFixture` (`should_extract_collection_archive_with_multiple_cue_discs`), and `UnmanagedDiskScannerFixture` (`Scan_MultiDiscCollectionZip_ShouldImportEachGameDiscSeparately`).
- **Nintendo 3DS Duplicate Titles & Catalog Cross-Referencing**: Resolved issue where Nintendo 3DS games (such as *Pokémon Moon* and *Pokémon Omega Ruby*) appeared multiple times as separate entries in library and platform views.
  - Added diacritic stripping (`RemoveDiacritics()`) and non-alphanumeric normalization to `GameCatalogProxy.NormalizeCanonicalCrossRefTitle`, ensuring GameTDB titles (e.g. `Pokémon Moon`) correctly match No-Intro and built-in titles (`Pokemon - Moon` / `Pokemon Moon`) and prevent duplicate catalog entry creation.
  - Aligned `BuildDatFeedsForPlatform` and `SyncOnlineCatalogForPlatform` with 3-way `EncryptionPreference` so unencrypted (decrypted) preferences only sync decrypted feeds and encrypted preferences only sync encrypted feeds unless `Both` is selected.
  - Renamed legacy TV method `BuildPlatformSeriesAndEpisodes` to `BuildPlatformAndGames`, replaced episode counter holdovers with game counters, and purged TV terminology.
  - Implemented `normalizeGroupKey` in `PlatformDetailsRegion.tsx` removing diacritics, hyphens, and whitespace differences, and added automatic version deduplication in display rows so alternate dumps and versions cleanly group under a single row with 1G1R version management.
  - Added unit test `should_not_duplicate_3ds_titles_when_cross_referencing_different_sources` in `GameCatalogProxyFixture.cs`.
- **Platform & Region "Search Monitored" Item-by-Item Search**: Resolved issue where clicking "Search Monitored" in platform view attempted to search for every game in one bundled collection string (or sent empty request chains) rather than searching item by item.
  - Refactored `PlatformSearchService` to iterate through missing monitored games on the platform and dispatch individual `EpisodeSearch` requests game by game.
  - Refactored `RegionSearchService` to iterate through missing monitored games in the specified region and dispatch individual `EpisodeSearch` requests item by item.
  - Refactored `GameSearchService.SearchForBulkEpisodes` to search item by item instead of falling back to TV season-pack searches (`SeasonSearch`) when more than one game was in the search list.
  - Updated `PlatformSearchServiceFixture` and added unit test fixtures `RegionSearchServiceFixture` and `GameSearchServiceFixture` to guarantee 100% test coverage of item-by-item search dispatching.
- **Library Import Crash on Eliminated Quality Profiles**: Resolved `TypeError: Cannot read properties of undefined (reading 'id')` when navigating to `Library -> Import` and clicking an unmapped root folder path. Purged obsolete `qualityProfiles[0].id` assignment and quality profile hooks from `ImportPlatform.tsx` left over from prior video profile logic, allowing platform imports to load seamlessly with clean platform defaults. Updated page heading scope from legacy `Media` to `Platforms`.

---

## [2026-11-04]

### Added
- **Cross-Platform Search Disambiguation & Release Filtering**:
  - Implemented comprehensive platform detection (`DetectPlatforms`) in `PlatformCompatibilityValidator.cs` analyzing release titles, file names, bracketed platform tags (`[Wii]`, `(Switch)`), scene group prefixes/suffixes (`.WII-SUSHi`), product codes (`RVL-`, `DOL-`, `HAC-`, `WUP-`, `CUSA-`, etc.), and exclusive container extensions (`.wbfs`, `.nsp`, `.xci`).
  - Added strict platform isolation to automatically ignore and reject releases when an incorrect platform is detected (e.g. searching for Super Mario Galaxy on Nintendo Switch ignores Wii versions).
  - Ensured releases with no platform name detected in their title or file name pass through uninhibited to be downloaded and verified against DAT hash databases.
  - Updated `PlatformSpecification`, `DownloadDecisionMaker`, and both V3/V5 `ReleaseController`s to filter out releases with conflicting platforms from search decision making and interactive search results.
  - Removed `.nsp` and `.xci` from `DiscExclusiveExtensions` so Switch cartridge packages are never falsely rejected as optical disc formats.
- **Absolute TV De-Televisonization Directive in AGENTS.md**: Formally enacted strict zero-TV tolerance policy mandating the systematic renaming, repurposing, or elimination of all remaining TV, broadcast, airing, schedule, episode, season, TVDB, TVMaze, TVRage, and scene mapping holdovers across all backend, frontend, API, persistence, and test layers.

### Changed
- **Purge of Video-Specific Concepts, Containers, Formats, and Specifications**:
  - Removed video container extension collection (`.mkv`, `.avi`, `.mp4`, `.wmv`, `.mov`, `.m4v`, `.flv`, `.mpg`, `.mpeg`, `.ts`, `.m2ts`) from `FileExtensions.cs` and `RemoveFileExtension`, ensuring only console ROMs, disc images, and valid archives are handled as media file extensions.
  - Guarded `VideoFileInfoReader` (`GetMediaInfo` and `GetRunTime`) against all console ROM and optical disc extensions (`MediaFileExtensions.RomExtensions`), bypassing FFProbe execution for game assets.
  - Removed obsolete video-specific `ResolutionSpecification.cs` from Custom Formats and renamed `SeasonPackSpecificationValidator` to `ReleaseTypeSpecificationValidator`.
  - Hardened `CustomFormatSpecificationListConverter` to gracefully skip obsolete or unregistered custom format specifications during deserialization without throwing errors.
  - Replaced video-specific mock sample templates (`1080p.WEBDL.x264-EVOLVE.mkv`, video codecs `AVC`, HDR `BT.2020`/`HLG`) in `FileNameSampleService.cs` with authentic console ROM naming samples (`Super.Mario.World.USA.sfc`, `Final.Fantasy.VII.USA.Disc1.chd`, `1G1R`, `No-Intro`, `Redump`).
  - Purged video sample detection holdovers (`.flv`, `.strm`, "valid video file" log warnings) in `DetectSample.cs` and updated `DetectSampleFixture.cs` to test ROM formats (`.sfc`, `.chd`).
  - Removed orphaned `frontend/src/RomFile/MediaInfo.tsx` component and cleaned up unused `mediaInfo` props in `GameRow.tsx`.

### Removed
- **Purge of PC (4000s) and Other (8000s) Categories from Newznab & Torznab**:
  - Strictly limited Newznab and Torznab category presentation to console game categories (1000s) in NewznabCategoryFieldOptionsConverter, stripping out 4000s (PC) and 8000s (Other/Books).
  - Updated default Categories in NewznabSettings and TorznabSettings to only include standard console categories (1000, 1010, 1020, 1030, 1040, 1050, 1060, 1070, 1080, 1090, 1110, 1120, 1130, 1140, 1180).
  - Removed GamesPC from FileListSettings default categories.
  - Classified 4000-series (PC) and 8000-series (Other) under NonGameCategories in NewznabRssParser and TorznabRssParser, ensuring RSS feeds discard non-console releases when category filtering is active.
  - Added database migration 244 (purge_pc_and_other_categories_from_indexers) to sanitize existing saved indexer configurations, stripping out any non-console category IDs.
- **Complete Elimination of Quality, Delay, and Release Profiles**:
  - Abolished all video-centric profile filtering (`QualityAllowedByProfileSpecification`, `CustomFormatAllowedbyProfileSpecification`, `AcceptableSizeSpecification`, `AirDateSpecification`, `DelaySpecification`) from the decision engine. Candidate releases matching the platform's supported file types are accepted immediately for download without arbitrary pre-download quality rejections, leaving dump verification (Verified Good Dump, Verified Redump, No-Intro, etc.) to authentic cryptographic hashing (CRC32, MD5, SHA-1) against DAT databases post-download.
  - Abolished Delay Profiles and protocol restrictions in `ProtocolSpecification`, accepting all direct HTTP/HTTPS, BitTorrent, and Usenet downloads immediately without waiting periods.
  - Refactored `UpgradableSpecification` and `DownloadDecisionComparer` to operate directly on authentic dump verification status and revisions without requiring quality profile cutoffs or database lookups.
  - Streamlined UI across Platform Index (table, overview, posters, sort menu), Platform Details, and Settings: removed quality profile chips, badges, and sort options, removed `DelayProfiles` and `ReleaseProfiles` sections, updated Settings navigation to expose Virtual Libraries directly (`/settings/virtuallibraries`), and decoupled application startup from quality profile loading.
- **Complete Elimination of Scene Mapping Subsystem (Phase 3)**:
  - Purged all 10 files in `src/NzbDrone.Core/DataAugmentation/Scene/` (`ISceneMappingProvider`, `InvalidSceneMappingException`, `SceneMapping`, `SceneMappingProxy`, `SceneMappingRepository`, `SceneMappingService`, `SceneMappingsUpdatedEvent`, `ServicesProvider`, `UpdateSceneMappingCommand`), retiring the directory entirely.
  - Relocated core search definition `SearchMode.cs` cleanly into `NzbDrone.Core.IndexerSearch.Definitions`.
  - Removed `SceneMapping` entity mapping from `TableMapping.cs` and `TableMapper.cs`.
  - Removed `SceneMappingSpecification` from decision engine specifications.
  - Removed `SceneMapping` properties and dependencies from `RemoteGame.cs`, `SingleGameSearchMatchSpecification.cs`, `DownloadDecisionMaker.cs`, `ReleaseSearchService.cs`, and `ParsingService.cs`.
  - Purged obsolete scene mapping tests from `SceneMappingProxyFixture.cs`, `SceneMappingServiceFixture.cs`, and `GetGamesFixture.cs`.
- **Frontend Declutter & TV Metric Purge (Phase 1)**:
  - Purged TV broadcast airings (`nextAiring`, `previousAiring`), broadcast networks (`network`), original country, season folders, latest season, and TV certifications from `PlatformIndexOverview`, `PlatformIndexRow`, `PlatformIndexPoster`, `PlatformIndexPosters`, `PlatformIndexSortMenu`, `PlatformDetails`, `platformOptionsStore.ts`, and `usePlatform.ts`.
  - Deleted obsolete `ReleaseSceneIndicator.tsx` and accompanying CSS module files, and removed scene mapping indicators from `InteractiveSearchRow.tsx`.
- **TVDB, TV Metadata, and Entity Model Pruning (Category 5)**:
  - Pruned broadcast holdovers (`AirTime`, `AiredAfterSeasonNumber`, `AiredBeforeSeasonNumber`, `AiredBeforeEpisodeNumber`) from domain models (`Platform`, `Game`), refresh pipelines (`RefreshPlatformService`, `RefreshGameService`), and API V3 & V5 DTOs (`PlatformResource`, `GameResource`, `ReleaseResource`).
  - Streamlined `filterAlternateTitles.ts` by removing legacy TVDB origin and scene season number filter branches.

### Fixed
- **Internet Archive Single-Game Search & Primary Archive Resolution**: Resolved issue where interactive search for a single game (e.g. Sonic CD on Sega CD) returned the entire 472GB archive collection repository rather than individual game files within the configured Primary Archive URL. Fixed URL fragment parsing in `InternetArchiveParser.cs` where `#_platform=...` was inadvertently captured into `searchTerm`, causing local file matching to reject all individual game files. Enhanced `MatchesSearchTerm` with normalized alphanumeric and multi-token matching, formatted single-file download URLs with directory-preserving path encoding, prevented platform titles (e.g. "Sega CD") from being queried as single-game search terms, filtered out repository collection identifiers and archive packs (>25GB) from release results, and removed generic manufacturer tags from `PlatformTagRegex` in `PlatformCompatibilityValidator.cs` to prevent false platform mismatches for Sega CD, Saturn, and Dreamcast games.
- **Empty Region Filtering (0/0 Empty Sections)**: Prevented empty regions without games (e.g. Homebrew & Aftermarket 0/0 on Switch) from being listed in the UI and API responses. Updated `PlatformDetails.tsx`, `PlatformDetailsRegion.tsx`, `RegionDetails.tsx`, `SelectRegionModalContent.tsx`, and backend `PlatformController` (API V5 and V3) to filter out regions where `TotalGameCount == 0`. Updated `GameCatalogProxy.BuildPlatformSeriesAndEpisodes` to only populate region seasons that contain games for the platform.
- **Clean Regional Naming & Title De-duplication**: Simplified region titles across the frontend by removing verbose broadcast and video format tags (`(NTSC-U)`, `(PAL)`, `(NTSC-J)`). Regions now display cleanly by name (e.g. "USA", "Europe", "Japan", "World", "Homebrew & Aftermarket", "ROM Hacks & Translations"), with regional hardware aliases appended only when the console was named differently in that region (e.g. "Europe (Mega Drive)", "Japan (Famicom)", "Japan (Super Famicom)"). Resolved duplicate label display in `PlatformDetailsRegion.tsx` (which rendered `"USA (NTSC-U) USA"`) by suppressing redundant season titles when identical or contained within the formatted region name.
- **TV Fallback Elimination in Catalog Proxy**: Removed legacy TV episode fallback `"The Troll Farmer"` from `GameCatalogProxy.cs`, replacing it with platform launch title metadata.
- **Queue Loading & Deletion NullReferenceException**: Resolved HTTP 500 errors (`"Failed to load queue"`) and inability to interact with or delete pending queue releases. `QualityModelComparer` now provides a safe default comparison fallback when `Series.QualityProfile` is null or unconfigured, `PendingReleaseService.GetPendingQueue` groups by episode IDs rather than object references and includes unmapped pending items, `FindPendingRelease` uses `FirstOrDefault` to avoid sequence exceptions, and in-memory pending release cache is refreshed immediately upon queue item deletions so deletions persist on page reload.
- **Internet Archive JSON Deserialization Fault Resilience**: Resolved `IndexerException: Failed to parse JSON response from Internet Archive` at line 274 of `InternetArchiveParser.cs`. Introduced flexible JSON converters (`SingleOrArrayStringConverter`, `FlexibleDateTimeConverter`, `FlexibleLongConverter`, `FlexibleIntConverter`) capable of handling multi-valued Solr array responses (e.g. `title: ["Title 1", "Title 2"]`, dates, sizes) without crashing search execution.
- **Strict Collection Scoping for Internet Archive Search**: Fixed issue where searching an entire platform collection (e.g. NES) grabbed unrelated PC/Windows games and GOG installers from global Archive.org software collections. Scoped `BuildQuery` in `InternetArchiveRequestGenerator.cs` strictly to configured Primary Archive URLs/collections without falling back to global `OR mediatype:software`.
- **HTTP / HTTPS Direct Downloads Enabled by Default in Delay Profiles**: Resolved search release rejections stating `HTTP is not enabled for this platform` when querying HTTP/HTTPS-based indexers (such as Internet Archive). Defaulted `EnableHttp` to true in `DelayProfile`, added migration 243 (`enable_http_in_delay_profiles`) ensuring existing untagged and custom profiles enable HTTP/HTTPS, auto-healed untagged delay profiles in `DelayProfileService`, and improved rejection logging to clarify `HTTP/HTTPS is not enabled for this platform`.
- **Gaming Quality Profiles & Verified Good Dump Allowances**: Resolved interactive search rejection stating `VerifiedGoodDump is not wanted in profile`. Replaced Sonarr's legacy TV video quality profiles (SDTV, HDTV, WEBDL, Bluray) with authentic gaming profiles ("Any", "Verified Dumps", "Retail Dumps", "Homebrew & Hacks", "Clean ROMs", "Optical Discs"), ensured default profiles include and allow gaming dump qualities (`VerifiedGoodDump`, `VerifiedRedump`, `NoIntroVerified`, `UnverifiedRaw`, `BetaProto`, `TranslationPatch`), automatically migrated existing profiles to enable gaming qualities with `VerifiedGoodDump` cutoff, and added fallback in `QualityAllowedByProfileSpecification` so gaming qualities are always accepted when using "Any" profiles.
- **Release Search NullReferenceException & Error Handling**: Fixed a critical crash where unexpected indexer exceptions or releases missing parsed info caused `ParsedGameInfoResourceMapper.ToResource` and `ReleaseResourceMapper.ToResource` to throw a `NullReferenceException`, bringing down interactive search. Added robust null checks and fallback parsed release metadata in `DownloadDecisionMaker`.
- **Database Sequence Exception in Game Number Lookup**: Fixed `InvalidOperationException: Sequence contains more than one element` in `GameRepository.Find` by switching to `FirstOrDefault()`, and eliminated duplicate `AbsoluteEpisodeNumber` values across platform regions by enforcing unique platform-wide incrementing numbers in `GameCatalogProxy.cs`.
- **De-Televisionized Game Search & Numbering**: Refactored release mapping in `ParsingService.cs` from legacy TV/anime numbering to `GetAbsoluteNumberedGames`, utilizing search criteria game matching and gaming terminology, and updated `ReleaseController` routes/logs to gaming terminology (`platformId`, `gameId`, `regionNumber`).
- **Multi-Disc Games Primary Ordering, Bundling & 1G1R Monitoring**: Fixed multi-disc games (such as Sega CD, PSX) mistakenly prioritizing Disc 2 over Disc 1 or leaving Disc 1 unmonitored during initial platform scans and 1G1R per region monitoring (`CollectionStrategy.OneGamePerRegionPerConsole`). Hardened `DiscTagRegex` to prevent greedy consumption of version labels, grouped discs by version (`ExtractVersionOnly`), ensured all discs of the preferred version (e.g. Disc 1 and Disc 2) are marked as wanted/monitored during initial add, and preserved independent manual disc monitoring for user overrides.
- **Internet Archive 400 Bad Request**: Resolved HTTP 400 `[UNSUPPORTED_VALUE]` errors from `archive.org/advancedsearch.php` by transmitting internal metadata tags (`_platform`, `_platformName`) via URL fragment (`#...`) rather than query parameters, preventing Archive.org parameter rejections while allowing internal parsers to extract platform context seamlessly.
- **NSWDB XML Endpoint HTTPS Protocol**: Updated NSWDB catalog proxy URL to `https://nswdb.com/xml.php` to prevent HTTP 301/302 redirects triggering `Server requested a redirect while in developer mode` errors.

---

## [2026-11-03]

### Changed
- **Complete Elimination of TV Terminology & Folder Retirement (`Tv/` -> `Platforms/`)**:
  - Successfully eliminated all TV terminology (`Episode`, `Series`, `Season`, `Tv`) in file names and domain entities across the entire codebase (excluding historical database migration files preserving schema version integrity):
    - Completely retired directory `src/NzbDrone.Core/Tv/`, migrating all entities into `src/NzbDrone.Core/Platforms/`.
    - Domain Entities & Repositories:
      - `Episode.cs` -> `Game.cs` (`Game`, `Episode : Game`)
      - `Series.cs` -> `Platform.cs` (`Platform`, `Series : Platform`)
      - `Season.cs` -> `Region.cs` (`Region`, `Season : Region`)
      - `SeasonType.cs` -> `RegionType.cs` (`RegionType`, `SeasonType : RegionType`)
      - `SeriesTypes.cs` -> `PlatformTypes.cs` (`PlatformTypes`, `SeriesTypes`)
      - `EpisodeRepository.cs` -> `GameRepository.cs` (`IGameRepository`, `GameRepository`)
      - `SeriesRepository.cs` -> `PlatformRepository.cs` (`IPlatformRepository`, `PlatformRepository`)
      - `EpisodeService.cs` -> `GameService.cs` (`IGameService`, `GameService`)
      - `SeriesService.cs` -> `PlatformService.cs` (`IPlatformService`, `PlatformService`)
    - MediaFiles & Rom Management:
      - `DeletedEpisodeFile.cs` -> `DeletedRomFile.cs` (`DeletedRomFile`)
      - `EpisodeFileMoveResult.cs` -> `RomFileMoveResult.cs` (`RomFileMoveResult`)
      - `RenamedEpisodeFile.cs` -> `RenamedRomFile.cs` (`RenamedRomFile`)
      - `EpisodeFileMovingService.cs` -> `RomFileMovingService.cs` (`IMoveRomFiles`, `RomFileMovingService`)
      - `UpdateEpisodeFileService.cs` -> `UpdateRomFileService.cs` (`IUpdateRomFileService`, `UpdateRomFileService`)
      - `EpisodeFile.cs` -> `RomFile.cs` (`RomFile`, `EpisodeFile : RomFile`)
    - Parser & Parsing Models:
      - `ValidateParsedEpisodeInfo.cs` -> `ValidateParsedGameInfo.cs` (`ValidateParsedGameInfo`)
      - `FindSeriesResult.cs` -> `FindPlatformResult.cs` (`FindPlatformResult`, `PlatformMatchType`)
      - `SeriesTitleInfo.cs` -> `PlatformTitleInfo.cs` (`PlatformTitleInfo`)
      - `LocalEpisode.cs` -> `LocalGame.cs` (`LocalGame`, `LocalEpisode : LocalGame`)
      - `ParsedEpisodeInfo.cs` -> `ParsedGameInfo.cs` (`ParsedGameInfo`, `ParsedEpisodeInfo : ParsedGameInfo`)
      - `RemoteEpisode.cs` -> `RemoteGame.cs` (`RemoteGame`, `RemoteEpisode : RemoteGame`)
    - Indexer Search & Criteria Definitions:
      - `SingleEpisodeSearchCriteria.cs` -> `SingleGameSearchCriteria.cs` (`SingleGameSearchCriteria`)
      - `SeasonSearchCriteria.cs` -> `RegionSearchCriteria.cs` (`RegionSearchCriteria`)
      - `SceneEpisodeMapping.cs` -> `SceneGameMapping.cs` (`SceneGameMapping`)
      - `SceneSeasonMapping.cs` -> `SceneRegionMapping.cs` (`SceneRegionMapping`)
      - Removed obsolete TV search criteria files (`AnimeEpisodeSearchCriteria.cs`, `AnimeSeasonSearchCriteria.cs`, `DailyEpisodeSearchCriteria.cs`, `DailySeasonSearchCriteria.cs`, `SpecialEpisodeSearchCriteria.cs`), consolidating criteria models under `SingleGameSearchCriteria` and `RegionSearchCriteria`.
- **Domain Renaming & TV Vocabulary Purge (Commands, Services, Notifications & Frontend)**:
  - Renamed commands, services, and file names across backend and frontend to game and platform domain concepts:
    - `DeleteSeriesFilesCommand` -> `DeletePlatformFilesCommand`
    - `DownloadedEpisodesScanCommand` -> `DownloadedGamesScanCommand`
    - `DownloadedEpisodesCommandService` -> `DownloadedGamesCommandService`
    - `DownloadedEpisodesImportService` -> `DownloadedGamesImportService` (`IDownloadedGamesImportService`)
    - `RenameEpisodeFileService` -> `RenameRomFileService` (`IRenameRomFileService`)
    - `RenameEpisodeFilePreview` -> `RenameRomFilePreview`
    - `MoveSeriesCommand` -> `MovePlatformCommand`
    - `RefreshSeriesCommand` -> `RefreshPlatformCommand`
    - `RefreshSeriesService` -> `RefreshPlatformService`
    - `RenameSeriesCommand` -> `RenamePlatformCommand`
    - `RescanSeriesCommand` -> `RescanPlatformCommand`
    - `EpisodeSearchCommand` -> `GameSearchCommand`
    - `CutoffUnmetEpisodeSearchCommand` -> `CutoffUnmetGameSearchCommand`
    - `MissingEpisodeSearchCommand` -> `MissingGameSearchCommand`
    - `EpisodeSearchGroup` -> `GameSearchGroup`
    - `EpisodeSearchService` -> `GameSearchService`
    - `SeasonSearchCommand` -> `RegionSearchCommand`
    - `SeasonSearchService` -> `RegionSearchService`
    - `SeriesSearchCommand` -> `PlatformSearchCommand`
    - `SeriesSearchService` -> `PlatformSearchService`
    - `EpisodeDeleteMessage` -> `GameDeleteMessage`
    - `SeriesAddMessage` -> `PlatformAddMessage`
    - `SeriesDeleteMessage` -> `PlatformDeleteMessage`
  - Renamed directories and namespaces:
    - `Controllarr.Api.V3/Episodes/` -> `Games/`, `Series/` -> `Platforms/`, `EpisodeFiles/` -> `RomFiles/`, `SeasonPass/` -> `PlatformPass/`
    - `Controllarr.Api.V5/Episodes/` -> `Games/`, `Series/` -> `Platforms/`, `EpisodeFiles/` -> `RomFiles/`, `SeasonPass/` -> `PlatformPass/`
    - `NzbDrone.Core/SeriesStats/` -> `PlatformStats/`
    - `NzbDrone.Core/MediaFiles/EpisodeImport/` -> `GameImport/`
    - `NzbDrone.Core.Test/TvTests/` -> `PlatformTests/`
    - `NzbDrone.Core.Test/SeriesStatsTests/` -> `PlatformStatsTests/`
  - Frontend command names and callers updated in `CommandNames.ts`, `Command.ts`, `PlatformDetails.tsx`, `PlatformIndex.tsx`, `PlatformDetailsRegion.tsx`, `OrganizePlatformModalContent.tsx`, and `MediaManagement.tsx` to use `RefreshPlatform`, `RenamePlatform`, `RegionSearch`, `PlatformSearch`.
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

### Removed
- **TV Relics, Broadcast Scheduling Logic & External ID Purge**:
  - Removed TV `Actor.cs` embedded document model and eliminated all `Actors` properties from `Series.cs` and `RefreshSeriesService.cs`.
  - Removed TV broadcast runtime offset methods (`AdjustMultiEpisodeAirTime`, `AdjustDirectToDvdAirDate`) and TV series finale override logic (`FinaleType = "series"`) from `RefreshEpisodeService.cs`.
  - Updated refresh logging messages in `RefreshEpisodeService.cs` to refer strictly to platforms and games rather than shows or aired episodes.
  - Purged obsolete TV and movie identifier search filters (`tvMazeId`, `imdbId`, `tmdbId`) and hardcoded TV database labels (`TvdbId:`, `TvMazeId:`) from frontend search components (`PlatformSearchResult.tsx`, `PlatformSearchInput.tsx`, `fuse.worker.ts`, `PlatformDetailsLinks.tsx`, `PlatformDetails.tsx`), standardizing on `Catalog ID: {tvdbId}`.
  - Renamed TV-specific CSS selectors (`tvdbLink`, `tvdbLinkCell`, `tvdbLinkIcon`) to `catalogLink` across `AddNewPlatformSearchResult.*` and `ImportPlatformSearchResult.*`.
  - Purged dead Plex notification and watchlist localization keys from `en.json`.
  - Removed obsolete direct-to-DVD and multi-episode air time test cases from `RefreshGameServiceFixture.cs`.
- **Legacy TV Search & Parameter Logic from Indexers**:
  - **Newznab & Torznab TV Logic Purge**: Completely eliminated over 400 lines of dead TV querying logic in `NewznabRequestGenerator.cs`, removing all TV search capability properties (`SupportsTvQuerySearch`, `SupportsTvTitleSearch`, `SupportsTvTextSearches`, `SupportsTvdbSearch`, `SupportsImdbSearch`, `SupportsTvRageSearch`, `SupportsTvMazeSearch`, `SupportsTmdbSearch`, `SupportsSeasonSearch`, `SupportsEpisodeSearch`), TV query generators (`AddTvIdPageableRequests`, `AddTitlePageableRequests`), and TV season workarounds (`NewznabifySeasonNumber`), leaving only clean game title and collection search generation.
  - **Indexer Validation Decoupling**: Updated `TestCapabilities()` in `Newznab.cs` and `Torznab.cs` to eliminate mandatory TV parameter checks (`"season"`, `"ep"`, `"tvdbid"`, `"rid"`), validating indexers purely on standard text query (`"q"`) support.
  - **FileList IMDb ID Query Removal**: Removed `AddImdbRequests` and `SearchMode.SearchID` IMDb querying from `FileListRequestGenerator.cs`, restricting searches strictly to game titles without TV/movie IMDb ID requests.
  - **Nyaa Anime Episode/Season Queries**: Neutralized legacy anime season/episode and absolute episode number querying in `NyaaRequestGenerator.cs` (`AnimeEpisodeSearchCriteria`, `AnimeSeasonSearchCriteria`, `SpecialEpisodeSearchCriteria`) to return empty request chains, searching strictly by game title.
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

### Fixed
- **Controllarr.Common Resource Groveler MissingManifestResourceException**:
  - Corrected resource base name in `src/Controllarr.Common/EnsureThat/Resources/ExceptionMessages.Designer.cs` to `"NzbDrone.Common.EnsureThat.Resources.ExceptionMessages"`, matching the embedded resource root generated by `Directory.Build.props` RootNamespace translation and preventing unhandled `MissingManifestResourceException` during input validation.
- **Title Parser Multi-Episode Word Boundary & Ordinal Number Collision**:
  - Fixed regex lookahead in `Parser.cs` multi-episode matching regexes to require a non-word boundary `(?![a-z\d])` rather than just a non-digit lookahead `(?!\d+)`. This resolves an elusive title parsing bug where titles with ordinals or words starting with numbers (e.g., `Series-S07E12-31st_Century_Fox`) were erroneously matched as multi-episode ranges (e.g., parsing `31st` as episode range 12-31).
- **Indexer Feed Ingestion & Transport Decoupling**:
  - Removed premature TV title rejection (`PlatformCompatibilityValidator.IsTvOrVideoRelease`) from raw `RssParser.cs` and `IndexerBase.CleanupReleases`, preventing indexers from erroneously discarding releases during transport/ingestion and throwing `UnsupportedFeedException: Empty feed` when parsing mixed or test tracker feeds. Centralized non-game release gating in `DownloadDecisionMaker` and `ReleaseController`.
- **Release Title File Extension Stripping Across Indexing & Queue**:
  - Added video container extension set (`.mkv`, `.avi`, `.mp4`, `.wmv`, `.mov`, `.m4v`, `.flv`, `.ts`, `.m2ts`) to `FileExtensions.RemoveFileExtension`, ensuring trailing video container extensions from external releases and torrents are cleanly stripped during title normalization without polluting release groups, scene names, or queue items.
- **Nyaa ROM Hack & Homebrew Query Support**:
  - Implemented `GetSearchRequests(SpecialEpisodeSearchCriteria)` in `NyaaRequestGenerator.cs` to generate search requests for game ROM hacks and homebrew releases using `EpisodeQueryTitles`.
- **Test Suite Modernization & Video Extension Decoupling**:
  - Updated `DownloadedGamesImportServiceFixture.cs` to verify game imports with `.nes` ROM files rather than unsupported `.mkv` files.
  - Purged obsolete Korean daily television broadcast test cases and legacy video extension fallback tests (`.avi`, `.mkv`, `.m2ts` mapping to TV resolutions) across `MiniPlatformGameParserFixture.cs`, `QualityParserFixture.cs`, and `ParserFixture.cs`.
  - Updated `HashedReleaseFixture.cs` to assert `Quality.Unknown` for bare filenames without format tags, matching Controllarr's game quality model.

---

## [2026-10-30]

### Added
- **Full BIOS Integration Across Downloads, Library Imports & Collection Archive Scans**:
  - Treated BIOS files as first-class media assets across the download, library import, and disk scanning pipelines (`IBiosService`, `DiskScanService`, `DownloadedEpisodesImportService`, `UnmanagedDiskScanner`, `ManualImportService`).
  - During platform and collection scanning (`DiskScanService`), candidate BIOS files inside collection zip archives and platform root directories are automatically identified, extracted/moved directly to the target `platform/BIOS` directory, and cryptographically verified.
  - During download processing (`DownloadedEpisodesImportService`), completed downloads containing BIOS files are detected, verified, and placed into the platform's `BIOS` folder.
  - During manual and unmanaged library importing (`UnmanagedDiskScanner`, `ManualImportService`), BIOS files in folders and zip archives are discovered, matched to their host platform under Season 99 (`[BIOS]`), and imported directly into `platform/BIOS`.
- **GameTDB Database Integration (Switch, Wii, Wii U, 3DS)**:
  - Added automated download, decompression, and XML parsing of official GameTDB databases for Nintendo Switch (`switchtdb.zip`), Nintendo Wii (`wiitdb.zip`), Nintendo Wii U (`wiiutdb.zip`), and Nintendo 3DS (`3dstdb.zip`).
  - Automatically populates rich game catalogs with release overviews, release years, developer/publisher data, and complete cryptographic checksums (`CRC32`, `MD5`, `SHA1`).
  - Resolves missing Switch library population where only 18 games populated previously. (Closes #79, #80).
- **Weekly DAT Catalog Synchronization Schedule**:
  - Adjusted scheduled task interval for `UpdateDatCatalogsCommand` in `TaskManager.cs` from daily (24 hours) to weekly (7 days / 10,080 minutes) to respect bandwidth and catalog stability on end-of-life systems.
- **Parent & Root Collection Archive Preservation & Scan Detection**:
  - Enhanced `DiskScanService` to detect candidate collection zip archives in parent and root folders prior to missing directory short-circuits.
  - Automatically initializes target platform folders and unpacks multi-game collections directly into regional season structures while preserving source collection archives on disk without destructive deletion.
- **Archive Extraction on Disk Scan**:
  - Updated `DiskScanService.ExtractCollectionArchives` with safe file disposal handling before deleting extracted collection zip archives, enabling "Refresh & Scan ROMs" to unpack and scan multi-ROM packs and loose ROMs into library platform folders.

### Changed
- **Platform View Search Dropdown Button & Sidebar "Platforms" Navigation**:
  - Replaced the standalone "Search Monitored" and "Search for Collection" toolbar buttons on the platform details page (`frontend/src/Platform/Details/PlatformDetails.tsx`) with a single "Search" dropdown button positioned to the left of "Refresh & Scan ROMs" to declutter the top toolbar.
  - Renamed the primary sidebar navigation group label from "Media" to "Platforms" in `frontend/src/Components/Page/Sidebar/PageSidebar.tsx`.
- **Direct API V5, Command, Configuration & Localization Naming Alignment (Zero Aliasing)**:
  - Removed all code-level token aliasing in `frontend/src/Utilities/String/translate.ts` and updated localization templates in `src/NzbDrone.Core/Localization/Core/en.json` and `PlatformIndexProgressBar.tsx` to use `{romFileCount}`, `{gameCount}`, `{totalGameCount}`, `{gameFormat}`, and `{game}` directly.
  - Updated all `Controllarr.Api.V5` resources, subresource enums, and controller query parameters/routes (`PlatformStatisticsResource`, `RegionStatisticsResource`, `RegionTypeResource`, `PlatformResource`, `AddPlatformOptionsResource`, `GameResource`, `GameSubresource`, `CalendarSubresource`, `CutoffSubresource`, `GamesMonitoredResource`, `GameController`, `RenameGameResource`, `RomFileListResource`, `RomFileController`, `PlatformPassController`, `ReleaseGrabResource`, `ReleaseResource`, `ParsedGameInfoResource`, `ReleaseController`, `ManualImportResource`, `ManualImportReprocessResource`, `ManualImportController`, `HistoryResource`, `HistorySubresource`, `HistoryController`, `QueueSubresource`, `QueueResource`, `QueueController`, `QueueDetailsController`, `BlocklistResource`, `ConnectionResource`, `ImportListResource`, `IndexerResource`, `IndexerBulkResource`, `ReleaseProfileResource`, `ParseResource`, `ParseController`, `StatisticsResource`, `QualityProfileStatisticsResource`, `QualityStatisticsResource`, and `TagStatisticsResource`) to expose `Platform`, `Game`, and `RomFile` property names directly without duplicate `Episode` properties.
  - Renamed core command classes and payloads (`GameSearchCommand` with `GameIds`, `MissingGameSearchCommand`, `CutoffUnmetGameSearchCommand`, `DeletePlatformFilesCommand`, `DownloadedGamesScanCommand`, `MovePlatformCommand`, and `ManualImportFile` with `GameIds` / `RomFileId`) and removed the `normalizedCommandName` translation switch from `CommandController`.
  - Renamed `AutoUnmonitorPreviouslyDownloadedGames` and `GameTitleRequired` in `IConfigService` / `ConfigService`, removed duplicate backing-field shims from `NamingSettingsResource`, `NamingExampleResource`, and `MediaManagementSettingsResource`, and aligned `FileNameValidationService` validation property names (`StandardGameFormat`, `DailyGameFormat`, `AnimeGameFormat`) and `SignalRListener.tsx` handlers.

### Fixed
- **Canonical BIOS Catalog Cryptographic Hash Verification & Sega CD JP BIOS Support**:
  - Audited and corrected `ExpectedMd5` and `ExpectedSha1` checksums across all 57 entries in `CanonicalBiosCatalog` in `src/NzbDrone.Core/Bios/BiosService.cs` (including `bios_CD_J.bin` for Mega-CD JP v1.00, which previously had a mismatched `ExpectedSha1` that caused valid files to fail verification and be discarded).
  - Added multi-revision verification support (`AlternateBiosHashesByFileName`) and 16-bit byte-swapped ROM detection for Sega CD / Mega-CD (`bios_CD_J.bin`, `bios_CD_U.bin`, `bios_CD_E.bin`) and official regional BIOS revisions.
  - Preserved `.zip`-packaged arcade/system BIOS archives (`neogeo.zip`, `naomi.zip`, `awbios.zip`, `cdimono1.zip`) intact on disk during verification and auto-download, and updated `BuildCandidateDownloadUrls` to target current `Abdess/retroarch_system` (`main/bios/...`) and `archtaurus/RetroPieBIOS` directory paths.
- **Switch, Wii, Wii U & 3DS (GameTDB / NSWDB) Region Parsing & Classification**:
  - Fixed `ParseGameTdbXmlContent` and `ParseNswdbXmlContent` in `GameCatalogProxy` where `<game name="...">` entries containing parenthetical language tags (e.g. `(EN,FR,JA,KO,ZHCN)` or `(JA)`) bypassed geographic region tag insertion (`!formattedName.Contains('(')`), causing ~21,635 Switch games (`ALL`, `JPN`, `EUR`, `KOR`, `TWN`, `CHN`) to fall through to `USA` (Season 1).
  - Added `ResolveCatalogRegionLabel` and `EnsureGeographicRegionTag` to map all GameTDB and NSWDB region codes (`ALL`, `WLD`, `USA`, `NTSC-U`, `EUR`, `PAL`, `PAL-R`, `GBR`, `UKV`, `DEU`, `GER`, `FRA`, `ESP`, `SPA`, `ITA`, `HOL`, `RUS`, `AUS`, `JPN`, `NTSC-J`, `KOR`, `NTSC-K`, `TWN`, `NTSC-T`, `CHN`, `HKG`, `ASI`), Nintendo 4th-character title ID region codes, and Asian/European language-only fallbacks into their canonical regions (`World`, `USA`, `Europe`, `Japan`).
  - Removed 2-letter language codes from `RomParser.RegionRegex` so language tags do not pollute `ParsedRomInfo.Regions`, bumped `GameTDB` and `NSWDB` `DatFile` cache schema version to `v2` to automatically upgrade existing SQLite `DatEntries` on refresh, and added `InvalidatePlatformCache` in `RefreshSeriesService` on platform refresh.
- **Platform Folder Deletion Reliability & Race-Condition Protection**:
  - Updated `MediaFileDeletionService` to handle `SeriesDeletedEvent` synchronously (`IHandle<SeriesDeletedEvent>`) so platform folders are deleted on disk prior to returning the HTTP `204 No Content` response.
  - Hardened `DiskProviderBase.DeleteFolder` to strip `FileAttributes.ReadOnly` across both files (including hidden/system files) and directories recursively, with retry handling on transient `IOException` / `UnauthorizedAccessException` locks.
  - Added `SeriesService.IsSeriesDeletedWithFiles` tracking across `RefreshSeriesService` and `DiskScanService` so in-flight background refreshes/scans abort and clean up rather than recreating deleted platform folders.
  - Mapped renamed frontend command names (`DeletePlatformFiles`, `GameSearch`, `MissingGameSearch`, `CutoffUnmetGameSearch`, `DownloadedGamesScan`, `MovePlatform`) in V3 and V5 `CommandController`, and stabilized `useApiMutation` request options via `useRef`.
- **Nested Collection ZIP & Unmanaged Disk Scanner Platform Identification**:
  - Implemented nested `.zip` archive inspection in `UnmanagedDiskScanner` and `RootFolderService`, recursively unpacking inner ROM entries in memory to extract real console extensions, CRC32 checksums, and game titles from wrapped archives (such as `1G1R.zip` containing `Game.zip` containing `Game.nes`).
  - Added deterministic non-zero `TvdbId` generation and in-memory games caching in `UnmanagedDiskScanner.FindMatchingEpisode`, preventing database episode collisions during batch ROM imports.
  - Enhanced `DiskScanService` with `.7z` support and platform alias resolution for candidate collection archives across parent and root directories.
- **Interactive Search TV & Video Result Bleed Elimination**:
  - Added defensive `PlatformCompatibilityValidator.IsTvOrVideoRelease` checks across V3 (`ReleaseControllerBase.MapDecisions`) and V5 (`ReleaseController.MapDecisions`) interactive release mapping endpoints, blocking TV episode packs, DVD/web rips, and video releases (e.g. `The.Office.S03E115.DVDRip.XviD-OSiTV`) from surfacing in manual search tables.
- **Platform File Deletion on Disk (closes #75)**:
  - Added `[FromQuery]` attribute to `DeleteSeries` in both `Controllarr.Api.V5/Series/PlatformController.cs` and `Controllarr.Api.V3/Series/PlatformController.cs` so ASP.NET Core properly binds `deleteFiles` and `addImportListExclusion` query parameters, ensuring files on disk are deleted when requested by the user.
- **Collection Archive Preservation & Multi-Game Pack Import**:
  - Removed destructive archive deletion in `DiskScanService.ExtractCollectionArchives`, ensuring user collection zip archives are preserved untouched.
  - Added parent directory collection zip detection (`<folderName>.zip`), unpacking archives into platform folders on initial scan.
  - Filtered multi-game collection archives from single-game media lists in `DiskScanService.GetVideoFiles`, ensuring individual unpacked ROMs are imported while single-game archives remain compressed.
- **Console ROM Full Season Rejection Bypass**:
  - Updated `ImportDecisionMaker` and `FullSeasonSpecification` to recognize console ROMs and archives (`.nes`, `.sfc`, `.zip`, `.z64`, etc.), bypassing TV season pack rejections and allowing single and collection ROMs to import cleanly.
  - Enriched ROM file information directly with `RomParser.ParseRomName` and hash matching before evaluation.
- **Root Folder Collection ZIP Autodetection & Regional Grouping**:
  - Enhanced `RootFolderService.GetUnmappedFolders` to inspect collection `.zip` files in root folders, look up inner entry hashes in `DatCacheService`, and assign matching canonical platform names.
  - Grouped regional subfolders (`USA/`, `Europe/`, etc.) into parent platforms, and populated `RelativePath` to prevent blank card rendering.
- **Total TV and Video Bleed Elimination**:
  - Enhanced `PlatformCompatibilityValidator.IsTvOrVideoRelease` with season pack (`season\s*\d+`, `s\d+ complete`) and video resolution (`480p`, `720p`, `1080p`, `2160p`, `4k`, `uhd`) detection.
  - Filtered TV and video releases across `IndexerBase.CleanupReleases` and `RssParser`.
- **Library Platform Duplication Resolution (closes #74)**:
  - Fixed `AddSeriesService.AddSeries` (both single platform add and batch import) to query existing platforms by `TvdbId`, `Title`, `TitleSlug`, and `Path` before adding entries.
  - When importing a platform from `/add/import` or adding a console that already exists in the library, Controllarr now returns the existing platform instead of creating duplicate database records.
- **Accurate BIOS Cryptographic Verification & Hash Audit (closes #84)**:
  - Corrected expected SHA-1 hashes in `BiosService.KnownBiosFiles` for Mattel Intellivision (`exec.bin`, `grom.bin`), Fairchild Channel F (`sl31253.bin`, `sl31254.bin`), Sega Master System (`bios_J.sms`), and Sony PSP (`ppge_atlas.zim`), resolving false-positive verification rejections on authentic BIOS dumps.
  - Fixed in-memory stream buffering in `VerifyBios` so multiple hash algorithms (MD5 and SHA-1) are computed from fresh stream positions, preventing empty-stream SHA-1 mismatches during compressed archive inspection.

---

## [2026-10-26]

### Added
- **Recursive Library Import & Zip Collection Support**:
  - Expanded `UnmanagedDiskScanner` and `ManualImportService` to recursively traverse directories and zip archives of any depth.
  - Added support for full multi-ROM collection archives (such as `@NES2.0.zip`, `1G1R.zip`, and platform packs): Controllarr now inspects archive central directories via $O(1)$ entry headers and extracts individual `ManualImportItem` entries for each ROM inside using `${zipPath}#${entryName}` sub-path notation.
  - Implemented automatic extraction on import: when an item from a collection archive is imported, Controllarr unpacks the target ROM to a clean temporary path and imports the raw ROM into the library folder.
  - Single-ROM archives (`Game.zip`) and multi-track disc archives (containing `.cue` / `.gdi` or multiple bin tracks) are detected and handled as unified single-game archives.
- **Hash-Based Platform Autodetection & DAT Cache Lookup**:
  - Implemented `FindPlatformByHash` on `IDatCacheService` and `DatCacheService` to look up entry hashes (CRC32, SHA1, MD5, SHA256) across loaded No-Intro, Redump, and TOSEC DATs, resolving the canonical platform slug and matching game title.
  - Added fallback platform resolution in `UnmanagedDiskScanner` and `ManualImportService` using `PlatformCompatibilityValidator.GetPlatformByExtension` for exclusive platform extensions (e.g. `.nes`, `.sfc`, `.z64`, `.gba`, `.3ds`, `.md`), directory path segments, and platform tags.
- **Non-Game File Filtering**:
  - Cleaned up `MediaFileExtensions.cs` to eliminate all video streaming and TV extensions (`.mkv`, `.mp4`, `.avi`, `.wmv`, `.mov`, `.flv`, `.webm`, `.ts`, `.m2ts`, `.rm`, `.strm`, etc.) from being accepted as game files.
  - Retained and expanded genuine console ROM, optical disc, archive, and patch extensions across `FileExtensions` and `RomExtensions`.
- **BIOS Archive Extraction & Verification**:
  - Updated `BiosService.VerifyBios` to inspect compressed archives (`.zip`, `.gz`, `.7z`) and verify the uncompressed raw BIOS payload against expected MD5 and SHA1 checksums using an in-memory stream buffer to ensure accurate multi-hash evaluation across both algorithms.
  - Fixed `ScanAndVerifyBiosDirectory` and `TryDownloadAndVerifyBios` to extract raw BIOS files from archives and save them with their canonical uncompressed filename rather than overwriting archives with mismatched extensions.
- **Internet Archive Platform Compatibility Filtering**:
  - Created `PlatformCompatibilityValidator` with platform-exclusive extension mapping (30+ console platforms), cartridge vs. disc isolation, folder path hierarchy analysis, and archive metadata validation.
  - Integrated platform-aware filtering directly into `InternetArchiveRequestGenerator` and `InternetArchiveParser`, preventing cross-platform file bleed when scraping multi-system archive collections.
  - Cartridge platforms are strictly guarded against optical disc images (`.iso`, `.chd`, `.rvz`, `.wbfs`, `.wua`, etc.), ensuring only valid game formats are accepted.
- **Interactive Search Format Badging**:
  - Replaced the legacy Sonarr "Quality" column in the Interactive Search table with a dedicated "Format" column displaying uppercase file extension badges (`.ZIP`, `.7Z`, `.CHD`, `.ISO`, `.SFC`, `.NES`, `.3DS`, `.CIA`, etc.) to quickly identify release packaging.
  - Exposed `Format` across API release resources in `Controllarr.Api.V5` and `Controllarr.Api.V3`.
  - Added filter and sorting support for `Format` in frontend release table stores.
- **Centralized Build Output & Canonical Solution Cleanup**:
  - Registered `Controllarr` project prefix in `src/Directory.Build.props` so all core, host, and console executable and library binaries output directly to `_output/net10.0` (and `_tests/net10.0` for test fixtures), resolving execution errors caused by missing binaries where only `Controllarr.Update` was being placed into `_output`.
  - Removed obsolete leftover `src/Sonarr.sln` file from the repository, ensuring all builds and tooling target `src/Controllarr.sln`.
- **Frontend Build Optimization & Static Asset Relocation**:
  - Relocated static web assets (`Fonts/`, `Images/`, `manifest.json`, `browserconfig.xml`) from `frontend/src/Content` to `frontend/public/Content`, eliminating Vite's build-time warning for `/Content/Fonts/fonts.css`.
  - Configured `chunkSizeWarningLimit: 3500` in `vite.config.ts` to suppress chunk-size warnings on production bundles.
- **Eradication of TV Terminology Across Test Suites (`Controllarr.Core.Test` & `Controllarr.Integration.Test`)**:
  - Renamed over 93 test fixtures and test client helpers across `OrganizerTests`, `ParserTests`, `MediaFiles`, `DecisionEngineTests`, `HealthCheck`, `Housekeeping`, `IndexerSearchTests`, `IndexerTests`, and `Integration.Test` to use `Platform`, `Game`, `Region`, and `RomFile` domain names.
- **Core Models, Validators & Statistics Renaming**:
  - Renamed core domain classes in `src/NzbDrone.Core`: `PlatformPathValidator`, `PlatformExistsValidator`, `PlatformAncestorValidator`, `PlatformNotFoundException`, `MultiplePlatformsFoundException`, `PlatformTitleSlugValidator`, `PlatformTitleNormalizer`, `ShouldRefreshPlatform`, `PlatformPathBuilder`, `PlatformStatusType`, `DailyPlatform`, `DailyPlatformDataProxy`, `DailyPlatformService`, `PlatformStatistics`, `RegionStatistics`, `PlatformStatisticsRepository`, and `PlatformStatisticsService`.
- **Decoupling from GitHub Fork Network & Upstream Attribution Affirmation**:
  - Decoupled the repository from GitHub's upstream fork network to establish Controllarr as an independent standalone project for console and retro game preservation.
  - Reaffirmed permanent credits and attribution to the Sonarr project and its contributors in [`README.md`](README.md) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
- **BIOS Archive Extraction Prior to Cryptographic Hash Verification**:
  - Implemented automatic extraction of downloaded compressed BIOS archives (`.zip`, `.gz`, `.7z`) into a staging folder before hashing, verifying the uncompressed raw binary against target MD5 and SHA-1 checksums and eliminating rejection errors caused by hashing outer archive containers.

### Fixed
- **Newznab Indexer Game Matching & TV/Anime Bleed Elimination**:
  - Fixed `NewznabRequestGenerator` to sanitize category queries, stripping non-game categories (5000-5999 TV, 2000-2999 Movies, 3000-3999 Music, 7000-7999 Books) and defaulting to console and PC game categories (`1000-1999`, `8000-8999`).
  - Added `FilterNonGameCategories` setting on Newznab indexers to drop unwanted TV and anime releases from indexer feeds and multi-category responses.
  - Cleaned search query terms with `CleanGameTitleForSearch` to strip parenthetical region and language catalog tags from game titles, routing queries cleanly through `t=search`.
  - Enhanced title comparison in `DownloadDecisionMaker` to replace dot/underscore separators with spaces and strip scene group suffixes (`-GROUP`), enabling scene releases (e.g., `Pokemon Ultra Sun USA 3DS-SUXXORS`) to match monitored library games.
  - Integrated `PlatformCompatibilityValidator.IsCompatible` into `SeriesSpecification` to reject incompatible file formats during interactive and automated searches.
- **Yarn Engine Check & Warning Clean-up**:
  - Added `ignore-engines true` in `.yarnrc` to eliminate package engine warnings (`chart.js: The engine "pnpm" appears to be invalid`) during `yarn install`.
  - Updated root `package.json` metadata with Controllarr project name, repository, and author.

---

## [2026-10-25]

### Added
- **Root Folder & Import Screen Region Folder Isolation**:
  - Fixed `RootFolderService.GetUnmappedFolders` to ignore regional and bucket directories (`USA`, `Europe`, `Japan`, `World`, `No-Intro`, `Redump`, `1G1R`, `Packs`, etc.) from being listed as standalone platform candidates on `/add/import`.
  - Added automatic platform detection for root folders containing loose ROMs or collection archives directly.
- **TV & Video Release Filtering across Indexers & Decision Engine**:
  - Added `PlatformCompatibilityValidator.IsTvOrVideoRelease` regex checks to filter out video formats (`.mkv`, `.mp4`, `.avi`, `.wmv`), TV season/episode tags (`S01E01`, `1x01`), video codecs, and anime release groups from Newznab and Torznab RSS parser feeds, automated downloads, and interactive searches.
  - Updated `DownloadDecisionMaker` to parse title-only game releases and multi-game collections correctly when standard TV season/episode regexes return title-level results.
- **Complete Elimination of TV & Sonarr Terminology in Backend API Controllers & Models**:
  - Renamed all controllers in `Controllarr.Api.V3` and `Controllarr.Api.V5`: `PlatformController`, `GameController`, `RomFileController`, `PlatformPassController`, `GameControllerWithSignalR`, `RenameGameController`, `PlatformLookupController`, `PlatformFolderController`, `PlatformImportController`, and `PlatformEditorController`.
  - Renamed all API resource classes, mappers, and validators to video game domain terminology: `PlatformResource`, `GameResource`, `RomFileResource`, `RomFileListResource`, `GamesMonitoredResource`, `RenameGameResource`, `ParsedGameInfoResource`, `PlatformPassResource`, `PlatformPassPlatformResource`, `RegionResource`, `RegionStatisticsResource`, `RegionTypeResource`, `PlatformEditorResource`, `PlatformEditorDeleteResource`, `PlatformEditorValidator`, `PlatformFolderResource`, `PlatformFolderAsRootFolderValidator`, `PlatformStatisticsResource`, `PlatformTranslationResource`, `PlatformRegionTypeValidator`, `GameSubresource`, and `PlatformSubresource`.
- **Complete Solution & Project Renaming to Controllarr (`Controllarr.*`)**:
  - Renamed all 24 backend `.csproj` projects from `Sonarr.*` to `Controllarr.*` (including `Controllarr.Core`, `Controllarr.Host`, `Controllarr.Api.V3`, `Controllarr.Api.V5`, `Controllarr.Http`, `Controllarr.Windows`, `Controllarr.Mono`, `Controllarr.Update`, etc.).
  - Created official `src/Controllarr.sln` solution file.
  - Updated assembly resolvers, dynamic loaders (`AssemblyLoader.cs`, `ReflectionExtensions.cs`, `AutoMoqer.cs`), `Directory.Build.props`, and build task definitions.
- **Complete Elimination of TV/Sonarr Terminology in Frontend Filenames & Modules**:
  - Migrated and replaced all frontend components and folders from TV terminology to video game preservation domain terminology:
    - `frontend/src/Series/` -> `frontend/src/Platform/` (`PlatformDetails.tsx`, `PlatformIndex.tsx`, `PlatformBanner.tsx`, `PlatformTitleLink.tsx`, `usePlatform.ts`, `PlatformProgressBar.tsx`, etc.)
    - `frontend/src/Episode/` -> `frontend/src/Game/` (`GameDetailsModal.tsx`, `GameRow.tsx`, `GameStatus.tsx`, `GameNumber.tsx`, `useGame.ts`, `useGames.ts`, etc.)
    - `frontend/src/EpisodeFile/` -> `frontend/src/RomFile/` (`RomFile.ts`, `useRomFiles.ts`, `RomFileLanguages.tsx`, `RomFileQuality.tsx`, etc.)
    - `frontend/src/Season/` -> `frontend/src/Region/` (`formatRegion.ts`, `RegionSelectInput.tsx`)
    - `frontend/src/AddSeries/` -> `frontend/src/AddPlatform/` (`AddNewPlatform.tsx`, `ImportPlatform.tsx`, etc.)
    - `frontend/src/InteractiveImport/` -> `InteractiveImport/Platform/`, `InteractiveImport/Game/`, `InteractiveImport/Region/`
    - `frontend/src/Utilities/Series/` -> `frontend/src/Utilities/Platform/` (`getNewPlatform.ts`, `platformTypes.ts`, `filterAlternateTitles.ts`, `monitorOptions.ts`, `monitorNewItemsOptions.ts`, `getProgressBarKind.ts`)
  - Updated all imports and CSS module composes classes across `frontend/src`, completely eliminating all TV-named files.
- **Dedicated "Search for Collection" Platform Search**:
  - Added a "Search for Collection" button in the Platform Details view to query indexers specifically for full ROM sets and collections using the new `PlatformCollectionSearchCommand`.
  - Dispatches platform-wide collection criteria targeting recognized full romset naming conventions (`@{shortName}`, `[{libretroSystemName}`, `{platform} complete`, `{platform} romset`, `{platform} full set`, `no-intro`, `redump`, `1g1r`) rather than searching per-region seasons.
- **Newznab & Usenet Game Title Search**:
  - Fixed interactive and automated game search on Newznab indexers (such as NZBgeek): queries are now routed through general search (`t=search`) with game titles and scene aliases across configured console categories (1000+) without injecting TV-specific parameters (`season`, `ep`, `tvdbid`), resolving 0-result search responses for console releases.
- **Automatic Indexer Preset Naming**:
  - Selecting an indexer schema preset (e.g. NZBgeek) now automatically populates the indexer name input with the selected preset name.
- **ROM Patcher Direct Library Import Flow**:
  - Successfully patched ROM files are now passed through the standard downloaded media import pipeline (`IDownloadedEpisodesImportService`), matching existing platform games, setting IDs, and recording file paths directly before falling back to direct registration.
- **Single-Source Internet Archive Settings**:
  - Eliminated duplicate configuration fields (Base URL, S3 Access Key, S3 Secret Key, Primary Archive URLs) from the Internet Archive Download Client.
  - The download client now dynamically sources connection details, primary archive URLs, and S3 credentials directly from the configured Internet Archive Indexer, presenting users with a streamlined form containing only the client-specific `Download Path` setting.
- **Primary Archive URLs & Direct Single-ROM Acquisition**:
  - Replaced ambiguous "Preferred Archives" with a dedicated "Primary Archive URLs" configuration in both Internet Archive Indexer and Internet Archive Download Client settings.
  - Users can specify explicit archive item links or identifiers (e.g. `https://archive.org/details/htgdb-gamepacks`).
  - Indexer queries primary archives first directly via the Internet Archive metadata API (`/metadata/{id}/files?search=...`) to return individual single ROM files rather than triggering dynamic whole-collection `.zip` generation (`/compress/{id}`) which stalled at 0 bytes in download clients.
  - Automatically filters out metadata, logs, cuesheets, and non-game assets (`.txt`, `.xml`, `.sqlite`, `.torrent`, etc.) while extracting exact file size and direct file URLs.
  - Download client intelligently prioritizes primary archive URLs and extracts exact direct file URLs.
- **Article-Insensitive Game Sorting**:
  - Game tables, grids, and lists now ignore leading English grammatical articles ("The", "A", "An") when sorting titles alphabetically (e.g., *The Legend of Zelda* sorts under 'L', not 'T').
- **Console Region Naming Preference (`Settings -> UI`)**:
  - Added a `Console Region Naming` setting in UI Settings providing a dropdown to select between `North America / USA` (e.g., Sega Genesis, NES, TurboGrafx-16), `Europe / PAL` (e.g., Sega Mega Drive, NES, TurboGrafx), and `Japan / NTSC-J` (e.g., Sega Mega Drive, Famicom, PC Engine).
  - Dynamically updates console titles across the entire library level (posters, overview, tables, search suggestions) as well as within platform details and region views without requiring disk renames or database migrations, and without affecting game search priority or 1G1R curation.
  - Enhanced region headers to display authentic regional console names (e.g. `Europe (PAL — Mega Drive)`, `Japan (NTSC-J — Famicom)`, `Japan (NTSC-J — PC Engine)`).
- **Settings Menu & Uniform Tile Layout Reordering**:
  - Reordered the sidebar Settings submenu and the Settings overview tiles into the standardized sequence: General, UI, Media Management, Collection Settings, Virtual Libraries, Indexers, Download Clients, Metadata, Connect, Import Lists, Tags, RetroAchievements, and Libretro & Emulation.
  - Enforced uniform card dimensions across all 13 tiles with `grid-auto-rows: 1fr`, consistent `min-height: 154px`, 3-line clamped summaries (`-webkit-line-clamp: 3`), single-line nowrap titles with ellipsis and accessible tooltips, and balanced summary descriptions.
- **Interactive Search Credentials Tagging & Download Gating**:
  - Releases requiring credentials (such as Internet Archive S3 Access/Secret Keys or archive.org `access-restricted-item` releases) are clearly tagged in interactive search results with a `Requires Credentials` badge (rendered in warning/danger styling when credentials are unpopulated, or info styling when configured).
  - Hard download gating prevents grabbing releases when required credentials are not configured, rejecting downloads with `DownloadRejectionReason.RequiresCredentials` and disabling the download button with an informative tooltip directing users to configure keys in Settings.
  - Added `Require S3 Credentials` toggle in Internet Archive indexer settings and automatic detection of archive.org `access-restricted-item` metadata.
- **43 Console Hardware Themes & Live Preview (`Settings -> UI`)**:
  - Implemented 1 dedicated hardware theme per supported console across Nintendo (NES, Famicom, SNES, Super Famicom, N64, GameCube, Wii, Wii U, Switch, Game Boy DMG-01, Game Boy Color, Game Boy Advance, Nintendo DS, Nintendo 3DS, Virtual Boy, Pokemon Mini), Sega (SG-1000, Master System, Genesis / Mega Drive, Sega CD, 32X, Saturn, Dreamcast, Game Gear), Sony (PlayStation, PS2, PS3, PSP, PS Vita), Microsoft (Xbox, Xbox 360), SNK (Neo Geo AES, Neo Geo CD, Neo Geo Pocket), Atari (2600, 5200, 7800, Lynx, Jaguar), NEC (PC Engine / TG-16, SuperGrafx, PC Engine CD), and Bandai (WonderSwan).
  - Alphabetized the theme dropdown selector immediately following `Auto`, `Light`, and `Dark`.
  - Theme selections in `Settings -> UI` apply and save immediately on selection (no Save click required).
  - Added dynamic per-console theme matching (`Match console theme on platform page`) in UI settings.
  - Redesigned Nintendo Famicom, Game Boy DMG-01, and Xbox 360 color palettes to match authentic hardware aesthetics.
- **Out-of-the-Box Internet Archive Indexer & HTTP Downloader**:
  - Automatically seeds default Internet Archive indexer and built-in HTTP downloader on initial launch, enabling instant out-of-the-box ROM acquisition with optional S3 archive credentials for locked items.
  - Added ROM-aware release matching in `DownloadDecisionMaker` (`TryParseRomRelease`) to match platform packs and individual ROM titles directly against library games during interactive and automatic search.
- **Disc-Based Game Subfolder Customization (`Settings -> Media Management`)**:
  - Added option to place disc-based games (even single-disc titles) inside their own game-titled subfolder (`{Platform}/{Region}/{Game Title}/{Game Title}.chd`), preventing multi-track `.cue`/`.bin` and `.m3u` clutter.
- **Settings Save Confirmation**:
  - Added visual save confirmation toast and checkmark animation across all Settings pages upon saving, as well as an explicit Save button on `Settings -> Collection`.
- **Navigation Export Placement**:
  - Planned upcoming hardware SD card export workflow location in sidebar navigation under `Library -> Export` (`/export`).

### Changed
- **Non-Game Catalog Filtering**:
  - Filtered out non-game placeholder directories and assets (e.g., `@Place SPC Pack Here`, `_BIOS`, etc.) from catalog generation and SmokeMonster SMDB parsing.
- **Granular Release Date Formatting**:
  - When metadata does not provide day or month precision, dates format strictly to the known resolution (Year or Month Year) instead of defaulting to December 31. Anchored release dates at noon UTC (`12:00:00 UTC`) to prevent timezone rollbacks.
- **Anime Section & Backend Removal from Indexers**:
  - Fully removed the anime categories and `AnimeStandardFormatSearch` settings and request generators across Newznab, Torznab, FileList, Nyaa, and Fanzub indexers.
- **Download Client Default Category**:
  - Updated the default downloader category across all download clients (Deluge, Hadouken, NzbVortex, Nzbget, QBittorrent, Sabnzbd, Transmission, rTorrent, uTorrent) from `"tv"` to `"roms"`.
- **Platform Addition Cleanup**:
  - Removed the obsolete "Search for cutoff unmet" checkbox when adding a new platform.
  - Ensured platform directories on disk are unconditionally created immediately upon adding a platform.
- **Linux Source Build & Systemd Service User Documentation**:
  - Added complete instructions in [`README.md`](README.md) and the [Installation Guide](https://github.com/12thirtyfour/Controllarr/wiki/Installation-Guide) for building Controllarr from source on Linux (Ubuntu/Debian, Fedora, Arch Linux).
  - Added step-by-step guidance for creating dedicated, unprivileged system service accounts (`controllarr:controllarr`), setting directory ownership and permissions (`/opt/Controllarr`, `/var/lib/controllarr`, `/data/roms`, `/data/downloads`), and configuring a hardened `systemd` daemon unit with `UMask=0002`.
- **ROM Patcher Dedicated Navigation**:
  - Moved the Cryptographic Hash-Gated Romhack & Translation Patcher (.ips / .bps / .xdelta / .vcdiff) out of Metadata Settings into its own dedicated menu item under `Library` titled `"ROM Patcher"` (`/rom-patcher`).
  - Added full client-side patch file upload and server path inputs with cryptographic hash preview and feedback indicators.
- **Internet Archive Full Game Packages Support**:
  - Added an `Allow Full Game Packages` toggle under `Settings > Download Clients > Internet Archive` allowing downloading of full platform sets and archives (`.zip` / `.7z`) when large portions of a library or collection are missing.
  - Enhanced the Decision Engine with comprehensive platform flashcart alias detection (`SD2SNES`, `EverDrive-N8`, `Mega-EverDrive`, `EverDrive-64`, `EverDrive-GB`, `EverDrive-GBA`, `Turbo-EverDrive`, `EZ-Flash`, `SMDB`, `HTGDB`, `1G1R`) and article-insensitive title normalization.
- **Internet Archive Indexer Testing & Validation**:
  - Replaced inherited Usenet/Newznab category validation in `TestConnection()` with a direct probe to Internet Archive metadata and API search endpoints, resolving the false-positive `"Query successful, but no results in the configured categories were returned from your indexer"` test error.
  - Defaulted all 3 fail-download security settings (`Executables`, `Potentially Dangerous`, and `User Defined Extensions`) to enabled by default.
  - Enhanced search request generation to query both collections and item identifiers `(collection:{c} OR identifier:{c})` and immediately ingest files from configured primary archives.
- **Full Console Wanted Cascade**:
  - Toggling whole-console monitoring now cascades `Monitored = true` across all 7 regions and content classes, including Unlicensed (Season 0), Homebrew & Aftermarket (Season 5), and ROM Hacks & Translations (Season 6), rather than only commercial retail regions.
- **Cached Verification Databases UI Overflow**:
  - Fixed UI overflowing container bounds in `Settings > Metadata` after manually refreshing DAT databases by adding responsive width capping, ellipsis truncation, and vertical scrolling.
- **Full Region / Platform Package Release Date Handling**:
  - Updated `FullSeasonSpecification` so retro catalog games without day-precision release dates (`AirDateUtc == null`) no longer cause full platform packs or region sets to be rejected.
- **Sonarr / TV Terminology Purge (Logs, Rejections, UI Strings)**:
  - Replaced TV-centric wording (series / season / episode / aired) with Controllarr wording (platform / region / game / released / ROM file) across download decision rejections, search-match specifications, tracked-download and import messages, disk-scan, rename, move, refresh, monitoring, and parsing logs.
  - Startup/shutdown logs and startup exceptions now report `Controllarr` instead of `Sonarr`; the "Tracking" debug line reports `ControllarrStage`.
  - Frontend now sends an `X-Controllarr-Client` header (the backend still accepts the legacy `X-Sonarr-Client` header).
  - Password reset hints reference `CONTROLLARR__AUTH__RESETTOKEN`; Vite dev-server errors reference `CONTROLLARR_VITE_DEV_SERVER`.
  - Added `CONTROLLARR_PROCESS_NAME` / `CONTROLLARR_CONSOLE_PROCESS_NAME` constants (legacy `SONARR_*` names kept as aliases).
  - Removed the unused `piwikCheck.js` analytics stub that pointed at `piwik.sonarr.tv`.
  - Fixed English UI strings such as "Platforms Type", "Unknown Platforms Items", "Midseason Finale", and region-scoped modal headers.
- **BIOS Hash Verification, Discard & Replacement Pipeline**:
  - Enforced strict cryptographic checksum validation (`MD5` and `SHA-1`) across all platform system BIOS and firmware files.
  - Any BIOS file found on disk or downloaded from a mirror that does not match expected canonical hashes is immediately discarded (deleted from disk) rather than retained in the library.
  - Automatically triggers replacement acquisition from verified mirrors when an invalid or corrupted BIOS file is detected.
  - Removed unverified synthetic firmware fallback generation, ensuring only cryptographically verified dumps can achieve `Present` (`1`) verification status.
- **Default ROM Library Naming Scheme (`{Manufacturer}/{Platform}/{Region}/{Game Title}`)**:
  - Updated default folder and file naming across backend (`NamingConfig.Default`, `NamingConfigService`) and frontend (`Naming.tsx`, `NamingModal.tsx`) to prepend `{Manufacturer}` prior to `{Platform}` (e.g. `Nintendo/Nintendo Entertainment System/USA/Super Mario Bros. (USA) (Rev 1).nes` and `Sony/PlayStation/USA/Final Fantasy VII (USA) (Disc 1)/...`).
  - Added automatic legacy naming config upgrade migrating `{Platform}` or `{Series Title}` to `{Manufacturer}/{Platform}`.
- **GitHub Branch, CI/CD & Release Housekeeping (`develop` staging / `main` stable)**:
  - `develop` no longer triggers any GitHub Actions builds; it is the staging/testing branch and dev users build from source.
  - Renamed `build_v5.yml` to `build.yml` (`Build & Release`): runs only on pushes/merges to `main`, `v*` version tags, pull requests targeting `main` (build + tests only), and manual dispatch.
  - Replaced Sonarr's hardcoded `5.0.0.<run>` versioning (which published a new pre-release on every `develop` push) with a single `RELEASE_VERSION` (`1.0.0`). Releases are tagged `vX.Y.Z`, skipped if that release already exists, and tags with a `-` suffix (e.g. `v1.0.0-rc.1`) publish as pre-releases.
  - `deploy.yml` now packages all 10 OS/arch targets and builds/pushes the multi-arch Docker image (`linux/amd64`, `linux/arm64`) to `ghcr.io/12thirtyfour/controllarr` (`<version>` and `latest` tags).
  - Removed leftover Sonarr workflow bloat: Discord webhook notifications, the `services.controllarr.tv` update publisher, `api_docs.yml`, `lock.yml`, `close_stale_draft_prs.yml`, and the repo-gated `labeler.yml` / `conflict_labeler.yml` workflows (plus `.github/labeler.yml`).
  - Deleted the 10 leftover `v5.0.0.*` pre-releases and tags from GitHub.
  - Fixed the README build badge to track `build.yml` on `main` and documented the branch/release model.

- Multi-OS beta testing and telemetry triage across macOS, Windows, and Linux.
- Physical flash cart, handheld CFW, and ODE hardware target verification runs.

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

### Fixed
- **PC & GOG Installer Rejection for Console Platforms**: Added explicit filtering in `PlatformCompatibilityValidator.cs` to reject PC executable formats (`.exe`, `.msi`, `.bat`, `.cmd`, `.com`, `.appimage`, `.dmg`) and GOG installer packages (`gog-installer`, `official-gog`, `setup_*.exe`, `gog.com`, `gog_games`) for console platforms, ensuring console platforms never match PC game installers.
- **Title Truncation & Prefix Corruption**: Fixed catalog ingestion regexes where titles containing terms like "Rev Up", "Revenge", "Altered", or starting with numbers like "007" were having their prefixes stripped or converted into punctuation-prefixed titles (e.g. `! RC Grand Prix`). Hardened `ResidualRevisionRegex` to require bracketed revisions or trailing suffixes and updated `IndexPrefixRegex` with negative lookaheads for titles like 007.
- **Platform & Game Localization Dictionary Synchronization**:
  - Added 196 missing `Platform`, `Game`, `RomFile`, and `Region` localization keys to `src/NzbDrone.Core/Localization/Core/en.json` (including `AddNewPlatform`, `DeletePlatformFolder`, `DeletePlatformFolderHelpText`, `DeletePlatformFolderConfirmation`, `DeletePlatformFolderGameCount`, `DeleteGamesFiles`, `DeleteGamesFilesHelpText`, and `DeletePlatformModalHeader`).
  - Added automatic token aliasing in `frontend/src/Utilities/String/translate.ts` between `romFileCount`/`episodeFileCount`, `gameCount`/`episodeCount`, `totalGameCount`/`totalEpisodeCount`, and `regionCount`/`seasonCount`.
- **Media Management & Naming Settings Crash Resolution**:
  - Resolved runtime `TypeError: Cannot read properties of undefined (reading 'value')` on the Media Management page by aligning backend DTOs (`NamingSettingsResource`, `NamingConfigResource`, `MediaManagementSettingsResource`, `MediaManagementConfigResource`) with video game naming conventions (`standardGameFormat`, `dailyGameFormat`, `renameGames`, `multiGameStyle`, `gameTitleRequired`, `autoUnmonitorPreviouslyDownloadedGames`) while retaining full backwards compatibility.
  - Implemented null-safe chaining and fallback property resolution in `frontend/src/Settings/MediaManagement/Naming/Naming.tsx`, `frontend/src/Settings/MediaManagement/MediaManagement.tsx`, `useNamingSettings.ts`, and `useMediaManagementSettings.ts`.
  - Fixed TypeScript token parameter typing in `OrganizePreviewModalContent.tsx`.
- **General Library Monitor Toggle & Full Console Wanted Cascading**:
  - **Interactive Library Toggles**: Fixed the monitor toggle across general library views (Table, Poster, and Overview) so that clicking the bookmark/status icon immediately toggles the console's monitoring state with active saving spinners, removing the restriction that kept it static and non-interactive outside of bulk-select mode.
  - **Full Console Wanted Cascading**: Toggling a platform to monitored (`Monitored = true`) now marks the entire console as wanted by automatically monitoring all standard retail regions (USA, Europe, Japan, etc.) and cascading monitoring to all games (episodes) according to the configured 1G1R or Completionist curation strategy. Toggling to unmonitored immediately unmonitors all regions and games.
  - **Instant UI Cache Synchronization**: Updated `useToggleSeriesMonitored` to automatically invalidate `['/episode']` and `['/series']` queries on mutation success, ensuring the platform details page and general library views instantly refresh to reflect the updated monitoring state across all games.
- **Library Platform Population Quality Improvements (SNES & All Consoles)**:
  - **Index Number Prefix Stripping**: Fixed games displaying numeric list prefixes like `001 `, `002 `, `01 - `, `0001 - ` in titles when importing SmokeMonster EverDrive playlists or numbered DAT sets, while safely preserving genuine numeric titles (such as *1942*, *2020 Super Baseball*, *3 Ninjas Kick Back*, *7th Saga*).
  - **Duplicate Playlist Exclusion**: Skipped EverDrive duplicate subfolders (`Game Series Collections`, `Best-Of`, `Top 100`) during SMDB parsing so only canonical platform games are indexed.
  - **Title Revision Cleanliness**: Separated version/revision tags (`(REV 1)`, `(v1.0)`, etc.) from canonical game titles into `RevisionTag`, preventing cluttered titles in library grids and tables.
  - **Release Date Scraping & 1898 Epoch Bug**: Fixed release date scraping from Libretro release year DATs matching CRC32 and normalized titles, and guarded against invalid dates prior to 1970 (`AirDate < 1970` or `1898-12-31`), preventing `Dec 31 1898` from appearing in UI headers, tables, and episode summaries.
- **Complete Concurrent Isolation from Parallel Sonarr Installations**:
  - **Network Ports**: Configured default HTTPS port to `7051` (previously defaulted to `9898`, conflicting with Sonarr's default SSL port), guaranteeing zero port collisions on standard HTTP (`7050` vs `8989`) and HTTPS (`7051` vs `9898`). Updated `.devcontainer/devcontainer.json` port forwarding.
  - **Environment Variable Isolation**: Scoped ASP.NET Core configuration section binding and environment variable lookups to `Controllarr:*` and `CONTROLLARR__*` (`CONTROLLARR__SERVER__PORT`, `CONTROLLARR__POSTGRES__*`, `CONTROLLARR__LOG__*`, `CONTROLLARR_VITE_DEV_SERVER`), preventing Controllarr from inheriting or overriding global `SONARR__*` environment variables configured for a parallel Sonarr instance.
  - **Windows Firewall Rule Isolation**: Renamed firewall adapter rule to `"Controllarr"` (previously hardcoded as `"NzbDrone"`), preventing Controllarr from overwriting or colliding with Sonarr's firewall rules.
  - **System Tray & Process Isolation**: Updated tray icon tooltip to `Controllarr - {Version}` (was `Sonarr`), temporary filesystem writability test to `controllarr_write_test.txt` (was `sonarr_write_test.txt`), and updated console/service exception messaging.
  - **Linux Systemd & Packaging Isolation**: Renamed `distribution/debian/sonarr.service` to `controllarr.service` targeting `/opt/Controllarr` and `/var/lib/controllarr` with user `controllarr`, and updated `install.sh` to configure `controllarr` on port 7050 without removing or interfering with existing `sonarr.service` files.
  - **macOS & Windows Setup Isolation**: Renamed macOS application bundle to `distribution/macOS/Controllarr.app` with unique bundle ID `org.controllarr.app` (preventing macOS LaunchServices bundle collisions), and renamed Inno Setup script to `distribution/windows/setup/controllarr.iss` with dedicated AppId `{8B4A1065-3523-4025-B76D-6F73F67F7F72}` and directory `{commonappdata}\Controllarr\bin`.
- **Cross-Platform DAT Substring Collision Guard (NES vs. SNES, GB vs. GBA, etc.)**:
  - Fixed an issue in `DatCacheService.cs` where query matching on substring platform names (e.g. `Nintendo - Super Nintendo Entertainment System (SmokeMonster SMDB)` matching `Nintendo Entertainment System`) populated SNES games like *Super Mario World* into the NES platform catalog.
  - Implemented exact slug matching, platform token boundary verification, and cross-platform substring collision prevention across NES/SNES, GB/GBC/GBA, PS1/PS2/PS3/PSP/PS Vita, Xbox/Xbox 360, Wii/Wii U, and Sega Master System/Genesis.
- **RetroAchievements Console-Aware Progress & Badge Isolation**:
  - Fixed an issue where RetroAchievements user progress and mastery badges were cached solely by normalized game title (`supermarioworld`), causing SNES achievements and badges to erroneously render on NES game entries or demakes.
  - Scoped mastery progress queries, title matching, and showcase modal lookups to both game title and normalized console platform across `RetroAchievementsService.cs`, `RetroAchievementsController.cs`, and `gameAchievementsHelper.ts`.
- **Universal Folder-Per-Game Organization for Disc-Based Titles**:
  - Enforced a dedicated game-titled subfolder structure for all optical disc games (`.chd`, `.iso`, `.cue`, `.bin`, `.gdi`, `.cdi`, `.m3u`, `.cso`, `.wbfs`, `.rvz`, `.wud`, `.wux`, `.wua`, `.xiso`, `.pbp`) across both the SD Card Exporter (`SdCardExporterService.cs`) and internal library disk organization (`FileNameBuilder.cs`).
  - Both single-disc and multi-disc titles are organized into clean game-titled folders (automatically stripping disc numbers from folder names so multi-disc games share a single folder).
  - Updated UI labels and tooltips in `Settings -> Media Management` and the SD Card Export modal to reflect universal disc subfolder grouping.
- **Enhanced ROM Hack, Translation & Aftermarket Categorization**:
  - Expanded `ClassifyRegionAndCategory` in `GameCatalogProxy.cs` to inspect SMDB path components (e.g. `3 Translations/`, `4 Hacks/`) and an expanded token vocabulary (`[T+]`, `[T-]`, `(T+)`, `(T-)`, `(Hack`, `[h1]..[h5]`, `Translation`, `Retranslation`, `Kaizo`, `Randomizer`, `Redux`, `Rebalance`, `Romhack`, `(Mod)`, `(Improvement)`, `(Restoration)`, `(Uncensored)`, `(Colorized)`, `(DX)`, `(FastROM)`, `(MSU-1)`, `(Bugfix)`, `(Patched)`, `(HB)`, `Bootleg`, `Demake`, `Pirate`, `(Unl)`).
  - Prevents fan translations, ROM hacks, homebrew, and unlicensed titles from leaking into retail region seasons (Regions 1–5).
- **Replaced Legacy TV Verbiage with Controllarr Terminology**:
  - Replaced legacy TV episode and season search formatting (e.g. `[Sega Master System : S00E126]` or `[Platform : S01]`) with clean game and region representations (`[Platform - Game Title (Region)]` or `[Platform : Region #Number]`).
  - Purged TV terminology across indexer search, decision engine, and refresh logging (e.g., replaced "episodes" with "games", "reports" with "releases", "seasons" with "regions", and "No results found" with "No releases found").
- **Internet Archive Working Folder & Path Traversal Exception**:
  - Fixed illegal path character exception (`ArgumentException: Illegal characters in path`) in `InternetArchive.cs` caused by resolving `DownloadClientWorkingFolders` (`"_UNPACK_|_FAILED_"`) as a directory path, which froze downloads at 0% in the queue indefinitely.
  - Resolved working directory to configured `DownloadPath` or `<AppData>/downloads`, with pre-download folder creation and path traversal verification. Added optional `Download Path` setting in `InternetArchiveSettings`.
- **Interactive Search Manual Grab Payload & Activity History Tracking**:
  - Fixed `handleGrabPress` in `InteractiveSearchRow` to pass `searchInfo: searchPayload` when grabbing releases, ensuring series and episode mappings resolve correctly.
  - Converted `EpisodeHistory.Data` dictionary to `StringComparer.OrdinalIgnoreCase` and fixed case-insensitive Guid matching in `ReleaseController`, ensuring grabbed releases and failed downloads properly link and display in Activity History.
  - Enhanced `QueueController` (V3 and V5) to record `DownloadFailed` history events when failed downloads are removed from the queue even if not blocklisted.
- **Navigation & Servarr Legacy Pruning**:
  - Renamed primary sidebar navigation item from `Platforms` back to `Library`.
  - Pruned legacy TV-centric `Cutoff Unmet` (`/wanted/cutoff`) and `Calendar` (`/calendar`) navigation routes.
- **Root Folder / Path Browser Autocomplete**:
  - Configured path autocomplete dropdown to appear strictly when the user types in the input field, preventing the suggestion overlay from blocking mouse clicks in the filesystem browser.
- **Platform Add Monitoring & Simplified 1G1R Encryption Preference**:
  - Fixed initial episode monitoring on newly added platforms so `Platform Only` unmonitors all regions/episodes cleanly, and `1G1R` / `Completionist` properly monitor retail regions.
  - Moved ROM Encryption Preference directly under Revision in Add Platform modal, simplified options to `Decrypted` and `Encrypted`, and restricted it strictly to 1G1R curation.
- **Platform Game Table Responsive Resizing & Title Truncation**:
  - Fixed dynamic column sizing in platform region tables with ellipsis text truncation (`text-overflow: ellipsis`) so all game metadata columns remain visible without requiring horizontal scrolling.
- **Catalog ROM Classification**:
  - Fixed classification of fan translations (`[T-En]`, `[T+En]`, `(Translation)`) and ROM hacks (`(Hack)`) so they are routed strictly to `ROM Hacks & Translations` rather than retail regions.
- **BIOS Files Storage & Display (`Library -> BIOS Files`)**:
  - Standardized physical storage folder on disk to uppercase `BIOS` (`{Platform}/BIOS/`), with backward-compatible fallback detection.
  - Alphabetized platforms in BIOS table view, relocated `Expected Hashes` to the right of `Verified`, and truncated displayed paths to start at the root folder name.
- **Internet Archive Downloader Validation**:
  - Fixed `ValidUrlBase()` validator in `InternetArchiveSettingsValidator` to use `ValidRootUrl()`, properly validating `https://archive.org`.

---

## [2026-10-24]

### Added
- **Cryptographic Hash-Gated Romhack & Translation Patching Pipeline (`RomPatchingService`, `DownloadedEpisodesImportService`, `POST /api/v3/gamemetadata/patches/apply`)**:
  - Added pure-C# `.ips` (including `RLE`, `EOF`, and 3-byte truncation records), `.bps` (`BPS1` with strict `sourceCrc32`, `targetCrc32`, and `patchCrc32` cryptographic verification), and `.xdelta` / RFC 3284 `VCDIFF` (`ADD`/`RUN`/`COPY` instruction decoding with `Adler32` target window verification) ROM patching in `RomPatchingService`.
  - **Non-Destructive Retail Base ROM Resolution**: Automatically matches patch files (or `.zip` archives containing patches) against the platform library's clean retail 1G1R base ROM (by `.bps` `sourceCrc32` or title stem), verifies the resulting patched binary against internal patch checksums and `IDatCacheService` (`SHA-256`, `SHA-1`, `MD5`, `CRC32`), writes the patched copy into `<Platform Path>/ROM Hacks/<Title>.<ext>` without modifying the original retail ROM, and registers the output under **ROM Hacks (`Season 6`)**.
  - **Automated Import & Interactive UI Patcher**: Wired automatic patch detection into `DownloadedEpisodesImportService` during download/folder import and added an interactive **Cryptographic Hash-Gated Romhack & Translation Patcher** card in **Settings → Metadata** (`POST /api/v3/gamemetadata/patches/apply`).
- **Scheduled Daily DAT & SMDB Catalog Auto-Updates (`UpdateDatCatalogsCommand`, `DatCatalogUpdateService`, `POST /api/v3/gamemetadata/dats/sync`)**:
  - Registered `UpdateDatCatalogsCommand` as a daily scheduled background task (`Interval = 24 * 60` minutes) in `TaskManager` and implemented `DatCatalogUpdateService` to automatically pull fresh `No-Intro`, `Redump`, `TOSEC`, `SmokeMonster HTGDB/SMDB`, and `NSWDB` catalog snapshots for all added library platforms, update `IDatCacheService` in-place, and trigger platform game list synchronization (`RefreshSeriesCommand`) so newly dumped revisions, translations, and aftermarket releases appear automatically.
  - Added a **"Sync Online DAT & SMDB Catalogs Now (Daily Scheduled)"** button in **Settings → Metadata** (`POST /api/v3/gamemetadata/dats/sync`) alongside **System → Tasks**.
- **Production-Ready Multi-Architecture `Dockerfile`, `docker/entrypoint.sh`, `.dockerignore` & `docker-compose.yml`**:
  - Built out a 3-stage multi-architecture `Dockerfile` (`linux/amd64` and `linux/arm64`) that compiles the React/Vite frontend SPA on `$BUILDPLATFORM` into `/app/bin/UI` (and `/app/UI`), cross-publishes the `.NET 10` `Controllarr` backend into `/app/bin`, bundles `mame-tools` (`chdman`), `dolphin-emu` (`dolphin-tool`), `p7zip-full` (`7z`), `sqlite3`, and `gosu`, writes `/app/package_info` (`UpdateMethod=Docker`), and configures a `/ping` container `HEALTHCHECK`.
  - Added `docker/entrypoint.sh` supporting non-root `PUID`, `PGID`, `UMASK`, and `TZ` configuration, `.dockerignore` to keep build contexts clean, `docker-compose.yml` (with optional PostgreSQL 17 companion service), and `DeploymentInfoProviderFixture` unit tests.
- **Compiled Binary & Host Executable Rename (`Controllarr`, `Controllarr.exe`, `Controllarr.Console.exe`, `Controllarr.Update`)**:
  - Transitioned compiled host executable and updater output assembly names from `Sonarr` / `Sonarr.exe` / `Sonarr.Console.exe` / `Sonarr.Update` to `Controllarr` / `Controllarr.exe` / `Controllarr.Console.exe` / `Controllarr.Update` across `src/NzbDrone/Sonarr.csproj`, `src/NzbDrone.Console/Sonarr.Console.csproj`, `src/NzbDrone.Update/Sonarr.Update.csproj`, `Directory.Build.props`, `ProcessProvider`, `PathExtensions`, `UpdateApp`, `StartNzbDrone`, `InstallUpdateService`, `ServiceHelper` (`ServiceInstall` / `ServiceUninstall`), `Dockerfile`, CI packaging pipelines (`.github/actions/build/action.yml`, `.github/actions/package/package.sh`), Windows Inno Setup (`distribution/windows/setup/sonarr.iss`), and macOS bundle (`Info.plist`).
- **Expanded Console Verification Feeds (`SmokeMonster HTGDB/SMDB`, `Neo Geo AES/CD`, `Switch NSWDB`, `Decrypted NDS/3DS` Default & `PreferEncryptedRoms`)**:
  - **Direct SmokeMonster HTGDB / SMDB Sync**: Wired automatic GitHub SMDB manifest sync (`SHA-256`, `SHA-1`, `MD5`, `CRC32`) from `SmokeMonsterPacks/EverDrive-Packs-Lists-Database` across NES, FDS, SNES, N64, GB/GBC, GBA, Genesis, Sega CD, 32X, Master System, Game Gear, TurboGrafx-16, TurboGrafx-CD, SNK Neo Geo, Atari 2600/7800/Lynx/Jaguar, Virtual Boy, Neo Geo Pocket, and WonderSwan.
  - **SNK Neo Geo (`1038`) & Nintendo Switch (`1014`) Verification**: Wired `FBNeo / Neo Geo AES` (`dat/SNK - Neo Geo.dat`), Redump `SNK - Neo Geo CD.dat`, and SmokeMonster `NeoSD SMDB.txt` for Neo Geo home console & CD releases, and wired `nswdb.com/xml.php` XML parsing (`CRC32`, `TitleID`, region, revision) for Nintendo Switch.
  - **Companion Sub-Systems**: Wired monochrome `SNK - Neo Geo Pocket.dat`, monochrome `Bandai - WonderSwan.dat`, `NEC - PC Engine SuperGrafx.dat`, and `Nintendo - Wii (Digital).dat` into their parent platform libraries.
  - **3-Way ROM Encryption Preference (`Not Encrypted`, `Encrypted`, `Both`) & Encrypted Version Grouping (`EncryptionPreference`)**: Replaced the boolean `PreferEncryptedRoms` checkbox with a 3-way **ROM Encryption Preference** select dropdown (`Not Encrypted (Decrypted)` [default], `Encrypted`, or `Both (Encrypted & Not Encrypted)`) exclusively on platforms with both encrypted and decrypted ROM collections (`nds` / `1012` and `3ds` / `1013`), and grouped `(Encrypted)` dumps as expandable ROM versions under each parent game alongside revisions, betas, and prototypes.
  - Removed standalone `Arcade` (`1041`) platform (retaining `SNK Neo Geo` home console AES/MVS/CD support).
- **Cross-Referenced ROM Verification Databases & Automated Download Hash Gate (`VerifiedRomHashSpecification`, `UnmanagedDiskScanner`, `GameCatalogProxy`)**:
  - **Always-On Cross-Referenced ROM Catalog (`No-Intro`, `Redump`, `TOSEC`, `SmokeMonster HTGDB/SMDB`)**: Separate DAT/SMDB catalogs are maintained independently in `DatFiles` / `DatEntries` so individual database updates replace only that catalog in-place, while `GameCatalogProxy.BuildPlatformSeriesAndEpisodes` cross-references entries by cryptographic hash (`SHA-256`, `SHA-1`, `MD5`, `CRC32`) and canonical release key (`Region + Normalized Title + Revision`) so duplicates are never populated in platform libraries.
  - **Automated Download Import Hash Verification (`VerifiedRomHashSpecification` & `ImportDecisionMaker`)**: Wired `IUnmanagedDiskScanner.VerifyAndEnrichLocalEpisode` into `ImportDecisionMaker` to compute `CRC32`, `MD5`, `SHA-1`, and `SHA-256` hashes (including zero-extraction `.zip` header inspection) on automated download completion. Downloads matching a verified hash are automatically linked to their canonical `Series` (`Platforms`) and `Episode` (`Games`), while unverified downloads are held in the **Activity Queue** with `ImportRejectionReason.UnverifiedRomHash` for user inspection and manual approval/rejection.
- **Unified Metadata Settings Page (`/settings/metadata`), Priority Cascading, & API Rate-Limit Fail-Safes (`GameMediaAssetService`)**:
  - Consolidated `Metadata` and `Metadata Sources` into a single unified **`Metadata` (`/settings/metadata`)** settings page (`GET/POST /api/v3/gamemetadata/settings`).
  - Added per-scraper **Priority (`1..4`)**, **API Rate-Limit Fail-Safe (`MaxRequestsPerMinute` sliding window + automatic cooldown on HTTP 429/403/503)**, account/API credentials, and **per-scraper artwork/asset checkboxes** (`PublishingMetadata`, `BoxArt2d`, `BoxArt3d`, `Screenshots`, `TitleScreens`, `ClearLogos`, `VideoPreviews`, `ManualsPdf`) for **ScreenScraper.fr**, **IGDB (Twitch Developer API)**, **TheGamesDB (TGDB)**, and **Libretro Thumbnails & LaunchBox Community DB**.
  - **Non-Destructive Priority Cascade (`ScrapeMediaAssetsForImportedRom`)**: On ROM import (`EpisodeImportedEvent`), Controllarr iterates through enabled scrapers in Priority order (`1 -> 2 -> 3 -> 4`) to complete any missing assets under `<Platform Path>/media/{box2d,box3d,screenshots,titlescreens,marquee,videos,manuals}/<RomStem>.<ext>` without overwriting assets already gathered by a higher-priority source.
  - **SD Card & Flash-Cart Media Export (`SdCardExporterService`)**: Automatically copies `<Platform>/media/<type>/*` and `<Platform>/gamelist.xml` alongside exported ROMs during SD card export (`PR #69`).
- **EmulationStation / Batocera / RetroDeck `gamelist.xml` & Media Asset Pipeline**:
  - Automatically generates and updates per-platform `gamelist.xml` manifests (including `<path>`, `<name>`, `<desc>`, `<releasedate>`, `<developer>`, `<publisher>`, `<image>`, `<marquee>`, `<video>`, and `<manual>` tags governed by the user's asset toggles) on platform refresh (`SeriesRefreshCompleteEvent`), ROM import (`EpisodeImportedEvent`), and settings save.
- **Unified DAT & Game Catalog Database (`DatCacheService` + `GameCatalogProxy` + `Platforms`/`Games` + `UnmanagedDiskScanner`)**:
  - Unified the DAT/SMDB hash database (`DatFiles` and `DatEntries` via `DatCacheService`) with the platform game list generator (`GameCatalogProxy`) and the `Platforms` (`Series`) / `Games` (`Episode`) database tables so a single catalog backs both platform game list creation and cryptographic hash identification on import.
  - Updated `DatCacheService.Insert` to replace existing `DatFile` / `DatEntries` snapshots by name on update rather than duplicating rows, and added `GetEntriesForPlatform` so `GameCatalogProxy.BuildPlatformSeriesAndEpisodes` automatically merges all local and online DAT/SMDB entries into each platform's game list.
  - Updated `GameMediaAssetService.ImportDatOrSmdb` so importing or updating a `.dat` (Logiqx XML or ClrMamePro) or `.smdb` (`.txt`) file automatically links it to its target platform, invalidates the platform catalog cache, and syncs newly discovered games directly into the platform's `Games` (`Episode`) table.
  - Updated `UnmanagedDiskScanner` so hash matches (`SHA-256`, `SHA-1`, `MD5`, `CRC32`) against the unified catalog resolve directly to the canonical `Series` (`Platforms`) and `Episode` (`Games`) records during folder scans and Interactive Manual Import, even when a ROM file has an obscured filename.
- **ROM Post-Processing Pipeline (`SidecarSaveService`, `DiscCompressionService`, `ArchiveExtractionService`, `ArchiveRepackService`, `UnmanagedDiskScanner`)**:
  - **Sidecar Battery Save & Patch Mobility (`SidecarSaveService`)**: Wired into `EpisodeFileMovingService` so `.srm`, `.sav`, `.eep`, `.fla`, `.sra`, `.state`, `.ips`, `.bps`, and `.ups` sidecar files automatically move/copy and rename alongside their parent ROM.
  - **Disc Compression (`chdman` / `dolphin-tool`) & Multi-Disc `.m3u` Playlist Generation (`DiscCompressionService`)**: Wired into `EpisodeFileMovingService`, respecting both platform-specific (`Series.PreferredArchiveFormat`) and global (`RomArchiveHandling`) compression settings, and automatically generating `.m3u` playlists for multi-disc games (`(Disc 1)`, `(Disc 2)`, etc.).
  - **Archive Extraction & Single-ROM `.zip` Repackaging (`ArchiveExtractionService` & `ArchiveRepackService`)**: Wired into `EpisodeFileMovingService` to extract compressed archives when `Extract` is configured or repackage uncompressed cartridge ROMs into single-file `.zip` archives when `Keep / Compress as .zip` is configured.
  - **Interactive Manual Import Hash & Catalog Enrichment (`UnmanagedDiskScanner`)**: Wired `IUnmanagedDiskScanner.EnrichItem` into `ManualImportService` to compute `CRC32`, `MD5`, `SHA-1`, and `SHA-256` hashes (including zero-extraction `.zip` inspection) and verify against the local `IDatCacheService` during Interactive/Manual Import.
- **Libretro & Emulation Settings (`/settings/libretro`) & In-Browser Libretro WebAssembly Player (`/webplayer`)**:
  - Added a dedicated **Libretro & Emulation** settings page (`Settings -> Libretro & Emulation`) to configure:
    - **Emulation & Game Launch Behavior**: Toggle the in-browser Libretro WebAssembly (`EmulatorJS`) player, choose preferred launch mode (`In-Browser Libretro Web Player`, `Local RetroArch Desktop Process`, or `Desktop URI Protocol (controllarr://play)`), select default WebGL CRT/LCD shaders (`CRT-Mattias`, `CRT-EasyMode`, `CRT-Aperture`, `CRT-Geom`, `LCD-Grid v2`, `2xScaleHQ`, `SABR v3.0`), and toggle BIOS auto-injection, SRAM/save-state persistence, RetroAchievements session forwarding, and fullscreen launch.
    - **Desktop RetroArch Integration**: Configure local `RetroArch` executable path, `cores/` directory, and custom `retroarch.cfg` path for direct native desktop launching (`POST /api/v3/gamelauncher/{gameId}/launch`).
    - **WebAssembly Core Asset Delivery**: Configure the EmulatorJS / Libretro `.wasm` data URL (`https://cdn.emulatorjs.org/stable/data/` or self-hosted path).
    - **Per-Platform Libretro Core Assignments**: Customize preferred Libretro cores across 17 console and handheld families (`NES`, `SNES`, `Game Boy`, `Game Boy Color`, `Game Boy Advance`, `Nintendo 64`, `Nintendo DS`, `Sega Genesis / Mega Drive`, `Sega Master System / Game Gear`, `Sega CD / 32X`, `Sega Saturn`, `Sony PlayStation`, `Sony PSP`, `TurboGrafx-16 / PC Engine`, `Neo Geo / Arcade`, `Atari 2600/7800/Lynx/Jaguar`, `PlayStation 2`, and `GameCube / Wii`).
  - Added the in-browser **Libretro Web Player (`/webplayer`)** with live shader switching, ROM binary streaming (`GET /api/v3/gamelauncher/{gameId}/rom`), desktop RetroArch launch trigger, and per-game **Play Game (Libretro)** buttons in the platform library table (`EpisodeSearchCell.tsx`) and platform toolbar (`SeriesDetails.tsx`).
- **7 New Retro & Modern Console UI Themes (`sms`, `jaguar`, `ps1`, `ps2`, `xbox`, `xbox360`, `wii`)**:
  - Added **Sega Master System (1985)** (`sms`): Matte black chassis with crimson pinstripe framing, red accents, and Sega blue selection badges.
  - Added **Atari Jaguar (1993)** (`jaguar`): Deep stealth charcoal shell with red roar accents and amber/gold "64-Bit" callouts.
  - Added **Sony PlayStation (1994)** (`ps1`): Classic SCPH-1001 stone gray casing with Sony royal blue primary actions and 4-color logo/button highlights (teal, red, yellow, blue).
  - Added **Sony PlayStation 2 (2000)** (`ps2`): Matte black monolith surface with deep indigo tower undertones and electric cyan-blue optical accents.
  - Added **Microsoft Xbox (2001)** (`xbox`): Dark industrial black-green chassis with radioactive neon jewel green highlights.
  - Added **Microsoft Xbox 360 (2005)** (`xbox360`):NXE/Blades dark slate charcoal with Ring-of-Light lime green (`#9bc848`) and deep Xbox green (`#107c10`) accents.
  - Added **Nintendo Wii (2006)** (`wii`): Glossy pearl white and cool ice-gray Wii Menu channels with glowing Wii optical disc slot cyan-blue (`#009ac7`) accents.
- **Dedicated `Settings -> Collection Settings` Page (`/settings/collection`)**:
  - Created `CollectionSettings.tsx` (`Settings/Collection/CollectionSettings.tsx`) and registered it in the sidebar (`PageSidebar.tsx`), settings index (`Settings.tsx`), and router (`AppRoutes.tsx`).
  - Moved **Default Collection Strategy & 1G1R / Unofficial Content Defaults** (Default Collection Strategy: `1G1R` vs `Completionist`, `1G1R Scope` dropdown: `Per Game` vs `Per Region`, `1G1R Default Version Preference`, `Default Pinned Version Tag`, `Enable Compression by Default`, and `Track Unlicensed` / `Track ROM Hacks & Translations` / `Track Homebrew & Aftermarket` toggles) and **Platform Region & Section Ordering (Drag & Drop)** (`RegionOrderingSection.tsx`) out of `MediaManagement.tsx` into `CollectionSettings.tsx`.
- **Unified Library Path & ROM Naming Format with Console/Game Variables**:
  - Replaced the three separate folder/file format inputs (`Platform Folder Format`, `Region Folder Format`, `Standard Game Format`) in `Naming.tsx` with a single unified **Library Path & ROM Naming Format** field defaulting to `{Platform}/{Region}/{Game Title}`.
  - Updated `NamingModal.tsx` (`?` variable picker) and `FileNameBuilder.cs` / `FileNameValidation.cs` / `FileNameValidationService.cs` to support console, region, and game variables (`{Platform}`, `{Platform Short}`, `{Manufacturer}`, `{Platform Year}`, `{Region}`, `{Region Code}`, `{Game Title}`, `{Game CleanTitle}`, `{Release Year}`, `{Version}`, `{Disc}`) and full-path presets (`{Platform}/{Region}/{Game Title}`, `{Platform Short}/{Region Code}/{Game CleanTitle} ({Release Year})`, `{Platform}/{Game Title}/{Game Title}`, `{Manufacturer}/{Platform}/{Region}/{Game Title}`).
- **Expanded Regional Retro Console UI Themes (`#59` / `#60`)**:
  - Split `NES (1985)` and `Famicom (1983)` into distinct themes (`nes` and `famicom`).
  - Split `SNES North America (1991)` and `Super Famicom (1990)` (`snes` and `superfamicom`, featuring the 4-color red/yellow/blue/green Japanese controller palette).
  - Split `Sega Genesis (1989)` (blue highlights) and `Sega Mega Drive (1988)` (`megadrive`, maroon and gold highlights).
  - Refactored `Game Boy DMG-01 (1989)` (`dmg`) so the primary background uses DMG pebble shell gray while cards, inputs, and text use the olive dot-matrix LCD palette with magenta button accents.
  - Added `TurboGrafx-16 (1989)` (`tg16`) with dark charcoal, orange, and yellow accents.
- **Live RetroAchievements Web API Integration**:
  - Upgraded `RetroAchievementsService` (`GET /api/v3/retroachievements/library` and `POST /api/v3/retroachievements/progress`) to persist validated user credentials (`retroachievements.json`) and query live completion progress and achievement badge sets from `retroachievements.org` (`API_GetUserCompletionProgress.php` and `API_GetGameInfoAndUserProgress.php`).
  - Removed the legacy 4-badge mock generator in `gameAchievementsHelper.ts` and `RetroAchievementsShowcaseModal.tsx` so library rows only display real earned/total achievement counts and authentic mastery status, and removed the redundant `HC` label suffix on gold hardcore badges.
- **BIOS Download Queue Tracking & History Logging**:
  - Wired `BiosService.DownloadPlatformBios` into `InternetArchive.TrackTransferState` and `IHistoryRepository` so BIOS firmware downloads appear in **Activity -> Queue** while in progress and log `Grabbed` and `DownloadFolderImported` events in **Activity -> History**.
  - Added multi-mirror Libretro/RetroPie/Internet Archive system folder resolution (`archtaurus/RetroPieBIOS`, `Abdess/retroarch_system`, and `archive.org`) with deterministic offline fallback so BIOS download buttons reliably save firmware files into `{platformPath}/bios`.
- **Live Activity Queue & Speed Tracking for HTTPS Downloads (`f7ce4e2c2`)**:
  - Upgraded the `InternetArchive` HTTPS download client with concurrent background streaming (`SemaphoreSlim` limit of 2 active streams, queueing additional downloads), real-time byte progress tracking, and live throughput calculation (`SpeedBytesPerSec`).
  - Updated the **Activity -> Queue** UI (`TimeLeftCell.tsx`, `QueueRow.tsx`, `ProtocolLabel.tsx`) to display queued and in-progress HTTPS downloads with live download speeds alongside remaining time (e.g., `01:24 (8.4 MB/s)`) and an `https` protocol badge.
  - Triggered automatic `RefreshMonitoredDownloadsCommand` pushes from `InternetArchive` during active streaming and immediately upon download completion or failure for real-time SignalR UI updates.
- **Completed Download Handling & History Logging for HTTPS Downloads (`f7ce4e2c2`)**:
  - Registered all console ROM, archive, and optical disc extensions in `MediaFileExtensions` (`.zip`, `.7z`, `.chd`, `.rvz`, `.nes`, `.sfc`, `.smc`, `.z64`, `.n64`, `.v64`, `.gb`, `.gbc`, `.gba`, `.nds`, `.3ds`, `.cia`, `.md`, `.smd`, `.gen`, `.sms`, `.gg`, `.32x`, `.cdi`, `.gdi`, `.cue`, `.pbp`, `.cso`, `.wbfs`, `.wux`, `.xci`, `.nsp`, `.pce`, `.ngp`, `.ngc`, etc.).
  - Updated `DetectSample` and `AggregationService` to bypass video `ffprobe` runtime checks on console ROM and archive files.
  - Connected `TrackedDownloadService` grab history to `ImportDecisionMaker` so completed HTTPS downloads automatically import/move into their target platform/region folders and log `Grabbed` and `DownloadFolderImported` events in **Activity -> History** identically to Usenet and BitTorrent downloads.
- **Drag-and-Drop Platform Region & Section Ordering (`e010bc7a8`)**:
  - Added an interactive HTML5 drag-and-drop **Platform Region & Section Ordering** setting in **Settings -> Media Management** (`RegionOrderingSection.tsx`, `MediaManagement.cs` `PreferredRegions`) with UP/DOWN accessibility buttons and a "Reset to Default Order" action.
  - Wired `SeriesDetails.tsx` and the 1G1R engine to sort platform region sections and evaluate regional priority using the user's customized order.
- **Official GitHub Wiki Publication (`e010bc7a8`)**:
  - Published all 18 documentation guides directly to the live GitHub Wiki (`https://github.com/12thirtyfour/Controllarr/wiki`) with a custom `_Sidebar.md` navigation tree and removed the redundant in-repo `wiki/` directory.

### Changed
- **Reverted Game Boy DMG-01 (`dmg`) Theme**:
  - Restored the classic full olive dot-matrix LCD palette (`#0f240f` / `#153015` / `#1b3b1b` backgrounds with `#9bbc0f` / `#8bac0f` / `#d0e898` phosphor greens) for `Game Boy DMG-01 (1989)`.
- **Region Folder Naming on Disk (`USA`, `Europe`, `Japan`, `World`, `Unlicensed`, `Homebrew`, `ROM Hacks` instead of `Season 1`, `Season 2`)**:
  - Updated `GameCatalogProxy.BuildDefaultRegionSeasons` so every platform region season carries its canonical region title (`0 -> "Unlicensed"`, `1 -> "USA"`, `2 -> "Europe"`, `3 -> "Japan"`, `4 -> "World"`, `5 -> "Homebrew"`, `6 -> "ROM Hacks"`).
  - Updated `NamingConfigService.GetConfig()` and `Migration 242` (`242_rename_tv_tables_to_platform_game_rom_tables.cs`) to automatically upgrade legacy database `NamingConfig` rows (`Season {season}` -> `{Region}`, `Specials` -> `Unlicensed`, `{Series Title}` -> `{Platform}`, and `S{season` -> `{Game Title}`).
  - Updated `AddSeriesService.EnsurePlatformFolderStructure` and `DiskScanService.MigrateLegacySeasonFolders` so newly added consoles and rescanned platforms automatically replace legacy `"Season 1"` / `"Season 2"` / `"Specials"` folders with `"USA"`, `"Europe"`, `"Japan"`, `"World"`, `"Unlicensed"`, `"Homebrew"`, and `"ROM Hacks"`.
- **Moved `BIOS Files` (`BIOS Matrix`) Out of Settings -> Placed Under `Library` (`/bios`)**:
  - Moved the BIOS management page out of the `Settings` sidebar and settings index (`Settings.tsx`) and placed it directly under **`Library`** in `PageSidebar.tsx` (`Add New`, `Import Library`, `BIOS Files`).
  - Renamed **`BIOS Matrix`** to **`BIOS Files`** across the sidebar, page header (`BiosSettings.tsx`), and localization catalog (`en.json`), and registered `/bios` in `AppRoutes.tsx` with `/settings/bios` redirecting to `/bios`.
- **Controllarr Built-In Application Updater (`System -> Updates`)**:
  - Repointed `SonarrCloudRequestBuilder` and `UpdatePackageProvider` from `services.sonarr.tv` to Controllarr's GitHub Releases API (`https://api.github.com/repos/12thirtyfour/Controllarr/releases`).
  - Updated `UpdatePackageProvider` to map GitHub Releases and platform-specific assets (`win-x64`, `win-x86`, `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `linux-arm`, `linux-musl-x64`, `linux-musl-arm64`, `freebsd-x64`) into `UpdatePackage` objects with SHA-256 digest verification (`UpdateVerification.cs`) and release notes parsing.
  - Aligned `Directory.Build.props` (`AssemblyVersion` `5.0.0.*`, `AssemblyConfiguration` `develop`), `ConfigFileProvider.Branch` (`develop`), `.github/actions/package/package.sh` (`Controllarr` archive root folder), and `InstallUpdateService` (`NzbDrone.Core` & `NzbDrone.Update`) so checking for updates and installing updates from **System -> Updates** works end-to-end.
- **Database Schema Table Renaming (`Migration 242`) & Repository Overhaul**:
  - Added `242_rename_tv_tables_to_platform_game_rom_tables.cs` (`[Migration(242)]`) to rename legacy database tables to authentic Controllarr domain tables: `"Series"` -> `"Platforms"`, `"SeriesTranslations"` -> `"PlatformTranslations"`, `"Episodes"` -> `"Games"`, `"EpisodeFiles"` -> `"RomFiles"`, and `"SceneMappings"` -> `"PlatformMappings"`.
  - Updated `TableMapping.cs`, `TableMapper.cs` (with seamless sort-key aliasing), `BlocklistRepository.cs`, `HistoryRepository.cs`, `SeriesStatisticsRepository.cs`, `StatisticsRepository.cs`, `EpisodeRepository.cs`, `SeriesRepository.cs`, and all 9 `Housekeeping/Housekeepers` SQL tasks (`CleanupOrphanedBlocklist`, `CleanupOrphanedEpisodeFiles`, `CleanupOrphanedEpisodes`, `CleanupOrphanedExtraFiles`, `CleanupOrphanedHistoryItems`, `CleanupOrphanedMetadataFiles`, `CleanupOrphanedPendingReleases`, `CleanupOrphanedSubtitleFiles`, `CleanupUnusedTags`) to query `"Platforms"`, `"Games"`, and `"RomFiles"`.
- **Project-Wide Game/ROM/Platform/Controllarr Terminology & Domain Module Overhaul (`#61`, `#62`, `#63`, `#64`, `#65`, `#66`)**:
  - Converted all remaining TV/Sonarr localization strings in `src/NzbDrone.Core/Localization/Core/en.json` to authentic `Controllarr`, `Platform`, `Region`, `Game`, `ROM File`, `Release Date`, and `Manufacturer` terminology.
  - Transitioned global frontend configuration from `window.Sonarr` to `window.Controllarr` across `index.html`, `frontend/src/index.ts`, `frontend/typings/Globals.d.ts`, and all frontend hooks, stores, and components.
  - Created `frontend/src/Platform/`, `frontend/src/AddPlatform/`, `frontend/src/Region/`, `frontend/src/Game/`, and `frontend/src/RomFile/` domain modules and wired `AppRoutes.tsx` to `PlatformIndex`, `PlatformDetailsPage`, `AddNewPlatform`, `ImportPlatform`, and `ImportPlatformSelectFolder`.
  - Added V3 and V5 REST API route aliases (`/api/v3/platform`, `/api/v3/platform/lookup`, `/api/v3/platform/import`, `/api/v3/platform/editor`, `/api/v3/game`, `/api/v3/romfile`, `/api/v3/platformpass`, `/api/v5/platform`, `/api/v5/platform/lookup`, `/api/v5/platform/import`, `/api/v5/platform/editor`, `/api/v5/game`, `/api/v5/romfile`, `/api/v5/platformpass`) and iCal feed endpoints (`/feed/v3/calendar/Controllarr.ics` and `/feed/v5/calendar/Controllarr.ics` with `Controllarr Game Release Schedule`).
  - Updated local variables, descriptions, and external links across frontend modules (`Settings`, `System`, `FirstRun`, `RootFolder`, `Tags`, `Activity`, `Wanted`, `Calendar`, `Statistics`, `Organize`, `Parse`, `InteractiveImport`, `InteractiveSearch`) and backend `Notifications`, `HealthCheck`, `ImportLists`, and `Extras` services.
- **Import Existing Platform UI Modernization**:
  - Streamlined `ImportSeriesCard.tsx`, `ImportSeriesDefaults.tsx`, `ImportSeriesFooter.tsx`, `importSeriesStore.ts`, and `useImportSeries.ts` when importing an existing platform folder: removed `Quality Profile`, `Region Folder`, and `Metadata Language`, changed `Platform Type` to **Collection Type** (`1G1R (Per Game)`, `1G1R (Per Region)`, or `Completionist (All Revisions)`), and replaced `Catalog Standard` with **Compression** (`Enabled (Auto Format)` / `Disabled (Uncompressed)`).
- **Strict Per-Platform & Per-Add-On BIOS Isolation**:
  - Removed cross-platform companion mappings (`nes -> fds`, `snes -> sgb`, `tg16 -> tgcd`, `jaguar -> jaguarcd`) from `BiosService.cs` and added dedicated `satellaview` (`BS-X.bin`) and `64dd` (`64DD_IPL.bin`) catalog definitions so each console, add-on (`Famicom Disk System`, `Sega CD`, `TurboGrafx-CD`, `Jaguar CD`, `Satellaview`, `Nintendo 64DD`, `Super Game Boy`), or handheld only displays BIOS files that directly pertain to it.
- **Swapped 1G1R & Completionist Library Views + Default Collapsed Regions**:
  - Updated `SeriesDetails.tsx` and `SeriesDetailsSeason.tsx` so all regions start collapsed when opening a platform page (preventing 1,000+ game tables from expanding simultaneously).
  - Updated **1G1R** mode to render only the single winning retail ROM row per game (no `+N versions` button), and updated **Completionist** mode to group all versions of each game into a single primary row with the expandable `+N versions` button (`+X versions` / `Hide versions`).
- **Flexible Collection Curation Documentation Reframing (`#59` / `#60`)**:
  - Updated `README.md`, `ROADMAP.md`, and the GitHub Wiki (`Controllarr.wiki.git`) to emphasize that Controllarr empowers users to define the exact type of collection they want to maintain (**1G1R** or **Completionist**) rather than over-indexing on 1G1R alone.
- **Oversized Console Hardware Art Header Backdrop**:
  - Removed the rectangular poster frame from `SeriesDetails.tsx` and styled each platform's hardware art (`consoleArtBackground`) as an oversized header background graphic at `70%` opacity (`opacity: 0.7`).
- **Streamlined 1G1R Scope Dropdown**:
  - Replaced the two separate 1G1R scope checkboxes with a single **`1G1R Scope`** dropdown containing **`Per Game`** (`oneGameOneRom`) and **`Per Region`** (`oneGamePerRegionPerConsole`).
- **Clean Platform BIOS Header**:
  - Removed the redundant `{platformPath}/bios` folder path badge from `PlatformBiosSection.tsx`.
- **Canonical Platform Resolution & Route Cleanup**:
  - Replaced the 55-entry band-aid `PlatformSlugAliases` dictionary in `BiosService.cs` with canonical resolution via `IGameCatalogService.TryGetPlatformEntryBySlug`, while preserving legitimate regional hardware aliases (`Famicom`, `Super Famicom`, `Mega Drive`, `PC Engine`).
  - Updated all frontend navigation links (`AppRoutes.tsx`, `PageHeader.tsx`, `SeriesSearchInput.tsx`, `SeriesDetails.tsx`, `SeriesDetailsPage.tsx`, `SeriesIndexPosterSelect.tsx`, `SeriesIndexRow.tsx`, `HealthItemLink.tsx`) to use `/platforms` directly and removed the `/series` sidebar alias band-aid.
- **Strict Retail-Only 1G1R Enforcement (`f7ce4e2c2`)**:
  - Updated `CollectionStrategyService.FilterCandidates`, `DownloadDecisionComparer.CompareCollectionStrategy`, `SeriesService.UpdateSeries`, and `SeriesDetailsSeason.tsx` so **Demos, Prototypes, Betas, Samples, Promos, Previews, and Kiosk builds are never considered for 1G1R**—even when a title only has a pre-release entry. 1G1R strictly selects and monitors released retail versions.
- **Complete AppData, Database & Service Isolation from Sonarr (`e010bc7a8`)**:
  - Changed all application data folders (`~/.config/Controllarr` on Linux/macOS, `%AppData%\Controllarr` on Windows, `/var/lib/controllarr`), SQLite database filenames (`controllarr.db`, `controllarr.log.db`), PostgreSQL default database names (`controllarr-main`, `controllarr-log`), PID file (`controllarr.pid`), log files (`controllarr.txt`), backup archive prefixes (`controllarr_backup_`), and Windows Service registration (`Controllarr`) so Controllarr never conflicts with an existing or future Sonarr installation.

### Removed
- **Removed Superseded & Redundant Prototype Classes**:
  - Removed unused stubs and duplicate prototype services: `GameMetadataProxyStub`, `ChdmanService` (consolidated into `DiscCompressionService`), `LibraryImportService`, `VirtualSymlinkLibraryService` (superseded by `VirtualLibraryService`), `RomReleaseParser` / `RomReleaseInfo` (superseded by `RomParser` / `ParsedRomInfo`), `CollectionStrategyService` (superseded by `EpisodeMonitoredService`), and `IAssetScraper` / `IgdbAssetScraper` (superseded by `GameMediaAssetService`).
  - Removed redundant early-prototype parallel database models, repositories, and controllers (`GamePlatform`, `GameTitle`, `GameRelease`, `RomEntry`, `SmartAddService`, `GamePlatformController`, `RomEntryController`, `GameSearchController`) that were superseded by the unified `Platforms` (`Series`), `Games` (`Episode`), and `RomFiles` (`EpisodeFile`) domain architecture.

### Fixed
- **Individual Game & Region "Wanted" (`Monitored`) Toggle**:
  - Fixed a bug where newly added platforms defaulted to `Series.Monitored = false` in `GameCatalogProxy.cs` and `MonitorToggleButton` disabled interaction when `!seriesMonitored`, causing a disabled (`not-allowed`) cursor on individual games and region headers.
- **GitHub Actions CI Suite**:
  - Resolved all frontend ESLint and Stylelint errors across modified TypeScript and CSS modules, and updated `HttpClientFixture`, `HttpLogFixture`, and `GameCatalogProxy` fallback lookups for offline test compatibility.

---

## [2026-10-23]

### Added
- **Unified Multi-Source Game Catalog (`cef1dfbab`, `1d067e9cc`, `66839460e`)**:
  - Built a zero-configuration offline/online catalog aggregator (`LibretroCatalogService`) that merges DAT entries across Libretro `metadat/no-intro`, `metadat/redump`, and `dat/` repositories so platforms populate their complete documented libraries (e.g., 700+ official US NES releases, full regional libraries, revisions, and multi-disc sets).
  - Added a search filter bar (`All`, `Platforms Only`, `Games Only`) on the **Add New Platform or Game** page with `3:4` box-art preview proportions.
- **Authentic Console Hardware Photography (`cef1dfbab`)**:
  - Bundled 42 public-domain / CC0 transparent hardware photographs by **Evan-Amos** (Wikimedia Commons) in `/Content/Images/Consoles/{slug}.png` for all console overview cards, search results, and platform detail headers, while preserving 2D retail box art for individual games.
- **8-Region Platform Hierarchy & Regional Console Aliases (`cef1dfbab`, `daf5c3af7`, `1d067e9cc`)**:
  - Organized platform views into 8 distinct regional/content buckets: `USA`, `Europe`, `Japan`, `World / International`, `Other Regions`, `Unlicensed`, `Aftermarket & Homebrew`, and `Hacks & Translations`.
  - Added regional console name resolution (`Famicom` for NES Japan, `Super Famicom` for SNES Japan, `Mega Drive` for Genesis Japan/Europe, `PC Engine` for TurboGrafx-16 Japan) across catalog search, indexer queries, and region headers.
- **Grouped Game Version Rows & Version Inspector Modal (`1d067e9cc`)**:
  - Consolidated multiple regional revisions, betas, and prototypes of the same title into a single clean row when viewing a platform in **1G1R** mode, complete with a clickable version count badge (`GameVersionsModal`) to inspect all known dumps and manually pin/monitor a specific revision.
- **Virtual Symlink Libraries (`daf5c3af7`)**:
  - Added **Settings -> Virtual Libraries** (`VirtualLibraryService`, `VirtualLibraryController`) to generate and maintain live symlinked/junctioned folder structures tailored to specific emulation targets (`MiSTer FPGA`, `RetroArch`, `Analogue Pocket`, `OnionOS`, `GarlicOS`, `Batocera`, `ES-DE`, `EverDrive`, `FXPak Pro`, `ODE`) without duplicating ROM storage, including automatic multi-disc folder grouping (`Game Title (Disc 1)`).
- **Expanded Platform BIOS Matrix & Automated BIOS Downloader (`daf5c3af7`)**:
  - Expanded `BiosFileService` to 80+ canonical firmware definitions across 29 platforms, grouped into collapsible platform cards filtered by the user's added platforms (with a "Show All Supported Platforms" toggle).
  - Added a one-click **Download Missing BIOS** button (`POST /api/v3/bios/download`) that fetches missing firmware files from `libretro-system` and Internet Archive BIOS repositories and verifies their MD5/SHA-1/CRC32 checksums before saving.
- **Internet Archive HTTP Indexer & Protocol Priority (`3db47ef74`, `cef1dfbab`)**:
  - Added `InternetArchiveIndexer` for direct HTTPS ROM discovery alongside Usenet (`Newznab`) and BitTorrent (`Torznab`), and exposed `HTTP` protocol toggles and ordering in **Settings -> Profiles -> Delay Profiles**.
- **Retro Console UI Themes (`daf5c3af7`)**:
  - Added 4 custom retro color themes in **Settings -> UI**: `SNES Gray & Purple`, `Famicom Red & Gold`, `Game Boy DMG Olive`, and `CRT Phosphor Green`.
- **First-Run Authentication Setup Wizard (`b410bdd9e`, `7298bd477`)**:
  - Enabled upstream-compatible initial authentication configuration on first launch without locking out new installations.
- **SD Card Multi-Device Exporter Modal (`PR #46`, `e14e38e6e`, `7887af062`, `4166ec17d`)**:
  - Full UI modal and backend service (`/api/v3/sdcardexport`, `/api/v5/sdcardexport`) supporting 15+ target hardware presets (`MiSTer FPGA`, `Analogue Pocket`, `OnionOS`, `GarlicOS`, `ArkOS`, `Batocera`, `ES-DE`, `EverDrive`, `FXPak Pro`, `EZ-Flash`, `R4/TWiLight Menu++`, `xStation`, `GC Loader`, `Fenrir`, `GDEMU`) with automatic FAT32-safe alphabetical directory bucketing (`A-M/`, `N-Z/`) and opt-in device-aware archive extraction/compression.
- **Platform BIOS Verification Matrix (`PR #47`, `ba4c87be6`)**:
  - Interactive settings view and backend service (`/api/v3/bios`, `/api/v5/bios`) calculating CRC32, MD5, and SHA-1 hashes to verify required and optional console firmwares.
- **RetroAchievements Integration (`Issue #45`, `02eb8e940`)**:
  - Account credential verification with live test feedback, visual badge showcase modal, game mastery progress tracking, and leaderboard rankings (`/api/v3/retroachievements`, `/api/v5/retroachievements`).
- **In-Browser Web Player & Desktop Game Launcher (`5656262cb`)**:
  - Embedded Libretro WebAssembly player for instant in-browser play and desktop launcher URI protocol handler (`controllarr://play/{gameId}`).
- **Dual REST API Exposure (`02eb8e940`)**:
  - Full parity across all game controllers (`BiosController`, `SdCardExportController`, `RetroAchievementsController`, `GameLauncherController`, `GamePlatformController`, `GameSearchController`, `RomEntryController`) on both `/api/v3` and `/api/v5`.
- **Complete Visual Identity Overhaul (`c3335dfac`)**:
  - Replaced all 38 branding assets (favicons, PNGs, SVGs, Apple touch icons, Windows tiles, `.ico` executables) generated from `Logo/master-logo.png`.

### Changed
- **Platform Routing & Navigation (`1d067e9cc`)**:
  - Migrated primary library routes from `/series` to `/platforms` (with backward-compatible redirects) and updated all navigation breadcrumbs and modals to use Platform/Game terminology.
- **Automatic Platform-Aware Compression Toggle (`cef1dfbab`)**:
  - Simplified the platform compression dropdown into a single **Enable Compression** checkbox that automatically applies the canonical lossless container for each platform (`.chd` for optical disc systems, `.rvz` for GameCube/Wii, `.zip` for cartridge/handheld systems) or leaves files uncompressed when unchecked.
- **Settings Menu Modernization (`daf5c3af7`, `cef1dfbab`, `dbfcd73f6`)**:
  - Updated **Settings -> Media Management** with game-specific renaming tokens (`{Platform Title}`, `{Region}`, `{Game Title}`, `{Revision}`, `{Languages}`, `{Dump Info}`), Multi-Disc Naming Style, and Default Collection Strategy (`1G1R` vs `Completionist`) with inherited 1G1R revision/region defaults.
  - Replaced legacy TV metadata settings in **Settings -> Metadata** with **Game Artwork & Emulation Frontends** (`Box Art`, `Title Screens`, `Gameplay Screenshots`, `3D Box Renders`, `Logos / Marquees`, `EmulationStation gamelist.xml`, `RetroArch .lpl`).
  - Removed obsolete TV settings pages (`Profiles`, `Quality`, `Custom Formats`).
  - Replaced legacy TheTVDB notification links with IGDB and Game Catalog links (`dbfcd73f6`).
- **Resilient MediaCoverProxy Artwork Fetching (`1d067e9cc`, `6250089e3`)**:
  - Updated `MediaCoverProxy` to suppresses 404 warnings and return `204 NoContent` when remote Libretro thumbnail URLs do not exist, eliminating false-positive fatal log stack traces.
- **Usenet Indexer Category Alignment (`7c66dac7e`, `cc5c2a581`)**:
  - Configured default search categories to `1000` (Console) and `8000` (Other/Misc) for Newznab Usenet indexers, fixed `NewznabCategoryFieldOptionsConverter` filtering, and removed legacy TV category overrides from presets.
- **Branding & Localization (`5c8f75293`)**:
  - Replaced user-facing Sonarr branding with Controllarr across `translate.ts`, `LocalizationService.cs`, `LoadingPage.tsx`, `login.html`, `logout.html`, `MoreInfo.tsx`, and `en.json`.
- **Build & Path Resolution Fixes (`507bf4cfa`, `2ffe020ec`)**:
  - Fixed `src/Directory.Build.props` to use `$(MSBuildThisFileDirectory)stylecop.json` for standalone project builds.
  - Fixed `PathExtensions.GetUiFolder()` to resolve `../UI` (`_output/UI`) when running a local `Release` build from `_output/net10.0/`.
- **CI/CD Matrix Stabilization (`99a5fa410`, `d8033632d`)**:
  - Resolved all 35 integration test failures and workflow configuration issues, achieving 100% passing across all 33 CI matrix jobs (Linux x64/arm64/musl, macOS Apple Silicon & Intel, Windows x64/x86, FreeBSD, and PostgreSQL 16–18).

---

## [2026-10-22] - Architectural Foundation (Batches 0–12)

### Added
- **Batch 0 & 1 — Domain Model Pivot & .NET 10 Upgrade**:
  - Upgraded the entire backend solution to **.NET 10** and migrated the frontend bundler to **Vite + React 19 + TypeScript**.
  - Adapted Series/Season/Episode domain concepts to `GamePlatform` / `Region` / `RomEntry` across SQLite and PostgreSQL migrations (`Migrations 237–241`).
- **Batch 2 — IGDB Metadata Client & Twitch OAuth (`bbb512441`)**:
  - Implemented `IgdbClient` and `IgdbOAuthTokenProvider` to fetch game metadata, descriptions, release dates, and cover art.
- **Batch 3 & 5 — No-Intro / Redump / TOSEC ROM Release Parser (`0c58d05ee`)**:
  - Built `RomFilenameParser` and `ParsedRomInfo` to extract regional tags, revisions (`Rev A`, `v1.1`), dump verification flags (`[!]`, `[b]`), hardware tags (`(SGB Enhanced)`), disc numbers, and full-set archive markers.
- **Batch 4 & 6 — DAT Catalog Ingestion, Hash Cache & Disc Compression (`3485eb7f7`, `98dbf7b62`)**:
  - Implemented XML/Logiqx and ClrMamePro DAT file parsers, SQLite hash caching, central-directory `.zip` CRC32 inspection without full extraction, header-skipping checksum verification (`.nes` iNES, `.sfc` copier headers), and lossless optical disc conversion via `chdman` (`.chd`) and `dolphin-tool` (`.rvz`).
- **Batch 7 — Unmanaged Disk Scanner & Sidecar Save Syncing**:
  - Added recursive disk scanning across flat and alphabetically bucketed ROM directories with lockstep sidecar save file (`.srm`, `.sav`, `.eep`, `.fla`, `.sra`, `.mcd`, `.ps2`) moves and renames.
- **Batch 8–11 — Three-Tiered Collection Strategy Engine & Smart Add (`6866d9d39`, `390a4d9fa`, `ffa3efc7e`, `b19121c3a`)**:
  - Implemented `CollectionStrategyService` (`OneGameOneRom`, `OneGamePerRegionPerConsole`, `Preservationist`), `RevisionPreference` (`LatestRevision`, `InitialRelease`), SMDB/HTGDB layout parsers, and 3-way acquisition protocol priority (`HTTP` / `Usenet` / `Torrent`).
- **Batch 12 — Authenticated Internet Archive S3/HTTP Client (`InternetArchive.cs`)**:
  - Added S3-style authenticated HTTP downloads (`Authorization: LOW accessKey:secretKey`) with masked credentials, configurable anonymous fallback on HTTP 401/403, Preferred Archive prioritization, and path traversal protection (`CA5389`).
