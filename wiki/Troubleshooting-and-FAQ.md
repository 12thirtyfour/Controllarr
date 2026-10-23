# Troubleshooting & FAQ

This guide covers common questions, performance optimizations, and solutions to frequently encountered issues.

---

## 1. Frequently Encountered Issues

### Q: When I navigate to `http://localhost:7050`, I see a `404 Not Found` JSON error.
**Cause**: The .NET backend is running, but it cannot find the compiled React frontend static files.  
**Solution**:
1. Ensure you have built the React frontend:
   ```bash
   yarn install
   yarn build
   ```
2. Make sure your local repository is updated with the latest changes (`git pull origin develop`), which includes the automatic `../UI` path resolution fix in `PathExtensions.cs`.
3. Restart Controllarr (`./_output/net10.0/Sonarr --nobrowser`).

---

### Q: Port 7050 is already in use.
**Cause**: Another application or a previously backgrounded instance of Controllarr is running on port `7050`.  
**Solution**:
* **Find and kill the process**:
  * **macOS / Linux**:
    ```bash
    lsof -i :7050
    kill -9 <PID>
    ```
  * **Windows**:
    ```powershell
    Get-Process -Id (Get-NetTCPConnection -LocalPort 7050).OwningProcess | Stop-Process -Force
    ```
* **Change the port**: Edit `<Port>7050</Port>` in `config.xml` to a different port (e.g. `7055`).

---

### Q: My Usenet indexer returns 0 results for game searches.
**Cause**: The indexer may be querying TV categories (`5000`) or the indexer does not support gaming categories.  
**Solution**:
1. In Controllarr, go to **Settings** → **Indexers** and open your Newznab indexer.
2. Verify that **Categories** includes **`1000` (Console)** and **`8000` (Other)**.
3. Test your indexer directly in Controllarr to ensure its API supports `t=search&cat=1000`.

---

### Q: My BIOS file shows as "Missing" even though I copied it into the BIOS folder.
**Cause**: 
* **Incorrect Filename or Casing**: On case-sensitive filesystems (Linux and standard macOS), `scph5501.bin` is completely different from `SCPH5501.BIN`.
* **Mismatched Checksum**: The file may be a bad or modified dump that does not match the official MD5/SHA-1 catalog.  
**Solution**:
1. Check the [Platform BIOS Matrix](Platform-BIOS-Matrix.md) for the exact required filename and MD5 checksum.
2. In Controllarr, click **Scan BIOS Files** to check if Controllarr detects the file under a mismatched name and click **Auto-Rename**.

---

### Q: SQLite database is locked (`database is locked` error).
**Cause**: Multiple processes accessing the SQLite database file simultaneously, or running SQLite over an SMB/NFS network share without file-locking support.  
**Solution**:
* Store the Controllarr AppData folder on a local SSD/NVMe drive rather than a remote network share.
* If you require storing data on a remote server or NAS, configure **PostgreSQL** in `config.xml`.

---

## 2. Where to Find Application Logs

Controllarr records detailed application logs in two formats:

1. **Log Files (Text)**:
   * **macOS**: `~/.config/Controllarr/logs/controllarr.txt`
   * **Linux**: `~/.config/Controllarr/logs/controllarr.txt` (or `/var/lib/controllarr/logs/`)
   * **Windows**: `C:\ProgramData\Controllarr\logs\controllarr.txt`
2. **Log Database (SQLite)**:
   * Viewable directly inside the web UI at **System** → **Logs**.

### Enabling Trace Logging for Deep Troubleshooting
If you are troubleshooting a complex ingestion or acquisition issue:
1. Navigate to **Settings** → **General** → **Logging**.
2. Change **Log Level** from `Info` to **`Trace`**.
3. Re-run the failing action. The log will output exact HTTP queries, XML/JSON payloads, and full stack traces.
