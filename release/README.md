# Geo Gaming Hub v0.1.0

**English** · [한국어](README.ko.md)

One app that installs stereo 3D into PC games. Pick the game's exe and it chooses the matching 3D method, copies it into
the game folder and sets it up. Output is SR weaving on SR displays (Acer SpatialLabs / Leia SR), otherwise
side-by-side / top-and-bottom.

## 3D methods

| Game | Method | Bits |
|---|---|---|
| Unity (DirectX 11) | geo-11 + Unity Universal Fix | 32 / 64 |
| DirectX 11 (generic) | geo-11 (Preferred) | 32 / 64 |
| Unreal Engine 4 | geo-11 + UE4 Universal Fix 2 (run with `-dx11`) | 64 |
| DirectX 9 | dgVoodoo2 (D3D9 → D3D11) + geo-11 | 32 / 64 |
| OpenGL | OGL3D (`opengl32.dll`) | 32 / 64 |
| Vulkan | VK3D (`vulkan-1.dll`) | 64 |
| DirectX 12 | DX12_3D (`d3d12.dll`) | 64 |
| Games with a TriDef 3D profile | an installed TriDef 3D + Tridef3D_Play (Steam games) | 32 (DX9) / 64 (DX11) |

geo-11 games are woven by SRWeave (`dxgi.dll`); the own wrappers (OGL3D / VK3D / DX12_3D) weave by themselves.
A DirectX 9 / 11 game can also be run as Vulkan through DXVK and use VK3D (experimental, 64-bit).

### TriDef 3D

**TriDef 3D must be installed.** TriDef 3D (DDD, discontinued in 2017) has to be on the PC; this app contains none of its
files (they may not be redistributed). Instead of TriDef 3D Ignition's window, the app registers the game with TriDef (the
TriDef profile is picked by the game's name), and **[Run]** starts it through `Tridef3D_Play`, which injects TriDef's DLLs
(an administrator prompt appears). Nothing goes into the game folder. Steam games only; started directly from Steam the
game is not in 3D, so use the app's [Run]. Depth is adjusted in game with TriDef's hotkeys. A game with a TriDef profile
gets this method automatically when its exe is chosen.

## Contents

| Path | Contents |
|---|---|
| `GeoGamingHub.exe` | the app |
| `data\game_candidates.json` | per-game recommended method for the Candidates tab |
| `data\tridef_profiles.txt` | TriDef 3D profile names (to recognise games on PCs without TriDef) |
| `Packages\Geo-11_Unity` | geo-11 0.6.109 + Unity Universal Fix + SRWeave |
| `Packages\Geo-11_DX11` | geo-11 0.6.109 (Preferred) + SRWeave |
| `Packages\Geo-11_UE4` | UE4 Universal Fix 2 + geo-11 0.6.109 + SRWeave |
| `Packages\Geo-11_DX9` | dgVoodoo2 2.79.1 / 2.86.5 / 2.87.3 |
| `Packages\OGL3D` | OGL3D 1.0.0 (x64, x86) |
| `Packages\VK3D` | VK3D 0.1.0 (x64) + Quake II RTX fix |
| `Packages\DX12_3D` | DX12_3D 0.1.0 (x64) + DXC (Windows SDK) |
| `Packages\DXVK` | DXVK 3.1.1 (x64) |
| `Packages\TriDef` | Tridef3D_Play (tridef3d-win11-injector) + Inject32 + SRWeave DX9/DX11 - no TriDef files |

Keep `Packages` next to `GeoGamingHub.exe`. Unzip everything first; don't run it from inside the zip.

## Requirements

- Windows 10 / 11, .NET Framework 4.x (built into Windows)
- SR weaving: an SR display and its SR platform runtime (Acer: SpatialLabs Experience Center). With SR weaving off, the
  output is SBS / TAB for other 3D displays and no SR runtime is needed.
- Run games at the panel's native resolution, full screen or borderless.

## How to use

1. Run `GeoGamingHub.exe`, then on **Games** click **[Add game (start setup)]** and pick the game's exe (or drop it on
   the window). You can also start from an installed game on the **Candidates** tab.
2. Go through the four steps **3D Method** → **Install** → **Display** → **Stereo values** with [Next], then click
   [Finish]. The 3D method is detected from the exe; change it if it is wrong. Without an SR display, pick **SBS only**
   (3D TV, non-SR monitor) on the Install step.
3. Start the game. Adjust and save the values in-game with the hotkeys.
4. Uninstall: select the game on **Games**, then **[Uninstall 3D fix]** (deletes every file and restores the kept
   originals) or **[Remove SR weaving only]** (3D stays as SBS).

A different DLL of the same name already in the game folder is kept as `.geo11sr_bak` and restored on uninstall.

## Hotkeys

geo-11 games: see and change them on the **Hotkeys** tab. The own wrappers read `[Keys]` in their ini; defaults:

| Key | Function |
|---|---|
| Ctrl+F3 / Ctrl+F4 | Separation − / + |
| Ctrl+F5 / Ctrl+F6 | Convergence − / + |
| Ctrl+F7 | save the current values |
| Ctrl+F1 | show values |
| Ctrl+F2 | auto convergence (OGL3D, VK3D) |
| Ctrl+T | 3D on / off |
| Ctrl+Alt+W | SR weaving ↔ SBS |

## Anti-cheat

The app refuses to install into a game folder with anti-cheat (EasyAntiCheat, BattlEye, GameGuard, ...): DLLs in the game
folder can get the account banned. For example, Helldivers 2 (GameGuard) cannot be set up with the app. Installing by
hand is at your own account risk.

## Known limits

- **DX12_3D**: DLSS does not reach the right eye (grey image): use the game's render scale or FSR / XeSS. No auto convergence yet.
- The **DXVK route** is experimental; games that work with geo-11 are better on geo-11.
- Games that block third-party DLLs load none of these methods.

## Community reports

**[Report 3D...]** opens the GitHub report form (game, PCGamingWiki link, 3D method and setup filled in), and the
**Compatibility list** link opens the community list built from the reports (a GitHub account is needed to report).

## Troubleshooting

Send the app's **Log** tab together with the wrapper log in the game folder (`OGL3D.log`, `VK3D.log`, `DX12_3D.log`;
geo-11: `d3d11_log.txt`). Each wrapper's README is inside `Packages\<wrapper>`.

## Licenses

- OGL3D, DX12_3D: MIT (`Packages\OGL3D\LICENSE`).
- DXVK: zlib/libpng (`Packages\DXVK\LICENSE`).
- Tridef3D_Play / Inject32 / SRWeave (tridef3d-win11-injector): MIT (`Packages\TriDef\LICENSE`). TriDef 3D itself is not
  included and is DDD's software.
- geo-11 / 3DMigoto, Unity Universal Fix, UE4 Universal Fix 2, dgVoodoo2: their authors' terms (in each package folder).
- DXC (`dxcompiler.dll`, `dxil.dll`): Microsoft, Windows SDK redistributables.
