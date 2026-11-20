#!/usr/bin/env bash
set -euo pipefail

PUID="${PUID:-1000}"
PGID="${PGID:-1000}"
UMASK="${UMASK:-002}"
TZ="${TZ:-UTC}"

export HOME="/config"
umask "${UMASK}" || true

if [ "$(id -u)" = "0" ]; then
    if [ -f "/usr/share/zoneinfo/${TZ}" ]; then
        ln -snf "/usr/share/zoneinfo/${TZ}" /etc/localtime 2>/dev/null || true
        echo "${TZ}" > /etc/timezone 2>/dev/null || true
    fi

    mkdir -p /config /roms /downloads 2>/dev/null || true
    chown -R "${PUID}:${PGID}" /config 2>/dev/null || true

    if ! gosu "${PUID}:${PGID}" test -w /config; then
        CONFIG_UID="$(stat -c '%u' /config 2>/dev/null || echo "${PUID}")"
        CONFIG_GID="$(stat -c '%g' /config 2>/dev/null || echo "${PGID}")"

        if [ "${CONFIG_UID}:${CONFIG_GID}" != "${PUID}:${PGID}" ] && gosu "${CONFIG_UID}:${CONFIG_GID}" test -w /config; then
            echo "[entrypoint] WARNING: /config is not writable by ${PUID}:${PGID} (for example, restricted ACLs on TrueNAS ZFS). Falling back to /config owner ${CONFIG_UID}:${CONFIG_GID}."
            PUID="${CONFIG_UID}"
            PGID="${CONFIG_GID}"
        else
            echo "[entrypoint] WARNING: /config does not appear writable by ${PUID}:${PGID}. Check dataset/volume permissions."
        fi
    fi

    chown -R "${PUID}:${PGID}" /app 2>/dev/null || true
    chown "${PUID}:${PGID}" /roms /downloads 2>/dev/null || true

    if ! getent group controllarr >/dev/null 2>&1; then
        groupadd -o -g "${PGID}" controllarr 2>/dev/null || true
    else
        groupmod -o -g "${PGID}" controllarr 2>/dev/null || true
    fi

    if ! id -u controllarr >/dev/null 2>&1; then
        useradd -o -u "${PUID}" -g "${PGID}" -d /config -s /bin/bash -M controllarr 2>/dev/null || true
    else
        usermod -o -u "${PUID}" -g "${PGID}" controllarr 2>/dev/null || true
    fi

    exec gosu "${PUID}:${PGID}" /app/bin/Controllarr -nobrowser -data=/config "$@"
fi

mkdir -p /config 2>/dev/null || true
exec /app/bin/Controllarr -nobrowser -data=/config "$@"
