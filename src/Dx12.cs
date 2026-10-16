// DirectX 12 games: DX12_3D is a d3d12.dll proxy (64-bit) that renders the game for both eyes and weaves them itself
// with the SR SDK's DX12 weaver - no geo-11, no SRWeave. The DX12_3D package holds
//   DX12_3D\x64\d3d12.dll, DX12_3D\DX12_3D.ini
//   DX12_3D\DX12_3D\{dxcompiler,dxil}.dll        DXC for the shader patch (installed as <game>\DX12_3D\)
//   DX12_3D\profiles\<exe name>\preset.ini        DX12_3D.ini values for that game ([Section] key = value), optional
// DX12_3D has no automatic convergence yet: the convergence is a fixed value (game units; Helldivers 2 ~ 2-5 metres).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GeoGamingHub
{
    static class Dx12Ops
    {
        public const string Dll = "d3d12.dll";
        public const string IniName = "DX12_3D.ini";
        public const string RuntimeDirName = "DX12_3D";   // dxcompiler.dll + dxil.dll next to the game
        public static readonly string[] RuntimeFiles = { "dxcompiler.dll", "dxil.dll" };
        const string BackupSuffix = ".geo11sr_bak";

        // what the wizard sets in DX12_3D.ini; everything else keeps the file's values
        public sealed class Options
        {
            public bool Sr = true;              // [Stereo] output = auto (SR weaving, SBS without an SR runtime)
            public string Mode = "sbs";         // output when Sr is off: sbs, tab
            public bool Swap;                   // [Stereo] swap_eyes
            public decimal Separation = 25;     // [Stereo] separation (percent of max_separation)
            public decimal Convergence = 100;   // [Stereo] convergence (game units)
        }

        public static readonly string[] Modes = { "sbs", "tab" };

        public static string PackageDll(string pkgDir) { return Path.Combine(pkgDir, "x64", Dll); }

        static string Product(string dll)
        {
            try { return File.Exists(dll) ? FileVersionInfo.GetVersionInfo(dll).ProductName : null; }
            catch { return null; }
        }

        // DX12_3D's d3d12.dll carries the product name "DX12_3D"; the system one does not
        public static bool IsDx12_3d(string dll) { return Product(dll) == "DX12_3D"; }

        public static bool Installed(string gameDir) { return gameDir != null && IsDx12_3d(Path.Combine(gameDir, Dll)); }

        public static string Version(string dll)
        {
            try { return IsDx12_3d(dll) ? FileVersionInfo.GetVersionInfo(dll).ProductVersion : null; }
            catch { return null; }
        }

        // A DirectX 12 game: imports d3d12.dll and not d3d11.dll / dxgi-only DX11, or (dynamic loading) names
        // d3d12.dll but not d3d11.dll. Games with both renderers stay with geo-11 (DX11) unless picked by hand.
        public static bool IsDx12Game(string exe)
        {
            try
            {
                var imports = Util.PeImports(exe);
                if (imports.Contains("d3d12.dll")) return !imports.Contains("d3d11.dll");
                if (imports.Contains("d3d11.dll") || imports.Contains("d3d9.dll")) return false;
                byte[] b = File.ReadAllBytes(exe);
                return Has(b, "d3d12.dll") && !Has(b, "d3d11.dll");
            }
            catch { return false; }
        }

        static bool Has(byte[] hay, string s)
        {
            foreach (var needle in new[] { Encoding.ASCII.GetBytes(s), Encoding.Unicode.GetBytes(s) })
                for (int i = 0; i <= hay.Length - needle.Length; i++)
                {
                    int j = 0;
                    while (j < needle.Length && (hay[i + j] | 0x20) == (needle[j] | 0x20)) j++;
                    if (j == needle.Length) return true;
                }
            return false;
        }

        // ---- per-game profiles (DX12_3D\profiles\<exe name without extension, lower case>)
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

        // the wizard's values from a DX12_3D.ini (the game's, or the package's defaults with the game's preset on top)
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
            if (s != null && s.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)) d = Num(get("Stereo", "auto_start"));
            else d = Num(s);
            if (d.HasValue && d.Value > 0) o.Convergence = d.Value;
            return o;
        }

        public static void Write(string ini, Options o)
        {
            GlOps.Set(ini, "Stereo", "output", o.Sr ? "auto" : o.Mode);
            GlOps.Set(ini, "Stereo", "swap_eyes", o.Swap ? "1" : "0");
            GlOps.Set(ini, "Stereo", "separation", o.Separation.ToString("0.##", CultureInfo.InvariantCulture));
            GlOps.Set(ini, "Stereo", "convergence", o.Convergence.ToString("0.####", CultureInfo.InvariantCulture));
        }

        public static string Describe(Options o)
        {
            return "output=" + (o.Sr ? "auto" : o.Mode) + " swap_eyes=" + (o.Swap ? 1 : 0) +
                   " separation=" + o.Separation.ToString("0.##", CultureInfo.InvariantCulture) +
                   " convergence=" + o.Convergence.ToString("0.####", CultureInfo.InvariantCulture);
        }

        public static List<string> Install(string pkgDir, string exe, string gameDir, Options o)
        {
            var log = new List<string>();
            if (Util.ExeBits(exe) == 32) throw new InvalidOperationException(Lang.T("DX12_3D는 64비트 게임만 지원합니다."));
            string src = PackageDll(pkgDir);
            if (!File.Exists(src)) throw new FileNotFoundException(Lang.T("DX12_3D 파일이 패키지에 없습니다: ") + src);
            string rtSrc = Path.Combine(pkgDir, RuntimeDirName);
            foreach (var n in RuntimeFiles)
                if (!File.Exists(Path.Combine(rtSrc, n))) throw new FileNotFoundException(Lang.T("DX12_3D 파일이 패키지에 없습니다: ") + Path.Combine(rtSrc, n));
            string pkgIni = Path.Combine(pkgDir, IniName);
            if (!File.Exists(pkgIni)) throw new FileNotFoundException(Lang.T("DX12_3D 파일이 패키지에 없습니다: ") + pkgIni);

            string dst = Path.Combine(gameDir, Dll);
            if (File.Exists(dst) && !IsDx12_3d(dst))
            {
                File.Copy(dst, dst + BackupSuffix, true);
                log.Add("  " + Dll + Lang.T(" (다른 파일) → ") + Dll + BackupSuffix);
            }
            if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal);
            File.Copy(src, dst, true);
            File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
            log.Add("  DX12_3D " + (Version(src) ?? "?") + " x64 d3d12.dll → " + gameDir);

            string rt = Path.Combine(gameDir, RuntimeDirName);
            Directory.CreateDirectory(rt);
            foreach (var n in RuntimeFiles)
            {
                string t = Path.Combine(rt, n);
                if (File.Exists(t)) File.SetAttributes(t, FileAttributes.Normal);
                File.Copy(Path.Combine(rtSrc, n), t, true);
            }
            log.Add(Lang.T("  셰이더 패치용 DXC: ") + RuntimeDirName + "\\" + string.Join(", ", RuntimeFiles));

            string ini = Path.Combine(gameDir, IniName);
            bool fresh = !File.Exists(ini);
            if (fresh)
            {
                File.Copy(pkgIni, ini);
                log.Add(Lang.T("  DX12_3D.ini 복사 (패키지 기본값)"));
                string profile = Profile(pkgDir, exe);
                if (profile != null)
                {
                    foreach (var p in PresetLines(profile)) GlOps.Set(ini, p[0], p[1], p[2]);
                    log.Add(Lang.T("  게임 전용 권장 값 적용: ") + Path.GetFileName(profile));
                }
            }
            else log.Add(Lang.T("  DX12_3D.ini가 이미 있어 그대로 두고 아래 값만 바꿈 (게임 안에서 저장한 값 유지)"));
            Write(ini, o);
            log.Add("  DX12_3D.ini: " + Describe(o));
            return log;
        }

        public static List<string> Remove(string gameDir)
        {
            var log = new List<string>();
            string f = Path.Combine(gameDir, Dll);
            if (IsDx12_3d(f))
            {
                File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); log.Add("  " + Dll + Lang.T(" 삭제"));
                string bak = f + BackupSuffix;
                if (File.Exists(bak)) { File.Move(bak, f); log.Add("  " + Dll + BackupSuffix + " → " + Dll + Lang.T(" 복원")); }
            }
            foreach (var n in new[] { IniName, "DX12_3D.log" })
            {
                string p = Path.Combine(gameDir, n);
                if (File.Exists(p)) { File.Delete(p); log.Add("  " + n + Lang.T(" 삭제")); }
            }
            string rt = Path.Combine(gameDir, RuntimeDirName);
            if (Directory.Exists(rt))
            {
                foreach (var n in RuntimeFiles) { string p = Path.Combine(rt, n); if (File.Exists(p)) { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); } }
                if (!Directory.EnumerateFileSystemEntries(rt).Any()) Directory.Delete(rt);
                log.Add("  " + RuntimeDirName + Lang.T("\\ (DXC) 삭제"));
            }
            string shaders = Path.Combine(gameDir, "DX12_3D_shaders");   // [Debug] dump_shaders
            if (Directory.Exists(shaders)) { Directory.Delete(shaders, true); log.Add(Lang.T("  DX12_3D_shaders 삭제")); }
            int extra = 0;
            foreach (var pat in new[] { "DX12_3D_frame*.bmp", "DX12_3D_pass_*.bmp" })   // frame / pass dumps
                foreach (var d in Directory.GetFiles(gameDir, pat)) { File.Delete(d); extra++; }
            if (extra > 0) log.Add(Lang.T("  DX12_3D 프레임 덤프 삭제: ") + extra);
            return log;
        }
    }
}
