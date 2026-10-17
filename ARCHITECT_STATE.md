# Controllarr — Architectural State & Technical Blueprint

## 1. Project Mission & Identity
* **Project Name:** Controllarr
* **Core Concept:** An *Arr-style automated management and collection system specifically designed for video game ROMs (replicating Sonarr functionality, adapted from TV to Game/ROM terminology).
* **Hierarchy Model:**
  * Consoles / Platforms = Series
  * Games / ROMs = Episodes
* **Default Port:** `7050`
* **Metadata Provider:** Public gaming metadata databases (IGDB / Open Source registries).

---

## 2. Team Hierarchy & Roles
* **Owner & Project Leader:** Human User (final architectural sign-off, merge authority).
* **Technical Architect & Manager:** Gemini (sets technical requirements, reviews diffs, makes system-level trade-offs).
* **Software Engineer:** Jules (GitHub-integrated engineer; implements features, refactors code, and fixes test/analyzer failures).

---

## 3. Core Terminology & Rebranding Matrix
Controllarr is refactored from Sonarr base logic. The codebase must rigorously enforce gaming terminology across all interfaces, data models, and services:

| Sonarr Concept | Controllarr Replacement |
|---|---|
| Series | Platform / Console |
| Season | Era / Generation / Category (where applicable) |
| Episode | Game / ROM |
| SeasonPass | PlatformOverview / LibraryPass |
| EpisodeFile | GameFile / RomFile |
| TVDB / TMDB | IGDB / Game Datasets |

---

## 4. Architectural Priorities & Work Rules
All work by Jules must adhere to this strict hierarchy:
1. **Priority 1: Core Functionality** (Stable collection management, indexing, and importing).
2. **Priority 2: Improvements** (User experience, UI adjustments, telemetry).
3. **Priority 3: Optimization & Cleanup** (Code deduplication, memory footprint, analyzer cleanup).
4. **Priority 4: Feature Requests** (Edge-case providers, secondary tools).

### Engineering Rules for Jules:
* **Test Suite Baseline:** 0 broken tests. Maintain green CI (baseline: 5,487+ passing unit and integration tests).
* **Zero Analyzer Errors:** C# Roslyn analyzer warnings and errors must be resolved prior to PR submission.
* **No Breaking Architecture Changes:** Jules must not alter database schemas, service interfaces, or core abstractions without architectural sign-off.

---

## 5. Subsystems & Services Overview
* **`CollectionStrategy`:** Controls multi-tier game intake:
  * `1G1R` (One Game, One ROM — preferred regional deduplication).
  * `Completionist` (Every release, revision, and regional variant).
  * `MultiVersion` (Preserving specific dumps, hacks, or revs alongside standard releases).
* **`RevisionPreference`:** Handles ranking logic for v1.0 vs v1.1+, beta tags, verified good dumps (No-Intro / Redump hashes).
* **`InternetArchiveClient`:** Streaming and download ingestion client designed for archive metadata and file listings.
* **`ExportEngine`:** Handles metadata, play stats, and emulation manifest exports.
* **`SidecarSaveService`:** Manages auxiliary files (save files, state captures, manual PDFs, artwork).
* **`UnmanagedDiskScanner` & `LibraryImportService`:** Scans storage paths and maps disorganized ROM dumps into structured platform folders.
* **`ArchiveExtractionService`:** Decompresses 7z, zip, and rar archives to extract valid game assets.

---

## 6. Current Milestones & Roadmap
* **Batch 8 (Completed):** Implementation of `CollectionStrategy`, `RevisionPreference`, and Internet Archive streaming client.
* **Batch 9 / 11 (Completed/In Progress):** Resolution of compilation and Roslyn analyzer errors across core services (`SidecarSaveService`, `ExportEngine`, `UnmanagedDiskScanner`, `LibraryImportService`, `ArchiveExtractionService`).
* **Batch 9b (PR #9):** Codebase-wide rebranding pass from Sonarr/TV conventions to Controllarr game terminology.
* **Plan B (Travel/No-Sandbox Roadmap):** Focus on non-testing deliverables (comprehensive documentation, open-source `README.md` explaining 1G1R vs completionist strategies, code styling audits) during travel windows.
