#!/usr/bin/env python3
"""GeoGamingHub for Linux: installs geo-11 (DX11) into Steam/Proton games.

Runs in the DroidDeck or Steam Deck desktop. Reuses GeoGamingHub's Packages
folder (Packages/Geo-11_DX11, Packages/Geo-11_Unity). Output is side-by-side
(geo-11 direct_mode=sbs, half width), which DroidDeck shows with
Picture "Side-by-side 3D" and Viewer "No lens".
"""
import argparse
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import time

VERSION = "0.1.0"
STATE_DIR = ".geo11_linux"
MANIFEST = "manifest.json"
MODES = ["sbs", "sbs_reversed", "tab", "tab_reversed"]
UNITY_INCLUDES = {
    "55": r"ShaderFixes\Unity55_DHR.ini",
    "56": r"ShaderFixes\Unity56_DHR.ini",
    "2017": r"ShaderFixes\Unity20171_DHR.ini",
    "2019": r"ShaderFixes\Unity_2019_1.ini",
}
SKIP_TOP = {"dxgi.dll", "srweave.ini", "srweave.log", "uninstall.bat"}
SKIP_DIRS = {os.path.join("ShaderFixes", "SRWeave").lower()}
RUNTIME_ARTIFACTS = ["d3dx_user.ini", "d3d11_log.txt", "d3d11_profile_log.txt", "nvapi_log.txt",
                     "ShaderCache", "ShaderCacheDM", "ShaderFromGame", "DMAutoPatchCache", "DMAutoPatchFailures"]
NOT_GAMES = re.compile(r"Proton|Steamworks|Steam Linux Runtime|SteamVR|Redistributable", re.I)
NOT_EXE = re.compile(r"unins|crash|setup|launcher|redist|vcredist|dxsetup|dotnet|ue4prereq|helper|report|updater|easyanticheat|battleye", re.I)

KO = os.environ.get("LANG", "").lower().startswith("ko") or os.environ.get("GGH_LANG") == "ko"


def T(ko, en):
    return ko if KO else en


# ---------- Steam library discovery ----------

def steam_roots(extra):
    home = os.path.expanduser("~")
    cands = list(extra) + [os.path.join(home, ".local/share/Steam"), os.path.join(home, ".steam/steam"),
                           os.path.join(home, ".steam/root"), "/mnt/droiddeck-sd/x86-64", "/mnt/droiddeck-sd"]
    libs, seen = [], set()

    def add(p):
        if not p or not os.path.isdir(os.path.join(p, "steamapps")):
            return
        rp = os.path.realpath(p)
        if rp not in seen:
            seen.add(rp)
            libs.append(p)

    for root in cands:
        add(root)
        vdf = os.path.join(root, "steamapps", "libraryfolders.vdf")
        try:
            with open(vdf, encoding="utf-8", errors="replace") as f:
                for m in re.finditer(r'"path"\s+"([^"]+)"', f.read()):
                    add(m.group(1).replace("\\\\", "/"))
        except OSError:
            pass
    return libs


def steam_games(libs):
    games = []
    for lib in libs:
        sa = os.path.join(lib, "steamapps")
        for entry in sorted(os.listdir(sa)):
            if not (entry.startswith("appmanifest_") and entry.endswith(".acf")):
                continue
            try:
                with open(os.path.join(sa, entry), encoding="utf-8", errors="replace") as f:
                    kv = dict(re.findall(r'"(appid|name|installdir)"\s+"([^"]*)"', f.read()))
            except OSError:
                continue
            if not kv.get("installdir") or kv.get("appid") == "228980" or NOT_GAMES.search(kv.get("name", "")):
                continue
            d = os.path.join(sa, "common", kv["installdir"])
            if os.path.isdir(d):
                games.append({"appid": kv.get("appid", ""), "name": kv.get("name", kv["installdir"]), "dir": d})
    return sorted(games, key=lambda g: g["name"].lower())


