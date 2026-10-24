# 1G1R & Curation Modes

Controllarr provides flexible curation strategies and revision management tools tailored specifically for video game collections. This document details the **UI routes**, the **Add / Edit Platform** curation controls, and the **Platform Details** table version grouping capabilities.

---

## Navigation & UI Routes

Controllarr's primary library interface uses `/platforms` endpoints:

* **Platform Index (`/platforms`)**: The main library landing page displaying all monitored gaming systems, poster views, platform statistics, and quick system actions.
* **Platform Details (`/platforms/:titleSlug`)**: The detailed view for a specific console platform (e.g., `/platforms/nintendo-snes` or `/platforms/sega-genesis`). Displays the complete title catalog, regional breakdown, disc usage, and revision grouping controls.

---

## Add / Edit Platform Curation Options

When adding a new console platform or editing an existing system's settings via the **Add / Edit Platform** dialog, you can configure the following curation parameters:

### 1. Collection Strategy
Controls how Controllarr de-duplicates regional releases and variants across the platform catalog:

* **`1G1R (Single Preferred Region)`**: The standard **1 Game 1 ROM** strategy. Controllarr evaluates all regional releases and revision dumps for a title against your region hierarchy and retains only **one** preferred release per game.
* **`1G1R Multi-Region (1 Per Monitored Region)`**: Retains the single best ROM release for **each** monitored region. For example, if USA, Europe, and Japan are monitored, Controllarr will keep up to 3 preferred ROMs (one per region) for that title.
* **`Completionist (All Revisions, Regions & Variants)`**: Disables de-duplication. Controllarr tracks and monitors all available revisions, regional releases, alternate dumps, and variants in the catalog.

---

### 2. Version / Revision Preference
Determines which ROM revision or dump version Controllarr prioritizes when multiple revisions exist in 1G1R mode:

* **`Newest / Latest Revision (Rev A, v1.1, v1.2)`**: Prioritizes late-stage bug-fix revisions, audio/video patch updates, and updated ROM dumps (e.g., favoring `v1.2` over `v1.0`).
* **`Original Retail Release (Rev 0 / v1.0)`**: Prioritizes initial launch day retail dumps (`Rev 0` / `v1.0`). Ideal for speedrunners or historical purists relying on unpatched glitches or launch assets.
* **`Pinned Version / Tag`**: Allows you to specify a custom tag or version string (e.g., `Rev A`, `Rev 1`, `v1.1`) to pin a specific revision as preferred across the platform.

---

### 3. Preferred Compression / Container Format
Configures the preferred storage container and archive format for games on this platform:

* **`Compressed Archive (.zip / .7z)`**: Lossless archive compression commonly used for cartridge platforms (NES, SNES, Genesis, Game Boy, GBA).
* **`Compressed Disc / CHD (.chd / .rvz / .cso)`**: Modern losslessly compressed disc image formats optimized for optical platforms (PS1, PS2, Saturn, Dreamcast, GameCube, Wii).
* **`Uncompressed / Raw ROM (.nes, .sfc, .gba, .iso, .bin/.cue)`**: Native uncompressed ROM files or raw disc dumps.

---

### 4. Unofficial Content Region Toggles
Allows granular monitoring of non-commercial and community content:

* **`Track Homebrew & Aftermarket (Region 5)`**: Enables monitoring of homebrew software, indie cartridge releases, and aftermarket titles. Hash-gated against HTGDB / SMDB DAT checksums.
* **`Track ROM Hacks & Translations (Region 6)`**: Enables monitoring of fan translations, quality-of-life ROM hacks, and gameplay overhauls. Hash-gated against community patch databases.
* **`Track Prototypes, Betas & Unlicensed (Region 0)`**: Enables monitoring of pre-release prototype builds, beta samples, tech demos, and unlicensed commercial titles.

---

## Platform Details Version Grouping

In **1G1R Mode**, multiple revisions, regional variants, or alternate dumps of the same title in the Platform Details view (`/platforms/:titleSlug`) are neatly grouped together in the main catalog table under the **Version / Type** column.

### Visual Grouping Features

1. **Alternating Row Group Backgrounds**: The table applies distinct alternating background shading (`primaryGameGroup` and `alternateGameGroup`) across game title groups, visually separating individual titles and their child revisions.
2. **Inline Version Toggle**: Next to the primary version badge in the **Version / Type** column, an inline toggle displays the count of additional versions (e.g., `+2 versions`). Clicking this toggle expands or collapses the row group directly in place (`Hide versions`), allowing you to inspect alternate revisions without leaving the main platform view.
