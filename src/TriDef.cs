// TriDef 3D games. TriDef 3D (DDD, discontinued in 2017) must be installed by the user: its driver DLLs, its ~1500
// per-game profiles and its licence are its own, and this app ships none of them. What the app replaces is TriDef 3D
// Ignition's own window:
//   - registering: HKCU\SOFTWARE\DDD\TriDefIgnition\Games\<name> with Path = the game exe, Profile = one of TriDef's
//     .TriDefGame files (C:\ProgramData\DDD\TriDefGames), the same values Ignition writes when a game is added by exe;
//   - starting: Tridef3D_Play.exe (tridef3d-win11-injector, MIT, the TriDef package of this app) starts the game and
//     injects TriDef's DLLs into it (and SRWeave for an SR panel with --sr). A Steam game is started with steam:// (the
//     launcher finds the app id from the exe's Steam folder) and the launcher is run with "runas", as before. Any other
//     game (GOG, EA app, Epic, a disc install) is started from its exe, and the launcher is run with the user's normal
//     rights so that the game gets them too; if the exe hands over to its store's launcher and comes back as a new
//     process, the launcher injects that one as well.
//   - launch method (per game, see Mode): for a game that only runs when its own launcher starts it (GTA5.exe via
//     PlayGTAV.exe, GP4.exe via CSM) the launcher is started with --wait and never starts the game exe itself: it
//     waits for it and injects it. "launcher": this app then starts the game's launcher; "wait": the user does.
// Nothing goes into the game folder. 32-bit games get TriDef's DirectX 9 set, 64-bit games its DirectX 11 set. With SR
// weaving a DirectX 9 game below the panel's resolution is weaved at the panel's resolution (SRWeaveDX9 "upscale").
// The profile list comes from TriDef's own folder, or without TriDef from data\tridef_profiles.txt (names only), so a
// game with a profile is recognised either way and the user is told that TriDef 3D has to be installed.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GeoGamingHub
{
    static class TriOps
    {
        public static string GamesKey = @"SOFTWARE\DDD\TriDefIgnition\Games";   // tests point this elsewhere
        const string OursValue = "GeoGamingHub";      // 1 = the key was created by this app (uninstall deletes it)
        const string SrValue = "GeoGamingHubSR";      // 1 = start with SR weaving (Tridef3D_Play --sr)
        public const string Launcher = "Tridef3D_Play.exe";

        public static string ProfilesDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DDD", "TriDefGames"); }
        }

        // TriDef's install (HKLM\SOFTWARE\WOW6432Node\DDD InstallApps) with its Ignition driver DLLs, else null
        public static string DriverDir()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\DDD"))
                {
                    string apps = k == null ? null : k.GetValue("InstallApps") as string;
                    if (string.IsNullOrEmpty(apps)) return null;
                    string d = Path.Combine(apps, "TriDef", "TriDefIgnition", "Driver");
                    return File.Exists(Path.Combine(d, "TriDefIgnition64.dll")) || File.Exists(Path.Combine(d, "TriDefIgnition.dll")) ? d : null;
                }
            }
            catch { return null; }
        }

        public static bool Installed() { return DriverDir() != null; }

        // ---- profiles
        static List<string> profiles;
        public static string BundledList;   // data\tridef_profiles.txt (set by the form)

        public static List<string> ProfileNames()
        {
            if (profiles != null) return profiles;
            var list = new List<string>();
            try
            {
                if (Directory.Exists(ProfilesDir))
                    list.AddRange(Directory.GetFiles(ProfilesDir, "*.TriDefGame").Select(Path.GetFileNameWithoutExtension));
            }
            catch { }
            if (list.Count == 0 && BundledList != null && File.Exists(BundledList))
                list.AddRange(File.ReadAllLines(BundledList, Encoding.UTF8).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")));
            profiles = list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            return profiles;
        }

        public static void ResetCache() { profiles = null; }

        public static string ProfileFile(string name) { return Path.Combine(ProfilesDir, name + ".TriDefGame"); }

        // "Torchlight (DDD)" -> "Torchlight"
        public static string Title(string profile) { return Regex.Replace(profile ?? "", @"\s*\([^)]*\)\s*$", "").Trim(); }

        static readonly string[] Suffixes = {
            "game of the year edition", "goty edition", "goty", "definitive edition", "enhanced edition", "complete edition",
            "special edition", "deluxe edition", "standard edition", "directors cut", "remastered", "anniversary edition",
            "collectors edition", "ultimate edition", "gold edition", "digital deluxe edition", "edition" };

        // the same normalization as tools\library_survey (collect.py norm_loose), so the app and the survey match alike
        public static string Norm(string t)
        {
            t = (t ?? "").ToLowerInvariant().Replace("&", " and ");
            t = Regex.Replace(t, "[\u2122\u00ae\u00a9]", "");
            t = Regex.Replace(t, "['\u2019`]", "");
            t = Regex.Replace(t, "[^0-9a-z]+", " ").Trim();
            t = Regex.Replace(t, "^the ", "");
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var s in Suffixes)
                    if (t.EndsWith(" " + s)) { t = t.Substring(0, t.Length - s.Length - 1).Trim(); changed = true; }
            }
            return t;
        }

        // the names a game goes by: its Steam / Heroic name, the app's display name, the folder and exe names
        public static IEnumerable<string> NamesOf(string exe)
        {
            string dir = Path.GetDirectoryName(exe), a, b, title;
            if (Util.FindSteamApp(dir, out a, out title) && !string.IsNullOrWhiteSpace(title)) yield return title;
            string ht = null;
            try { if (Launchers.FindHeroicApp(dir, out a, out b, out ht)) { } } catch { ht = null; }
            if (!string.IsNullOrWhiteSpace(ht)) yield return ht;
            yield return Util.GameDisplayName(exe);
            yield return Path.GetFileName(dir);
            yield return Path.GetFileNameWithoutExtension(exe);
        }

        // the profile for an exe (DDD's own preferred where several authors made one), else null
        public static string Match(string exe)
        {
            var byTitle = new Dictionary<string, List<string>>();
            foreach (var p in ProfileNames())
            {
                string n = Norm(Title(p));
                if (n.Length == 0) continue;
                List<string> l;
                if (!byTitle.TryGetValue(n, out l)) byTitle[n] = l = new List<string>();
                l.Add(p);
            }
            foreach (var name in NamesOf(exe))
            {
                List<string> l;
                if (byTitle.TryGetValue(Norm(name), out l))
                    return l.OrderByDescending(p => p.EndsWith("(DDD)", StringComparison.OrdinalIgnoreCase)).First();
            }
            return null;
        }

        // ---- registration (Ignition's games list)
        // the registered name of this exe (Path equals the exe), else null
        public static string RegisteredName(string exe)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(GamesKey))
                {
                    if (k == null) return null;
                    foreach (var n in k.GetSubKeyNames())
                        using (var g = k.OpenSubKey(n))
                        {
                            string p = g == null ? null : g.GetValue("Path") as string;
                            if (p != null && string.Equals(Path.GetFullPath(p), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase)) return n;
                        }
                }
            }
            catch { }
            return null;
        }

        static object Value(string name, string value)
        {
            try { using (var g = Registry.CurrentUser.OpenSubKey(GamesKey + "\\" + name)) return g == null ? null : g.GetValue(value); }
            catch { return null; }
        }

        public static string Profile(string name) { var p = Value(name, "Profile") as string; return p == null ? null : Path.GetFileNameWithoutExtension(p); }
        public static bool Sr(string name) { var v = Value(name, SrValue); return v is int ? (int)v != 0 : true; }
        public static bool Ours(string name) { var v = Value(name, OursValue); return v is int && (int)v != 0; }

        // a registry key name: Ignition's games are keyed by their display name
        public static string KeyName(string title)
        {
            string n = Regex.Replace(title ?? "", @"[\\/]", " ").Trim();
            return n.Length > 0 ? n : "Game";
        }

        public static List<string> Register(string name, string exe, string profile, bool sr)
        {
            var log = new List<string>();
            bool existed;
            using (var k = Registry.CurrentUser.OpenSubKey(GamesKey + "\\" + name)) existed = k != null;
            using (var g = Registry.CurrentUser.CreateSubKey(GamesKey + "\\" + name))
            {
                g.SetValue("__dddvartype", 6, RegistryValueKind.DWord);
                g.SetValue("Argument", "", RegistryValueKind.String);
                g.SetValue("Dir", "", RegistryValueKind.String);
                g.SetValue("IconPath", exe, RegistryValueKind.String);
                g.SetValue("Path", exe, RegistryValueKind.String);
                g.SetValue("Profile", ProfileFile(profile), RegistryValueKind.String);
                g.SetValue(SrValue, sr ? 1 : 0, RegistryValueKind.DWord);
                if (!existed) g.SetValue(OursValue, 1, RegistryValueKind.DWord);
            }
            log.Add(Lang.T("  TriDef Ignition 등록: ") + name + " → " + Path.GetFileName(exe));
            log.Add(Lang.T("  프로필: ") + profile + (File.Exists(ProfileFile(profile)) ? "" : Lang.T(" (TriDef 프로필 폴더에 없음)")));
            log.Add("  SR " + (sr ? Lang.T("위빙 켬 (--sr)") : Lang.T("위빙 끔")));
            return log;
        }

        public static void SetSr(string name, bool sr)
        {
            using (var g = Registry.CurrentUser.OpenSubKey(GamesKey + "\\" + name, true))
                if (g != null) g.SetValue(SrValue, sr ? 1 : 0, RegistryValueKind.DWord);
        }

        // deletes the key when this app made it; a game added in Ignition itself keeps its key (only our values go)
        public static List<string> Unregister(string name)
        {
            var log = new List<string>();
            if (Ours(name))
            {
                Registry.CurrentUser.DeleteSubKeyTree(GamesKey + "\\" + name, false);
                log.Add(Lang.T("  TriDef Ignition 등록 삭제: ") + name);
            }
            else
            {
                using (var g = Registry.CurrentUser.OpenSubKey(GamesKey + "\\" + name, true))
                    if (g != null) g.DeleteValue(SrValue, false);
                log.Add(Lang.T("  TriDef Ignition에서 직접 등록한 게임이라 등록은 그대로 둡니다: ") + name);
            }
            return log;
        }

        // ---- launch method (the same values Tridef3D_Play's "Launch method" dialog writes, play3d.py)
        //   Play3DMode     "auto" (or absent): Steam games via steam://, any other game from its exe
        //                  "launcher": start Play3DLauncher, then wait for the registered exe (GTA5.exe via PlayGTAV.exe)
        //                  "wait": start nothing - the user starts the game from its launcher / a shortcut
        //   Play3DLauncher an exe, a shortcut or a URI (steam://...);  Play3DLauncherArgs: its arguments
        // Path stays the game's own exe either way: it picks the DLL set and is the process that gets injected.
        public const string ModeAuto = "auto", ModeLauncher = "launcher", ModeWait = "wait";
        const string ModeValue = "Play3DMode", LauncherValue = "Play3DLauncher", LauncherArgsValue = "Play3DLauncherArgs";
        // the line Tridef3D_Play writes to launcher.log once it is watching (injector.py poll_launch_and_inject)
        const string WaitingLine = "WAITING for ";

        public static string Mode(string name)
        {
            string m = ((Value(name, ModeValue) as string) ?? "").Trim().ToLowerInvariant();
            if (m == ModeLauncher && string.IsNullOrWhiteSpace(LauncherOf(name))) return ModeAuto;
            return m == ModeLauncher || m == ModeWait ? m : ModeAuto;
        }

        public static string LauncherOf(string name) { return ((Value(name, LauncherValue) as string) ?? "").Trim(); }
        public static string LauncherArgsOf(string name) { return ((Value(name, LauncherArgsValue) as string) ?? "").Trim(); }

        // "auto" removes the values (the key is Ignition's own again)
        public static void SetLaunch(string name, string mode, string launcher, string args)
        {
            using (var g = Registry.CurrentUser.OpenSubKey(GamesKey + "\\" + name, true))
            {
                if (g == null) return;
                if (mode != ModeLauncher && mode != ModeWait)
                {
                    foreach (var v in new[] { ModeValue, LauncherValue, LauncherArgsValue }) g.DeleteValue(v, false);
                    return;
                }
                g.SetValue(ModeValue, mode, RegistryValueKind.String);
                g.SetValue(LauncherValue, mode == ModeLauncher ? (launcher ?? "").Trim().Trim('"') : "", RegistryValueKind.String);
                g.SetValue(LauncherArgsValue, mode == ModeLauncher ? (args ?? "").Trim() : "", RegistryValueKind.String);
            }
        }

        // 0 = most likely the game's launcher, -1 = not one: play*.exe (PlayGTAV.exe), csm*.exe (GP4's CSM), *launcher*.exe
        static int LauncherRank(string file)
        {
            string low = Path.GetFileName(file).ToLowerInvariant();
            if (!low.EndsWith(".exe") || low.Contains("crash") || low.Contains("unins")) return -1;
            if (low.StartsWith("play")) return 0;
            if (low.StartsWith("csm")) return 1;
            if (low.Contains("launcher")) return 2;
            return -1;
        }

        // a guess at what starts this game, to pre-fill the launcher: a launcher-looking exe in the game's folder or
        // the one above it, else the Steam address of a Steam game, else ""
        public static string SuggestLauncher(string exe, string steamAppId)
        {
            string best = null; int bestScore = int.MaxValue;
            try
            {
                string here = Path.GetDirectoryName(exe);
                var dirs = new[] { here, Path.GetDirectoryName(here) };
                for (int depth = 0; depth < dirs.Length; depth++)
                {
                    if (string.IsNullOrEmpty(dirs[depth]) || !Directory.Exists(dirs[depth])) continue;
                    foreach (var f in Directory.GetFiles(dirs[depth], "*.exe").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        int r = LauncherRank(f);
                        if (r < 0 || string.Equals(Path.GetFileName(f), Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase)) continue;
                        int score = depth * 10 + r;
                        if (score < bestScore) { bestScore = score; best = f; }
                    }
                }
            }
            catch { }
            if (best != null) return best;
            return string.IsNullOrEmpty(steamAppId) ? "" : "steam://rungameid/" + steamAppId;
        }

        // starts the game through the launcher. "--console" with a name makes the (windowed) launcher run without any
        // window and write its output to launcher.log in the package folder. A Steam game: elevated, as it always was.
        // Any other game: the user's own rights - the launcher starts the game itself, and an elevated launcher would
        // make the game run as administrator.
        // Launch method "wait" / "launcher": Tridef3D_Play only waits (--wait) and never starts the game, so it runs
        // elevated - it can then inject into a game its launcher runs as administrator too. For "launcher" this app
        // starts the game's launcher itself, with the user's own rights, once launcher.log says Tridef3D_Play is
        // watching (so the game can't appear before it does). Returns the started Tridef3D_Play process (may be null).
        public static Process Launch(string pkgDir, string name, bool sr, bool steam)
        {
            string exe = Path.Combine(pkgDir, Launcher);
            if (!File.Exists(exe)) throw new FileNotFoundException(Lang.T("TriDef 실행기가 패키지에 없습니다: ") + exe);
            string mode = Mode(name);
            bool wait = mode != ModeAuto;
            if (wait) try { File.Delete(LauncherLog(pkgDir)); } catch { }   // only this run's "WAITING" line counts
            launchedUtc = DateTime.UtcNow;
            var psi = new ProcessStartInfo(exe, LaunchArgs(name, sr, wait))
            { UseShellExecute = true, WorkingDirectory = pkgDir, WindowStyle = ProcessWindowStyle.Hidden };
            if (steam || wait) psi.Verb = "runas";
            return Process.Start(psi);
        }

        public static string LaunchArgs(string name, bool sr) { return LaunchArgs(name, sr, false); }
        public static string LaunchArgs(string name, bool sr, bool wait)
        {
            return "\"" + name + "\"" + (sr ? " --sr" : "") + (wait ? " --wait" : "") + " --console";
        }

        // launch method "launcher": waits (on a background thread) until Tridef3D_Play is watching, then starts the game's
        // launcher with this app's (the user's) rights. done gets a log line; it runs on the background thread.
        public static void StartLauncherWhenReady(string pkgDir, Process play, string name, Action<string> done, int timeoutMs = 180000)
        {
            string launcher = LauncherOf(name), args = LauncherArgsOf(name), log = LauncherLog(pkgDir);
            new System.Threading.Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    if (LogSaysWaiting(log)) break;
                    bool exited = false;
                    try { exited = play != null && play.HasExited; } catch { }
                    if (exited) { done(Lang.T("  TriDef 실행기가 기다리기 전에 끝났습니다. 런처를 실행하지 않습니다: ") + log); return; }
                    System.Threading.Thread.Sleep(200);
                }
                if (!LogSaysWaiting(log)) { done(Lang.T("  TriDef 실행기가 준비되지 않아 런처를 실행하지 않습니다: ") + log); return; }
                try
                {
                    StartLauncher(launcher, args);
                    done(Lang.T("  게임 런처 실행: ") + launcher + (args.Length > 0 ? " " + args : ""));
                }
                catch (Exception ex) { done(Lang.T("  게임 런처를 실행하지 못했습니다: ") + launcher + " - " + ex.Message); }
            }) { IsBackground = true }.Start();
        }

        static DateTime launchedUtc = DateTime.MinValue;

        // launcher.log of this run (written after Launch started it - a log that couldn't be deleted is older) says it waits
        static bool LogSaysWaiting(string log)
        {
            try
            {
                if (!File.Exists(log) || File.GetLastWriteTimeUtc(log) < launchedUtc.AddSeconds(-1)) return false;
                using (var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var r = new StreamReader(fs, Encoding.UTF8))
                    return r.ReadToEnd().Contains(WaitingLine);
            }
            catch { return false; }
        }

        // an exe in its own folder; a shortcut or an address (steam://...) through the shell
        public static void StartLauncher(string launcher, string args)
        {
            var psi = new ProcessStartInfo(launcher) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            if (launcher.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(launcher))
                psi.WorkingDirectory = Path.GetDirectoryName(launcher);
            Process.Start(psi);
        }

        public static string LauncherLog(string pkgDir) { return Path.Combine(pkgDir, "launcher.log"); }

        public static string SrLog(string pkgDir, string exe)
        {
            return Path.Combine(pkgDir, "sr", Util.ExeBits(exe) == 32 ? "x86" : "x64", "SRWeave.log");
        }
    }
}
