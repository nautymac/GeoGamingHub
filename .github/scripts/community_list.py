#!/usr/bin/env python3
"""Builds the community game list from the issues labelled 'community' (3D report and Suggest a game forms).

Every report carries a PCGamingWiki link. The page is read and classified exactly like the app's candidate list
(tools/library_survey: pcgw.py fetches and parses the wikitext, hubclass.py turns engine + graphics API into the
3D method), so reported and surveyed games are described the same way.

Output:
  COMMUNITY.md     English list, by program (Geo Gaming Hub / Flato3D) and 3D method
  COMMUNITY.ko.md  Korean list
  community.json   the same data for the app (merged into its candidate list)

Run by .github/workflows/community-list.yml. Local test:
  python community_list.py --fixture issues.json --out OUTDIR
"""
import argparse
import json
import os
import re
import sys
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "tools", "library_survey"))
from pcgw import pcgw_pages, pcgw_by_appid, parse_pcgw   # noqa: E402  (the survey's own code)
from hubclass import classify                           # noqa: E402

REPO_URL = "https://github.com/{repo}"
EXCLUDE_LABELS = {"invalid", "duplicate", "spam", "wontfix"}
NO_RESPONSE = "_No response_"

RESULTS = [("broken", r"does\s*not|doesn.?t|not\s*work|안\s*됨"), ("issues", r"issue|problem|문제"), ("works", r"works|잘|가능")]
RESULT_TEXT = {
    "en": {"works": "✅ Works well", "issues": "⚠️ Works with issues", "broken": "❌ Does not work", "untested": "💡 Suggested"},
    "ko": {"works": "✅ 잘 됨", "issues": "⚠️ 문제 있음", "broken": "❌ 안 됨", "untested": "💡 제안됨"},
}
ICON = {"works": "✅", "issues": "⚠️", "broken": "❌", "untested": "💡"}

PROGRAMS = [("hub", "Geo Gaming Hub", "Geo Gaming Hub"), ("flato3d", "Flato3D (flat 2D)", "Flato3D (평면 2D)"), ("none", "No route yet", "아직 방법 없음")]
METHOD_TEXT = {
    "unity": ("geo-11 Unity", "geo-11 Unity"), "ue4": ("geo-11 UE4 (UF2)", "geo-11 UE4 (UF2)"), "dx11": ("geo-11 generic (DX11)", "geo-11 일반 (DX11)"),
    "dx9": ("DX9 → dgVoodoo2 + geo-11", "DX9 → dgVoodoo2 + geo-11"), "opengl": ("OpenGL → OGL3D", "OpenGL → OGL3D"), "vulkan": ("Vulkan → VK3D", "Vulkan → VK3D"),
    "dx12": ("DX12 (not supported yet)", "DX12 (아직 미지원)"), "old": ("DX8 or older", "DX8 이하"), "none": ("no route", "불가"),
    "unknown": ("unknown", "미확인"), "flato3d": ("Flato3D", "Flato3D"),
}
# the method a reporter used (the app writes these words) -> method key; first match wins
USED = [("flato3d", r"flato"), ("vulkan", r"vk3d|vulkan|dxvk"), ("opengl", r"ogl3d|opengl"), ("ue4", r"ue\s*-?\s*4|uf2|unreal"),
        ("unity", r"unity"), ("dx9", r"dx\s*9|dgvoodoo|d3d\s*9"), ("dx11", r"dx\s*1[01]|generic|preferred|일반|geo-?11")]


def api(url, token):
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json", "User-Agent": "geogaminghub-community-list"})
    if token:
        req.add_header("Authorization", "Bearer " + token)
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read().decode("utf-8"))


def fetch_issues(repo, token):
    issues, page = [], 1
    while True:
        batch = api("https://api.github.com/repos/%s/issues?labels=community&state=all&per_page=100&page=%d" % (repo, page), token)
        issues += [i for i in batch if "pull_request" not in i]
        if len(batch) < 100:
            return issues
        page += 1


