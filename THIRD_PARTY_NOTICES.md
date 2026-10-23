# Third-Party Notices, Upstream Credits & License Attributions

**Controllarr** is free and open-source software licensed under the **GNU General Public License v3.0 (GPL-3.0)** (see [LICENSE.md](LICENSE.md)).

This document provides explicit credit and license disclosures for the upstream projects, external command-line tools, preservation databases, and open-source libraries that Controllarr builds upon or integrates with.

---

## 1. Core Upstream Architecture (Fork Attribution)

### Sonarr
- **Project**: [Sonarr](https://github.com/Sonarr/Sonarr) (`NzbDrone`)
- **Authors**: Sonarr Contributors (Kayone, markus101, Taloth, and the Sonarr Team)
- **License**: [GNU General Public License v3.0 (GPL-3.0)](https://github.com/Sonarr/Sonarr/blob/develop/LICENSE.md)
- **Usage in Controllarr**: Controllarr is directly forked from the Sonarr backend (.NET) and frontend (React/TypeScript) architecture. All core scheduling, indexer/download client pipelines, database migration frameworks, and UI components originate from Sonarr's codebase and have been adapted for video game console preservation under the terms of the GPL-3.0 license.

---

## 2. External Command-Line Tools & Runtime Utilities

Controllarr invokes external command-line utilities via separate OS processes (`IProcessProvider`) for lossless disc image compression and archive extraction. Controllarr does not statically link these tools into its .NET assemblies; when running via the official Docker image, `mame-tools` and `p7zip-full` are installed from upstream Debian repositories.

### `chdman` (MAMECompressed Hunks of Data Manager)
- **Project**: [MAME (Multiple Arcade Machine Emulator)](https://github.com/mamedev/mame)
- **Authors**: Aaron Giles, Nicola Salmoria, and the MAMEdev Team
- **License**: **GPL-2.0-or-later** (application) / **BSD-3-Clause** (core MAME components) — see [MAME COPYING](https://github.com/mamedev/mame/blob/master/COPYING)
- **Usage in Controllarr**: Invoked as an external CLI process (`chdman createcd`, `chdman createdvd`, `chdman verify`) by `DiscCompressionService` to convert `.cue`/`.bin`, `.iso`, and `.gdi` optical disc dumps into compressed `.chd` containers.

### `dolphin-tool` (Dolphin Emulator Project)
- **Project**: [Dolphin Emulator](https://github.com/dolphin-emu/dolphin)
- **Authors**: Dolphin Emulator Project Contributors
- **License**: **GPL-2.0-or-later** — see [Dolphin license.txt](https://github.com/dolphin-emu/dolphin/blob/master/license.txt)
- **Usage in Controllarr**: Invoked as an external CLI process (`dolphin-tool convert -f rvz`) by `DiscCompressionService` to convert Nintendo GameCube and Wii `.iso`/`.gcm` disc images into lossless `.rvz` format.

### 7-Zip / `p7zip`
- **Project**: [7-Zip](https://www.7-zip.org/) / [p7zip](https://github.com/p7zip-project/p7zip)
- **Authors**: Igor Pavlov and p7zip Contributors
- **License**: **GNU LGPL-2.1-or-later** (with unRAR license restriction for RAR decompression code)
- **Usage in Controllarr**: Installed in container environments (`p7zip-full`) to support multi-format archive extraction alongside managed archive readers.

---

## 3. Emulation & Launcher Ecosystem

### Libretro & RetroArch
- **Project**: [Libretro / RetroArch](https://github.com/libretro/RetroArch)
- **Authors**: Libretro Team and individual core authors
- **License**: **GPL-3.0** (RetroArch); individual Libretro cores (`snes9x`, `nestopia`, `genesis_plus_gx`, `mgba`, `sameboy`, `melonds`, `stella`, `mupen64plus_next`, `mednafen_psx_hw`, `pcsx2`, `dolphin`) are governed by their respective upstream licenses (including GPL-2.0, GPL-3.0, MPL-2.0, MIT, BSD, and Snes9x non-commercial terms).
- **Usage in Controllarr**: `GameLauncherService` generates URI launch descriptors (`controllarr://play/{gameId}`) and web player routes referencing Libretro core identifiers. Controllarr does **not** bundle proprietary console BIOS files or copyrighted ROMs.

---

## 4. Preservation Databases, Taxonomies & Community APIs

Controllarr parses user-supplied DAT/SMDB catalogs and communicates with community APIs to verify cryptographic hashes and organize hardware-ready folder trees:

- **Hardware Target Game Database (HTGDB / SMDB)**: Created by **SmokeMonster**, **Frederic Mahe**, and HTGDB contributors ([GitHub Repository](https://github.com/frederic-mahe/Hardware-Target-Game-Database)). Controllarr's `SmdbParserService` parses tab-delimited SHA-256 SMDB manifests and supports SmokeMonster/HTGDB directory naming presets.
- **No-Intro** ([no-intro.org](https://www.no-intro.org/)), **Redump** ([redump.org](http://redump.org/)), and **TOSEC** ([tosecdev.org](https://www.tosecdev.org/)): Controllarr's `DatParserService` reads standard Logiqx XML and ClrMamePro DAT files provided by these preservation projects for local CRC32, MD5, SHA-1, and SHA-256 ROM verification.
- **RetroAchievements**: [RetroAchievements.org](https://retroachievements.org/) ([GitHub](https://github.com/RetroAchievements), GPL-3.0 / MIT). Integrated via `RetroAchievementsService` for mastery tracking and achievement badge display.
- **Flash-Cart, Handheld CFW & ODE Ecosystem Targets**: Directory export layouts in `SdCardExporterService` and `VirtualSymlinkLibraryService` are designed for compatibility with [MiSTer FPGA](https://github.com/MiSTer-devel) (GPL-3.0), [RetroArch](https://github.com/libretro/RetroArch) (GPL-3.0), [OnionOS](https://github.com/OnionUI/Onion) (GPL-3.0), [Batocera.linux](https://github.com/batocera-linux) (GPL-2.0), [EmulationStation DE (ES-DE)](https://gitlab.com/es-de/emulationstation-de) (MIT), [ArkOS](https://github.com/christianhaitian/arkos) / [ROCKNIX](https://github.com/ROCKNIX/distribution) (GPL-2.0), [TWiLight Menu++](https://github.com/DS-Homebrew/TWiLightMenu) (GPL-3.0), [Swiss (GameCube)](https://github.com/emukidid/swiss-gc) (GPL-2.0), Analogue openFPGA, Krikzz EverDrive, EZ-Flash, xStation, Fenrir, GDEMU, and FXPak Pro / sd2snes ([ikari_01](https://github.com/mrehkopf/sd2snes), GPL-2.0).

---

## 5. Key Open-Source Libraries (.NET & Frontend)

- **SharpCompress** — [MIT License](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt) (Archive inspection and extraction)
- **Dapper** — [Apache License 2.0](https://github.com/DapperLib/Dapper/blob/main/License.txt) (Micro-ORM data access)
- **FluentMigrator** — [Apache License 2.0](https://github.com/fluentmigrator/fluentmigrator/blob/main/LICENSE.txt) (SQLite & PostgreSQL schema migrations)
- **SQLite / System.Data.SQLite** — [Public Domain](https://www.sqlite.org/copyright.html)
- **Npgsql** — [PostgreSQL License](https://github.com/npgsql/npgsql/blob/main/LICENSE)
- **NLog** — [BSD-3-Clause License](https://github.com/NLog/NLog/blob/master/LICENSE.txt)
- **DryIoc** — [MIT License](https://github.com/dadhi/DryIoc/blob/master/LICENSE.txt)
- **NUnit / Moq / FluentAssertions** — MIT / BSD-3-Clause / Apache-2.0 (Offline test suite)
- **React, Redux, Vite & TypeScript** — MIT / Apache-2.0 (Frontend SPA)
