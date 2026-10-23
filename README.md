Disclaimer: This is both an experiment and project. I have no coding experience at all. Other than the work put into Sonarr, everything from the fork forward was done by AI. I am basically the project owner, Gemini is the manager and architect, and then Jules is the engineer and developer.

# Controllarr: Automated ROM Library Manager

Welcome to **Controllarr**, the automated ROM management and acquisition engine for retro and modern console gaming enthusiasts.

Controllarr brings the seamless, automated experience of modern media management to your game collection: pick the consoles you care about, set your collection rules, and let the system automatically find, verify, organize, and even launch your games.

## What Does Controllarr Do?

Managing game ROMs and disc images often means juggling different file formats, sorting through duplicates from multiple regions, manually extracting multi-part `.zip` or `.7z` archives, and tracking down missing BIOS files.

Controllarr automates this entire lifecycle:

* **Monitors Your Wishlist**: Add games individually, monitor complete console catalogs, or track curated regional lists.

* **Searches Multiple Sources Automatically**: Finds releases across direct HTTP archives (such as the Internet Archive), Usenet, and BitTorrent, prioritizing your preferred sources.

* **Verifies Dumps with Canonical Databases**: Automatically compares every downloaded or scanned file against official **No-Intro**, **Redump**, and **TOSEC** hash catalogs (CRC32, MD5, SHA-1). You know immediately whether a game is a clean, verified dump or an unverified bad rip.

* **Eliminates Duplicate Clutter (1G1R)**: If you only want one version of each game, Controllarr's **1 Game 1 ROM (1G1R)** engine automatically keeps your preferred regional release (e.g., USA over Europe or Japan) and revision, while archiving or ignoring duplicate regional clones.

* **Instant In-Browser Playback**: Built-in web emulation allows you to simply click **Play** on any verified ROM in your library to launch it immediately in your browser or through your desktop launcher.

* **Syncs to Real Hardware**: Export curated sets directly to SD cards formatted and organized specifically for flash carts and handhelds (such as EverDrive, MiSTer FPGA, Analogue Pocket, RetroArch, and OnionOS/Miyoo Mini).

## Universal Console Scope

Controllarr is dedicated specifically to video game consoles, handheld systems, and console hardware addons across every generation.

### Supported Systems

* **Home Consoles**: Nintendo (NES, SNES, N64, GameCube, Wii, Switch), Sega (Master System, Genesis, Saturn, Dreamcast), Sony PlayStation (PS1, PS2, PS3), Microsoft Xbox (Original, 360), NEC (TurboGrafx-16, PC-FX), SNK Neo Geo, Atari (2600 through Jaguar), 3DO, CD-i, and more.

* **Handhelds**: Game Boy, Game Boy Color, Game Boy Advance, Nintendo DS, Nintendo 3DS, PlayStation Portable (PSP), PlayStation Vita, Game Gear, Neo Geo Pocket, WonderSwan, and more.

* **Independent Add-on Systems**: Expansion hardware like the **Sega CD**, **Sega 32X**, **Famicom Disk System**, and **PC Engine CD-ROM²** are treated as distinct, first-class consoles with their own dedicated directory structures, BIOS requirements, and collection profiles.

*(Note: Controllarr focuses exclusively on console and handheld hardware. General computer architectures like PC/DOS, Commodore, and Amiga, as well as arcade sets like MAME, are not supported).*

## Key Features

### 1. Smart Collection Profiles: 1G1R vs. Completionist

Customize how each console in your collection is curated:

* **One Game, One ROM (1G1R)**: Keep only the single best version of each title. You define your region hierarchy (for example: `USA > Europe > Japan`) and revision preference (`Newest Revision` for bug-fixed releases or `Original Release` for launch fidelity). Controllarr filters out the rest.

* **Completionist**: Perfect for digital archivists. Automatically monitors, matches, and organizes every regional release, revision, and alternate edition.

