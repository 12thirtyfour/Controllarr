# Controllarr Project Roadmap

> **Note**: Completed architectural phases and feature milestones are moved from this roadmap into [**CHANGELOG.md**](CHANGELOG.md) upon completion. For full release history and implementation details, see [**CHANGELOG.md**](CHANGELOG.md) and the [**Controllarr Wiki**](https://github.com/12thirtyfour/Controllarr/wiki).

---

## Completed Milestones (Moved to [CHANGELOG.md](CHANGELOG.md))

| Phase | Milestone Summary | Status | Reference |
| :--- | :--- | :---: | :--- |
| **Phase 1** | Foundation, .NET 10 Upgrade, Vite SPA Migration & Database Baselines (Migrations 237–240) | ✅ Completed | [1.0.0-alpha](CHANGELOG.md#100-alpha---architectural-foundation-batches-012) |
| **Phase 2** | HTGDB/SMDB Integration, ROM Ingestion Pipeline, Protocol Priority & Internet Archive S3/HTTP Client | ✅ Completed | [1.0.0-alpha](CHANGELOG.md#100-alpha---architectural-foundation-batches-012) |
| **Phase 3** | 1G1R Curation Engine, ROM Naming Presets, `/platforms` UI Modernization & Global Game Search | ✅ Completed | [1.0.0-beta.1](CHANGELOG.md#100-beta1---2026-10-23) / [1.0.0-beta.2](CHANGELOG.md#100-beta2---2026-10-23) |
| **Phase 4** | Platform BIOS Matrix (80+ entries + Auto-Downloader), `chdman`/`dolphin-tool` Compression & Virtual Symlink Libraries | ✅ Completed | [1.0.0-beta.2](CHANGELOG.md#100-beta2---2026-10-23) |
| **Phase 5** | Multi-Architecture Docker Packaging, Port `7050` Standardization & Official GitHub Wiki Suite | ✅ Completed | [1.0.0-beta.1](CHANGELOG.md#100-beta1---2026-10-23) / [1.0.0-beta.3](CHANGELOG.md#100-beta3---2026-10-24) |
| **Phase 6** | SD Card Multi-Device Exporter (15+ Hardware Targets), RetroAchievements Showcase & WebAssembly/Desktop Game Launcher | ✅ Completed | [1.0.0-beta.1](CHANGELOG.md#100-beta1---2026-10-23) |
| **Phase 7** | Brand Unification, AppData/DB Isolation (`~/.config/Controllarr`), 8-Region Drag-and-Drop Ordering, Strict Retail-Only 1G1R & HTTPS Activity Queue/History | ✅ Completed | [1.0.0-beta.2](CHANGELOG.md#100-beta2---2026-10-23) / [1.0.0-beta.3](CHANGELOG.md#100-beta3---2026-10-24) |

---

## Active Phase: Beta Verification & v1.0 Release Candidate

* **Multi-OS Live Beta Testing**:
  * Real-world library management, Usenet (`Newznab`), BitTorrent (`Torznab`), and HTTPS (`InternetArchive`) acquisition testing across macOS, Windows, and Linux.
  * Continuous UI/UX polish based on live beta testing feedback.
* **Physical Hardware & Flash-Cart Target Verification**:
  * End-to-end validation of **Virtual Symlink Libraries** (SMB/NFS network shares and local emulator mounts) on **MiSTer FPGA** and **RetroArch**.
  * End-to-end validation of **SD Card Exporter** folder layouts and FAT32 bucketing on physical **EverDrive / FXPak Pro**, **Analogue Pocket**, **OnionOS / GarlicOS** handhelds, and **Optical Drive Emulators** (`xStation`, `GDEMU`, `Fenrir`, `GC Loader`).
* **Binary & Host Executable Rename**:
  * Transition the compiled binary output name (`Sonarr` / `Sonarr.exe` -> `Controllarr` / `Controllarr.exe`) alongside existing `Controllarr` AppData, database, and service isolation for the `v1.0.0` Release Candidate.
* **v1.0.0 Release Candidate (RC) & Stable Distribution**:
  * Tagging `v1.0.0-rc.1` and publishing standalone multi-architecture release binaries and Docker images.

---

## Pitched Ideas & Future Enhancements (Post-1.0 Backlog)

* **Automated Multi-Disc `.m3u` Playlist Generation**:
  * Automatically generate `.m3u` playlist files referencing `Disc 1`, `Disc 2`, etc. inside multi-disc game folders during import and Virtual Library sync for seamless disc swapping in RetroArch, MiSTer, and Batocera.
* **Cryptographic Hash-Gated Romhack & Translation Patching Pipeline**:
  * Automatically apply `.ips`, `.bps`, and `.xdelta` translation or romhack patches on top of hash-verified retail base ROMs, outputting patched copies into the `Hacks & Translations` section without modifying the clean retail 1G1R file.
* **Scheduled DAT Catalog Auto-Updates**:
  * Background task to periodically pull updated No-Intro, Redump, and TOSEC DAT snapshots so newly dumped revisions, translations, and aftermarket releases appear automatically.
* **Emulation Frontend Metadata & Artwork Export**:
  * Automated generation of local `gamelist.xml` (EmulationStation / ES-DE / Batocera) and `.lpl` playlist files paired with downloaded box art, title screens, and marquees directly inside Virtual Libraries and exported SD cards.
* **Battery Save & Save State Synchronization**:
  * Optional bi-directional backup and synchronization of `.srm`/`.sav` battery saves and emulator save states across local emulators, network shares, and handheld SD cards.