def find_exes(game_dir):
    """Candidate game exes, best first (same rules as GeoGamingHub Library.FindExe)."""
    shipping = []
    for root, dirs, files in os.walk(game_dir):
        if re.search(r"[/\\]Binaries[/\\]Win(64|32)$", root):
            shipping += [os.path.join(root, f) for f in files if re.search(r"-Shipping\.exe$", f, re.I)]
    if shipping:
        return sorted(shipping, key=lambda p: ("Win64" not in p, p))
    out = []
    for sub in ["", "bin", "Bin", "x64", "bin64", "Binaries", "game", "Game", "Binaries/Win64", "bin/x64"]:
        d = os.path.join(game_dir, sub)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            p = os.path.join(d, f)
            if f.lower().endswith(".exe") and os.path.isfile(p) and not NOT_EXE.search(f):
                out.append(p)
    key = re.sub(r"[^a-z0-9]", "", os.path.basename(game_dir).lower())

    def score(p):
        n = re.sub(r"[^a-z0-9]", "", os.path.splitext(os.path.basename(p))[0].lower())
        return (-(n == key) * 3 - (n in key or key in n) * 2, -os.path.getsize(p))
    return sorted(dict.fromkeys(out), key=score)


# ---------- PE inspection ----------

def pe_info(path):
    """Returns (bits, set of imported dll names, lower case)."""
    try:
        with open(path, "rb") as f:
            data = f.read()
    except OSError:
        return None, set()
    try:
        if data[:2] != b"MZ":
            return None, set()
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0":
            return None, set()
        machine, nsec = struct.unpack_from("<HH", data, pe + 4)
        opt_size = struct.unpack_from("<H", data, pe + 20)[0]
        opt = pe + 24
        magic = struct.unpack_from("<H", data, opt)[0]
        bits = 64 if machine == 0x8664 else 32 if machine == 0x14C else None
        dd = opt + (112 if magic == 0x20B else 96)
        imp_rva = struct.unpack_from("<I", data, dd + 8)[0]
        secs = []
        for i in range(nsec):
            s = opt + opt_size + i * 40
            vsize, va, rsize, raw = struct.unpack_from("<IIII", data, s + 8)
            secs.append((va, max(vsize, rsize), raw))

        def off(rva):
            for va, size, raw in secs:
                if va <= rva < va + size:
                    return rva - va + raw
            return None
        names = set()
        o = off(imp_rva) if imp_rva else None
        while o is not None and o + 20 <= len(data):
            name_rva = struct.unpack_from("<I", data, o + 12)[0]
            if name_rva == 0:
                break
            no = off(name_rva)
            if no is not None:
                names.add(data[no:data.index(b"\0", no)].decode("latin-1").lower())
            o += 20
        if not names:
            low = data.lower()
            for dll in (b"d3d11.dll", b"dxgi.dll", b"d3d9.dll", b"d3d12.dll", b"vulkan-1.dll", b"opengl32.dll"):
                if dll in low or dll.decode().encode("utf-16-le") in low:
                    names.add(dll.decode())
        return bits, names
    except (struct.error, ValueError):
        return None, set()


def graphics_api(imports):
    if "d3d12.dll" in imports and "d3d11.dll" not in imports:
        return "dx12"
    if "d3d11.dll" in imports or "dxgi.dll" in imports:
        return "dx11"
    if "d3d9.dll" in imports:
        return "dx9"
    if "vulkan-1.dll" in imports:
        return "vulkan"
    if "opengl32.dll" in imports:
        return "opengl"
    return "unknown"


def is_ue4(exe):
    d = os.path.dirname(exe)
    if re.search(r"-Win(64|32)-Shipping\.exe$", exe, re.I):
        return True
    if re.search(r"[/\\]Binaries[/\\]Win(64|32)$", d):
        project = os.path.dirname(os.path.dirname(d))
        return os.path.isdir(os.path.join(project, "Content")) or os.path.isdir(os.path.join(os.path.dirname(project), "Engine"))
    return False


