# Supported Platforms & Regional Model

Controllarr organizes video game console, handheld, and hardware add-on libraries using a standardized **7-Region Model** and canonical system metadata standards.

---

## The 7-Region Model

In Controllarr, each platform's catalog is partitioned into seven distinct regional buckets (historically represented as season buckets in the underlying schema). This structure organizes commercial retail releases, multi-region titles, and community-created software into clear, predictable subfolders.

| Region ID | Regional Bucket Name | Description | Default Monitored | Verification / Hash Gating |
|---|---|---|---|---|
| **Region 1** | **USA (NTSC-U)** | Commercial retail releases for North America | Enabled (`true`) | No-Intro / Redump DAT checksums |
| **Region 2** | **Europe (PAL)** | Commercial retail releases for European / PAL territories | Enabled (`true`) | No-Intro / Redump DAT checksums |
| **Region 3** | **Japan (NTSC-J)** | Commercial retail releases for Japan / NTSC-J territories | Enabled (`true`) | No-Intro / Redump DAT checksums |
| **Region 4** | **World / Multi-Region** | Global releases, worldwide builds, and multi-region dumps | Enabled (`true`) | No-Intro / Redump DAT checksums |
| **Region 5** | **Homebrew & Aftermarket** | Non-commercial homebrew games, indie hardware cartridges, and modern aftermarket releases | Disabled (`false`) | Hash-gated against HTGDB / SMDB databases |
| **Region 6** | **ROM Hacks & Translations** | Fan translation patches, quality-of-life ROM hacks, and community overhauls | Disabled (`false`) | Hash-gated against HTGDB / SMDB databases |
| **Region 0** | **Prototypes, Betas & Unlicensed** | Unreleased prototype builds, pre-launch beta samples, tech demos, and unlicensed commercial software | Disabled (`false`) | Verified prototype / beta DAT manifests |

---

## Directory Organization & Regional Folders

When regional subfolders are enabled (`seasonFolder: true`), Controllarr automatically structures system directories according to the Hardware Target Game Database (HTGDB) standards:

```text
{RootFolder}/
  └── Nintendo Entertainment System/
      ├── USA/
      │   └── Legend of Zelda, The (USA) (Rev 1).nes
      ├── Europe/
      │   └── Legend of Zelda, The (Europe).nes
      ├── Japan/
      │   └── Zelda no Densetsu 1 (Japan).nes
      ├── World/
      ├── Homebrew & Aftermarket/
      │   └── Micro Mages (Homebrew).nes
      ├── ROM Hacks & Translations/
      │   └── Legend of Zelda, The - Redux (ROM Hack).nes
      └── Prototypes, Betas & Unlicensed/
          └── EarthBound (USA) (Proto).nes
```

---

## Supported Console Matrix

### 1. Nintendo

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **Nintendo Entertainment System (NES)** | `nes` | Cartridge | `.nes`, `.unf`, `.fds` | ZIP / 7z |
| **Famicom Disk System (FDS)** | `fds` | Magnetic Disk | `.fds` | ZIP / 7z |
| **Super Nintendo Entertainment System (SNES)** | `snes` | Cartridge | `.sfc`, `.smc` | ZIP / 7z |
| **Nintendo 64 (N64)** | `n64` | Cartridge | `.z64`, `.n64`, `.v64` | ZIP / 7z |
| **Nintendo GameCube** | `gamecube`| Optical Disc | `.iso`, `.rvz`, `.ciso`, `.gcm` | `.rvz` (via `dolphin-tool`) |
| **Nintendo Wii** | `wii` | Optical Disc | `.iso`, `.rvz`, `.wbfs` | `.rvz` (via `dolphin-tool`) |
| **Nintendo Wii U** | `wiiu` | Optical Disc | `.wux`, `.wud`, `.rpx` | Extracted raw format |
| **Nintendo Switch** | `switch` | Cartridge/Flash | `.xci`, `.nsp` | Raw / Compressed |

---

### 2. Nintendo Handhelds

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **Game Boy** | `gb` | Cartridge | `.gb` | ZIP / 7z |
| **Game Boy Color** | `gbc` | Cartridge | `.gbc` | ZIP / 7z |
| **Game Boy Advance** | `gba` | Cartridge | `.gba` | ZIP / 7z |
| **Nintendo DS** | `nds` | Flash Card | `.nds` | ZIP / 7z |
| **Nintendo 3DS** | `3ds` | Flash Card | `.3ds`, `.cia` | Raw |
| **Virtual Boy** | `vb` | Cartridge | `.vb` | ZIP / 7z |

---

