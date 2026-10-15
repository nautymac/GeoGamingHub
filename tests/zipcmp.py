import sys
import zipfile

old = zipfile.ZipFile(sys.argv[1])
new = zipfile.ZipFile(sys.argv[2])


def norm(n):
    return n.replace(chr(92), '/').rstrip('/')


o = {norm(i.filename): i for i in old.infolist()}
n = {norm(i.filename): i for i in new.infolist()}
print('entries old', len(o), 'new', len(n))
print('backslash names in new:', sum(chr(92) in i.filename for i in new.infolist()))
print('only in old:', sorted(set(o) - set(n))[:10])
print('only in new:', sorted(set(n) - set(o)))
files = [k for k in set(o) & set(n) if not o[k].is_dir()]
changed = sorted(k for k in files if o[k].CRC != n[k].CRC or o[k].file_size != n[k].file_size)
print('changed files:', changed)
tchanged = sorted(k for k in files if o[k].date_time != n[k].date_time and k not in changed)
print('timestamp-only changes:', len(tchanged), tchanged[:8])
for k in sorted(n):
    if k.startswith('geo-11_SR/OGL3D'):
        print('  ', k, n[k].date_time, n[k].file_size)
print('testzip:', new.testzip())
