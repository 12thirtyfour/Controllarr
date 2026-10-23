# Controllarr Project Roadmap

## Phase 1: Foundation & Architecture Baseline (Completed)
* Step 1: Forked and adapted the core Sonarr architecture framework for console gaming domain models
* Step 2: Upgraded backend infrastructure to target .NET 10 runtime and updated global project dependencies
* Step 3: Replaced legacy build tooling and integrated Vite into the frontend build and asset pipeline
* Step 4: Established initial database schema migration baselines (Migrations 237–240) for platform and metadata tracking
* Step 5: Configured local developer pre-flight tooling, editor configurations, and strict StyleCop rulesets (`SA1200`, `SA1028`)
* Step 6: Initialized CI/CD workflow actions, cross-platform build pipelines, and passed local Alpha Verification Gate #1 on macOS

## Phase 2: Core Pipeline Adaptation, Ingestion & Hardware Target Integration (Completed)
* Step 1: Hardware Target Game Database (HTGDB / SMDB) Integration (Source Material DataStructures and record parsing models)
* Step 2: Root Folder & Platform Path Hierarchy (`\RootFolder\Manufacturer\Console\Region\`)
* Step 3: Per-Console Extension Mappings & Core ROM Ingestion Pipeline
* Step 4: Acquisition Protocol Priority (HTTP / Usenet / Torrent via Migration 241)
* Step 5: Native Internet Archive S3/HTTP Downloader Integration (Including user-defined Preferred Archives configuration)
* Step 6: Verification Gate #2 (Local Disk Scan & Acquisition Test)

## Phase 3: Rules Engine, Naming Presets & Core UI Modernization (React & Vite SPA) (Completed)
* Step 1: Advanced 1G1R Strategy Engine (1 Game/Console vs. 1 Game/Region/Console with Console-level Override)
* Step 2: ROM Naming Engine & Presets (Custom `*arr` Tokens + SmokeMonster / HTGDB Presets)
* Step 3: Core UI Shell Adaptation (Terminology & Platform Views)
* Step 4: Game Library Grids, Platform Detail & 1G1R Revision Views
* Step 5: Global Game Search & Smart Add Workflow (Search individual games across all platforms, auto-provision parent consoles, and mark target titles as wanted; excludes ROM hacks/homebrew by default with strict cryptographic hash-gating for future unofficial support)
* Step 6: Verification Gate #3 (End-to-End System Smoke Test)

## Phase 4: Media Optimization, Symlink Views & BIOS Management (Completed)
* Step 1: Platform BIOS Tracking & Verification Engine (Core system requirements foundation)
* Step 2: Integrated Disc Compression (`chdman` & `rvz`) (File optimization pipeline)
* Step 3: Dynamic Virtual Symlink Library Engine (Live network/local symlink trees for MiSTer, RetroArch, EmulationStation, Batocera, etc.)

## Phase 5: Packaging & Core Stability Baseline (Completed)
* Step 1: Documentation Baseline & Core Release Candidate Validation
* Step 2: Multi-Architecture Docker & Standalone Binary Packaging (Locking in the stable core runtime prior to new ecosystem additions)

## Phase 6: Ecosystem Integration & Interactive Views (Completed)
* Step 1: SD Card Multi-Device Exporter Engine & Modal (MiSTer FPGA, RetroArch, Batocera, EmulationStation/ES-DE, ArkOS/ROCKNIX, GarlicOS, OnionOS, Analogue Pocket, EverDrive, FXPak Pro, EZ-Flash, R4/TWiLight Menu++, and ODEs [xStation, Swiss/GC Loader, Fenrir, GDEMU] with FAT32 alphabetical buckets)
* Step 2: Platform BIOS Verification Matrix Settings View & API (Canonical hash matching, file verification badges, missing BIOS inspection)
* Step 3: RetroAchievements Integration (Credential Setup & Validation, Badge Showcase Modal, Mastery Progress Bar, Leaderboard Stats)
* Step 4: WebAssembly/Libretro Player & Desktop App Launcher ("Play Now" in-browser and `controllarr://play/{gameId}` desktop protocol)

## Phase 7: Beta Hardening, Brand Identity & Platform Stabilization (Completed)
* Step 1: Brand Unification & Asset Generation (Master logo vectorization and generation of all 38 icons, tiles, SVGs, and executables)
* Step 2: Comprehensive Sonarr to Controllarr UI and localization rebranding
* Step 3: Usenet Indexer Category Alignment (Console category `1000` & Other category `8000` defaults, removed filter suppressing `1000`, fixed preset overrides)
* Step 4: Dual REST API Parity (Exposed all gaming controllers across both `/api/v3` and `/api/v5`)
* Step 5: Cross-Platform Build & Runtime Fixes (Fixed StyleCop MSBuild path resolution for standalone csproj builds, fixed `GetUiFolder` Release path resolution)
* Step 6: CI/CD Test Harness Stabilization (Passed 100% of all 33 GitHub Actions matrix jobs across Linux, macOS Apple Silicon & Intel, Windows, and PostgreSQL 16–18)

## Phase 8: Beta Testing, Physical Device Verification & v1.0 Release Candidate (Current / Upcoming)
* Step 1: Local macOS, Linux, and Windows beta deployment and testing
* Step 2: Physical hardware target verification on flash carts and handhelds (EverDrive, MiSTer, OnionOS)
* Step 3: Community feedback triage and edge-case fixes
* Step 4: v1.0.0 Release Candidate (RC) tag and stable release distribution
