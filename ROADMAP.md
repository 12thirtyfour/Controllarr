# Controllarr Project Roadmap

> **Note**: Completed architectural phases and feature milestones are moved from this roadmap into [**CHANGELOG.md**](CHANGELOG.md) upon completion. For full release history and implementation details, see [**CHANGELOG.md**](CHANGELOG.md) and the [**Controllarr Wiki**](https://github.com/12thirtyfour/Controllarr/wiki).

---

## Completed Milestones (Moved to [CHANGELOG.md](CHANGELOG.md))

| Phase | Milestone Summary | Status | Reference |
| :--- | :--- | :---: | :--- |
| **Phase 1** | Foundation, .NET 10 Upgrade, Vite SPA Migration & Database Baselines (Migrations 237–240) | ✅ Completed | [1.0.0-alpha](CHANGELOG.md#100-alpha---architectural-foundation-batches-012) |
| **Phase 2** | HTGDB/SMDB Integration, ROM Ingestion Pipeline, Protocol Priority & Internet Archive S3/HTTP Client | ✅ Completed | [1.0.0-alpha](CHANGELOG.md#100-alpha---architectural-foundation-batches-012) |
| **Phase 3** | Flexible Collection Curation Engine (1G1R & Completionist), Unified Path Naming, `/platforms` UI Modernization & Global Game Search | ✅ Completed | [1.0.0-beta.1](CHANGELOG.md#100-beta1---2026-10-23) / [1.0.0-beta.4](CHANGELOG.md#100-beta4---2026-10-24) |
| **Phase 4** | Platform BIOS Files (Strict Add-On Isolation + Auto-Downloader under `Library -> BIOS Files`), `chdman`/`dolphin-tool` Compression & Virtual Symlink Libraries | ✅ Completed | [1.0.0-beta.2](CHANGELOG.md#100-beta2---2026-10-23) / [1.0.0-beta.6](CHANGELOG.md#100-beta6---2026-10-24) |
| **Phase 5** | Multi-Architecture Docker Packaging, Port `7050` Standardization & Official GitHub Wiki Suite | ✅ Completed | [1.0.0-beta.1](CHANGELOG.md#100-beta1---2026-10-23) / [1.0.0-beta.3](CHANGELOG.md#100-beta3---2026-10-24) |
| **Phase 6** | SD Card Multi-Device Exporter (15+ Hardware Targets), Live RetroAchievements API Integration & WebAssembly/Desktop Game Launcher | ✅ Completed | [1.0.0-beta.1](CHANGELOG.md#100-beta1---2026-10-23) / [1.0.0-beta.4](CHANGELOG.md#100-beta4---2026-10-24) |
| **Phase 7** | Brand Unification, AppData/DB Isolation (`~/.config/Controllarr`), Database Table Renames (`Migration 242`), Built-In GitHub Releases Updater, Collection Settings, Regional Console Themes & HTTPS Activity Queue/History | ✅ Completed | [1.0.0-beta.2](CHANGELOG.md#100-beta2---2026-10-23) / [1.0.0-beta.6](CHANGELOG.md#100-beta6---2026-10-24) |
| **Phase 8** | Cross-Referenced ROM Verification Catalogs (`No-Intro`, `Redump`, `TOSEC`, `SmokeMonster HTGDB/SMDB`, `Neo Geo AES/CD`, `Switch NSWDB`), 3-Way `EncryptionPreference` (`NotEncrypted`, `Encrypted`, `Both`) & Encrypted Version Grouping, Automated Download Cryptographic Hash Gate (`VerifiedRomHashSpecification`), Priority-Cascading Metadata Scrapers (`IGDB`, `ScreenScraper`, `TheGamesDB`, `Libretro/LaunchBox`) with API Rate-Limit Fail-Safes, `gamelist.xml` & SD Card Media Export, Local DAT/SMDB Import, Scheduled Daily DAT Catalog Auto-Updates (`UpdateDatCatalogsCommand`), Cryptographic Hash-Gated `.ips`/`.bps`/`.xdelta` Romhack & Translation Patching Pipeline (`RomPatchingService`), Disc Compression (`.chd`/`.rvz`) & `.m3u` Generation, Archive Repackaging, Sidecar Save Mobility, Compiled Binary Rename (`Controllarr`) & Production Multi-Arch `Dockerfile` + `docker-compose.yml` | ✅ Completed | [1.0.0-beta.7](CHANGELOG.md#100-beta7---2026-10-24) |

---

## Active Phase: Beta Verification & v1.0 Release Candidate

* **Console Theme Overhauls, Live Preview & Dynamic Per-Platform Theming**:
  * **Alphabetized Theme Dropdown**: Keep `Auto`, `Light`, and `Dark` at the top of the selector in `Settings -> UI`, followed by all console themes strictly in alphabetical order for effortless scanning.
  * **Instant Live Preview**: Switching the theme dropdown in `Settings -> UI` dynamically applies styles on the fly for immediate preview without persisting to database until the user clicks Save (reverting on cancel or navigation).
  * **Dynamic Per-Console Theme Matching**: Add a checkbox setting in `Settings -> UI` (between the Theme selector and Color Impaired toggle) to automatically apply the corresponding console theme when viewing that platform's details page, reverting to the user's global default theme on all other pages.
  * **Theme Palette Redesigns**:
    * **Nintendo Famicom (1983)**: Replace high-contrast harsh red with iconic Famicom palette—deep maroon/burgundy shell accents, warm ivory/cream body, gold controller faceplates, and charcoal accents.
    * **Nintendo Game Boy DMG-01 (1989)**: Evolve beyond monolithic monochrome green to incorporate the warm off-white/gray DMG shell, dark slate-blue screen lens bezel, magenta/burgundy button accents, and olive dot-matrix LCD matrix accents.
    * **Microsoft Xbox 360 (2005)**: Redesign as a crisp light theme based on the original 2005 matte white console chassis, Ring-of-Light emerald blade greens, chrome accents, and subtle 4-color button highlights (green, red, blue, yellow).
    * **Controllarr Brand Default Themes**: Differentiate default Dark and Light themes from upstream Sonarr styling with Controllarr brand aesthetics.
* **Disc-Based Game Folder Structure & Path Customization (`Settings -> Media Management`)**:
  * Dedicated option to house disc-based titles (even single-disc games) inside their own game-titled subfolder (e.g. `{Platform}/{Region}/{Game Title}/{Game Title}.chd` or `{Game Title} (Disc 1).chd`) alongside companion `.m3u` playlists, multi-track `.cue`/`.bin` files, and sidecar saves, keeping region folders neat and preventing multi-file disc clutter.
* **Settings Save Confirmation & Visual Feedback (Toast / Checkmark)**:
  * Display a prominent green success toast notification or temporary checkmark animation when saving settings across all Settings pages instead of only silently transitioning the toolbar button to a disabled "No Changes" state.
* **Navigation & Leftover Servarr UI Pruning**:
  * Remove Servarr-era artifacts that do not apply to console ROM collections (e.g., "Cutoff Unmet" filter views and the TV-centric Calendar, unless backed by verified aftermarket/homebrew release schedules).
* **First-Run Setup & Guided Onboarding Wizard (Pre-v1.0 Milestone)**:
  * Interactive first-run setup wizard launching on initial boot before handing off to the dashboard:
    1. Authentication & Login Security (Forms, Basic, or Disabled).
    2. Primary Root Folder configuration for game libraries.
    3. Default Metadata Scrapers enablement (ScreenScraper, IGDB, Libretro Thumbnails).
    4. Default Acquisition Indexer (Internet Archive HTTP client enabled out-of-the-box).
* **Multi-OS Live Beta Testing**:
  * Real-world library management, Usenet (`Newznab`), BitTorrent (`Torznab`), and HTTPS (`InternetArchive`) acquisition testing across macOS, Windows, and Linux.
  * Continuous UI/UX polish based on live beta testing feedback.
* **Physical Hardware & Flash-Cart Target Verification**:
  * End-to-end validation of **Virtual Symlink Libraries** (SMB/NFS network shares and local emulator mounts) on **MiSTer FPGA** and **RetroArch**.
  * End-to-end validation of **SD Card Exporter** folder layouts, media/`gamelist.xml` assets, and FAT32 bucketing on physical **EverDrive / FXPak Pro**, **Analogue Pocket**, **OnionOS / GarlicOS** handhelds, and **Optical Drive Emulators** (`xStation`, `GDEMU`, `Fenrir`, `GC Loader`).
  * **UI Navigation Integration**: Place the dedicated SD Card Exporter page in the left sidebar under the **Library** menu directly below **BIOS Files**, labeled simply as **"Export"** (`/export`).
* **Torznab / Newznab Game-Specific Indexer Attributes & Volume Factors**:
  * Extend `Torznab` and `Newznab` indexer definitions with game-specific category attributes (ROM container format hints, region tags, and download/upload ratio volume factors) for specialized private retro trackers.
* **v1.0.0 Release Candidate (RC) & Stable Distribution**:
  * Tagging `v1.0.0-rc.1` and publishing standalone multi-architecture release binaries and Docker images.

---

## Pitched Ideas & Future Enhancements (Post-1.0 Backlog)

* **Battery Save & Save State Synchronization**:
  * Optional bi-directional backup and synchronization of `.srm`/`.sav` battery saves and emulator save states across local emulators, network shares, and handheld SD cards.