FIELDS = (("game", ("game", "게임")), ("pcgw", ("pcgamingwiki",)), ("program", ("program", "담당")), ("used", ("3d method", "사용한")),
          ("result", ("result", "결과")), ("details", ("setup", "설정")), ("notes", ("notes", "메모")))


def sections(body):
    """'### Heading' + value blocks of an issue form body -> {field: value}."""
    out = {}
    parts = re.split(r"^###\s+(.+?)\s*$", (body or "").replace("\r\n", "\n"), flags=re.M)
    for i in range(1, len(parts) - 1, 2):
        head, val = parts[i].strip().lower(), parts[i + 1].strip()
        if val == NO_RESPONSE:
            val = ""
        for field, words in FIELDS:
            if any(head.startswith(w) for w in words) and field not in out:
                out[field] = val
                break
    return out


def pcgw_target(text):
    """PCGamingWiki page title, or ('appid', N) for a Steam id / store link / PCGamingWiki appid link."""
    t = (text or "").strip()
    m = re.search(r"pcgamingwiki\.com/wiki/([^?#\s]+)", t, re.I)
    if m:
        return urllib.parse.unquote(m.group(1)).replace("_", " ").strip()
    m = re.search(r"appid(?:\.php)?\?appid=(\d+)|store\.steampowered\.com/app/(\d+)|^(\d{3,8})$", t, re.I)
    if m:
        return ("appid", next(g for g in m.groups() if g))
    return None


def result_of(text):
    t = (text or "").lower()
    for key, pat in RESULTS:
        if re.search(pat, t):
            return key
    return None


def used_of(text):
    t = (text or "").lower()
    for key, pat in USED:
        if re.search(pat, t):
            return key
    return None


def game_key(name):
    n = re.sub(r"[™®©]", "", (name or "").lower())
    return re.sub(r"[\W_]+", "", n)


def cell(text, limit=120):
    """Untrusted issue text -> one safe Markdown table cell."""
    t = re.sub(r"\s+", " ", text or "").strip()
    if len(t) > limit:
        t = t[: limit - 1].rstrip() + "…"
    t = t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    return re.sub(r"([\\`*_\[\]|#!])", r"\\\1", t)


def parse(issue):
    labels = {((l.get("name") if isinstance(l, dict) else l) or "").lower() for l in issue.get("labels", [])}
    if labels & EXCLUDE_LABELS or issue.get("state_reason") == "not_planned":
        return None
    f = sections(issue.get("body"))
    game = (f.get("game") or "").strip()
    target = pcgw_target(f.get("pcgw"))
    suggestion = "suggestion" in labels or not f.get("result")
    result = "untested" if suggestion else result_of(f.get("result"))
    if not game or not result or not target:
        return None
    prog = (f.get("program") or "").lower()
    program = "flato3d" if "flato" in prog else "hub" if "geo gaming" in prog else ""
    return {
        "number": issue["number"], "url": issue.get("html_url", ""), "created": issue.get("created_at", ""),
        "game": game, "target": target, "program": program, "used": None if suggestion else used_of(f.get("used")),
        "result": result, "notes": f.get("notes", ""),
    }


def resolve(reports):
    """PCGamingWiki page for every report (fetched in batches of 50, like the survey) -> page title + parsed fields."""
    cache = {}
    for r in reports:
        if isinstance(r["target"], tuple):
            pc = pcgw_by_appid(r["target"][1], cache)
            r["page"] = pc["page"] if pc else None
            r["_text"] = pc["text"] if pc else None
    titles = [r["target"] for r in reports if isinstance(r["target"], str)]
    if titles:
        pcgw_pages(titles, cache)
    for r in reports:
        if isinstance(r["target"], str):
            pc = cache.get(r["target"])
            r["page"] = pc["page"] if pc else None
            r["_text"] = pc["text"] if pc else None
        r["pc"] = parse_pcgw(r["_text"]) if r["_text"] else None


