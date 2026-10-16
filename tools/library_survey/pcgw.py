# PCGamingWiki access and wikitext parsing (from Flato3D-SR tools/library_survey/classify.py) and the engine
# defaults for games whose page has no graphics API.
import json, re, time, urllib.parse, urllib.request
from collect import norm_loose

UA = {'User-Agent': 'Mozilla/5.0 GeoGamingHub-library-survey/0.1'}

# ---------------------------------------------------------------- PCGamingWiki
def http(url, raw=False, tries=6, follow=True):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers=UA)
            if not follow:
                class NoRedir(urllib.request.HTTPRedirectHandler):
                    def redirect_request(self, *a, **k): return None
                op = urllib.request.build_opener(NoRedir)
                try:
                    r = op.open(req, timeout=40); return r.geturl()
                except urllib.error.HTTPError as e:
                    if e.code in (301, 302, 303): return e.headers.get('Location')
                    if e.code == 404: return None
                    raise
            with urllib.request.urlopen(req, timeout=60) as r:
                b = r.read().decode('utf-8')
                return b if raw else json.loads(b)
        except urllib.error.HTTPError as e:
            if e.code in (429, 502, 503, 504): time.sleep(15 * (i + 1)); continue
            if e.code == 404: return None
            raise
        except Exception as e:
            time.sleep(4 * (i + 1))
    raise RuntimeError('failed ' + url)

API = 'https://www.pcgamingwiki.com/w/api.php?'

def pcgw_pages(titles, cache):
    """Fetch wikitext for titles (50 per request). cache: title -> {'page': name, 'text': ...} or None."""
    todo = [t for t in dict.fromkeys(titles) if t and t not in cache]
    for i in range(0, len(todo), 50):
        chunk = todo[i:i + 50]
        q = {'action': 'query', 'prop': 'revisions', 'rvprop': 'content', 'rvslots': 'main', 'redirects': 1,
             'format': 'json', 'titles': '|'.join(chunk)}
        d = http(API + urllib.parse.urlencode(q))
        qd = d.get('query', {})
        alias = {}
        for n in qd.get('normalized', []): alias[n['from']] = n['to']
        red = {r['from']: r['to'] for r in qd.get('redirects', [])}
        pages = {p['title']: p for p in qd.get('pages', {}).values()}
        for t in chunk:
            name = alias.get(t, t); name = red.get(name, name)
            p = pages.get(name)
            if p and 'revisions' in p:
                cache[t] = {'page': p['title'], 'text': p['revisions'][0]['slots']['main']['*']}
            else:
                cache[t] = None
        print('pcgw titles', i + len(chunk), '/', len(todo), flush=True)
        time.sleep(1.0)

def pcgw_by_appid(appid, cache):
    k = 'appid:%s' % appid
    if k in cache: return cache[k]
    loc = http('https://www.pcgamingwiki.com/api/appid.php?appid=%s' % appid, follow=False)
    page = None
    if loc and '/wiki/' in loc:
        page = urllib.parse.unquote(loc.split('/wiki/', 1)[1]).replace('_', ' ')
    cache[k] = None
    if page:
        pcgw_pages([page], cache)
        cache[k] = cache.get(page)
    time.sleep(0.5)
    return cache[k]

def pcgw_search(title, cache):
    k = 'search:' + title
    if k in cache: return cache[k]
    d = http(API + urllib.parse.urlencode({'action': 'query', 'list': 'search', 'srsearch': title, 'srlimit': 3, 'format': 'json'}))
    cache[k] = None
    for r in (d or {}).get('query', {}).get('search', []):
        if norm_loose(r['title']) == norm_loose(title):
            pcgw_pages([r['title']], cache); cache[k] = cache.get(r['title']); break
    time.sleep(0.5)
    return cache[k]

def field(text, name):
    m = re.search(r'^\|\s*' + re.escape(name) + r'\s*=([^\n]*)', text, re.M | re.I)
    return m.group(1).strip() if m else ''

def parse_pcgw(text):
    eng = []
    for m in re.finditer(r'\{\{Infobox game/row/engine\|([^|}]+)([^}]*)', text):
        nm = re.search(r'\|\s*name\s*=\s*([^|}]+)', m.group(2))
        eng.append((nm.group(1) if nm else m.group(1)).strip())
    tax = {}
    for m in re.finditer(r'\{\{Infobox game/row/taxonomy/([a-z ]+?)\s*\|([^}]*)\}\}', text):
        v = [x.strip() for x in m.group(2).split(',') if x.strip()]
        if v: tax[m.group(1).strip()] = v
    def clean(v):
        v = re.sub(r'<ref.*?(</ref>|/>)', '', v); v = re.sub(r'\{\{[^}]*\}\}', '', v)
        return v.strip()
    d3d = clean(field(text, 'direct3d versions'))
    return {
        'engines': list(dict.fromkeys(eng)),
        'steam_appids': re.findall(r'\d+', field(text, 'steam appid')),
        'd3d': [x.strip() for x in re.split(r'[,/]', d3d) if x.strip()],
        'ddraw': clean(field(text, 'directdraw versions')),
        'opengl': clean(field(text, 'opengl versions')),
        'opengl_notes': clean(field(text, 'opengl notes')),
        'vulkan': clean(field(text, 'vulkan versions')),
        'glide': clean(field(text, 'glide versions')),
        'software': clean(field(text, 'software mode')),
        'x86': field(text, 'windows 32-bit exe').lower(), 'x64': field(text, 'windows 64-bit exe').lower(),
        'perspectives': tax.get('perspectives', []), 'art': tax.get('art styles', []), 'genres': tax.get('genres', []),
    }

ENGINE_API = [
    ('unreal engine', 'd3d11'), ('unity 4', 'd3d9'), ('unity 3', 'd3d9'), ('unity', 'd3d11'),
    ('gamemaker: studio 1', 'd3d9'), ('game maker', 'd3d9'), ('gamemaker', 'd3d11'), ('clickteam', 'd3d9'),
    ('chowdren', 'opengl'), ('libgdx', 'opengl'), ('lwjgl', 'opengl'), ('solar2d', 'opengl'), ('corona', 'opengl'),
    ('löve', 'opengl'), ('love2d', 'opengl'), ('defold', 'opengl'), ('godot 3', 'opengl'), ('godot', 'opengl'),
    ('openfl', 'opengl'), ('lime', 'opengl'), ('haxeflixel', 'opengl'), ('ren\'py', 'opengl'), ('monogame', 'opengl'),
    ('fna', 'd3d11'), ('xna', 'd3d9'), ('heaps', 'd3d11'), ('adventure game studio', 'd3d9'),
    ('visionaire', 'opengl'), ('wintermute', 'd3d9'), ('source', 'd3d9'), ('adobe air', 'd3d11'),
]
GL_SWITCH = [
    ('adventure game studio', 'winsetup.exe / acsetup.cfg에서 driver=OGL'),
    ('unity', '실행 옵션 -force-glcore (또는 -force-opengl)'),
    ('fna', '환경 변수 FNA3D_FORCE_DRIVER=OpenGL'),
    ('heaps', '게임 설정 확인'),
]

