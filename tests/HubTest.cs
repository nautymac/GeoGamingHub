// Self-tests of Geo Gaming Hub (OpenGL kind here, Vulkan kind in VkTests.cs).
//   HubTest.exe <GeoGamingHub.exe> <out dir> [unit|gui|survey|pkg|vkunit|vkgui|cand|dx12unit|dx12gui]
// build (tests folder): csc /target:exe /win32manifest:..\src\app.manifest /out:..\HubTest.exe /r:System.Windows.Forms.dll
//                      /r:System.Drawing.dll /r:System.Core.dll HubTest.cs VkTests.cs CandTests.cs Dx12Tests.cs
// HubTest.exe must sit in the project root (next to Packages\) for gui / vkgui / pkg.
// unit:   GlOps on temp folders + detection on the installed games (read-only)
// gui:    MainForm in-process (it finds Packages\ next to Application.ExecutablePath); wizard flows on copies of game exes; message boxes are answered
//         OK / Yes by a watcher thread and recorded
// survey: IsOpenGL / DirectXOf over every exe of the Steam library
// pkg:    MainForm from an unpacked all-in-one zip: every package folder found
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static partial class HubTest
{
    static Assembly A;
    static int pass, fail;
    static string outDir;
    static readonly List<string> dialogs = new List<string>();
    const string Common = @"E:\Steam\steamapps\common\";
    static readonly string Curse = Common + @"Curse of the Dead Gods\Curse of the Dead Gods.exe";
    static readonly string Amnesia = Common + @"Amnesia The Dark Descent\Amnesia.exe";
    static readonly string BrokenAge = Common + @"Broken Age\BrokenAge.exe";
    static readonly string Dd64 = Common + @"DarkestDungeon\_windows\win64\Darkest.exe";
    static readonly string Dd32 = Common + @"DarkestDungeon\_windows\win32\Darkest.exe";
    static readonly string SineMora = Common + @"Sine Mora EX\SineMoraEX.exe";
    static readonly string ActionSquad = Common + @"Door Kickers - Action Squad\ActionSquad.exe";
    static readonly string Rfg = Common + @"Red Faction Guerrilla Re-MARS-tered\rfg.exe";
    static readonly string SteelRats = Common + @"Steel Rats\SteelRats\Binaries\Win64\SteelRats-Win64-Shipping.exe";
    static readonly string Torchlight = Common + @"Torchlight\Torchlight.exe";
    static readonly string ArtOfRally = @"E:\Heroic\Games\artofrally\artofrally.exe";

    static void Ok(bool c, string what)
    {
        if (c) pass++; else fail++;
        Console.WriteLine((c ? "PASS " : "FAIL ") + what);
    }

    static void Eq(object got, object want, string what)
    {
        bool c = Equals(got, want) || (got != null && want != null && got.ToString() == want.ToString());
        Ok(c, what + (c ? "" : "  (got '" + got + "', want '" + want + "')"));
    }

    [STAThread]
    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        A = Assembly.LoadFrom(args[0]);
        outDir = args[1];
        Directory.CreateDirectory(outDir);
        string mode = args.Length > 2 ? args[2] : "unit";
        var sw = Stopwatch.StartNew();
        new Thread(() => { Thread.Sleep(240000); Console.WriteLine("FAIL timeout - " + pass + " passed, " + fail + " failed so far"); Environment.Exit(3); }) { IsBackground = true }.Start();
        try
        {
            if (mode == "unit") Unit(args[0]);
            else if (mode == "gui") Gui();
            else if (mode == "survey") Survey();
            else if (mode == "pkg") Pkg();
            else if (mode == "vkunit") VkUnit(args[0]);
            else if (mode == "vkgui") VkGui();
            else if (mode == "cand") Cands();
            else if (mode == "tri") Tri();
            else if (mode == "dx12unit") Dx12Unit(args[0]);
            else if (mode == "dx12gui") Dx12Gui();
        }
        catch (Exception e) { fail++; Console.WriteLine("FAIL exception: " + e); }
        Console.WriteLine("== " + mode + ": " + pass + " passed, " + fail + " failed (" + sw.ElapsedMilliseconds + " ms)");
        return fail == 0 ? 0 : 1;
    }

    // ---- reflection helpers
    static Type Ty(string n) { return A.GetType("GeoGamingHub." + n, true); }

    static object S(string type, string m, params object[] a)
    {
        var mi = Ty(type).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).First(x => x.Name == m && x.GetParameters().Length == a.Length);
        try { return mi.Invoke(null, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    static object F(object o, string name)
    {
        return o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);
    }

    static T C<T>(object o, string name) { return (T)F(o, name); }

    static object Call(object o, string m, params object[] a)
    {
        var mi = o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).First(x => x.Name == m && x.GetParameters().Length == a.Length);
        try { return mi.Invoke(o, a); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }

    // the control's own visibility flag (Control.Visible is false while a parent, e.g. an unselected tab, is hidden)
    static bool Shown(Control c)
    {
        return (bool)typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, new object[] { 2 });
    }

    static object Opt(bool sr, string mode, bool swap, bool up, decimal sep, bool auto, decimal conv)
    {
        var t = Ty("GlOps+Options");
        var o = Activator.CreateInstance(t);
        t.GetField("Sr").SetValue(o, sr); t.GetField("Mode").SetValue(o, mode); t.GetField("Swap").SetValue(o, swap);
        t.GetField("Upscale").SetValue(o, up); t.GetField("Separation").SetValue(o, sep); t.GetField("Auto").SetValue(o, auto);
        t.GetField("Convergence").SetValue(o, conv);
        return o;
    }

    static string Desc(object o) { return (string)S("GlOps", "Describe", o); }
    static string Get(string ini, string sec, string key) { return (string)S("GlOps", "Get", ini, sec, key); }

    static int Machine(string f)
    {
        var b = File.ReadAllBytes(f);
        int pe = BitConverter.ToInt32(b, 0x3C);
        return BitConverter.ToUInt16(b, pe + 4);
    }

    static bool SameBytes(string a, string b) { return File.ReadAllBytes(a).SequenceEqual(File.ReadAllBytes(b)); }

    static string Fresh(string name)
    {
        string d = Path.Combine(outDir, name);
        if (Directory.Exists(d)) Directory.Delete(d, true);
        Directory.CreateDirectory(d);
        return d;
    }

    static readonly string[] Keys5 = { "output", "swap_eyes", "upscale", "separation", "convergence" };

    // the file's lines without the five keys the wizard writes (to check that nothing else changed)
    static List<string> Rest(string ini)
    {
        return File.ReadAllLines(ini, Encoding.Default).Where(l =>
        {
            int eq = l.IndexOf('=');
            string k = eq > 0 ? l.Substring(0, eq).Trim() : "";
            return !(eq > 0 && !l.TrimStart().StartsWith(";") && Keys5.Contains(k));
        }).ToList();
    }

    // ================================================================ unit
    static void Unit(string installer)
    {
        string pkg = Path.Combine(Path.GetDirectoryName(installer), @"Packages\OGL3D");
        Ok(Directory.Exists(pkg), "package folder " + pkg);

        // Util.RunHidden: a batch file that writes far more to stderr than a pipe holds (Uninstall.bat in a long game
        // path, Blade Runner Enhanced Edition) must not deadlock, and "pause" must not wait for a key
        string longDir = Path.Combine(outDir, "Very Long Game Folder Name For The Uninstall Pipe Test", "Another Long Sub Folder Name Here");
        Directory.CreateDirectory(longDir);
        var bat = new StringBuilder();
        for (int i = 0; i < 300; i++) bat.AppendLine("del not_there_" + i + ".dll");
        bat.AppendLine("pause");
        bat.AppendLine("echo done> finished.txt");
        File.WriteAllText(Path.Combine(longDir, "Uninstall.bat"), bat.ToString());
        var sw = Stopwatch.StartNew();
        string runOut = (string)S("Util", "RunHidden", "cmd.exe", "/c \"" + Path.Combine(longDir, "Uninstall.bat") + "\"", longDir);
        Ok(sw.ElapsedMilliseconds < 30000 && File.Exists(Path.Combine(longDir, "finished.txt")), "RunHidden: noisy batch file finishes (" + sw.ElapsedMilliseconds + " ms, " + runOut.Length + " chars of output)");

        // detection on the installed games (read-only)
        foreach (var e in new[] { Curse, Amnesia, BrokenAge, Dd64, Dd32 })
        {
            string dir = Path.GetDirectoryName(e);
            Ok((bool)S("GlOps", "IsOpenGL", e), "IsOpenGL true: " + e);
            Ok((bool)S("GlOps", "Installed", dir), "Installed true (OGL3D deployed): " + dir);
            Eq(S("GlOps", "Version", Path.Combine(dir, "opengl32.dll")), "1.0.0", "Version: " + dir);
        }
        Eq(S("Util", "DirectXOf", BrokenAge), "DX9", "Broken Age: DirectXOf says DX9 (so OpenGL must be checked first)");
        foreach (var e in new[] { SineMora, ActionSquad, Rfg, SteelRats, Torchlight, ArtOfRally, @"C:\Windows\System32\notepad.exe" })
            if (File.Exists(e)) Ok(!(bool)S("GlOps", "IsOpenGL", e), "IsOpenGL false: " + e);
            else Console.WriteLine("skip (missing) " + e);
        Ok(!(bool)S("GlOps", "IsOpenGL", Path.Combine(outDir, "nope.exe")), "IsOpenGL false for a missing file");
        Ok(!(bool)S("GlOps", "IsOgl3d", @"C:\Windows\System32\opengl32.dll"), "IsOgl3d false for System32 opengl32.dll");
        Ok(!(bool)S("GlOps", "IsOgl3d", Path.Combine(outDir, "nope.dll")), "IsOgl3d false for a missing file");
        Ok(!(bool)S("GlOps", "Installed", (string)null), "Installed false for null");
        Ok((bool)S("GlOps", "IsOgl3d", Path.Combine(pkg, @"x64\opengl32.dll")), "IsOgl3d package x64");
        Ok((bool)S("GlOps", "IsOgl3d", Path.Combine(pkg, @"x86\opengl32.dll")), "IsOgl3d package x86");
        Eq(Machine(Path.Combine(pkg, @"x64\opengl32.dll")), 0x8664, "package x64 DLL is AMD64");
        Eq(Machine(Path.Combine(pkg, @"x86\opengl32.dll")), 0x14c, "package x86 DLL is i386");

        // Read: real game inis (read-only), package defaults, missing file
        Eq(Desc(S("GlOps", "Read", Path.Combine(Path.GetDirectoryName(Curse), "OGL3D.ini"))), "output=auto swap_eyes=0 upscale=0 separation=18.05 convergence=21.6692", "Read Curse ini (in-game saved values)");
        var ba = S("GlOps", "Read", Path.Combine(Path.GetDirectoryName(BrokenAge), "OGL3D.ini"));
        Ok(Desc(ba).Contains("convergence=auto"), "Read Broken Age ini: auto  (" + Desc(ba) + ")");
        // auto_start is rewritten by the game while it runs: compare with the file
        Eq(ba.GetType().GetField("Convergence").GetValue(ba), decimal.Parse(Get(Path.Combine(Path.GetDirectoryName(BrokenAge), "OGL3D.ini"), "Stereo", "auto_start"), System.Globalization.CultureInfo.InvariantCulture), "Read Broken Age ini: auto_start as the shown convergence");
        Eq(Desc(S("GlOps", "Read", Path.Combine(pkg, "OGL3D.ini"))), "output=auto swap_eyes=0 upscale=0 separation=50 convergence=auto", "Read package defaults");
        Eq(Desc(S("GlOps", "Read", Path.Combine(outDir, "missing.ini"))), "output=auto swap_eyes=0 upscale=0 separation=50 convergence=auto", "Read missing file = defaults");

        // Get / Set: section-aware, comments, new keys and sections
        string d0 = Fresh("ini");
        string t = Path.Combine(d0, "t.ini");
        File.WriteAllText(t, "; head\r\n[Stereo]\r\nenable = 1\r\n; separation = 99\r\nseparation = 50\r\n\r\n[Inject]\r\nenable = 1\r\n", Encoding.Default);
        Eq(Get(t, "Stereo", "separation"), "50", "Get skips commented keys");
        Eq(Get(t, "stereo", "ENABLE"), "1", "Get is case-insensitive");
        Eq(Get(t, "Stereo", "missing"), null, "Get missing key = null");
        S("GlOps", "Set", t, "Stereo", "enable", "0");
        Eq(Get(t, "Stereo", "enable"), "0", "Set [Stereo] enable");
        Eq(Get(t, "Inject", "enable"), "1", "Set [Stereo] enable leaves [Inject] enable alone");
        S("GlOps", "Set", t, "Stereo", "newkey", "x");
        var lines = File.ReadAllLines(t);
        int iNew = Array.IndexOf(lines, "newkey = x"), iInj = Array.IndexOf(lines, "[Inject]");
        Ok(iNew > 0 && iNew < iInj && lines[iInj - 1].Trim() == "", "Set adds a new key at the end of its section, before the blank line");
        S("GlOps", "Set", t, "Display", "upscale", "1");
        Eq(Get(t, "Display", "upscale"), "1", "Set creates a missing section");
        Ok(File.ReadAllLines(t).Last() == "upscale = 1" && File.ReadAllLines(t).Contains("[Display]"), "new section at the end");
        File.WriteAllText(t, "[Stereo]\r\noutput = TAB\r\nconvergence = AUTO\r\nauto_start = 0\r\n", Encoding.Default);
        Eq(Desc(S("GlOps", "Read", t)), "output=tab swap_eyes=0 upscale=0 separation=50 convergence=auto", "Read: case-insensitive values, auto_start 0 ignored");
        File.WriteAllText(t, "[Stereo]\r\noutput = sr\r\nconvergence = 4000.5\r\nseparation = 12.5\r\n", Encoding.Default);
        Eq(Desc(S("GlOps", "Read", t)), "output=auto swap_eyes=0 upscale=0 separation=12.5 convergence=4000.5", "Read: output=sr counts as SR weaving");

        // Install into an empty folder (x64)
        string dA = Fresh("instA");
        var oA = Opt(true, "sbs", false, false, 50m, true, 3m);
        var logA = (List<string>)S("GlOps", "Install", pkg, 64, dA, oA);
        Console.WriteLine("  log: " + string.Join(" / ", logA));
        string dllA = Path.Combine(dA, "opengl32.dll"), iniA = Path.Combine(dA, "OGL3D.ini");
        Ok(File.Exists(dllA) && Machine(dllA) == 0x8664 && (bool)S("GlOps", "IsOgl3d", dllA), "Install x64: OGL3D x64 opengl32.dll");
        Ok(SameBytes(dllA, Path.Combine(pkg, @"x64\opengl32.dll")), "Install x64: identical to the package DLL");
        Eq(File.GetLastWriteTimeUtc(dllA), File.GetLastWriteTimeUtc(Path.Combine(pkg, @"x64\opengl32.dll")), "Install keeps the DLL timestamp");
        Ok(File.Exists(iniA), "Install copies OGL3D.ini");
        Eq(Desc(S("GlOps", "Read", iniA)), Desc(oA), "Install writes the options");
        Ok(Rest(iniA).SequenceEqual(Rest(Path.Combine(pkg, "OGL3D.ini"))), "Install: the rest of the package ini unchanged");
        Ok(!File.Exists(dllA + ".geo11sr_bak"), "Install into empty folder: no backup");
        // upgrade / reinstall over OGL3D: no backup of our own DLL
        var logA2 = (List<string>)S("GlOps", "Install", pkg, 64, dA, Opt(false, "right", true, true, 77m, false, 9.5m));
        Ok(!File.Exists(dllA + ".geo11sr_bak"), "Reinstall over OGL3D: no backup");
        Eq(Desc(S("GlOps", "Read", iniA)), "output=right swap_eyes=1 upscale=1 separation=77 convergence=9.5", "Reinstall writes new options into the existing ini");
        Ok(logA2.Any(l => l.Contains("OGL3D.ini") && l.Contains("이미")), "Reinstall logs that the ini was kept");

        // Install over a foreign opengl32.dll (x86), then Remove restores it
        string dB = Fresh("instB");
        string dllB = Path.Combine(dB, "opengl32.dll");
        File.Copy(@"C:\Windows\SysWOW64\opengl32.dll", dllB);
        string foreign = Path.Combine(outDir, "foreign_opengl32.dll");
        File.Copy(@"C:\Windows\SysWOW64\opengl32.dll", foreign, true);
        S("GlOps", "Install", pkg, 32, dB, Opt(true, "sbs", false, false, 50m, true, 3m));
        Ok(File.Exists(dllB + ".geo11sr_bak") && SameBytes(dllB + ".geo11sr_bak", foreign), "Install over a foreign DLL keeps it as .geo11sr_bak");
        Ok(Machine(dllB) == 0x14c && (bool)S("GlOps", "IsOgl3d", dllB), "Install x86: OGL3D x86 opengl32.dll");
        File.WriteAllText(Path.Combine(dB, "OGL3D.log"), "log");
        foreach (var n in new[] { "OGL3D_frame00012.bmp", "OGL3D_frame00012_srinput.bmp", "OGL3D_flicker3.bmp", "keep.bmp", "game.exe" })
            File.WriteAllText(Path.Combine(dB, n), "x");
        var logB = (List<string>)S("GlOps", "Remove", dB);
        Console.WriteLine("  log: " + string.Join(" / ", logB));
        Ok(File.Exists(dllB) && SameBytes(dllB, foreign), "Remove restores the foreign DLL");
        Ok(!File.Exists(dllB + ".geo11sr_bak"), "Remove: backup consumed");
        var left = Directory.GetFiles(dB).Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Eq(string.Join(",", left), "game.exe,keep.bmp,opengl32.dll", "Remove deletes ini, log and frame dumps only");
        Ok(logB.Any(l => l.Contains("3")), "Remove logs the number of dumps");
        // Remove when nothing of OGL3D is there: no change
        var logB2 = (List<string>)S("GlOps", "Remove", dB);
        Ok(logB2.Count == 0 && SameBytes(dllB, foreign), "Remove on a folder without OGL3D changes nothing");

        // existing game ini (copy of the Curse ini with saved values): only the five keys change
        string dC = Fresh("instC");
        string iniC = Path.Combine(dC, "OGL3D.ini");
        File.Copy(Path.Combine(Path.GetDirectoryName(Curse), "OGL3D.ini"), iniC);
        var before = Rest(iniC);
        var oC = Opt(false, "tab", true, true, 33.5m, false, 123.4567m);
        S("GlOps", "Install", pkg, 64, dC, oC);
        Eq(Desc(S("GlOps", "Read", iniC)), Desc(oC), "existing ini: options written");
        Ok(Rest(iniC).SequenceEqual(before), "existing ini: every other line unchanged (saved keys, [Inject], [ShaderOverride] ...)");
        Eq(Get(iniC, "Inject", "enable"), "1", "existing ini: [Inject] enable untouched");
        S("GlOps", "Write", iniC, Opt(true, "sbs", false, false, 18.05m, true, 21.6692m));
        Eq(Get(iniC, "Stereo", "convergence"), "auto", "Write auto -> convergence = auto");
        Eq(Get(iniC, "Stereo", "output"), "auto", "Write SR -> output = auto");
        Eq(Get(iniC, "Stereo", "separation"), "18.05", "Write separation format");
        // missing package -> clear error, game folder untouched
        string dD = Fresh("instD");
        try { S("GlOps", "Install", Path.Combine(outDir, "nopkg"), 64, dD, oA); Ok(false, "Install from a missing package throws"); }
        catch (FileNotFoundException ex) { Ok(ex.Message.Contains("opengl32.dll"), "Install from a missing package throws FileNotFound (" + ex.Message + ")"); }
        Ok(Directory.GetFiles(dD).Length == 0, "missing package: nothing written");
    }

    // ================================================================ survey
    static void Survey()
    {
        var roots = new[] { @"E:\Steam\steamapps\common", @"E:\Heroic\Games" };
        foreach (var r in roots.Where(Directory.Exists))
            foreach (var game in Directory.GetDirectories(r))
            {
                IEnumerable<string> exes;
                try { exes = Directory.EnumerateFiles(game, "*.exe", SearchOption.AllDirectories).Where(f => f.Count(ch => ch == '\\') - game.Count(ch => ch == '\\') <= 4).ToList(); }
                catch { continue; }
                foreach (var e in exes)
                {
                    long len = new FileInfo(e).Length;
                    if (len > 600L << 20) { Console.WriteLine("skip (big) " + e); continue; }
                    var sw = Stopwatch.StartNew();
                    bool gl = (bool)S("GlOps", "IsOpenGL", e);
                    long ms = sw.ElapsedMilliseconds;
                    string dx = (string)S("Util", "DirectXOf", e);
                    HashSet<string> imp;
                    try { imp = (HashSet<string>)S("Util", "PeImports", e); } catch { imp = new HashSet<string>(); }
                    var gfx = imp.Where(i => i.StartsWith("d3d") || i == "dxgi.dll" || i == "opengl32.dll" || i.StartsWith("vulkan")).OrderBy(x => x);
                    Console.WriteLine((gl ? "GL   " : "     ") + (dx ?? "-").PadRight(5) + (ms + "ms").PadLeft(7) + "  " + (len >> 20) + "MB  [" + string.Join(",", gfx) + "]  " + e.Substring(r.Length + 1));
                }
            }
    }

    // ================================================================ gui
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetDlgItemText(IntPtr h, int id, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

    // answers every message box of this process with Yes (if it has one) or OK, and records caption | text
    static void Watcher()
    {
        uint me = (uint)Process.GetCurrentProcess().Id;
        var seen = new HashSet<IntPtr>();
        while (true)
        {
            seen.RemoveWhere(h => !IsWindow(h));
            EnumWindows((h, l) =>
            {
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid != me || !IsWindowVisible(h)) return true;
                var cls = new StringBuilder(64); GetClassName(h, cls, 64);
                if (cls.ToString() != "#32770") return true;
                if (seen.Add(h))
                {
                    var cap = new StringBuilder(256); GetWindowText(h, cap, 256);
                    var txt = new StringBuilder(8192); GetDlgItemText(h, 0xFFFF, txt, 8192);
                    lock (dialogs) dialogs.Add(cap + " | " + txt.ToString().Replace("\r", "").Replace("\n", " "));
                }
                // Yes if there is one, else OK (a MB_OK box's only button has the id IDCANCEL = 2)
                int id = GetDlgItem(h, 6) != IntPtr.Zero ? 6 : GetDlgItem(h, 1) != IntPtr.Zero ? 1 : 2;
                PostMessage(h, 0x0111, (IntPtr)id, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
            Thread.Sleep(100);
        }
    }

    static List<string> TakeDialogs() { lock (dialogs) { var r = dialogs.ToList(); dialogs.Clear(); return r; } }

    static void Pump() { for (int i = 0; i < 8; i++) { Application.DoEvents(); Thread.Sleep(15); } }

    static void Shot(Form f, string name)
    {
        Pump();
        using (var bmp = new Bitmap(f.Width, f.Height))
        {
            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
            bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
        }
    }

    static string[] Row(object form, string exe)
    {
        var lv = C<ListView>(form, "lvGames");
        foreach (ListViewItem it in lv.Items)
            if (string.Equals((string)it.Tag, exe, StringComparison.OrdinalIgnoreCase))
                return it.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text).ToArray();
        return null;
    }

    static string Copy(string exe, string rel)
    {
        string dst = Path.Combine(outDir, "games", rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dst));
        File.Copy(exe, dst, true);
        return dst;
    }

    static void Gui()
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        string root = Path.Combine(exeDir, "Packages");
        Ok(File.Exists(Path.Combine(root, @"OGL3D\x64\opengl32.dll")), "test root has the OGL3D package: " + root);
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        string gamesDir = Path.Combine(outDir, "games");
        if (Directory.Exists(gamesDir)) Directory.Delete(gamesDir, true);
        string ba = Copy(BrokenAge, @"BrokenAge\BrokenAge.exe");
        string cu = Copy(Curse, @"Curse\Curse of the Dead Gods.exe");
        string cuDir = Path.GetDirectoryName(cu);
        File.Copy(@"C:\Windows\System32\opengl32.dll", Path.Combine(cuDir, "opengl32.dll"));
        string sm = Copy(SineMora, @"SineMora\SineMoraEX.exe");
        string rf = Copy(Rfg, @"Rfg\rfg.exe");
        string sr = Copy(SteelRats, @"SteelRats\SteelRats\Binaries\Win64\SteelRats-Win64-Shipping.exe");
        Directory.CreateDirectory(Path.Combine(outDir, @"games\SteelRats\SteelRats\Content"));

        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(40, 40);
        form.Show();
        Pump();
        var tabs = C<TabControl>(form, "tabs");
        Func<string, RadioButton> rb = n => C<RadioButton>(form, n);
        Func<string, CheckBox> cb = n => C<CheckBox>(form, n);
        Func<string, Control> ctl = n => C<Control>(form, n);
        var numSep = C<NumericUpDown>(form, "numSep");
        var numConv = C<NumericUpDown>(form, "numConv");
        var cbGlMode = C<ComboBox>(form, "cbGlMode");
        string baDir = Path.GetDirectoryName(ba), baIni = Path.Combine(baDir, "OGL3D.ini");

        // ---- 1. Broken Age (x86 OpenGL; DirectXOf says DX9): detected as OpenGL, package defaults shown
        Call(form, "StartWizard", ba);
        Pump();
        Ok(rb("rbKindGl").Checked && !rb("rbKindDx9").Checked && !rb("rbKindGeo").Checked && !rb("rbKindUe4").Checked, "Broken Age: kind OpenGL (not DX9)");
        Ok(rb("rb32").Checked && !rb("rb32").Enabled && !rb("rb64").Enabled, "Broken Age: 32-bit from the exe, bits locked");
        Ok(!Shown(ctl("pnlScope")) && Shown(ctl("lblGlInstall")), "install step: scope hidden, OGL3D note shown");
        Ok(!Shown(ctl("lblEngine")) && !Shown(ctl("rowEngine")) && !Shown(ctl("rowUnityVer")), "install step: engine / Unity rows hidden");
        Ok(Shown(ctl("pnlGlMode")) && !Shown(ctl("pnlGeoMode")) && !Shown(cb("chkLens")), "display step: OGL3D output shown, direct_mode and lens hidden");
        Eq(ctl("lblSrHead").Text, "SR 위빙 (OGL3D)", "display step heading");
        Ok(C<System.Collections.IEnumerable>(form, "geoUpscaleControls").Cast<Control>().All(c => !Shown(c)) && !Shown(ctl("gridUpAdv")) && Shown(ctl("lblUpNoteGl")), "display step: geo-11 upscale extras hidden, OGL3D upscale note shown");
        Ok(Shown(ctl("lblStereoGl")) && C<System.Collections.IEnumerable>(form, "geoStereoNotes").Cast<Control>().All(c => !Shown(c)), "stereo step: OGL3D note shown, geo-11 notes hidden");
        Eq(ctl("lblStereoHead").Text, "기본 입체 값 (OGL3D.ini [Stereo])", "stereo step heading");
        Ok(cb("chkSr").Checked && !cbGlMode.Enabled && cb("chkSwap").Enabled, "package defaults: SR on (output fixed), swap enabled");
        Ok(cb("chkAutoConv").Checked && !numConv.Enabled && numSep.Value == 50m && !cb("chkUpscale").Checked, "package defaults: auto convergence (value locked), separation 50, upscale off");
        Ok(ctl("lblDetect").Text.Contains("OpenGL") && ctl("lblKindDetect").Text.Contains("OGL3D"), "detect notes: " + ctl("lblDetect").Text + " / " + ctl("lblKindDetect").Text);
        Ok(!ctl("lblDetect").Text.Contains("Unity 아님") && !ctl("lblDetect").Text.Contains("2GB"), "detect notes: no geo-11 / 4GB-patch hints for an OpenGL game");
        for (int i = 2; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "ko_gl_step" + (i - 1)); }
        Ok(TakeDialogs().Count == 0, "no message box so far");

        // ---- 2. finish with SR off / tab / swap / upscale / manual convergence
        cb("chkSr").Checked = false; Pump();
        Ok(cbGlMode.Enabled, "SR off: output selectable");
        cbGlMode.SelectedItem = "tab";
        cb("chkSwap").Checked = true; cb("chkUpscale").Checked = true;
        numSep.Value = 33.5m;
        cb("chkAutoConv").Checked = false; Pump();
        Ok(numConv.Enabled, "auto off: convergence editable");
        numConv.Value = 123.45m;
        tabs.SelectedIndex = 5;
        Call(form, "Apply");
        Pump();
        var dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("OGL3D를 설치했습니다"), "Apply: one info box (" + string.Join(" || ", dl) + ")");
        string baDll = Path.Combine(baDir, "opengl32.dll");
        Ok(File.Exists(baDll) && Machine(baDll) == 0x14c && (bool)S("GlOps", "IsOgl3d", baDll), "Apply: x86 OGL3D opengl32.dll in the game folder");
        Eq(Desc(S("GlOps", "Read", baIni)), "output=tab swap_eyes=1 upscale=1 separation=33.5 convergence=123.45", "Apply: OGL3D.ini values");
        Ok(!File.Exists(Path.Combine(baDir, "dxgi.dll")) && !File.Exists(Path.Combine(baDir, "d3d11.dll")) && !File.Exists(Path.Combine(baDir, "d3dxdm.ini")) && !File.Exists(Path.Combine(baDir, "SRWeave.ini")), "Apply: no geo-11 / SRWeave files");
        Eq(tabs.SelectedIndex, 0, "Apply returns to the game list");
        var row = Row(form, ba);
        Ok(row != null, "game listed");
        if (row != null)
        {
            Console.WriteLine("  row: " + string.Join(" | ", row));
            Eq(row[1], "설정 완료", "list: state"); Eq(row[3], "OpenGL", "list: engine"); Eq(row[4], "32", "list: bits");
            Eq(row[5], "끔", "list: SR"); Eq(row[6], "켬", "list: upscale"); Eq(row[7], "33.5", "list: Sep"); Eq(row[8], "123.45", "list: Conv");
        }
        Ok(File.ReadAllText(Path.Combine(exeDir, "games.list")).Contains(ba + "\t1"), "games.list records the game as done");
        Shot(form, "ko_list_after_apply");

        // ---- 3. edit again: values come back from the game's OGL3D.ini
        Call(form, "StartWizard", ba);
        Pump();
        Ok(!cb("chkSr").Checked && (string)cbGlMode.SelectedItem == "tab" && cb("chkSwap").Checked && cb("chkUpscale").Checked, "re-edit: SR off, tab, swap, upscale loaded");
        Ok(numSep.Value == 33.5m && !cb("chkAutoConv").Checked && numConv.Value == 123.45m, "re-edit: separation / manual convergence loaded");
        Ok(ctl("lblDetect").Text.Contains("OGL3D 설치됨 1.0.0"), "re-edit: detect notes show the installed version");
        cb("chkSr").Checked = true; cb("chkAutoConv").Checked = true; cb("chkUpscale").Checked = false; cb("chkSwap").Checked = false;
        Call(form, "Apply");
        Pump();
        TakeDialogs();
        Eq(Desc(S("GlOps", "Read", baIni)), "output=auto swap_eyes=0 upscale=0 separation=33.5 convergence=auto", "re-apply: SR on + auto");
        row = Row(form, ba);
        if (row != null) { Eq(row[5], "켬", "list: SR on"); Eq(row[8], "auto", "list: Conv auto"); Eq(row[6], "끔", "list: upscale off"); }

        // ---- 4. list actions on the OpenGL game
        var lv = C<ListView>(form, "lvGames");
        foreach (ListViewItem it in lv.Items) it.Selected = string.Equals((string)it.Tag, ba, StringComparison.OrdinalIgnoreCase);
        Pump();
        Eq(C<Button>(form, "bLogGame").Text, "OGL3D.log", "log button names OGL3D.log for the OpenGL game");
        // community report: the hub's form on main, with the method used and the PCGamingWiki link filled in
        string rurl = (string)S("Report", "Url", ba, Path.Combine(outDir, "nodx9"), "https://www.pcgamingwiki.com/api/appid.php?appid=1");
        Ok(rurl.StartsWith("https://github.com/nautymac/GeoGamingHub/issues/new?template=game_report.yml") && rurl.Contains("&used=OGL3D&") && rurl.Contains("&pcgw=https%3A%2F%2Fwww.pcgamingwiki.com"), "report form URL: " + rurl.Substring(0, Math.Min(140, rurl.Length)));
        Ok(((string)Ty("Report").GetProperty("ListUrl").GetValue(null)).EndsWith("/blob/main/COMMUNITY.ko.md"), "community list link on main");
        var parsed = (System.Collections.ICollection)S("Report", "Parse", "{\"version\":1,\"games\":[{\"game\":\"Quake II RTX\",\"key\":\"quakeiirtx\",\"method\":\"vulkan\",\"status\":\"works\",\"works\":1,\"issues\":0,\"broken\":0,\"steam\":[\"1089130\"]},{\"game\":\"Torchlight\",\"key\":\"torchlight\",\"method\":\"dx9\",\"status\":\"untested\",\"works\":0,\"issues\":0,\"broken\":0,\"steam\":[\"41500\"]}]}");
        Eq(parsed.Count, 1, "community.json: tested games read, suggestions left out");
        var accent = (System.Drawing.Color)Ty("Theme").GetField("Accent").GetValue(null);
        Ok(C<Button>(form, "btnLaunch").BackColor == accent, "set-up game selected: [실행] in the accent colour");
        foreach (ListViewItem it in lv.Items) it.Selected = false;
        Pump();
        Ok(C<Button>(form, "btnLaunch").BackColor != accent, "nothing selected: [실행] plain");
        foreach (ListViewItem it in lv.Items) it.Selected = string.Equals((string)it.Tag, ba, StringComparison.OrdinalIgnoreCase);
        Pump();
        Call(form, "ApplyHotkeysOnly");
        Ok(!File.Exists(Path.Combine(baDir, "d3dxdm.ini")) && !File.Exists(Path.Combine(baDir, "d3dx.ini")) && !Directory.Exists(Path.Combine(baDir, "ShaderFixesDM")) && !File.Exists(Path.Combine(baDir, "SRWeave.ini")), "hotkeys-only writes nothing into an OpenGL game");
        Call(form, "RemoveSr");
        Pump();
        Eq(Get(baIni, "Stereo", "output"), "sbs", "remove SR weaving: output = sbs");
        Ok(File.Exists(baDll), "remove SR weaving keeps OGL3D");
        row = Row(form, ba);
        if (row != null) Eq(row[5], "끔", "list: SR off after removing weaving");
        Ok(TakeDialogs().Count == 0, "no message boxes for hotkeys-only / remove SR");

        // ---- 5. Curse (x64) over a foreign opengl32.dll, then uninstall restores it
        Call(form, "StartWizard", cu);
        Pump();
        Ok(rb("rbKindGl").Checked && rb("rb64").Checked, "Curse: OpenGL, 64-bit");
        Ok(ctl("lblDetect").Text.Contains("다른 opengl32.dll"), "Curse: foreign opengl32.dll noted (" + ctl("lblDetect").Text + ")");
        Call(form, "Apply");
        Pump();
        TakeDialogs();
        string cuDll = Path.Combine(cuDir, "opengl32.dll");
        Ok(Machine(cuDll) == 0x8664 && (bool)S("GlOps", "IsOgl3d", cuDll), "Curse: x64 OGL3D installed");
        Ok(File.Exists(cuDll + ".geo11sr_bak") && SameBytes(cuDll + ".geo11sr_bak", @"C:\Windows\System32\opengl32.dll"), "Curse: foreign DLL kept as .geo11sr_bak");
        File.WriteAllText(Path.Combine(cuDir, "OGL3D.log"), "x");
        Call(form, "UninstallAll");
        Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("OGL3D(OpenGL)를 제거합니다"), "uninstall asks once (" + string.Join(" || ", dl) + ")");
        Ok(SameBytes(cuDll, @"C:\Windows\System32\opengl32.dll") && !File.Exists(cuDll + ".geo11sr_bak"), "uninstall restores the foreign DLL");
        Ok(!File.Exists(Path.Combine(cuDir, "OGL3D.ini")) && !File.Exists(Path.Combine(cuDir, "OGL3D.log")), "uninstall deletes OGL3D.ini / OGL3D.log");
        row = Row(form, cu);
        if (row != null) Eq(row[1], "재설정 필요", "list: Curse back to 'needs setup'");

        // ---- 6. other kinds unchanged
        Call(form, "StartWizard", sm);
        Pump();
        Ok(rb("rbKindDx9").Checked && !rb("rbKindGl").Checked, "Sine Mora EX: DX9 kind");
        Ok(Shown(ctl("pnlScope")) && !Shown(ctl("lblGlInstall")) && Shown(ctl("pnlGeoMode")) && !Shown(ctl("pnlGlMode")) && Shown(cb("chkLens")), "DX9: geo-11 controls shown, OGL3D ones hidden");
        Ok(rb("rb32").Enabled && rb("rb64").Enabled, "DX9: bits selectable");
        Eq(ctl("lblSrHead").Text, "SR 위빙 (SRWeave dxgi.dll)", "DX9: SR heading");
        Call(form, "StartWizard", rf);
        Pump();
        Ok(rb("rbKindGeo").Checked && !rb("rbKindGl").Checked, "Red Faction: geo-11 kind");
        Ok(C<System.Collections.IEnumerable>(form, "geoUpscaleControls").Cast<Control>().All(Shown) && !Shown(ctl("lblUpNoteGl")) && !Shown(ctl("lblStereoGl")), "geo-11: upscale extras / notes shown");
        cb("chkAutoConv").Checked = true; Pump();
        Ok(numConv.Enabled, "geo-11: convergence stays editable with auto convergence");
        Eq(ctl("lblStereoHead").Text, "기본 입체 값 (d3dxdm.ini [Stereo])", "geo-11: stereo heading");
        rb("rbKindGl").Checked = true; Pump();
        Ok(Shown(ctl("pnlGlMode")) && !Shown(ctl("pnlScope")) && !rb("rb32").Enabled && rb(Machine(rf) == 0x8664 ? "rb64" : "rb32").Checked, "manual switch to OpenGL: OGL3D controls, bits locked to the exe's (rfg.exe is 32-bit)  [glMode=" + Shown(ctl("pnlGlMode")) + " scope=" + Shown(ctl("pnlScope")) + " rb32.En=" + rb("rb32").Enabled + " rb64=" + rb("rb64").Checked + " gl=" + rb("rbKindGl").Checked + " geo=" + rb("rbKindGeo").Checked + "]");
        rb("rbKindGeo").Checked = true; Pump();
        Ok(!Shown(ctl("pnlGlMode")) && Shown(ctl("pnlScope")) && rb("rb32").Enabled, "switch back to geo-11: geo-11 controls");
        // install scope: full (SR on) / SBS only (SR off) follow [SR 위빙 사용]; 'SR 위빙만 추가' only over an existing fix
        Ok(rb("rbFull").Checked && cb("chkSr").Checked && Shown(rb("rbSbsOnly")) && !Shown(rb("rbSrOnly")), "scope: full install, SBS only offered, SR-only hidden (no d3d11.dll)");
        cb("chkSr").Checked = false; Pump();
        Ok(rb("rbSbsOnly").Checked && !cb("chkSr").Checked && C<ComboBox>(form, "cbMode").Enabled, "SR off -> SBS only, output mode selectable");
        rb("rbFull").Checked = true; Pump();
        Ok(cb("chkSr").Checked && !rb("rbSbsOnly").Checked, "full install -> SR on again");
        rb("rbSbsOnly").Checked = true; Pump();
        Ok(!cb("chkSr").Checked, "SBS only -> SR off");
        string rfFix = Path.Combine(Path.GetDirectoryName(rf), "d3d11.dll");
        File.WriteAllBytes(rfFix, new byte[] { 0 });
        Call(form, "StartWizard", rf);
        Pump();
        Ok(Shown(rb("rbSrOnly")) && rb("rbSrOnly").Checked, "existing d3d11.dll: 'SR 위빙만 추가' shown and picked");
        File.Delete(rfFix);
        Call(form, "StartWizard", rf);
        Pump();
        Ok(!Shown(rb("rbSrOnly")) && rb("rbFull").Checked && cb("chkSr").Checked, "no fix again: SR-only hidden, full install with SR");
        // look: category cards drive the hidden category radios
        var cards = rb("rbCatGeo").Parent.Controls.Cast<Control>().SelectMany(c => c.Controls.Cast<Control>()).Where(c => c.GetType().Name == "CatCard").ToList();
        Ok(cards.Count == 3 && !Shown(rb("rbCatGeo")) && !Shown(rb("rbCatWrap")) && !Shown(rb("rbCatTri")), "3D method: three category cards, radios hidden");
        Call(cards[1], "OnClick", EventArgs.Empty); Pump();
        Ok(rb("rbCatWrap").Checked && Shown(ctl("pnlKindWrap")), "card click picks the own-wrapper category");
        Call(cards[0], "OnClick", EventArgs.Empty); Pump();
        Ok(rb("rbCatGeo").Checked && Shown(ctl("pnlKindGeo")), "card click picks geo-11 again");
        Call(form, "StartWizard", sr);
        Pump();
        Ok(rb("rbKindUe4").Checked && !rb("rbKindGl").Checked && rb("rb64").Checked && !rb("rb32").Enabled, "Steel Rats: UE4 kind, 64-bit only");
        Ok(TakeDialogs().Count == 0, "no message boxes for detection");

        // ---- 7. English UI rebuilt in place (language switch)
        Ty("Lang").GetField("English").SetValue(null, true);
        Call(form, "RebuildUi");
        Pump();
        Eq(C<System.Collections.ICollection>(form, "geoUpscaleControls").Count, 4, "rebuild: upscale control list not duplicated");
        Eq(C<System.Collections.ICollection>(form, "geoStereoNotes").Count, 2, "rebuild: stereo note list not duplicated");
        tabs = C<TabControl>(form, "tabs");   // new controls after the rebuild
        cbGlMode = C<ComboBox>(form, "cbGlMode");
        Call(form, "StartWizard", ba);
        Pump();
        Eq(ctl("lblSrHead").Text, "SR weaving (OGL3D)", "English: SR heading");
        Eq(cb("chkAutoConv").Text, "Auto-convergence (convergence = auto) - OGL3D follows the scene (recommended)", "English: auto convergence label");
        Ok(!cb("chkSr").Checked && (string)cbGlMode.SelectedItem == "sbs" && cbGlMode.Enabled, "English re-edit: output = sbs (after removing SR weaving) shows as SR off / sbs");
        for (int i = 2; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "en_gl_step" + (i - 1)); }
        tabs.SelectedIndex = 0; Shot(form, "en_list");
        tabs.SelectedIndex = 7; Shot(form, "en_help");
        Ok(TakeDialogs().Count == 0, "no message boxes in English pass");
        form.Close();
        Pump();
    }

    // ================================================================ pkg
    static void Pkg()
    {
        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        var pk = C<Dictionary<string, string>>(form, "pkgDirs");
        foreach (var kv in pk) Ok(Directory.Exists(kv.Value), "package " + kv.Key + ": " + kv.Value);
        Ok(File.Exists(Path.Combine(pk["ogl3d"], @"x64\opengl32.dll")) && File.Exists(Path.Combine(pk["ogl3d"], @"x86\opengl32.dll")) && File.Exists(Path.Combine(pk["ogl3d"], "OGL3D.ini")), "OGL3D package complete");
        form.Dispose();
    }
}