def aggregate(reports):
    games = {}
    for r in sorted(reports, key=lambda r: (r["created"], r["number"])):
        key = game_key(r["page"]) if r["page"] else game_key(r["game"])
        g = games.setdefault(key, {"key": key, "reports": [], "works": 0, "issues": 0, "broken": 0, "untested": 0,
                                   "used": {}, "programs": {}, "note": ""})
        g["reports"].append(r)
        g[r["result"]] += 1
        g["game"] = r["page"] or r["game"]
        g["page"] = r["page"] or ""
        g["pc"] = r["pc"] or g.get("pc")
        g["updated"] = r["created"][:10]
        if r["result"] != "untested":
            g["last"] = r["result"]
        if r["used"]:
            g["used"][r["used"]] = g["used"].get(r["used"], 0) + 1
        if r["program"]:
            g["programs"][r["program"]] = g["programs"].get(r["program"], 0) + 1
        if r["notes"]:
            g["note"] = r["notes"]
    for g in games.values():
        methods, note, engines, apitext, bits = classify(g["pc"], g["game"])
        tested = {k: g[k] for k in ("works", "issues", "broken")}
        if sum(tested.values()):
            best = max(tested.values())
            tied = [k for k, v in tested.items() if v == best]
            g["status"] = g["last"] if g["last"] in tied else tied[0]   # majority, the newest report breaks a tie
        else:
            g["status"] = "untested"
        used = max(g["used"], key=g["used"].get) if g["used"] else None
        if used == "flato3d":
            program = "flato3d"
        elif g["programs"]:
            program = max(g["programs"], key=g["programs"].get)
        else:
            program = "none" if methods[0] == "none" else "hub"
        if used and used != "flato3d" and used in methods:
            methods.remove(used)
        if used and used != "flato3d":
            methods.insert(0, used)
        g.update(program=program, method=methods[0], alts=methods[1:], engine=engines, api=apitext, bits=bits,
                 class_note=note, steam=(g["pc"] or {}).get("steam_appids", []))
    return games


def render(games, lang, repo, total):
    ko = lang == "ko"
    forms = REPO_URL.format(repo=repo) + "/issues/new/choose"
    L = []
    if ko:
        L += ["# 커뮤니티 게임 목록", "",
              "사용자 보고와 후보 제안으로 자동으로 만들어지는 목록입니다. 엔진과 그래픽 API는 각 보고의 PCGamingWiki 문서에서 앱의 후보 목록과 같은 방식으로 읽습니다. "
              "보고는 앱의 **[3D 동작 보고...]** 버튼이나 [양식](%s)으로 합니다." % forms, "",
              "✅ 잘 됨 · ⚠️ 문제 있음 · ❌ 안 됨 · 💡 제안됨(아직 시험 안 함). 상태는 시험 보고의 다수결이고, 같으면 최근 보고를 따릅니다.", "",
              "게임 %d개 · 보고 %d건 · English: [COMMUNITY.md](COMMUNITY.md)" % (len(games), total), ""]
        head = "| 게임 | 상태 | 보고 | 엔진 | API | 비트 | 최근 메모 | 이슈 |"
    else:
        L += ["# Community game list", "",
              "Built automatically from user reports and suggestions. The engine and graphics API come from each report's PCGamingWiki page, read the same way as the app's candidate list. "
              "Report with the app's **[Report 3D...]** button or the [forms](%s)." % forms, "",
              "✅ works well · ⚠️ works with issues · ❌ does not work · 💡 suggested (not tried yet). The status is the majority of the test reports; a tie goes to the newest one.", "",
              "%d games · %d reports · 한국어: [COMMUNITY.ko.md](COMMUNITY.ko.md)" % (len(games), total), ""]
        head = "| Game | Status | Reports | Engine | API | Bits | Latest note | Issues |"
    for pkey, pen, pko in PROGRAMS:
        rows_p = [g for g in games.values() if g["program"] == pkey]
        if not rows_p:
            continue
        L += ["## " + (pko if ko else pen), ""]
        order = ["unity", "ue4", "dx11", "dx9", "opengl", "vulkan", "dx12", "old", "none", "unknown"]
        for m in sorted({g["method"] for g in rows_p}, key=lambda x: order.index(x) if x in order else 99):
            rows = sorted((g for g in rows_p if g["method"] == m), key=lambda g: g["game"].lower())
            if pkey != "flato3d":
                L += ["### " + METHOD_TEXT.get(m, (m, m))[1 if ko else 0], ""]
            L += [head, "|---|---|---|---|---|---|---|---|"]
            for g in rows:
                counts = " · ".join("%s %d" % (ICON[k], g[k]) for k in ("works", "issues", "broken", "untested") if g[k])
                links = " ".join("[#%d](%s)" % (r["number"], r["url"]) for r in g["reports"][-5:])
                name = cell(g["game"], 80)
                if g["page"]:
                    name = "[%s](https://www.pcgamingwiki.com/wiki/%s)" % (name, urllib.parse.quote(g["page"].replace(" ", "_")))
                L.append("| %s | %s | %s | %s | %s | %s | %s | %s |" % (name, RESULT_TEXT[lang][g["status"]], counts, cell(", ".join(g["engine"]), 60) or "-",
                                                                      cell(g["api"], 60) or "-", g["bits"] or "-", cell(g["note"]) or "-", links))
            L.append("")
    return "\n".join(L)


