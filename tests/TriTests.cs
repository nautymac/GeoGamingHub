// Self-tests of the TriDef 3D category (TriDef.cs); part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> tri
// Needs TriDef 3D installed on this PC (profiles in C:\ProgramData\DDD\TriDefGames) and Torchlight (Steam, 32-bit DX9,
// TriDef profile "Torchlight (DDD)"). Registrations go to HKCU\SOFTWARE\GeoGamingHubTest\... instead of TriDef's own
// key, and nothing is started (the launcher would run the game). Torchlight's folder is only read.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static partial class HubTest
{
    static readonly string Torchlight2 = Common + @"Torchlight\Torchlight.exe";
    const string TriTestKey = @"SOFTWARE\GeoGamingHubTest\TriDefIgnition\Games";

    static void Tri()
    {
        var tri = Ty("TriOps");
        tri.GetField("GamesKey").SetValue(null, TriTestKey);
        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\GeoGamingHubTest", false);
        Func<string, object[], object> T = (m, a) => S("TriOps", m, a);

        // ---- names and profiles
        Eq(T("Norm", new object[] { "Batman\u2122: Arkham City GOTY Edition" }), "batman arkham city", "Norm: trademark, punctuation, edition suffix");
        Eq(T("Norm", new object[] { "The Cave" }), "cave", "Norm: leading 'the'");
        Eq(T("Title", new object[] { "Torchlight (DDD)" }), "Torchlight", "Title: author tag removed");
        Ok((bool)T("Installed", new object[0]), "TriDef 3D installed on this PC: " + T("DriverDir", new object[0]));
        int n = ((System.Collections.IList)T("ProfileNames", new object[0])).Count;
        Ok(n > 1000, "profile names: " + n);
        Eq(T("Match", new object[] { Torchlight2 }), "Torchlight (DDD)", "Match: Torchlight (Steam name)");
        Eq(T("Match", new object[] { Q2rtx }), null, "Match: Quake II RTX has no TriDef profile");
        string copy = Copy(Torchlight2, @"tri\Torchlight\Torchlight.exe");
        Eq(T("Match", new object[] { copy }), "Torchlight (DDD)", "Match: a copy outside Steam (folder / exe name)");

        // ---- registration (test key)
        Eq(T("RegisteredName", new object[] { Torchlight2 }), null, "test key starts empty");
        var log = (System.Collections.Generic.List<string>)T("Register", new object[] { "Torchlight", Torchlight2, "Torchlight (DDD)", true });
        Console.WriteLine("  log: " + string.Join(" / ", log));
        Eq(T("RegisteredName", new object[] { Torchlight2 }), "Torchlight", "Register: found by exe path");
        Eq(T("Profile", new object[] { "Torchlight" }), "Torchlight (DDD)", "Register: profile");
        Ok((bool)T("Sr", new object[] { "Torchlight" }) && (bool)T("Ours", new object[] { "Torchlight" }), "Register: SR on, made by this app");
        using (var k = Registry.CurrentUser.OpenSubKey(TriTestKey + @"\Torchlight"))
        {
            Eq(k.GetValue("Path"), Torchlight2, "Ignition value Path");
            Eq(k.GetValue("IconPath"), Torchlight2, "Ignition value IconPath");
            Eq(k.GetValue("Profile"), @"C:\ProgramData\DDD\TriDefGames\Torchlight (DDD).TriDefGame", "Ignition value Profile");
            Eq(k.GetValue("__dddvartype"), 6, "Ignition value __dddvartype (DWORD 6)");
            Eq(k.GetValue("Argument"), "", "Ignition value Argument (empty: the launcher finds the Steam app from the exe folder)");
        }
        T("SetSr", new object[] { "Torchlight", false });
        Ok(!(bool)T("Sr", new object[] { "Torchlight" }), "SetSr off");
        T("Unregister", new object[] { "Torchlight" });
        Eq(T("RegisteredName", new object[] { Torchlight2 }), null, "Unregister: our key deleted");
        // a game added in Ignition itself keeps its key
        using (var k = Registry.CurrentUser.CreateSubKey(TriTestKey + @"\Ignition Game")) { k.SetValue("Path", copy); k.SetValue("Profile", "x"); }
        T("Register", new object[] { "Ignition Game", copy, "Torchlight (DDD)", true });
        Ok(!(bool)T("Ours", new object[] { "Ignition Game" }), "Register over Ignition's key: not marked as ours");
        T("Unregister", new object[] { "Ignition Game" });
        Eq(T("RegisteredName", new object[] { copy }), "Ignition Game", "Unregister: Ignition's own key stays");
        Registry.CurrentUser.DeleteSubKeyTree(TriTestKey + @"\Ignition Game", false);

        // ---- the app
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        foreach (var f in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, f));
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        string tlDir = Path.GetDirectoryName(Torchlight2);
        string before = string.Join("|", Directory.GetFileSystemEntries(tlDir).OrderBy(x => x));
        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(40, 40);
        form.Show();
        Pump();
        var tabs = C<TabControl>(form, "tabs");
        Func<string, RadioButton> rb = x => C<RadioButton>(form, x);
        Func<string, CheckBox> cb = x => C<CheckBox>(form, x);
        Func<string, Control> ctl = x => C<Control>(form, x);
        Eq(rb("rbCatTri").Text, "TriDef 3D - TriDef 프로필이 있는 게임 (TriDef 3D 설치 필수)", "third category");

        Call(form, "StartWizard", Torchlight2);
        Pump();
        Ok(rb("rbKindTri").Checked && rb("rbCatTri").Checked && !rb("rbKindGeo").Checked && !rb("rbKindDx9").Checked, "Torchlight: TriDef 3D picked automatically (profile)");
        Ok(Shown(ctl("pnlKindTri")) && !Shown(ctl("pnlKindGeo")) && !Shown(ctl("pnlKindWrap")), "only the TriDef panel shown");
        Eq(C<ComboBox>(form, "cbTriProfile").SelectedItem, "Torchlight (DDD)", "profile preselected");
        Ok(ctl("lblTriState").Text.StartsWith("TriDef 3D 설치됨"), "state: installed (" + ctl("lblTriState").Text + ")");
        Ok(ctl("lblKindDetect").Text.Contains("TriDef") && ctl("lblDetect").Text.Contains("TriDef 프로필: Torchlight (DDD)"), "detect notes: " + ctl("lblDetect").Text);
        Ok(rb("rb32").Checked && !rb("rb32").Enabled, "32-bit from the exe, locked");
        Ok(Shown(ctl("lblTriInstall")) && !Shown(ctl("pnlScope")), "install step: TriDef note, no install scope");
        Ok(!Shown(ctl("pnlGeoMode")) && !Shown(ctl("pnlGlMode")) && !Shown(cb("chkLens")) && !Shown(cb("chkSwap")) && !Shown(cb("chkUpscale")) && Shown(cb("chkSr")), "display step: only SR weaving");
        Eq(ctl("lblSrHead").Text, "SR 위빙 (TriDef + SRWeave)", "display heading");
        Ok(!Shown(ctl("gridStereo")) && Shown(ctl("lblStereoTri")) && !Shown(cb("chkAutoConv")), "stereo step: TriDef note only");
        for (int i = 2; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "ko_tri_step" + (i - 1)); }
        Ok(TakeDialogs().Count == 0, "no message box so far");

        // category switches keep the radios exclusive
        rb("rbCatGeo").Checked = true; Pump();
        Ok(!rb("rbKindTri").Checked && (rb("rbKindGeo").Checked || rb("rbKindDx9").Checked || rb("rbKindUe4").Checked) && Shown(ctl("pnlScope")), "category geo-11: a geo-11 type, geo-11 steps");
        rb("rbCatTri").Checked = true; Pump();
        Ok(rb("rbKindTri").Checked && !rb("rbKindGeo").Checked && !rb("rbKindDx9").Checked, "back to TriDef 3D");

        // apply: registration only
        Call(form, "Apply");
        Pump();
        var dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("TriDef 3D에 등록했습니다"), "Apply: one info box (" + string.Join(" || ", dl) + ")");
        Eq(T("RegisteredName", new object[] { Torchlight2 }), "Torchlight", "Apply: registered (test key)");
        Eq(string.Join("|", Directory.GetFileSystemEntries(tlDir).OrderBy(x => x)), before, "Apply: nothing written to the game folder");
        var row = Row(form, Torchlight2);
        if (row != null) { Console.WriteLine("  row: " + string.Join(" | ", row)); Eq(row[1], "설정 완료", "list: state"); Eq(row[3], "TriDef", "list: engine"); Eq(row[5], "켬", "list: SR"); Eq(row[9], "TriDef 실행기", "list: launch"); }
        else Ok(false, "Torchlight listed");
        var lv = C<ListView>(form, "lvGames");
        foreach (ListViewItem it in lv.Items) it.Selected = string.Equals((string)it.Tag, Torchlight2, StringComparison.OrdinalIgnoreCase);
        Pump();
        Call(form, "SelectGameForAction");
        Call(form, "RemoveSr"); Pump();
        Ok(!(bool)T("Sr", new object[] { "Torchlight" }), "remove SR weaving: SR flag off");
        row = Row(form, Torchlight2);
        if (row != null) Eq(row[5], "끔", "list: SR off");
        Call(form, "ApplyHotkeysOnly");
        Ok(!File.Exists(Path.Combine(tlDir, "d3dxdm.ini")) && !File.Exists(Path.Combine(tlDir, "SRWeave.ini")), "hotkeys-only writes nothing");
        Call(form, "UninstallAll"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("TriDef 3D 등록을 지웁니다"), "uninstall asks once");
        Eq(T("RegisteredName", new object[] { Torchlight2 }), null, "uninstall: registration removed");
        Eq(string.Join("|", Directory.GetFileSystemEntries(tlDir).OrderBy(x => x)), before, "game folder unchanged throughout");

        // a game without a profile is not TriDef
        Call(form, "StartWizard", Q2rtx);
        Pump();
        Ok(!rb("rbKindTri").Checked && rb("rbKindVk").Checked, "Quake II RTX: not TriDef (no profile), Vulkan");

        Ty("Lang").GetField("English").SetValue(null, true);
        Call(form, "RebuildUi");
        Pump();
        tabs = C<TabControl>(form, "tabs");
        Call(form, "StartWizard", Torchlight2);
        Pump();
        Eq(rb("rbCatTri").Text, "TriDef 3D - games with a TriDef profile (TriDef 3D must be installed)", "English: category");
        tabs.SelectedIndex = 2; Shot(form, "en_tri_step1");
        Ok(TakeDialogs().Count == 0, "no message boxes in English pass");
        form.Close();
        Pump();
        foreach (var f in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, f));
        Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\GeoGamingHubTest", false);
    }
}
