# Core Concepts & Curation

Controllarr is engineered specifically for video game collections, which differ fundamentally from movies, music, or television series. This guide explains Controllarr's core domain models, collection strategies, and naming architecture.

---

## 1. The 1G1R (1 Game 1 ROM) Engine

In retro gaming, single titles frequently had dozens of distinct releases: regional versions (USA, Europe, Japan, Australia), bug-fix revisions (Rev A, Rev 1.1, Rev 1.2), promotional demos, and alternate language translations. Storing all of these often leads to massive directory clutter where 80% of your ROMs are unwanted duplicates.

Controllarr implements an advanced **1 Game 1 ROM (1G1R)** strategy engine:

* **Parent/Clone Relationships**: Every game is recognized as a canonical intellectual property (the "Parent Title"), linked to all its regional and revision dumps ("Clones").
* **Single Best Version**: When 1G1R is active, Controllarr evaluates all available releases against your prioritization rules and selects exactly **one** release to keep active in your primary library.

### Configuring Your Region Hierarchy
You can customize your preferred region order globally or per console:
```text
USA > Europe > Japan > World > Asia > Other
```
If a title was released in the USA, Controllarr keeps the USA release. If a game was a European or Japanese exclusive, Controllarr falls back to the next preferred region in your list.

### Revision Preference
* **Newest Revision (Default)**: Favors late releases that fix game-breaking bugs, improve audio/video rendering, or adjust gameplay balance (e.g., *v1.2* over *v1.0*).
* **Launch Fidelity (v1.0)**: Favors original release dumps for speedrunners or historical purists who rely on launch-day glitches and uncensored assets.

---

## 2. Preservationist / Completionist Strategy

For digital archivists who want complete historical libraries:
* **All Releases Monitored**: Every regional variant, alternate language edition, and revision is tracked and matched.
* **Revision Coexistence**: Controllarr names each file with clear revision tokens (e.g., `Super Mario World (USA) (Rev 1).sfc` alongside `Super Mario World (USA).sfc`) without overwriting or archiving.

---

## 3. Directory Layout & Root Folders

Controllarr organizes libraries using the **Hardware Target Game Database (HTGDB)** standard:

```text
{RootFolder}/
  ├── Nintendo/
  │   ├── Nintendo Entertainment System/
  │   │   ├── USA/
  │   │   │   ├── Super Mario Bros. (USA).nes
  │   │   │   └── Legend of Zelda, The (USA) (Rev 1).nes
  │   │   └── Europe/
  │   └── Super Nintendo Entertainment System/
  │       └── USA/
  │           └── Chrono Trigger (USA).sfc
  └── Sega/
      ├── Genesis/
      │   └── USA/
      │       └── Sonic The Hedgehog (USA, Europe).md
      └── Sega CD/
          └── USA/
              └── Sonic CD (USA).chd
```

### Dedicated Add-on Consoles
Unlike general media managers that treat add-on accessories as mere tags, Controllarr treats hardware add-ons as **distinct first-class consoles**:
* **Sega CD** and **Sega 32X** are independent consoles separate from the Sega Genesis.
* **Famicom Disk System** is distinct from the standard NES.
* **PC Engine CD-ROM²** is distinct from the TurboGrafx-16.

Each has its own dedicated directory tree, its own BIOS matrix requirements, and independent collection profiles.

---

## 4. ROM Naming Tokens & Presets

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

## 5. Unofficial Content & ROM Hacks Gating

Controllarr strictly separates commercial retail releases from homebrew, fan translations, and ROM hacks:
* **Retail-First Default**: By default, only officially licensed commercial retail titles cataloged in No-Intro or Redump are added to automated search queues.
* **Cryptographic Hash-Gating**: When unofficial content, fan translations, or ROM hacks are enabled, Controllarr requires strict cryptographic checksum verification against community patch databases to ensure stability and compatibility before ingestion.
