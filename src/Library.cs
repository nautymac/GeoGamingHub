// Candidate list: which 3D method fits a game.
//   data\game_candidates.json  the author's owned library classified by tools\library_survey (PCGamingWiki engine and
//                              graphics API, plus this project's test results)
//   installed games            Steam libraries and Heroic (Epic, GOG) on this PC; a game without a data entry gets
//                              its method from its exe (Unity / UE4 layout, imported graphics DLLs)
// Installed-game scanning and the exe pick follow Flato3D-SR's installer (Library.cs).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace GeoGamingHub
{
    sealed class InstalledGame
    {
        public string Title, Store, Dir, SteamAppId, HeroicExe;
    }

    sealed class Candidate
    {
        public string Title, SteamAppId, Method, Engine, Api, Bits, Status, Note;
        public string Product = "hub";            // hub (geometric stereo), flato3d (flat 2D layers: Flato3D-SR), none (AI 2D->3D only)
        public string FlatoRoute, FlatoStatus;     // Flato3D-SR's route (opengl / wined3d / d3d9 / d3d11 ...) and test status
        public string[] Alts = new string[0];
        public bool Flat2D, FromExe;   // FromExe: an installed game without a data entry, method read from its exe
        public InstalledGame Installed;
        public string Exe;             // the installed game's main exe (found on demand)
        public string AntiCheat;       // name of the anti-cheat found in the installed game's folder: never set up
    }

    static class Library
    {
        public static readonly string[] Methods = { "unity", "ue4", "dx11", "dx9", "opengl", "vulkan", "tridef", "dx12", "old", "none", "unknown" };

        public static string MethodLabel(string m)
        {
            switch (m)
            {
                case "unity": return Lang.T("geo-11 Unity");
                case "ue4": return Lang.T("geo-11 UE4 (UF2)");
                case "dx11": return Lang.T("geo-11 일반 (DX11)");
                case "dx9": return Lang.T("DX9 → dgVoodoo2 + geo-11");
                case "opengl": return Lang.T("OpenGL → OGL3D");
                case "vulkan": return Lang.T("Vulkan → VK3D");
                case "tridef": return Lang.T("TriDef 3D (프로필, 설치 필수)");
                case "dx12": return Lang.T("DX12 → DX12_3D");
                case "old": return Lang.T("DX8 이하 (미지원)");
                case "none": return Lang.T("불가");
                default: return Lang.T("미확인");
            }
        }

        public static readonly string[] Products = { "hub", "flato3d", "none" };

        public static string ProductLabel(string p)
        {
            return p == "flato3d" ? "Flato3D" : p == "none" ? Lang.T("AI 2D→3D만") : "Geo Gaming Hub";
        }

        // Flato3D-SR's route for a flat 2D game (its survey's api / its profile's route)
        public static string FlatoRouteLabel(string r)
        {
            switch (r)
            {
                case "opengl": return Lang.T("Flato3D: OpenGL");
                case "opengl_alt": return Lang.T("Flato3D: OpenGL (전환 필요)");
                case "d3d9": return Lang.T("Flato3D: D3D9 → WineD3D");
                case "d3d11": return Lang.T("Flato3D: D3D11 → WineD3D");
                case "wined3d": return Lang.T("Flato3D: WineD3D");
                case "browser": return Lang.T("Flato3D: 어려움 (브라우저)");
                case "unsupported": return Lang.T("Flato3D: 불가 (D3D12/Vulkan)");
                default: return Lang.T("Flato3D: API 미확인");
            }
        }

        public static string StatusLabel(Candidate c)
        {
            if (c.FromExe) return Lang.T("exe 분석");
            if (c.Product == "flato3d")
                return c.FlatoStatus == "verified" ? Lang.T("검증됨") : c.FlatoStatus == "working" ? Lang.T("동작") : c.FlatoStatus == "checked" ? Lang.T("확인함") : Lang.T("시험 전");
            switch (c.Status)
            {
                case "verified": return Lang.T("검증됨");
                case "checked": return Lang.T("확인함");
                default: return Lang.T("시험 전");
            }
        }

        public static string Norm(string s) { return s == null ? "" : Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]", ""); }

        // ---- data file
        public static List<Candidate> Load(string file)
        {
            var list = new List<Candidate>();
            if (file == null || !File.Exists(file)) return list;
            var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
            object games;
            if (root == null || !root.TryGetValue("games", out games) || !(games is object[])) return list;
            foreach (var o in (object[])games)
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                list.Add(new Candidate
                {
                    Title = Str(d, "title"), SteamAppId = Str(d, "steam_appid"), Method = Str(d, "method") ?? "unknown",
                    Alts = Arr(d, "alts"), Engine = string.Join(", ", Arr(d, "engine")), Api = Str(d, "api") ?? "", Bits = Str(d, "bits") ?? "",
                    Status = Str(d, "status") ?? "untested", Note = Str(d, "note") ?? "", Flat2D = Str(d, "flat2d") == "True",
                    Product = Str(d, "product") ?? "hub", FlatoRoute = Str(d, "flato_route") ?? "", FlatoStatus = Str(d, "flato_status") ?? ""
                });
            }
            return list;
        }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        static string[] Arr(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is object[] ? ((object[])v).Select(x => Convert.ToString(x)).ToArray() : new string[0];
        }

        // ---- installed games
        public static List<InstalledGame> ScanInstalled()
        {
            var games = new List<InstalledGame>();
            try { ScanSteam(games); } catch { }
            try { ScanHeroic(games); } catch { }
            return games;
        }

        static void ScanSteam(List<InstalledGame> games)
        {
            string steam = Launchers.SteamPath();
            if (steam == null) return;
            steam = steam.Replace('/', '\\');
            var libs = new List<string> { steam };
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string apps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(apps)) continue;
                foreach (string acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
                {
                    string t = File.ReadAllText(acf, Encoding.UTF8);
                    string id = Field(t, "appid"), name = Field(t, "name"), dir = Field(t, "installdir");
                    if (id == null || dir == null || id == "228980" || Regex.IsMatch(name ?? "", "Proton|Steamworks|Steam Linux Runtime|SteamVR|Redistributable")) continue;
                    string full = Path.Combine(apps, "common", dir);
                    if (!Directory.Exists(full) || !seen.Add(full)) continue;
                    games.Add(new InstalledGame { Title = name ?? dir, Store = "Steam", Dir = full, SteamAppId = id });
                }
            }
        }

        static string Field(string acf, string key)
        {
            var m = Regex.Match(acf, "\"" + key + "\"\\s+\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        static void ScanHeroic(List<InstalledGame> games)
        {
            var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            string h = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic");
            string epic = Path.Combine(h, "legendaryConfig", "legendary", "installed.json");
            if (File.Exists(epic))
                foreach (var kv in (Dictionary<string, object>)js.DeserializeObject(File.ReadAllText(epic, Encoding.UTF8)))
                {
                    var e = kv.Value as Dictionary<string, object>;
                    if (e == null) continue;
                    string dir = Str(e, "install_path");
                    if (dir == null || !Directory.Exists(dir) || Str(e, "is_dlc") == "True") continue;
                    games.Add(new InstalledGame { Title = Str(e, "title") ?? Path.GetFileName(dir), Store = "Epic", Dir = dir, HeroicExe = Str(e, "executable") });
                }
            string gog = Path.Combine(h, "gog_store", "installed.json");
            if (File.Exists(gog))
            {
                var root = js.DeserializeObject(File.ReadAllText(gog, Encoding.UTF8)) as Dictionary<string, object>;
                object inst;
                if (root != null && root.TryGetValue("installed", out inst) && inst is object[])
                    foreach (var o in (object[])inst)
                    {
                        var e = o as Dictionary<string, object>;
                        if (e == null) continue;
                        string dir = Str(e, "install_path");
                        if (dir == null || !Directory.Exists(dir) || Str(e, "is_dlc") == "True") continue;
                        string title = null, a, b;
                        try { if (!Launchers.FindHeroicApp(dir, out a, out b, out title)) title = null; } catch { }
                        games.Add(new InstalledGame { Title = title ?? Path.GetFileName(dir.TrimEnd('\\')), Store = "GOG", Dir = dir, HeroicExe = Str(e, "executable") });
                    }
            }
        }

        // the data entries plus every installed game (matched by Steam appid, else by title / folder name)
        public static List<Candidate> Merge(List<Candidate> data, List<InstalledGame> installed)
        {
            var bySteam = new Dictionary<string, Candidate>();
            var byTitle = new Dictionary<string, Candidate>();
            foreach (var c in data)
            {
                if (!string.IsNullOrEmpty(c.SteamAppId) && !bySteam.ContainsKey(c.SteamAppId)) bySteam[c.SteamAppId] = c;
                string n = Norm(c.Title);
                if (n.Length > 0 && !byTitle.ContainsKey(n)) byTitle[n] = c;
            }
            var all = new List<Candidate>(data);
            foreach (var g in installed)
            {
                Candidate c = null;
                if (g.SteamAppId != null) bySteam.TryGetValue(g.SteamAppId, out c);
                if (c == null) byTitle.TryGetValue(Norm(g.Title), out c);
                if (c == null) byTitle.TryGetValue(Norm(Path.GetFileName(g.Dir.TrimEnd('\\'))), out c);
                if (c == null)
                {
                    c = new Candidate { Title = g.Title, SteamAppId = g.SteamAppId, Method = null, FromExe = true, Status = "untested", Note = "" };
                    all.Add(c);
                }
                if (c.Installed == null) c.Installed = g;
            }
            return all;
        }

        // ---- the main exe of an installed game: Heroic's record, an Unreal shipping exe, else the exe whose name matches
        // the game best (the largest among equals)
        static readonly string[] NotGame = { "unins", "crash", "setup", "launcher", "redist", "vcredist", "dxsetup", "dotnet", "update", "helper", "report", "unitycrashhandler", "ue4prereq", "easyanticheat", "cefsharp", "nw.exe" };

        public static string FindExe(InstalledGame g)
        {
            if (!string.IsNullOrEmpty(g.HeroicExe) && File.Exists(Path.Combine(g.Dir, g.HeroicExe))) return Path.Combine(g.Dir, g.HeroicExe);
            var ship = SafeFiles(g.Dir, "*-Shipping.exe", SearchOption.AllDirectories).FirstOrDefault(f => f.IndexOf(@"\Binaries\Win", StringComparison.OrdinalIgnoreCase) >= 0);
            if (ship != null) return ship;
            var exes = new List<string>(SafeFiles(g.Dir, "*.exe", SearchOption.TopDirectoryOnly));
            foreach (string sub in new[] { "bin", "Bin", "x64", "bin64", "Binaries", "game", "Game" })
                if (Directory.Exists(Path.Combine(g.Dir, sub))) exes.AddRange(SafeFiles(Path.Combine(g.Dir, sub), "*.exe", SearchOption.TopDirectoryOnly));
            exes = exes.Where(e => !NotGame.Any(n => Path.GetFileName(e).ToLowerInvariant().Contains(n))).ToList();
            if (exes.Count == 0) return null;
            string nt = Norm(g.Title), nf = Norm(Path.GetFileName(g.Dir.TrimEnd('\\')));
            return exes.OrderByDescending(e => { string n = Norm(Path.GetFileNameWithoutExtension(e)); return n == nt || n == nf ? 2 : (nt.Contains(n) || nf.Contains(n)) && n.Length > 2 ? 1 : 0; })
                       .ThenByDescending(e => new FileInfo(e).Length).First();
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern, SearchOption opt)
        {
            try { return Directory.GetFiles(dir, pattern, opt); } catch { return new string[0]; }
        }

        // Anti-cheat in a game folder (up to three levels down): folders named EasyAntiCheat, BattlEye, GameGuard (nProtect),
        // XIGNCODE or *AntiCheat*, and executables / drivers (.exe .dll .sys .des) with those names, mhyprot or Denuvo
        // Anti-Cheat (ARC Raiders: Installers\AntiCheatInstaller.exe, HELLDIVERS 2: bin\GameGuard). Only folder and binary
        // names count: game content such as a "ricochet1.ogg" sound must not match. A proxy DLL next to such a game can
        // get the player banned, so the hub never sets these up.
        static readonly Regex AntiCheatDir = new Regex(@"^(easyanticheat.*|battleye|gameguard|xigncode.*|.*anti.?cheat.*)$", RegexOptions.IgnoreCase);
        static readonly Regex AntiCheatBin = new Regex(@"easyanticheat|beservice|battleye|gameguard|xigncode|anti.?cheat|mhyprot|denuvo.?anti", RegexOptions.IgnoreCase);
        static readonly string[] BinExt = { ".exe", ".dll", ".sys", ".des" };

        public static string AntiCheat(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return null;
            var todo = new Queue<KeyValuePair<string, int>>();
            todo.Enqueue(new KeyValuePair<string, int>(dir, 0));
            int seen = 0;
            while (todo.Count > 0 && seen < 20000)
            {
                var cur = todo.Dequeue();
                IEnumerable<string> entries;
                try { entries = Directory.EnumerateFileSystemEntries(cur.Key); } catch { continue; }
                foreach (var e in entries)
                {
                    if (++seen > 20000) break;
                    string n = Path.GetFileName(e);
                    bool isDir = Directory.Exists(e);
                    if (isDir ? AntiCheatDir.IsMatch(n) : BinExt.Contains(Path.GetExtension(n).ToLowerInvariant()) && AntiCheatBin.IsMatch(n)) return n;
                    if (isDir && cur.Value < 2) todo.Enqueue(new KeyValuePair<string, int>(e, cur.Value + 1));
                }
            }
            return null;
        }

        // the method an exe points to (the same order as the 3D method step's detection)
        public static string MethodOfExe(string exe)
        {
            if (exe == null || !File.Exists(exe)) return "unknown";
            string dir = Path.GetDirectoryName(exe);
            if (VkOps.Installed(dir)) return "vulkan";
            if (!File.Exists(Path.Combine(dir, "d3d11.dll")) && TriOps.RegisteredName(exe) != null) return "tridef";
            if (Dx12Ops.Installed(dir)) return "dx12";
            if (GlOps.Installed(dir)) return "opengl";
            if (Util.UnityVersion(dir, exe) != null) return "unity";
            if (Util.IsUe4(exe)) return "ue4";
            if (GlOps.IsOpenGL(exe)) return "opengl";
            if (VkOps.ImportsVulkan(exe) && !VkOps.ImportsDirect3D(exe)) return "vulkan";
            switch (Util.DirectXOf(exe))
            {
                case "DX11": return "dx11";
                case "DX9": return "dx9";
                case "DX12": return "dx12";
            }
            return "unknown";
        }
    }
}
