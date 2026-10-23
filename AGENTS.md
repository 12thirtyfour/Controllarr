# Agent Directives: Controllarr

## Identity & Architecture
- Lead engineering agent for Controllarr, an automated console ROM and game library manager for Usenet and BitTorrent users, forked from the Sonarr architecture pattern.
- Primary Branch Target: develop (transitioning to main).
- Target Application Port: 7050.

## Tech Stack & Persistence
- .NET 10 backend
- React/Vite TypeScript frontend
- Dual SQLite/PostgreSQL persistence layer

## Coding Standards
- Strict StyleCop compliance (mandatory adherence to rules such as `SA1200` using-directives inside namespaces).
- xUnit test execution with **Moq** for test isolation.
- Clean domain-driven separation mirroring Sonarr's indexer/provider/download client model.
- Zero-Warnings Policy: `<TreatWarningsAsErrors>true` is strictly enforced. Release builds must output 0 errors and 0 warnings.
- No Blanket Formatters: Running `dotnet format` or global whitespace cleanup scripts is strictly forbidden. Edit only lines directly pertinent to the task.
- Offline Testing: All unit tests must run offline without live network dependencies or external endpoints.
- Atomic Changes: Do not combine global refactoring with architectural feature work.

## Domain Curation Philosophy
- Retail-first priority using No-Intro, Redump, and TOSEC data structures.
- Hardware Target Game Database (HTGDB) / SourceMaterial Database (SMDB) folder layouts for flash-carts and ODEs (MiSTer, OnionOS, Analogue Pocket, EverDrive, etc.).
- Strict cryptographic hash-gating for unofficial content, ROM hacks, and homebrew.

## Pre-Commit Verification Gate
Before opening any PR, you must run and verify:
1. `dotnet build --configuration Release` (must yield 0 errors, 0 warnings).
2. `dotnet test --filter "FullyQualifiedName!~Integration"` (must pass cleanly, ignoring known legacy Sonarr.Http.Test failures).
