# Quick Start Tutorial

This guide takes you through setting up Controllarr, adding your first game console, scanning your existing collection, and downloading or playing a title.

---

## Step 1: Access the Dashboard

1. Start Controllarr.
2. In your web browser, open:
   ```text
   http://localhost:7050
   ```
3. You will be greeted by the Controllarr main dashboard.

---

## Step 2: Configure a Root Folder

Controllarr organizes game files into structured directory hierarchies according to console manufacturers:

1. Go to **Settings** → **Media Management**.
2. Under **Root Folders**, click **Add Root Folder**.
3. Select or enter the path where you store your console ROMs (e.g., `/Volumes/Games/ROMs` or `D:\Games\ROMs`).
4. Controllarr will automatically create hardware-standard platform subdirectories (such as `Nintendo/Super Nintendo Entertainment System/USA/`).

---

## Step 3: Add Your First Game Platform

1. In the sidebar, click **Consoles** (or **Game Platforms**).
2. Click **+ Add Platform**.
3. Select the console you want to manage (e.g., *Super Nintendo Entertainment System*, *PlayStation*, or *Game Boy Advance*).
4. Choose your **Collection Profile**:
   * **1 Game 1 ROM (1G1R)**: Retains only the best regional release and newest revision, eliminating clone clutter.
   * **Completionist / Preservationist**: Tracks and stores every regional release, revision, and alternate edition.
5. Click **Save**.

---

## Step 4: Scan and Ingest Existing ROMs

If you already have game files or disc images on your hard drive:

1. Open your console view in Controllarr.
2. Click **Scan & Ingest**.
3. Point Controllarr to your unorganized folder or archive directory.
4. Controllarr will:
   * Calculate CRC32, MD5, and SHA-1 checksums without unpacking zip/7z archives when possible.
   * Cross-reference hashes against official **No-Intro** or **Redump** catalogs.
   * Move or copy the files into your canonical root folder layout.
   * Fetch cover artwork, developer info, release dates, and descriptions from **IGDB**.

---

## Step 5: Connect an Acquisition Source

Controllarr can automatically acquire missing titles from three distinct sources:

1. Navigate to **Settings** → **Indexers**.
2. Click **+** to add an indexer:
   * **Internet Archive**: Enter your public archive endpoints or S3 keys for instant direct HTTP downloads.
   * **Usenet (Newznab)**: Add your favorite Usenet indexer (such as NZBgeek, DrunkenSlug, or DOGnzb). Controllarr defaults to the **`1000` (Console)** and **`8000` (Other)** categories.
   * **Torznab**: Add your torrent indexer with minimum seed criteria.
3. Configure your download client in **Settings** → **Download Clients** (SABnzbd, NZBGet, qBittorrent, Transmission, etc.).

---

## Step 6: Search & Add Games

1. Click the **Search** icon in the top navigation bar.
2. Type the name of a title (e.g., *Chrono Trigger* or *Metal Gear Solid*).
3. Controllarr displays matching titles across all platforms.
4. Click **+ Add & Search**: Controllarr will auto-provision the platform if not already monitored, dispatch searches across your indexers according to your protocol priority, and notify you when the game is verified and ready.

---

## Step 7: Play or Export!

* **In-Browser Play**: Click the green **Play** button on any supported 8-bit, 16-bit, or handheld title to boot it directly in your browser.
* **Desktop Launcher**: Click **Launch in Emulator** to trigger a native emulator session on your PC or Mac via `controllarr://play/{gameId}`.
* **SD Card Exporter**: Click **Export to SD** to transfer your curated collection formatted specifically for your flash cart, MiSTer, or handheld!
