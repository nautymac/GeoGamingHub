// Vulkan games: VK3D is a vulkan-1.dll proxy (64-bit) that renders the game for both eyes and weaves them itself
// with the SR SDK (through D3D11 interop) - no geo-11, no SRWeave. The VK3D package holds
//   VK3D\x64\vulkan-1.dll, VK3D\VK3D.ini
//   VK3D\profiles\<exe name>\VK3D_fix\...   per-game shader fix (Quake II RTX), optional
//   VK3D\profiles\<exe name>\preset.ini     VK3D.ini values for that game ([Section] key = value), optional
// Games without a fix use VK3D's generic stereo (every vertex shader shifted per eye, right eye from twin targets).
// Experimental: a 64-bit DirectX 9/11 game can be run through DXVK (D3D -> Vulkan) and get VK3D on top; the DXVK
// package holds DXVK\x64\{d3d9,d3d10core,d3d11,dxgi}.dll; all four go in, because the API is often not known from the
// exe (Unity players name d3d9, d3d11 and d3d12) and DXVK takes whichever the game loads. geo-11 is usually the better
// choice for those games.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GeoGamingHub
{
    static class VkOps
    {
        public const string Dll = "vulkan-1.dll";
        public const string IniName = "VK3D.ini";
        public const string FixDirName = "VK3D_fix";
        const string BackupSuffix = ".geo11sr_bak";

        // what the wizard sets in VK3D.ini; everything else keeps the file's values
        public sealed class Options
        {
            public bool Sr = true;              // [Stereo] output = auto (SR weaving, SBS without an SR runtime)
            public string Mode = "sbs";         // output when Sr is off: sbs, tab
            public bool Swap;                   // [Stereo] swap_eyes
            public decimal Separation = 25;     // [Stereo] separation (percent of max_separation)
            public bool Auto = true;            // [Stereo] convergence = auto
            public decimal Convergence = 30;    // [Stereo] convergence when Auto is off (game units)
            public bool Dxvk;                   // DirectX game through DXVK (experimental)
        }

        public static readonly string[] Modes = { "sbs", "tab" };

        public static string PackageDll(string pkgDir) { return Path.Combine(pkgDir, "x64", Dll); }

        static string Product(string dll)
        {
            try { return File.Exists(dll) ? FileVersionInfo.GetVersionInfo(dll).ProductName : null; }
            catch { return null; }
        }

        // VK3D's vulkan-1.dll carries the product name "VK3D"; the Vulkan loader does not
        public static bool IsVk3d(string dll) { return Product(dll) == "VK3D"; }
        public static bool IsDxvk(string dll) { return Product(dll) == "DXVK"; }

        public static bool Installed(string gameDir) { return gameDir != null && IsVk3d(Path.Combine(gameDir, Dll)); }

        public static string Version(string dll)
        {
            try { return IsVk3d(dll) ? FileVersionInfo.GetVersionInfo(dll).ProductVersion : null; }
            catch { return null; }
        }

        // a Vulkan game imports vulkan-1.dll (Quake II RTX does); games that also import Direct3D have both renderers
        public static bool ImportsVulkan(string exe)
        {
            try { return Util.PeImports(exe).Contains(Dll); }
            catch { return false; }
        }

        public static bool ImportsDirect3D(string exe)
        {
            try
            {
                var imports = Util.PeImports(exe);
                return imports.Contains("d3d9.dll") || imports.Contains("d3d11.dll") || imports.Contains("dxgi.dll") || imports.Contains("d3d12.dll");
            }
            catch { return false; }
        }

        // ---- per-game profiles (VK3D\profiles\<exe name without extension, lower case>)
        public static string Profile(string pkgDir, string exe)
        {
            if (pkgDir == null || exe == null) return null;
            string d = Path.Combine(pkgDir, "profiles", Path.GetFileNameWithoutExtension(exe).ToLowerInvariant());
            return Directory.Exists(d) ? d : null;
        }

        static IEnumerable<string[]> PresetLines(string profile)
        {
            string f = profile == null ? null : Path.Combine(profile, "preset.ini");
            if (f == null || !File.Exists(f)) yield break;
            string section = null;
            foreach (var raw in File.ReadAllLines(f, Encoding.Default))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith(";")) continue;
                if (l.StartsWith("[") && l.EndsWith("]")) { section = l.Substring(1, l.Length - 2).Trim(); continue; }
                int eq = l.IndexOf('=');
                if (section != null && eq > 0) yield return new[] { section, l.Substring(0, eq).Trim(), l.Substring(eq + 1).Trim() };
            }
        }

        static decimal? Num(string v)
        {
            decimal d;
            return v != null && decimal.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : (decimal?)null;
        }

        // the wizard's values from a VK3D.ini (the game's, or the package's defaults with the game's preset on top)
        public static Options Read(string ini, string profile = null)
        {
            var o = new Options();
            Func<string, string, string> get = (sec, key) =>
            {
                string v = GlOps.Get(ini, sec, key);
                foreach (var p in PresetLines(profile))
                    if (string.Equals(p[0], sec, StringComparison.OrdinalIgnoreCase) && string.Equals(p[1], key, StringComparison.OrdinalIgnoreCase)) v = p[2];
                return v;
            };
            string s = get("Stereo", "output");
            if (s != null)
            {
                s = s.Trim().ToLowerInvariant();
                o.Sr = s == "auto" || s == "sr";
                if (!o.Sr && Modes.Contains(s)) o.Mode = s;
            }
            s = get("Stereo", "swap_eyes"); if (s != null) o.Swap = s.Trim() != "0";
            var d = Num(get("Stereo", "separation")); if (d.HasValue) o.Separation = d.Value;
            s = get("Stereo", "convergence");
            if (s != null && s.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                o.Auto = true;
                d = Num(get("Stereo", "auto_start"));   // where auto-convergence last settled
                if (d.HasValue && d.Value > 0) o.Convergence = d.Value;
            }
            else if ((d = Num(s)).HasValue) { o.Auto = false; o.Convergence = d.Value; }
            return o;
        }

        public static void Write(string ini, Options o)
        {
            GlOps.Set(ini, "Stereo", "output", o.Sr ? "auto" : o.Mode);
            GlOps.Set(ini, "Stereo", "swap_eyes", o.Swap ? "1" : "0");
            GlOps.Set(ini, "Stereo", "separation", o.Separation.ToString("0.##", CultureInfo.InvariantCulture));
            GlOps.Set(ini, "Stereo", "convergence", o.Auto ? "auto" : o.Convergence.ToString("0.####", CultureInfo.InvariantCulture));
        }

        public static string Describe(Options o)
        {
            return "output=" + (o.Sr ? "auto" : o.Mode) + " swap_eyes=" + (o.Swap ? 1 : 0) +
                   " separation=" + o.Separation.ToString("0.##", CultureInfo.InvariantCulture) +
                   " convergence=" + (o.Auto ? "auto" : o.Convergence.ToString("0.####", CultureInfo.InvariantCulture));
        }

        // ---- DXVK (experimental route for 64-bit DirectX 9 / 11 games)
        public static readonly string[] DxvkFiles = { "d3d9.dll", "d3d11.dll", "dxgi.dll", "d3d10core.dll" };

        public static bool DxvkInstalled(string gameDir)
        {
            return gameDir != null && new[] { "d3d9.dll", "d3d11.dll", "dxgi.dll" }.Any(n => IsDxvk(Path.Combine(gameDir, n)));
        }

        // geo-11 / SRWeave / dgVoodoo / ReShade DLLs that DXVK would replace (DXVK cannot sit under them)
        public static string DxvkConflict(string gameDir)
        {
            foreach (var n in new[] { "d3d9.dll", "d3d11.dll", "dxgi.dll" })
            {
                string f = Path.Combine(gameDir, n);
                if (File.Exists(f) && !IsDxvk(f)) return n;
            }
            return null;
        }

        static void CopyKeepingForeign(string src, string dst, List<string> log)
        {
            if (File.Exists(dst) && !IsVk3d(dst) && !IsDxvk(dst))
            {
                File.Copy(dst, dst + BackupSuffix, true);
                log.Add("  " + Path.GetFileName(dst) + Lang.T(" (다른 파일) → ") + Path.GetFileName(dst) + BackupSuffix);
            }
            if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal);
            File.Copy(src, dst, true);
            File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
            {
                string t = Path.Combine(dst, Path.GetFileName(f));
                if (File.Exists(t)) File.SetAttributes(t, FileAttributes.Normal);
                File.Copy(f, t, true);
                File.SetLastWriteTimeUtc(t, File.GetLastWriteTimeUtc(f));
            }
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        public static List<string> Install(string pkgDir, string dxvkDir, string exe, string gameDir, Options o)
        {
            var log = new List<string>();
            if (Util.ExeBits(exe) == 32) throw new InvalidOperationException(Lang.T("VK3D는 64비트 게임만 지원합니다."));
            string src = PackageDll(pkgDir);
            if (!File.Exists(src)) throw new FileNotFoundException(Lang.T("VK3D 파일이 패키지에 없습니다: ") + src);
            string api = o.Dxvk ? Util.DirectXOf(exe) : null;
            if (o.Dxvk)
            {
                if (api == null || ImportsVulkan(exe)) throw new InvalidOperationException(Lang.T("DXVK는 DirectX 9 / 11 게임에만 씁니다."));   // a Vulkan game names d3d11/dxgi too (Quake II RTX)
                string conflict = DxvkConflict(gameDir);
                if (conflict != null) throw new InvalidOperationException(conflict + Lang.T("이(가) 게임 폴더에 있습니다(geo-11, SR 위빙, dgVoodoo 또는 ReShade). DXVK는 그 위에 설치할 수 없으니 먼저 [3D 픽스 제거]하세요."));
            }

            string dst = Path.Combine(gameDir, Dll);
            CopyKeepingForeign(src, dst, log);
            log.Add("  VK3D " + (Version(src) ?? "?") + " x64 vulkan-1.dll → " + gameDir);

            string ini = Path.Combine(gameDir, IniName);
            bool fresh = !File.Exists(ini);
            if (fresh)
            {
                string pkgIni = Path.Combine(pkgDir, IniName);
                if (!File.Exists(pkgIni)) throw new FileNotFoundException(Lang.T("VK3D 파일이 패키지에 없습니다: ") + pkgIni);
                File.Copy(pkgIni, ini);
                log.Add(Lang.T("  VK3D.ini 복사 (패키지 기본값)"));
            }
            else log.Add(Lang.T("  VK3D.ini가 이미 있어 그대로 두고 아래 값만 바꿈 (게임 안에서 저장한 값 유지)"));

            string profile = Profile(pkgDir, exe);
            if (profile != null)
            {
                string fix = Path.Combine(profile, FixDirName);
                if (Directory.Exists(fix))
                {
                    CopyDir(fix, Path.Combine(gameDir, FixDirName));
                    log.Add(Lang.T("  게임 전용 픽스 설치: ") + FixDirName + " (" + Path.GetFileName(profile) + ")");
                }
                if (fresh)
                    foreach (var p in PresetLines(profile)) GlOps.Set(ini, p[0], p[1], p[2]);
            }
            Write(ini, o);
            log.Add("  VK3D.ini: " + Describe(o));

            if (o.Dxvk)
            {
                foreach (var n in DxvkFiles)
                {
                    string f = Path.Combine(dxvkDir, "x64", n);
                    if (!File.Exists(f)) throw new FileNotFoundException(Lang.T("DXVK 파일이 패키지에 없습니다: ") + f);
                    CopyKeepingForeign(f, Path.Combine(gameDir, n), log);
                }
                log.Add("  DXVK (x64, " + api + "): " + string.Join(", ", DxvkFiles) + Lang.T(" → 게임 폴더 (실험적)"));
            }
            return log;
        }

        public static List<string> Remove(string gameDir)
        {
            var log = new List<string>();
            var dlls = new List<string> { Dll, "d3d9.dll", "d3d11.dll", "dxgi.dll", "d3d10core.dll" };
            foreach (var n in dlls)
            {
                string f = Path.Combine(gameDir, n);
                bool ours = n == Dll ? IsVk3d(f) : IsDxvk(f);
                if (ours) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); log.Add("  " + n + Lang.T(" 삭제")); }
                string bak = f + BackupSuffix;
                if (ours && File.Exists(bak) && !File.Exists(f)) { File.Move(bak, f); log.Add("  " + n + BackupSuffix + " → " + n + Lang.T(" 복원")); }
            }
            foreach (var n in new[] { IniName, "VK3D.log", "VK3D.ini.bak" })
            {
                string p = Path.Combine(gameDir, n);
                if (File.Exists(p)) { File.Delete(p); log.Add("  " + n + Lang.T(" 삭제")); }
            }
            string fix = Path.Combine(gameDir, FixDirName);
            if (Directory.Exists(fix)) { Directory.Delete(fix, true); log.Add("  " + FixDirName + Lang.T(" 삭제")); }
            int extra = 0;
            foreach (var pat in new[] { "VK3D_frame*.bmp", "*_d3d9.log", "*_d3d11.log", "*_dxgi.log" })   // frame dumps, DXVK logs
                foreach (var f in Directory.GetFiles(gameDir, pat)) { File.Delete(f); extra++; }
            if (extra > 0) log.Add(Lang.T("  VK3D 프레임 덤프·DXVK 로그 삭제: ") + extra);
            return log;
        }
    }
}
