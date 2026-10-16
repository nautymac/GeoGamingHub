// Self-tests of Geo Gaming Hub's DirectX 12 (DX12_3D) kind; part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> dx12unit
//   HubTest.exe <GeoGamingHub.exe> <out dir> dx12gui
// dx12unit: Dx12Ops on temp folders with a copy of a game exe (Helldivers 2 = 64-bit DX12, loads d3d12 dynamically),
//           detection on DX11 / DX9 / Vulkan / 32-bit exes (read-only)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static partial class HubTest
{
    static readonly string Hd2 = Common + @"Helldivers 2\bin\helldivers2.exe";
    static readonly string Dx12App = @"D:\ClaudeCode\Projects\DX12-3D-Wrapper\tests\app\bin\dx12test.exe";

    static object DOpt(bool sr, string mode, bool swap, decimal sep, decimal conv)
    {
        var t = Ty("Dx12Ops+Options");
        var o = Activator.CreateInstance(t);
        t.GetField("Sr").SetValue(o, sr); t.GetField("Mode").SetValue(o, mode); t.GetField("Swap").SetValue(o, swap);
        t.GetField("Separation").SetValue(o, sep); t.GetField("Convergence").SetValue(o, conv);
        return o;
    }

    static string DDesc(object o) { return (string)S("Dx12Ops", "Describe", o); }

    static void Dx12Unit(string app)
    {
        string pk = Path.Combine(Path.GetDirectoryName(app), "Packages", "DX12_3D");
        string dll = Path.Combine(pk, @"x64\d3d12.dll");
        Ok((bool)S("Dx12Ops", "IsDx12_3d", dll), "package: DX12_3D d3d12.dll");
        Eq(Machine(dll), 0x8664, "package: DX12_3D DLL is AMD64");
        Ok(File.Exists(Path.Combine(pk, @"DX12_3D\dxcompiler.dll")) && File.Exists(Path.Combine(pk, @"DX12_3D\dxil.dll")), "package: DXC runtime");
        Ok(!(bool)S("Dx12Ops", "IsDx12_3d", @"C:\Windows\System32\d3d12.dll"), "IsDx12_3d false for System32 d3d12.dll");

        // detection
        // Helldivers 2's exe is packed (no import table, no Direct3D names): its kind comes from an existing install;
        // the DX12_3D test app imports d3d12.dll
        Ok((bool)S("Dx12Ops", "IsDx12Game", Dx12App), "DX12_3D test app: a DirectX 12 game");
        foreach (var e in new[] { SteelRats, Q2rtx, Lara, Amnesia, SineMora })
            Ok(!(bool)S("Dx12Ops", "IsDx12Game", e), "not DirectX 12 only: " + Path.GetFileName(e));
        Eq(S("Library", "MethodOfExe", Hd2), "dx12", "Library: Helldivers 2 -> dx12");
        string prof = (string)S("Dx12Ops", "Profile", pk, Hd2);
        Ok(prof != null && File.Exists(Path.Combine(prof, "preset.ini")), "profile for helldivers2.exe: " + prof);
        Ok(S("Dx12Ops", "Profile", pk, SteelRats) == null, "no profile for Steel Rats");
        Eq(DDesc(S("Dx12Ops", "Read", Path.Combine(pk, "DX12_3D.ini"), null)), "output=auto swap_eyes=0 separation=25 convergence=100", "Read package defaults");
        Eq(DDesc(S("Dx12Ops", "Read", Path.Combine(pk, "DX12_3D.ini"), prof)), "output=auto swap_eyes=0 separation=12 convergence=4.7", "Read package defaults + Helldivers 2 preset");

        // Helldivers 2 copy: DX12_3D + DXC + preset
        string dH = Fresh("dxH");
        string h = Path.Combine(dH, "helldivers2.exe");
        File.Copy(Hd2, h);
        var oH = S("Dx12Ops", "Read", Path.Combine(pk, "DX12_3D.ini"), prof);
        var logH = (List<string>)S("Dx12Ops", "Install", pk, h, dH, oH);
        Console.WriteLine("  log: " + string.Join(" / ", logH));
        string iniH = Path.Combine(dH, "DX12_3D.ini");
        Ok((bool)S("Dx12Ops", "IsDx12_3d", Path.Combine(dH, "d3d12.dll")) && SameBytes(Path.Combine(dH, "d3d12.dll"), dll), "install: DX12_3D d3d12.dll");
        Ok(SameBytes(Path.Combine(dH, @"DX12_3D\dxcompiler.dll"), Path.Combine(pk, @"DX12_3D\dxcompiler.dll")) && File.Exists(Path.Combine(dH, @"DX12_3D\dxil.dll")), "install: DXC in DX12_3D\\");
        Eq(DDesc(S("Dx12Ops", "Read", iniH, null)), "output=auto swap_eyes=0 separation=12 convergence=4.7", "install: DX12_3D.ini with the preset values");
        Ok((bool)S("Dx12Ops", "Installed", dH), "Installed true");
        Eq(Get(iniH, "Matrices", "enable"), "0", "install: camera matrix fix stays off (package default)");
        // reinstall: ini kept, only the wizard's keys change
        S("GlOps", "Set", iniH, "Generic", "min_width", "32");
        S("Dx12Ops", "Install", pk, h, dH, DOpt(false, "tab", true, 9.5m, 6m));
        Eq(DDesc(S("Dx12Ops", "Read", iniH, null)), "output=tab swap_eyes=1 separation=9.5 convergence=6", "reinstall: new values in the kept ini");
        Eq(Get(iniH, "Generic", "min_width"), "32", "reinstall: other keys kept");
        File.WriteAllText(Path.Combine(dH, "DX12_3D.log"), "x");
        File.WriteAllText(Path.Combine(dH, "DX12_3D_frame00300.bmp"), "x");
        File.WriteAllText(Path.Combine(dH, "DX12_3D_pass_00300_00_001d_L.bmp"), "x");
        Directory.CreateDirectory(Path.Combine(dH, "DX12_3D_shaders"));
        File.WriteAllText(Path.Combine(dH, @"DX12_3D_shaders\a.dxbc"), "x");
        var logR = (List<string>)S("Dx12Ops", "Remove", dH);
        Console.WriteLine("  log: " + string.Join(" / ", logR));
        Eq(Files(dH), "helldivers2.exe", "remove: only the exe left");

        // foreign d3d12.dll kept and restored
        string dF = Fresh("dxF");
        string f = Path.Combine(dF, "helldivers2.exe");
        File.Copy(Hd2, f);
        File.Copy(@"C:\Windows\System32\d3d12.dll", Path.Combine(dF, "d3d12.dll"));
        S("Dx12Ops", "Install", pk, f, dF, DOpt(true, "sbs", false, 25m, 100m));
        Ok(SameBytes(Path.Combine(dF, "d3d12.dll.geo11sr_bak"), @"C:\Windows\System32\d3d12.dll"), "install over another d3d12.dll keeps it as .geo11sr_bak");
        S("Dx12Ops", "Remove", dF);
        Ok(SameBytes(Path.Combine(dF, "d3d12.dll"), @"C:\Windows\System32\d3d12.dll") && !File.Exists(Path.Combine(dF, "d3d12.dll.geo11sr_bak")), "remove restores the other d3d12.dll");

        // 32-bit exe refused, nothing written
        string d32 = Fresh("dx32");
        string a32 = Path.Combine(d32, "Amnesia.exe");
        File.Copy(Amnesia, a32);
        try { S("Dx12Ops", "Install", pk, a32, d32, DOpt(true, "sbs", false, 25m, 100m)); Ok(false, "32-bit exe refused"); }
        catch (InvalidOperationException e) { Ok(e.Message.Contains("64"), "32-bit exe refused (" + e.Message + ")"); }
        Eq(Files(d32), "Amnesia.exe", "32-bit: nothing written");
    }

    // dx12gui: the wizard on a copy of the DX12_3D test app named helldivers2.exe (DX12 imports + the Helldivers 2 preset),
    //          and on a copy next to a GameGuard folder (refused)
    static void Dx12Gui()
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        Ok(Directory.Exists(Path.Combine(exeDir, @"Packages\DX12_3D")), "test exe next to Packages\\DX12_3D: " + exeDir);
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        string gamesDir = Path.Combine(outDir, "games");
        if (Directory.Exists(gamesDir)) Directory.Delete(gamesDir, true);
        string h = Copy(Dx12App, @"HD2\helldivers2.exe");
        string ac = Copy(Dx12App, @"AC\bin\dx12game.exe");
        Directory.CreateDirectory(Path.Combine(outDir, @"games\AC\bin\GameGuard"));
        string hDir = Path.GetDirectoryName(h), acDir = Path.GetDirectoryName(ac);

        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(40, 40);
        form.Show();
        Pump();
        var tabs = C<TabControl>(form, "tabs");
        Func<string, RadioButton> rb = n => C<RadioButton>(form, n);
        Func<string, CheckBox> cb = n => C<CheckBox>(form, n);
        Func<string, Control> ctl = n => C<Control>(form, n);
        var numSep = C<NumericUpDown>(form, "numSep");
        var numConv = C<NumericUpDown>(form, "numConv");
        var cbMode = C<ComboBox>(form, "cbGlMode");

        // ---- DirectX 12 kind, Helldivers 2 preset
        Call(form, "StartWizard", h);
        Pump();
        Ok(rb("rbKindDx12").Checked && !rb("rbKindVk").Checked && !rb("rbKindGl").Checked && !rb("rbKindGeo").Checked, "DX12 app: DirectX 12 kind");
        Ok(rb("rbCatWrap").Checked && Shown(ctl("pnlKindWrap")) && !Shown(ctl("pnlKindGeo")), "DX12 app: own-wrapper category");
        Ok(rb("rb64").Checked && !rb("rb32").Enabled && !rb("rb64").Enabled, "DX12 app: 64-bit, bits locked");
        Ok(Shown(ctl("lblDx12Install")) && !Shown(ctl("lblVkInstall")) && !Shown(ctl("lblGlInstall")) && !Shown(ctl("pnlScope")) && !cb("chkVkDxvk").Enabled, "install step: DX12_3D note only, DXVK option off");
        Ok(Shown(ctl("pnlGlMode")) && !Shown(ctl("pnlGeoMode")) && !Shown(cb("chkLens")) && !Shown(cb("chkUpscale")) && !Shown(ctl("lblUpHead")), "display step: DX12_3D output, no lens / upscale");
        Eq(string.Join(",", cbMode.Items.Cast<object>()), "sbs,tab", "display step: DX12_3D output choices");
        Eq(ctl("lblSrHead").Text, "SR 위빙 (DX12_3D)", "display heading");
        Eq(ctl("lblStereoHead").Text, "기본 입체 값 (DX12_3D.ini [Stereo])", "stereo heading");
        Ok(Shown(ctl("lblStereoDx12")) && !Shown(ctl("lblStereoVk")) && !Shown(ctl("lblStereoGl")) && !Shown(cb("chkAutoConv")), "stereo step: DX12_3D note, no auto-convergence");
        Ok(numSep.Value == 12m && numConv.Value == 4.7m, "Helldivers 2 preset: separation 12, convergence 4.7 (" + numSep.Value + ", " + numConv.Value + ")");
        Console.WriteLine("  detect: " + ctl("lblDetect").Text + "  |  kind: " + ctl("lblKindDetect").Text);
        Ok(ctl("lblDetect").Text.Contains("권장 값") && ctl("lblKindDetect").Text.Contains("DX12_3D"), "detect notes name DX12_3D and the preset");
        for (int i = 0; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "ko_dx12_tab" + i); }
        Ok(TakeDialogs().Count == 0, "no message box so far");
        cb("chkSr").Checked = false; Pump();
        cbMode.SelectedItem = "tab";
        numSep.Value = 15m;
        Call(form, "Apply");
        Pump();
        var dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("DX12_3D를 설치했습니다"), "Apply: one info box (" + string.Join(" || ", dl) + ")");
        Eq(DDesc(S("Dx12Ops", "Read", Path.Combine(hDir, "DX12_3D.ini"), null)), "output=tab swap_eyes=0 separation=15 convergence=4.7", "Apply: DX12_3D.ini");
        Ok((bool)S("Dx12Ops", "Installed", hDir) && File.Exists(Path.Combine(hDir, @"DX12_3D\dxil.dll")), "Apply: d3d12.dll + DXC");
        var row = Row(form, h);
        if (row != null) { Console.WriteLine("  row: " + string.Join(" | ", row)); Eq(row[1], "설정 완료", "list: state"); Eq(row[3], "DX12", "list: engine"); Eq(row[5], "끔", "list: SR"); Eq(row[6], "-", "list: upscale"); Eq(row[7], "15", "list: Sep"); Eq(row[8], "4.7", "list: Conv"); }
        else Ok(false, "DX12 app listed");
        var lv = C<ListView>(form, "lvGames");
        foreach (ListViewItem it in lv.Items) it.Selected = string.Equals((string)it.Tag, h, StringComparison.OrdinalIgnoreCase);
        Pump();
        Eq(C<Button>(form, "bLogGame").Text, "DX12_3D.log", "log button: DX12_3D.log");
        Call(form, "StartWizard", h);   // re-edit
        Pump();
        Ok(rb("rbKindDx12").Checked && !cb("chkSr").Checked && (string)cbMode.SelectedItem == "tab" && numSep.Value == 15m && numConv.Value == 4.7m, "re-edit: values from the game's DX12_3D.ini");
        Ok(ctl("lblDetect").Text.Contains("DX12_3D 설치됨"), "re-edit: installed version noted");
        Call(form, "ApplyHotkeysOnly");
        Ok(!File.Exists(Path.Combine(hDir, "d3dxdm.ini")) && !File.Exists(Path.Combine(hDir, "SRWeave.ini")), "hotkeys-only writes nothing into a DX12_3D game");
        cb("chkSr").Checked = true; Pump();
        Call(form, "Apply"); Pump(); TakeDialogs();
        Eq(Get(Path.Combine(hDir, "DX12_3D.ini"), "Stereo", "output"), "auto", "SR on: output = auto");
        Call(form, "RemoveSr"); Pump();
        Eq(Get(Path.Combine(hDir, "DX12_3D.ini"), "Stereo", "output"), "sbs", "remove SR weaving: output = sbs");
        Call(form, "UninstallAll"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("DX12_3D(DirectX 12)를 제거합니다"), "uninstall asks once");
        Eq(Files(hDir), "helldivers2.exe", "uninstall: only the exe left");
        row = Row(form, h);
        if (row != null) Eq(row[1], "재설정 필요", "list: back to 'needs setup'");

        // ---- anti-cheat folder: noted at detection, Apply refuses
        Call(form, "StartWizard", ac);
        Pump();
        Ok(rb("rbKindDx12").Checked && ctl("lblDetect").Text.Contains("GameGuard"), "anti-cheat game: noted (" + ctl("lblDetect").Text + ")");
        TakeDialogs();
        Call(form, "Apply"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("안티치트"), "anti-cheat game: Apply refused (" + string.Join(" || ", dl) + ")");
        Eq(Files(acDir), "dx12game.exe,GameGuard", "anti-cheat game: nothing written");

        // ---- English
        Ty("Lang").GetField("English").SetValue(null, true);
        Call(form, "RebuildUi");
        Pump();
        tabs = C<TabControl>(form, "tabs");
        Call(form, "StartWizard", h);
        Pump();
        Eq(ctl("lblSrHead").Text, "SR weaving (DX12_3D)", "English: SR heading");
        Eq(rb("rbKindDx12").Text, "DirectX 12 games - DX12_3D (d3d12.dll, 64-bit)", "English: game type");
        for (int i = 0; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "en_dx12_tab" + i); }
        Ok(TakeDialogs().Count == 0, "no message boxes in English pass");
        form.Close();
        Pump();
    }
}
