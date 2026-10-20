# Agent Directives: Controllarr

## Core Architecture
- Platform: .NET 10 backend, React/Vite frontend.
- Target Application Port: 7050.
- Primary Branch Target: develop (transitioning to main).

## Build & Quality Mandates
- Zero-Warnings Policy: `<TreatWarningsAsErrors>true` is strictly enforced. Release builds must output 0 errors and 0 warnings.
- No Blanket Formatters: Running `dotnet format` or global whitespace cleanup scripts is strictly forbidden. Edit only lines directly pertinent to the task.
- Offline Testing: All unit tests must run offline without live network dependencies or external endpoints.
- Atomic Changes: Do not combine global refactoring with architectural feature work.

## Pre-Commit Verification Gate
Before opening any PR, you must run and verify:
1. `dotnet build --configuration Release` (must yield 0 errors, 0 warnings).
2. `dotnet test --filter "FullyQualifiedName!~Integration"` (must pass cleanly, ignoring known legacy Sonarr.Http.Test failures).
