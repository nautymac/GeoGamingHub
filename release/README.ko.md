# Geo Gaming Hub v0.1.0

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
| TriDef 3D 프로필이 있는 게임 | 설치된 TriDef 3D + Tridef3D_Play (Steam 게임) | 32 (DX9) / 64 (DX11) |

geo-11 게임의 SR 위빙은 SRWeave(`dxgi.dll`)가, 자체 래퍼(OGL3D / VK3D / DX12_3D)는 래퍼가 직접 합니다.
DXVK로 DirectX 9 / 11 게임을 Vulkan으로 바꿔 VK3D로 실행하는 방법도 있습니다(실험적, 64비트).

### TriDef 3D

**TriDef 3D 설치 필수.** TriDef 3D(DDD, 2017년 단종)가 PC에 설치돼 있어야 합니다. 이 앱에는 TriDef 파일이 들어 있지 않습니다(재배포 불가).
TriDef 3D Ignition 창 대신 앱이 게임을 TriDef에 등록하고(게임 이름으로 TriDef 프로필 자동 선택), **[실행]**은 `Tridef3D_Play`로
게임을 시작해 TriDef DLL을 주입합니다(관리자 권한 확인 창). 게임 폴더에는 아무것도 넣지 않습니다. Steam 게임만 되고, Steam에서
바로 실행하면 3D가 되지 않으니 앱의 [실행]으로 시작하세요. 깊이는 게임 안에서 TriDef 단축키로 조절합니다. 실행 파일을 고르면 TriDef
프로필이 있는 게임은 자동으로 이 방식이 선택됩니다.

## 들어 있는 것

| 경로 | 내용 |
|---|---|
| `GeoGamingHub.exe` | 앱 |
| `data\game_candidates.json` | 후보 목록 탭의 게임별 추천 3D 방식 |
| `data\tridef_profiles.txt` | TriDef 3D 프로필 이름(TriDef가 없는 PC에서 게임 확인용) |
| `Packages\Geo-11_Unity` | geo-11 0.6.109 + Unity Universal Fix + SRWeave |
| `Packages\Geo-11_DX11` | geo-11 0.6.109 (Preferred) + SRWeave |
| `Packages\Geo-11_UE4` | UE4 Universal Fix 2 + geo-11 0.6.109 + SRWeave |
| `Packages\Geo-11_DX9` | dgVoodoo2 2.79.1 / 2.86.5 / 2.87.3 |
| `Packages\OGL3D` | OGL3D 1.0.0 (x64, x86) |
| `Packages\VK3D` | VK3D 0.1.0 (x64) + Quake II RTX 픽스 |
| `Packages\DX12_3D` | DX12_3D 0.1.0 (x64) + DXC (Windows SDK) |
| `Packages\DXVK` | DXVK 3.1.1 (x64) |
| `Packages\TriDef` | Tridef3D_Play (tridef3d-win11-injector) + Inject32 + SRWeave DX9/DX11 - TriDef 파일 없음 |

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
3. 게임을 실행합니다. 값은 게임 안에서 단축키로 바꾸고 저장합니다.
4. 제거: 게임 목록에서 게임을 고르고 **[3D 픽스 제거]**(모든 파일 삭제, 보관해 둔 원래 파일 복원) 또는
   **[SR 위빙만 제거]**(3D는 SBS로 유지).

게임 폴더에 있던 같은 이름의 다른 DLL(예: 다른 `d3d12.dll`)은 `.geo11sr_bak`으로 보관했다가 제거할 때 되돌립니다.

## 단축키

geo-11 게임은 **단축키** 탭에서 확인하고 바꿉니다. 자체 래퍼는 각 ini의 `[Keys]`에서 바꾸며 기본값은 다음과 같습니다.

| 키 | 기능 |
|---|---|
| Ctrl+F3 / Ctrl+F4 | Separation − / + |
| Ctrl+F5 / Ctrl+F6 | Convergence − / + |
| Ctrl+F7 | 현재 값 저장 |
| Ctrl+F1 | 값 표시 |
| Ctrl+F2 | 자동 Convergence (OGL3D, VK3D) |
| Ctrl+T | 3D 켜기/끄기 |
| Ctrl+Alt+W | SR 위빙 ↔ SBS |

## 안티치트

게임 폴더에 안티치트(EasyAntiCheat, BattlEye, GameGuard 등)가 있으면 앱이 설치하지 않습니다. 게임 폴더에 DLL을 넣으면 계정이
정지될 수 있기 때문입니다. 예: Helldivers 2(GameGuard)는 앱으로 설치되지 않습니다. 직접 설치는 본인 계정의 위험을 감수하는 것입니다.

## 알려진 제한

- **DX12_3D**: DLSS는 오른쪽 눈에 적용되지 않습니다(회색 화면). 게임의 렌더 스케일이나 FSR / XeSS를 쓰세요. 자동 Convergence는 아직 없습니다.
- **DXVK 경로**는 실험적입니다. geo-11이 되는 게임은 geo-11이 더 낫습니다.
- 제3자 DLL을 막는 게임에서는 어느 방식도 로드되지 않습니다.

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
- DXC (`dxcompiler.dll`, `dxil.dll`): Microsoft, Windows SDK 재배포 파일.
