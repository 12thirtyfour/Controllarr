# In-Browser Play, Desktop Launchers & RetroAchievements

Controllarr allows you to enjoy your game library immediately without leaving the web browser or having to manually configure emulator file paths.

---

## 1. Instant In-Browser Play (WebAssembly Player)

For lightweight 8-bit, 16-bit, and handheld systems:
* Click the green **Play** button on any game card or detail view in Controllarr.
* An interactive modal opens with an embedded **WebAssembly Libretro Core** (such as FCEUmm for NES, Snes9x for SNES, or Genesis Plus GX for Sega).
* **Controls**:
  * Supports USB/Bluetooth gamepads via the browser Gamepad API with automatic XInput/DirectInput button mapping.
  * Customizable keyboard bindings.
  * Real-time audio rendering and fullscreen mode.
* **Save States**: Saves game states directly to your browser's IndexedDB with optional automatic syncing back to your Controllarr server.

---

## 2. Desktop Launcher Protocol (`controllarr://`)

For resource-intensive or modern optical disc systems (PlayStation 2, GameCube, Wii, Switch):
* Clicking **Launch in Emulator** triggers Controllarr's desktop protocol handler:
  ```text
  controllarr://play/{gameId}
  ```
* Controllarr tells your operating system to launch your local native emulator and passes the exact verified ROM or disc image path:
  * **RetroArch**: Runs the appropriate Libretro core fullscreen.
  * **DuckStation**: Boots PlayStation 1 `.chd` images.
  * **PCSX2**: Boots PlayStation 2 `.iso`/`.chd` images.
  * **Dolphin**: Boots GameCube and Wii `.rvz` dumps.

### Registering the Custom URI Handler
* On Windows, running `Sonarr.exe` registers `controllarr://` in the Windows Registry automatically.
* On macOS and Linux, the Controllarr application package includes the URL handler definition in `Info.plist` (macOS) and `controllarr.desktop` (Linux `xdg-open`).

---

## 3. RetroAchievements Integration

Controllarr features direct integration with **[RetroAchievements.org](https://retroachievements.org/)**, the global community dedicated to adding achievement sets to retro games.

### Setting Up Your Account
1. Create a free account at [RetroAchievements.org](https://retroachievements.org/).
2. In Controllarr, navigate to **Settings** → **RetroAchievements**.
3. Enter your **Username** and your **Web API Key** (found in your RetroAchievements user settings under "Keys").
4. Click **Test**: Controllarr validates the credentials against the RetroAchievements API and confirms the connection.

### Features in Controllarr
* **Badge Showcase Modal**: Open any game detail screen to see your unlocked achievements, achievement rarity (softcore vs. hardcore), and total RetroPoints earned.
* **Game Mastery Tracking**: View mastery badges and completion percentages directly in your platform library views.
* **Global Leaderboards**: Inspect leaderboard scores for supported games directly inside the Controllarr UI.
