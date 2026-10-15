// OpenGL games: OGL3D (https://github.com/nautymac/OGL3D-SR) is an opengl32.dll proxy that renders the game for both
// eyes and weaves them itself with the SR SDK - no geo-11, no SRWeave, no dgVoodoo. The OGL3D package holds
//   OGL3D\x64\opengl32.dll, OGL3D\x86\opengl32.dll, OGL3D\OGL3D.ini
// The installer copies the DLL that matches the exe's bitness plus the ini, and sets the few options the wizard
// shows. An existing OGL3D.ini is kept: it holds the values saved in-game (Ctrl+F7), the remembered auto-convergence
// start value and the shader choices of hunting mode.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GeoGamingHub
{
    static class GlOps
    {
        public const string Dll = "opengl32.dll";
        public const string IniName = "OGL3D.ini";
        const string BackupSuffix = ".geo11sr_bak";

        // what the wizard sets in OGL3D.ini; everything else keeps the file's values
        public sealed class Options
        {
            public bool Sr = true;              // [Stereo] output = auto (SR weaving, SBS without an SR runtime)
            public string Mode = "sbs";         // output when Sr is off: sbs, tab, left, right
            public bool Swap;                   // [Stereo] swap_eyes
            public bool Upscale;                // [Display] upscale
            public decimal Separation = 50;     // [Stereo] separation (percent)
            public bool Auto = true;            // [Stereo] convergence = auto
            public decimal Convergence = 3;     // [Stereo] convergence when Auto is off
        }

        public static readonly string[] Modes = { "sbs", "tab", "left", "right" };

        public static string PackageDll(string pkgDir, int bits)
        {
            return Path.Combine(pkgDir, bits == 64 ? "x64" : "x86", Dll);
        }

        // OGL3D's opengl32.dll carries the product name "OGL3D" in its version resource; the system one and other
        // wrappers (e.g. helifax's 3D Vision wrapper) do not
        public static bool IsOgl3d(string dll)
        {
            try { return File.Exists(dll) && FileVersionInfo.GetVersionInfo(dll).ProductName == "OGL3D"; }
            catch { return false; }
        }

        public static bool Installed(string gameDir) { return gameDir != null && IsOgl3d(Path.Combine(gameDir, Dll)); }

        public static string Version(string dll)
        {
            try { return IsOgl3d(dll) ? FileVersionInfo.GetVersionInfo(dll).ProductVersion : null; }
            catch { return null; }
        }

        // An OpenGL game imports opengl32.dll (all games verified with OGL3D do). An exe without any graphics import
        // that names opengl32.dll and a GL context function counts as OpenGL too, unless it also names Direct3D.
        public static bool IsOpenGL(string exe)
        {
            try
            {
                var imports = Util.PeImports(exe);
                bool dx = imports.Contains("d3d9.dll") || imports.Contains("d3d11.dll") || imports.Contains("dxgi.dll") || imports.Contains("d3d12.dll");
                if (imports.Contains(Dll)) return !dx;
                if (dx) return false;
                byte[] b = File.ReadAllBytes(exe);
                Func<string, bool> has = s => Contains(b, Encoding.ASCII.GetBytes(s)) || Contains(b, Encoding.Unicode.GetBytes(s));
                if (!has(Dll) || !(has("wglCreateContext") || has("SDL_GL_CreateContext") || has("glfwCreateWindow"))) return false;
                return !(has("d3d9.dll") || has("d3d11.dll") || has("dxgi.dll") || has("d3d12.dll"));
            }
            catch { return false; }
        }

        static bool Contains(byte[] hay, byte[] needle)
        {
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && (hay[i + j] | 0x20) == (needle[j] | 0x20)) j++;   // case-insensitive for ASCII letters
                if (j == needle.Length) return true;
            }
            return false;
        }

        // ---- OGL3D.ini: keys repeat across sections (enable is in [Stereo] and [Inject]), so read and write by section
        public static string Get(string ini, string section, string key)
        {
            if (!File.Exists(ini)) return null;
            bool inSec = false;
            foreach (var raw in File.ReadAllLines(ini, Encoding.Default))
            {
                string l = raw.Trim();
                var h = Regex.Match(l, @"^\[(.+?)\]$");
                if (h.Success) { inSec = string.Equals(h.Groups[1].Value, section, StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSec || l.StartsWith(";")) continue;
                int eq = l.IndexOf('=');
                if (eq > 0 && string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) return l.Substring(eq + 1).Trim();
            }
            return null;
        }

        // replaces the key's line in [section], or adds it at the end of that section (or a new section at the end)
        public static void Set(string ini, string section, string key, string value)
        {
            var lines = File.Exists(ini) ? File.ReadAllLines(ini, Encoding.Default).ToList() : new List<string>();
            int start = -1, end = lines.Count;
            for (int i = 0; i < lines.Count; i++)
            {
                var h = Regex.Match(lines[i].Trim(), @"^\[(.+?)\]$");
                if (!h.Success) continue;
                if (start >= 0) { end = i; break; }
                if (string.Equals(h.Groups[1].Value, section, StringComparison.OrdinalIgnoreCase)) start = i;
            }
            string line = key + " = " + value;
            if (start < 0)
            {
                if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(line);
            }
            else
            {
                int found = -1;
                for (int i = start + 1; i < end; i++)
                {
                    string l = lines[i].Trim();
                    int eq = l.IndexOf('=');
                    if (!l.StartsWith(";") && eq > 0 && string.Equals(l.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) { found = i; break; }
                }
                if (found >= 0) lines[found] = line;
                else
                {
                    int at = end;
                    while (at > start + 1 && lines[at - 1].Trim().Length == 0) at--;   // before the blank lines between sections
                    lines.Insert(at, line);
                }
            }
            File.WriteAllLines(ini, lines, Encoding.Default);
        }

        static decimal? Num(string v)
        {
            decimal d;
            return v != null && decimal.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : (decimal?)null;
        }

        // the wizard's values from an OGL3D.ini (the game's, or the package's defaults)
        public static Options Read(string ini)
        {
            var o = new Options();
            string v = Get(ini, "Stereo", "output");
            if (v != null)
            {
                v = v.Trim().ToLowerInvariant();
                o.Sr = v == "auto" || v == "sr";
                if (!o.Sr && Modes.Contains(v)) o.Mode = v;
            }
            v = Get(ini, "Stereo", "swap_eyes"); if (v != null) o.Swap = v.Trim() != "0";
            v = Get(ini, "Display", "upscale"); if (v != null) o.Upscale = v.Trim() != "0";
            var d = Num(Get(ini, "Stereo", "separation")); if (d.HasValue) o.Separation = d.Value;
            v = Get(ini, "Stereo", "convergence");
            if (v != null && v.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                o.Auto = true;
                d = Num(Get(ini, "Stereo", "auto_start"));   // where auto-convergence last settled
                if (d.HasValue && d.Value > 0) o.Convergence = d.Value;
            }
            else if ((d = Num(v)).HasValue) { o.Auto = false; o.Convergence = d.Value; }
            return o;
        }

        public static void Write(string ini, Options o)
        {
            Set(ini, "Stereo", "output", o.Sr ? "auto" : o.Mode);
            Set(ini, "Stereo", "swap_eyes", o.Swap ? "1" : "0");
            Set(ini, "Display", "upscale", o.Upscale ? "1" : "0");
            Set(ini, "Stereo", "separation", o.Separation.ToString("0.##", CultureInfo.InvariantCulture));
            Set(ini, "Stereo", "convergence", o.Auto ? "auto" : o.Convergence.ToString("0.####", CultureInfo.InvariantCulture));
        }

        public static string Describe(Options o)
        {
            return "output=" + (o.Sr ? "auto" : o.Mode) + " swap_eyes=" + (o.Swap ? 1 : 0) + " upscale=" + (o.Upscale ? 1 : 0) +
                   " separation=" + o.Separation.ToString("0.##", CultureInfo.InvariantCulture) +
                   " convergence=" + (o.Auto ? "auto" : o.Convergence.ToString("0.####", CultureInfo.InvariantCulture));
        }

        public static List<string> Install(string pkgDir, int bits, string gameDir, Options o)
        {
            var log = new List<string>();
            string src = PackageDll(pkgDir, bits);
            if (!File.Exists(src)) throw new FileNotFoundException(Lang.T("OGL3D 파일이 패키지에 없습니다: ") + src);
            string dst = Path.Combine(gameDir, Dll);
            if (File.Exists(dst) && !IsOgl3d(dst))
            {
                File.Copy(dst, dst + BackupSuffix, true);
                log.Add(Lang.T("  기존 opengl32.dll(OGL3D 아님, 다른 래퍼?)을 opengl32.dll.geo11sr_bak으로 보관"));
            }
            if (File.Exists(dst)) File.SetAttributes(dst, FileAttributes.Normal);
            File.Copy(src, dst, true);
            File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
            log.Add("  OGL3D " + (Version(src) ?? "?") + " " + (bits == 64 ? "x64" : "x86") + " opengl32.dll → " + gameDir);

            string ini = Path.Combine(gameDir, IniName);
            if (!File.Exists(ini))
            {
                string pkgIni = Path.Combine(pkgDir, IniName);
                if (!File.Exists(pkgIni)) throw new FileNotFoundException(Lang.T("OGL3D 파일이 패키지에 없습니다: ") + pkgIni);
                File.Copy(pkgIni, ini);
                log.Add(Lang.T("  OGL3D.ini 복사 (패키지 기본값)"));
            }
            else log.Add(Lang.T("  OGL3D.ini가 이미 있어 그대로 두고 아래 값만 바꿈 (게임 안에서 저장한 값·셰이더 선택 유지)"));
            Write(ini, o);
            log.Add("  OGL3D.ini: " + Describe(o));
            return log;
        }

        public static List<string> Remove(string gameDir)
        {
            var log = new List<string>();
            string dst = Path.Combine(gameDir, Dll);
            if (IsOgl3d(dst)) { File.SetAttributes(dst, FileAttributes.Normal); File.Delete(dst); log.Add(Lang.T("  OGL3D opengl32.dll 삭제")); }
            string bak = dst + BackupSuffix;
            if (File.Exists(bak) && !File.Exists(dst)) { File.Move(bak, dst); log.Add(Lang.T("  opengl32.dll.geo11sr_bak → opengl32.dll 복원")); }
            foreach (var n in new[] { IniName, "OGL3D.log" })
            {
                string p = Path.Combine(gameDir, n);
                if (File.Exists(p)) { File.Delete(p); log.Add("  " + n + Lang.T(" 삭제")); }
            }
            int dumps = 0;
            foreach (var pat in new[] { "OGL3D_frame*.bmp", "OGL3D_flicker*.bmp" })
                foreach (var f in Directory.GetFiles(gameDir, pat)) { File.Delete(f); dumps++; }
            if (dumps > 0) log.Add(Lang.T("  OGL3D 프레임 덤프 삭제: ") + dumps);
            return log;
        }
    }
}
