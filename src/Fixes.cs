// Community fixes for geo-11 / 3DMigoto games (HelixMod and the like: d3dx.ini + ShaderFixes\, often with their own
// d3d11.dll). A fix is imported once per game - an archive (.zip, .rar, .7z) or an unpacked folder - into
// <hub>\fixes\<exe file name>\ (its whole content, plus fix.ini: where it came from and the chosen driver), and is
// merged into the hub's own geo-11 install every time the game is set up with [완료]:
//   - the hub's d3dx.ini / d3dxdm.ini stay the base. [Logging] [System] [Device] [Stereo] [Rendering] [Hunting]
//     [Loader] [Profile] of the fix are not taken: they would drop the hub's SR weaving, upscaling and overlay
//     settings, and they are not what makes a fix game-specific;
//   - every other d3dx.ini section the hub's file lacks is appended ([Key*], [Preset*], [ShaderOverride*],
//     [TextureOverride*], [ShaderRegex*], [CommandList*], [Resource*], [CustomShader*] ...), and the active lines of
//     [Include], [Constants] and [Present] that the hub's file lacks are added to those sections;
//   - ShaderFixes\ and ShaderFixesDM\ files are copied unless the hub's package has the same name there (upscale,
//     mouse, 3dvision2sbs, hotkeys.ini: the hub's own stay);
//   - the geo-11 driver (d3d11.dll) is the hub's or one found inside the fix, of the game's bitness: the newest by
//     default. Star Wars Battlefront II (2017) crashes in the NVIDIA driver about a minute in with geo-11 0.6.109
//     (the hub's) and 0.6.56 (its fix's) and runs with 0.6.198, which the fix ships as "If crash, use this DLL".
// The fix's separation / convergence (its d3dxdm.ini) become the stereo step's starting values when it is imported.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeoGamingHub
{
    static class FixOps
    {
        public static string Root;                    // <hub>\fixes (set by the form)
        const string InfoFile = "fix.ini";
        static readonly Encoding Raw = Encoding.GetEncoding(28591);   // byte-exact round trip whatever the ini's encoding
        static readonly string[] CoreSections = { "logging", "system", "device", "stereo", "rendering", "hunting", "loader", "profile" };
        static readonly string[] LineSections = { "include", "constants", "present" };
        static readonly Regex Header = new Regex(@"^\s*\[([^\]]+)\]\s*$");

        public sealed class Driver
        {
            public string Path;       // full path of the d3d11.dll
            public string Rel;        // "hub" or the path relative to the fix folder
            public string Version;    // "0.6.198", or null when the file does not say
            public string Label;
            public bool Hub;
        }

        public sealed class Info
        {
            public string Dir;        // fixes\<exe>
            public string FixRoot;    // the folder inside it that has d3dx.ini
            public string Name;       // the archive / folder it came from
            public string Choice;     // driver: "hub", a path relative to Dir, or "" (newest)
            public string Sep, Conv;  // the fix's d3dxdm.ini values, null when absent
            public int Shaders, Sections;
        }

        public static string DirOf(string exe)
        {
            if (string.IsNullOrEmpty(Root) || string.IsNullOrEmpty(exe)) return null;
            return System.IO.Path.Combine(Root, System.IO.Path.GetFileName(exe).ToLowerInvariant());
        }

        public static Info Load(string exe)
        {
            string d = DirOf(exe);
            if (d == null || !Directory.Exists(d)) return null;
            string root = FindFixRoot(d);
            if (root == null) return null;
            string ini = System.IO.Path.Combine(d, InfoFile);
            var info = new Info { Dir = d, FixRoot = root, Name = ReadValue(ini, "name") ?? System.IO.Path.GetFileName(root), Choice = ReadValue(ini, "geo11") ?? "" };
            string dm = System.IO.Path.Combine(root, "d3dxdm.ini");
            info.Sep = Ini.Get(dm, "dm_separation");
            info.Conv = Ini.Get(dm, "dm_convergence");
            foreach (var sub in new[] { "ShaderFixes", "ShaderFixesDM" })
            {
                string s = System.IO.Path.Combine(root, sub);
                if (Directory.Exists(s)) info.Shaders += Directory.GetFiles(s, "*", SearchOption.AllDirectories).Length;
            }
            string dx = System.IO.Path.Combine(root, "d3dx.ini");
            if (File.Exists(dx)) info.Sections = Parse(dx).Count(s => s.Name.Length > 0 && !IsCore(s.Name) && !IsLines(s.Name));
            return info;
        }

        // the shallowest folder with a d3dx.ini (archives often wrap the fix in a folder of its own)
        static string FindFixRoot(string dir)
        {
            var level = new List<string> { dir };
            for (int depth = 0; depth < 6 && level.Count > 0; depth++)
            {
                foreach (var d in level.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    if (File.Exists(System.IO.Path.Combine(d, "d3dx.ini"))) return d;
                var next = new List<string>();
                foreach (var d in level) { try { next.AddRange(Directory.GetDirectories(d)); } catch { } }
                level = next;
            }
            return null;
        }

        // ---- import / remove ------------------------------------------------------------------------------
        public static Info Import(string exe, string source)
        {
            string d = DirOf(exe);
            if (d == null) throw new Exception(Lang.T("게임을 먼저 고르세요."));
            string tmp = d + ".new";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);
            try
            {
                if (Directory.Exists(source)) Util.CopyDir(source, tmp, null);
                else Extract(source, tmp);
                // a fix folder often holds the fix as an archive next to extras ("geo-11 v0.6.198 (If crash, use
                // this DLL)\d3d11.dll" next to the Battlefront II fix's .rar): unpack archives found inside too
                ExtractNested(tmp, 0);
                if (FindFixRoot(tmp) == null)
                    throw new Exception(Lang.T("d3dx.ini가 없습니다 - geo-11 / 3DMigoto 픽스가 아닙니다:\n") + source);
                string name = System.IO.Path.GetFileName(source.TrimEnd('\\', '/'));
                File.WriteAllText(System.IO.Path.Combine(tmp, InfoFile), "[Fix]\r\nname=" + name + "\r\nsource=" + source + "\r\ngeo11=\r\n", Encoding.UTF8);
                if (Directory.Exists(d)) Directory.Delete(d, true);
                Directory.Move(tmp, d);
            }
            catch
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
                throw;
            }
            return Load(exe);
        }

        static readonly string[] ArchiveExts = { ".zip", ".rar", ".7z" };

        static void ExtractNested(string dir, int depth)
        {
            if (depth > 2) return;
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (!ArchiveExts.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())) continue;
                string to = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(f), System.IO.Path.GetFileNameWithoutExtension(f));
                if (Directory.Exists(to)) to += "_unpacked";
                Directory.CreateDirectory(to);
                try { Extract(f, to); File.Delete(f); ExtractNested(to, depth + 1); }
                catch { try { Directory.Delete(to, true); } catch { } }   // not a readable archive: leave the file as it is
            }
        }

        // a geo-11 d3d11.dll from anywhere (a newer build, a fix's "If crash" DLL): kept with the fix and chosen
        public static string AddDriver(string exe, string dll)
        {
            string d = DirOf(exe);
            if (d == null || !Directory.Exists(d)) throw new Exception(Lang.T("먼저 픽스를 불러오세요."));
            string v = Geo11Version(dll);
            if (v == null) throw new Exception(Lang.T("geo-11 / 3DMigoto의 d3d11.dll이 아닙니다 (버전을 읽지 못함):\n") + dll);
            string to = System.IO.Path.Combine(d, "drivers", v + (Util.ExeBits(dll) == 32 ? "_x32" : ""));
            Directory.CreateDirectory(to);
            File.Copy(dll, System.IO.Path.Combine(to, "d3d11.dll"), true);
            string rel = System.IO.Path.Combine(to, "d3d11.dll").Substring(d.Length).TrimStart('\\');
            SetChoice(exe, rel);
            return rel;
        }

        public static bool Remove(string exe)
        {
            string d = DirOf(exe);
            if (d == null || !Directory.Exists(d)) return false;
            Directory.Delete(d, true);
            return true;
        }

        // .zip with .NET; .rar / .7z with 7-Zip or WinRAR's UnRAR when installed, else Windows' own tar (libarchive)
        static void Extract(string archive, string dest)
        {
            if (!File.Exists(archive)) throw new FileNotFoundException(Lang.T("파일이 없습니다: ") + archive);
            string ext = System.IO.Path.GetExtension(archive).ToLowerInvariant();
            string output = "";
            if (ext == ".zip") ZipFile.ExtractToDirectory(archive, dest);
            else
            {
                string seven = FindTool(@"7-Zip\7z.exe"), unrar = ext == ".rar" ? FindTool(@"WinRAR\UnRAR.exe") : null;
                string tar = System.IO.Path.Combine(Environment.SystemDirectory, "tar.exe");
                if (seven != null) output = Util.RunHidden(seven, "x -y -o\"" + dest + "\" \"" + archive + "\"", dest);
                else if (unrar != null) output = Util.RunHidden(unrar, "x -o+ -inul \"" + archive + "\" \"" + dest + "\\\\\"", dest);
                else if (File.Exists(tar)) output = Util.RunHidden(tar, "-xf \"" + archive + "\" -C \"" + dest + "\"", dest);
            }
            if (!Directory.EnumerateFileSystemEntries(dest).Any())
                throw new Exception(Lang.T("압축을 풀지 못했습니다: ") + archive + (output.Length > 0 ? "\n" + output : "") +
                    Lang.T("\n\n7-Zip이나 WinRAR로 직접 푼 뒤 [폴더에서...]로 그 폴더를 고르세요."));
        }

        static string FindTool(string rel)
        {
            foreach (var pf in new[] { Environment.GetEnvironmentVariable("ProgramW6432"), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                       Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                string p = System.IO.Path.Combine(pf, rel);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        static string ReadValue(string ini, string key)
        {
            if (!File.Exists(ini)) return null;
            foreach (var l in File.ReadAllLines(ini, Encoding.UTF8))
            {
                int i = l.IndexOf('=');
                if (i > 0 && l.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return l.Substring(i + 1).Trim();
            }
            return null;
        }

        public static void SetChoice(string exe, string rel)
        {
            string ini = System.IO.Path.Combine(DirOf(exe), InfoFile);
            var lines = File.Exists(ini) ? File.ReadAllLines(ini, Encoding.UTF8).ToList() : new List<string> { "[Fix]" };
            int at = lines.FindIndex(l => l.TrimStart().StartsWith("geo11", StringComparison.OrdinalIgnoreCase) && l.Contains("="));
            if (at >= 0) lines[at] = "geo11=" + rel; else lines.Add("geo11=" + rel);
            File.WriteAllLines(ini, lines, Encoding.UTF8);
        }

        // ---- geo-11 drivers --------------------------------------------------------------------------------
        static readonly Dictionary<string, string> versionCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // geo-11 prints "D3D11 DLL starting init - v %s" with its version kept as a separate string ("0.6.198")
        public static string Geo11Version(string dll)
        {
            try
            {
                var fi = new FileInfo(dll);
                if (!fi.Exists) return null;
                string key = dll + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks;
                string v;
                if (versionCache.TryGetValue(key, out v)) return v;
                var m = Regex.Match(Raw.GetString(File.ReadAllBytes(dll)), "\0(0\\.\\d{1,2}\\.\\d{1,3})\0");
                v = m.Success ? m.Groups[1].Value : null;
                versionCache[key] = v;
                return v;
            }
            catch { return null; }
        }

        static Version Ver(string v)
        {
            Version r;
            return v != null && Version.TryParse(v, out r) ? r : new Version(0, 0);
        }

        // the hub's driver first, then every d3d11.dll inside the fix that matches the game's bitness
        public static List<Driver> Drivers(Info fix, string hubDll, int bits)
        {
            var list = new List<Driver>();
            if (File.Exists(hubDll))
            {
                string v = Geo11Version(hubDll);
                list.Add(new Driver { Path = hubDll, Rel = "hub", Version = v, Hub = true, Label = Lang.T("허브 기본 geo-11 ") + (v ?? "?") });
            }
            foreach (var f in Directory.GetFiles(fix.Dir, "d3d11.dll", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var b = Util.ExeBits(f);
                if (b.HasValue && b.Value != bits) continue;
                string rel = f.Substring(fix.Dir.Length).TrimStart('\\');
                string v = Geo11Version(f);
                list.Add(new Driver { Path = f, Rel = rel, Version = v, Label = Lang.T("픽스의 geo-11 ") + (v ?? "?") + "  (" + rel + ")" });
            }
            return list;
        }

        public static Driver Chosen(Info fix, List<Driver> drivers)
        {
            if (drivers.Count == 0) return null;
            var d = drivers.FirstOrDefault(x => x.Rel.Equals(fix.Choice ?? "", StringComparison.OrdinalIgnoreCase));
            return d ?? drivers.OrderByDescending(x => Ver(x.Version)).ThenBy(x => x.Hub ? 0 : 1).First();
        }

        // ---- merge -----------------------------------------------------------------------------------------
        sealed class Section { public string Name; public List<string> Lines = new List<string>(); }

        static bool IsCore(string name) { return CoreSections.Contains(name.Trim().ToLowerInvariant()); }
        static bool IsLines(string name) { return LineSections.Contains(name.Trim().ToLowerInvariant()); }

        // sections in file order; the lines before the first header go in a nameless one. Line ends are kept.
        static List<Section> Parse(string file)
        {
            var list = new List<Section> { new Section { Name = "" } };
            string text = File.ReadAllText(file, Raw);
            int i = 0;
            while (i < text.Length)
            {
                int n = text.IndexOf('\n', i);
                string line = n < 0 ? text.Substring(i) : text.Substring(i, n - i + 1);
                i = n < 0 ? text.Length : n + 1;
                var m = Header.Match(line.TrimEnd('\r', '\n'));
                if (m.Success) list.Add(new Section { Name = m.Groups[1].Value.Trim() });
                list[list.Count - 1].Lines.Add(line);
            }
            return list;
        }

        static bool Active(string line)
        {
            string s = line.Trim();
            return s.Length > 0 && !s.StartsWith(";") && !Header.IsMatch(s);
        }

        static string Norm(string line) { return Regex.Replace(line.Trim().ToLowerInvariant(), @"\s+", ""); }

        // the hub's d3dx.ini (in the game folder) plus the game-specific part of the fix's
        public static List<string> MergeD3dx(string hubIni, string fixIni)
        {
            var log = new List<string>();
            var hub = Parse(hubIni);
            var fix = Parse(fixIni);
            string nl = hub.Any(s => s.Lines.Any(l => l.EndsWith("\r\n"))) ? "\r\n" : "\n";
            var hubNames = new HashSet<string>(hub.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var fs in fix.Where(s => IsLines(s.Name)))
            {
                var target = hub.FirstOrDefault(s => s.Name.Equals(fs.Name, StringComparison.OrdinalIgnoreCase));
                var have = new HashSet<string>(target == null ? new string[0] : target.Lines.Where(Active).Select(Norm));
                var add = fs.Lines.Where(Active).Where(l => !have.Contains(Norm(l))).ToList();
                if (add.Count == 0) continue;
                if (target == null) { target = new Section { Name = fs.Name }; target.Lines.Add("[" + fs.Name + "]" + nl); hub.Add(target); }
                while (target.Lines.Count > 1 && target.Lines[target.Lines.Count - 1].Trim().Length == 0) target.Lines.RemoveAt(target.Lines.Count - 1);
                if (target.Lines.Count > 0 && !target.Lines[target.Lines.Count - 1].EndsWith("\n")) target.Lines[target.Lines.Count - 1] += nl;
                target.Lines.Add("; --- from the community fix" + nl);
                foreach (var l in add) target.Lines.Add(l.TrimEnd('\r', '\n') + nl);
                target.Lines.Add(nl);
                log.Add("  d3dx.ini [" + fs.Name + "]: +" + add.Count + "  " + string.Join(", ", add.Select(l => l.Trim()).Take(4)));
            }

            var extra = fix.Where(s => s.Name.Length > 0 && !IsCore(s.Name) && !IsLines(s.Name) && !hubNames.Contains(s.Name)).ToList();
            var same = fix.Count(s => s.Name.Length > 0 && !IsCore(s.Name) && !IsLines(s.Name) && hubNames.Contains(s.Name));
            if (extra.Count > 0)
            {
                var tail = hub[hub.Count - 1];
                if (tail.Lines.Count > 0 && !tail.Lines[tail.Lines.Count - 1].EndsWith("\n")) tail.Lines[tail.Lines.Count - 1] += nl;
                var head = new Section { Name = "" };
                head.Lines.Add(nl);
                head.Lines.Add(";" + new string('-', 100) + nl);
                head.Lines.Add("; Game-specific sections of the community fix (merged by Geo Gaming Hub)" + nl);
                head.Lines.Add(";" + new string('-', 100) + nl);
                hub.Add(head);
                foreach (var s in extra)
                {
                    var c = new Section { Name = s.Name };
                    foreach (var l in s.Lines) c.Lines.Add(l.TrimEnd('\r', '\n') + nl);
                    hub.Add(c);
                }
                var kinds = extra.GroupBy(s => Regex.Match(s.Name, "^[A-Za-z]+").Value).OrderBy(g => g.Key)
                    .Select(g => g.Key + " x" + g.Count());
                log.Add(Lang.T("  d3dx.ini: 게임 전용 섹션 ") + extra.Count + Lang.T("개 추가 (") + string.Join(", ", kinds) + ")");
            }
            if (same > 0) log.Add(Lang.T("  d3dx.ini: 허브에도 있는 섹션 ") + same + Lang.T("개는 허브 것을 둠"));
            var skipped = fix.Where(s => IsCore(s.Name)).Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (skipped.Count > 0) log.Add(Lang.T("  d3dx.ini: 픽스의 기본 설정 섹션은 넣지 않음 (허브 설정 유지): ") + string.Join(", ", skipped));

            var sb = new StringBuilder();
            foreach (var s in hub) foreach (var l in s.Lines) sb.Append(l);
            File.WriteAllText(hubIni, sb.ToString(), Raw);
            return log;
        }

        // everything of the fix into a game folder the hub's package has just been copied into (full install)
        public static List<string> Merge(Info fix, string gameDir, string pkgSrc, int bits)
        {
            var log = new List<string>();
            log.Add(Lang.T("  커뮤니티 픽스: ") + fix.Name);
            string dx = System.IO.Path.Combine(gameDir, "d3dx.ini"), fdx = System.IO.Path.Combine(fix.FixRoot, "d3dx.ini");
            if (File.Exists(dx) && File.Exists(fdx)) log.AddRange(MergeD3dx(dx, fdx));
            else log.Add(Lang.T("  d3dx.ini가 게임 폴더나 픽스에 없어 합치지 않음"));

            int copied = 0, kept = 0;
            foreach (var sub in new[] { "ShaderFixes", "ShaderFixesDM" })
            {
                string s = System.IO.Path.Combine(fix.FixRoot, sub);
                if (!Directory.Exists(s)) continue;
                string pkgSub = System.IO.Path.Combine(pkgSrc, sub);
                var pkgNames = new HashSet<string>(Directory.Exists(pkgSub) ? Directory.GetFileSystemEntries(pkgSub).Select(System.IO.Path.GetFileName) : new string[0], StringComparer.OrdinalIgnoreCase);
                string dst = System.IO.Path.Combine(gameDir, sub);
                Directory.CreateDirectory(dst);
                foreach (var f in Directory.GetFiles(s))
                {
                    if (pkgNames.Contains(System.IO.Path.GetFileName(f))) { kept++; continue; }
                    string to = System.IO.Path.Combine(dst, System.IO.Path.GetFileName(f));
                    if (File.Exists(to)) File.SetAttributes(to, FileAttributes.Normal);
                    File.Copy(f, to, true);
                    try { File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(f)); } catch { }
                    copied++;
                }
                foreach (var d in Directory.GetDirectories(s))
                {
                    if (pkgNames.Contains(System.IO.Path.GetFileName(d))) { kept++; continue; }
                    Util.CopyDir(d, System.IO.Path.Combine(dst, System.IO.Path.GetFileName(d)), null);
                    copied += Directory.GetFiles(d, "*", SearchOption.AllDirectories).Length;
                }
            }
            log.Add(Lang.T("  셰이더 파일: 픽스에서 ") + copied + Lang.T("개 복사, 허브 패키지에 같은 이름이 있는 ") + kept + Lang.T("개는 허브 것을 둠"));

            var drivers = Drivers(fix, System.IO.Path.Combine(pkgSrc, "d3d11.dll"), bits);
            var drv = Chosen(fix, drivers);
            if (drv != null && !drv.Hub)
            {
                string to = System.IO.Path.Combine(gameDir, "d3d11.dll");
                if (File.Exists(to)) File.SetAttributes(to, FileAttributes.Normal);
                File.Copy(drv.Path, to, true);
                log.Add(Lang.T("  geo-11 드라이버: ") + drv.Label);
            }
            else if (drv != null) log.Add(Lang.T("  geo-11 드라이버: ") + drv.Label);
            return log;
        }
    }
}
