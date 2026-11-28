# Geo Gaming Hub v0.1.8

[English](README.md) · **한국어**

PC 게임에 입체 3D를 설치하는 통합 앱입니다. 게임 실행 파일을 고르면 맞는 3D 방식을 골라 게임 폴더에 넣고 설정합니다.
SR 디스플레이(Acer SpatialLabs / Leia SR)에서는 SR 위빙, 그 밖의 3D 디스플레이에서는 Side-by-Side / Top-and-Bottom으로 출력합니다.

## 3D 방식

| 게임 | 방식 | 비트 |
|---|---|---|
| Unity (DirectX 11) | geo-11 + Unity Universal Fix | 32 / 64 |
| DirectX 11 (일반) | geo-11 (Preferred) | 32 / 64 |
| Unreal Engine 4 | geo-11 + UE4 Universal Fix 2 (`-dx11`로 실행) | 64 |
| DirectX 9 | dgVoodoo2 (D3D9 → D3D11) + geo-11 | 32 / 64 |
| OpenGL | OGL3D (`opengl32.dll`) | 32 / 64 |
| Vulkan | VK3D (`vulkan-1.dll`) | 64 |
| DirectX 12 | DX12_3D (`d3d12.dll`) | 64 |
| TriDef 3D 프로필이 있는 게임 | 설치된 TriDef 3D + Tridef3D_Play (Steam 게임과 그 밖의 게임) | 32 (DX9) / 64 (DX11) |

geo-11 게임의 SR 위빙은 SRWeave(`dxgi.dll`)가, 자체 래퍼(OGL3D / VK3D / DX12_3D)는 래퍼가 직접 합니다.
DXVK로 DirectX 9 / 11 게임을 Vulkan으로 바꿔 VK3D로 실행하는 방법도 있습니다(실험적, 64비트).
OGL3D와 DX12_3D 게임은 패널보다 낮은 해상도로 실행할 수 있습니다. 디스플레이 단계에서 **업스케일 사용**을 켜면 게임은
1920×1080(200% 배율의 4K 패널: 메뉴·글씨가 읽기 좋음)으로 그리고, 래퍼가 게임 창 위의 자체 창에 패널 해상도로 위빙합니다.
게임에서는 1920×1080 테두리 없는 창을 고르세요.

### TriDef 3D

**TriDef 3D 설치 필수.** TriDef 3D(DDD, 2017년 단종)가 PC에 설치돼 있어야 합니다. 이 앱에는 TriDef 파일이 들어 있지 않습니다(재배포 불가).
TriDef 3D Ignition 창 대신 앱이 게임을 TriDef에 등록하고(게임 이름으로 TriDef 프로필 자동 선택), **[실행]**은 `Tridef3D_Play`로
게임을 시작해 TriDef DLL을 주입합니다. Steam 게임은 Steam으로 시작하고(관리자 권한 확인 창), 그 밖의 게임(GOG, EA 앱, Epic,
디스크 설치본)은 실행 파일로 직접 시작합니다. 실행 파일이 자기 런처(EA 앱 등)에 넘겼다가 새 프로세스로 다시 뜨면 그 프로세스에도
주입합니다. 게임 폴더에는 아무것도 넣지 않습니다. Steam이나 다른 런처에서 바로 실행하면 3D가 되지 않으니 앱의 [실행]으로 시작하세요.
깊이는 게임 안에서 TriDef 단축키로 조절합니다. 실행 파일을 고르면 TriDef 프로필이 있는 게임은 자동으로 이 방식이 선택됩니다.

GTA V(`GTA5.exe`는 `PlayGTAV.exe`가 실행해야 뜸)나 CSM으로 시작하는 GP4처럼 **자기 런처가 실행해야만 뜨는 게임**은 3D 방식 단계의
**실행 방법**을 바꿉니다. 등록은 게임 자신의 실행 파일(`GTA5.exe`)로 합니다.
- **런처로 실행**: [실행]이 지정한 런처(실행 파일, 바로 가기, `steam://` 주소)를 사용자 권한으로 실행하고, 런처가 게임을 띄우는 순간
  주입합니다. 게임 폴더에 `Play*.exe`, `CSM*.exe`, `*Launcher*.exe`가 있으면 자동으로 채웁니다.
