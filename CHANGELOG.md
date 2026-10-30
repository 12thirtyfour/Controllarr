# Changelog

All notable changes to **Controllarr** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Recursive Library Import & Zip Collection Support**:
  - Expanded `UnmanagedDiskScanner` and `ManualImportService` to recursively traverse directories and zip archives of any depth.
  - Added support for full multi-ROM collection archives (such as `@NES2.0.zip`, `1G1R.zip`, and platform packs): Controllarr now inspects archive central directories via $O(1)$ entry headers and extracts individual `ManualImportItem` entries for each ROM inside using `${zipPath}#${entryName}` sub-path notation.
  - Implemented automatic extraction on import: when an item from a collection archive is imported, Controllarr unpacks the target ROM to a clean temporary path and imports the raw ROM into the library folder.
  - Single-ROM archives (`Game.zip`) and multi-track disc archives (containing `.cue` / `.gdi` or multiple bin tracks) are detected and handled as unified single-game archives.
- **Hash-Based Platform Autodetection & DAT Cache Lookup**:
  - Implemented `FindPlatformByHash` on `IDatCacheService` and `DatCacheService` to look up entry hashes (CRC32, SHA1, MD5, SHA256) across loaded No-Intro, Redump, and TOSEC DATs, resolving the canonical platform slug and matching game title.
  - Added fallback platform resolution in `UnmanagedDiskScanner` and `ManualImportService` using `PlatformCompatibilityValidator.GetPlatformByExtension` for exclusive platform extensions (e.g. `.nes`, `.sfc`, `.z64`, `.gba`, `.3ds`, `.md`), directory path segments, and platform tags.
- **Root Folder & Import Screen Region Folder Isolation**:
  - Fixed `RootFolderService.GetUnmappedFolders` to ignore regional and bucket directories (`USA`, `Europe`, `Japan`, `World`, `No-Intro`, `Redump`, `1G1R`, `Packs`, etc.) from being listed as standalone platform candidates on `/add/import`.
  - Added automatic platform detection for root folders containing loose ROMs or collection archives directly.
- **Non-Game File Filtering**:
  - Cleaned up `MediaFileExtensions.cs` to eliminate all video streaming and TV extensions (`.mkv`, `.mp4`, `.avi`, `.wmv`, `.mov`, `.flv`, `.webm`, `.ts`, `.m2ts`, `.rm`, `.strm`, etc.) from being accepted as game files.
  - Retained and expanded genuine console ROM, optical disc, archive, and patch extensions across `FileExtensions` and `RomExtensions`.
- **BIOS Archive Extraction & Verification**:
  - Updated `BiosService.VerifyBios` to inspect compressed archives (`.zip`, `.gz`, `.7z`) and verify the uncompressed raw BIOS payload against expected MD5 and SHA1 checksums using an in-memory stream buffer to ensure accurate multi-hash evaluation across both algorithms.
  - Fixed `ScanAndVerifyBiosDirectory` and `TryDownloadAndVerifyBios` to extract raw BIOS files from archives and save them with their canonical uncompressed filename rather than overwriting archives with mismatched extensions.
- **TV & Video Release Filtering across Indexers & Decision Engine**:
  - Added `PlatformCompatibilityValidator.IsTvOrVideoRelease` regex checks to filter out video formats (`.mkv`, `.mp4`, `.avi`, `.wmv`), TV season/episode tags (`S01E01`, `1x01`), video codecs, and anime release groups from Newznab and Torznab RSS parser feeds, automated downloads, and interactive searches.
  - Updated `DownloadDecisionMaker` to parse title-only game releases and multi-game collections correctly when standard TV season/episode regexes return title-level results.
- **Archive Extraction on Disk Scan**:
  - Updated `DiskScanService.ExtractCollectionArchives` with safe file disposal handling before deleting extracted collection zip archives, enabling "Refresh & Scan ROMs" to unpack and scan multi-ROM packs and loose ROMs into library platform folders.

### Added
- **Internet Archive Platform Compatibility Filtering**:
  - Created `PlatformCompatibilityValidator` with platform-exclusive extension mapping (30+ console platforms), cartridge vs. disc isolation, folder path hierarchy analysis, and archive metadata validation.
  - Integrated platform-aware filtering directly into `InternetArchiveRequestGenerator` and `InternetArchiveParser`, preventing cross-platform file bleed when scraping multi-system archive collections.
  - Cartridge platforms are strictly guarded against optical disc images (`.iso`, `.chd`, `.rvz`, `.wbfs`, `.wua`, etc.), ensuring only valid game formats are accepted.
