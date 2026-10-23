<p align="center">
  <img src="Logo/256.png" alt="Controllarr" width="160" />
</p>

<h1 align="center">Controllarr</h1>

<p align="center">
  <strong>The automated ROM library manager and acquisition engine for retro and modern game consoles.</strong>
</p>

<p align="center">
  <a href="LICENSE.md"><img src="https://img.shields.io/badge/license-GPL--3.0-blue.svg" alt="License: GPL-3.0" /></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-purple.svg" alt=".NET 10" /></a>
  <a href="https://react.dev/"><img src="https://img.shields.io/badge/Frontend-React%20%7C%20TypeScript%20%7C%20Vite-61dafb.svg" alt="React Vite" /></a>
  <a href="wiki/Home.md"><img src="https://img.shields.io/badge/docs-wiki-brightgreen.svg" alt="Wiki Documentation" /></a>
  <a href="https://github.com/12thirtyfour/Controllarr/actions"><img src="https://img.shields.io/github/actions/workflow/status/12thirtyfour/Controllarr/build.yml?branch=develop&label=build" alt="Build Status" /></a>
</p>

---

> 📖 **Comprehensive Documentation**: Explore the [**Controllarr Wiki**](wiki/Home.md) for detailed installation guides, console compatibility charts, BIOS verification matrices, and hardware exporter configurations.

---

## What is Controllarr?

**Controllarr** brings the effortless, automated library management experience made famous by media managers like Sonarr and Radarr to your video game collection.

Instead of wrestling with disorganized ROM folders, unverified dumps, multi-region duplicate clutter, missing BIOS files, or manually copying games to flash carts and handhelds, Controllarr automates the entire lifecycle:

1. **Pick Your Consoles & Collections**: Add the systems you love, from 8-bit classics to modern disc-based systems.
2. **Find & Acquire**: Automatically search and download titles across direct HTTP archives (such as the Internet Archive), Usenet (Newznab), and BitTorrent (Torznab).
3. **Verify Every Byte**: Match your dumps against canonical **No-Intro**, **Redump**, and **TOSEC** hash databases.
4. **Curate with 1G1R**: Automatically keep only your preferred regional release (e.g., USA > Europe > Japan) and revision, filtering out duplicate regional clones.
5. **Sync to Hardware**: Export curated sets formatted specifically for your flash carts, handhelds, and ODEs (EverDrive, MiSTer FPGA, Analogue Pocket, OnionOS, and more).
6. **Play Instantly**: Launch games directly in your web browser with embedded Libretro cores or hand off to native desktop emulators with a single click.

---

## Key Features

### 🎯 1 Game 1 ROM (1G1R) & Smart Curation
* **Region Hierarchy**: Prioritize your preferred regions (e.g., `USA > Europe > Japan > World`).
* **Revision Preferences**: Choose between latest revisions (bug fixes and balance updates) or original release versions for launch fidelity.
* **Archivist / Completionist Mode**: Track and catalog all regional variants, alternate revisions, and languages when you want a complete historical archive.

### 🛡️ Canonical Hash Verification
* Embedded DAT catalog support for **No-Intro** (cartridge systems), **Redump** (optical disc systems), and **TOSEC** (homebrew, revisions, firmware).
* Validates CRC32, MD5, and SHA-1 checksums on ingestion.
* Color-coded verification badges in the UI immediately signal verified clean dumps versus bad or modified rips.

### 💾 Hardware Target Exporter (HTGDB / SMDB)
* Built-in multi-device exporter modal based on the **Hardware Target Game Database (HTGDB / SMDB)**.
* **FAT32-Safe Splits**: Automatically splits large directories into `A-M/` and `N-Z/` subdirectories to prevent file-count limits on legacy hardware.
* **Hardware Profiles**: One-click exports tailored for:
  * **FPGA**: MiSTer FPGA, Analogue Pocket (openFPGA).
  * **Handheld OSes**: OnionOS (Miyoo Mini), GarlicOS, MuOS, ArkOS, ROCKNIX, AmberELEC.
  * **Flash Carts**: EverDrive (Krikzz), FXPak Pro, EZ-Flash, R4 / TWiLight Menu++.
  * **Optical Drive Emulators (ODEs)**: xStation, Fenrir, MODE, GDEMU, GC Loader / Swiss.
  * **Frontends**: EmulationStation DE (ES-DE / EmuDeck), Batocera, RetroArch.

