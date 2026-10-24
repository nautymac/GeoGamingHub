// Community 3D reports.
//   [3D 동작 보고...] opens the GitHub issue form in the app's language (.github/ISSUE_TEMPLATE/game_report.yml English,
//   game_report.ko.yml Korean; same field ids, and community_list.py reads both) in the browser with the
//   game, its PCGamingWiki link, the 3D method used and a setup summary filled in; the user picks the result and
//   submits. Nothing is sent by the app itself, and no folder paths go into the report.
//   A GitHub Action (.github/workflows/community-list.yml, .github/scripts/community_list.py) turns the reports into
//   COMMUNITY(.ko).md plus community.json, reading each game's PCGamingWiki page like the candidate list does. The app
//   reads community.json (cached next to the exe for offline use) for the game list's [3D 보고] column and disables the
//   report button for games that already have test reports.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace GeoGamingHub
{
    static class Report
    {
        const string Repo = "nautymac/GeoGamingHub";
        public static string FormUrl { get { return Lang.Setting("report", "https://github.com/" + Repo + "/issues/new"); } }
        public static string ListUrl { get { return Lang.Setting("compat_list", "https://github.com/" + Repo + "/blob/main/" + (Lang.English ? "COMMUNITY.md" : "COMMUNITY.ko.md")); } }
        static string JsonUrl { get { return Lang.Setting("compat_json", "https://raw.githubusercontent.com/" + Repo + "/main/community.json"); } }

        public sealed class Entry
        {
            public string Engine, Game, Key, Status;   // Status: works | issues | broken
            public int Works, Issues, Broken;
            public string[] Steam = new string[0], Exe = new string[0];
            public int Count { get { return Works + Issues + Broken; } }
        }

        static List<Entry> entries = new List<Entry>();
        static string cacheFile;
        public static bool Loaded { get; private set; }

        // ---- the published list (compat.json) ---------------------------------------------------

        public static void LoadCache(string exeDir)
        {
            cacheFile = Path.Combine(exeDir, "community.json");
            try { if (File.Exists(cacheFile)) { entries = Parse(File.ReadAllText(cacheFile, Encoding.UTF8)); Loaded = true; } }
            catch { }
        }

        // download the current list in the background; done(true) runs on the worker thread when it changed
        public static void FetchAsync(Action<bool> done)
        {
            Task.Run(() =>
            {
                bool ok = false;
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    var req = (HttpWebRequest)WebRequest.Create(JsonUrl + "?t=" + DateTime.UtcNow.Ticks);   // skip stale proxies
                    req.Timeout = 10000; req.ReadWriteTimeout = 10000; req.UserAgent = "GeoGamingHub";
                    string json;
                    using (var resp = req.GetResponse())
                    using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) json = sr.ReadToEnd();
                    var list = Parse(json);
                    entries = list; Loaded = true; ok = true;
                    if (cacheFile != null) try { File.WriteAllText(cacheFile, json, new UTF8Encoding(false)); } catch { }
                }
                catch { }   // offline or not published yet: keep the cached list
                if (done != null) done(ok);
            });
        }

        static List<Entry> Parse(string json)
        {
            var list = new List<Entry>();
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            object games;
            if (root == null || !root.TryGetValue("games", out games) || !(games is ArrayList)) return list;
            foreach (var o in (ArrayList)games)
            {
                var g = o as Dictionary<string, object>;
                if (g == null || Int(g, "works") + Int(g, "issues") + Int(g, "broken") == 0) continue;   // suggestions only: nobody tried it yet
                list.Add(new Entry
                {
                    Engine = Str(g, "method"), Game = Str(g, "game"), Key = Str(g, "key"), Status = Str(g, "status"),
                    Works = Int(g, "works"), Issues = Int(g, "issues"), Broken = Int(g, "broken"),
                    Steam = Arr(g, "steam"), Exe = Arr(g, "exe"),
                });
            }
            return list;
        }
        static string Str(Dictionary<string, object> g, string k) { object v; return g.TryGetValue(k, out v) && v != null ? v.ToString() : ""; }
        static int Int(Dictionary<string, object> g, string k) { int i; return int.TryParse(Str(g, k), out i) ? i : 0; }
        static string[] Arr(Dictionary<string, object> g, string k)
        {
            object v;
            return g.TryGetValue(k, out v) && v is ArrayList ? ((ArrayList)v).Cast<object>().Select(x => (x ?? "").ToString().ToLowerInvariant()).ToArray() : new string[0];
        }

        // same normalization as community_list.py game_key()
        public static string Key(string name)
        {
            string n = Regex.Replace((name ?? "").ToLowerInvariant(), "[™®©]", "");
            return Regex.Replace(n, @"[\W_]+", "");
        }

        // the list entry of this game: Steam AppID, then exe name, then game name; the same 3D method wins a tie
        public static Entry Find(string exe)
        {
            if (string.IsNullOrEmpty(exe) || entries.Count == 0) return null;
            string dir = Path.GetDirectoryName(exe), appId, nm;
            string id = Util.FindSteamApp(dir, out appId, out nm) ? appId : null;
            string file = Path.GetFileName(exe).ToLowerInvariant(), key = Key(GameName(exe)), used = File.Exists(exe) ? MethodKey(exe) : null;
            Func<IEnumerable<Entry>, Entry> pick = c => c.OrderByDescending(e => e.Engine == used).FirstOrDefault();
            return (id != null ? pick(entries.Where(e => e.Steam.Contains(id))) : null)
                ?? pick(entries.Where(e => e.Exe.Contains(file)))
                ?? pick(entries.Where(e => e.Key.Length > 0 && e.Key == key));
        }

        // text for the game list column ("가능 (3)" ...)
        public static string StatusText(Entry e)
        {
            if (e == null) return "-";
            string s = e.Status == "works" ? Lang.T("가능") : e.Status == "issues" ? Lang.T("문제 있음") : Lang.T("안 됨");
            return s + " (" + e.Count + ")";
        }

        // ---- a new report ----------------------------------------------------------------------

        // a fix of this app is in the game folder (geo-11, OGL3D, VK3D, DX12_3D) or the game is registered with TriDef 3D
        public static bool Installed(string exe)
        {
            string dir = Path.GetDirectoryName(exe);
            return (File.Exists(Path.Combine(dir, "d3d11.dll")) && File.Exists(Path.Combine(dir, "d3dxdm.ini"))) || GlOps.Installed(dir) || VkOps.Installed(dir) ||
                   Dx12Ops.Installed(dir) || TriOps.RegisteredName(exe) != null;
        }

        // the engine / API word of the setup summary
        public static string Engine(string exe)
        {
            string dir = Path.GetDirectoryName(exe);
            if (GlOps.Installed(dir)) return "OpenGL";
            if (VkOps.Installed(dir)) return "Vulkan";
            if (Dx12Ops.Installed(dir)) return "DX12";
            if (!File.Exists(Path.Combine(dir, "d3d11.dll")) && TriOps.RegisteredName(exe) != null) return "TriDef";
            if (Util.UnityVersion(dir, exe) != null) return "Unity";
            if (Util.IsUe4(exe)) return "UE4";
            if (Dx9Ops.Installed(dir)) return "DX9";
            return "DX11";
        }

        // the 3D method installed, in community_list.py's method keys
        public static string MethodKey(string exe)
        {
            switch (Engine(exe))
            {
                case "OpenGL": return "opengl";
                case "Vulkan": return "vulkan";
                case "DX12": return "dx12";
                case "TriDef": return "tridef";
                case "Unity": return "unity";
                case "UE4": return "ue4";
                case "DX9": return "dx9";
                default: return "dx11";
            }
        }

        // the form's "3D method used" (community_list.py USED reads these words)
        public static string MethodUsed(string exe)
        {
            switch (MethodKey(exe))
            {
                case "opengl": return "OGL3D";
                case "vulkan": return VkOps.DxvkInstalled(Path.GetDirectoryName(exe)) ? "VK3D + DXVK" : "VK3D";
                case "dx12": return "DX12_3D";
                case "tridef": return "TriDef 3D";
                case "unity": return "geo-11 Unity";
                case "ue4": return "geo-11 UE4 (UF2)";
                case "dx9": return "DX9 dgVoodoo2 + geo-11";
                default: return "geo-11 DX11";
            }
        }

        // the game's PCGamingWiki page for the form: the Steam AppID (PCGamingWiki resolves it), else the candidate list's page
        public static string PcgwLink(string exe, IEnumerable<Candidate> candidates)
        {
            string dir = Path.GetDirectoryName(exe), appId, nm;
            if (Util.FindSteamApp(dir, out appId, out nm) && !string.IsNullOrEmpty(appId)) return "https://www.pcgamingwiki.com/api/appid.php?appid=" + appId;
            string key = Key(GameName(exe));
            var c = (candidates ?? Enumerable.Empty<Candidate>()).FirstOrDefault(x => !string.IsNullOrEmpty(x.PcgwPage) && Key(x.Title) == key);
            return c != null ? "https://www.pcgamingwiki.com/wiki/" + Uri.EscapeDataString(c.PcgwPage.Replace(' ', '_')) : "";
        }

        // Steam / Heroic name first: the list groups reports by name, and store names are the same for everyone
        public static string GameName(string exe)
        {
            string dir = Path.GetDirectoryName(exe), a, b, title;
            if (Util.FindSteamApp(dir, out a, out title) && !string.IsNullOrWhiteSpace(title)) return title.Trim();
            try { if (Launchers.FindHeroicApp(dir, out a, out b, out title) && !string.IsNullOrWhiteSpace(title)) return title.Trim(); } catch { }
            return Util.GameDisplayName(exe);
        }

        // "Key: value" lines for the form's Setup field (English, so every report reads the same)
        public static string Details(string exe, string dx9Pkg)
        {
            string dir = Path.GetDirectoryName(exe), engine = Engine(exe);
            var bits = Util.ExeBits(exe);
            var d = new List<string>();
            d.Add("App: Geo Gaming Hub (build " + File.GetLastWriteTime(Application()).ToString("yyyy-MM-dd") + ")");
            d.Add("Executable: " + Path.GetFileName(exe));
            d.Add("Engine: " + engine);
            d.Add("Bits: " + (bits.HasValue ? bits.Value.ToString() : "?"));
            string appId, nm, app, store, title;
            if (Util.FindSteamApp(dir, out appId, out nm)) d.Add("Store: Steam " + appId);
            else if (Launchers.FindHeroicApp(dir, out app, out store, out title)) d.Add("Store: " + store + " (Heroic)");
            string uv = Util.UnityVersion(dir, exe);
            if (uv != null) d.Add("Unity: " + uv);

            string tri = engine == "TriDef" ? TriOps.RegisteredName(exe) : null;
            if (tri != null)
            {
                d.Add("TriDef 3D profile: " + (TriOps.Profile(tri) ?? "?"));
                d.Add("SR weaving: " + (TriOps.Sr(tri) ? "on (SRWeave)" : "off"));
            }
            else if (VkOps.Installed(dir))
            {
                var v = VkOps.Read(Path.Combine(dir, VkOps.IniName));
                d.Add("VK3D: " + (VkOps.Version(Path.Combine(dir, VkOps.Dll)) ?? "?") + (Directory.Exists(Path.Combine(dir, VkOps.FixDirName)) ? " + game fix" : " generic"));
                if (VkOps.DxvkInstalled(dir)) d.Add("DXVK: yes (" + (Util.DirectXOf(exe) ?? "?") + ")");
                d.Add("Output: " + (v.Sr ? "SR weaving" : v.Mode));
                d.Add("Separation / convergence: " + v.Separation.ToString(System.Globalization.CultureInfo.InvariantCulture) + " / " +
                      (v.Auto ? "auto" : v.Convergence.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            else if (Dx12Ops.Installed(dir))
            {
                var o = Dx12Ops.Read(Path.Combine(dir, Dx12Ops.IniName));
                d.Add("DX12_3D: " + (Dx12Ops.Version(Path.Combine(dir, Dx12Ops.Dll)) ?? "?"));
                d.Add("Output: " + (o.Sr ? "SR weaving" : o.Mode));
                d.Add("Separation / convergence: " + o.Separation.ToString(System.Globalization.CultureInfo.InvariantCulture) + " / " +
                      o.Convergence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (GlOps.Installed(dir))
            {
                var o = GlOps.Read(Path.Combine(dir, GlOps.IniName));
                d.Add("OGL3D: " + (GlOps.Version(Path.Combine(dir, GlOps.Dll)) ?? "?"));
                d.Add("Output: " + (o.Sr ? "SR weaving" : o.Mode));
                d.Add("Upscaling: " + (o.Upscale ? "on" : "off"));
                d.Add("Separation / convergence: " + o.Separation.ToString(System.Globalization.CultureInfo.InvariantCulture) + " / " +
                      (o.Auto ? "auto" : o.Convergence.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            else
            {
                string dll = Path.Combine(dir, "d3d11.dll"), dm = Path.Combine(dir, "d3dxdm.ini"), dx = Path.Combine(dir, "d3dx.ini");
                if (File.Exists(dll)) try { d.Add("3DMigoto (geo-11) d3d11.dll: " + FileVersionInfo.GetVersionInfo(dll).FileVersion); } catch { }
                if (engine == "DX9")
                {
                    d.Add("dgVoodoo2: " + (Dx9Ops.InstalledVersion(dir, dx9Pkg, bits ?? 32) ?? "?") + ", VRAM " + (Dx9Ops.Vram(dir) ?? "default") +
                          ", resolution " + (Dx9Ops.DesktopResolution(dir) == true ? "desktop" : "unforced"));
                    if (bits == 32) d.Add("4GB patch (LAA): " + (Util.IsLargeAddressAware(exe) == true ? "yes" : "no"));
                }
                string srIni = Path.Combine(dir, "SRWeave.ini");
                bool sr = File.Exists(Path.Combine(dir, "dxgi.dll")) && File.Exists(srIni);
                string w = sr ? Ini.Get(srIni, "weave") : null;
                d.Add("SR weaving: " + (sr && (w == null || w.Trim() != "0") ? "on" : "off"));
                string u = Ini.Get(dx, "upscaling"); int un;
                d.Add("Upscaling: " + (u != null && int.TryParse(u.Trim(), out un) && un > 0 ? "on" : "off"));
                string sep = Ini.GetPersisted(dir, "separation") ?? Ini.Get(dm, "dm_separation");
                string conv = Ini.GetPersisted(dir, "convergence") ?? Ini.Get(dm, "dm_convergence");
                if (sep != null || conv != null) d.Add("Separation / convergence: " + (sep ?? "?").Trim() + " / " + (conv ?? "?").Trim());
            }
            d.Add("Windows: " + Environment.OSVersion.Version);
            string gpu = Gpu();
            if (gpu != null) d.Add("GPU: " + gpu);
            return string.Join("\n", d);
        }

        static string Application() { return typeof(Report).Assembly.Location; }   // the installer exe (also when loaded by a test host)

        // display adapters from the registry (skips Microsoft's basic adapters)
        static string Gpu()
        {
            try
            {
                var names = new List<string>();
                using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                    if (cls != null)
                        foreach (var sub in cls.GetSubKeyNames().Where(n => Regex.IsMatch(n, @"^\d{4}$")))
                            using (var k = cls.OpenSubKey(sub))
                            {
                                string desc = k == null ? null : k.GetValue("DriverDesc") as string;
                                if (!string.IsNullOrWhiteSpace(desc) && !desc.StartsWith("Microsoft") && !names.Contains(desc)) names.Add(desc.Trim());
                            }
                return names.Count > 0 ? string.Join(", ", names) : null;
            }
            catch { return null; }
        }

        public static string Url(string exe, string dx9Pkg, string pcgw)
        {
            string name = GameName(exe);
            return FormUrl + "?template=" + (Lang.English ? "game_report.yml" : "game_report.ko.yml") +
                   "&title=" + Uri.EscapeDataString("[3D] " + name) +
                   "&game=" + Uri.EscapeDataString(name) +
                   "&pcgw=" + Uri.EscapeDataString(pcgw ?? "") +
                   "&used=" + Uri.EscapeDataString(MethodUsed(exe)) +
                   "&details=" + Uri.EscapeDataString(Details(exe, dx9Pkg));
        }
    }
}
