# Download Clients

Controllarr connects to your favorite Usenet and BitTorrent download clients to manage active downloads, report live progress, and automatically import completed files.

---

## 1. Supported Clients

### Usenet Clients
* **SABnzbd**: Full support for categories, priority queues, and API status monitoring.
* **NZBGet**: Seamless integration with post-processing scripts.

### Torrent Clients
* **qBittorrent**: Fast, reliable integration via WebUI API.
* **Transmission**: Lightweight RPC connection.
* **Deluge**: Connects via Web plugin API.
* **rTorrent / ruTorrent**: Connects via XML-RPC.

---

## 2. Configuration & Category Management

To prevent Controllarr downloads from mingling with movies, music, or television shows, configure a dedicated category or tag in your download client:

1. In Controllarr, navigate to **Settings** → **Download Clients**.
2. Click **+** and choose your client (e.g., *qBittorrent* or *SABnzbd*).
3. Enter your connection details:
   * **Host**: IP address or hostname of your client (e.g. `127.0.0.1` or `192.168.1.100`).
   * **Port**: Client's web UI port (e.g., `8080` for qBittorrent, `8085` for SABnzbd).
   * **Credentials**: Username and password / API Key.
   * **Category**: Set to **`games`** or **`controllarr`**.
4. Click **Test** to ensure Controllarr can communicate with your client, then click **Save**.

---

## 3. Completed Download Handling

Controllarr monitors your download client's queue every 60 seconds:

1. **Queue Progress**: You can view active download speeds, remaining times, and download states in **Activity** → **Queue**.
2. **Automated Ingestion**: As soon as a download finishes:
   * Controllarr detects the completed directory or file.
   * If the download is a `.zip`, `.7z`, or `.rar` archive, Controllarr unpacks and inspects the payload.
   * Calculates CRC32/MD5/SHA-1 checksums and matches against No-Intro or Redump DATs.
   * Moves or hardlinks the verified ROM into your canonical `{Manufacturer}/{Console}/{Region}` root folder.
   * If configured, triggers `chdman` or `dolphin-tool` to compress raw disc images into `.chd` or `.rvz`.
   * Tells the download client to remove the finished task or continue seeding according to your ratio rules.
