# Metadata Refactoring Plan

## 1. Architectural Audit: Decoupling TV Metadata Providers

Currently, the system is deeply coupled with TV metadata schemas (SkyHook, TheTVDB, Trakt, and TVMaze), tracking media hierarchically as `Series > Season > Episode`. To pivot this Sonarr fork towards tracking console ROMs, we need to adapt the schema to `Platforms > Titles > Versions`.

### Core Metadata Providers and Interfaces

The primary metadata sources and classes include:
- **SkyHook:** Located in `src/NzbDrone.Core/MetadataSource/SkyHook`. Specifically `SkyHookProxy.cs` implements fetching metadata from SkyHook (which wraps TVDB).
- **TheTVDB:** Represented largely by IDs and dependencies spread across indexers, models, and metadata sources.
- **Trakt:** Handled under `src/NzbDrone.Core/Notifications/Trakt/` and `src/NzbDrone.Core/ImportLists/Trakt/`.
- **TVMaze:** IDs used in various indexers and search requests.
- **Interfaces:** `IProvideSeriesInfo` and `ISearchForNewSeries` inside `src/NzbDrone.Core/MetadataSource/`. These define the contract for searching and retrieving information for series.

### Core Models

The core domain models representing media items are tightly coupled to the TV domain (found in `src/NzbDrone.Core/Tv/`):
- `Series.cs`: Features strong coupling via properties like `TvdbId`, `TvRageId`, `TvMazeId`, `ImdbId`, and TV-specific fields like `AirTime`, `Network`, `SeasonFolder`.
- `Season.cs`: Represents a TV season (`SeasonNumber`).
- `Episode.cs`: Represents an episode with fields like `SeriesId`, `TvdbId`, `SeasonNumber`, `EpisodeNumber`, `AbsoluteEpisodeNumber`, and `AirDate`.

### Impact on Other Subsystems

- **Indexers:** (e.g., `Newznab`, `BroadcastheNet`, `Torznab`) heavily rely on `tvdbid`, `tvmazeid`, and `season`/`episode` search parameters to find and parse releases.
- **Data Augmentation:** Features like Scene Mapping (`SceneMappingService.cs`, `XemService.cs`) are heavily reliant on mapping scene names and numbering back to `TvdbId`.
- **Tracked Downloads & Import Lists:** All heavily depend on `TvdbId`.

## 2. Refactoring Path: Introducing Game Metadata (Platforms > Titles > Versions)

To support tracking console ROMs using an abstract game metadata provider (e.g., IGDB, OpenVGDB), the following architectural changes are necessary:

### Domain Model Changes

Instead of modifying existing classes immediately, a safer approach is to introduce new generalized concepts or replace existing ones:
- **Series -> Platform/Title:** `Series` should be abstracted to `GameTitle` (or `Title`). The properties like `TvdbId` must be replaced with an agnostic `ProviderId` or specific IDs like `IgdbId`.
- **Season -> Platform:** A collection of titles often maps to a `Platform` (e.g., SNES, Genesis). The hierarchy might actually be `Platform -> Title -> Version` instead of `Series -> Season -> Episode`.
- **Episode -> Version/Release:** `Episode` should be replaced with `GameVersion` or `RomRelease` tracking versions (e.g., regions, revisions).

### Interface Modifications

The `IProvideSeriesInfo` and `ISearchForNewSeries` interfaces in `src/NzbDrone.Core/MetadataSource/` need to be replaced or adapted:
```csharp
// Current
public interface IProvideSeriesInfo {
    Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Language language, string seasonType);
}

// Proposed for Games
public interface IProvideGameInfo {
    Tuple<GameTitle, List<GameVersion>> GetGameInfo(string providerId, Language language);
}
```

### Provider Implementations

- **Remove/Deprecate:** `SkyHookProxy.cs` and Trakt integrations.
- **Add:** Implement `IProvideGameInfo` and `ISearchForNewGames` using a new proxy (e.g., `IgdbProxy.cs` or `OpenVgdbProxy.cs`).

### Indexer Changes

- Indexer search parameters must be updated to search for game titles, platforms, and version strings rather than `season`/`episode`/`tvdbid`.
- Parsers (`TorznabRssParser`, `NewznabRssParser`) must extract game release names and map them to `GameTitle` and `GameVersion` instead of parsing S01E01 patterns.

### File Modification List (High-Level)

**Models:**
- `src/NzbDrone.Core/Tv/Series.cs` -> Refactor to `GameTitle.cs`
- `src/NzbDrone.Core/Tv/Season.cs` -> Refactor to `Platform.cs` (or adjust hierarchy)
- `src/NzbDrone.Core/Tv/Episode.cs` -> Refactor to `GameVersion.cs`

**Metadata Source:**
- `src/NzbDrone.Core/MetadataSource/IProvideSeriesInfo.cs`
- `src/NzbDrone.Core/MetadataSource/ISearchForNewSeries.cs`
- `src/NzbDrone.Core/MetadataSource/SkyHook/` -> To be replaced by Game Metadata Provider directory (e.g., `Igdb/`).

**Data Augmentation & Mapping:**
- `src/NzbDrone.Core/DataAugmentation/Scene/SceneMappingService.cs` -> Update to map ROM scene names to standard game titles.
- `src/NzbDrone.Core/DataAugmentation/Xem/` -> Likely to be removed or replaced with game equivalent.

**Indexers (Parsing & Search):**
- `src/NzbDrone.Core/Indexers/Newznab/NewznabRequestGenerator.cs`
- `src/NzbDrone.Core/Indexers/Newznab/NewznabRssParser.cs`
- `src/NzbDrone.Core/Indexers/Torznab/`

**Import Lists:**
- `src/NzbDrone.Core/ImportLists/Trakt/` -> Remove or replace with user lists from IGDB/custom.

## 3. Conclusion

The refactoring is highly intrusive because Sonarr's core logic is deeply intertwined with TV metadata identifiers (especially TVDB) and episodic naming patterns.
The transition to a generic game metadata provider will require replacing the core media hierarchy (`Series -> Season -> Episode`) with (`Platform -> Title -> Version`), fundamentally altering metadata ingestion, indexer searching, and release parsing.
