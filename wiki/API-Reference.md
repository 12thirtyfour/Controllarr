# REST API Reference

Controllarr provides a comprehensive RESTful API adhering to the OpenAPI specification. It exposes dual-versioned endpoints across both **`/api/v3`** and **`/api/v5`** for maximum backward compatibility and forward-looking third-party integration.

---

## 1. Authentication

Every API request requires your Controllarr API key (found in **Settings** → **General** → **Security**).

You can pass your API key in two ways:
* **HTTP Header (Recommended)**:
  ```http
  X-Api-Key: your_api_key_here
  ```
* **Query Parameter**:
  ```http
  GET http://localhost:7050/api/v3/gameplatform?apikey=your_api_key_here
  ```

---

## 2. OpenAPI Specification & Interactive Docs

* **v3 OpenAPI Spec**: `http://localhost:7050/api/v3/openapi.json`
* **v5 OpenAPI Spec**: `http://localhost:7050/api/v5/openapi.json`

---

## 3. Key Gaming Endpoints

### 🎮 Game Platforms (`/api/v3/gameplatform`)
* `GET /api/v3/gameplatform`: List all monitored console platforms, total game counts, and collection strategies.
* `POST /api/v3/gameplatform`: Add a new console to monitor.
* `PUT /api/v3/gameplatform/{id}`: Update platform configuration (e.g. change 1G1R region hierarchy).
* `DELETE /api/v3/gameplatform/{id}`: Remove a console platform.

### 💾 ROM Entries (`/api/v3/romentry`)
* `GET /api/v3/romentry`: Search or paginate through ingested ROM files and disc images.
* `GET /api/v3/romentry/{id}`: Retrieve detailed file metadata, CRC32/MD5/SHA-1 checksums, and No-Intro/Redump match status.
* `DELETE /api/v3/romentry/{id}`: Delete a ROM file from disk and library.

### 🔍 Global Game Search (`/api/v3/gamesearch`)
* `GET /api/v3/gamesearch?term={query}`: Query IGDB metadata across all platforms.
* `POST /api/v3/gamesearch/add`: Smart-add a game directly to your library, auto-provisioning the console platform if necessary.

### 🕹️ Platform BIOS Matrix (`/api/v3/bios`)
* `GET /api/v3/bios`: List all platform BIOS files, expected checksums, and status (Verified, Missing, Mismatched).
* `POST /api/v3/bios/scan`: Trigger an immediate disk rescan of your BIOS directory.

### 📤 SD Card Exporter (`/api/v3/sdcardexport`)
* `GET /api/v3/sdcardexport/profiles`: List available hardware target profiles (MiSTer, Analogue Pocket, OnionOS, EverDrive, etc.).
* `POST /api/v3/sdcardexport/start`: Start an SD card export task.
* `GET /api/v3/sdcardexport/status/{taskId}`: Monitor transfer progress, bytes written, and estimated time remaining.

### 🏆 RetroAchievements (`/api/v3/retroachievements`)
* `GET /api/v3/retroachievements/profile`: Retrieve player profile, score, and rank.
* `GET /api/v3/retroachievements/game/{gameId}`: Retrieve unlocked badges and progression stats for a specific title.

### 🚀 Game Launcher (`/api/v3/gamelauncher`)
* `POST /api/v3/gamelauncher/play/{romId}`: Launch a title via web Libretro core or trigger the `controllarr://` desktop URI handler.

---

## 4. Code Examples

### cURL: List Monitored Consoles
```bash
curl -X GET "http://localhost:7050/api/v3/gameplatform" \
     -H "X-Api-Key: your_api_key_here" \
     -H "Accept: application/json"
```

### Python: Check BIOS Verification Status
```python
import requests

API_KEY = "your_api_key_here"
BASE_URL = "http://localhost:7050/api/v3"

headers = {
    "X-Api-Key": API_KEY,
    "Accept": "application/json"
}

response = requests.get(f"{BASE_URL}/bios", headers=headers)
bios_entries = response.json()

for entry in bios_entries:
    status_icon = "🟢" if entry["isVerified"] else "🔴"
    print(f"{status_icon} [{entry['platformName']}] {entry['filename']} - Status: {entry['status']}")
```
