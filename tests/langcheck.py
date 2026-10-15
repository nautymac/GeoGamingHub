"""Lists Lang.T("...") literals missing from Lang.cs's English table, and duplicate table keys.

usage: langcheck.py <src dir>
"""
import glob
import os
import re
import sys

src = sys.argv[1]
lit = r'"((?:[^"\\]|\\.)*)"'
lang = open(os.path.join(src, 'Lang.cs'), encoding='utf-8-sig').read()
table = lang[lang.index('static readonly Dictionary<string, string> en'):]
keys = re.findall(r'\{\s*' + lit + r'\s*,\s*' + lit + r'\s*\}', table)
seen, dups = set(), []
for k, _ in keys:
    if k in seen:
        dups.append(k)
    seen.add(k)

missing = []
for f in sorted(glob.glob(os.path.join(src, '*.cs'))):
    s = open(f, encoding='utf-8-sig').read()
    for m in re.finditer(r'Lang\.T\(\s*' + lit, s):
        k = m.group(1)
        if k not in seen and re.search(r'[가-힣]', k):
            line = s.count('\n', 0, m.start()) + 1
            missing.append((os.path.basename(f), line, k))

print('table keys:', len(keys), 'duplicates:', len(dups))
for d in dups:
    print('  DUP', d)
print('missing:', len(missing))
done = set()
for f, line, k in missing:
    if k in done:
        continue
    done.add(k)
    print('  %s:%d  %s' % (f, line, k))