### 3. Sega

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **SG-1000** | `sg1000` | Cartridge | `.sg` | ZIP / 7z |
| **Sega Master System** | `sms` | Cartridge | `.sms` | ZIP / 7z |
| **Sega Genesis / Mega Drive** | `genesis` | Cartridge | `.md`, `.smd`, `.bin`, `.gen` | ZIP / 7z |
| **Sega CD / Mega CD** | `segacd` | Optical Disc | `.chd`, `.cue`, `.bin`, `.iso` | `.chd` (via `chdman`) |
| **Sega 32X** | `sega32x` | Cartridge | `.32x`, `.bin` | ZIP / 7z |
| **Sega Saturn** | `saturn` | Optical Disc | `.chd`, `.cue`, `.bin`, `.iso` | `.chd` (via `chdman`) |
| **Sega Dreamcast** | `dreamcast`| Optical Disc | `.chd`, `.cdi`, `.gdi`, `.cue` | `.chd` (via `chdman`) |
| **Sega Game Gear** | `gamegear` | Cartridge | `.gg` | ZIP / 7z |

---

### 4. Sony

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **PlayStation (PS1)** | `ps1` | Optical Disc | `.chd`, `.cue`, `.bin`, `.pbp`, `.iso` | `.chd` (via `chdman`) |
| **PlayStation 2 (PS2)** | `ps2` | Optical Disc | `.chd`, `.iso`, `.cso` | `.chd` (via `chdman`) |
| **PlayStation 3 (PS3)** | `ps3` | Blu-ray Disc | `.iso`, Folder structure (`PS3_GAME`) | `.iso` |
| **PlayStation Portable (PSP)**| `psp` | UMD Disc | `.iso`, `.cso`, `.chd` | `.cso` / `.chd` |
| **PlayStation Vita** | `psvita` | Cartridge/Flash | `.vpk`, `.zip`, NoNpDrm folders | Raw |

---

### 5. Microsoft

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **Microsoft Xbox (Original)** | `xbox` | Optical Disc | `.iso`, `.xbe`, Folder structure | `.iso` (XISO) |
| **Microsoft Xbox 360** | `xbox360` | Optical Disc | `.iso`, `.xex`, GOD format | Raw / `.iso` |

---

### 6. NEC & SNK

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **TurboGrafx-16 / PC Engine** | `pce` | HuCard | `.pce` | ZIP / 7z |
| **PC Engine CD-ROM²** | `pcecd` | Optical Disc | `.chd`, `.cue`, `.bin` | `.chd` (via `chdman`) |
| **PC-FX** | `pcfx` | Optical Disc | `.chd`, `.cue`, `.bin` | `.chd` (via `chdman`) |
| **Neo Geo AES / MVS** | `neogeo` | Cartridge | `.zip`, `.7z`, `.neo` | ZIP / 7z |
| **Neo Geo CD** | `neogeocd` | Optical Disc | `.chd`, `.cue`, `.bin` | `.chd` (via `chdman`) |
| **Neo Geo Pocket / Color** | `ngp` | Cartridge | `.ngp`, `.ngc` | ZIP / 7z |

---

### 7. Classic & Specialty Systems

| Platform | Short Code | Media Type | Supported Extensions | Recommended Compression |
|---|---|---|---|---|
| **Atari 2600** | `atari2600`| Cartridge | `.a26`, `.bin` | ZIP / 7z |
| **Atari 5200** | `atari5200`| Cartridge | `.a52`, `.bin` | ZIP / 7z |
| **Atari 7800** | `atari7800`| Cartridge | `.a78`, `.bin` | ZIP / 7z |
| **Atari Lynx** | `lynx` | Cartridge | `.lnx` | ZIP / 7z |
| **Atari Jaguar / Jaguar CD** | `jaguar` | Cartridge/CD | `.j64`, `.jag`, `.chd` | ZIP / `.chd` |
| **3DO Interactive Multiplayer**| `3do` | Optical Disc | `.chd`, `.iso`, `.cue` | `.chd` (via `chdman`) |
| **Philips CD-i** | `cdi` | Optical Disc | `.chd`, `.iso`, `.cue` | `.chd` (via `chdman`) |
| **Bandai WonderSwan / Color** | `wonderswan`| Cartridge | `.ws`, `.wsc` | ZIP / 7z |
| **ColecoVision** | `colecovision`| Cartridge | `.col`, `.bin` | ZIP / 7z |
| **Mattel Intellivision** | `intellivision`| Cartridge | `.int`, `.bin` | ZIP / 7z |
| **Vectrex** | `vectrex` | Cartridge | `.vec`, `.bin` | ZIP / 7z |

---

## Multi-Disc Handling & Playlist Generation

For optical disc platforms containing multidisc games (e.g., *Final Fantasy VII* on PS1 or *Resident Evil Code: Veronica* on Dreamcast):

1. **Disc Tokens**: Multi-disc titles are cataloged with explicit disc numbers:
   ```text
   Final Fantasy VII (USA) (Disc 1).chd
   Final Fantasy VII (USA) (Disc 2).chd
   Final Fantasy VII (USA) (Disc 3).chd
   ```
2. **Automatic `.m3u` Playlist Creation**: Controllarr generates `.m3u` playlist files automatically alongside multi-disc sets, allowing frontend emulators, RetroArch, and handheld targets to present multidisc titles as a single unified entry while facilitating smooth disc swapping.
