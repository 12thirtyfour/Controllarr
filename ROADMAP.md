# Controllarr Roadmap

## Phase 1: Foundation & Architecture Baseline [COMPLETED]
* Repository synchronization, Sonarr structural adaptation, base project files, and CI/CD pre-flight checks.

## Phase 2: Core Data Ingestion & Storage Pipeline [CURRENT ACTIVE]
* SQLite/PostgreSQL dual persistence layer implementation, HTGDB/SMDB record parsing models, and ROM hash indexing.

## Phase 3: Acquisition & Indexer Integration
* Torznab/Newznab indexers customized with game-specific attributes (ROM formats, region tagging, download/upload volume factors).

## Phase 4: Curation, Unpacking & UI/UX Dashboard
* No-Intro/Redump matching logic, automated archive unpacking (`.zip`, `.7z`, `.rar`), optional `chdman` disc compression, and React frontend poster/library management views.

## Phase 5: Optimization & Smart Add Engine
* Dynamic network symlinks, BIOS tracking matrices, and automated console onboarding.

## Phase 6: Pre-Release Repository Cleanup & Code Optimization
* Mandatory pre-release gate for scrubbing orphaned files, removing accidental scratch files, pruning test artifacts, profiling memory, and tightening async loops.

## Phase 7: Device Packaging, SD Card Exporters & Ecosystem Polish
* Multi-device export profiles targeting hardware flash-carts and optical drive emulators (MiSTer, OnionOS, Analogue Pocket, EverDrive, EZ-Flash, FXPak Pro, R4).
