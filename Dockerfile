# syntax=docker/dockerfile:1

# ==============================================================================
# Stage 1: Build React / TypeScript / Vite Frontend SPA
# ==============================================================================
FROM --platform=$BUILDPLATFORM node:22-bookworm-slim AS frontend-build
WORKDIR /src

RUN corepack enable

COPY package.json yarn.lock .yarnrc tsconfig.json vite.config.ts index.html ./
RUN yarn install --frozen-lockfile --network-timeout 300000

COPY frontend ./frontend
COPY src/NzbDrone.Core/Localization/Core ./src/NzbDrone.Core/Localization/Core

RUN yarn build

# ==============================================================================
# Stage 2: Build & Publish .NET 10 Backend (linux-x64 / linux-arm64)
# ==============================================================================
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
ARG TARGETARCH
ARG BRANCH=develop
ARG VERSION=5.0.0.0
WORKDIR /src

COPY global.json ./
COPY src ./src
COPY LICENSE.md ./

RUN if [ -n "$VERSION" ] && [ "$VERSION" != "5.0.0.0" ]; then \
      sed -i "s|<AssemblyVersion>[0-9.*]\+</AssemblyVersion>|<AssemblyVersion>${VERSION}</AssemblyVersion>|g" src/Directory.Build.props src/Controllarr.Common/Controllarr.Common.csproj && \
      sed -i "s|<AssemblyConfiguration>[^<]\+</AssemblyConfiguration>|<AssemblyConfiguration>${BRANCH}</AssemblyConfiguration>|g" src/Directory.Build.props src/Controllarr.Common/Controllarr.Common.csproj; \
    fi

RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet publish src/NzbDrone.Console/Sonarr.Console.csproj \
      --configuration Release \
      --runtime "$RID" \
      --self-contained false \
      -p:PublishSingleFile=false \
      -o /app/bin && \
    rm -f /app/bin/ServiceInstall.* /app/bin/ServiceUninstall.* /app/bin/Sonarr.Windows.* && \
    cp LICENSE.md /app/bin/

# ==============================================================================
# Stage 3: Production Runtime Image
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG BRANCH=develop
ARG VERSION=5.0.0.0

LABEL org.opencontainers.image.title="Controllarr" \
      org.opencontainers.image.description="Automated ROM library manager and acquisition engine for retro and modern game consoles" \
      org.opencontainers.image.url="https://github.com/12thirtyfour/Controllarr" \
      org.opencontainers.image.source="https://github.com/12thirtyfour/Controllarr" \
      org.opencontainers.image.licenses="GPL-3.0-only"

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_EnableDiagnostics=0 \
    COMPlus_EnableDiagnostics=0 \
    PUID=1000 \
    PGID=1000 \
    UMASK=002 \
    TZ=UTC \
    PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/usr/games"

WORKDIR /app

# Install runtime dependencies, archive extractors, and disc compression CLI tools:
# - mame-tools: provides `chdman` for lossless .chd optical disc compression
# - dolphin-emu: provides `dolphin-tool` (when available for target arch) for GameCube/Wii .rvz conversion
# - p7zip-full: provides `7z` / `7za` for .7z and multi-format archive extraction
# - gosu: non-root PUID/PGID privilege drop in entrypoint
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    gosu \
    libicu-dev \
    mame-tools \
    p7zip-full \
    sqlite3 \
    tzdata \
    && (apt-get install -y --no-install-recommends dolphin-emu || true) \
    && rm -rf /var/lib/apt/lists/* \
    && if [ -x /usr/games/chdman ] && [ ! -e /usr/local/bin/chdman ]; then ln -s /usr/games/chdman /usr/local/bin/chdman; fi \
    && if [ -x /usr/games/dolphin-tool ] && [ ! -e /usr/local/bin/dolphin-tool ]; then ln -s /usr/games/dolphin-tool /usr/local/bin/dolphin-tool; fi

COPY --from=backend-build /app/bin /app/bin
COPY --from=frontend-build /src/_output/UI /app/bin/UI
COPY docker/entrypoint.sh /entrypoint.sh

RUN ln -snf /app/bin/UI /app/UI && \
    chmod +x /app/bin/Controllarr /entrypoint.sh && \
    find /app/bin -name "ffprobe" -exec chmod +x {} \; && \
    printf "PackageVersion=%s-%s\nPackageAuthor=Team Controllarr (https://github.com/12thirtyfour/Controllarr)\nUpdateMethod=Docker\nUpdateMethodMessage=Pull the latest Controllarr Docker image to update\nBranch=%s\nReleaseVersion=%s\n" \
      "${VERSION}" "${BRANCH}" "${BRANCH}" "${VERSION}" > /app/package_info && \
    mkdir -p /config /roms /downloads

VOLUME ["/config", "/roms", "/downloads"]
EXPOSE 7050

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -fsS http://localhost:7050/ping || exit 1

ENTRYPOINT ["/entrypoint.sh"]
