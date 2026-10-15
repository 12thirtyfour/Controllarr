# Metadata Refactoring Plan

## 1. Architectural Audit: Decoupling TV Metadata Providers

Currently, the system is deeply coupled with TV metadata schemas (SkyHook, TheTVDB, Trakt, and TVMaze), tracking media hierarchically as `Series > Season > Episode`. To pivot this Sonarr fork towards tracking console ROMs, we need to adapt the schema to `Platforms > Titles > Versions`.

### Delivery Roadmap

- **Batch 0 (Completed):** Architectural pivot from TV to ROM collections. Renaming Series/Season/Episode domain concepts to GamePlatform/GameTitle/GameRelease. Unified regional platforms vs distinct add-on platforms.
- **Batch 1 (Completed):** Core data models creation and base repository modifications.
- **Batch 2 (Completed):** Implementation of IGDB client and Twitch OAuth. Retained Newtonsoft.Json for cross-solution Sonarr compatibility. Resolution of Autofac DI registration via assembly scanning. Bypassed NLog warning assertions in test fixtures due to harness overrides.
- **Batch 3 (Completed):** No-Intro/Redump/SmokeMonster parsing, deprecation of video resolutions in favor of dump verification flags, full-set archive detection, and gaming Torznab indexer classification.
- **Batch 4 (Completed):** Ingestion & Processing Pipeline:
  - 1-to-many archive unpacking (full-set archive extraction and indexing).
  - PlatformFormatSettings: Allowed file extensions and file size boundaries per console platform.
  - System BIOS Management: Tracking required firmware, MD5/SHA1 checksum verification against DATs.
  - Non-destructive conversion policies: Explicit opt-in toggles (AutoCompressDiscsToChd default false; DeleteOriginalAfterConversion default false).
  - Automated chdman disc conversion (.bin/.cue/.iso -> .chd) with integrity verification before source cleanup.
  - Export Engine: Target profiles for SD cards and dynamic symlinks (MiSTer FPGA, OnionOS, Analogue Pocket, EverDrive, EZ-Flash, FXPak Pro, R4, SummerCart64) with alphabetical bucket structuring.
- **Batch 5 (Current): Frontend & Emulation:**
  - React UI overhaul for Consoles/ROMs/Releases.
  - Activity > Media Conversions dashboard with live stdout progress streaming, pause/resume/cancel controls, and batch conversion review drawers.
  - Libretro/WASM in-browser emulation testing.
