# Geo Gaming Hub v0.1.7

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
| Games with a TriDef 3D profile | an installed TriDef 3D + Tridef3D_Play (Steam games and others) | 32 (DX9) / 64 (DX11) |

geo-11 games are woven by SRWeave (`dxgi.dll`); the own wrappers (OGL3D / VK3D / DX12_3D) weave by themselves.
A DirectX 9 / 11 game can also be run as Vulkan through DXVK and use VK3D (experimental, 64-bit).
OGL3D and DX12_3D games can run below the panel's resolution: tick **Upscale** in the Display step. The game then
renders at 1920×1080 (on a 4K panel at 200 % scaling: readable menus and text) and the wrapper weaves it at the
panel's resolution in its own window over the game window; choose 1920×1080 borderless in the game.

### TriDef 3D

**TriDef 3D must be installed.** TriDef 3D (DDD, discontinued in 2017) has to be on the PC; this app contains none of its
files (they may not be redistributed). Instead of TriDef 3D Ignition's window, the app registers the game with TriDef (the
TriDef profile is picked by the game's name), and **[Run]** starts it through `Tridef3D_Play`, which injects TriDef's DLLs.
A Steam game is started through Steam (an administrator prompt appears); any other game (GOG, EA app, Epic, a disc install)
is started from its exe, and if that exe hands over to its own launcher (the EA app, for one) and comes back as a new
process, that process is injected too. Nothing goes into the game folder. Started directly from Steam or another launcher
the game is not in 3D, so use the app's [Run]. Depth is adjusted in game with TriDef's hotkeys. A game with a TriDef
profile gets this method automatically when its exe is chosen.

Some games only run when their own launcher starts them: GTA V's `GTA5.exe` needs `PlayGTAV.exe`, and GP4 may be started
through CSM. For these, change the **launch method** in the 3D method step. The game stays registered by its own exe
(`GTA5.exe`).
- **Via launcher**: [Run] starts the launcher you set (an exe, a shortcut or a `steam://` address) with your normal rights,
  and the game is injected the moment the launcher starts it. A `Play*.exe`, `CSM*.exe` or `*Launcher*.exe` in the game's
  folder is filled in automatically.
- **Wait only**: [Run] starts nothing. Start the game as usual, from its launcher or a shortcut, and it is injected.

Either way `Tridef3D_Play` never starts the game exe itself. It waits up to 10 minutes and asks for administrator rights,
so it can inject even when the launcher runs the game as administrator. GTA V Enhanced (DirectX 12 only) does not work
with TriDef; GTA V Legacy (DirectX 11) does. Don't use this for modes with anti-cheat (BattlEye), such as GTA Online.

With SR weaving a DirectX 9 game may run below the panel's resolution: SRWeave weaves it at the panel's resolution
(upscaling), so an old game need not be run at 4K with tiny text. To turn that off, put `upscale=0` into
`Packages\TriDef\sr\x86\SRWeave.ini`. 64-bit DirectX 11 games (`sr\x64\SRWeaveDX11.dll`) have the same upscaling now,
not yet tried in a game (pre-release); turn it off with `upscale=0` in `Packages\TriDef\sr\x64\SRWeave.ini`.

## Contents

| Path | Contents |
|---|---|
| `GeoGamingHub.exe` | the app |
| `data\game_candidates.json` | per-game recommended method for the Candidates tab |
| `data\tridef_profiles.txt` | TriDef 3D profile names (to recognise games on PCs without TriDef) |
| `data\dx9fixes` | per-game shader fixes for DX9 games under dgVoodoo2 (e.g. the Beyond Good & Evil HUD), installed with the DX9 kind when the dgVoodoo2 version matches |
| `Packages\Geo-11_Unity` | geo-11 0.6.109 + Unity Universal Fix + SRWeave |
| `Packages\Geo-11_DX11` | geo-11 0.6.109 (Preferred) + SRWeave |
| `Packages\Geo-11_UE4` | UE4 Universal Fix 2 + geo-11 0.6.109 + SRWeave |
| `Packages\Geo-11_DX9` | dgVoodoo2 2.79.1 / 2.86.5 / 2.87.3 |
| `Packages\OGL3D` | OGL3D 1.0.0 (x64, x86) |
| `Packages\VK3D` | VK3D 0.1.0 (x64) + Quake II RTX fix |
| `Packages\DX12_3D` | DX12_3D 0.1.0 (x64) + DXC (Windows SDK) |
| `Packages\DXVK` | DXVK 3.1.1 (x64) |
| `Packages\TriDef` | Tridef3D_Play (tridef3d-win11-injector) + Inject32 + SRWeave DX9/DX11 - no TriDef files |
| `Packages\Fixes` | Community fixes shipped with the hub, one folder per game exe. Now: the STAR WARS Battlefront II (2017) fix and geo-11 0.6.198 |

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

### Community fixes (geo-11 games)

If there is a geo-11 / 3DMigoto fix made **for that game** (HelixMod and the like), import it on the **Install** step under
**Community fix** with **[Import fix (archive)...]** (zip, rar, 7z) or **[From folder...]**, then click [Finish]. Archives
inside a chosen folder are unpacked too (a fix archive next to another driver folder such as "If crash, use this DLL").
rar and 7z are unpacked with 7-Zip or WinRAR when installed, otherwise with Windows' tar.

At [Finish] the hub installs its own geo-11 first and then merges **only the game-specific part** of the fix:

