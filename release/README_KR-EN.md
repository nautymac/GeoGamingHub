# Geo Gaming Hub v0.1.0

PC 게임에 입체 3D를 설치하는 통합 앱입니다. 게임 실행 파일을 고르면 맞는 3D 방식을 골라 게임 폴더에 넣고 설정합니다.
SR 디스플레이(Acer SpatialLabs / Leia SR)에서는 SR 위빙, 그 밖의 3D 디스플레이에서는 Side-by-Side / Top-and-Bottom으로 출력합니다.

One app that installs stereo 3D into PC games. Pick the game's exe and it chooses the matching 3D method, copies it into
the game folder and sets it up. Output is SR weaving on SR displays (Acer SpatialLabs / Leia SR), otherwise
side-by-side / top-and-bottom.

## 3D 방식 / 3D methods

| 게임 / Game | 방식 / Method | 비트 / Bits |
|---|---|---|
| Unity (DirectX 11) | geo-11 + Unity Universal Fix | 32 / 64 |
| DirectX 11 (일반 / generic) | geo-11 (Preferred) | 32 / 64 |
| Unreal Engine 4 | geo-11 + UE4 Universal Fix 2 (`-dx11`로 실행 / run with `-dx11`) | 64 |
| DirectX 9 | dgVoodoo2 (D3D9 → D3D11) + geo-11 | 32 / 64 |
| OpenGL | OGL3D (`opengl32.dll`) | 32 / 64 |
| Vulkan | VK3D (`vulkan-1.dll`) | 64 |
| DirectX 12 | DX12_3D (`d3d12.dll`) | 64 |
| TriDef 3D 프로필이 있는 게임 / games with a TriDef 3D profile | 설치된 TriDef 3D + Tridef3D_Play (Steam 게임) / an installed TriDef 3D + Tridef3D_Play (Steam games) | 32 (DX9) / 64 (DX11) |

geo-11 게임의 SR 위빙은 SRWeave(`dxgi.dll`)가, 자체 래퍼(OGL3D / VK3D / DX12_3D)는 래퍼가 직접 합니다.
DXVK로 DirectX 9 / 11 게임을 Vulkan으로 바꿔 VK3D로 실행하는 방법도 있습니다(실험적, 64비트).

geo-11 games are woven by SRWeave (`dxgi.dll`); the own wrappers (OGL3D / VK3D / DX12_3D) weave by themselves.
A DirectX 9 / 11 game can also be run as Vulkan through DXVK and use VK3D (experimental, 64-bit).

### TriDef 3D

**TriDef 3D 설치 필수.** TriDef 3D(DDD, 2017년 단종)가 PC에 설치돼 있어야 합니다. 이 앱에는 TriDef 파일이 들어 있지 않습니다(재배포 불가).
TriDef 3D Ignition 창 대신 앱이 게임을 TriDef에 등록하고(게임 이름으로 TriDef 프로필 자동 선택), **[실행]**은 `Tridef3D_Play`로
게임을 시작해 TriDef DLL을 주입합니다(관리자 권한 확인 창). 게임 폴더에는 아무것도 넣지 않습니다. Steam 게임만 되고, Steam에서
바로 실행하면 3D가 되지 않으니 앱의 [실행]으로 시작하세요. 깊이는 게임 안에서 TriDef 단축키로 조절합니다. 실행 파일을 고르면 TriDef
프로필이 있는 게임은 자동으로 이 방식이 선택됩니다.

**TriDef 3D must be installed.** TriDef 3D (DDD, discontinued in 2017) has to be on the PC; this app contains none of its
files (they may not be redistributed). Instead of TriDef 3D Ignition's window, the app registers the game with TriDef (the
TriDef profile is picked by the game's name), and **[Run]** starts it through `Tridef3D_Play`, which injects TriDef's DLLs
(an administrator prompt appears). Nothing goes into the game folder. Steam games only; started directly from Steam the
game is not in 3D, so use the app's [Run]. Depth is adjusted in game with TriDef's hotkeys. A game with a TriDef profile
gets this method automatically when its exe is chosen.

## 들어 있는 것 / Contents

