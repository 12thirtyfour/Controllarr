# Changelog

All notable changes to **Controllarr** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

- Multi-OS beta testing and telemetry triage across macOS, Windows, and Linux.
- Physical flash cart, handheld CFW, and ODE hardware target verification runs.

---

## [1.0.0-beta.7] - 2026-10-24

### Added
- **Expanded Console Verification Feeds (`SmokeMonster HTGDB/SMDB`, `Neo Geo AES/CD`, `Switch NSWDB`, `Decrypted NDS/3DS` Default & `PreferEncryptedRoms`)**:
  - **Direct SmokeMonster HTGDB / SMDB Sync**: Wired automatic GitHub SMDB manifest sync (`SHA-256`, `SHA-1`, `MD5`, `CRC32`) from `SmokeMonsterPacks/EverDrive-Packs-Lists-Database` across NES, FDS, SNES, N64, GB/GBC, GBA, Genesis, Sega CD, 32X, Master System, Game Gear, TurboGrafx-16, TurboGrafx-CD, SNK Neo Geo, Atari 2600/7800/Lynx/Jaguar, Virtual Boy, Neo Geo Pocket, and WonderSwan.
  - **SNK Neo Geo (`1038`) & Nintendo Switch (`1014`) Verification**: Wired `FBNeo / Neo Geo AES` (`dat/SNK - Neo Geo.dat`), Redump `SNK - Neo Geo CD.dat`, and SmokeMonster `NeoSD SMDB.txt` for Neo Geo home console & CD releases, and wired `nswdb.com/xml.php` XML parsing (`CRC32`, `TitleID`, region, revision) for Nintendo Switch.
  - **Companion Sub-Systems**: Wired monochrome `SNK - Neo Geo Pocket.dat`, monochrome `Bandai - WonderSwan.dat`, `NEC - PC Engine SuperGrafx.dat`, and `Nintendo - Wii (Digital).dat` into their parent platform libraries.
  - **Default Unencrypted (Decrypted) ROM Verification for Nintendo DS & 3DS (`PreferEncryptedRoms`)**: Defaulted Nintendo DS (`1012`), Nintendo 3DS (`1013`), and Wii U (`1026`) catalog & hash verification to **Decrypted** DATs (`Nintendo - Nintendo DS (Decrypted).dat`, `Nintendo - Nintendo DSi (Decrypted).dat`, `Nintendo - Nintendo 3DS (Decrypted).dat`, `Nintendo - New Nintendo 3DS (Decrypted).dat`, `Nintendo - Nintendo 3DS (Digital).dat`), and added a per-platform **"Prefer Encrypted ROM Hashes"** (`PreferEncryptedRoms`) toggle in the Add/Edit Platform modal to prioritize encrypted DAT hashes when desired.
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

### Removed
- **Removed Superseded & Redundant Prototype Classes**:
  - Removed unused stubs and duplicate prototype services: `GameMetadataProxyStub`, `ChdmanService` (consolidated into `DiscCompressionService`), `LibraryImportService`, `VirtualSymlinkLibraryService` (superseded by `VirtualLibraryService`), `RomReleaseParser` / `RomReleaseInfo` (superseded by `RomParser` / `ParsedRomInfo`), `CollectionStrategyService` (superseded by `EpisodeMonitoredService`), and `IAssetScraper` / `IgdbAssetScraper` (superseded by `GameMediaAssetService`).
  - Removed redundant early-prototype parallel database models, repositories, and controllers (`GamePlatform`, `GameTitle`, `GameRelease`, `RomEntry`, `SmartAddService`, `GamePlatformController`, `RomEntryController`, `GameSearchController`) that were superseded by the unified `Platforms` (`Series`), `Games` (`Episode`), and `RomFiles` (`EpisodeFile`) domain architecture.

---

## [1.0.0-beta.6] - 2026-10-24

### Added
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

---

## [1.0.0-beta.5] - 2026-10-24

### Changed
- **Database Schema Table Renaming (`Migration 242`) & Repository Overhaul**:
  - Added `242_rename_tv_tables_to_platform_game_rom_tables.cs` (`[Migration(242)]`) to rename legacy database tables to authentic Controllarr domain tables: `"Series"` -> `"Platforms"`, `"SeriesTranslations"` -> `"PlatformTranslations"`, `"Episodes"` -> `"Games"`, `"EpisodeFiles"` -> `"RomFiles"`, and `"SceneMappings"` -> `"PlatformMappings"`.
  - Updated `TableMapping.cs`, `TableMapper.cs` (with seamless sort-key aliasing), `BlocklistRepository.cs`, `HistoryRepository.cs`, `SeriesStatisticsRepository.cs`, `StatisticsRepository.cs`, `EpisodeRepository.cs`, `SeriesRepository.cs`, and all 9 `Housekeeping/Housekeepers` SQL tasks (`CleanupOrphanedBlocklist`, `CleanupOrphanedEpisodeFiles`, `CleanupOrphanedEpisodes`, `CleanupOrphanedExtraFiles`, `CleanupOrphanedHistoryItems`, `CleanupOrphanedMetadataFiles`, `CleanupOrphanedPendingReleases`, `CleanupOrphanedSubtitleFiles`, `CleanupUnusedTags`) to query `"Platforms"`, `"Games"`, and `"RomFiles"`.
