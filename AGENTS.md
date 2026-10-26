# Agent Directives: Controllarr

## Identity & Architecture
- Lead engineering agent for Controllarr, an automated console ROM and game library manager for Usenet and BitTorrent users, forked from the Sonarr architecture pattern.
- Branch Model: `develop` is the staging/testing branch (no automated CI/CD builds on push; dev users build from source); `main` is the production/stable branch that runs full multi-architecture builds, checks, packaging (all 10 OS/arch targets + multi-arch Docker), and publishes official `v1.0.0+` GitHub Releases.
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

## Proportional Verification & Push Hygiene
Match local build/test verification to the scope of the changes made:
1. **Documentation-only changes** (`README.md`, `ROADMAP.md`, `CHANGELOG.md`, `THIRD_PARTY_NOTICES.md`, `AGENTS.md`, Wiki): Do **not** run `dotnet build`, `dotnet test`, or `yarn build`.
2. **Frontend UI / CSS / cosmetic-only changes**: Run `corepack yarn --cwd frontend build` (and `typecheck` when TypeScript interfaces change); do **not** run backend `dotnet build` or `dotnet test`.
3. **Backend C# logic changes**: Run `dotnet build src/Controllarr.sln --configuration Release` (must yield 0 errors, 0 warnings) and run **targeted** unit tests (`dotnet test --no-build --configuration Release --filter "FullyQualifiedName~<RelevantFixture>"`) covering the modified service/domain.
4. **Full Release / Merge-to-Main Gate**: Run full `dotnet build --configuration Release`, `corepack yarn --cwd frontend build`, and `dotnet test --filter "FullyQualifiedName!~Integration"` before merging `develop` into `main` for an official release.
5. **Continuous Push to Develop**: Push verified changes to `origin/develop` immediately upon completing and locally validating each task or fix to facilitate seamless syncing across multiple test machines. Prior to every push, strictly enforce local testing to guarantee 0 errors and 0 warnings (0e0w). Do not worry about GitHub Actions or remote CI workflows on push to develop.

## Terminal Execution (Windows)
- Always execute `git add`, `git commit`, `git push`, `gh`, `dotnet build`, and `dotnet test` directly without attempting a failing sandboxed run first (the Windows sandbox restricts `.git/index.lock` writes and `vstest.console` loopback sockets).

## Jules Delegation & 5-Minute Check-In Protocol
- **Proactive Task Splitting & Session Conservation**: For multi-part requests, evaluate whether independent, non-conflicting side tasks can be delegated to **Jules** via GitHub Issues (`gh issue create --label "jules"`). To conserve the 100/day session limit, bundle related side tasks into a single comprehensive issue rather than spawning multiple small issues, and **always reuse existing sessions** for follow-up changes or fixes by commenting on the existing Issue or PR thread instead of creating a new issue.
- **Exact File & Symbol Verification**: Before creating a GitHub issue for Jules, verify that all file paths and class/method names referenced in the issue body exist in `origin/develop` and do not overlap with files being actively edited locally.
- **Automatic 5-Minute Check-In Schedule**: Whenever one or more tasks are delegated to Jules, immediately set a recurring 5-minute schedule (`CronExpression: "*/5 * * * *"`) using the `schedule` tool to check `gh pr list --state open` and `gh issue view <id> --comments`.
- **Review, Merge & Cleanup**: When Jules opens a PR, review the diff, verify it builds/tests cleanly, merge it into `develop` (`gh pr merge <PR> --merge --delete-branch`), pull `origin/develop`, and cancel the recurring schedule task via `manage_task` (`Action: "kill"`) once all delegated Jules tasks are complete.

## Documentation Synchronization Policy
- **Mandatory Continuous Documentation Updates**: All documentation (`CHANGELOG.md`, `ROADMAP.md`, `README.md`, `THIRD_PARTY_NOTICES.md`, and the official GitHub Wiki at `https://github.com/12thirtyfour/Controllarr.wiki.git`) must be kept 100% up to date at all times.
- **Changelog & Roadmap Lifecycle**:
  - Whenever a feature, bug fix, or architectural change is completed, immediately document it in `CHANGELOG.md`.
  - Whenever a roadmap item is completed, move it off the active section of `ROADMAP.md` into `CHANGELOG.md`.
  - Whenever new feature ideas or enhancements are pitched by the user, add them to `ROADMAP.md`.
- **Third-Party Software & Attribution Synchronization**: Whenever third-party libraries, CLI tools, emulation runtimes, metadata scrapers/APIs, or preservation databases are added, removed, or modified, immediately update `THIRD_PARTY_NOTICES.md` (and the Credits section of `README.md`).
- **Wiki Synchronization**: Whenever user-facing workflows, settings, curation rules, or supported hardware/platforms change, update the corresponding pages in `Controllarr.wiki.git` in the same session.