def unity_version(exe):
    d = os.path.dirname(exe)
    data = os.path.join(d, os.path.splitext(os.path.basename(exe))[0] + "_Data")
    if not os.path.isdir(data):
        return None
    for n in ("globalgamemanagers", "mainData", "data.unity3d"):
        try:
            with open(os.path.join(data, n), "rb") as f:
                m = re.search(rb"(\d{1,4}\.\d+\.\d+)[abfp]\d+", f.read(4096))
            if m:
                return m.group(1).decode()
        except OSError:
            pass
    player = os.path.join(d, "UnityPlayer.dll")
    try:
        with open(player, "rb") as f:
            m = re.search(rb"F\0i\0l\0e\0V\0e\0r\0s\0i\0o\0n\0\0\0[\0]*((?:\d\0)+\.\0(?:\d\0)+\.\0(?:\d\0)+)", f.read())
        if m:
            return m.group(1).decode("utf-16-le")
    except OSError:
        pass
    return "unknown"


def unity_code(version):
    m = re.match(r"(\d+)\.(\d+)", version or "")
    if not m:
        return "2019"
    major, minor = int(m.group(1)), int(m.group(2))
    if major >= 2019:
        return "2019"
    if major >= 2017:
        return "2017"
    if major == 5:
        return "56" if minor >= 6 else "55"
    return "2019"


# ---------- ini editing (byte-exact round trip, keeps CRLF) ----------

class Ini:
    def __init__(self, path):
        self.path = path
        with open(path, "rb") as f:
            raw = f.read().decode("latin-1")
        self.nl = "\r\n" if "\r\n" in raw else "\n"
        self.lines = raw.split(self.nl)

    def save(self):
        with open(self.path, "wb") as f:
            f.write(self.nl.join(self.lines).encode("latin-1"))

    def _section_range(self, section):
        start = None
        for i, line in enumerate(self.lines):
            if re.match(r"\s*\[", line):
                if start is not None:
                    return start, i
                if line.strip().lower() == "[%s]" % section.lower():
                    start = i
        return (start, len(self.lines)) if start is not None else (None, None)

    def _ensure_section(self, section):
        s, e = self._section_range(section)
        if s is None:
            self.lines += ["", "[%s]" % section]
            s, e = self._section_range(section)
        return s, e

    def set(self, name, value, section):
        active = re.compile(r"\s*%s\s*=" % re.escape(name), re.I)
        commented = re.compile(r"\s*;\s*%s\s*=[^:]*$" % re.escape(name), re.I)
        hits = [i for i, l in enumerate(self.lines) if active.match(l)]
        if hits:
            self.lines[hits[0]] = "%s = %s" % (name, value)
            for i in reversed(hits[1:]):
                del self.lines[i]
            return
        s, e = self._ensure_section(section)
        spots = [i for i in range(s + 1, e) if commented.match(self.lines[i])]
        if spots:
            self.lines[spots[-1]] = "%s = %s" % (name, value)
        else:
            self.lines.insert(s + 1, "%s = %s" % (name, value))

    def disable(self, name):
        active = re.compile(r"\s*%s\s*=" % re.escape(name), re.I)
        self.lines = [";" + l.lstrip() if active.match(l) else l for l in self.lines]

    def set_include(self, target, on):
        pat = re.compile(r"\s*;?\s*include\s*=\s*%s\s*$" % re.escape(target).replace(r"\\", r"[\\/]"), re.I)
        found = False
        for i, l in enumerate(self.lines):
            if pat.match(l):
                self.lines[i] = ("" if on else ";") + "include = " + target
                found = True
        if on and not found:
            s, _ = self._ensure_section("Include")
            self.lines.insert(s + 1, "include = " + target)


# ---------- install / uninstall ----------

