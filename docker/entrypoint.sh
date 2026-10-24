#!/usr/bin/env bash
set -euo pipefail

PUID="${PUID:-1000}"
PGID="${PGID:-1000}"
UMASK="${UMASK:-002}"
TZ="${TZ:-UTC}"

if [ -f "/usr/share/zoneinfo/${TZ}" ]; then
    ln -snf "/usr/share/zoneinfo/${TZ}" /etc/localtime
    echo "${TZ}" > /etc/timezone
fi

umask "${UMASK}"

if [ "$(id -u)" = "0" ]; then
    if ! getent group controllarr >/dev/null 2>&1; then
        groupadd -o -g "${PGID}" controllarr
    else
        groupmod -o -g "${PGID}" controllarr
    fi

    if ! id -u controllarr >/dev/null 2>&1; then
        useradd -o -u "${PUID}" -g "${PGID}" -d /config -s /bin/bash -M controllarr
    else
        usermod -o -u "${PUID}" -g "${PGID}" controllarr
    fi

    mkdir -p /config /roms /downloads
    chown -R "${PUID}:${PGID}" /config /app

    exec gosu "${PUID}:${PGID}" /app/bin/Controllarr -nobrowser -data=/config "$@"
fi

exec /app/bin/Controllarr -nobrowser -data=/config "$@"
