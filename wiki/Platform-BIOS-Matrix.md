# Platform BIOS Verification Matrix & Auto-Downloader

Many consoles—especially optical disc and 32-bit/64-bit systems—require proprietary system firmware (BIOS) files to boot games, play audio tracks, or run without emulation glitches.

Controllarr includes a built-in **Platform BIOS Verification Matrix** accessible globally under **Settings** → **BIOS Verification** (and via `/api/v3/bios` and `/api/v5/bios`), as well as directly inside each platform's detail page.

---

## 1. Storage Location & Automatic Handling

To keep platform assets cleanly isolated and portable, BIOS files in Controllarr are stored automatically inside each platform's root directory:

```text
{PlatformPath}/bios/
```

* **No Global Path Required**: You do not need to configure complex global BIOS paths or environment variables. Controllarr automatically creates and manages the `{PlatformPath}/bios/` folder for each configured console.
* **Emulation Readiness**: When launching games via the Libretro in-browser player or exporting libraries via [Hardware Target Exporter](Hardware-Target-Exporter.md), Controllarr sources verified firmware files directly from this directory.

---

## 2. Platform Details Page Integration

Each system's **Platform Details** page includes a dedicated **System BIOS & Firmware** section:

* Displays a real-time status card for every required and optional firmware file for that console.
* Lists file checksums (MD5, SHA-1, CRC32), file size specifications, and specific region/purpose notes (e.g., PS1 USA vs. Europe vs. Japan BIOS).
* Displays live status indicators:
  * 🟢 **Verified**: Correct filename and exact matching checksum in `{PlatformPath}/bios/`.
  * 🟡 **Name Mismatch**: Correct checksum detected under a non-canonical filename (includes 1-click auto-rename).
  * 🔴 **Missing**: File is absent from `{PlatformPath}/bios/`.

---

## 3. BIOS Auto-Downloader (Internet Archive Repository)

When firmware files are missing, Controllarr provides automated retrieval powered by the official **Internet Archive RetroArch BIOS repository**:

### Per-File "Download Missing" Button
Beside any missing or unverified BIOS file entry in the matrix or Platform Details page, click **Download Missing** to fetch that specific firmware file directly into `{PlatformPath}/bios/`. Controllarr automatically hashes and verifies the downloaded file upon completion.

### Bulk "Download Missing BIOS" Button
At the top of the **BIOS Verification Matrix** and the **System BIOS & Firmware** section, click **Download Missing BIOS** to automatically identify, queue, and download all missing required and optional firmware files across platforms in a single automated batch operation.

---

## 4. Common System BIOS Requirements

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

## 5. Hardware Syncing with Exporter

When using the [Hardware Target Exporter](Hardware-Target-Exporter.md) to build an SD card for MiSTer, OnionOS, Analogue Pocket, or flash carts, Controllarr automatically copies verified BIOS files from `{PlatformPath}/bios/` into the appropriate hardware target directories (e.g., `/bios/`, `/system/`, or `/Assets/common/`) so your destination device boots immediately without manual file transfers.
