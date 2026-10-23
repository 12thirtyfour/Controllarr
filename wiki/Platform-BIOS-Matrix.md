# Platform BIOS Verification Matrix

Many consoles—especially optical disc and 32-bit/64-bit systems—require proprietary system firmware (BIOS) files to boot games, play audio tracks, or run without emulation glitches.

Controllarr includes a dedicated **Platform BIOS Verification Matrix** located in **Settings** → **BIOS Verification** (and via `/api/v3/bios` and `/api/v5/bios`).

---

## 1. Why BIOS Verification Matters

* **Boot Reliability**: Disc systems like PlayStation 1 (`SCPH1001.BIN`), Sega CD (`bios_CD_U.bin`), and Saturn (`sega_101.bin`) will fail to launch without exact BIOS files.
* **Region Compatibility**: European, American, and Japanese titles frequently require regional BIOS counterparts to render localized fonts and timings.
* **Corrupted or Incompatible Rips**: Using an incorrect or bad dump of a BIOS can lead to subtle bugs, broken audio loops, or crashes mid-game.

---

## 2. Common System BIOS Requirements

| Console | Filename | Purpose | MD5 Checksum | Status |
|---|---|---|---|---|
| **Sony PlayStation (PS1)** | `scph5501.bin` | USA BIOS (Most compatible) | `490f666e1afb15b7362b406ed1cea246` | Required |
| **Sony PlayStation (PS1)** | `scph5500.bin` | Japan BIOS | `8dd7d5296a650fac7319bce665a6a53c` | Required for JP |
| **Sony PlayStation (PS1)** | `scph5502.bin` | Europe BIOS | `32736f17079d0b2b202a5da07f5c3052` | Required for EU |
| **Sony PlayStation 2 (PS2)** | `SCPH-70012_BIOS_V12_USA_200.BIN` | USA BIOS | `9a9e8d976375052f75d86c4e03f90ff8` | Required |
| **Sega CD** | `bios_CD_U.bin` | USA BIOS | `2efd74e3232ff260e371b99f84024f7f` | Required |
| **Sega CD** | `bios_CD_E.bin` | Europe BIOS | `e66fa1dc5820d254611fd9fb60731703` | Required for EU |
| **Sega CD** | `bios_CD_J.bin` | Japan BIOS | `278a9397d192149e84e820ac621a8edd` | Required for JP |
| **Sega Saturn** | `mpr-17933.bin` | USA/Europe BIOS | `4af505f7c3fe9787ffb0072b0c3f56d9` | Required |
| **Sega Saturn** | `sega_101.bin` | Japan BIOS | `85ec9ca47d8f6807718151cbcca8b964` | Required for JP |
| **Sega Dreamcast** | `dc_boot.bin` | Main System BIOS | `e10c53c2f8b90bab96ead2d368858623` | Required |
| **Sega Dreamcast** | `dc_flash.bin` | Flash NVRAM | `74e3f69c2bb92bc1e57a24050e639655` | Recommended |
| **Nintendo GBA** | `gba_bios.bin` | Official Boot ROM | `a860e8c0b6d573d191e4ec7db1b1e4f6` | Optional |
| **SNK Neo Geo** | `neogeo.zip` | Universal UniBIOS / MVS | Multi-hash archive | Required |
| **PC Engine CD** | `syscard3.pce` | Super CD-ROM² System Card v3.0 | `ff1a674273fe3540ccef576376407e1d` | Required |

---

## 3. Using the BIOS Verification Matrix

1. Navigate to **Settings** → **BIOS Verification**.
2. The matrix lists all configured platforms with their required and optional firmware files.
3. Place your BIOS files in your designated BIOS directory (e.g., `/Volumes/Games/BIOS/` or configured RetroArch `system/` directory).
4. Click **Scan BIOS Files**:
   * Controllarr scans the folder, compares CRC32/MD5/SHA-1 hashes against official catalogs, and displays immediate status indicators:
     * 🟢 **Verified**: Correct filename, correct checksum, ready to play.
     * 🟡 **Name Mismatch**: Correct checksum found under an alternate filename (Controllarr offers a 1-click rename button).
     * 🔴 **Missing**: BIOS file is absent.

---

## 4. Hardware Syncing with Exporter

When using the [Hardware Target Exporter](Hardware-Target-Exporter.md) to build an SD card for MiSTer, OnionOS, Analogue Pocket, or flash carts, Controllarr automatically copies verified BIOS files into the appropriate hardware directories (e.g. `/bios/`, `/system/`, or `/Assets/common/`) so you never have to manually hunt down firmware files.