| 경로 / Path | 내용 / Contents |
|---|---|
| `GeoGamingHub.exe` | 앱 / the app |
| `data\game_candidates.json` | 후보 목록 탭의 게임별 추천 3D 방식 / per-game recommended method for the Candidates tab |
| `data\tridef_profiles.txt` | TriDef 3D 프로필 이름(TriDef가 없는 PC에서 게임 확인용) / TriDef 3D profile names (to recognise games on PCs without TriDef) |
| `Packages\Geo-11_Unity` | geo-11 0.6.109 + Unity Universal Fix + SRWeave |
| `Packages\Geo-11_DX11` | geo-11 0.6.109 (Preferred) + SRWeave |
| `Packages\Geo-11_UE4` | UE4 Universal Fix 2 + geo-11 0.6.109 + SRWeave |
| `Packages\Geo-11_DX9` | dgVoodoo2 2.79.1 / 2.86.5 / 2.87.3 |
| `Packages\OGL3D` | OGL3D 1.0.0 (x64, x86) |
| `Packages\VK3D` | VK3D 0.1.0 (x64) + Quake II RTX 픽스 / fix |
| `Packages\DX12_3D` | DX12_3D 0.1.0 (x64) + DXC (Windows SDK) |
| `Packages\DXVK` | DXVK 3.1.1 (x64) |
| `Packages\TriDef` | Tridef3D_Play (tridef3d-win11-injector) + Inject32 + SRWeave DX9/DX11 - TriDef 파일 없음 / no TriDef files |

`Packages` 폴더는 `GeoGamingHub.exe` 옆에 있어야 합니다. zip을 모두 풀고 실행하세요(zip 안에서 바로 실행하지 마세요).
Keep `Packages` next to `GeoGamingHub.exe`. Unzip everything first; don't run it from inside the zip.

## 필요한 것 / Requirements

- Windows 10 / 11, .NET Framework 4.x (Windows에 기본 포함 / built into Windows)
- SR 위빙: SR 디스플레이와 SR 플랫폼 런타임(Acer는 SpatialLabs Experience Center). SR 위빙을 끄면 일반 3D 디스플레이용
  SBS / TAB 출력으로 쓸 수 있고, 이때는 SR 런타임이 필요 없습니다.
  SR weaving: an SR display and its SR platform runtime (Acer: SpatialLabs Experience Center). With SR weaving off, the
  output is SBS / TAB for other 3D displays and no SR runtime is needed.
- 게임은 SR 패널의 원래 해상도로, 전체화면 또는 테두리 없는 창으로 실행합니다.
  Run games at the panel's native resolution, full screen or borderless.

## 사용법 / How to use

1. `GeoGamingHub.exe`를 실행하고 **게임 목록** 탭에서 **[게임 추가 (설정 시작)]**으로 게임 실행 파일을 고릅니다
   (창에 끌어다 놓아도 됩니다). **후보 목록** 탭에서 설치된 게임을 골라 시작할 수도 있습니다.
   Run `GeoGamingHub.exe`, then on **Games** click **[Add game (start setup)]** and pick the game's exe (or drop it on
   the window). You can also start from an installed game on the **Candidates** tab.
2. **3D 방식** → **설치** → **디스플레이** → **입체 값**의 4단계를 [다음]으로 넘기고 [완료]를 누릅니다. 3D 방식은 실행 파일에서
   자동으로 고르며, 다르면 바꿉니다. SR 디스플레이가 없으면 설치 단계에서 **SBS만 설치**(3D TV, Non-SR 모니터용)를 고릅니다.
   Go through the four steps **3D Method** → **Install** → **Display** → **Stereo values** with [Next], then click
   [Finish]. The 3D method is detected from the exe; change it if it is wrong. Without an SR display, pick **SBS only**
   (3D TV, non-SR monitor) on the Install step.
3. 게임을 실행합니다. 값은 게임 안에서 단축키로 바꾸고 저장합니다.
   Start the game. Adjust and save the values in-game with the hotkeys.
4. 제거: 게임 목록에서 게임을 고르고 **[3D 픽스 제거]**(모든 파일 삭제, 보관해 둔 원래 파일 복원) 또는
   **[SR 위빙만 제거]**(3D는 SBS로 유지).
   Uninstall: select the game on **Games**, then **[Uninstall 3D fix]** (deletes every file and restores the kept
   originals) or **[Remove SR weaving only]** (3D stays as SBS).

게임 폴더에 있던 같은 이름의 다른 DLL(예: 다른 `d3d12.dll`)은 `.geo11sr_bak`으로 보관했다가 제거할 때 되돌립니다.
A different DLL of the same name already in the game folder is kept as `.geo11sr_bak` and restored on uninstall.

## 단축키 / Hotkeys