- **Interactive Search Format Badging**:
  - Replaced the legacy Sonarr "Quality" column in the Interactive Search table with a dedicated "Format" column displaying uppercase file extension badges (`.ZIP`, `.7Z`, `.CHD`, `.ISO`, `.SFC`, `.NES`, `.3DS`, `.CIA`, etc.) to quickly identify release packaging.
  - Exposed `Format` across API release resources in `Controllarr.Api.V5` and `Controllarr.Api.V3`.
  - Added filter and sorting support for `Format` in frontend release table stores.

### Fixed
- **Library Platform Duplication Resolution (closes #74)**:
  - Fixed `AddSeriesService.AddSeries` (both single platform add and batch import) to query existing platforms by `TvdbId`, `Title`, `TitleSlug`, and `Path` before adding entries.
  - When importing a platform from `/add/import` or adding a console that already exists in the library, Controllarr now returns the existing platform instead of creating duplicate database records.
- **Accurate BIOS Cryptographic Verification & Hash Audit (closes #84)**:
  - Corrected expected SHA-1 hashes in `BiosService.KnownBiosFiles` for Mattel Intellivision (`exec.bin`, `grom.bin`), Fairchild Channel F (`sl31253.bin`, `sl31254.bin`), Sega Master System (`bios_J.sms`), and Sony PSP (`ppge_atlas.zim`), resolving false-positive verification rejections on authentic BIOS dumps.
  - Fixed in-memory stream buffering in `VerifyBios` so multiple hash algorithms (MD5 and SHA-1) are computed from fresh stream positions, preventing empty-stream SHA-1 mismatches during compressed archive inspection.
- **Newznab Indexer Game Matching & TV/Anime Bleed Elimination**:
  - Fixed `NewznabRequestGenerator` to sanitize category queries, stripping non-game categories (5000-5999 TV, 2000-2999 Movies, 3000-3999 Music, 7000-7999 Books) and defaulting to console and PC game categories (`1000-1999`, `8000-8999`).
  - Added `FilterNonGameCategories` setting on Newznab indexers to drop unwanted TV and anime releases from indexer feeds and multi-category responses.
  - Cleaned search query terms with `CleanGameTitleForSearch` to strip parenthetical region and language catalog tags from game titles, routing queries cleanly through `t=search`.
  - Enhanced title comparison in `DownloadDecisionMaker` to replace dot/underscore separators with spaces and strip scene group suffixes (`-GROUP`), enabling scene releases (e.g., `Pokemon Ultra Sun USA 3DS-SUXXORS`) to match monitored library games.
  - Integrated `PlatformCompatibilityValidator.IsCompatible` into `SeriesSpecification` to reject incompatible file formats during interactive and automated searches.

### Fixed
- **Media Management & Naming Settings Crash Resolution**:
  - Resolved runtime `TypeError: Cannot read properties of undefined (reading 'value')` on the Media Management page by aligning backend DTOs (`NamingSettingsResource`, `NamingConfigResource`, `MediaManagementSettingsResource`, `MediaManagementConfigResource`) with video game naming conventions (`standardGameFormat`, `dailyGameFormat`, `renameGames`, `multiGameStyle`, `gameTitleRequired`, `autoUnmonitorPreviouslyDownloadedGames`) while retaining full backwards compatibility.
  - Implemented null-safe chaining and fallback property resolution in `frontend/src/Settings/MediaManagement/Naming/Naming.tsx`, `frontend/src/Settings/MediaManagement/MediaManagement.tsx`, `useNamingSettings.ts`, and `useMediaManagementSettings.ts`.
  - Fixed TypeScript token parameter typing in `OrganizePreviewModalContent.tsx`.
- **Yarn Engine Check & Warning Clean-up**:
  - Added `ignore-engines true` in `.yarnrc` to eliminate package engine warnings (`chart.js: The engine "pnpm" appears to be invalid`) during `yarn install`.
  - Updated root `package.json` metadata with Controllarr project name, repository, and author.

### Added
- **Centralized Build Output & Canonical Solution Cleanup**:
  - Registered `Controllarr` project prefix in `src/Directory.Build.props` so all core, host, and console executable and library binaries output directly to `_output/net10.0` (and `_tests/net10.0` for test fixtures), resolving execution errors caused by missing binaries where only `Controllarr.Update` was being placed into `_output`.
  - Removed obsolete leftover `src/Sonarr.sln` file from the repository, ensuring all builds and tooling target `src/Controllarr.sln`.
- **Frontend Build Optimization & Static Asset Relocation**:
  - Relocated static web assets (`Fonts/`, `Images/`, `manifest.json`, `browserconfig.xml`) from `frontend/src/Content` to `frontend/public/Content`, eliminating Vite's build-time warning for `/Content/Fonts/fonts.css`.
  - Configured `chunkSizeWarningLimit: 3500` in `vite.config.ts` to suppress chunk-size warnings on production bundles.
- **Complete Elimination of TV & Sonarr Terminology in Backend API Controllers & Models**:
  - Renamed all controllers in `Controllarr.Api.V3` and `Controllarr.Api.V5`: `PlatformController`, `GameController`, `RomFileController`, `PlatformPassController`, `GameControllerWithSignalR`, `RenameGameController`, `PlatformLookupController`, `PlatformFolderController`, `PlatformImportController`, and `PlatformEditorController`.
  - Renamed all API resource classes, mappers, and validators to video game domain terminology: `PlatformResource`, `GameResource`, `RomFileResource`, `RomFileListResource`, `GamesMonitoredResource`, `RenameGameResource`, `ParsedGameInfoResource`, `PlatformPassResource`, `PlatformPassPlatformResource`, `RegionResource`, `RegionStatisticsResource`, `RegionTypeResource`, `PlatformEditorResource`, `PlatformEditorDeleteResource`, `PlatformEditorValidator`, `PlatformFolderResource`, `PlatformFolderAsRootFolderValidator`, `PlatformStatisticsResource`, `PlatformTranslationResource`, `PlatformRegionTypeValidator`, `GameSubresource`, and `PlatformSubresource`.
- **Eradication of TV Terminology Across Test Suites (`Controllarr.Core.Test` & `Controllarr.Integration.Test`)**:
  - Renamed over 93 test fixtures and test client helpers across `OrganizerTests`, `ParserTests`, `MediaFiles`, `DecisionEngineTests`, `HealthCheck`, `Housekeeping`, `IndexerSearchTests`, `IndexerTests`, and `Integration.Test` to use `Platform`, `Game`, `Region`, and `RomFile` domain names.
- **Core Models, Validators & Statistics Renaming**:
  - Renamed core domain classes in `src/NzbDrone.Core`: `PlatformPathValidator`, `PlatformExistsValidator`, `PlatformAncestorValidator`, `PlatformNotFoundException`, `MultiplePlatformsFoundException`, `PlatformTitleSlugValidator`, `PlatformTitleNormalizer`, `ShouldRefreshPlatform`, `PlatformPathBuilder`, `PlatformStatusType`, `DailyPlatform`, `DailyPlatformDataProxy`, `DailyPlatformService`, `PlatformStatistics`, `RegionStatistics`, `PlatformStatisticsRepository`, and `PlatformStatisticsService`.
- **Decoupling from GitHub Fork Network & Upstream Attribution Affirmation**:
  - Decoupled the repository from GitHub's upstream fork network to establish Controllarr as an independent standalone project for console and retro game preservation.
  - Reaffirmed permanent credits and attribution to the Sonarr project and its contributors in [`README.md`](README.md) and [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
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
- **BIOS Archive Extraction Prior to Cryptographic Hash Verification**:
  - Implemented automatic extraction of downloaded compressed BIOS archives (`.zip`, `.gz`, `.7z`) into a staging folder before hashing, verifying the uncompressed raw binary against target MD5 and SHA-1 checksums and eliminating rejection errors caused by hashing outer archive containers.
- **Automatic Indexer Preset Naming**:
  - Selecting an indexer schema preset (e.g. NZBgeek) now automatically populates the indexer name input with the selected preset name.
- **ROM Patcher Direct Library Import Flow**:
  - Successfully patched ROM files are now passed through the standard downloaded media import pipeline (`IDownloadedEpisodesImportService`), matching existing platform games, setting IDs, and recording file paths directly before falling back to direct registration.

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

### Changed
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

### Changed
- **Sonarr / TV Terminology Purge (Logs, Rejections, UI Strings)**:
  - Replaced TV-centric wording (series / season / episode / aired) with Controllarr wording (platform / region / game / released / ROM file) across download decision rejections, search-match specifications, tracked-download and import messages, disk-scan, rename, move, refresh, monitoring, and parsing logs.
  - Startup/shutdown logs and startup exceptions now report `Controllarr` instead of `Sonarr`; the "Tracking" debug line reports `ControllarrStage`.
  - Frontend now sends an `X-Controllarr-Client` header (the backend still accepts the legacy `X-Sonarr-Client` header).
  - Password reset hints reference `CONTROLLARR__AUTH__RESETTOKEN`; Vite dev-server errors reference `CONTROLLARR_VITE_DEV_SERVER`.
  - Added `CONTROLLARR_PROCESS_NAME` / `CONTROLLARR_CONSOLE_PROCESS_NAME` constants (legacy `SONARR_*` names kept as aliases).
  - Removed the unused `piwikCheck.js` analytics stub that pointed at `piwik.sonarr.tv`.
  - Fixed English UI strings such as "Platforms Type", "Unknown Platforms Items", "Midseason Finale", and region-scoped modal headers.

### Added
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

### Fixed
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

### Changed
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

---

## [1.0.0-beta.7] - 2026-10-24

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