def packages_dir(arg):
    here = os.path.dirname(os.path.realpath(__file__))
    for c in [arg, os.environ.get("GGH_PACKAGES"), os.path.join(here, "Packages"),
              os.path.join(here, "..", "Packages"), os.path.expanduser("~/GeoGamingHub/Packages")]:
        if c and os.path.isdir(os.path.join(c, "Geo-11_DX11")):
            return os.path.realpath(c)
    return None


def state_path(game_dir, *parts):
    return os.path.join(game_dir, STATE_DIR, *parts)


def load_manifest(game_dir):
    try:
        with open(state_path(game_dir, MANIFEST), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def installed_geo11(game_dir):
    return os.path.isfile(os.path.join(game_dir, "d3d11.dll")) and os.path.isfile(os.path.join(game_dir, "d3dxdm.ini"))


def launch_options(bits):
    nv = "nvapi64" if bits == 64 else "nvapi"
    return 'WINEDLLOVERRIDES="d3d11,%s,d3dcompiler_47=n,b" %%command%%' % nv


def inspect(exe):
    bits, imports = pe_info(exe)
    info = {"exe": exe, "bits": bits, "api": graphics_api(imports), "engine": "generic", "unity": None}
    if is_ue4(exe):
        info["engine"] = "ue4"
        info["bits"] = 64
    else:
        uv = unity_version(exe)
        if uv:
            info["engine"] = "unity"
            info["unity"] = uv
    return info


def install(exe, pkgs, mode="sbs", upscale=True, separation=None, convergence=None, log=print):
    info = inspect(exe)
    game_dir = os.path.dirname(exe)
    if info["engine"] == "ue4":
        raise SystemExit(T("UE4 게임은 아직 지원하지 않습니다 (UE4 전용 패키지가 필요).",
                           "UE4 games are not supported yet (they need the UE4 package)."))
    if info["api"] not in ("dx11", "unknown"):
        raise SystemExit(T("geo-11은 DX11 게임용입니다. 이 게임은 %s 입니다.", "geo-11 is for DX11 games. This game is %s.")
                         % info["api"].upper())
    if info["bits"] not in (32, 64):
        raise SystemExit(T("exe의 32/64비트를 알 수 없습니다: ", "Cannot tell whether the exe is 32 or 64 bit: ") + exe)
    pkg = os.path.join(pkgs, "Geo-11_Unity" if info["engine"] == "unity" else "Geo-11_DX11",
                       "x64" if info["bits"] == 64 else "x32")
    if not os.path.isdir(pkg):
        raise SystemExit(T("패키지 폴더가 없습니다: ", "Package folder missing: ") + pkg)

    old = load_manifest(game_dir)
    if old:
        log(T("이미 설치되어 있어 먼저 제거 후 다시 설치합니다.", "Already installed: removing it first."))
        uninstall(game_dir, log=lambda *_: None)

    backup_root = state_path(game_dir, "backup")
    manifest = {"tool": VERSION, "time": time.strftime("%Y-%m-%d %H:%M:%S"), "exe": os.path.basename(exe),
                "package": pkg, "mode": mode, "created": [], "backed_up": [], "dirs": [],
                "preexisting_artifacts": [a for a in RUNTIME_ARTIFACTS if os.path.exists(os.path.join(game_dir, a))]}
    os.makedirs(backup_root, exist_ok=True)

    for root, dirs, files in os.walk(pkg):
        rel_root = os.path.relpath(root, pkg)
        rel_root = "" if rel_root == "." else rel_root
        dirs[:] = [d for d in dirs if os.path.join(rel_root, d).lower() not in SKIP_DIRS]
        for d in dirs:
            target = os.path.join(game_dir, rel_root, d)
            if not os.path.isdir(target):
                os.makedirs(target)
                manifest["dirs"].append(os.path.join(rel_root, d))
        for f in files:
            if not rel_root and (f.lower() in SKIP_TOP or f.lower().endswith((".bat", ".ps1"))):
                continue
            rel = os.path.join(rel_root, f)
            dst = os.path.join(game_dir, rel)
            if os.path.exists(dst):
                b = os.path.join(backup_root, rel)
                os.makedirs(os.path.dirname(b), exist_ok=True)
                shutil.copy2(dst, b)
                manifest["backed_up"].append(rel)
            else:
                manifest["created"].append(rel)
            if os.path.exists(dst):
                os.chmod(dst, 0o644)
            shutil.copy2(os.path.join(root, f), dst)

    bump_stale_bins(game_dir)

    dm = Ini(os.path.join(game_dir, "d3dxdm.ini"))
    dm.set("direct_mode", mode, "Device")
    if separation is not None:
        dm.set("dm_separation", "%g" % separation, "Stereo")
    if convergence is not None:
        dm.set("dm_convergence", "%g" % convergence, "Stereo")
    dm.save()

    dx = Ini(os.path.join(game_dir, "d3dx.ini"))
    if upscale:
        dx.set("upscaling", "1", "Device")
        dx.set("upscale_mode", "1", "Device")
        dx.disable("width")
        dx.disable("height")
        dx.set_include(r"ShaderFixes\upscale.ini", True)
    else:
        dx.set("upscaling", "0", "Device")
        dx.set_include(r"ShaderFixes\upscale.ini", False)
    if info["engine"] == "unity":
        code = unity_code(info["unity"])
        for c, inc in UNITY_INCLUDES.items():
            dx.set_include(inc, c == code)
        manifest["unity"] = {"version": info["unity"], "include": UNITY_INCLUDES[code]}
    dx.save()

    for cache in ("ShaderCache", "ShaderCacheDM"):
        if cache not in manifest["preexisting_artifacts"]:
            shutil.rmtree(os.path.join(game_dir, cache), ignore_errors=True)

    with open(state_path(game_dir, MANIFEST), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1, ensure_ascii=False)
    return info, manifest


def bump_stale_bins(game_dir):
    sf = os.path.join(game_dir, "ShaderFixes")
    if not os.path.isdir(sf):
        return
    now = time.time()
    for f in os.listdir(sf):
        if not f.lower().endswith(".bin"):
            continue
        stem = os.path.join(sf, f[:-4])
        bin_t = os.path.getmtime(os.path.join(sf, f))
        if any(os.path.exists(stem + ext) and os.path.getmtime(stem + ext) > bin_t for ext in (".txt", ".hlsl")):
            os.utime(os.path.join(sf, f), (now, now))


def uninstall(game_dir, log=print):
    m = load_manifest(game_dir)
    if not m:
        raise SystemExit(T("이 도구로 설치한 기록이 없습니다: ", "Nothing installed by this tool in: ") + game_dir)
    for rel in m["created"]:
        try:
            os.remove(os.path.join(game_dir, rel))
        except FileNotFoundError:
            pass
    backup_root = state_path(game_dir, "backup")
    for rel in m["backed_up"]:
        src = os.path.join(backup_root, rel)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(game_dir, rel))
    for a in RUNTIME_ARTIFACTS:
        if a in m.get("preexisting_artifacts", []):
            continue
        p = os.path.join(game_dir, a)
        if os.path.isdir(p):
            shutil.rmtree(p, ignore_errors=True)
        elif os.path.exists(p):
            os.remove(p)
    for rel in sorted(m["dirs"], key=len, reverse=True):
        p = os.path.join(game_dir, rel)
        if os.path.isdir(p) and not os.listdir(p):
            os.rmdir(p)
    shutil.rmtree(state_path(game_dir), ignore_errors=True)
    log(T("제거했습니다: ", "Removed from: ") + game_dir)


