# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && corepack enable \
    && rm -rf /var/lib/apt/lists/*

COPY . .

RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet publish src/NzbDrone.Console/Sonarr.Console.csproj \
      --configuration Release \
      --runtime "$RID" \
      --self-contained false \
      -o /app/bin

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    mame-tools \
    p7zip-full \
    sqlite3 \
    libicu-dev \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/bin /app/bin

VOLUME ["/config", "/roms", "/downloads"]
EXPOSE 7050

ENTRYPOINT ["/app/bin/Controllarr", "-nobrowser", "-data=/config"]