### 🕹️ Platform BIOS Verification Matrix
* Track, inspect, and verify required BIOS and firmware files for every console.
* Displays required filenames, expected MD5/SHA-1 hashes, and status (Verified, Missing, Mismatched).
* Guarantees games will boot smoothly before you launch or export them.

### 🏆 RetroAchievements Integration
* Connect your [RetroAchievements.org](https://retroachievements.org/) account to showcase your gaming achievements directly inside your library.
* Visual badge showcase, progression statistics, masteries, and leaderboard ranks tied to your game collection.

### 🎮 Play Anywhere (Browser & Desktop)
* **Web Player**: Click **Play** on supported cartridge and handheld titles to run them immediately inside your browser using embedded Libretro cores.
* **Desktop Launcher Protocol**: Launch disc-based or demanding games directly in local native emulators (RetroArch, DuckStation, PCSX2, Dolphin, etc.) via custom URI handler (`controllarr://play/{gameId}`).

### 📦 Automated Archive Ingestion & Lossless Compression
* Ingests `.zip`, `.7z`, and `.rar` multi-file archives on the fly.
* Compresses optical disc images into space-saving, emulator-ready formats:
  * `.chd` conversion using `chdman` for CD/DVD-based consoles (PS1, PS2, Saturn, Dreamcast, Sega CD, PC Engine CD).
  * Lossless `.rvz` conversion using `dolphin-tool` for GameCube and Wii.

### 🔍 3-Way Acquisition Priority
Controllarr searches and grabs releases according to your configured protocol hierarchy:
1. **HTTP / Direct Download**: Native retrieval from public archives (Internet Archive S3 API) with zero external tools required.
2. **Usenet**: Native Newznab integration configured for console gaming categories (`1000` Console & `8000` Other/Misc).
3. **BitTorrent**: Torznab integration with leading torrent clients and indexers.

---

## Universal Console Scope

Controllarr is dedicated specifically to video game consoles, handheld systems, and console expansion hardware across all generations.

| Category | Supported Platforms |
|---|---|
| **Nintendo** | NES, Famicom Disk System, SNES, Nintendo 64, GameCube, Wii, Wii U, Nintendo Switch |
| **Nintendo Handhelds** | Game Boy, Game Boy Color, Game Boy Advance, Nintendo DS, Nintendo 3DS, Virtual Boy |
| **Sega** | Master System, SG-1000, Genesis / Mega Drive, Sega CD, Sega 32X, Saturn, Dreamcast, Game Gear |
| **Sony** | PlayStation (PS1), PlayStation 2 (PS2), PlayStation 3 (PS3), PSP, PlayStation Vita |
| **Microsoft** | Xbox (Original), Xbox 360 |
| **NEC & SNK** | TurboGrafx-16 / PC Engine, PC Engine CD-ROM², PC-FX, Neo Geo AES/MVS, Neo Geo CD, Neo Geo Pocket / Color |
| **Classic & Others** | Atari (2600, 5200, 7800, Lynx, Jaguar), 3DO, Philips CD-i, Bandai WonderSwan / Color, ColecoVision, Intellivision, Vectrex |

> **Note**: Controllarr focuses exclusively on dedicated console and handheld hardware. General computer architectures (PC/DOS, Commodore, Amiga) and arcade machine dumps (MAME) are outside the project's scope.

---

## Architecture & Technical Specifications

Controllarr is built as a high-performance, container-ready client-server architecture:

```
┌────────────────────────────────────────────────────────┐
│               React 19 / TypeScript SPA                │
│       Vite • React Query • React-Table • FontAwesome    │
└───────────────────────────┬────────────────────────────┘
                            │ REST API (v3 / v5) + SignalR
┌───────────────────────────▼────────────────────────────┐
│                    Controllarr Core                    │
│             ASP.NET Core (.NET 10.0 Runtime)           │
├────────────────────────────────────────────────────────┤
│ • 1G1R Engine          • DAT Catalog Ingestion (No-Intro)│
│ • HTGDB/SMDB Exporter  • Hash Verifier (CRC32/MD5/SHA1)│
│ • BIOS Matrix Service  • Disc Compressor (CHD/RVZ)     │
│ • 3-Way Search Dispatch (HTTP / Usenet / Torznab)      │
├───────────────────────────┬────────────────────────────┤
│   SQLite (Default)        │   PostgreSQL (Enterprise)  │
└───────────────────────────┴────────────────────────────┘
```

* **Backend**: ASP.NET Core on **.NET 10**, compiled with StyleCop compliance and zero-warning enforcement (`<TreatWarningsAsErrors>true`).
* **Frontend**: React 19 single-page application built with **TypeScript**, **Vite**, and modernized styling.
* **Persistence Layer**: Dual-database abstraction supporting embedded **SQLite** out-of-the-box or external **PostgreSQL** (versions 16, 17, and 18) for high-scale multi-user instances.
* **REST API**: Dual-exposed OpenAPI-compliant endpoints on both `/api/v3` and `/api/v5` for full backward compatibility and rich third-party integration.
* **Target Application Port**: `7050` (default).

---

## Getting Started

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Node.js](https://nodejs.org/) (v20+ or v24+) & [Yarn](https://yarnpkg.com/)
* Supported OS: macOS (Apple Silicon / Intel), Linux (x64 / ARM64), or Windows (x64).

### Building and Running from Source

1. **Clone the repository:**
   ```bash
   git clone https://github.com/12thirtyfour/Controllarr.git
   cd Controllarr
   ```

2. **Build the React frontend:**
   ```bash
   yarn install
   yarn build
   ```

3. **Build the .NET backend:**
   ```bash
   dotnet build src/Sonarr.sln --configuration Release
   ```

4. **Launch Controllarr:**
   ```bash
   # macOS / Linux
   ./_output/net10.0/Sonarr --nobrowser

   # Windows
   .\_output\net10.0\Sonarr.exe --nobrowser
   ```

5. **Open your browser:**
   Navigate to [http://localhost:7050](http://localhost:7050).

---

## Credits, Upstream Attribution & Licenses

Controllarr is open-source software licensed under the [GNU General Public License v3.0 (GPL-3.0)](LICENSE.md).

We stand on the shoulders of incredible open-source projects, preservation databases, and emulation communities. Detailed license texts and notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md):

* **[Sonarr](https://github.com/Sonarr/Sonarr)** (**GPL-3.0**) — Upstream media management foundation from which Controllarr was forked.
* **[Hardware Target Game Database (HTGDB / SMDB)](https://github.com/frederic-mahe/Hardware-Target-Game-Database)** — Flash-cart directory layouts and curation schemas by SmokeMonster and Frédéric Mahé.
* **[No-Intro](https://www.no-intro.org/)**, **[Redump](http://redump.org/)**, & **[TOSEC](https://www.tosecdev.org/)** — Canonical ROM and optical disc checksum databases.
* **[RetroAchievements](https://retroachievements.org/)** — Achievement metadata and community badge services.
* **[IGDB](https://www.igdb.com/)** — Comprehensive video game metadata and high-resolution media.
* **[Libretro / RetroArch](https://github.com/libretro/RetroArch)** — Web emulation cores and desktop launch targets.
* **`chdman` / [MAME Project](https://github.com/mamedev/mame)** — Optical disc compression into `.chd`.
* **`dolphin-tool` / [Dolphin Project](https://github.com/dolphin-emu/dolphin)** — GameCube & Wii `.rvz` disc compression.

---

## Project Disclaimer & Development Model

> [!NOTE]
> **Experimental AI-Native Development Experiment**  
> Controllarr is both an active passion project and an AI-driven software engineering experiment. The project owner has no professional coding experience. Other than the upstream open-source foundation provided by Sonarr, all architectural design, backend C#/.NET 10 development, frontend React/TypeScript UI, and refactoring from the initial fork onward have been designed and implemented autonomously through artificial intelligence:
>
> * **Project Owner**: Human visionary, direction, functional requirements, and physical hardware verification.
> * **Gemini**: Project manager, requirements evaluator, and system architect.
> * **Jules**: Feature engineer and autonomous developer.
> * **Antigravity**: Lead engineering agent, pair programmer, CI/CD triage specialist, and release steward.
