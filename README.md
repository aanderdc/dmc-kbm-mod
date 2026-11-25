# DMC KBM Mod (DMC4 / DMC4SE & DMC HD Collection)

> 🌐 **Language:** **English** | [Español](README.es.md)

Lightweight, portable **Keyboard & Mouse (KBM)** mod for **Devil May Cry 4**, **Devil May Cry 4 Special Edition**, and **Devil May Cry HD Collection (DMC1, DMC2, and DMC3)** on Windows.

Runs cleanly in the background without touching or overwriting any game files, making it **100% compatible** with `dinput8.dll` mods such as **DDMK**, **DMC3 Crimson**, **dmc4_hook**, and **LauncherFix**.

---

## Key Features

- **Automatic game detection** (only intercepts keyboard/mouse while a supported game window is focused):
  - `DevilMayCry4SpecialEdition.exe` (DMC4 SE)
  - `DevilMayCry4_DX9.exe` / `DevilMayCry4_DX10.exe` (DMC4 2008)
  - `dmc1.exe`, `dmc2.exe`, `dmc3.exe` (DMC HD Collection — leaves `dmcLauncher.exe` untouched so mouse clicks work normally in the launcher)
- **Instant toggle between 2 control presets** (from the app UI, system tray, or in-game with **`F2`**):
  - **DMC5 Style**: `LMB` Sword · `RMB` Style/Buster · `Mouse Side 4 / MMB` Gun
  - **Classic Style**: `LMB` Sword · `RMB` Gun · `Mouse Side 4 / MMB` Style/Buster
- **DirectInput-ready hardware scan codes**: injects proper `KEYEVENTF_SCANCODE` + `EXTENDEDKEY` inputs so MT Framework (DMC4/SE) and DMC HD Collection respond reliably, including sustained holds (Charge Shot, Exceed, Lock-On, Royal Guard).
- **Raw Input Mouse Camera (`WM_INPUT`)**: independent X/Y sensitivity, deadzone, release smoothing, axis inversion, horizontal-only mode for DMC1/2/3, and optional cursor confinement (`ClipCursor`).
- **Bilingual UI (`EN ⇄ ES`)**: auto-detects Windows language and lets you switch between English and Spanish live with one click.
- **Built-in Live Tester**: test mouse clicks, wheel, keyboard remaps, camera radar, and the DMC5 ⇄ Classic switch right inside the app without even launching a game.
- **Zero external dependencies**: portable ~80 KB executable (`DMC_KBM_Mod.exe`), saving all settings to `dmc_kbm_config.ini`.

---

## Default Controls

Movement (`W, A, S, D`) and Dante's Styles (`1, 2, 3, 4` in DMC4 / DDMK) pass through natively.

| Action | DMC5 Style | Classic Style | Sent Key (DMC4 / SE) | Sent Key (DMC1/2/3 HDC) |
| :--- | :--- | :--- | :--- | :--- |
| **Sword / Melee Attack** | `Left Click (LMB)` | `Left Click (LMB)` | `I` | `I` |
| **Style / Devil Bringer / Action** | `Right Click (RMB)` | `Mouse Side 4` / `MMB` | `L` | `L` |
| **Gun / Shoot** | `Mouse Side 4` / `MMB` | `Right Click (RMB)` | `J` | `J` |
| **Jump** | `Space` | `Space` | `K` | `K` |
| **Lock-On** | `Left Shift` | `Left Shift` | `Space` | `Space` |
| **Devil Trigger** | `F` | `F` | `N` | `F` |
| **Switch Gun / Exceed** | `Q` / `Wheel Up` | `Q` / `Wheel Up` | `Q` | `Q` |
| **Switch Melee Weapon** | `E` / `Wheel Down` | `E` / `Wheel Down` | `E` | `E` |
| **Center Camera** | `Mouse Side 5` / `R` | `Mouse Side 5` / `R` | `P` | `C` |
| **Switch Target** | `Tab` / `C` | `Tab` / `C` | `O` | `O` |
| **Taunt** | `V` | `V` | `M` | `M` |
| **Move Camera** | `Mouse` | `Mouse` | `Arrow Keys` | `Arrow Keys` (Horizontal-only in DMC3 by default) |

> Every binding in both presets and every game's target keys can be freely customized in the UI or via `dmc_kbm_config.ini`.

---

## In-Game Hotkeys

- **`F1`**: Enable / Pause the mod (handy for menus or free mouse usage).
- **`F2`**: Switch on the fly between **DMC5 Style** and **Classic Style** (shows a non-focus-stealing HUD toast in the top-right corner).
- **`F3`**: Toggle mouse camera on/off (keeps mouse button bindings active).

---

## Usage

1. Download **`DMC_KBM_Mod.exe`** from [Releases](https://github.com/aanderdc/dmc-kbm-mod/releases) and run it (no installation needed).
2. Pick **DMC5 Style** or **Classic Style** at the top (or switch anytime in-game with `F2`).
3. *(Optional)* Open the **Live Tester** tab and click **Start Local Test Mode** to verify clicks, scroll wheel, keys, and mouse camera behavior immediately.
4. Launch DMC4 / DMC4SE or DMC HD Collection with default keyboard bindings while `DMC_KBM_Mod.exe` runs in the background (you can minimize it to the system tray).

---

## Troubleshooting & Building from Source

- **Game doesn't respond to mod inputs:** If Steam or the game runs as Administrator, Windows blocks input from non-elevated apps (UIPI). Click the **"Run as Admin"** button in the top bar of `DMC_KBM_Mod.exe`.
- **Custom in-game bindings:** If you already rebound keys inside a game's own options menu, update the target keys under the **Game Profiles** tab.
- **Build from source:** Run `powershell -ExecutionPolicy Bypass -File build.ps1` (uses the built-in Windows `.NET Framework 4.x` C# compiler — Visual Studio is not required).
- **Run test suite:** Run `powershell -ExecutionPolicy Bypass -File tests\test_suite.ps1`.
