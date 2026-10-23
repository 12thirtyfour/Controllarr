# Acquisition & Indexers

Controllarr features an automated 3-way acquisition engine designed to search, score, and grab game releases from multiple distinct distribution networks.

---

## 1. The 3-Way Acquisition Protocol Priority

Unlike media managers limited to Usenet and Torrents, Controllarr natively introduces **Direct HTTP / Public Archives** as a primary distribution tier:

1. **HTTP / Direct Download (Internet Archive)**:
   * Direct high-speed download from preservation repositories (such as the Internet Archive).
   * Supports public S3 API authentication.
   * Completely bypasses P2P ratios and Usenet server subscriptions.
2. **Usenet (Newznab)**:
   * Rapid retention and clean binary grabs using NZB files.
   * Configured specifically for Console and Game categories.
3. **BitTorrent (Torznab)**:
   * Full P2P indexing via Torznab protocol (compatible with Jackett and Prowlarr).

### Configuring Priority
In **Settings** → **Acquisition Priority**, you can drag and drop your preferred protocol order (e.g., `HTTP > Usenet > Torrent`). When a game is searched, Controllarr queries all configured indexers but prioritizes grabs according to your ranking.

---

## 2. Internet Archive (HTTP / S3 Downloader)

Controllarr includes a native streaming downloader optimized for the Internet Archive's S3-compatible API:

### Setup
1. Go to **Settings** → **Indexers** and click **+ Add Indexer** → **Internet Archive**.
2. **Settings**:
   * **Base URL**: Defaults to `https://archive.org`.
   * **Access Key / Secret Key**: (Optional) Enter your IA S3 keys from `archive.org/account/s3.php` to access restricted collections or increase bandwidth limits.
   * **Anonymous Fallback**: If keys are empty or invalid, Controllarr automatically falls back to public anonymous access.
   * **Preferred Archives**: Specify collections to search first (e.g., curated No-Intro or Redump collection identifiers).

---

## 3. Usenet Indexers (Newznab)

Controllarr supports all Newznab-compatible indexers (such as **NZBgeek**, **DrunkenSlug**, **DOGnzb**, **NZBFinder.ws**, and **nzbplanet**).

### Crucial Category Configuration: `1000` & `8000`
Standard media managers search category `5000` (TV) or `2000` (Movies). In Controllarr, search queries automatically target console gaming categories:

* **Category `1000` (Console)**:
  * `1010`: Nintendo DS (NDS)
  * `1020`: Sony PlayStation Portable (PSP)
  * `1030`: Nintendo Wii
  * `1040`: Microsoft Xbox
  * `1050`: Microsoft Xbox 360
  * `1060`: Nintendo WiiWare / Virtual Console
  * `1070`: Xbox 360 DLC
  * `1080`: Sony PlayStation 3 (PS3)
  * `1090`: Other Console
  * `1110`: Nintendo 3DS
  * `1120`: Sony PlayStation Vita (PS Vita)
  * `1130`: Nintendo Wii U
  * `1140`: Microsoft Xbox One
  * `1180`: Sony PlayStation 4 (PS4)
* **Category `8000` (Other / Misc)**:
  * `8010`: Misc / Archive Packs / Full-set collections
  * `8020`: Hashed ROM Sets

> [!NOTE]
> Controllarr's Newznab integration automatically selects these categories by default and prevents TV categories (`5000`) from cluttering your indexer options.

---

## 4. Torrent Indexers (Torznab)

Controllarr connects to any Torznab indexer, including self-hosted aggregators like **Prowlarr** and **Jackett**.

### Setup
1. In Controllarr, go to **Settings** → **Indexers** → **Torznab**.
2. Enter the **URL** and **API Key** provided by your Torznab indexer or Prowlarr.
3. Configure **Minimum Seeders** (defaults to 1) to prevent grabbing stalled or dead torrents.
4. **Reject Blocklisted Hashes**: Automatically blocks known bad torrent info-hashes.