def to_json(games):
    out = []
    for g in sorted(games.values(), key=lambda g: g["game"].lower()):
        out.append({"game": g["game"], "page": g["page"], "key": g["key"], "program": g["program"], "method": g["method"], "alts": g["alts"],
                    "engine": g["engine"], "api": g["api"], "bits": g["bits"], "status": g["status"],
                    "works": g["works"], "issues": g["issues"], "broken": g["broken"], "suggested": g["untested"],
                    "steam": g["steam"], "updated": g["updated"], "note": g["note"], "class_note": g["class_note"],
                    "reports": [r["number"] for r in g["reports"]]})
    return {"version": 1, "source": "GitHub issues (community label) + PCGamingWiki (CC BY-NC-SA 3.0)", "games": out}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY", "nautymac/GeoGamingHub"))
    ap.add_argument("--out", default=".")
    ap.add_argument("--fixture", help="JSON file with issues (API format) instead of calling GitHub")
    ap.add_argument("--check", type=int, help="only parse this issue: write listed=true|false and what= to GITHUB_OUTPUT")
    a = ap.parse_args()
    gh_out = os.environ.get("GITHUB_OUTPUT")
    if a.check:
        r = parse(api("https://api.github.com/repos/%s/issues/%d" % (a.repo, a.check), os.environ.get("GITHUB_TOKEN")))
        if r:
            resolve([r])
        ok = bool(r and r["page"])
        what = ""
        if ok:
            g = aggregate([r])
            what = "%s / %s" % (r["page"], METHOD_TEXT.get(next(iter(g.values()))["method"], ("?",))[0])
        print("issue #%d: %s" % (a.check, what if ok else "not listed (no game, result or readable PCGamingWiki link, or excluded)"))
        if gh_out:
            with open(gh_out, "a", encoding="utf-8") as f:
                f.write("listed=%s\nwhat=%s\n" % ("true" if ok else "false", what))
        return 0
    if a.fixture:
        with open(a.fixture, encoding="utf-8") as f:
            issues = json.load(f)
    else:
        issues = fetch_issues(a.repo, os.environ.get("GITHUB_TOKEN"))
    reports = [r for r in map(parse, issues) if r]
    resolve(reports)
    unread = [r for r in reports if not r["page"]]
    for r in unread:
        print("issue #%d: PCGamingWiki page not found for %r - left out" % (r["number"], r["target"]))
    reports = [r for r in reports if r["page"]]
    games = aggregate(reports)
    for name, text in (("COMMUNITY.md", render(games, "en", a.repo, len(reports))), ("COMMUNITY.ko.md", render(games, "ko", a.repo, len(reports)))):
        with open(os.path.join(a.out, name), "w", encoding="utf-8", newline="\n") as f:
            f.write(text + "\n")
    with open(os.path.join(a.out, "community.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(to_json(games), f, ensure_ascii=False, indent=1)
        f.write("\n")
    print("%d reports from %d issues, %d games" % (len(reports), len(issues), len(games)))


if __name__ == "__main__":
    sys.exit(main())
