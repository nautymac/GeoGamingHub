# Geo Gaming Hub

**English** · [한국어](README.ko.md)

One app that installs stereo 3D into PC games. Pick the game's exe and it chooses the matching 3D method, copies it into
the game folder and sets it up. Output is SR weaving on SR displays (Acer SpatialLabs / Leia SR), otherwise side-by-side /
top-and-bottom for 3D TVs and other monitors.

![Geo Gaming Hub](docs/screenshot.png)

| Game | Method |
|---|---|
| Unity, DirectX 11, Unreal Engine 4, DirectX 9 | geo-11 (+ Unity / UE4 Universal Fix, dgVoodoo2 for DX9) + SRWeave |
| OpenGL | [OGL3D](https://github.com/nautymac/OGL3D-SR) |
| Vulkan | VK3D |
| DirectX 12 | DX12_3D |
| Games with a TriDef 3D profile | an installed TriDef 3D + Tridef3D_Play (TriDef 3D must be installed) |

Usage, hotkeys, the bundled packages and their licenses: [release/README.md](release/README.md).

## Repository

| Path | Contents |
|---|---|
| `src\` | app source (C# WinForms, .NET Framework 4.x); `src\build.cmd` → `GeoGamingHub.exe` |
| `tests\` | `HubTest.exe` self-tests (unit, gui, vkunit, vkgui, cand, dx12unit, dx12gui, tri, pkg) |
| `data\` | candidate list data, TriDef profile names |
| `tools\library_survey\` | scripts that build the candidate list (PCGamingWiki) |
| `tools\icon\make_icon.py` | builds the icon (`src\app.ico`) |
| `.github\` | game report forms |

The install packages (`Packages\`: geo-11, OGL3D, VK3D, DX12_3D, DXVK, TriDef launcher) are not in the repository; they
ship in the release zip. `Packages\` must sit next to `GeoGamingHub.exe`.
