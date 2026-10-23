# Controllarr Roadmap

## Phase 1: Foundation & Architecture Baseline (Completed)
* Step 1: Forked and adapted the core Sonarr architecture framework for console gaming domain models
* Step 2: Upgraded backend infrastructure to target .NET 10 runtime and updated global project dependencies
* Step 3: Replaced legacy build tooling and integrated Vite into the frontend build and asset pipeline
* Step 4: Established initial database schema migration baselines (Migrations 237–240) for platform and metadata tracking
* Step 5: Configured local developer pre-flight tooling, editor configurations, and strict StyleCop rulesets (`SA1200`, `SA1028`)
* Step 6: Initialized CI/CD workflow actions, cross-platform build pipelines, and passed local Alpha Verification Gate #1 on macOS

## Phase 2: Core Pipeline Adaptation, Ingestion & Hardware Target Integration (Active)
* Step 1: Hardware Target Game Database (HTGDB / SMDB) Integration (Source Material DataStructures and record parsing models)
* Step 2: Root Folder & Platform Path Hierarchy (`\RootFolder\Manufacturer\Console\Region\`)
* Step 3: Per-Console Extension Mappings & Core ROM Ingestion Pipeline
* Step 4: Acquisition Protocol Priority (HTTP / Usenet / Torrent via Migration 241)
* Step 5: Native Internet Archive S3/HTTP Downloader Integration (Including user-defined Preferred Archives configuration)
* Step 6: Verification Gate #2 (Local Disk Scan & Acquisition Test)

## Phase 3: Rules Engine, Naming Presets & Core UI Modernization (React & Vite SPA)
* Step 1: Advanced 1G1R Strategy Engine (1 Game/Console vs. 1 Game/Region/Console with Console-level Override)
* Step 2: ROM Naming Engine & Presets (Custom `*arr` Tokens + SmokeMonster / HTGDB Presets)
* Step 3: Core UI Shell Adaptation (Terminology & Platform Views)
* Step 4: Game Library Grids, Platform Detail & 1G1R Revision Views
* Step 5: Global Game Search & Smart Add Workflow (Search individual games across all platforms, auto-provision parent consoles, and mark target titles as wanted; excludes ROM hacks/homebrew by default with strict cryptographic hash-gating for future unofficial support)
* Step 6: Verification Gate #3 (End-to-End System Smoke Test)

## Phase 4: Media Optimization, Symlink Views & BIOS Management
* Step 1: Platform BIOS Tracking & Verification Engine (Core system requirements foundation)
* Step 2: Integrated Disc Compression (`chdman` & `rvz`) (File optimization pipeline)
* Step 3: Dynamic Virtual Symlink Library Engine (Live network/local symlink trees for MiSTer, RetroArch, EmulationStation, Batocera, etc.)

## Phase 5: Packaging & Core Stability Baseline
* Step 1: Documentation Baseline & Core Release Candidate Validation
* Step 2: Multi-Architecture Docker & Standalone Binary Packaging (Locking in the stable core runtime prior to new ecosystem additions)

## Phase 6: New Features (Ecosystem Integration & Interactive Views)
* Step 1: SD Card Multi-Device Exporter Engine (OnionOS, Analogue Pocket, EverDrive FAT32 alphabetical buckets)
* Step 2: RetroAchievements Integration (Credential Setup, Badge Showcase, Mastery Progress Bar)
* Step 3: WebAssembly/Libretro Player & Desktop App Launcher ("Play Now" Button)
