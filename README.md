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
  <a href="https://github.com/12thirtyfour/Controllarr/wiki"><img src="https://img.shields.io/badge/docs-wiki-brightgreen.svg" alt="Wiki Documentation" /></a>
  <a href="https://github.com/12thirtyfour/Controllarr/actions"><img src="https://img.shields.io/github/actions/workflow/status/12thirtyfour/Controllarr/build.yml?branch=develop&label=build" alt="Build Status" /></a>
</p>

---

> 📖 **Full Documentation**: Explore the [**Controllarr Wiki**](https://github.com/12thirtyfour/Controllarr/wiki) for in-depth setup guides, console platform compatibility, BIOS matrices, and hardware exporter profiles.

---

## What is Controllarr?

**Controllarr** brings the effortless, automated library management experience made famous by media managers like Sonarr and Radarr to your video game collection.

Instead of wrestling with disorganized folders, unverified dumps, multi-region duplicates, missing BIOS files, or manually copying games to flash carts and handhelds, Controllarr automates the entire lifecycle:

1. **Pick Your Consoles**: Manage cartridge, disc, and handheld systems across all generations.
2. **Find & Acquire**: Automatically search and grab titles across direct HTTP archives (Internet Archive), Usenet (Newznab), and BitTorrent (Torznab).
3. **Verify Every Byte**: Check dumps against canonical **No-Intro**, **Redump**, and **TOSEC** hash databases.
4. **Curate with 1G1R**: Automatically keep only your preferred regional release (e.g., USA > Europe > Japan) and revision, filtering out duplicate clones.
5. **Sync to Hardware**: Export curated sets formatted specifically for flash carts, ODEs, and handhelds (EverDrive, MiSTer FPGA, Analogue Pocket, OnionOS, etc.).
6. **Play Instantly**: Launch games directly in your browser with embedded Libretro cores or hand off to native desktop emulators with a single click.

---

## Highlights

Detailed feature breakdowns and technical documentation are available in the [**Controllarr Wiki**](https://github.com/12thirtyfour/Controllarr/wiki):

* **[🎯 1 Game 1 ROM (1G1R) & Smart Curation](https://github.com/12thirtyfour/Controllarr/wiki/Core-Concepts)**: Eliminate duplicate regional clutter while retaining the single best release and revision based on your custom region hierarchy.
* **[🛡️ Canonical Hash Verification](https://github.com/12thirtyfour/Controllarr/wiki/Ingestion-and-Hash-Verification)**: Validates CRC32, MD5, and SHA-1 checksums on ingestion against No-Intro, Redump, and TOSEC DATs.
* **[💾 Hardware Target Exporter (HTGDB / SMDB)](https://github.com/12thirtyfour/Controllarr/wiki/Hardware-Target-Exporter)**: One-click SD card exports with automatic FAT32-safe alphabetical bucketing (`A-M/`, `N-Z/`) for MiSTer, Analogue Pocket, OnionOS, EverDrive, and ODEs.
* **[🕹️ Platform BIOS Verification Matrix](https://github.com/12thirtyfour/Controllarr/wiki/Platform-BIOS-Matrix)**: Inspect, checksum, and verify required system firmwares before launching or exporting games.
* **[🏆 RetroAchievements Integration](https://github.com/12thirtyfour/Controllarr/wiki/In-Browser-Play-and-Emulators)**: Showcase your gaming badges, progression, and leaderboard statistics directly in your library.
* **[🎮 Play Anywhere](https://github.com/12thirtyfour/Controllarr/wiki/In-Browser-Play-and-Emulators)**: Embedded WebAssembly Libretro player for instant browser gaming, plus one-click protocol launcher (`controllarr://play/{gameId}`) for native desktop emulators.
* **[📦 Automated Archive Ingestion & Compression](https://github.com/12thirtyfour/Controllarr/wiki/Ingestion-and-Hash-Verification)**: Extracts `.zip`, `.7z`, and `.rar` sets, and losslessly converts disc images into compressed `.chd` (via `chdman`) and `.rvz` (via `dolphin-tool`).
* **[🔍 3-Way Acquisition Priority](https://github.com/12thirtyfour/Controllarr/wiki/Acquisition-and-Indexers)**: Search across public HTTP archives (Internet Archive S3 API), Usenet (Newznab gaming categories `1000` & `8000`), and BitTorrent (Torznab).

---

## Universal Console Scope

Controllarr is dedicated specifically to video game consoles, handhelds, and hardware expansion systems across every generation:

* **Nintendo**: NES, Famicom Disk System, SNES, N64, GameCube, Wii, Wii U, Switch, Game Boy, GBC, GBA, DS, 3DS, Virtual Boy.
* **Sega**: SG-1000, Master System, Genesis / Mega Drive, Sega CD, Sega 32X, Saturn, Dreamcast, Game Gear.
* **Sony**: PlayStation (PS1), PlayStation 2 (PS2), PlayStation 3 (PS3), PSP, PlayStation Vita.
* **Microsoft**: Xbox, Xbox 360.
* **NEC & SNK**: TurboGrafx-16 / PC Engine, PC Engine CD-ROM², PC-FX, Neo Geo AES/MVS, Neo Geo CD, Neo Geo Pocket.
* **Classic & Others**: Atari (2600 through Jaguar), 3DO, Philips CD-i, Bandai WonderSwan, ColecoVision, Intellivision, Vectrex.

*(Note: Controllarr focuses exclusively on dedicated console and handheld hardware; computer architectures and arcade sets are excluded. See the full [Supported Consoles Matrix](https://github.com/12thirtyfour/Controllarr/wiki/Supported-Consoles) for file extension and compression specifications).*

---

## Architecture

Controllarr is built as a high-performance client-server application:

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
* **Persistence Layer**: Dual-database abstraction supporting embedded **SQLite** out-of-the-box or external **PostgreSQL** (versions 16–18).
* **REST API**: Dual-exposed OpenAPI endpoints on both `/api/v3` and `/api/v5`.
* **Default Port**: `7050`.

---

## Quick Start

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [Node.js](https://nodejs.org/) (v20+ or v24+) & [Yarn](https://yarnpkg.com/)
* Supported OS: macOS (Apple Silicon / Intel), Linux (x64 / ARM64), or Windows (x64).

### Build & Run
```bash
# 1. Clone the repository
git clone https://github.com/12thirtyfour/Controllarr.git
cd Controllarr

# 2. Build the React frontend
yarn install
yarn build

# 3. Build the .NET backend
dotnet build src/Sonarr.sln --configuration Release

# 4. Launch Controllarr
# macOS / Linux:
./_output/net10.0/Sonarr --nobrowser

# Windows:
.\_output\net10.0\Sonarr.exe --nobrowser
```

Open your browser and navigate to **[http://localhost:7050](http://localhost:7050)**.

*(For reverse proxies, systemd services, or container deployment, see the [Installation Guide](https://github.com/12thirtyfour/Controllarr/wiki/Installation-Guide) and [Quick Start Tutorial](https://github.com/12thirtyfour/Controllarr/wiki/Quick-Start)).*

---

## Credits, Upstream Attribution & Licenses

Controllarr is open-source software licensed under the [GNU General Public License v3.0 (GPL-3.0)](LICENSE.md).

We stand on the shoulders of incredible open-source projects, preservation databases, and emulation communities. Detailed notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md):

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
