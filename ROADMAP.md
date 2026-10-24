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
| **Phase 8** | Cross-Referenced ROM Verification Catalogs (`No-Intro`, `Redump`, `TOSEC`, `SmokeMonster HTGDB/SMDB`, `Neo Geo AES/CD`, `Switch NSWDB`), Decrypted NDS/3DS Default & `PreferEncryptedRoms` Toggle, Automated Download Cryptographic Hash Gate (`VerifiedRomHashSpecification`), Priority-Cascading Metadata Scrapers (`IGDB`, `ScreenScraper`, `TheGamesDB`, `Libretro/LaunchBox`) with API Rate-Limit Fail-Safes, `gamelist.xml` & SD Card Media Export, Local DAT/SMDB Import, Disc Compression (`.chd`/`.rvz`) & `.m3u` Generation, Archive Repackaging, Sidecar Save Mobility & Compiled Binary/Host Executable Rename (`Controllarr` / `Controllarr.exe` / `Controllarr.Console.exe` / `Controllarr.Update`) | ✅ Completed | [1.0.0-beta.7](CHANGELOG.md#100-beta7---2026-10-24) |

---

## Active Phase: Beta Verification & v1.0 Release Candidate

* **Multi-OS Live Beta Testing**:
  * Real-world library management, Usenet (`Newznab`), BitTorrent (`Torznab`), and HTTPS (`InternetArchive`) acquisition testing across macOS, Windows, and Linux.
  * Continuous UI/UX polish based on live beta testing feedback.
* **Physical Hardware & Flash-Cart Target Verification**:
  * End-to-end validation of **Virtual Symlink Libraries** (SMB/NFS network shares and local emulator mounts) on **MiSTer FPGA** and **RetroArch**.
  * End-to-end validation of **SD Card Exporter** folder layouts, media/`gamelist.xml` assets, and FAT32 bucketing on physical **EverDrive / FXPak Pro**, **Analogue Pocket**, **OnionOS / GarlicOS** handhelds, and **Optical Drive Emulators** (`xStation`, `GDEMU`, `Fenrir`, `GC Loader`).
* **Torznab / Newznab Game-Specific Indexer Attributes & Volume Factors**:
  * Extend `Torznab` and `Newznab` indexer definitions with game-specific category attributes (ROM container format hints, region tags, and download/upload ratio volume factors) for specialized private retro trackers.
* **Pre-Release Memory Profiling & Async Ingestion Optimization**:
  * Memory allocation profiling and async loop tightening across large multi-thousand-entry No-Intro / Redump / SmokeMonster SMDB catalog syncs and archive extraction pipelines.
* **v1.0.0 Release Candidate (RC) & Stable Distribution**:
  * Tagging `v1.0.0-rc.1` and publishing standalone multi-architecture release binaries and Docker images.

---

## Pitched Ideas & Future Enhancements (Post-1.0 Backlog)

* **Cryptographic Hash-Gated Romhack & Translation Patching Pipeline**:
  * Automatically apply `.ips`, `.bps`, and `.xdelta` translation or romhack patches on top of hash-verified retail base ROMs, outputting patched copies into the `Hacks & Translations` section without modifying the clean retail 1G1R file.
* **Scheduled DAT Catalog Auto-Updates**:
  * Background task to periodically pull updated No-Intro, Redump, and TOSEC DAT snapshots so newly dumped revisions, translations, and aftermarket releases appear automatically.
* **Battery Save & Save State Synchronization**:
  * Optional bi-directional backup and synchronization of `.srm`/`.sav` battery saves and emulator save states across local emulators, network shares, and handheld SD cards.
