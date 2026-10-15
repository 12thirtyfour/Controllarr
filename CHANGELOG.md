# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Added / Changed

- **Batch 0 & 1:**
  - *What was changed:* Architectural pivot from TV to ROM collections. Renaming Series/Season/Episode domain concepts to GamePlatform/GameTitle/GameRelease.
  - *Why:* To natively track console ROM hierarchies instead of episodic television formats. Unified regional platforms chosen over distinct add-on platforms for simpler UI presentation.
- **Batch 2:**
  - *What was changed:* Implementation of IGDB client and Twitch OAuth.
  - *Why:* Replaces SkyHook/TheTVDB as the primary metadata source. Newtonsoft.Json retained over System.Text.Json for cross-solution Sonarr compatibility. Resolved Autofac DI registration via assembly scanning. Bypassed NLog warning assertions in test fixtures due to harness overrides.
- **Batch 3:**
  - *What was changed:* No-Intro/Redump/SmokeMonster parsing, deprecation of video resolutions in favor of dump verification flags, full-set archive detection, and gaming Torznab indexer classification.
  - *Why:* Transition the parsing and classification engine to handle standard console ROM release standards, archive packs, and full-set archives, and ensure query parameters to indexers target gaming/ROM categories instead of TV/Episode categories.
- **Batch 4:**
  - *What was changed:* Ingestion & Processing Pipeline including Archive Extraction/Repacking, Sidecar Save Syncing, CHDMAN disc conversions, System BIOS management, and Hardware Export Engine.
  - *Why:* To natively process incoming game archives, enforce canonical formats (Deflate level 9, stripping copier headers), safely backup sidecar save files alongside ROM moves, provide a framework for exporting directly to Flash Carts or Handheld setups, and ensure system firmware exists and is verified for emulation pipelines.
- **Batch 6:**
  - *What was changed:* Ingestion & Standardization Engine updates.
  - *Why:* Improved ROM parsing stability and expanded processing capabilities.
- **Batch 7:**
  - *What was changed:* Existing Library Scanner & Disk Importer.
  - *Why:* Implements unmanaged disk scanning capable of analyzing deeply nested or bucketed folder structures. Integrates ROM hash inspection directly within .zip central directories (avoiding costly full extractions), enabling non-destructive or destructive import pipelines (Copy vs Move). Sidecar save files are now properly synchronized in lockstep with canonical ROM moves/renames to prevent data loss.