# ---------- user interface ----------

def copy_to_clipboard(text):
    for cmd in (["wl-copy"], ["xclip", "-selection", "clipboard"], ["xsel", "--clipboard", "--input"]):
        if shutil.which(cmd[0]):
            try:
                subprocess.run(cmd, input=text.encode(), check=True, timeout=5)
                return True
            except (subprocess.SubprocessError, OSError):
                pass
    return False


def report_install(info, manifest):
    game_dir = os.path.dirname(info["exe"])
    opts = launch_options(info["bits"])
    print()
    print(T("geo-11 설치 완료", "geo-11 installed") + ": " + game_dir)
    print("  exe: %s (%d-bit, %s%s)" % (os.path.basename(info["exe"]), info["bits"], info["api"].upper(),
                                        ", Unity " + info["unity"] if info["unity"] else ""))
    print("  direct_mode = %s, upscaling = %s" % (manifest["mode"], T("켜짐", "on")))
    print()
    print(T("1) Steam > 게임 속성 > 실행 옵션에 다음을 넣으세요:", "1) Put this in Steam > game Properties > Launch Options:"))
    print("   " + opts)
    if copy_to_clipboard(opts):
        print(T("   (클립보드에 복사했습니다)", "   (copied to the clipboard)"))
    print(T("2) DroidDeck: Cardboard VR > 화면 \"사이드 바이 사이드 3D\", 뷰어 \"렌즈 없음\"",
            "2) DroidDeck: Cardboard VR > Picture \"Side-by-side 3D\", Viewer \"No lens\""))
    print(T("3) 게임이 시작할 때 멈추면 게임 안에서 창 모드 전체화면(windowed fullscreen)으로 바꿔 보세요.",
            "3) If the game freezes at start with upscaling, switch it to windowed fullscreen in its settings."))
    print(T("   단축키: Ctrl+T 3D 켜기/끄기, Ctrl+F3/F4 깊이, Ctrl+F5/F6 수렴, Ctrl+F7 저장",
            "   Keys: Ctrl+T toggle 3D, Ctrl+F3/F4 depth, Ctrl+F5/F6 convergence, Ctrl+F7 save"))


