# Installation Guide

Controllarr is distributed as a self-contained cross-platform application powered by the .NET 10 runtime and a modern React/Vite single-page application.

---

## System Requirements

* **Operating System**:
  * **macOS**: macOS 12 (Monterey) or higher (Native Apple Silicon `arm64` and Intel `x64`).
  * **Linux**: glibc 2.27+ (`linux-x64`, `linux-arm64`, `linux-arm`) or musl-based (`linux-musl-x64`, `linux-musl-arm64`).
  * **Windows**: Windows 10 (version 1809+) or Windows 11; Windows Server 2019+.
  * **FreeBSD**: FreeBSD 13+.
* **Memory**: 512 MB minimum, 2 GB+ recommended for scanning multi-gigabyte disc archives.
* **Storage**: 250 MB for the Controllarr application binary, plus your ROM and disc image storage.
* **Network**: Default application port **`7050`** (configurable in `config.xml`).

---

## 1. Installation on macOS

Controllarr provides native packages for both Apple Silicon (M1/M2/M3/M4) and Intel Macs:

### Option A: Binary Archive
1. Download the latest `Controllarr.osx-arm64.tar.gz` (or `Controllarr.osx-x64.tar.gz` for Intel) from the [Releases](https://github.com/12thirtyfour/Controllarr/releases) page.
2. Extract the archive into your preferred application location:
   ```bash
   tar -xvf Controllarr.osx-arm64.tar.gz -C /Applications/
   cd /Applications/Controllarr
   ```
3. Run Controllarr:
   ```bash
   ./Sonarr --nobrowser
   ```
4. Access the web interface at **`http://localhost:7050`**.

### Option B: Building from Source
```bash
# 1. Install prerequisites (Node.js, Yarn, and .NET 10 SDK)
brew install dotnet-sdk node yarn

# 2. Clone repository
git clone https://github.com/12thirtyfour/Controllarr.git
cd Controllarr

# 3. Build React frontend
yarn install
yarn build

# 4. Build .NET backend
dotnet build src/Sonarr.sln --configuration Release

# 5. Launch
./_output/net10.0/Sonarr --nobrowser
```

---

## 2. Installation on Linux

### Option A: Standalone Service
1. Download the appropriate tarball (e.g., `Controllarr.linux-x64.tar.gz` or `Controllarr.linux-arm64.tar.gz`).
2. Extract to `/opt/Controllarr`:
   ```bash
   sudo mkdir -p /opt/Controllarr
   sudo tar -xvf Controllarr.linux-x64.tar.gz -C /opt/Controllarr/
   sudo chown -R $USER:$USER /opt/Controllarr
   ```
3. (Optional) Create a `systemd` service for background daemon execution:
   ```ini
   # /etc/systemd/system/controllarr.service
   [Unit]
   Description=Controllarr ROM Library Manager
   After=network.target

   [Service]
   Type=simple
   User=controllarr
   Group=controllarr
   WorkingDirectory=/opt/Controllarr
   ExecStart=/opt/Controllarr/Sonarr -nobrowser -data=/var/lib/controllarr/
   Restart=on-failure
   RestartSec=5

   [Install]
   WantedBy=multi-user.target
   ```
4. Enable and start the service:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now controllarr
   ```

---

## 3. Installation on Windows

### Option A: Standalone Executable
1. Download `Controllarr.win-x64.zip` from GitHub Releases.
2. Extract to `C:\ProgramData\Controllarr` or your preferred location.
3. Run `Sonarr.exe --nobrowser`.
4. Open your browser and navigate to `http://localhost:7050`.

### Option B: Windows Service
To run Controllarr as a silent background Windows Service:
```powershell
# From an administrative PowerShell prompt:
.\ServiceInstall.exe
Start-Service Controllarr
```

---

## 4. Database Selection: SQLite vs. PostgreSQL

Controllarr features a dual-database architecture:

| Database | Best For | Configuration |
|---|---|---|
| **SQLite (Default)** | Single-user setups, local drives, standard libraries | Zero-config. Automatically creates `sonarr.db` in AppData. |
| **PostgreSQL (v16–18)** | Massive libraries (10,000+ titles), remote NAS, network-attached drives | High concurrency and rapid indexing. |

### Configuring PostgreSQL
To use PostgreSQL, edit `config.xml` (located in your AppData directory) before starting Controllarr:
```xml
<Config>
  <Port>7050</Port>
  <PostgresUser>controllarr_user</PostgresUser>
  <PostgresPassword>YourSecurePassword</PostgresPassword>
  <PostgresHost>localhost</PostgresHost>
  <PostgresPort>5432</PostgresPort>
  <PostgresMainDb>controllarr_main</PostgresMainDb>
  <PostgresLogDb>controllarr_log</PostgresLogDb>
</Config>
```
Controllarr will automatically apply all schema migrations (Migrations 237–241+) on first launch.

---

## 5. Reverse Proxy Configuration

If hosting Controllarr behind a reverse proxy (e.g., `https://games.yourdomain.com`), configure URL rewriting and WebSocket support for SignalR:

### Nginx Example
```nginx
server {
    listen 80;
    server_name games.yourdomain.com;

    location / {
        proxy_pass http://127.0.0.1:7050;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-Host $host;

        # WebSocket support for SignalR events
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_read_timeout 86400;
    }
}
```

### Caddy Example
```caddy
games.yourdomain.com {
    reverse_proxy 127.0.0.1:7050
}
```
