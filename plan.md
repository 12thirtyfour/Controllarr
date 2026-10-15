1. **Model Updates (`src/NzbDrone.Core/Games`)**:
   - Update `src/NzbDrone.Core/Games/GamePlatform.cs` to inherit from `ModelBase` and include requested fields: `Id`, `Name`, `Slug`, `IgdbId`, `Overview`, `Path`, `Monitored`, `RootFolderPath`, etc.
   - Update `src/NzbDrone.Core/Games/GameTitle.cs` to inherit from `ModelBase` and include requested fields: `Id`, `PlatformId`, `Title`, `CleanTitle`, `IgdbId`, `Monitored`, etc.
   - Update `src/NzbDrone.Core/Games/GameRelease.cs` to inherit from `ModelBase` and include requested fields: `Id`, `GameTitleId`, `PlatformId`, `Title`, `VersionTag`, `Revision`, `Region`, `Monitored`, `FileId`, etc.

2. **Repository Updates (`src/NzbDrone.Core/Games`)**:
   - Create `src/NzbDrone.Core/Games/GamePlatformRepository.cs` and interface `IGamePlatformRepository` inheriting from `BasicRepository<GamePlatform>` and `IBasicRepository<GamePlatform>`.
   - Create `src/NzbDrone.Core/Games/GameTitleRepository.cs` and interface `IGameTitleRepository` inheriting from `BasicRepository<GameTitle>` and `IBasicRepository<GameTitle>`.
   - Create `src/NzbDrone.Core/Games/GameReleaseRepository.cs` and interface `IGameReleaseRepository` inheriting from `BasicRepository<GameRelease>` and `IBasicRepository<GameRelease>`.
   - Verify creation using `ls src/NzbDrone.Core/Games/Game*Repository.cs`.

3. **DI Registration**:
   - Given Sonarr uses Scrutor/Auto-Registration by convention (as it doesn't use `ContainerBuilder` or explicitly map interfaces), creating these types adhering to naming conventions (`IGamePlatformRepository` -> `GamePlatformRepository`) works automatically. Will verify during the build.

4. **Database Migration (`src/NzbDrone.Core/Datastore/Migration`)**:
   - Check the highest migration number (e.g. `ls src/NzbDrone.Core/Datastore/Migration/ | grep -E '^[0-9]+' | sort -n | tail -n 1`). Currently, the last migration is ~236. Create a script named `237_create_game_tables.cs` using FluentMigrator.
   - Script creates `GamePlatforms`, `GameTitles`, `GameReleases` with appropriate IDs and constraints.
   - Read the created file back to verify contents.

5. **Fix Compilation Errors**:
   - Run `dotnet build src/NzbDrone.Core/NzbDrone.Core.csproj`.
   - Update `using NzbDrone.Core.Tv;` to `using NzbDrone.Core.Games;` in `src/NzbDrone.Core/MetadataSource/IGameMetadataProxy.cs`.
   - Resolve any other namespace or compilation errors caused by modifying these models.
   - Run `dotnet build src/NzbDrone.Core/NzbDrone.Core.csproj` to verify fixes.

6. **Test execution**:
   - Run unit tests: `bash scripts/test.sh Linux Unit Test` to confirm there are no regressions.

7. **Pre-commit Steps**:
   - Complete pre commit steps to make sure proper testing, verifications, reviews and reflections are done.

8. **Submit**:
   - Create a pull request.
