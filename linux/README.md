# GeoGamingHub for Linux (geo-11)

DroidDeck 데스크탑 모드나 Steam Deck 데스크탑 모드에서 Steam(Proton) 게임에 geo-11 3D를 설치하는 도구입니다. GeoGamingHub의 `Packages` 폴더를 그대로 사용합니다.

## 지원 범위 (v0.1)
- DX11 게임: 일반 게임과 Unity 게임. Unity는 버전을 감지해서 맞는 Unity fix를 켭니다.
- 32비트와 64비트 게임.
- 출력: `direct_mode = sbs`(Half-SBS, 기본), `sbs_reversed`, `tab`, `tab_reversed`.
- upscale은 기본으로 켜집니다: `upscaling = 1`, `upscale_mode = 1`, `ShaderFixes\upscale.ini`.
- 아직 지원하지 않는 것:
  - UE4 게임
  - DX9, DX12, Vulkan, OpenGL 게임
  - 게임별 커뮤니티 fix 합치기
  - SR 위빙(SRWeave)

## 설치
1. GeoGamingHub 릴리스 zip을 기기에 풉니다. 예: `~/GeoGamingHub/`
2. 이 폴더의 파일을 GeoGamingHub 폴더 안에 넣습니다. 예: `~/GeoGamingHub/linux/`
3. 데스크탑 모드 터미널에서 실행합니다.
   ```sh
   sh ~/GeoGamingHub/linux/install-desktop.sh
   ```
   앱 메뉴와 바탕화면에 "GeoGamingHub (Linux)" 아이콘이 생깁니다.

`Packages` 폴더는 다음 순서로 찾습니다.
1. `--packages` 옵션으로 지정한 폴더
2. `GGH_PACKAGES` 환경 변수
3. 스크립트 옆의 `Packages`
4. 스크립트 상위 폴더의 `Packages`
5. `~/GeoGamingHub/Packages`

## 사용
1. 아이콘을 실행합니다.
2. 게임 번호를 고르고 "설치"를 누릅니다.
3. 안내에 나오는 실행 옵션을 Steam > 게임 속성 > 실행 옵션에 넣습니다. 클립보드에 자동으로 복사됩니다.
   ```
   WINEDLLOVERRIDES="d3d11,nvapi64,d3dcompiler_47=n,b" %command%
   ```
   32비트 게임은 `nvapi64` 대신 `nvapi`입니다.
4. DroidDeck에서는 Cardboard VR > 화면 "사이드 바이 사이드 3D", 뷰어 "렌즈 없음"으로 봅니다.

명령줄로도 쓸 수 있습니다.
```sh
python3 geogaminghub-linux.py --list
python3 geogaminghub-linux.py --install <appid|이름|exe경로> [--mode tab] [--no-upscale]
python3 geogaminghub-linux.py --uninstall <appid|이름|exe경로>
```

## 제거와 복원
- 설치할 때 덮어쓰는 기존 파일은 게임 폴더의 `.geo11_linux/backup/`에 백업합니다. 무엇을 설치했는지는 `.geo11_linux/manifest.json`에 기록합니다.
- 제거하면 다음을 수행해서 게임 폴더를 설치 전 상태로 되돌립니다.
  - 설치한 파일 삭제
  - 백업 복원
  - geo-11이 실행 중에 만든 파일 삭제: `ShaderCache`, `ShaderCacheDM`, `d3dx_user.ini` 등

## 참고
- 게임이 시작할 때 멈추면 게임 안에서 창 모드 전체화면(windowed fullscreen)으로 바꿔 보세요. upscale 사용 시 Steam Deck에서 보고된 증상입니다.
- 단축키:
  - Ctrl+T: 3D 켜기/끄기
  - Ctrl+F3 / Ctrl+F4: 깊이
  - Ctrl+F5 / Ctrl+F6: 수렴
  - Ctrl+F7: 저장
- geo-11 바이너리는 이 도구에 들어 있지 않습니다. 저작권은 geo-11 제작자(bo3b, DarkStarSword 외)에게 있습니다.