### 2. Metadata & Artwork

Through integration with **IGDB**, Controllarr presents your library in rich visual detail:

* High-resolution cover art, title screens, and fan art.

* Genre categorization, release years, developer and publisher credits, storyline summaries, and community ratings.

* Filter and browse your collection by release year, genre, region, or completion status.

### 3. Native Hash-Based Ingestion & Archive Unpacking

Drop raw ROMs or multi-game archive sets into your library folder:

* Controllarr extracts `.zip`, `.7z`, and `.rar` archives on the fly.

* It calculates CRC32, MD5, and SHA-1 checksums and checks them directly against its embedded local DAT cache.

* Verified games are renamed, organized cleanly into `{Manufacturer}/{Console}` directory trees, and instantly matched with their artwork and metadata.

### 4. 3-Way Acquisition Priority

When searching for missing titles, Controllarr evaluates releases across three download protocols based on your custom ranking:

1. **HTTP / Direct Download**: Native retrieval from public archives (such as the Internet Archive) with built-in S3-compatible authentication and zero command-line setup.

2. **Usenet**: Seamless integration with your favorite NZB indexers and download clients.

3. **BitTorrent**: Full support for Torznab indexers and torrent clients.

### 5. Play Directly from Your Library (Planned Feature)

No need to manually locate files or configure an emulator just to try a game:

* **Web Player**: Launch lightweight console and handheld games directly inside your web browser using embedded Libretro cores.

* **Desktop App Hand-off**: Use one-click launcher links (`controllarr://play/{gameId}`) to automatically hand off disc images or heavier games to native emulators like RetroArch, DuckStation, or Dolphin on your PC or Mac.

### 6. Hardware Target Exporter (Planned Feature)

Take your collection on the go. Controllarr integrates layout profiles based on the **Hardware Target Game Database (HTGDB / SMDB)** to format and copy games directly onto flash carts and emulation handhelds:

* **FAT32-Safe Splits**: Automatically splits large alphabetized directories into `A-M/` and `N-Z/` subdirectories to prevent file-count limits on legacy hardware.

* **Preset Profiles**: Dedicated directory configurations for **MiSTer FPGA**, **Analogue Pocket**, **OnionOS (Miyoo Mini)**, **EverDrive**, and **FXPak Pro**.

## How It Works

```
┌────────────────────────────────────────────────────────┐
│                      Controllarr                       │
│                                                        │
│  1. Search & Ingestion                                 │
│     • HTTP (Internet Archive) / Usenet / BitTorrent    │
│     • Local Disk Scans (.zip, .7z, .iso, .chd)         │
│                           │                            │
│                           ▼                            │
│  2. Identification & Verification                      │
│     • CRC32 / MD5 / SHA-1 Hash Calculation             │
│     • Local SQLite DAT Cache (No-Intro / Redump)       │
│                           │                            │
│                           ▼                            │
│  3. Collection Strategy Rules                          │
│     • 1G1R Deduplication (Region & Revision Filter)    │
│     • Automatic File Renaming & Organization           │
│                           │                            │
│                           ▼                            │
│  4. Library & Playback                                 │
│     • In-Browser "Play Now" via Libretro WebAssembly   │
│     • Export to Flash-Carts & Handheld SD Cards        │
└────────────────────────────────────────────────────────┘

```

## Project Status & Active Development

Controllarr is currently in active development.

* **Completed**: Core .NET 10 architecture, database schema, local SQLite DAT caching engine, IGDB metadata scrapers, and 1G1R collection rules. Local Alpha Verification Gate #1 has passed.

* **In Progress**: Root folder regional folder naming (`{Manufacturer}/{Console}`), automatic extension-based scanning and hash matching, and 3-way download protocol priority.

* **Upcoming**: Web UI revamp with rich game cards and revision views, in-browser emulation playback, automatic disc image compression (`chdman`), and flash cart SD card export presets.
