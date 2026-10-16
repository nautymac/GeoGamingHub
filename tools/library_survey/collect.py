# Stage 1: owned library (Steam + Heroic) -> Steam appids -> Steam tags with weights.
# Caches every network answer in cache_*.json so reruns are cheap.
import json, os, re, sys, time, urllib.parse, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
HEROIC = os.path.join(os.environ.get('APPDATA', ''), 'heroic', 'store_cache')   # Windows only (the survey); the module also runs on Linux in GitHub Actions
STEAM = 'E:/Steam'

def jload(p, default=None):
    try:
        return json.load(open(p, encoding='utf-8'))
    except FileNotFoundError:
        return default

def jsave(p, obj):
    tmp = p + '.tmp'
    json.dump(obj, open(tmp, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    os.replace(tmp, p)

def http_json(url, tries=6):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': 'GeoGamingHub-library-survey/0.1'})
            with urllib.request.urlopen(req, timeout=40) as r:
                return json.loads(r.read().decode('utf-8'))
        except urllib.error.HTTPError as e:
            if e.code in (429, 502, 503):
                time.sleep(10 * (i + 1)); continue
            raise
        except Exception:
            time.sleep(3 * (i + 1))
    raise RuntimeError('failed: ' + url)

SUFFIXES = [
    'game of the year edition', 'goty edition', 'goty', 'definitive edition', 'enhanced edition',
    'complete edition', 'special edition', 'deluxe edition', 'standard edition', 'directors cut',
    'remastered', 'anniversary edition', 'collectors edition', 'ultimate edition', 'gold edition',
    'digital deluxe edition', 'edition',
]

def norm(t):
    t = t.lower().replace('&', ' and ')
    t = re.sub(r'[\u2122\u00ae\u00a9]', '', t)
    t = re.sub(r"['\u2019`]", '', t)
    t = re.sub(r'[^0-9a-z]+', ' ', t).strip()
    t = re.sub(r'^the ', '', t)
    return t

def norm_loose(t):
    t = norm(t)
    changed = True
    while changed:
        changed = False
        for s in SUFFIXES:
            if t.endswith(' ' + s):
                t = t[: -len(s) - 1].strip(); changed = True
    return t

def library():
    sys.path.insert(0, HERE)
    import appinfo
    _, apps = appinfo.parse(os.path.join(STEAM, 'appcache', 'appinfo.vdf'))
    lc = {m.group() for m in (re.match(r'\d+', x) for x in os.listdir(os.path.join(STEAM, 'appcache', 'librarycache'))) if m}
    installed = set()
    for f in os.listdir(os.path.join(STEAM, 'steamapps')):
        m = re.match(r'appmanifest_(\d+)\.acf', f)
        if m: installed.add(int(m.group(1)))
    items = []
    for appid, v in apps.items():
        c = v.get('common', {})
        if c.get('type', '').lower() != 'game' or str(appid) not in lc:
            continue
        items.append({
            'store': 'steam', 'id': str(appid), 'title': c.get('name', ''), 'steam_appid': appid,
            'developer': v.get('extended', {}).get('developer', ''), 'installed': appid in installed,
            'launch': [x.get('executable', '') for x in (v.get('config', {}).get('launch') or {}).values()
                       if 'windows' in str((x.get('config') or {}).get('oslist', 'windows'))],
        })
    gog_api = jload(os.path.join(HEROIC, 'gog_api_info.json'), {})
    def steam_from_gamesdb(key):
        g = (gog_api.get(key) or {}).get('game') or {}
        for r in g.get('releases', []):
            if r.get('platform_id') == 'steam':
                try: return int(r['external_id'])
                except ValueError: pass
        return None
    for x in jload(os.path.join(HEROIC, 'legendary_library.json'))['library']:
        if (x.get('install') or {}).get('is_dlc'): continue
        items.append({'store': 'epic', 'id': x['app_name'], 'title': x.get('title', ''), 'developer': x.get('developer', ''),
                      'installed': bool(x.get('is_installed')), 'steam_appid': steam_from_gamesdb('epic_' + x['app_name'])})
    for x in jload(os.path.join(HEROIC, 'gog_library.json'))['games']:
        if (x.get('install') or {}).get('is_dlc') or x.get('app_name') == 'gog-redist': continue
        items.append({'store': 'gog', 'id': x['app_name'], 'title': x.get('title', ''), 'developer': x.get('developer', ''),
                      'installed': bool(x.get('is_installed')), 'genres': (x.get('extra') or {}).get('genres') or [],
                      'steam_appid': steam_from_gamesdb('gog_' + x['app_name'])})
    for x in jload(os.path.join(HEROIC, 'nile_library.json'))['library']:
        items.append({'store': 'amazon', 'id': x['app_name'], 'title': x.get('title', ''), 'developer': x.get('developer', ''),
                      'installed': bool(x.get('is_installed')), 'genres': (x.get('extra') or {}).get('genres') or [],
                      'steam_appid': None})
    return items

def map_steam(items):
    cache_p = os.path.join(HERE, 'cache_search.json')
    cache = jload(cache_p, {})
    by_name = {}
    for it in items:
        if it['store'] == 'steam':
            by_name.setdefault(norm_loose(it['title']), it['steam_appid'])
    n = 0
    for it in items:
        if it['steam_appid'] or not it['title']:
            continue
        key = norm_loose(it['title'])
        if key in by_name:
            it['steam_appid'] = by_name[key]; it['steam_match'] = 'owned-title'; continue
        if it['title'] not in cache:
            url = 'https://store.steampowered.com/api/storesearch/?' + urllib.parse.urlencode({'term': it['title'], 'l': 'english', 'cc': 'US'})
            try:
                cache[it['title']] = http_json(url).get('items', [])
            except Exception as e:
                print('search failed', it['title'], e); continue
            n += 1
            if n % 25 == 0:
                jsave(cache_p, cache); print('searched', n, flush=True)
            time.sleep(0.4)
        for r in cache[it['title']]:
            if r.get('type') == 'app' and (norm(r['name']) == norm(it['title']) or norm_loose(r['name']) == key):
                it['steam_appid'] = r['id']; it['steam_match'] = 'search'; break
    jsave(cache_p, cache)

def fetch_tags(items):
    cache_p = os.path.join(HERE, 'cache_tags.json')
    cache = jload(cache_p, {})
    ids = sorted({it['steam_appid'] for it in items if it['steam_appid'] and str(it['steam_appid']) not in cache})
    for i in range(0, len(ids), 50):
        chunk = ids[i:i + 50]
        q = {'ids': [{'appid': a} for a in chunk], 'context': {'language': 'english', 'country_code': 'US'},
             'data_request': {'include_tag_count': 20, 'include_basic_info': True, 'include_release': True}}
        url = 'https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?' + urllib.parse.urlencode({'input_json': json.dumps(q)})
        resp = http_json(url).get('response', {}).get('store_items', [])
        got = set()
        for s in resp:
            a = s.get('appid') or s.get('id')
            got.add(a)
            cache[str(a)] = {'name': s.get('name'), 'tags': [[t['tagid'], t.get('weight', 0)] for t in s.get('tags', [])],
                             'type': s.get('type'), 'success': s.get('success'),
                             'developers': [d.get('name') for d in (s.get('basic_info') or {}).get('developers', [])]}
        for a in chunk:
            if a not in got: cache[str(a)] = {'success': 0, 'tags': []}
        jsave(cache_p, cache); print('tags', i + len(chunk), '/', len(ids), flush=True)
        time.sleep(0.5)
    return cache

if __name__ == '__main__':
    items = library()
    print('library', len(items), {s: sum(1 for i in items if i['store'] == s) for s in ('steam', 'epic', 'gog', 'amazon')})
    map_steam(items)
    print('mapped', sum(1 for i in items if i['steam_appid']), '/', len(items))
    fetch_tags(items)
    jsave(os.path.join(HERE, 'library.json'), items)
