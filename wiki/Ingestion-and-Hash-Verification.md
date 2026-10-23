# Ingestion & Cryptographic Hash Verification

Controllarr's ingestion pipeline is built on strict data integrity. Unlike standard media managers that rely purely on filename regex matching, Controllarr validates game ROMs and disc images against canonical preservation hash databases.

---

## 1. Canonical Preservation Databases

Controllarr incorporates checksum schemas from the three primary preservation authorities:

* **[No-Intro](https://www.no-intro.org/)**: Standard for cartridge-based systems (NES, SNES, Genesis, N64, Game Boy, GBA, NDS). Catalogs clean, unheadered, byte-for-byte authentic ROM dumps.
* **[Redump](http://redump.org/)**: Standard for optical disc systems (PS1, PS2, Saturn, Dreamcast, GameCube, Wii, Sega CD). Focuses on accurate disc images, cue sheets, and sector tracks.
* **[TOSEC (The Official Sector Extended Collection)](https://www.tosecdev.org/)**: Standard for homebrew, educational software, firmware, and alternate revisions.

---

## 2. Checksum Calculation: CRC32, MD5 & SHA-1

When a file is ingested (either via automated download client grab or manual folder scan):

1. **Hash Generation**:
   * Controllarr computes **CRC32**, **MD5**, and **SHA-1** hashes for the ROM data.
2. **Catalog Cross-Referencing**:
   * Hashes are compared against Controllarr's embedded DAT cache.
   * If a match is found, Controllarr confirms the title, exact revision, region, and verified dump status.
3. **Status Badging**:
   * 🟢 **Verified (Green)**: 100% byte-accurate match with official No-Intro/Redump DATs.
   * 🟡 **Unverified / Alternate (Yellow)**: Valid playable ROM, but with a checksum not yet in official DATs (e.g. good homebrew, alternate translation, or undocumented revision).
   * 🔴 **Bad Dump / Corrupted (Red)**: Known bad dump or truncated file (flagged by No-Intro/Redump with `[b]`).

---

## 3. Ingesting Compressed Archives

To save space and avoid expensive extraction overhead:

* **ZIP Central Directory Inspection**: For `.zip` files, Controllarr inspects the central directory header to retrieve the CRC32 checksums of contained files *without* decompressing the archive to disk.
* **Multi-Part Archives**: `.7z`, `.rar`, and multi-file archives are parsed using native streaming extractors (via SharpCompress and 7-Zip).
* **Automatic Clean Repacking**: Files can be extracted, stripped of unwanted copier headers, and repacked into canonical zip or 7z formats.

---

## 4. Header Detection & Stripping

Legacy backup units (such as Super Magicom, Wild Card, or Doctor V64) frequently prepended 16-byte or 512-byte hardware copier headers to ROM files:

* **NES**: Distinguishes between obsolete iNES headers, clean PRG/CHR dumps, and modern **NES 2.0** extended mapper headers.
* **SNES**: Automatically detects and removes 512-byte copier headers (e.g., SMC headers) to ensure ROMs conform to No-Intro standards and run accurately on modern flash carts and FPGA cores.
* **N64**: Automatically detects byte-swapped formats (`.v64` little-endian vs. `.z64` big-endian) and normalizes them into native big-endian `.z64`.

---

## 5. Disc Image Compression: CHDMAN & Dolphin-Tool

Optical disc games (`.bin`/`.cue` or raw `.iso`) take up immense disk space and can be difficult to manage. Controllarr integrates automated conversion tools:

### CHDMAN (`.chd`)
* Converts multi-track CD/DVD images (PS1, PS2, Sega CD, Saturn, Dreamcast, PC Engine CD) into compressed, single-file **MAME CHD (Compressed Hunks of Data)** format.
* **Lossless**: Preserves 100% of CD audio tracks, subchannel data, and disc sectors.
* **Space Savings**: Typically reduces disc size by **40%–60%**.

### Dolphin-Tool (`.rvz`)
* Converts raw GameCube and Wii `.iso` dumps into modern **RVZ (Lossless Dolphin format)**.
* Strips junk padding sectors while retaining cryptographic hashes of original game data.
* Typically reduces disc sizes by **50%–70%**.

---

## 6. Sidecar Save File Synchronization

When organizing, moving, or upgrading a ROM release:
* Controllarr automatically detects and synchronizes sidecar save files (`.srm`, `.sav`, `.state`, `.rtc`) alongside the ROM.
* Ensures that renaming or moving a title never breaks your existing in-game progress.
