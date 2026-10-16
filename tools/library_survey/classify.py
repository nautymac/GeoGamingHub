# Stage 2: every owned game -> PCGamingWiki (engine, graphics API, bitness) -> the Geo Gaming Hub 3D method.
# Writes ../../data/game_candidates.json (public: no store / ownership data) and
# ../../data/private/game_candidates_full.json (with stores and the installed flag). The method rules are in
# hubclass.py (shared with the community list).
#
#   python -I collect.py     # owned library (Steam + Heroic) -> library.json   (from Flato3D-SR)
#   python -I classify.py    # -> data files above
#
import json, os, re, sys, time
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from collect import jload, jsave, norm, norm_loose
from pcgw import pcgw_pages, pcgw_by_appid, pcgw_search, parse_pcgw
from hubclass import classify   # shared with .github/scripts/community_list.py

ROOT = os.path.dirname(os.path.dirname(HERE))
FLATO = r'D:\ClaudeCode\Projects\Flato3D\data\private\game_candidates_full.json'   # Flato3D-SR's 2D survey (optional)
FLATO_PROFILES = r'D:\ClaudeCode\Projects\Flato3D\data\game_profiles.json'          # games Flato3D-SR set up
# Which program a game belongs to: 'hub' (geometric stereo: the game has real depth), 'flato3d' (flat 2D layers that
# need Flato3D-SR's per-layer depth), 'none' (one finished image: emulators, DOSBox; only AI 2D->3D)

# Results of this project's own tests (and the geo-11 SR community list): status + note, by Steam appid or title.
CHECKED = {
    1089130: ('verified', 'vulkan', 'VK3D 전용 픽스(양쪽 눈을 한 프레임에), fps 손실 없음'),
    540840: ('verified', 'vulkan', 'VK3D + DXVK(실험적): Unity DX9 게임이 geo-11로 안 될 때'),
    424840: ('checked', 'ue4', 'VK3D + DXVK로 3D는 되지만 화면 효과가 어긋남 → geo-11(UF2) 권장'),
    1123770: ('verified', 'opengl', 'OGL3D 검증'),
    57300: ('verified', 'opengl', 'OGL3D 검증(32비트, 업스케일)'),
    232790: ('verified', 'opengl', 'OGL3D 검증: 2.5D 원근 레이어, 자동 수렴'),
    262060: ('verified', 'opengl', 'OGL3D 검증'),
    619700: ('verified', 'ue4', 'geo-11 UF2 검증(-dx11)'),
    1549690: ('verified', 'ue4', 'geo-11 UF2 검증(-dx11)'),
    742300: ('verified', 'dx11', 'geo-11 일반 검증'),
    667720: ('verified', 'dx11', 'geo-11 일반 검증(32비트, sRGB)'),
    606730: ('verified', 'dx9', 'dgVoodoo2 + geo-11 검증(VRAM 4GB, 4GB 패치)'),
    'art of rally': ('verified', 'unity', 'geo-11 Unity 검증'),
}