- **기다리기만**: [실행]은 아무것도 실행하지 않습니다. 그다음 평소처럼 런처나 바로 가기로 게임을 시작하면 주입합니다.

두 방식 모두 `Tridef3D_Play`가 게임 실행 파일을 직접 띄우지 않고 최대 10분 기다리며, 관리자 권한 확인 창이 뜹니다(런처가 게임을
관리자 권한으로 띄워도 주입할 수 있게). GTA V Enhanced(DirectX 12 전용)는 TriDef로 안 되고 Legacy(DirectX 11)만 됩니다.
GTA Online처럼 안티치트(BattlEye)가 있는 모드에서는 쓰지 마세요.

SR 위빙을 켠 DirectX 9 게임은 패널보다 낮은 해상도로 실행해도 됩니다. SRWeave가 패널 해상도에서 위빙해 내보내므로(업스케일),
오래된 게임을 4K로 돌려 글자가 작아지는 일을 피할 수 있습니다. 끄려면 `Packages\TriDef\sr\x86\SRWeave.ini`에 `upscale=0`을 넣습니다.
64비트 DirectX 11 게임(`sr\x64\SRWeaveDX11.dll`)에도 같은 업스케일이 들어갔지만, 아직 게임에서 시험하지 않았습니다(프리릴리스).
끄려면 `Packages\TriDef\sr\x64\SRWeave.ini`에 `upscale=0`을 넣습니다.

## 들어 있는 것

| 경로 | 내용 |
|---|---|
| `GeoGamingHub.exe` | 앱 |
| `data\game_candidates.json` | 후보 목록 탭의 게임별 추천 3D 방식 |
| `data\tridef_profiles.txt` | TriDef 3D 프로필 이름(TriDef가 없는 PC에서 게임 확인용) |
| `data\dx9fixes` | dgVoodoo2로 도는 DX9 게임의 게임별 셰이더 픽스(예: Beyond Good & Evil HUD). DX9 방식으로 설치할 때 dgVoodoo2 버전이 맞으면 함께 설치 |
| `Packages\Geo-11_Unity` | geo-11 0.6.109 + Unity Universal Fix + SRWeave |
| `Packages\Geo-11_DX11` | geo-11 0.6.109 (Preferred) + SRWeave |
| `Packages\Geo-11_UE4` | UE4 Universal Fix 2 + geo-11 0.6.109 + SRWeave |
| `Packages\Geo-11_DX9` | dgVoodoo2 2.79.1 / 2.86.5 / 2.87.3 |
| `Packages\OGL3D` | OGL3D 1.0.0 (x64, x86) |
| `Packages\VK3D` | VK3D 0.1.0 (x64) + Quake II RTX 픽스 |
| `Packages\DX12_3D` | DX12_3D 0.1.0 (x64) + DXC (Windows SDK) |
| `Packages\DXVK` | DXVK 3.1.1 (x64) |
| `Packages\TriDef` | Tridef3D_Play (tridef3d-win11-injector) + Inject32 + SRWeave DX9/DX11 - TriDef 파일 없음 |
| `Packages\Fixes` | 함께 배포하는 커뮤니티 픽스(게임 실행 파일 이름별 폴더). 지금은 STAR WARS Battlefront II(2017) 픽스와 geo-11 0.6.198 |

`Packages` 폴더는 `GeoGamingHub.exe` 옆에 있어야 합니다. zip을 모두 풀고 실행하세요(zip 안에서 바로 실행하지 마세요).

## 필요한 것

- Windows 10 / 11, .NET Framework 4.x (Windows에 기본 포함)
- SR 위빙: SR 디스플레이와 SR 플랫폼 런타임(Acer는 SpatialLabs Experience Center). SR 위빙을 끄면 일반 3D 디스플레이용
  SBS / TAB 출력으로 쓸 수 있고, 이때는 SR 런타임이 필요 없습니다.
