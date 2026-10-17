// TriDef 3D games. TriDef 3D (DDD, discontinued in 2017) must be installed by the user: its driver DLLs, its ~1500
// per-game profiles and its licence are its own, and this app ships none of them. What the app replaces is TriDef 3D
// Ignition's own window:
//   - registering: HKCU\SOFTWARE\DDD\TriDefIgnition\Games\<name> with Path = the game exe, Profile = one of TriDef's
//     .TriDefGame files (C:\ProgramData\DDD\TriDefGames), the same values Ignition writes when a game is added by exe;
//   - starting: Tridef3D_Play.exe (tridef3d-win11-injector, MIT, the TriDef package of this app) launches the Steam game
//     with steam:// and injects TriDef's DLLs into it (and SRWeave for an SR panel with --sr). It needs administrator
//     rights, so the app starts it with "runas".
// Nothing goes into the game folder. Steam games only (the launcher finds the app id from the exe's Steam folder);
// 32-bit games get TriDef's DirectX 9 set, 64-bit games its DirectX 11 set.
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

        // starts the game through the launcher (elevated: injection needs administrator rights)
        public static void Launch(string pkgDir, string name, bool sr)
        {
            string exe = Path.Combine(pkgDir, Launcher);
            if (!File.Exists(exe)) throw new FileNotFoundException(Lang.T("TriDef 실행기가 패키지에 없습니다: ") + exe);
            Process.Start(new ProcessStartInfo(exe, "\"" + name + "\"" + (sr ? " --sr" : "")) { UseShellExecute = true, Verb = "runas", WorkingDirectory = pkgDir });
        }

        public static string SrLog(string pkgDir, string exe)
        {
            return Path.Combine(pkgDir, "sr", Util.ExeBits(exe) == 32 ? "x86" : "x64", "SRWeave.log");
        }
    }
}
