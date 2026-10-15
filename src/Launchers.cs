// Launch arguments in the launchers the game is started from. UE4 Universal Fix 2 needs the game started with
// "-dx11" (its own start scripts do `start "" "<exe>" -dx11`), so when the game is launched from Steam or from
// Heroic (Epic / GOG) the argument has to be stored in that launcher's per-game settings:
//   Steam  : <Steam>\userdata\<user>\config\localconfig.vdf  ->  UserLocalConfigStore/Software/Valve/Steam/apps/<appid>/LaunchOptions
//            (Steam rewrites the file while it runs, so Steam is shut down first, the file edited, Steam started again)
//   Heroic : %APPDATA%\heroic\GamesConfig\<appName>.json  ->  { "<appName>": { "launcherArgs": "-dx11" }, "version": "v0" }
//            appName comes from legendaryConfig\legendary\installed.json (Epic) or gog_store\installed.json (GOG)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GeoGamingHub
{
    static class Launchers
    {
        public const string Dx11 = "-dx11";

        // ---- Heroic ---------------------------------------------------------------------------------
        static string HeroicDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic"); } }

        static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }
        static bool Under(string dir, string root)
        {
            string d = Path.GetFullPath(dir).TrimEnd('\\') + "\\", r = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            return d.StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }

        // Epic (legendary) or GOG game managed by Heroic whose install folder contains gameDir
        public static bool FindHeroicApp(string gameDir, out string appName, out string store, out string title)
        {
            appName = store = title = null;
            try
            {
                var js = new JavaScriptSerializer();
                string leg = Path.Combine(HeroicDir, "legendaryConfig", "legendary", "installed.json");
                if (File.Exists(leg))
                {
                    var d = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(leg, Encoding.UTF8));
                    foreach (var kv in d)
                    {
                        var g = kv.Value as Dictionary<string, object>;
                        if (g == null) continue;
                        object ip; if (!g.TryGetValue("install_path", out ip) || ip == null) continue;
                        string p = ip.ToString();
                        if (Directory.Exists(p) && (SamePath(p, gameDir) || Under(gameDir, p)))
                        {
                            appName = kv.Key; store = "Epic"; object t; title = g.TryGetValue("title", out t) && t != null ? t.ToString() : kv.Key;
                            return true;
                        }
                    }
                }
                string gog = Path.Combine(HeroicDir, "gog_store", "installed.json");
                if (File.Exists(gog))
                {
                    var d = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(gog, Encoding.UTF8));
                    object arr;
                    d.TryGetValue("installed", out arr);
                    var list = arr as System.Collections.ArrayList;   // JavaScriptSerializer gives ArrayList for JSON arrays
                    if (list != null)
                        foreach (var o in list)
                        {
                            var g = o as Dictionary<string, object>;
                            if (g == null) continue;
                            object ip, an; if (!g.TryGetValue("install_path", out ip) || ip == null || !g.TryGetValue("appName", out an) || an == null) continue;
                            string p = ip.ToString();
                            if (Directory.Exists(p) && (SamePath(p, gameDir) || Under(gameDir, p)))
                            { appName = an.ToString(); store = "GOG"; title = an.ToString(); return true; }
                        }
                }
            }
            catch { }
            return false;
        }
        public static string HeroicArgs(string appName)
        {
            try
            {
                string f = Path.Combine(HeroicDir, "GamesConfig", appName + ".json");
                if (!File.Exists(f)) return null;
                var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(f, Encoding.UTF8));
                object g; if (root == null || !root.TryGetValue(appName, out g)) return null;
                var gd = g as Dictionary<string, object>; object a;
                return gd != null && gd.TryGetValue("launcherArgs", out a) && a != null ? a.ToString() : null;
            }
            catch { return null; }
        }

        // adds arg to the game's launcherArgs (keeps whatever else is there)
        public static string SetHeroicArgs(string appName, string arg)
        {
            string dir = Path.Combine(HeroicDir, "GamesConfig");
            Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, appName + ".json");
            var js = new JavaScriptSerializer();
            Dictionary<string, object> root = null;
            if (File.Exists(f)) { try { root = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(f, Encoding.UTF8)); } catch { root = null; } }
            if (root == null) root = new Dictionary<string, object>();
            object g; var gd = root.TryGetValue(appName, out g) ? g as Dictionary<string, object> : null;
            if (gd == null) { gd = new Dictionary<string, object>(); root[appName] = gd; }
            object cur; string args = gd.TryGetValue("launcherArgs", out cur) && cur != null ? cur.ToString() : "";
            if (!HasArg(args, arg)) args = (args.Trim() + " " + arg).Trim();
            gd["launcherArgs"] = args;
            if (!root.ContainsKey("version")) root["version"] = "v0";
            if (File.Exists(f)) File.Copy(f, f + ".bak", true);
            File.WriteAllText(f, js.Serialize(root), new UTF8Encoding(false));
            return args;
        }

        static bool HasArg(string args, string arg)
        {
            return (args ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Any(a => string.Equals(a, arg, StringComparison.OrdinalIgnoreCase));
        }

        // ---- Steam ----------------------------------------------------------------------------------
        public static string SteamPath()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var p = k != null ? k.GetValue("SteamPath") as string : null;
                    if (!string.IsNullOrEmpty(p)) return p.Replace('/', '\\');
                }
            }
            catch { }
            return null;
        }
        static string SteamExe()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var p = k != null ? k.GetValue("SteamExe") as string : null;
                    if (!string.IsNullOrEmpty(p)) return p.Replace('/', '\\');
                }
            }
            catch { }
            string sp = SteamPath();
            return sp != null ? Path.Combine(sp, "steam.exe") : null;
        }

        // the localconfig.vdf files that mention the app (normally one: the account that owns it)
        public static List<string> SteamLocalConfigs(string appId)
        {
            var r = new List<string>();
            string sp = SteamPath();
            if (sp == null) return r;
            string ud = Path.Combine(sp, "userdata");
            if (!Directory.Exists(ud)) return r;
            foreach (var u in Directory.GetDirectories(ud))
            {
                string f = Path.Combine(u, "config", "localconfig.vdf");
                if (File.Exists(f) && File.ReadAllText(f, Encoding.UTF8).Contains("\"" + appId + "\"")) r.Add(f);
            }
            return r;
        }

        public static string SteamLaunchOptions(string appId)
        {
            foreach (var f in SteamLocalConfigs(appId))
            {
                var lines = File.ReadAllLines(f, Encoding.UTF8).ToList();
                int open, close; string indent;
                if (FindAppBlock(lines, appId, out open, out close, out indent))
                    for (int i = open + 1; i < close; i++)
                    {
                        string v; if (IsKeyLine(lines[i], "LaunchOptions", out v)) return v;
                    }
            }
            return null;
        }

        static bool IsKeyLine(string line, string key, out string value)
        {
            value = null;
            string t = line.Trim();
            if (!t.StartsWith("\"" + key + "\"", StringComparison.OrdinalIgnoreCase)) return false;
            int q1 = t.IndexOf('"', key.Length + 2); if (q1 < 0) return false;
            int q2 = t.LastIndexOf('"'); if (q2 <= q1) return false;
            value = t.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return true;
        }

        // finds "apps" { ... "<appId>" { ... } } ; open = index of the "{" line of the app block, close = its "}" line
        static bool FindAppBlock(List<string> lines, string appId, out int open, out int close, out string indent)
        {
            open = close = -1; indent = "";
            int apps = lines.FindIndex(l => l.Trim() == "\"apps\"");
            if (apps < 0 || apps + 1 >= lines.Count || lines[apps + 1].Trim() != "{") return false;
            int depth = 0;
            for (int i = apps + 1; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t == "{") { depth++; continue; }
                if (t == "}") { depth--; if (depth == 0) return false; continue; }
                if (depth == 1 && t == "\"" + appId + "\"" && i + 1 < lines.Count && lines[i + 1].Trim() == "{")
                {
                    open = i + 1; indent = lines[i].Substring(0, lines[i].Length - lines[i].TrimStart().Length);
                    int d = 0;
                    for (int j = open; j < lines.Count; j++)
                    {
                        string u = lines[j].Trim();
                        if (u == "{") d++;
                        else if (u == "}") { d--; if (d == 0) { close = j; return true; } }
                    }
                    return false;
                }
            }
            return false;
        }

        static int AppsBlockClose(List<string> lines)
        {
            int apps = lines.FindIndex(l => l.Trim() == "\"apps\"");
            if (apps < 0) return -1;
            int depth = 0;
            for (int i = apps + 1; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t == "{") depth++;
                else if (t == "}") { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        // adds arg to the app's LaunchOptions in one localconfig.vdf; returns the resulting options, null = nothing to do / not found
        public static string EditLocalConfig(string file, string appId, string arg)
        {
            var lines = File.ReadAllLines(file, Encoding.UTF8).ToList();
            int open, close; string indent, resulting;
            if (FindAppBlock(lines, appId, out open, out close, out indent))
            {
                int at = -1; string cur = "";
                for (int i = open + 1; i < close; i++) { string v; if (IsKeyLine(lines[i], "LaunchOptions", out v)) { at = i; cur = v; break; } }
                string args = HasArg(cur, arg) ? cur : (cur.Trim() + " " + arg).Trim();
                string line = indent + "\t\"LaunchOptions\"\t\t\"" + args.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                if (at >= 0) { if (lines[at] == line) return null; lines[at] = line; }
                else lines.Insert(open + 1, line);
                resulting = args;
            }
            else
            {
                int end = AppsBlockClose(lines);
                if (end < 0) return null;
                string ind = lines[end].Substring(0, lines[end].Length - lines[end].TrimStart().Length) + "\t";
                lines.InsertRange(end, new[] { ind + "\"" + appId + "\"", ind + "{", ind + "\t\"LaunchOptions\"\t\t\"" + arg + "\"", ind + "}" });
                resulting = arg;
            }
            File.Copy(file, file + ".geo11sr.bak", true);
            File.WriteAllText(file, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            return resulting;
        }

        // writes LaunchOptions for the app into every localconfig.vdf that knows it; returns the files changed
        static List<string> WriteSteamLaunchOptions(string appId, string arg, out string resulting)
        {
            var changed = new List<string>();
            resulting = null;
            foreach (var f in SteamLocalConfigs(appId))
            {
                string r = EditLocalConfig(f, appId, arg);
                if (r == null) continue;
                resulting = r; changed.Add(f);
            }
            return changed;
        }

        static bool SteamRunning() { return Process.GetProcessesByName("steam").Length > 0; }

        // Steam keeps localconfig.vdf in memory and rewrites it, so: shut Steam down, edit, start it again.
        // Asks first when Steam is running. Returns a log line.
        public static string SetSteamLaunchOptions(string appId, string gameName, string arg, IWin32Window owner)
        {
            string current = SteamLaunchOptions(appId);
            if (current != null && HasArg(current, arg)) return Lang.T("  Steam 시작 옵션에 이미 ") + arg + Lang.T("가 있습니다: ") + current;
            if (SteamLocalConfigs(appId).Count == 0) return Lang.T("  Steam: 이 게임(appid ") + appId + Lang.T(")을 아는 localconfig.vdf를 찾지 못했습니다. Steam 라이브러리 → 속성 → 시작 옵션에 ") + arg + Lang.T("를 직접 넣으세요.");
            string steamExe = SteamExe();
            bool wasRunning = SteamRunning();
            if (wasRunning)
            {
                var r = MessageBox.Show(owner,
                    Lang.T("Unreal Engine 4 Universal Fix 2는 게임을 ") + arg + Lang.T(" 인자로 실행해야 합니다.\nSteam 라이브러리에서 실행할 때도 붙도록 Steam의 시작 옵션에 넣으려면 Steam을 잠시 종료해야 합니다 (편집 뒤 자동으로 다시 시작).\n\n") +
                    gameName + " (appid " + appId + ")\n\n" + Lang.T("지금 Steam을 종료하고 넣을까요?\n[아니요]를 누르면 Steam 라이브러리 → 속성 → 시작 옵션에 ") + arg + Lang.T("를 직접 넣어야 합니다."),
                    Lang.T("Steam 시작 옵션"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return Lang.T("  Steam 시작 옵션은 넣지 않았습니다. Steam 라이브러리 → 속성 → 시작 옵션에 ") + arg + Lang.T("를 직접 넣으세요.");
                try { if (steamExe != null && File.Exists(steamExe)) Process.Start(new ProcessStartInfo(steamExe, "-shutdown") { UseShellExecute = true }); } catch { }
                for (int i = 0; i < 90 && SteamRunning(); i++) Thread.Sleep(1000);
                if (SteamRunning()) return Lang.T("  Steam이 종료되지 않아 시작 옵션을 넣지 못했습니다. Steam을 직접 끝내고 [UE4 설정] 또는 [완료]를 다시 하세요.");
                Thread.Sleep(1500);   // Steam flushes its config files right before the process ends
            }
            string res;
            var changed = WriteSteamLaunchOptions(appId, arg, out res);
            if (wasRunning && steamExe != null && File.Exists(steamExe))
            {
                try { Process.Start(new ProcessStartInfo(steamExe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(steamExe) }); } catch { }
            }
            if (changed.Count == 0) return Lang.T("  Steam: localconfig.vdf를 바꾸지 못했습니다. 시작 옵션에 ") + arg + Lang.T("를 직접 넣으세요.");
            return Lang.T("  Steam 시작 옵션 = \"") + res + "\"  (" + string.Join(", ", changed) + ")";
        }

        // Puts "-dx11" where the game is launched from. Returns log lines.
        public static List<string> ApplyDx11(string gameExe, IWin32Window owner)
        {
            var log = new List<string>();
            string dir = Path.GetDirectoryName(gameExe), appId, nm, heroicApp, store, title;
            if (Util.FindSteamApp(dir, out appId, out nm))
                log.Add(SetSteamLaunchOptions(appId, nm, Dx11, owner));
            else if (FindHeroicApp(dir, out heroicApp, out store, out title))
            {
                string args = SetHeroicArgs(heroicApp, Dx11);
                log.Add(Lang.T("  Heroic(") + store + ") " + title + Lang.T(": 게임 설정 launcherArgs = \"") + args + "\"");
            }
            else
                log.Add(Lang.T("  Steam / Heroic 게임이 아닙니다. 이 설치기의 [실행]은 ") + Dx11 + Lang.T("를 붙여 실행하고, 바로 가기로 실행할 때는 ") + Dx11 + Lang.T("를 직접 붙이세요."));
            return log;
        }

        // what the games list shows: launcher + whether -dx11 is set there
        public static string LaunchLabel(string gameExe, bool ue4)
        {
            string dir = Path.GetDirectoryName(gameExe), appId, nm, heroicApp, store, title;
            if (Util.FindSteamApp(dir, out appId, out nm))
            {
                if (!ue4) return "Steam";
                string o = SteamLaunchOptions(appId);
                return "Steam" + (HasArg(o, Dx11) ? " " + Dx11 : Lang.T(" (-dx11 없음)"));
            }
            if (FindHeroicApp(dir, out heroicApp, out store, out title))
            {
                if (!ue4) return "Heroic";
                return "Heroic" + (HasArg(HeroicArgs(heroicApp), Dx11) ? " " + Dx11 : Lang.T(" (-dx11 없음)"));
            }
            return ue4 ? "exe " + Dx11 : "exe";
        }
    }
}