- 게임은 SR 패널의 원래 해상도로, 전체화면 또는 테두리 없는 창으로 실행합니다.

## 사용법

1. `GeoGamingHub.exe`를 실행하고 **게임 목록** 탭에서 **[게임 추가 (설정 시작)]**으로 게임 실행 파일을 고릅니다
   (창에 끌어다 놓아도 됩니다). **후보 목록** 탭에서 설치된 게임을 골라 시작할 수도 있습니다.
2. **3D 방식** → **설치** → **디스플레이** → **입체 값**의 4단계를 [다음]으로 넘기고 [완료]를 누릅니다. 3D 방식은 실행 파일에서
   자동으로 고르며, 다르면 바꿉니다. SR 디스플레이가 없으면 설치 단계에서 **SBS만 설치**(3D TV, Non-SR 모니터용)를 고릅니다.
   SBS만 설치도 SRWeave의 `dxgi.dll`은 위빙을 끈 채(`SRWeave.ini`의 `weave=0`) 넣습니다. geo-11만 있으면 해상도를 바꿀 때
   꺼지는 게임이 있기 때문입니다(STAR WARS Battlefront II가 전체화면 해상도로 바꿀 때). DirectX 9 게임과, 게임 폴더에 다른
   `dxgi.dll`(ReShade)이 있을 때는 넣지 않습니다.
3. 게임을 실행합니다. 값은 게임 안에서 단축키로 바꾸고 저장합니다.
4. 제거: 게임 목록에서 게임을 고르고 **[3D 픽스 제거]**(모든 파일 삭제, 보관해 둔 원래 파일 복원) 또는
   **[SR 위빙만 제거]**(3D는 SBS로 유지).

게임 폴더에 있던 같은 이름의 다른 DLL(예: 다른 `d3d12.dll`)은 `.geo11sr_bak`으로 보관했다가 제거할 때 되돌립니다.

### 커뮤니티 픽스 (geo-11 게임)

`Packages\Fixes`에 픽스가 있는 게임(또는 직접 불러온 픽스가 있는 게임)은 3D 방식 단계에서 "허브에 이 게임의 커뮤니티 픽스 있음"으로
표시되고, 다른 추정(DirectX 12, TriDef 등)보다 먼저 geo-11 + 픽스를 고릅니다.

HelixMod 등에서 받은 **그 게임 전용** geo-11 / 3DMigoto 픽스가 있으면, **설치** 단계의 **커뮤니티 픽스**에서
**[픽스 불러오기 (압축 파일)...]**(zip, rar, 7z) 또는 **[폴더에서...]**로 불러온 뒤 [완료]를 누릅니다. 폴더 안의 압축 파일도
함께 풉니다(픽스 압축 파일 옆에 "If crash, use this DLL" 같은 다른 드라이버 폴더가 있는 경우). rar·7z는 7-Zip이나 WinRAR가
있으면 그것으로, 없으면 Windows의 tar로 풉니다.

[완료] 때 허브는 먼저 자기 geo-11을 설치하고, 그 위에 픽스의 **게임 전용 부분만** 합칩니다.

- `d3dx.ini`: 허브에 없는 섹션(`[ShaderOverride*]`, `[TextureOverride*]`, `[ShaderRegex*]`, `[Key*]`, `[Preset*]`,
  `[CommandList*]`, `[Resource*]`, `[CustomShader*]` 등)을 끝에 붙이고, `[Include]`·`[Constants]`·`[Present]`에서 허브에 없는
  줄을 더합니다. `[Logging]` `[System]` `[Device]` `[Stereo]` `[Rendering]` `[Hunting]` `[Loader]` `[Profile]`은 넣지 않습니다.
  SR 위빙·업스케일·오버레이는 허브 설정 그대로입니다.