def ask(prompt, default=""):
    try:
        v = input(prompt).strip()
    except EOFError:
        sys.exit(0)
    return v or default


def pick_exe(game_dir):
    exes = find_exes(game_dir)
    if not exes:
        print(T("exe를 찾지 못했습니다.", "No exe found."))
        return None
    if len(exes) == 1:
        return exes[0]
    for i, e in enumerate(exes[:15], 1):
        print("  %2d) %s" % (i, os.path.relpath(e, game_dir)))
    v = ask(T("exe 번호 [1]: ", "exe number [1]: "), "1")
    return exes[int(v) - 1] if v.isdigit() and 1 <= int(v) <= min(15, len(exes)) else None


def interactive(args, pkgs):
    while True:
        games = steam_games(steam_roots(args.steam_root))
        print()
        print("GeoGamingHub for Linux %s  (geo-11 DX11 -> SBS)" % VERSION)
        print(T("패키지: ", "Packages: ") + pkgs)
        if not games:
            print(T("Steam 게임을 찾지 못했습니다. exe 경로를 직접 입력할 수 있습니다.",
                    "No Steam games found. You can type an exe path instead."))
        for i, g in enumerate(games, 1):
            mark = "[geo-11] " if installed_geo11_any(g["dir"]) else ""
            print("  %3d) %s%s" % (i, mark, g["name"]))
        v = ask(T("\n게임 번호, exe 경로, 또는 q(종료): ", "\nGame number, exe path, or q to quit: "))
        if v.lower() in ("q", "quit", "exit", ""):
            return
        if v.isdigit() and 1 <= int(v) <= len(games):
            exe = pick_exe(games[int(v) - 1]["dir"])
        elif os.path.isfile(v):
            exe = v
        else:
            print(T("잘못된 입력입니다.", "Not understood."))
            continue
        if not exe:
            continue
        game_menu(exe, pkgs)


def installed_geo11_any(game_dir):
    for root, dirs, files in os.walk(game_dir):
        if STATE_DIR in dirs:
            return True
        if root.count(os.sep) - game_dir.count(os.sep) > 4:
            dirs[:] = []
    return False


