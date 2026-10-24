# Core Concepts & Curation

Controllarr is engineered specifically for video game collections, which differ fundamentally from movies, music, or television series. This guide explains Controllarr's core domain models, collection strategies, and naming architecture.

For step-by-step configuration of UI curation options, see **[1G1R & Curation Modes](1G1R-and-Curation-Modes.md)** and **[Supported Platforms & Regional Model](Supported-Platforms.md)**.

---

## 1. UI Navigation & Platform Routes

Controllarr's web interface organizes video game systems under platform management routes:

* **`/platforms`**: Platform Index view listing all added console systems, platform posters, statistics, and global system health.
* **`/platforms/:titleSlug`**: Platform Details view for an individual platform (e.g., `/platforms/nintendo-snes` or `/platforms/sega-genesis`), providing game lists, revision grouping toggles, and regional filters.

---

## 2. The 1G1R (1 Game 1 ROM) Engine

In retro gaming, single titles frequently had dozens of distinct releases: regional versions (USA, Europe, Japan, Australia), bug-fix revisions (Rev A, Rev 1.1, Rev 1.2), promotional demos, and alternate language translations. Storing all of these often leads to massive directory clutter where 80% of your ROMs are unwanted duplicates.

Controllarr implements an advanced **1 Game 1 ROM (1G1R)** strategy engine:

* **Parent/Clone Relationships**: Every game is recognized as a canonical intellectual property (the "Parent Title"), linked to all its regional and revision dumps ("Clones").
* **Single Best Version**: When 1G1R mode is active, Controllarr evaluates all available releases against your prioritization rules and selects exactly **one** release to keep active in your primary library.

### Collection Strategies
* **`1G1R (Single Preferred Region)`**: Keeps a single best ROM release across all regions based on your region priority list.
* **`1G1R Multi-Region (1 Per Monitored Region)`**: Keeps the single best ROM release for each monitored region.
* **`Completionist (All Revisions, Regions & Variants)`**: Tracks and monitors all available revisions, regional variants, and dumps without de-duplication.

### Revision Preference
* **`Newest / Latest Revision (Rev A, v1.1, v1.2)`**: Favors late releases that fix game-breaking bugs, improve audio/video rendering, or adjust gameplay balance.
* **`Original Retail Release (Rev 0 / v1.0)`**: Favors original launch retail releases for speedrunners or historical purists.
* **`Pinned Version / Tag`**: Pins a specific revision identifier or tag string across the platform.

---

## 3. The 7-Region Model

Controllarr partitions platform catalogs into seven regional buckets:

* **Region 1**: USA (NTSC-U) — Monitored by default
* **Region 2**: Europe (PAL) — Monitored by default
* **Region 3**: Japan (NTSC-J) — Monitored by default
* **Region 4**: World / Multi-Region — Monitored by default
* **Region 5**: Homebrew & Aftermarket — Optional toggle (Hash-gated)
* **Region 6**: ROM Hacks & Translations — Optional toggle (Hash-gated)
* **Region 0**: Prototypes, Betas & Unlicensed — Optional toggle

---

## 4. Directory Layout & Root Folders

Controllarr organizes libraries using the **Hardware Target Game Database (HTGDB)** standard:

```text
{RootFolder}/
  ├── Nintendo Entertainment System/
  │   ├── USA/
  │   │   ├── Super Mario Bros. (USA).nes
  │   │   └── Legend of Zelda, The (USA) (Rev 1).nes
  │   ├── Europe/
  │   ├── Homebrew & Aftermarket/
  │   ├── ROM Hacks & Translations/
  │   └── Prototypes, Betas & Unlicensed/
  └── Super Nintendo Entertainment System/
      └── USA/
          └── Chrono Trigger (USA).sfc
```

### Dedicated Add-on Consoles
Unlike general media managers that treat add-on accessories as mere tags, Controllarr treats hardware add-ons as **distinct first-class consoles**:
* **Sega CD** and **Sega 32X** are independent consoles separate from the Sega Genesis.
* **Famicom Disk System** is distinct from the standard NES.
* **PC Engine CD-ROM²** is distinct from the TurboGrafx-16.

Each has its own dedicated directory tree, its own BIOS matrix requirements, and independent collection profiles.

---

## 5. ROM Naming Tokens & Presets

Controllarr features an extensible naming engine that reformats incoming dumps according to your preferred schema.

### Standard Token Variables

| Token | Description | Example Output |
|---|---|---|
| `{Game Title}` | Clean canonical game title | `Legend of Zelda, The` |
| `{Region}` | Canonical region code | `USA`, `Europe`, `Japan`, `World` |
| `{Revision}` | Revision identifier | `(Rev 1)`, `(v1.2)` |
| `{Manufacturer}`| Console hardware creator | `Nintendo`, `Sega`, `Sony` |
| `{Console}` | Platform name | `Super Nintendo Entertainment System` |
| `{Original Filename}` | Unmodified release filename | `Legend of Zelda, The (USA) (Rev A).sfc` |
| `{Hash:Crc32}` | CRC32 hexadecimal string | `D445D1C8` |
| `{Hash:Sha1}` | SHA-1 hexadecimal checksum | `8c5a24...` |

### Predefined Naming Presets
* **No-Intro Standard**: `{Game Title} ({Region}) [{Revision}]`
* **SmokeMonster / HTGDB**: `{Game Title} ({Region})`
* **Alphabetical FAT32**: Organized with directory prefixes for hardware flash carts (`A-M/`, `N-Z/`).

---

## 6. Unofficial Content & ROM Hacks Gating

Controllarr strictly separates commercial retail releases from homebrew, fan translations, and ROM hacks:
* **Retail-First Default**: By default, only officially licensed commercial retail titles cataloged in No-Intro or Redump are added to automated search queues.
* **Cryptographic Hash-Gating**: When unofficial content (Region 5 - Homebrew, Region 6 - ROM Hacks, Region 0 - Prototypes) is enabled, Controllarr requires strict cryptographic checksum verification against community DAT databases before ingestion.