- `d3dx.ini`: the sections the hub's file lacks (`[ShaderOverride*]`, `[TextureOverride*]`, `[ShaderRegex*]`, `[Key*]`,
  `[Preset*]`, `[CommandList*]`, `[Resource*]`, `[CustomShader*]` ...) are appended, and lines of `[Include]`,
  `[Constants]` and `[Present]` the hub's file lacks are added. `[Logging]` `[System]` `[Device]` `[Stereo]` `[Rendering]`
  `[Hunting]` `[Loader]` `[Profile]` are not taken, so SR weaving, upscaling and the overlay keep the hub's settings.
- `ShaderFixes\`, `ShaderFixesDM\`: files whose name the hub's package does not have (the hub's upscale, mouse and hotkey
  files stay).
- geo-11 driver (`d3d11.dll`): the hub's or one inside the fix; the newest by default, and [Add driver...] adds another
  `d3d11.dll`. If the game closes right after it starts, choose another driver and click [Finish] again. Example: Star
  Wars Battlefront II (2017) dies in the NVIDIA driver with geo-11 0.6.109 (the hub's) and 0.6.56 (the fix's) and runs
  with 0.6.198.
- The fix's Sep / Conv become the starting values of the Stereo values step.

An imported fix is kept in `fixes\<exe name>` in the hub's folder and is merged again whenever the game is set up.
[Remove fix] deletes it; the game folder goes back to the hub's own install at the next [Finish]. Not used for UE4 (UF2).

**Fixes shipped with the hub (`Packages\Fixes`).** A game with a fix there gets it without importing anything: the
Install step shows it as "Shipped fix" and [Finish] merges it. Now it holds STAR WARS Battlefront II (2017), which gets
geo-11 0.6.198 by default (with 0.6.109 and 0.6.56 the game closes about a minute in). A fix you import for the same game
is used instead; [Remove fix] turns the shipped one off for that game and [Use shipped fix again] turns it back on. The
fix authors are in `CREDITS.txt` in each folder.

## Hotkeys

geo-11 games: see and change them on the **Hotkeys** tab. The own wrappers read `[Keys]` in their ini; defaults:

| Key | Function |
|---|---|
| Ctrl+F3 / Ctrl+F4 | Separation − / + |
| Ctrl+F5 / Ctrl+F6 | Convergence − / + |
| Ctrl+F7 | save the current values |
| L1+R1 + D-pad (controller) | up / down = Separation + / −, right / left = Convergence + / − (geo-11, OGL3D) |
| (auto) | releasing an adjustment key or the controller combo saves the values (geo-11, OGL3D) |
| Ctrl+F1 | show values |
| Ctrl+F2 | auto convergence (OGL3D, VK3D) |
| Ctrl+T | 3D on / off |
| Ctrl+Alt+W | SR weaving ↔ SBS |

## Anti-cheat

In a game folder with anti-cheat (EasyAntiCheat, BattlEye, GameGuard, ...) the app refuses to install its own wrappers
(OGL3D, VK3D, DX12_3D) and TriDef 3D: DLLs in the game folder can get the account banned. For example, Helldivers 2
(GameGuard) cannot be set up with the app.

Only geo-11 (DirectX 9 / 11) is installed, and only after you answer [Yes] in a warning box (the default is [No]). The
anti-cheat may block the DLLs, so the game may not start or may not be in 3D, and in an online game the account could be
banned. Installing is at your own account risk.

## Known limits

- **DX12_3D**: DLSS does not reach the right eye (grey image): use the game's render scale or FSR / XeSS. No auto convergence yet.
- **Unreal Engine 4 (geo-11)**: DLSS can crash the game with a GPU error (0x887A0006 HUNG; seen in Lies of P). The app warns about games with DLSS, and when it finds DLSS on in the game's settings it offers to turn it off at [Finish] or in **UE4 settings**.
- **Unreal Engine 4 (geo-11) on AMD GPUs**: UF2's `overlay.ini` and `help.ini` make the driver lose the GPU device on the first frame (0x887A0020 / 0x887A0005; seen in Samurai Bringer on a Radeon RX 7600M XT). On any non-NVIDIA GPU (Intel not tried) the app turns these two includes off in `d3dx.ini`, so UF2's in-game overlay and help screen are not there.
- **Heaven Dust 2** (geo-11 Unity, Intel Arc): the Unity fix (`Unity_2019_1.ini`) crashes the game, on the first frame or once a game is started from the menu (seen on an Arc 140V). The app installs the game with that fix turned off (`data\unityfixes`), so some lights and shadows may sit at a slightly different depth in the two eyes.
- The **DXVK route** is experimental; games that work with geo-11 are better on geo-11.
- Games that block third-party DLLs load none of these methods.
- **Burnout Paradise Remastered** (geo-11, 32-bit): the game's webcam code (licence photo, Mugshots) sometimes runs on two threads at once
  and the game closes about 13 s into the intro. SRWeave turns the game's camera off (`hide_cameras=auto` in `SRWeave.ini`), so the
  game runs as on a PC without a webcam. `hide_cameras=1` hides the webcams (DirectShow) from any game whose webcam code crashes.

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
- Community fixes in `Packages\Fixes`: their authors' work, included as distributed. The STAR WARS Battlefront II fix is
  by DHR and DSS, as named in it ("Frostbite3 ShaderRegex by DHR", "Pattern CS Lights and Reflections by DSS");
  see `CREDITS.txt`.
- DXC (`dxcompiler.dll`, `dxil.dll`): Microsoft, Windows SDK redistributables.
