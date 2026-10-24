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

### `chdman` (MAME Compressed Hunks of Data Manager)
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
- **Usage in Controllarr**: Installed in container environments (`p7zip-full`) and invoked by `ArchiveExtractionService` to support `.7z` and `.rar` archive extraction alongside managed `.zip` archive readers.

---

## 3. Emulation & Launcher Ecosystem

### Libretro & RetroArch
- **Project**: [Libretro / RetroArch](https://github.com/libretro/RetroArch)
- **Authors**: Libretro Team and individual core authors
- **License**: **GPL-3.0** (RetroArch); individual Libretro cores (`fceumm`, `nestopia`, `snes9x`, `gambatte`, `sameboy`, `mgba`, `mupen64plus_next`, `melonds`, `genesis_plus_gx`, `picodrive`, `mednafen_saturn`, `mednafen_psx_hw`, `ppsspp`, `mednafen_pce_fast`, `fbneo`, `stella2014`, `pcsx2`, `dolphin`) are governed by their respective upstream licenses (including GPL-2.0, GPL-3.0, MPL-2.0, MIT, BSD, and Snes9x/FBNeo non-commercial terms).
- **Usage in Controllarr**: `GameLauncherService` generates URI launch descriptors (`controllarr://play/{gameId}`), spawns configured local desktop `RetroArch` processes, and configures web player routes referencing Libretro core identifiers. Controllarr does **not** bundle proprietary console BIOS files or copyrighted ROMs.

### EmulatorJS
- **Project**: [EmulatorJS](https://emulatorjs.org/) ([GitHub](https://github.com/EmulatorJS/EmulatorJS))
- **Authors**: Ethan O'Brien (`ethanaobrien`) and EmulatorJS Contributors
- **License**: **GPL-3.0** — see [EmulatorJS LICENSE](https://github.com/EmulatorJS/EmulatorJS/blob/main/LICENSE)
- **Usage in Controllarr**: Loaded dynamically by the in-browser WebAssembly player (`/webplayer`, `WebPlayerPage.tsx`) from the user-configured `WasmCoreBaseUrl` (defaulting to `https://cdn.emulatorjs.org/stable/data/`) to run Libretro `.wasm` cores and WebGL CRT/LCD shaders in the browser.

---

## 4. Preservation Databases, Metadata Scrapers & Community APIs

Controllarr parses online and user-supplied DAT/SMDB catalogs and communicates with community metadata APIs (subject to user-configured priorities and rate-limit fail-safes in `GameMediaAssetService`) to verify cryptographic hashes, gather artwork, and organize hardware-ready folder trees:

- **Hardware Target Game Database (HTGDB / SMDB)**: Created by **SmokeMonster**, **Frédéric Mahé**, and HTGDB contributors ([frederic-mahe/Hardware-Target-Game-Database](https://github.com/frederic-mahe/Hardware-Target-Game-Database) and [SmokeMonsterPacks/EverDrive-Packs-Lists-Database](https://github.com/SmokeMonsterPacks/EverDrive-Packs-Lists-Database)). Controllarr's `SmdbParserService` and `GameCatalogProxy` parse tab-delimited `SHA-256` / `SHA-1` / `MD5` / `CRC32` SMDB manifests and support SmokeMonster/HTGDB directory naming presets.
- **No-Intro** ([no-intro.org](https://www.no-intro.org/)), **Redump** ([redump.org](http://redump.org/)), **TOSEC** ([tosecdev.org](https://www.tosecdev.org/)), **FinalBurn Neo (`FBNeo`)** ([finalburnneo/FBNeo](https://github.com/finalburnneo/FBNeo)), & **Libretro Database** ([libretro/libretro-database](https://github.com/libretro/libretro-database)): Controllarr's `DatParserService`, `GameCatalogProxy`, and `UnmanagedDiskScanner` parse Logiqx XML and ClrMamePro `.dat` catalogs provided by these preservation projects for cross-referenced `CRC32`, `MD5`, `SHA-1`, and `SHA-256` ROM verification (including SNK Neo Geo AES/MVS/CD and default decrypted Nintendo DS/3DS verification).
- **NSWDB (`nswdb.com`)**: [NSWDB Community Switch Release Database](http://nswdb.com/) (`http://nswdb.com/xml.php`). Parsed by `GameCatalogProxy` to cross-reference Nintendo Switch cartridge releases (`CRC32`, `TitleID`, region, and revision).
- **IGDB (Internet Game Database / Twitch Developer API)**: [IGDB.com](https://www.igdb.com/). Integrated via `IgdbMetadataProxy`, `IgdbAuthService`, and `GameMediaAssetService` for console platform metadata, descriptions, box art, screenshots, and video previews using user-supplied Twitch Client ID/Secret credentials.
- **ScreenScraper (`ScreenScraper.fr`)**: [ScreenScraper.fr](https://www.screenscraper.fr/). Integrated in `GameMediaAssetService` for 2D/3D box art, screenshots, title screens, clear logos/wheels, video previews, and PDF game manuals.
- **TheGamesDB (`TGDB`)**: [TheGamesDB.net](https://thegamesdb.net/). Integrated in `GameMediaAssetService` for community game metadata, box art, screenshots, and clear logos.
- **Libretro Thumbnails & LaunchBox Community Database**: [libretro-thumbnails](https://github.com/libretro-thumbnails/libretro-thumbnails) and [LaunchBox Games Database](https://gamesdb.launchbox-app.com/). Integrated in `GameMediaAssetService` (`Named_Boxarts`, `Named_Snaps`, `Named_Titles`, `Named_Logos`) for open-source PNG artwork fallback.
- **Internet Archive (`archive.org`)**: [Internet Archive](https://archive.org/). Integrated via `InternetArchive` HTTP indexer (`ArchiveOrgClient`) and `BiosFileService` for querying public preservation collections and verifying system BIOS files.
- **RetroAchievements**: [RetroAchievements.org](https://retroachievements.org/) ([GitHub](https://github.com/RetroAchievements), GPL-3.0 / MIT). Integrated via `RetroAchievementsService` for mastery tracking and achievement badge display.
- **Evan-Amos Console Hardware Photography ([Vanamo Media](https://github.com/evan-amos))**: High-resolution public-domain (**CC0 1.0 Universal**) video game console hardware photography by [Evan-Amos](https://commons.wikimedia.org/wiki/User:Evan-Amos).
- **Flash-Cart, Handheld CFW & ODE Ecosystem Targets**: Directory export layouts in `SdCardExporterService` and `VirtualLibraryService` are designed for compatibility with [MiSTer FPGA](https://github.com/MiSTer-devel) (GPL-3.0), [RetroArch](https://github.com/libretro/RetroArch) (GPL-3.0), [OnionOS](https://github.com/OnionUI/Onion) (GPL-3.0), [Batocera.linux](https://github.com/batocera-linux) (GPL-2.0), [EmulationStation DE (ES-DE)](https://gitlab.com/es-de/emulationstation-de) (MIT), [ArkOS](https://github.com/christianhaitian/arkos) / [ROCKNIX](https://github.com/ROCKNIX/distribution) (GPL-2.0), [TWiLight Menu++](https://github.com/DS-Homebrew/TWiLightMenu) (GPL-3.0), [Swiss (GameCube)](https://github.com/emukidid/swiss-gc) (GPL-2.0), Analogue openFPGA, Krikzz EverDrive, EZ-Flash, xStation, Fenrir, GDEMU, and FXPak Pro / sd2snes ([ikari_01](https://github.com/mrehkopf/sd2snes), GPL-2.0).

---

## 5. Key Open-Source Libraries (.NET & Frontend)

- **SharpZipLib (`ICSharpCode.SharpZipLib`)** — [MIT License](https://github.com/icsharpcode/SharpZipLib/blob/master/LICENSE.txt) (Archive inspection, central directory CRC32 reading, and `.zip` extraction/repackaging)
- **SixLabors.ImageSharp** — [Six Labors Split License / Apache-2.0](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE) (Cover art and media image processing)
- **MonoTorrent** — [MIT License](https://github.com/alanmcgovern/monotorrent/blob/master/LICENSE) (BitTorrent `.torrent` file and magnet link parsing)
- **Dapper** — [Apache License 2.0](https://github.com/DapperLib/Dapper/blob/main/License.txt) (Micro-ORM data access)
- **FluentMigrator** — [Apache License 2.0](https://github.com/fluentmigrator/fluentmigrator/blob/main/LICENSE.txt) (SQLite & PostgreSQL schema migrations)
- **FluentValidation** — [Apache License 2.0](https://github.com/FluentValidation/FluentValidation/blob/main/License.txt) (Domain and REST API request validation)
- **SQLite / System.Data.SQLite / SourceGear.sqlite3** — [Public Domain](https://www.sqlite.org/copyright.html) / [Apache-2.0](https://github.com/ericsink/cb_sqlite)
- **Npgsql** — [PostgreSQL License](https://github.com/npgsql/npgsql/blob/main/LICENSE)
- **Polly** — [BSD-3-Clause License](https://github.com/App-vNext/Polly/blob/main/LICENSE) (Resilience and transient fault-handling policies)
- **MailKit** — [MIT License](https://github.com/jstedfast/MailKit/blob/master/License.md) (SMTP notification delivery)
- **Ical.Net** — [MIT License](https://github.com/ical-org/ical.net/blob/main/license.md) (`Controllarr.ics` release schedule feed generation)
- **Newtonsoft.Json** — [MIT License](https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md) (JSON serialization)
- **Swashbuckle.AspNetCore** — [MIT License](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/LICENSE) (OpenAPI v3/v5 specification generation)
- **NLog** — [BSD-3-Clause License](https://github.com/NLog/NLog/blob/master/LICENSE.txt) (Structured logging)
- **DryIoc** — [MIT License](https://github.com/dadhi/DryIoc/blob/master/LICENSE.txt) (Dependency injection container)
- **NUnit / Moq / FluentAssertions** — MIT / BSD-3-Clause / Apache-2.0 (Offline unit test suite)
- **React, Redux, Vite, FontAwesome & TypeScript** — MIT / CC-BY-4.0 / Apache-2.0 (Frontend SPA)
