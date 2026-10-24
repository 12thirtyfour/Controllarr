# Virtual Libraries (Symlinks)

In traditional media managers, "Quality Profiles" governed video resolutions and bitrates. Controllarr replaces Quality Profiles with **Virtual Libraries**—a directory symlink architecture designed specifically for retro gaming hardware, handheld OSs, and emulation frontends.

Virtual Libraries allow you to maintain a single, canonical ROM library on storage while simultaneously exposing target-specific directory trees for devices like **MiSTer FPGA**, **RetroArch**, **Analogue Pocket**, **OnionOS**, **Batocera**, **ES-DE (EmulationStation Desktop Edition)**, and **EverDrive**.

---

## 1. How Virtual Libraries Work

Instead of physically copying or duplicating large ROM files across multiple folders (which wastes disk space and breaks 1G1R deduplication), Virtual Libraries create lightweight filesystem **directory symlinks** (or file symlinks on systems that require them).

* **Single Canonical Storage**: All ROMs, disc images (`.chd`, `.rvz`, `.iso`), and multi-disc folders reside in your primary root library directory (`{RootFolder}/{PlatformName}/`).
* **Target-Specific Folder Hierarchies**: Virtual Libraries dynamically project custom directory structures mapping to individual frontend requirements without duplicating a single byte of media data.

### Supported Target Schemas

| Target System / OS | Canonical Platform Example | Virtual Library Symlink Path Example |
|---|---|---|
| **MiSTer FPGA** | `Nintendo Entertainment System` | `/media/fat/games/NES/` |
| **RetroArch** | `Sega Genesis / Mega Drive` | `/retroarch/system/Mega Drive/` |
| **Analogue Pocket** | `Game Boy Advance` | `/Assets/gba/common/` |
| **OnionOS (Miyoo Mini)** | `Super Nintendo Entertainment System` | `/SFC/` |
| **Batocera / ES-DE** | `PlayStation (PS1)` | `/userdata/roms/psx/` |
| **EverDrive / Flash Carts** | `TurboGrafx-16 / PC Engine` | `/PCE/` |

---

## 2. Console & Multi-Disc Folder Symlinking

Virtual Libraries intelligently handle both single-file cartridge dumps and complex optical disc hierarchies:

### Single File ROMs
Cartridge files (`.nes`, `.sfc`, `.md`, `.gba`) are symlinked directly into target system folders. If regional subfolders are configured, Virtual Libraries preserve or flatten regional directory layouts according to the selected target profile rules.

### Multi-Disc & Game Folders
Optical disc games stored as multi-disc collections or extracted game folders (e.g. PS1 multi-disc sets, PS3 `PS3_GAME` directories, or Wii U extracted packages) are symlinked at the **directory level**.
* **Zero Storage Duplication**: A 15 GB multi-disc PS1 title or a 25 GB PS3 game folder is exposed instantly as a symlinked directory in target folders without duplicating storage.
* **M3U Playlist Preservation**: Generated `.m3u` playlist files are symlinked alongside multi-disc sets to ensure seamless disc swapping in frontends like RetroArch and Batocera.

---

## 3. Managing & Syncing Virtual Libraries

Virtual Libraries are configured and managed in **Settings** → **Virtual Libraries** (accessible at `/settings/profiles`).

### Automatic Synchronization
Virtual Libraries are automatically synchronized and updated whenever:
* A new ROM is ingested or imported.
* A platform scan or library refresh executes.
* File structures, revisions, or region preferences are updated.

### Manual Synchronization ("Sync Now")
To force an immediate re-evaluation and regeneration of all active Virtual Library symlinks:
1. Navigate to **Settings** → **Virtual Libraries** (`/settings/profiles`).
2. Click the **Sync Now** button at the top of the Virtual Libraries management screen.
3. Controllarr will prune stale/broken symlinks, reconcile updated game paths, and re-create symlinks for all monitored platforms.
