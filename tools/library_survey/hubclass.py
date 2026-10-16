# The Geo Gaming Hub classification of one PCGamingWiki page: engine and graphics API -> 3D method.
# Shared by tools/library_survey/classify.py (the owned-library survey) and .github/scripts/community_list.py
# (games people report or suggest with a PCGamingWiki link), so both lists are made the same way.
#
# Methods, in order of preference when a game offers several:
#   unity  geo-11 Unity Universal Fix (Unity, Direct3D 11)
#   ue4    geo-11 Unreal Engine 4 Universal Fix 2 (UE4, Direct3D 11)
#   dx11   geo-11 generic (Direct3D 10 / 11)
#   dx9    dgVoodoo2 + geo-11 (Direct3D 9)
#   opengl OGL3D
#   vulkan VK3D (64-bit)
#   dx12   not supported yet (Direct3D 12 only)
#   old    not in the hub yet (Direct3D 8 and older, DirectDraw, Glide)
#   none   no 3D route (browser engines, emulators / DOSBox / ScummVM, software rendering)
#   unknown  no API information
import re
from pcgw import ENGINE_API

BROWSER = ('rpg maker mv', 'rpg maker mz', 'nw.js', 'electron', 'construct', 'chromium', 'html5', 'cocos creator')
ONEIMAGE = ('dosbox', 'scummvm', 'emulat', 'retroarch', 'mame', 'snes9x', 'zsnes', 'mednafen')

def yes(v):
    return bool(v) and v.lower() not in ('false', 'unknown', 'n/a', 'no', 'hackable')

def classify(pc, title):
    """-> (methods, note, engines, api text, bits)"""
    if not pc:
        return ['unknown'], 'PCGamingWiki 정보 없음', [], '', ''
    eng = ' '.join(pc['engines']).lower()
    d3d = sorted({int(m.group(1)) for v in pc['d3d'] for m in [re.match(r'(\d+)', v)] if m})
    gl, vk = yes(pc['opengl']), yes(pc['vulkan'])
    gnote = pc.get('opengl_notes', '')
    if gl and ((re.search(r'os x|macos|mac\b|linux', gnote, re.I) and not re.search(r'windows', gnote, re.I))
               or re.search(r'prior to|before v|older version|previous version|removed', gnote, re.I)):
        gl = False   # OpenGL only on Mac / Linux, or only in old builds
    old = yes(pc['ddraw']) or yes(pc['glide']) or any(v <= 8 for v in d3d)
    x64, x86 = yes(pc['x64']), yes(pc['x86'])
    bits = 'x86/x64' if x64 and x86 else 'x64' if x64 else 'x86' if x86 else ''
    note, est = [], False
    if not (d3d or gl or vk or old):
        for key, api in ENGINE_API:
            if key in eng:
                if api == 'opengl': gl = True
                elif api == 'd3d11': d3d = [11]
                elif api == 'd3d9': d3d = [9]
                est = True
                break
    api = []
    if d3d: api.append('D3D ' + '/'.join(str(v) for v in d3d))
    if gl: api.append('OpenGL')
    if vk: api.append('Vulkan')
    if yes(pc['ddraw']): api.append('DirectDraw')
    if yes(pc['glide']): api.append('Glide')
    apitext = ', '.join(api) + (' (엔진 기본값 추정)' if est else '')
    if any(b in eng for b in BROWSER) and 'unity' not in eng:
        return ['none'], '브라우저 엔진(NW.js / Electron 등)', pc['engines'], apitext, bits
    if any(o in eng for o in ONEIMAGE) or re.search(r'dosbox', title.lower()):
        return ['none'], '완성 화면 한 장만 냄(에뮬레이터 / DOSBox / ScummVM)', pc['engines'], apitext, bits
    if yes(pc['software']) and not (gl or d3d or vk):
        return ['none'], '소프트웨어 렌더링', pc['engines'], apitext, bits
    m = []
    dx11 = any(v in (10, 11) for v in d3d)
    ue = re.search(r'unreal engine (\d)', eng)
    if 'unity' in eng and (dx11 or not d3d): m.append('unity')
    elif ue and ue.group(1) == '4' and (dx11 or not d3d): m.append('ue4')
    if dx11 and not m: m.append('dx11')
    if ue and ue.group(1) == '5': note.append('Unreal Engine 5: UF2(UE4용) 대상 아님')
    if 9 in d3d: m.append('dx9')
    if gl: m.append('opengl')
    if vk: m.append('vulkan' if x64 or not bits else 'vulkan32')
    if 12 in d3d and not m: m.append('dx12')
    if old and not m: m.append('old')
    if 'vulkan32' in m:
        m.remove('vulkan32'); note.append('Vulkan 32비트: VK3D는 64비트만')
    if not m: m = ['unknown']
    if 'opengl' in m and m[0] != 'opengl': note.append('OpenGL 렌더러로 바꾸면 OGL3D도 가능')
    if 'vulkan' in m and m[0] != 'vulkan': note.append('Vulkan 렌더러로 바꾸면 VK3D도 가능')
    return m, '; '.join(note), pc['engines'], apitext, bits
