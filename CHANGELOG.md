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
