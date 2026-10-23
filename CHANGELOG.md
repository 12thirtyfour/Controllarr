# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

- Local macOS, Linux, and Windows beta feedback and testing.
- Physical flash cart and ODE hardware validation runs.

## [1.0.0-beta.1] - 2026-10-23

### Added
- **SD Card Multi-Device Exporter Modal (`PR #46`)**: Full UI modal and backend service (`/api/v3/sdcardexport`, `/api/v5/sdcardexport`) supporting 15+ target hardware presets (MiSTer FPGA, Analogue Pocket, OnionOS, GarlicOS, ArkOS, EverDrive, ODEs, etc.) with automatic FAT32-safe directory bucketing (`A-M/`, `N-Z/`).
- **Platform BIOS Verification Matrix (`PR #47`)**: Interactive settings view and backend service (`/api/v3/bios`, `/api/v5/bios`) calculating CRC32/MD5/SHA-1 hashes to verify required and optional console firmwares.
- **RetroAchievements Integration (Issue #45)**: Account credential verification with live test feedback, visual badge showcase modal, game mastery tracking, and leaderboard rankings (`/api/v3/retroachievements`, `/api/v5/retroachievements`).
- **In-Browser Web Player & Game Launcher**: Libretro WebAssembly player and desktop launcher URI protocol handler (`controllarr://play/{gameId}`).
- **Dual API Exposure**: Full parity across all game controllers (`BiosController`, `SdCardExportController`, `RetroAchievementsController`, `GameLauncherController`, `GamePlatformController`, `GameSearchController`, `RomEntryController`) on both `/api/v3` and `/api/v5`.
- **Complete Visual Identity Overhaul**: Replaced all 38 branding assets (favicons, PNGs, SVGs, Apple touch icons, Windows tiles, `.ico` executables) generated from `Logo/master-logo.png`.

### Changed
- **Usenet Indexer Category Alignment**: Configured default search categories to `1000` (Console) and `8000` (Other/Misc) for Newznab Usenet indexers. Fixed `NewznabCategoryFieldOptionsConverter` which previously filtered out category `1000`, and removed legacy TV category overrides from presets. Scoped adjustments specifically to Usenet without altering torrent indexer defaults.
- **Branding & Localization**: Replaced user-facing Sonarr branding with Controllarr across `translate.ts`, `LocalizationService.cs`, `LoadingPage.tsx`, `login.html`, `logout.html`, `MoreInfo.tsx`, and `en.json`.
- **Build & Path Resolution Fixes**:
  - Fixed `src/Directory.Build.props` to use `$(MSBuildThisFileDirectory)stylecop.json` so StyleCop rules resolve correctly when compiling individual `.csproj` files directly.
  - Fixed `PathExtensions.GetUiFolder()` to resolve `../UI` (`_output/UI`) when running a local `Release` build from `_output/net10.0/`.
- **CI/CD Reliability**: Resolved all 35 integration test failures and workflow configuration issues, achieving 100% passing across all 33 CI matrix jobs (Linux x64/arm64/musl, macOS Apple Silicon & Intel, Windows x64/x86, FreeBSD, and PostgreSQL 16–18).

---

## [1.0.0-alpha] - Baseline

### Architectural Milestones

- **Batch 0 & 1:**
  - *What was changed:* Architectural pivot from TV to ROM collections. Renaming Series/Season/Episode domain concepts to GamePlatform/GameTitle/GameRelease.
  - *Why:* To natively track console ROM hierarchies instead of episodic television formats. Unified regional platforms chosen over distinct add-on platforms for simpler UI presentation.
- **Batch 2:**
  - *What was changed:* Implementation of IGDB client and Twitch OAuth.
  - *Why:* Replaces SkyHook/TheTVDB as the primary metadata source. Newtonsoft.Json retained over System.Text.Json for cross-solution Sonarr compatibility. Resolved Autofac DI registration via assembly scanning. Bypassed NLog warning assertions in test fixtures due to harness overrides.
- **Batch 3:**
  - *What was changed:* No-Intro/Redump/SmokeMonster parsing, deprecation of video resolutions in favor of dump verification flags, full-set archive detection, and gaming Torznab indexer classification.
  - *Why:* Transition the parsing and classification engine to handle standard console ROM release standards, archive packs, and full-set archives, and ensure query parameters to indexers target gaming/ROM categories instead of TV/Episode categories.
- **Batch 4:**
  - *What was changed:* Ingestion & Processing Pipeline including Archive Extraction/Repacking, Sidecar Save Syncing, CHDMAN disc conversions, System BIOS management, and Hardware Export Engine.
  - *Why:* To natively process incoming game archives, enforce canonical formats (Deflate level 9, stripping copier headers), safely backup sidecar save files alongside ROM moves, provide a framework for exporting directly to Flash Carts or Handheld setups, and ensure system firmware exists and is verified for emulation pipelines.
- **Batch 5:**
  - *What was changed:* Controllarr baseline and container infrastructure setup.
  - *Why:* Establish new base for the project.
- **Batch 6:**
  - *What was changed:* Ingestion & Standardization Engine updates.
  - *Why:* Improved ROM parsing stability and expanded processing capabilities.
- **Batch 7:**
  - *What was changed:* Existing Library Scanner & Disk Importer.
  - *Why:* Implements unmanaged disk scanning capable of analyzing deeply nested or bucketed folder structures. Integrates ROM hash inspection directly within .zip central directories (avoiding costly full extractions), enabling non-destructive or destructive import pipelines (Copy vs Move). Sidecar save files are now properly synchronized in lockstep with canonical ROM moves/renames to prevent data loss.
- **Batch 8:**
  - *What was changed:* Search Engine, Ingestion Dispatch, Core React UI, and Collection Strategy Engine.
  - *Why:* Implementation of Three-Tiered Collection Strategy Engine (1G1R vs Preservationist).
- **Batch 12:**
  - *What was changed:* Added Internet Archive S3-style authenticated HTTP downloads (`LOW access:secret`), masked password settings for IA S3 keys with configurable anonymous fallback, enforced CA1849 non-blocking async streaming and CA5389 path protection on IA ingest pipelines, and added comprehensive offline unit test coverage in InternetArchiveFixture.cs.
  - *Why:* To securely fetch authenticated IA assets with stream optimizations and robust path traversal mitigations.
