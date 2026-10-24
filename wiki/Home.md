# Welcome to the Controllarr Wiki

Welcome to the official documentation and user guide for **Controllarr**, the automated console ROM and game library manager for retro and modern systems.

Controllarr brings the familiar, automated architecture of media managers into the console gaming ecosystem. Whether you are managing an 8-bit cartridge archive, organizing multidisc optical images, curating a clean **1 Game 1 ROM (1G1R)** library, verifying system BIOS files, or preparing an SD card for your favorite flash cart or handheld device, this wiki covers everything you need to know.

---

## 📚 Quick Navigation

### Getting Started
* **[Installation Guide](Installation-Guide.md)** — Setting up Controllarr on macOS, Linux, and Windows, configuring ports, reverse proxies, and database storage (SQLite / PostgreSQL).
* **[Quick Start Tutorial](Quick-Start.md)** — Step-by-step walkthrough from adding your first console platform (`/platforms`) to playing your first game.

### Library Organization & Curation
* **[1G1R & Curation Modes](1G1R-and-Curation-Modes.md)** — Detailed guide to the `/platforms` UI routes, Add/Edit Platform dialog curation options, Collection Strategy, Revision Preferences, and Platform Details version grouping.
* **[Supported Platforms & Regional Model](Supported-Platforms.md)** — Comprehensive list of supported home consoles, handhelds, short codes, and the 7-Region Model structure (USA, Europe, Japan, World, Homebrew, ROM Hacks, Prototypes).
* **[Core Concepts & Curation](Core-Concepts.md)** — Understanding 1G1R curation, region hierarchies, revision preferences, clone lists, and naming formats.
* **[Ingestion & Hash Verification](Ingestion-and-Hash-Verification.md)** — How Controllarr scans, verifies, and unpacks ROMs using No-Intro, Redump, and TOSEC DAT checksums (CRC32, MD5, SHA-1).
* **[Platform BIOS Matrix](Platform-BIOS-Matrix.md)** — Inspecting, hashing, and managing required system BIOSes and firmwares for optical and cartridge systems.

### Hardware Sync & Emulation
* **[Hardware Target Exporter](Hardware-Target-Exporter.md)** — Exporting curated libraries to SD cards for MiSTer FPGA, Analogue Pocket, OnionOS, GarlicOS, EverDrive, and Optical Drive Emulators (ODEs).
* **[In-Browser Play & Desktop Emulation](In-Browser-Play-and-Emulators.md)** — Using the Libretro web player, one-click desktop app hand-off (`controllarr://play/`), and RetroAchievements integration.

### Acquisition & Automation
* **[Acquisition & Indexers](Acquisition-and-Indexers.md)** — Setting up the 3-way acquisition engine: Internet Archive (HTTP/S3), Usenet (Newznab 1000/8000), and BitTorrent (Torznab).
* **[Download Clients](Download-Clients.md)** — Configuring Usenet (SABnzbd, NZBGet) and Torrent (qBittorrent, Transmission, Deluge) clients.

### Developers & Advanced Users
* **[REST API Reference](API-Reference.md)** — OpenAPI documentation for `/api/v3` and `/api/v5` endpoints, authentication, and webhooks.
* **[Troubleshooting & FAQ](Troubleshooting-and-FAQ.md)** — Resolving common errors, log analysis, database tuning, and performance optimizations.

---

## 🎯 Controllarr Philosophy

1. **Retail & Canonical First**: Every ROM and disc image is cross-referenced against official No-Intro, Redump, and TOSEC catalogs before categorization.
2. **Hardware Accuracy**: Directory layouts, filename standards, and FAT32-safe alphabetical splitting follow the proven **Hardware Target Game Database (HTGDB / SMDB)** specifications used across FPGA and flash-cart communities.
3. **Automated De-Duplication**: Solve ROM hoarding clutter by keeping only the single best version of each title based on customizable regional rules and revision criteria.
4. **Seamless Acquisition**: Three distinct download protocols (Direct HTTP archives, Usenet, and BitTorrent) prioritized according to your personal ranking.
5. **Zero Coding Required for Users**: An intuitive web UI providing rich IGDB artwork, screenshots, RetroAchievements badges, and one-click play functionality.
