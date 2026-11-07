// DirectX 9 games: dgVoodoo2 (by Dege) translates D3D9 to D3D11, so geo-11's d3d11.dll (generic "Preferred" package)
// and SRWeave work underneath it. The Geo-11_DX9 package holds dgVoodoo2's D3D9.dll per version and bitness:
//   Geo-11_DX9\<version>\x86\D3D9.dll, Geo-11_DX9\<version>\x64\D3D9.dll   (2.79.1 is the safe default)
// The installer copies the matching D3D9.dll into the game folder and writes a minimal dgVoodoo.conf
// (everything not listed keeps dgVoodoo's defaults): D3D11 output, no watermark, output at desktop resolution
// so the swap chain is panel-sized for weaving.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GeoGamingHub
{
    static class Dx9Ops
    {
        public const string DefaultVersion = "2.79.1";
        const string ConfMarker = "; written by geo-11 SR installer";

        // version folders of the package, newest last, default first in the UI
        public static string[] Versions(string pkgDir)
        {
            if (!Directory.Exists(pkgDir)) return new string[0];
            var v = Directory.GetDirectories(pkgDir).Select(Path.GetFileName)
                .Where(n => Directory.Exists(Path.Combine(pkgDir, n, "x86")) || Directory.Exists(Path.Combine(pkgDir, n, "x64")))
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (v.Remove(DefaultVersion)) v.Insert(0, DefaultVersion);
            return v.ToArray();
        }

        public static string Dll(string pkgDir, string version, int bits)
        {
            return Path.Combine(pkgDir, version, bits == 64 ? "x64" : "x86", "D3D9.dll");
        }

        // dgVoodoo's D3D9.dll carries its name in the file; the real Microsoft d3d9.dll does not
        public static bool IsDgVoodoo(string dll)
        {
            try
            {
                if (!File.Exists(dll)) return false;
                byte[] b = File.ReadAllBytes(dll);
                foreach (var pat in new[] { Encoding.ASCII.GetBytes("dgVoodoo"), Encoding.Unicode.GetBytes("dgVoodoo") })
                    for (int i = 0; i <= b.Length - pat.Length; i++)
                    {
                        int j = 0;
                        while (j < pat.Length && b[i + j] == pat[j]) j++;
                        if (j == pat.Length) return true;
                    }
            }
            catch { }
            return false;
        }

        public static bool Installed(string gameDir) { return IsDgVoodoo(Path.Combine(gameDir, "D3D9.dll")); }

        public static string InstalledVersion(string gameDir, string pkgDir, int bits)
        {
            string g = Path.Combine(gameDir, "D3D9.dll");
            if (!File.Exists(g)) return null;
            foreach (var v in Versions(pkgDir)) if (Util.SameFile(g, Dll(pkgDir, v, bits))) return v;
            return Installed(gameDir) ? "?" : null;
        }

        // true = "Resolution = desktop" in dgVoodoo.conf (null when there is no conf)
        public static bool? DesktopResolution(string gameDir)
        {
            string c = Path.Combine(gameDir, "dgVoodoo.conf");
            if (!File.Exists(c)) return null;
            bool inDx = false;
            foreach (var raw in File.ReadAllLines(c, Encoding.Default))
            {
                string l = raw.Trim();
                if (l.StartsWith("[")) { inDx = l.Equals("[DirectX]", StringComparison.OrdinalIgnoreCase); continue; }
                if (inDx && l.StartsWith("Resolution", StringComparison.OrdinalIgnoreCase) && l.Contains("="))
                    return l.Substring(l.IndexOf('=') + 1).Trim().StartsWith("desktop", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public static readonly string[] VramChoices = { "4GB", "2GB" };   // dgVoodoo "VRAM = 4GB"; the default 256 MB makes games crash at panel resolution

        // "4GB" / "2GB" / other value as written in dgVoodoo.conf [DirectX] VRAM, null when there is no conf
        public static string Vram(string gameDir)
        {
            string c = Path.Combine(gameDir, "dgVoodoo.conf");
            if (!File.Exists(c)) return null;
            bool inDx = false;
            foreach (var raw in File.ReadAllLines(c, Encoding.Default))
            {
                string l = raw.Trim();
                if (l.StartsWith("[")) { inDx = l.Equals("[DirectX]", StringComparison.OrdinalIgnoreCase); continue; }
                if (inDx && l.StartsWith("VRAM", StringComparison.OrdinalIgnoreCase) && l.Contains("=")) return l.Substring(l.IndexOf('=') + 1).Trim();
            }
            return null;
        }

        public static List<string> Install(string pkgDir, string version, int bits, string gameDir, bool desktopRes) { return Install(pkgDir, version, bits, gameDir, desktopRes, VramChoices[0]); }

        public static List<string> Install(string pkgDir, string version, int bits, string gameDir, bool desktopRes, string vram)
        {
            var log = new List<string>();
            string src = Dll(pkgDir, version, bits);
            if (!File.Exists(src)) throw new FileNotFoundException(Lang.T("dgVoodoo2 파일이 패키지에 없습니다: ") + src);
            string dst = Path.Combine(gameDir, "D3D9.dll");
            if (File.Exists(dst) && !IsDgVoodoo(dst)) { File.Copy(dst, dst + ".geo11sr_bak", true); log.Add(Lang.T("  기존 D3D9.dll(dgVoodoo 아님)을 D3D9.dll.geo11sr_bak으로 보관")); }
            File.Copy(src, dst, true);
            File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
            log.Add(Lang.T("  dgVoodoo2 ") + version + " " + (bits == 64 ? "x64" : "x86") + " D3D9.dll → " + gameDir);

            string conf = Path.Combine(gameDir, "dgVoodoo.conf");
            bool ours = File.Exists(conf) && File.ReadAllText(conf, Encoding.Default).Contains(ConfMarker);
            if (!File.Exists(conf) || ours)
            {
                var sb = new StringBuilder();
                sb.AppendLine(ConfMarker + " - options not listed keep dgVoodoo's defaults; edit freely");
                sb.AppendLine("[General]");
                sb.AppendLine("OutputAPI = d3d11_fl11_0");
                sb.AppendLine("FullScreenMode = true");
                sb.AppendLine();
                sb.AppendLine("[DirectX]");
                sb.AppendLine("; desktop = render at the desktop (SR panel) resolution so the lenticular pattern lines up; unforced = the game's own resolution");
                sb.AppendLine("Resolution = " + (desktopRes ? "desktop" : "unforced"));
                sb.AppendLine("; video memory reported to the game; dgVoodoo's default 256 MB is too small at panel resolution and games crash");
                sb.AppendLine("VRAM = " + (string.IsNullOrEmpty(vram) ? VramChoices[0] : vram));
                sb.AppendLine("dgVoodooWatermark = false");
                sb.AppendLine("AppControlledScreenMode = true");
                sb.AppendLine("DisableAltEnterToToggleScreenMode = true");
                File.WriteAllText(conf, sb.ToString(), Encoding.ASCII);
                log.Add(Lang.T("  dgVoodoo.conf 작성 (D3D11 출력, 워터마크 끔, VRAM ") + (string.IsNullOrEmpty(vram) ? VramChoices[0] : vram) + Lang.T(", 해상도 ") + (desktopRes ? Lang.T("바탕화면") : Lang.T("게임 설정")) + ")");
            }
            else log.Add(Lang.T("  dgVoodoo.conf가 이미 있어 그대로 둠 (설치기가 만든 것이 아님)"));
            return log;
        }

        // ---- per-game shader fixes for games under dgVoodoo2 ------------------------------------------------
        // data\dx9fixes\<exe file name>\<dgVoodoo version>\ShaderFixesDM\*.txt  (and optionally ShaderFixes\*)
        // geo-11 matches fixes by the hash of the vertex shaders dgVoodoo generates, so a fix only fits the dgVoodoo
        // build it was made with; the version folder says which one. Uninstall.bat removes ShaderFixesDM / ShaderFixes.
        public static string FixDir(string fixesRoot, string exeName)
        {
            if (string.IsNullOrEmpty(fixesRoot) || string.IsNullOrEmpty(exeName)) return null;
            string d = Path.Combine(fixesRoot, exeName.ToLowerInvariant());
            return Directory.Exists(d) ? d : null;
        }

        public static string[] FixVersions(string fixesRoot, string exeName)
        {
            string d = FixDir(fixesRoot, exeName);
            if (d == null) return new string[0];
            return Directory.GetDirectories(d).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        public static List<string> InstallFixes(string fixesRoot, string exeName, string version, string gameDir)
        {
            var log = new List<string>();
            var vers = FixVersions(fixesRoot, exeName);
            if (vers.Length == 0) return log;
            if (!vers.Contains(version))
            {
                log.Add(Lang.T("  게임별 픽스: 이 게임의 픽스는 dgVoodoo2 ") + string.Join(", ", vers) + Lang.T("용입니다. 다른 버전에서는 셰이더 해시가 달라 넣지 않았습니다."));
                return log;
            }
            string src = Path.Combine(FixDir(fixesRoot, exeName), version);
            int n = 0;
            foreach (var sub in new[] { "ShaderFixesDM", "ShaderFixes" })
            {
                string s = Path.Combine(src, sub);
                if (!Directory.Exists(s)) continue;
                string dst = Path.Combine(gameDir, sub);
                Directory.CreateDirectory(dst);
                foreach (var f in Directory.GetFiles(s)) { File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true); n++; }
            }
            log.Add(Lang.T("  게임별 픽스: ") + n + Lang.T("개 파일 설치 (dgVoodoo2 ") + version + ")");
            return log;
        }

        // settings that break games under dgVoodoo: geo-11 0.6.109's HUD detection also marks world shaders as HUD
        // (scene drawn flat), export_hlsl / export_shaders run every shader through decompile + recompile (broken
        // transparent effects), and calls=1 logs every draw (hundreds of MB per session through dgVoodoo)
        public static List<string> GuardSettings(string gameDir)
        {
            var log = new List<string>();
            var changed = new List<string>();
            string dx = Path.Combine(gameDir, "d3dx.ini"), dm = Path.Combine(gameDir, "d3dxdm.ini");
            foreach (var k in new[] { new[] { dx, "export_hlsl", "Rendering" }, new[] { dx, "export_shaders", "Rendering" }, new[] { dx, "calls", "Logging" }, new[] { dm, "dm_hud_detection", "Stereo" } })
            {
                if (!File.Exists(k[0])) continue;
                string v = Ini.Get(k[0], k[1]);
                if (v == null || v.Trim() == "0") continue;
                Ini.Set(k[0], k[1], "0", k[2]);
                changed.Add(Path.GetFileName(k[0]) + " " + k[1] + "=0");
            }
            if (changed.Count > 0) log.Add(Lang.T("  DX9 안전 설정: ") + string.Join(", ", changed));
            return log;
        }

        public static List<string> Remove(string gameDir)
        {
            var log = new List<string>();
            string dst = Path.Combine(gameDir, "D3D9.dll");
            if (IsDgVoodoo(dst)) { File.Delete(dst); log.Add(Lang.T("  dgVoodoo2 D3D9.dll 삭제")); }
            string bak = dst + ".geo11sr_bak";
            if (File.Exists(bak) && !File.Exists(dst)) { File.Move(bak, dst); log.Add(Lang.T("  D3D9.dll.geo11sr_bak → D3D9.dll 복원")); }
            string conf = Path.Combine(gameDir, "dgVoodoo.conf");
            if (File.Exists(conf) && File.ReadAllText(conf, Encoding.Default).Contains(ConfMarker)) { File.Delete(conf); log.Add(Lang.T("  dgVoodoo.conf 삭제")); }
            return log;
        }
    }
}
