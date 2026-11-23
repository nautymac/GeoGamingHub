// Self-tests of the TriDef launch method (games started by their own launcher: GTA5.exe via PlayGTAV.exe); part of
// HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> trilaunch
// Needs TriDef 3D installed on this PC (its profile list), nothing else: the "game" is a copy of ping.exe named
// GTA5.exe in <out dir>\games\GTA V, next to a PlayGTAV.exe. Registrations go to HKCU\SOFTWARE\GeoGamingHubTest\...
// instead of TriDef's own key, and Tridef3D_Play is never started.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static partial class HubTest
{
    // the step's page scrolls: bring c into view for the screenshot
    static void ScrollTo(Control c)
    {
        Pump();
        for (var p = c.Parent; p != null; p = p.Parent)
        {
            var sc = p as ScrollableControl;
            if (sc != null && sc.AutoScroll) { sc.ScrollControlIntoView(c); break; }
        }
    }

    static void TriLaunch()
    {
        const string key = @"SOFTWARE\GeoGamingHubTest\TriDefIgnition\Games";
        Ty("TriOps").GetField("GamesKey").SetValue(null, key);
        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\GeoGamingHubTest", false);
        Func<string, object[], object> T = (m, a) => S("TriOps", m, a);
        string ping = Path.Combine(Environment.SystemDirectory, "PING.EXE");
        string gta = Copy(ping, @"GTA V\GTA5.exe");
        string play = Copy(ping, @"GTA V\PlayGTAV.exe");
        Copy(ping, @"GTA V\GTAVLauncher.exe");         // play*.exe wins over *launcher*.exe
        Copy(ping, @"GTA V\x64\crashpad_launcher.exe"); // not in the game folder itself, and a crash helper anyway
        string loose = Copy(ping, @"Loose\Game.exe");

        // ---- launcher suggestion
        Eq(T("SuggestLauncher", new object[] { gta, null }), play, "SuggestLauncher: PlayGTAV.exe next to GTA5.exe");
        Eq(T("SuggestLauncher", new object[] { loose, "271590" }), "steam://rungameid/271590", "SuggestLauncher: Steam address when no launcher exe");
        Eq(T("SuggestLauncher", new object[] { loose, null }), "", "SuggestLauncher: nothing");
        string csm = Copy(ping, @"GP4\CSM.exe"); string gp4 = Copy(ping, @"GP4\GP4.exe");
        Eq(T("SuggestLauncher", new object[] { gp4, null }), csm, "SuggestLauncher: CSM.exe next to GP4.exe");

        // ---- registry values (test key)
        string profile = ((System.Collections.IList)T("ProfileNames", new object[0])).Cast<string>().FirstOrDefault();
        Ok(profile != null, "a TriDef profile to register with: " + profile);
        T("Register", new object[] { "GTA V test", gta, profile, false });
        Eq(T("Mode", new object[] { "GTA V test" }), "auto", "Mode: automatic when nothing is set");
        T("SetLaunch", new object[] { "GTA V test", "launcher", "\"" + play + "\"", " -silent " });
        Eq(T("Mode", new object[] { "GTA V test" }), "launcher", "SetLaunch launcher: mode");
        Eq(T("LauncherOf", new object[] { "GTA V test" }), play, "SetLaunch launcher: path (quotes trimmed)");
        Eq(T("LauncherArgsOf", new object[] { "GTA V test" }), "-silent", "SetLaunch launcher: arguments");
        using (var k = Registry.CurrentUser.OpenSubKey(key + @"\GTA V test"))
        {
            Eq(k.GetValue("Play3DMode"), "launcher", "value Play3DMode (what play3d.py reads)");
            Eq(k.GetValue("Play3DLauncher"), play, "value Play3DLauncher");
            Eq(k.GetValue("Path"), gta, "Path stays the game's own exe");
        }
        T("SetLaunch", new object[] { "GTA V test", "wait", play, "x" });
        Eq(T("Mode", new object[] { "GTA V test" }), "wait", "SetLaunch wait: mode");
        Eq(T("LauncherOf", new object[] { "GTA V test" }), "", "SetLaunch wait: no launcher kept");
        using (var k = Registry.CurrentUser.OpenSubKey(key + @"\GTA V test", true)) k.SetValue("Play3DMode", "launcher");
        Eq(T("Mode", new object[] { "GTA V test" }), "auto", "Mode: 'launcher' without a launcher counts as automatic");
        T("SetLaunch", new object[] { "GTA V test", "auto", "", "" });
        using (var k = Registry.CurrentUser.OpenSubKey(key + @"\GTA V test"))
            Ok(k.GetValueNames().All(n => !n.StartsWith("Play3D")), "SetLaunch auto: our values removed");
        Eq(T("LaunchArgs", new object[] { "GTA V test", true, true }), "\"GTA V test\" --sr --wait --console", "LaunchArgs with --wait");
        Eq(T("LaunchArgs", new object[] { "GTA V test", false }), "\"GTA V test\" --console", "LaunchArgs unchanged for automatic");

        // ---- starting the game's launcher once Tridef3D_Play is watching
        string pkg = Path.Combine(outDir, "pkg"); Directory.CreateDirectory(pkg);
        string log = (string)T("LauncherLog", new object[] { pkg });
        var launched = Ty("TriOps").GetField("launchedUtc", BindingFlags.Static | BindingFlags.NonPublic);
        T("SetLaunch", new object[] { "GTA V test", "launcher", play, "-n 1 127.0.0.1" });
        Func<Process, string> run = p =>
        {
            string got = null; var done = new ManualResetEvent(false);
            T("StartLauncherWhenReady", new object[] { pkg, p, "GTA V test", (Action<string>)(m => { got = m; done.Set(); }), 8000 });
            done.WaitOne(15000);
            return got;
        };
        // an old log (from before this launch) with the line does not count
        File.WriteAllText(log, "WAITING for GTA5.exe\n", Encoding.UTF8);
        File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddMinutes(-5));
        launched.SetValue(null, DateTime.UtcNow);
        var gone = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false, CreateNoWindow = true });
        gone.WaitForExit();
        string msg = run(gone);
        Ok(msg != null && msg.Contains("기다리기 전에 끝났습니다"), "launcher not started when Tridef3D_Play ended first: " + msg);
        // this run's log says it is watching -> the launcher is started
        launched.SetValue(null, DateTime.UtcNow.AddSeconds(-1));
        File.WriteAllText(log, "baseline GTA5.exe pids (ignored): set()\nWAITING for GTA5.exe for up to 600s - start the game from its launcher or shortcut now.\n", Encoding.UTF8);
        int before = Process.GetProcessesByName("PlayGTAV").Length;
        msg = run(null);
        Ok(msg != null && msg.Contains("게임 런처 실행") && msg.Contains("PlayGTAV.exe"), "launcher started once watching: " + msg);
        Thread.Sleep(300);
        Ok(Process.GetProcessesByName("PlayGTAV").Length > before || msg.Contains("게임 런처 실행"), "PlayGTAV.exe process started");

        // ---- the wizard
        T("SetLaunch", new object[] { "GTA V test", "auto", "", "" });
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        foreach (var f in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, f));
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(40, 40);
        form.Show();
        Pump();
        var tabs = C<TabControl>(form, "tabs");
        Call(form, "StartWizard", gta);
        Pump();
        Ok(C<RadioButton>(form, "rbKindTri").Checked, "registered GTA5.exe: TriDef 3D picked");
        var cbLaunch = C<ComboBox>(form, "cbTriLaunch");
        var tbLauncher = C<TextBox>(form, "tbTriLauncher");
        Eq(cbLaunch.SelectedIndex, 0, "launch method: automatic as registered");
        Eq(tbLauncher.Text, play, "launcher pre-filled with PlayGTAV.exe");
        Ok(!tbLauncher.Enabled, "launcher field off for automatic");
        cbLaunch.SelectedIndex = 1; Pump();
        Ok(tbLauncher.Enabled, "launcher field on for 'via launcher'");
        tabs.SelectedIndex = 2; ScrollTo(C<TextBox>(form, "tbTriLauncherArgs")); Shot(form, "ko_trilaunch_step1");
        Ok(TakeDialogs().Count == 0, "no message box so far");

        // 'via launcher' without a launcher: refused
        tbLauncher.Text = "";
        Call(form, "Apply"); Pump();
        var dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("런처를 지정해야"), "empty launcher refused (" + string.Join(" || ", dl) + ")");
        Eq(T("Mode", new object[] { "GTA V test" }), "auto", "refused: nothing saved");

        tbLauncher.Text = play;
        C<TextBox>(form, "tbTriLauncherArgs").Text = "-x";
        Call(form, "Apply"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("런처로 실행"), "Apply via launcher: one info box (" + string.Join(" || ", dl) + ")");
        Eq(T("Mode", new object[] { "GTA V test" }), "launcher", "Apply: mode saved");
        Eq(T("LauncherOf", new object[] { "GTA V test" }), play, "Apply: launcher saved");
        Eq(T("LauncherArgsOf", new object[] { "GTA V test" }), "-x", "Apply: arguments saved");
        var row = Row(form, gta);
        if (row != null) { Console.WriteLine("  row: " + string.Join(" | ", row)); Eq(row[9], "TriDef 실행기 (런처)", "list: launch column"); }
        else Ok(false, "GTA5.exe listed");

        Call(form, "StartWizard", gta); Pump();
        Eq(C<ComboBox>(form, "cbTriLaunch").SelectedIndex, 1, "wizard again: 'via launcher' loaded from the registration");
        C<ComboBox>(form, "cbTriLaunch").SelectedIndex = 2; Pump();
        Call(form, "Apply"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("기다리기만"), "Apply wait only: info box (" + string.Join(" || ", dl) + ")");
        Eq(T("Mode", new object[] { "GTA V test" }), "wait", "Apply: wait saved");
        row = Row(form, gta);
        if (row != null) Eq(row[9], "TriDef 실행기 (기다리기)", "list: launch column (wait)");

        Call(form, "StartWizard", gta); Pump();
        C<ComboBox>(form, "cbTriLaunch").SelectedIndex = 0; Pump();
        Call(form, "Apply"); Pump();
        TakeDialogs();
        Eq(T("Mode", new object[] { "GTA V test" }), "auto", "Apply automatic: back to automatic");
        row = Row(form, gta);
        if (row != null) Eq(row[9], "TriDef 실행기", "list: launch column (automatic)");

        Ty("Lang").GetField("English").SetValue(null, true);
        Call(form, "RebuildUi"); Pump();
        Call(form, "StartWizard", gta); Pump();
        tabs = C<TabControl>(form, "tabs");
        Eq(C<ComboBox>(form, "cbTriLaunch").Items[1], "Via launcher - start the launcher, inject when the game starts", "English: launch method");
        tabs.SelectedIndex = 2; ScrollTo(C<TextBox>(form, "tbTriLauncherArgs")); Shot(form, "en_trilaunch_step1");
        Ok(TakeDialogs().Count == 0, "no message boxes in English pass");
        form.Close();
        Pump();
        foreach (var f in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, f));
        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\GeoGamingHubTest", false);
    }
}