def game_menu(exe, pkgs):
    game_dir = os.path.dirname(exe)
    info = inspect(exe)
    print()
    print(exe)
    print("  %s-bit, %s, %s%s" % (info["bits"] or "?", info["api"].upper(), info["engine"],
                                  " " + info["unity"] if info["unity"] else ""))
    ours = load_manifest(game_dir)
    if ours:
        print(T("  이 도구로 설치됨 (%s, %s)", "  Installed by this tool (%s, %s)") % (ours["time"], ours["mode"]))
    elif installed_geo11(game_dir):
        print(T("  다른 방법으로 설치된 geo-11이 있습니다. 설치하면 백업 후 덮어씁니다.",
                "  geo-11 from somewhere else is present. Installing backs it up and overwrites it."))
    print(T("  1) 설치 (SBS)   2) 출력 방식 골라서 설치   3) 제거   4) 실행 옵션 보기   0) 뒤로",
            "  1) Install (SBS)   2) Install with output choice   3) Remove   4) Show launch options   0) Back"))
    v = ask("> ", "0")
    try:
        if v == "1":
            report_install(*install(exe, pkgs))
        elif v == "2":
            for i, m in enumerate(MODES, 1):
                print("  %d) %s" % (i, m))
            c = ask("[1]: ", "1")
            mode = MODES[int(c) - 1] if c.isdigit() and 1 <= int(c) <= len(MODES) else "sbs"
            report_install(*install(exe, pkgs, mode=mode))
        elif v == "3":
            uninstall(game_dir)
        elif v == "4":
            print("  " + launch_options(info["bits"] or 64))
    except SystemExit as e:
        print("!! %s" % e)


def resolve_target(target, args):
    if os.path.isfile(target):
        return target
    games = steam_games(steam_roots(args.steam_root))
    for g in games:
        if g["appid"] == target or g["name"].lower() == target.lower():
            exes = find_exes(g["dir"])
            if exes:
                return exes[0]
    raise SystemExit(T("게임을 찾지 못했습니다: ", "Game not found: ") + target)


def main():
    ap = argparse.ArgumentParser(description="Install geo-11 (DX11 stereo 3D) into Steam/Proton games.")
    ap.add_argument("--packages", help="GeoGamingHub Packages folder")
    ap.add_argument("--steam-root", action="append", default=[], help="extra Steam library root (repeatable)")
    ap.add_argument("--list", action="store_true", help="list Steam games and their geo-11 state")
    ap.add_argument("--install", metavar="EXE|APPID|NAME")
    ap.add_argument("--uninstall", metavar="EXE|APPID|NAME")
    ap.add_argument("--mode", choices=MODES, default="sbs")
    ap.add_argument("--no-upscale", action="store_true")
    ap.add_argument("--separation", type=float)
    ap.add_argument("--convergence", type=float)
    args = ap.parse_args()

    pkgs = packages_dir(args.packages)
    if args.uninstall:
        uninstall(os.path.dirname(resolve_target(args.uninstall, args)))
        return
    if args.list:
        for g in steam_games(steam_roots(args.steam_root)):
            exes = find_exes(g["dir"])
            info = inspect(exes[0]) if exes else None
            state = "geo-11" if installed_geo11_any(g["dir"]) else "-"
            print("%-8s %-7s %-6s %s" % (g["appid"], state, info["api"] if info else "?", g["name"]))
        return
    if not pkgs:
        raise SystemExit(T("GeoGamingHub의 Packages 폴더를 찾지 못했습니다. 이 스크립트를 GeoGamingHub 폴더 안에 두거나 --packages로 지정하세요.",
                           "GeoGamingHub's Packages folder was not found. Put this script in the GeoGamingHub folder or pass --packages."))
    if args.install:
        report_install(*install(resolve_target(args.install, args), pkgs, args.mode, not args.no_upscale,
                                args.separation, args.convergence))
    else:
        interactive(args, pkgs)


if __name__ == "__main__":
    main()
