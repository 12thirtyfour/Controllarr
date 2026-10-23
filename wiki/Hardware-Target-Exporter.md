# Hardware Target Exporter

One of Controllarr's most powerful features is the **Hardware Target Exporter**, allowing you to export curated game collections directly onto SD cards, USB drives, or local staging folders formatted specifically for real hardware, flash carts, and emulation handhelds.

---

## 1. The Challenge of Real Hardware

Different flash carts, FPGA cores, and handheld operating systems expect completely different folder layouts:
* **Legacy Filesystems**: Older flash carts formatted with FAT32 suffer file allocation table slowdowns or crash when more than 255 files exist in a single directory.
* **Inconsistent Directory Names**: SNES ROMs might be expected in `/snes/` on EverDrive, `/SNES/` on MiSTer, `/Assets/snes/common/` on Analogue Pocket, or `/Roms/SFC/` on OnionOS.
* **Missing BIOS Dependencies**: Optical disc and 32-bit cores fail silently if system BIOS files aren't placed in system-specific firmware folders.

Controllarr solves this using rules from the **Hardware Target Game Database (HTGDB / SMDB)** by SmokeMonster and Frédéric Mahé.

---

## 2. Supported Target Hardware Profiles

Controllarr includes built-in presets for over 15 device families:

### FPGA Platforms
* **MiSTer FPGA**: Organizes ROMs into `/games/{Core}/` (e.g., `/games/NES/`, `/games/Genesis/`, `/games/PSX/`).
* **Analogue Pocket (openFPGA)**: Organizes ROMs into `/Assets/{Core}/common/` with required core identifiers.

### Handheld Operating Systems
* **OnionOS (Miyoo Mini / Mini+)**: Formats into `/Roms/{System}/` (e.g., `/Roms/FC/`, `/Roms/SFC/`, `/Roms/MD/`, `/Roms/PS/`).
* **GarlicOS (RG35XX)**: Maps systems into `/ROMS/{System}/`.
* **MuOS, ArkOS & ROCKNIX**: Standardized Linux handheld layouts with subfolder support.
* **EmulationStation DE (ES-DE / EmuDeck)**: Desktop and Steam Deck standard folder schemas.
* **Batocera / Knulli**: Direct Batocera `/share/roms/{system}/` directory layouts.

### Flash Carts
* **Krikzz EverDrive Series**: Presets for EverDrive N8 (NES), Super EverDrive / FXPak Pro (SNES), EverDrive 64, Mega EverDrive (Genesis), GB / GBA X5.
* **EZ-Flash**: EZ-Flash Omega (GBA) and EZ-Flash Junior (GB/GBC).
* **Nintendo DS / 3DS**: R4, WoodR4, and TWiLight Menu++ directory trees.

### Optical Drive Emulators (ODEs)
* **Sony PlayStation**: xStation, PSIO.
* **Nintendo GameCube**: Swiss / GC Loader.
* **Sega Saturn**: Fenrir, Saroo, Satiator.
* **Sega Dreamcast**: GDEMU, TerraOnion MODE.

---

## 3. FAT32-Safe Alphabetical Bucketing

When dealing with large catalogs (e.g., thousands of NES or Genesis titles), flash cart menus can freeze or become unresponsive due to FAT32 directory limits.

Controllarr automatically calculates directory sizes and applies **alphabetical bucketing**:
* **2-Way Split**: `A-M/` and `N-Z/`
* **4-Way Split**: `A-F/`, `G-L/`, `M-S/`, `T-Z/`
* **Single Letter Buckets**: `A/`, `B/`, `C/`... `Z/`

Titles starting with numbers or symbols are cleanly routed into `# - Numbers/` folders.

---

## 4. How to Export an SD Card

1. Connect your SD card or USB drive to your computer.
2. In Controllarr, navigate to **Export** (or click **Export to SD** from any console screen).
3. **Select Target Device Profile**: Choose your hardware target (e.g., *Analogue Pocket*, *MiSTer FPGA*, or *OnionOS*).
4. **Select Consoles & Regions**: Choose which systems to export (e.g., SNES, Genesis, GBA) and whether to apply 1G1R curation.
5. **Select Destination**: Pick the root mount point of your SD card (e.g., `/Volumes/MISTER_SD/` or `E:\`).
6. **Options**:
   * *Copy System BIOS*: Automatically transfers verified BIOS files from your library to the target firmware folder.
   * *FAT32 Split*: Enable automatic alphabetical subfolder grouping.
   * *Playlist (.m3u) Generation*: Generates playlists for multi-disc games.
7. Click **Start Export**: Controllarr shows live transfer progress, verifies copied file sizes, and safely unmounts when finished.