def main():
    lib = jload(os.path.join(HERE, 'library.json'))
    cache_p = os.path.join(HERE, 'cache_pcgw.json')
    cache = jload(cache_p, {})
    # one entry per game: Steam appid when known, else the normalized title
    games = {}
    for it in lib:
        key = ('app', it['steam_appid']) if it.get('steam_appid') else ('t', norm_loose(it['title']))
        g = games.setdefault(key, {'title': it['title'], 'steam_appid': it.get('steam_appid'), 'stores': [], 'installed': False,
                                   'developer': it.get('developer', '')})
        if it['store'] == 'steam' or not g['title']: g['title'] = it['title']
        g['stores'].append({'store': it['store'], 'id': it['id']})
        g['installed'] = g['installed'] or bool(it.get('installed'))
    print('games', len(games), flush=True)
    titles = [g['title'] for g in games.values()]
    try:
        pcgw_pages(titles, cache)
    finally:
        jsave(cache_p, cache)
    n = 0
    for g in games.values():
        pc = cache.get(g['title'])
        if not pc and g['steam_appid']:
            pc = pcgw_by_appid(g['steam_appid'], cache); n += 1
        if not pc:
            pc = pcgw_search(g['title'], cache); n += 1
        if n >= 40: jsave(cache_p, cache); print('lookups +40', flush=True); n = 0
        g['_pc'] = pc
    jsave(cache_p, cache)

    flato = {}
    fl = jload(FLATO, {'games': []})
    for c in fl['games']:   # kind 1 = 2.5D / 3D-rendered (plain stereo works), 2 / 3 = flat layers, 4 = one finished image
        flato[c.get('steam_appid') or norm_loose(c['title'])] = c
    fprof = {}
    for pr in jload(FLATO_PROFILES, {'games': []})['games']:
        fprof[pr.get('steam_appid') or norm_loose(pr.get('name', ''))] = pr

    out = []
    for g in games.values():
        pc = parse_pcgw(g['_pc']['text']) if g['_pc'] else None
        methods, note, engines, api, bits = classify(pc, g['title'])
        status = 'untested'
        ck = CHECKED.get(g['steam_appid']) or CHECKED.get(g['title'].lower())
        if ck:
            status, best, cnote = ck
            if best in methods: methods.remove(best)
            methods.insert(0, best)
            # the "switch the renderer" hint of the tested method itself is no news
            hint = {'vulkan': 'Vulkan 렌더러로 바꾸면 VK3D도 가능', 'opengl': 'OpenGL 렌더러로 바꾸면 OGL3D도 가능'}.get(best)
            note = '; '.join(x for x in note.split('; ') if x and x != hint)
            note = cnote + ('; ' + note if note else '')
        f = flato.get(g['steam_appid']) or flato.get(norm_loose(g['title']))
        fp = fprof.get(g['steam_appid']) or fprof.get(norm_loose(g['title']))
        kind = f.get('kind') if f else None
        product = 'none' if methods[0] == 'none' else 'hub'
        froute, fstatus = '', ''
        if fp or kind in (2, 3):
            product = 'flato3d'
            froute = (fp or {}).get('route') or (f or {}).get('api', '')
            fstatus = (fp or {}).get('status') or (f or {}).get('status', 'untested')
            note = '평면 2D 레이어: Geo Gaming Hub로는 깊이가 거의 없음' + ('; ' + note if note else '')
        elif kind == 4:
            product = 'none'
            note = '완성 화면 한 장(에뮬레이터 등): AI 2D→3D만 가능' + ('; ' + note if note else '')
        elif kind == 1:
            note = '2.5D / 3D 렌더링: 일반 입체로 됨' + ('; ' + note if note else '')
        if ck and product == 'flato3d': product = 'hub'   # tested with the hub (Broken Age, Darkest Dungeon are 2.5D)
        e = {'title': g['title'], 'steam_appid': g['steam_appid'], 'product': product, 'method': methods[0], 'alts': methods[1:],
             'engine': engines, 'api': api, 'bits': bits, 'status': status, 'note': note,
             'flat2d': kind in (2, 3, 4), 'flato_route': froute, 'flato_status': fstatus,
             'pcgw_page': g['_pc']['page'] if g['_pc'] else ''}
        e['_stores'], e['_installed'] = g['stores'], g['installed']
        out.append(e)
    out.sort(key=lambda e: norm(e['title']))
    import collections
    print('methods', collections.Counter(e['method'] for e in out).most_common(), flush=True)
    print('products', collections.Counter(e['product'] for e in out).most_common(), flush=True)
    stamp = time.strftime('%Y-%m-%d')
    src = '소장 Steam·Epic·GOG·Amazon 라이브러리 %d개(%s). 엔진·그래픽 API: PCGamingWiki (CC BY-NC-SA 3.0). 평면 2D 판정: Flato3D-SR 조사. 시험 결과: 이 프로젝트, geo-11 SR, Flato3D-SR.' % (len(out), stamp)
    pub = [{k: v for k, v in e.items() if not k.startswith('_')} for e in out]
    full = [dict({k: v for k, v in e.items() if not k.startswith('_')}, stores=e['_stores'], installed=e['_installed']) for e in out]
    os.makedirs(os.path.join(ROOT, 'data', 'private'), exist_ok=True)
    jsave(os.path.join(ROOT, 'data', 'game_candidates.json'), {'schema': 1, 'generated': stamp, 'source': src, 'games': pub})
    jsave(os.path.join(ROOT, 'data', 'private', 'game_candidates_full.json'), {'schema': 1, 'generated': stamp, 'source': src, 'games': full})
    print('wrote', len(out))

if __name__ == '__main__':
    main()
