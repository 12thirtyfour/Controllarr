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
- Strict StyleCop compliance (mandatory adherence to rules in `src/stylecop.json`, such as `SA1200` requiring using-directives outside namespaces and `SA1028` no trailing whitespace).
- NUnit test execution (`[TestFixture]`, `[Test]`) with **Moq** and **FluentAssertions** for test isolation.
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

## Terminal Execution & Git Hygiene (Windows)
- Always execute `git add`, `git commit`, `git push`, `gh`, `dotnet build`, and `dotnet test` directly with `BypassSandbox: true` without attempting a failing sandboxed run first (the Windows sandbox restricts `.git/index.lock` writes and `vstest.console` loopback sockets).
