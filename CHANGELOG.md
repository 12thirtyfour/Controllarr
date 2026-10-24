# Changelog

All notable changes to **Controllarr** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

- Multi-OS beta testing and telemetry triage across macOS, Windows, and Linux.
- Physical flash cart, handheld CFW, and ODE hardware target verification runs.

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
