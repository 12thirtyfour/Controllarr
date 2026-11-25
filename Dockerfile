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

RUN yarn build && chmod -R a+rX /src/_output/UI

# ==============================================================================
# Stage 2: Build & Publish .NET 10 Backend (linux-x64 / linux-arm64)
# ==============================================================================
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
ARG TARGETARCH
ARG BRANCH=develop
ARG VERSION=5.0.0.0
WORKDIR /src

COPY global.json .editorconfig ./
COPY src ./src
COPY Logo ./Logo
COPY LICENSE.md ./

RUN if [ -n "$VERSION" ] && [ "$VERSION" != "5.0.0.0" ]; then \
      sed -i "s|<AssemblyVersion>[0-9.*]\+</AssemblyVersion>|<AssemblyVersion>${VERSION}</AssemblyVersion>|g" src/Directory.Build.props src/Controllarr.Common/Controllarr.Common.csproj && \
      sed -i "s|<AssemblyConfiguration>[^<]\+</AssemblyConfiguration>|<AssemblyConfiguration>${BRANCH}</AssemblyConfiguration>|g" src/Directory.Build.props src/Controllarr.Common/Controllarr.Common.csproj; \
    fi

RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet publish src/NzbDrone.Console/Controllarr.Console.csproj \
      --configuration Release \
      --framework net10.0 \
      --runtime "$RID" \
      --self-contained false \
      -p:PublishSingleFile=false \
      -o /app/bin && \
    rm -f /app/bin/ServiceInstall.* /app/bin/ServiceUninstall.* /app/bin/Controllarr.Windows.* && \
    cp LICENSE.md /app/bin/ && \
    chmod +x /app/bin/Controllarr && \
    find /app/bin -name "ffprobe" -exec chmod +x {} \; && \
    chmod -R a+rX /app/bin

# ==============================================================================
# Stage 3: Extract Minimal Headless Disc Tools (chdman / dolphin-tool)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS tools-extract

ENV DEBIAN_FRONTEND=noninteractive

RUN mkdir -p /opt/disc-tools/bin /opt/disc-tools/lib /opt/disc-tools/wrappers && \
    find /lib /usr/lib \( -type f -o -type l \) -name '*.so*' -exec basename {} \; | sort -u > /tmp/base_libs.txt && \
    apt-get update && \
    apt-get install -y --no-install-recommends mame-tools && \
    (apt-get install -y --no-install-recommends dolphin-emu || true) && \
    for tool in chdman dolphin-tool; do \
      src="/usr/games/${tool}"; \
      if [ -x "$src" ]; then \
        cp -L "$src" "/opt/disc-tools/bin/${tool}" && \
        chmod 755 "/opt/disc-tools/bin/${tool}"; \
        ldd "$src" | awk '/=> \// {print $3}' | while IFS= read -r lib_path; do \
          soname="$(basename "$lib_path")"; \
          case "$soname" in \
            libc.so*|libm.so*|libdl.so*|librt.so*|libpthread.so*|libresolv.so*|libgcc_s.so*|libstdc++.so*|ld-linux*) continue ;; \
          esac; \
          if ! grep -Fxq "$soname" /tmp/base_libs.txt && [ ! -e "/opt/disc-tools/lib/${soname}" ]; then \
            cp -L "$lib_path" "/opt/disc-tools/lib/${soname}"; \
          fi; \
        done; \
        printf '#!/bin/sh\nexec env LD_LIBRARY_PATH="/opt/disc-tools/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}" /opt/disc-tools/bin/%s "$@"\n' "$tool" > "/opt/disc-tools/wrappers/${tool}" && \
        chmod 755 "/opt/disc-tools/wrappers/${tool}"; \
      fi; \
    done && \
    for bin in /opt/disc-tools/bin/*; do \
      [ -e "$bin" ] || continue; \
      if LD_LIBRARY_PATH=/opt/disc-tools/lib ldd "$bin" | grep -q "not found"; then \
        echo "Missing shared libraries for $bin:"; \
        LD_LIBRARY_PATH=/opt/disc-tools/lib ldd "$bin"; \
        exit 1; \
      fi; \
    done && \
    rm -rf /var/lib/apt/lists/*

# ==============================================================================
# Stage 4: Production Runtime Image
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
    ASPNETCORE_HTTP_PORTS="" \
    DOTNET_EnableDiagnostics=0 \
    COMPlus_EnableDiagnostics=0 \
    HOME=/config \
    PUID=1000 \
    PGID=1000 \
    UMASK=002 \
    TZ=UTC \
    PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

WORKDIR /app

# Install minimal runtime dependencies:
# - curl: container healthcheck
# - gosu: non-root PUID/PGID privilege drop in entrypoint
# - libsqlite3-0: native SQLite library resolved by Controllarr
# - p7zip-full: `7z` / `7za` for .7z and multi-format archive extraction
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    gosu \
    libsqlite3-0 \
    p7zip-full \
    && rm -rf /var/lib/apt/lists/*

COPY --from=tools-extract /opt/disc-tools/bin /opt/disc-tools/bin
COPY --from=tools-extract /opt/disc-tools/lib /opt/disc-tools/lib
COPY --from=tools-extract /opt/disc-tools/wrappers/ /usr/local/bin/
COPY --from=backend-build /app/bin /app/bin
COPY --from=frontend-build /src/_output/UI /app/bin/UI
COPY --chmod=755 docker/entrypoint.sh /entrypoint.sh

RUN ln -snf /app/bin/UI /app/UI && \
    for bin in /opt/disc-tools/bin/*; do \
      [ -e "$bin" ] || continue; \
      if LD_LIBRARY_PATH=/opt/disc-tools/lib ldd "$bin" | grep -q "not found"; then \
        echo "Missing shared libraries in runtime stage for $bin:"; \
        LD_LIBRARY_PATH=/opt/disc-tools/lib ldd "$bin"; \
        exit 1; \
      fi; \
    done && \
    printf "PackageVersion=%s-%s\nPackageAuthor=Team Controllarr (https://github.com/12thirtyfour/Controllarr)\nUpdateMethod=Docker\nUpdateMethodMessage=Pull the latest Controllarr Docker image to update\nBranch=%s\nReleaseVersion=%s\n" \
      "${VERSION}" "${BRANCH}" "${BRANCH}" "${VERSION}" > /app/package_info && \
    mkdir -p /config /roms /downloads && \
    chmod 777 /config /roms /downloads

VOLUME ["/config", "/roms", "/downloads"]
EXPOSE 7050

HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --start-interval=2s --retries=3 \
    CMD curl -fsSL http://127.0.0.1:7050/ping || exit 1

ENTRYPOINT ["/entrypoint.sh"]