- **Project-Wide Game/ROM/Platform/Controllarr Terminology & Domain Module Overhaul (`#61`, `#62`, `#63`, `#64`, `#65`, `#66`)**:
  - Converted all remaining TV/Sonarr localization strings in `src/NzbDrone.Core/Localization/Core/en.json` to authentic `Controllarr`, `Platform`, `Region`, `Game`, `ROM File`, `Release Date`, and `Manufacturer` terminology.
  - Transitioned global frontend configuration from `window.Sonarr` to `window.Controllarr` across `index.html`, `frontend/src/index.ts`, `frontend/typings/Globals.d.ts`, and all frontend hooks, stores, and components.
  - Created `frontend/src/Platform/`, `frontend/src/AddPlatform/`, `frontend/src/Region/`, `frontend/src/Game/`, and `frontend/src/RomFile/` domain modules and wired `AppRoutes.tsx` to `PlatformIndex`, `PlatformDetailsPage`, `AddNewPlatform`, `ImportPlatform`, and `ImportPlatformSelectFolder`.
  - Added V3 and V5 REST API route aliases (`/api/v3/platform`, `/api/v3/platform/lookup`, `/api/v3/platform/import`, `/api/v3/platform/editor`, `/api/v3/game`, `/api/v3/romfile`, `/api/v3/platformpass`, `/api/v5/platform`, `/api/v5/platform/lookup`, `/api/v5/platform/import`, `/api/v5/platform/editor`, `/api/v5/game`, `/api/v5/romfile`, `/api/v5/platformpass`) and iCal feed endpoints (`/feed/v3/calendar/Controllarr.ics` and `/feed/v5/calendar/Controllarr.ics` with `Controllarr Game Release Schedule`).
  - Updated local variables, descriptions, and external links across frontend modules (`Settings`, `System`, `FirstRun`, `RootFolder`, `Tags`, `Activity`, `Wanted`, `Calendar`, `Statistics`, `Organize`, `Parse`, `InteractiveImport`, `InteractiveSearch`) and backend `Notifications`, `HealthCheck`, `ImportLists`, and `Extras` services.

---

## [1.0.0-beta.4] - 2026-10-24

### Added
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

### Changed
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

### Fixed
- **Individual Game & Region "Wanted" (`Monitored`) Toggle**:
  - Fixed a bug where newly added platforms defaulted to `Series.Monitored = false` in `GameCatalogProxy.cs` and `MonitorToggleButton` disabled interaction when `!seriesMonitored`, causing a disabled (`not-allowed`) cursor on individual games and region headers.
- **GitHub Actions CI Suite**:
  - Resolved all frontend ESLint and Stylelint errors across modified TypeScript and CSS modules, and updated `HttpClientFixture`, `HttpLogFixture`, and `GameCatalogProxy` fallback lookups for offline test compatibility.

---

## [1.0.0-beta.3] - 2026-10-24

### Added
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
- **Strict Retail-Only 1G1R Enforcement (`f7ce4e2c2`)**:
  - Updated `CollectionStrategyService.FilterCandidates`, `DownloadDecisionComparer.CompareCollectionStrategy`, `SeriesService.UpdateSeries`, and `SeriesDetailsSeason.tsx` so **Demos, Prototypes, Betas, Samples, Promos, Previews, and Kiosk builds are never considered for 1G1R**—even when a title only has a pre-release entry. 1G1R strictly selects and monitors released retail versions.
- **Complete AppData, Database & Service Isolation from Sonarr (`e010bc7a8`)**:
  - Changed all application data folders (`~/.config/Controllarr` on Linux/macOS, `%AppData%\Controllarr` on Windows, `/var/lib/controllarr`), SQLite database filenames (`controllarr.db`, `controllarr.log.db`), PostgreSQL default database names (`controllarr-main`, `controllarr-log`), PID file (`controllarr.pid`), log files (`controllarr.txt`), backup archive prefixes (`controllarr_backup_`), and Windows Service registration (`Controllarr`) so Controllarr never conflicts with an existing or future Sonarr installation.

---

## [1.0.0-beta.2] - 2026-10-23

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

---

## [1.0.0-beta.1] - 2026-10-23

### Added
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

## [1.0.0-alpha] - Architectural Foundation (Batches 0–12)

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