- `ShaderFixes\`, `ShaderFixesDM\`: 허브 패키지에 같은 이름이 없는 파일을 넣습니다(업스케일·마우스·단축키 파일은 허브 것).
- geo-11 드라이버(`d3d11.dll`): 허브 것과 픽스 안에 든 것 중 고릅니다. 기본은 가장 새 버전이고, [드라이버 추가...]로 다른
  `d3d11.dll`을 넣을 수 있습니다. 게임이 시작하자마자 꺼지면 다른 드라이버를 고르고 [완료]를 다시 누르세요.
  예: Star Wars Battlefront II(2017)는 geo-11 0.6.109(허브)와 0.6.56(픽스)에서 NVIDIA 드라이버 안에서 꺼지고 0.6.198에서 됩니다.
- 픽스의 Sep / Conv가 입체 값 단계의 시작 값이 됩니다.

불러온 픽스는 허브 폴더의 `fixes\<실행 파일 이름>`에 보관되어, 다시 설치해도 그대로 합쳐집니다. [픽스 빼기]는 보관한 픽스를
지우고, 게임 폴더는 다음 [완료] 때 허브 기본 설치로 돌아갑니다. UE4(UF2) 방식에는 쓰지 않습니다.

**함께 배포하는 픽스 (`Packages\Fixes`).** 픽스가 들어 있는 게임은 따로 불러오지 않아도 설치 단계에 "배포에 포함된 픽스"로 나오고
[완료] 때 합쳐집니다. 지금은 STAR WARS Battlefront II(2017)이 들어 있습니다. 이 게임은 geo-11 0.6.198이 기본으로 골라집니다
(0.6.109·0.6.56에서는 1분쯤 뒤 꺼짐). 직접 불러온 픽스가 있으면 그것을 쓰고, [픽스 빼기]를 누르면 그 게임에서 배포 픽스를 끕니다.
다시 쓰려면 [배포 픽스 다시 쓰기]를 누릅니다. 픽스 제작자는 각 폴더의 `CREDITS.txt`에 있습니다.

## 단축키

geo-11 게임은 **단축키** 탭에서 확인하고 바꿉니다. 자체 래퍼는 각 ini의 `[Keys]`에서 바꾸며 기본값은 다음과 같습니다.

| 키 | 기능 |
|---|---|
| Ctrl+F3 / Ctrl+F4 | Separation − / + |
| Ctrl+F5 / Ctrl+F6 | Convergence − / + |
| Ctrl+F7 | 현재 값 저장 |
| L1+R1 + 십자키 (게임패드) | 위 / 아래 = Separation + / −, 오른쪽 / 왼쪽 = Convergence + / − (geo-11, OGL3D) |
| (자동) | 조절 키나 패드 조합을 놓으면 값을 자동 저장 (geo-11, OGL3D) |
| Ctrl+F1 | 값 표시 |
| Ctrl+F2 | 자동 Convergence (OGL3D, VK3D) |
| Ctrl+T | 3D 켜기/끄기 |
| Ctrl+Alt+W | SR 위빙 ↔ SBS |

## 안티치트

게임 폴더에 안티치트(EasyAntiCheat, BattlEye, GameGuard 등)가 있으면 자체 래퍼(OGL3D, VK3D, DX12_3D)와 TriDef 3D는 앱이
설치하지 않습니다. 게임 폴더에 DLL을 넣으면 계정이 정지될 수 있기 때문입니다. 예: Helldivers 2(GameGuard)는 앱으로 설치되지 않습니다.

geo-11(DirectX 9 / 11)만 경고 창에서 [예]를 눌러야 설치됩니다(기본 선택은 [아니요]). 안티치트가 DLL을 막아 게임이 실행되지 않거나
3D가 되지 않을 수 있고, 온라인 게임에서는 계정이 정지될 수 있습니다. 설치는 본인 계정의 위험을 감수하는 것입니다.

## 알려진 제한

- **DX12_3D**: DLSS는 오른쪽 눈에 적용되지 않습니다(회색 화면). 게임의 렌더 스케일이나 FSR / XeSS를 쓰세요. 자동 Convergence는 아직 없습니다.
- **Unreal Engine 4 (geo-11)**: DLSS를 켜면 게임이 GPU 오류(0x887A0006 HUNG)로 꺼질 수 있습니다(Lies of P에서 확인). 앱이 DLSS가 있는 게임을 경고하고, 게임 설정에서 DLSS가 켜진 것을 찾으면 [완료]나 **UE4 설정**에서 끌 수 있습니다.
- **Unreal Engine 4 (geo-11), AMD GPU**: UF2의 `overlay.ini`와 `help.ini`가 첫 프레임에 드라이버의 GPU 장치를 잃게 해 게임이 꺼집니다(0x887A0020 / 0x887A0005, Radeon RX 7600M XT의 Samurai Bringer에서 확인). NVIDIA가 아닌 GPU에서는(Intel은 시험하지 않음) 앱이 `d3dx.ini`에서 이 두 include를 끄므로, UF2의 게임 내 오버레이와 도움말 화면은 나오지 않습니다.
- **Heaven Dust 2** (geo-11 Unity, Intel Arc): Unity 픽스(`Unity_2019_1.ini`)를 켜면 첫 프레임에서, 또는 메뉴에서 게임을 시작할 때 게임이 꺼집니다(Arc 140V에서 확인). 앱은 이 픽스를 끈 상태로 설치하므로(`data\unityfixes`), 일부 조명과 그림자가 두 눈에서 약간 다른 깊이로 보일 수 있습니다.
- **DXVK 경로**는 실험적입니다. geo-11이 되는 게임은 geo-11이 더 낫습니다.
- 제3자 DLL을 막는 게임에서는 어느 방식도 로드되지 않습니다.
- **Burnout Paradise Remastered** (geo-11, 32비트): 게임의 웹캠 코드(면허 사진, 머그샷)가 가끔 두 스레드에서 동시에 돌아서 인트로
  약 13초 만에 게임이 꺼집니다. SRWeave가 게임의 카메라 기능을 끄므로(`SRWeave.ini`의 `hide_cameras=auto`) 웹캠 없는 PC처럼
  실행됩니다. `hide_cameras=1`은 웹캠 코드에서 죽는 다른 게임에도 웹캠(DirectShow)을 숨깁니다.

## 커뮤니티 보고

**[3D 동작 보고...]**는 GitHub 보고 양식(게임, PCGamingWiki 링크, 3D 방식, 설정이 채워짐)을 열고, **호환 게임 목록**은 보고로
자동으로 만들어지는 커뮤니티 목록을 엽니다(보고하려면 GitHub 계정이 필요합니다).

## 문제 해결

앱의 **로그** 탭과 게임 폴더의 래퍼 로그(`OGL3D.log`, `VK3D.log`, `DX12_3D.log`, geo-11은 `d3d11_log.txt`)를 함께 보내 주세요.
각 래퍼의 자세한 설명은 `Packages\<래퍼>` 안의 README에 있습니다.

## 라이선스

- OGL3D, DX12_3D: MIT (`Packages\OGL3D\LICENSE`).
- DXVK: zlib/libpng (`Packages\DXVK\LICENSE`).
- Tridef3D_Play / Inject32 / SRWeave (tridef3d-win11-injector): MIT (`Packages\TriDef\LICENSE`). TriDef 3D 자체는 포함되지 않으며
  DDD의 소프트웨어입니다.
- geo-11 / 3DMigoto, Unity Universal Fix, UE4 Universal Fix 2, dgVoodoo2: 각 제작자의 조건 (각 패키지 폴더).
- `Packages\Fixes`의 커뮤니티 픽스: 각 제작자의 작업이며 받은 그대로 넣었습니다. STAR WARS Battlefront II 픽스는 픽스 안에
  "Frostbite3 ShaderRegex by DHR", "Pattern CS Lights and Reflections by DSS"로 표기된 DHR, DSS의 작업입니다(`CREDITS.txt`).
- DXC (`dxcompiler.dll`, `dxil.dll`): Microsoft, Windows SDK 재배포 파일.
