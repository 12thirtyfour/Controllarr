# Controllarr Project Roadmap

> **Note**: Completed architectural phases and feature milestones are moved from this roadmap into [**CHANGELOG.md**](CHANGELOG.md) upon completion. For full change history and implementation details, see [**CHANGELOG.md**](CHANGELOG.md) and the [**Controllarr Wiki**](https://github.com/12thirtyfour/Controllarr/wiki).

---

## Completed Milestones (Moved to [CHANGELOG.md](CHANGELOG.md))

| Phase | Milestone Summary | Status | Reference |
| :--- | :--- | :---: | :--- |
| **Phase 1** | Foundation, .NET 10 Upgrade, Vite SPA Migration & Database Baselines (Migrations 237–240) | ✅ Completed | [2026-10-22](CHANGELOG.md#2026-10-22---architectural-foundation-batches-012) |
| **Phase 2** | HTGDB/SMDB Integration, ROM Ingestion Pipeline, Protocol Priority & Internet Archive S3/HTTP Client | ✅ Completed | [2026-10-22](CHANGELOG.md#2026-10-22---architectural-foundation-batches-012) |
| **Phase 3** | Flexible Collection Curation Engine (1G1R & Completionist), Unified Path Naming, `/platforms` UI Modernization & Global Game Search | ✅ Completed | [2026-10-23](CHANGELOG.md#2026-10-23) / [2026-10-24](CHANGELOG.md#2026-10-24) |
| **Phase 4** | Platform BIOS Files (Strict Add-On Isolation + Auto-Downloader under `Library -> BIOS Files`), `chdman`/`dolphin-tool` Compression & Virtual Symlink Libraries | ✅ Completed | [2026-10-23](CHANGELOG.md#2026-10-23) / [2026-10-24](CHANGELOG.md#2026-10-24) |
| **Phase 5** | Multi-Architecture Docker Packaging, Port `7050` Standardization & Official GitHub Wiki Suite | ✅ Completed | [2026-10-23](CHANGELOG.md#2026-10-23) / [2026-10-24](CHANGELOG.md#2026-10-24) |
| **Phase 6** | SD Card Multi-Device Exporter (15+ Hardware Targets), Live RetroAchievements API Integration & WebAssembly/Desktop Game Launcher | ✅ Completed | [2026-10-23](CHANGELOG.md#2026-10-23) / [2026-10-24](CHANGELOG.md#2026-10-24) |
| **Phase 7** | Brand Unification, AppData/DB Isolation (`~/.config/Controllarr`), Database Table Renames (`Migration 242`), Built-In GitHub Releases Updater, Collection Settings, Regional Console Themes & HTTPS Activity Queue/History | ✅ Completed | [2026-10-23](CHANGELOG.md#2026-10-23) / [2026-10-24](CHANGELOG.md#2026-10-24) |
| **Phase 8** | Cross-Referenced ROM Verification Catalogs (`No-Intro`, `Redump`, `TOSEC`, `SmokeMonster HTGDB/SMDB`, `Neo Geo AES/CD`, `Switch NSWDB`), 3-Way `EncryptionPreference` (`NotEncrypted`, `Encrypted`, `Both`) & Encrypted Version Grouping, Automated Download Cryptographic Hash Gate (`VerifiedRomHashSpecification`), Priority-Cascading Metadata Scrapers (`IGDB`, `ScreenScraper`, `TheGamesDB`, `Libretro/LaunchBox`) with API Rate-Limit Fail-Safes, `gamelist.xml` & SD Card Media Export, Local DAT/SMDB Import, Scheduled Daily DAT Catalog Auto-Updates (`UpdateDatCatalogsCommand`), Cryptographic Hash-Gated `.ips`/`.bps`/`.xdelta` Romhack & Translation Patching Pipeline (`RomPatchingService`), Disc Compression (`.chd`/`.rvz`) & `.m3u` Generation, Archive Repackaging, Sidecar Save Mobility, Compiled Binary Rename (`Controllarr`) & Production Multi-Arch `Dockerfile` + `docker-compose.yml` | ✅ Completed | [2026-10-24](CHANGELOG.md#2026-10-24) |
| **Phase 9** | Complete Controllarr Unification & Upstream Detachment: .NET Project & Solution Renaming (`Controllarr.*`), Total Elimination of TV/Sonarr Filenames & Modules across React/Vite Frontend (`Platform/`, `Game/`, `RomFile/`, `Region/`), Backend API Controllers & Models (`PlatformController`, `GameController`, `RomFileController`, `PlatformResource`, etc.), Over 93+ Test Fixtures Renamed to Video Game Domain Terminology, Dedicated ROM Patcher Menu (`/patcher`) with Automatic Import Matching, "Search for Collection" Platform Search, Partial Date Precision Handling, Internet Archive Full Package Acquisition & Multi-OS Linux Source Build Standards | ✅ Completed | [2026-10-25](CHANGELOG.md#2026-10-25) - [2026-11-04](CHANGELOG.md#2026-11-04) |

---

## Active Phase: Alpha Baseline Stabilization & Beta Readiness Gate

Controllarr is currently in active **Alpha** development. Changes are tracked chronologically by **Date** in [**CHANGELOG.md**](CHANGELOG.md).

### Baseline Feature Checklist (Beta Readiness Gate)
To advance from Alpha to Beta testing, the following core capabilities must work reliably across both full collections and single ROMs, supporting all three download types (**Usenet**, **BitTorrent**, and **Internet Archive**):
1. **Library Creation & Management**: Adding console platforms, configuring root paths, and initializing platform folders.
2. **Library Import**: Scanning local directories, extracting archives, and identifying games against DAT/SMDB catalogs.
3. **Indexing**: Ingesting releases from configured indexers with clean console categorization and zero TV/PC bleed.
4. **Searching**: Automated and interactive searching for single games and entire collections.
5. **Downloading**: Reliable queue dispatch to clients (SABnzbd, NZBGet, qBittorrent, Transmission, etc.) and Internet Archive HTTP downloader.
6. **Cryptographic Verification**: Post-download hash verification (CRC32, MD5, SHA-1) against authentic DAT databases (No-Intro, Redump, TOSEC, SMDB).
7. **Library Import & Renaming**: Moving verified files to their correct destination directory on disk with authentic game and disc naming.

---

## Upcoming Phases

### Phase: Beta Feature Verification & Multi-Platform Testing
*Once baseline features are verified, advance to Beta to test all features end-to-end:*
* **First-Run Setup & Guided Onboarding Wizard**:
  * Interactive first-run setup wizard launching on initial boot before handing off to the dashboard (Authentication, Root Folders, Metadata Scrapers, Indexers).
* **Multi-OS Live Testing**:
  * Real-world testing across macOS, Windows, Linux, and Docker for Usenet (`Newznab`), BitTorrent (`Torznab`), and HTTPS (`InternetArchive`).
  * Continuous UI/UX polish based on live testing feedback.
* **Physical Hardware & Flash-Cart Target Verification**:
  * End-to-end validation of **Virtual Symlink Libraries** (SMB/NFS network shares and local emulator mounts) on **MiSTer FPGA** and **RetroArch**.
  * End-to-end validation of **SD Card Exporter** folder layouts, media/`gamelist.xml` assets, and FAT32 bucketing on physical **EverDrive / FXPak Pro**, **Analogue Pocket**, **OnionOS / GarlicOS** handhelds, and **Optical Drive Emulators** (`xStation`, `GDEMU`, `Fenrir`, `GC Loader`).
  * Place dedicated SD Card Exporter in the sidebar under **Library -> Export** (`/export`).
* **Torznab / Newznab Game-Specific Indexer Attributes & Volume Factors**:
  * Extend `Torznab` and `Newznab` indexer definitions with game-specific category attributes (ROM container format hints, region tags, and download/upload ratio volume factors) for specialized private retro trackers.

### Phase: Release Candidate (RC) & Production Distribution
*Once features work fully as expected in Beta, advance to Release Candidate:*
* **v1.0.0 Release Candidate (RC)**:
  * Create pre-release tag `v1.0.0-rc.1` to build and validate all 10 OS/arch binary distributions and multi-arch Docker image (`ghcr.io/12thirtyfour/controllarr`) via the production release pipeline.
* **Stable Production Release (v1.0.0)**:
  * Merge validated RC state into `main` and publish official `v1.0.0` release.

---

## Pitched Ideas & Future Enhancements (Post-1.0 Backlog)

* **Battery Save & Save State Synchronization**:
  * Optional bi-directional backup and synchronization of `.srm`/`.sav` battery saves and emulator save states across local emulators, network shares, and handheld SD cards.