geo-11 게임은 **단축키** 탭에서 확인하고 바꿉니다. 자체 래퍼는 각 ini의 `[Keys]`에서 바꾸며 기본값은 다음과 같습니다.
geo-11 games: see and change them on the **Hotkeys** tab. The own wrappers read `[Keys]` in their ini; defaults:

| 키 / Key | 기능 / Function |
|---|---|
| Ctrl+F3 / Ctrl+F4 | Separation − / + |
| Ctrl+F5 / Ctrl+F6 | Convergence − / + |
| Ctrl+F7 | 현재 값 저장 / save the current values |
| Ctrl+F1 | 값 표시 / show values |
| Ctrl+F2 | 자동 Convergence (OGL3D, VK3D) / auto convergence (OGL3D, VK3D) |
| Ctrl+T | 3D 켜기/끄기 / 3D on / off |
| Ctrl+Alt+W | SR 위빙 ↔ SBS / SR weaving ↔ SBS |

## 안티치트 / Anti-cheat

게임 폴더에 안티치트(EasyAntiCheat, BattlEye, GameGuard 등)가 있으면 앱이 설치하지 않습니다. 게임 폴더에 DLL을 넣으면 계정이
정지될 수 있기 때문입니다. 예: Helldivers 2(GameGuard)는 앱으로 설치되지 않습니다. 직접 설치는 본인 계정의 위험을 감수하는 것입니다.

The app refuses to install into a game folder with anti-cheat (EasyAntiCheat, BattlEye, GameGuard, ...): DLLs in the game
folder can get the account banned. For example, Helldivers 2 (GameGuard) cannot be set up with the app. Installing by
hand is at your own account risk.

## 알려진 제한 / Known limits

- **DX12_3D**: DLSS는 오른쪽 눈에 적용되지 않습니다(회색 화면). 게임의 렌더 스케일이나 FSR / XeSS를 쓰세요. 자동 Convergence는 아직 없습니다.
  DLSS does not reach the right eye (grey image): use the game's render scale or FSR / XeSS. No auto convergence yet.
- **DXVK 경로**는 실험적입니다. geo-11이 되는 게임은 geo-11이 더 낫습니다.
  The **DXVK route** is experimental; games that work with geo-11 are better on geo-11.
- 제3자 DLL을 막는 게임에서는 어느 방식도 로드되지 않습니다.
  Games that block third-party DLLs load none of these methods.
- **[3D 동작 보고...]**는 GitHub 보고 양식(게임, PCGamingWiki 링크, 3D 방식, 설정이 채워짐)을 열고, **호환 게임 목록**은 보고로
  자동으로 만들어지는 커뮤니티 목록을 엽니다. 저장소(nautymac/GeoGamingHub)가 아직 비공개라 지금은 저장소 권한이 있는 사람만 쓸 수 있습니다.
  **[Report 3D...]** opens the GitHub report form (game, PCGamingWiki link, 3D method and setup filled in) and the
  **Compatibility list** link opens the community list built from the reports. The repository (nautymac/GeoGamingHub) is
  still private, so for now only people with access to it can use them.

## 문제 해결 / Troubleshooting

앱의 **로그** 탭과 게임 폴더의 래퍼 로그(`OGL3D.log`, `VK3D.log`, `DX12_3D.log`, geo-11은 `d3d11_log.txt`)를 함께 보내 주세요.
각 래퍼의 자세한 설명은 `Packages\<래퍼>` 안의 README에 있습니다.

Send the app's **Log** tab together with the wrapper log in the game folder (`OGL3D.log`, `VK3D.log`, `DX12_3D.log`;
geo-11: `d3d11_log.txt`). Each wrapper's README is inside `Packages\<wrapper>`.

## 라이선스 / Licenses

- OGL3D, DX12_3D: MIT (`Packages\OGL3D\LICENSE`).
- DXVK: zlib/libpng (`Packages\DXVK\LICENSE`).
- Tridef3D_Play / Inject32 / SRWeave (tridef3d-win11-injector): MIT (`Packages\TriDef\LICENSE`). TriDef 3D 자체는 포함되지 않으며
  DDD의 소프트웨어입니다 / TriDef 3D itself is not included and is DDD's software.
- geo-11 / 3DMigoto, Unity Universal Fix, UE4 Universal Fix 2, dgVoodoo2: 각 제작자의 조건 (각 패키지 폴더) /
  their authors' terms (in each package folder).
- DXC (`dxcompiler.dll`, `dxil.dll`): Microsoft, Windows SDK 재배포 파일 / Windows SDK redistributables.
