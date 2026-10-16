import struct, json, sys
def parse(path):
    d = open(path, 'rb').read()
    magic, universe = struct.unpack_from('<II', d, 0)
    off = 8
    strings = None
    if magic == 0x07564429:
        st_off = struct.unpack_from('<q', d, off)[0]; off += 8
        n = struct.unpack_from('<I', d, st_off)[0]; p = st_off + 4; strings = []
        for _ in range(n):
            e = d.index(b'\0', p); strings.append(d[p:e].decode('utf-8', 'replace')); p = e + 1
    def kv(p):
        obj = {}
        while True:
            t = d[p]; p += 1
            if t == 8: return obj, p
            if strings is not None:
                k = strings[struct.unpack_from('<I', d, p)[0]]; p += 4
            else:
                e = d.index(b'\0', p); k = d[p:e].decode('utf-8', 'replace'); p = e + 1
            if t == 0: v, p = kv(p)
            elif t == 1:
                e = d.index(b'\0', p); v = d[p:e].decode('utf-8', 'replace'); p = e + 1
            elif t == 2: v = struct.unpack_from('<i', d, p)[0]; p += 4
            elif t == 3: v = struct.unpack_from('<f', d, p)[0]; p += 4
            elif t == 7: v = struct.unpack_from('<Q', d, p)[0]; p += 8
            elif t == 10: v = struct.unpack_from('<q', d, p)[0]; p += 8
            else: raise ValueError(t)
            obj[k] = v
    apps = {}
    while True:
        appid = struct.unpack_from('<I', d, off)[0]
        if appid == 0: break
        size = struct.unpack_from('<I', d, off + 4)[0]
        start = off + 8
        p = start + 4 + 4 + 8 + 20 + 4 + 20
        obj, _ = kv(p)
        apps[appid] = obj.get('appinfo', obj)
        off = start + size
    return magic, apps
if __name__ == '__main__':
    magic, apps = parse(sys.argv[1])
    print(hex(magic), len(apps))
    json.dump({str(k): v for k, v in apps.items()}, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False)
