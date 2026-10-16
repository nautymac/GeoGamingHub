// Self-tests of Geo Gaming Hub's Vulkan (VK3D) kind and the kind categories; part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> vkunit|vkgui
// vkunit: VkOps on temp folders with copies of game exes (Quake II RTX = Vulkan with a fix, Lara Croft GO = 64-bit
//         Unity DX9, Steel Rats = 64-bit UE4 DX11, Amnesia = 32-bit)
// vkgui:  MainForm in-process (HubTest.exe sits in the project root next to Packages\): wizard flows on copies
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static partial class HubTest
{
    static readonly string Q2rtx = Common + @"Quake II RTX\q2rtx.exe";
    static readonly string Lara = Common + @"Lara Croft GO\Lara Croft GO.exe";

    static object VOpt(bool sr, string mode, bool swap, decimal sep, bool auto, decimal conv, bool dxvk)
    {
        var t = Ty("VkOps+Options");
        var o = Activator.CreateInstance(t);
        t.GetField("Sr").SetValue(o, sr); t.GetField("Mode").SetValue(o, mode); t.GetField("Swap").SetValue(o, swap);
        t.GetField("Separation").SetValue(o, sep); t.GetField("Auto").SetValue(o, auto); t.GetField("Convergence").SetValue(o, conv);
        t.GetField("Dxvk").SetValue(o, dxvk);
        return o;
    }

    static string VDesc(object o) { return (string)S("VkOps", "Describe", o); }

    static string Files(string d)
    {
        return string.Join(",", Directory.GetFileSystemEntries(d).Select(Path.GetFileName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }

    static void VkUnit(string app)
    {
        string pk = Path.Combine(Path.GetDirectoryName(app), "Packages");
        string vk = Path.Combine(pk, "VK3D"), dx = Path.Combine(pk, "DXVK");
        Ok((bool)S("VkOps", "IsVk3d", Path.Combine(vk, @"x64\vulkan-1.dll")), "package: VK3D vulkan-1.dll");
        Eq(Machine(Path.Combine(vk, @"x64\vulkan-1.dll")), 0x8664, "package: VK3D DLL is AMD64");
        Ok(!(bool)S("VkOps", "IsVk3d", @"C:\Windows\System32\vulkan-1.dll"), "IsVk3d false for the Vulkan loader");
        foreach (var n in new[] { "d3d9.dll", "d3d11.dll", "dxgi.dll", "d3d10core.dll" })
            Ok((bool)S("VkOps", "IsDxvk", Path.Combine(dx, @"x64\" + n)), "package: DXVK " + n);
        Ok(!(bool)S("VkOps", "IsDxvk", @"C:\Windows\System32\d3d11.dll"), "IsDxvk false for System32 d3d11.dll");

        // detection
        Ok((bool)S("VkOps", "ImportsVulkan", Q2rtx) && !(bool)S("VkOps", "ImportsDirect3D", Q2rtx), "Quake II RTX imports vulkan-1.dll, no Direct3D");
        Ok(!(bool)S("VkOps", "ImportsVulkan", Lara), "Lara Croft GO does not import vulkan-1.dll");
        Ok(S("Util", "DirectXOf", Lara) != null, "Lara Croft GO: a DirectX game (Unity names d3d9 / d3d11 / d3d12: " + S("Util", "DirectXOf", Lara) + ")");
        Eq(S("Util", "DirectXOf", SteelRats), "DX11", "Steel Rats: DX11");
        string prof = (string)S("VkOps", "Profile", vk, Q2rtx);
        Ok(prof != null && File.Exists(Path.Combine(prof, @"VK3D_fix\fix.ini")), "profile for q2rtx.exe with VK3D_fix: " + prof);
        Ok(S("VkOps", "Profile", vk, Lara) == null, "no profile for Lara Croft GO");
        Eq(VDesc(S("VkOps", "Read", Path.Combine(vk, "VK3D.ini"), null)), "output=auto swap_eyes=0 separation=25 convergence=auto", "Read package defaults");
        Eq(VDesc(S("VkOps", "Read", Path.Combine(vk, "VK3D.ini"), prof)), "output=auto swap_eyes=0 separation=25 convergence=60", "Read package defaults + Quake II RTX preset");

        // Quake II RTX copy: VK3D + its fix + preset
        string dQ = Fresh("vkQ");
        string q = Path.Combine(dQ, "q2rtx.exe");
        File.Copy(Q2rtx, q);
        var oQ = S("VkOps", "Read", Path.Combine(vk, "VK3D.ini"), prof);
        var logQ = (List<string>)S("VkOps", "Install", vk, dx, q, dQ, oQ);
        Console.WriteLine("  log: " + string.Join(" / ", logQ));
        string iniQ = Path.Combine(dQ, "VK3D.ini");
        Ok((bool)S("VkOps", "IsVk3d", Path.Combine(dQ, "vulkan-1.dll")) && SameBytes(Path.Combine(dQ, "vulkan-1.dll"), Path.Combine(vk, @"x64\vulkan-1.dll")), "install: VK3D vulkan-1.dll");
        Eq(VDesc(S("VkOps", "Read", iniQ, null)), "output=auto swap_eyes=0 separation=25 convergence=60", "install: VK3D.ini with the preset values");
        Ok(Directory.GetFiles(Path.Combine(dQ, "VK3D_fix")).Length == Directory.GetFiles(Path.Combine(prof, "VK3D_fix")).Length, "install: VK3D_fix copied");
        Ok((bool)S("VkOps", "Installed", dQ) && !(bool)S("VkOps", "DxvkInstalled", dQ), "Installed true, no DXVK");
        // reinstall: ini kept, only the wizard's keys change; [Generic] etc. untouched
        File.AppendAllText(iniQ, "\r\n[FixParams]\r\nuser0 = 0.15\r\n", Encoding.Default);
        S("VkOps", "Install", vk, dx, q, dQ, VOpt(false, "tab", true, 40m, true, 60m, false));
        Eq(VDesc(S("VkOps", "Read", iniQ, null)), "output=tab swap_eyes=1 separation=40 convergence=auto", "reinstall: new values in the kept ini");
        Eq(Get(iniQ, "FixParams", "user0"), "0.15", "reinstall: other sections kept");
        Eq(Get(iniQ, "Generic", "enable"), "1", "reinstall: [Generic] enable untouched");
        File.WriteAllText(Path.Combine(dQ, "VK3D.log"), "x");
        File.WriteAllText(Path.Combine(dQ, "VK3D_frame00300.bmp"), "x");
        var logR = (List<string>)S("VkOps", "Remove", dQ);
        Console.WriteLine("  log: " + string.Join(" / ", logR));
        Eq(Files(dQ), "q2rtx.exe", "remove: only the exe left");

        // foreign vulkan-1.dll (the loader) kept and restored
        string dF = Fresh("vkF");
        string f = Path.Combine(dF, "q2rtx.exe");
        File.Copy(Q2rtx, f);
        File.Copy(@"C:\Windows\System32\vulkan-1.dll", Path.Combine(dF, "vulkan-1.dll"));
        S("VkOps", "Install", vk, dx, f, dF, VOpt(true, "sbs", false, 25m, true, 30m, false));
        Ok(SameBytes(Path.Combine(dF, "vulkan-1.dll.geo11sr_bak"), @"C:\Windows\System32\vulkan-1.dll"), "install over the loader keeps it as .geo11sr_bak");
        S("VkOps", "Remove", dF);
        Ok(SameBytes(Path.Combine(dF, "vulkan-1.dll"), @"C:\Windows\System32\vulkan-1.dll") && !File.Exists(Path.Combine(dF, "vulkan-1.dll.geo11sr_bak")), "remove restores the loader");

        // 32-bit exe refused, nothing written
        string d32 = Fresh("vk32");
        string a32 = Path.Combine(d32, "Amnesia.exe");
        File.Copy(Amnesia, a32);
        try { S("VkOps", "Install", vk, dx, a32, d32, VOpt(true, "sbs", false, 25m, true, 30m, false)); Ok(false, "32-bit exe refused"); }
        catch (InvalidOperationException e) { Ok(e.Message.Contains("64"), "32-bit exe refused (" + e.Message + ")"); }
        Eq(Files(d32), "Amnesia.exe", "32-bit: nothing written");

        // DXVK route: Lara Croft GO (64-bit DX9)
        string dL = Fresh("vkL");
        string l = Path.Combine(dL, "Lara Croft GO.exe");
        File.Copy(Lara, l);
        var logL = (List<string>)S("VkOps", "Install", vk, dx, l, dL, VOpt(true, "sbs", false, 25m, true, 30m, true));
        Console.WriteLine("  log: " + string.Join(" / ", logL));
        Ok(new[] { "d3d9.dll", "d3d11.dll", "dxgi.dll", "d3d10core.dll" }.All(n => (bool)S("VkOps", "IsDxvk", Path.Combine(dL, n))), "DXVK: all four DLLs");
        Ok((bool)S("VkOps", "DxvkInstalled", dL) && (bool)S("VkOps", "Installed", dL), "DXVK DX9: installed");
        File.WriteAllText(Path.Combine(dL, "Lara Croft GO_d3d9.log"), "x");
        S("VkOps", "Remove", dL);
        Eq(Files(dL), "Lara Croft GO.exe", "DXVK DX9: remove leaves only the exe (DXVK log too)");

        // DXVK route: Steel Rats (64-bit DX11) - three DLLs; refused over a foreign d3d11.dll (geo-11)
        string dS = Fresh("vkS");
        string sx = Path.Combine(dS, "SteelRats-Win64-Shipping.exe");
        File.Copy(SteelRats, sx);
        S("VkOps", "Install", vk, dx, sx, dS, VOpt(true, "sbs", false, 25m, true, 30m, true));
        Ok(new[] { "d3d9.dll", "d3d11.dll", "dxgi.dll", "d3d10core.dll" }.All(n => (bool)S("VkOps", "IsDxvk", Path.Combine(dS, n))), "DXVK DX11: all four DLLs");
        S("VkOps", "Remove", dS);
        Eq(Files(dS), "SteelRats-Win64-Shipping.exe", "DXVK DX11: removed");
        File.Copy(@"C:\Windows\System32\d3d11.dll", Path.Combine(dS, "d3d11.dll"));
        try { S("VkOps", "Install", vk, dx, sx, dS, VOpt(true, "sbs", false, 25m, true, 30m, true)); Ok(false, "DXVK over a foreign d3d11.dll refused"); }
        catch (InvalidOperationException e) { Ok(e.Message.Contains("d3d11.dll"), "DXVK over a foreign d3d11.dll refused (" + e.Message + ")"); }
        Eq(Files(dS), "d3d11.dll,SteelRats-Win64-Shipping.exe", "refused: nothing written");
        try { S("VkOps", "Install", vk, dx, q, Fresh("vkQ2"), VOpt(true, "sbs", false, 25m, true, 30m, true)); Ok(false, "DXVK for a non-DX game refused"); }
        catch (InvalidOperationException e) { Ok(true, "DXVK for a Vulkan game refused (" + e.Message + ")"); }
    }

    static Label FindLabel(Control root, string text)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Label && c.Text == text) return (Label)c;
            var r = FindLabel(c, text);
            if (r != null) return r;
        }
        return null;
    }

    static void VkGui()
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        Ok(Directory.Exists(Path.Combine(exeDir, @"Packages\VK3D")), "test exe next to Packages\\VK3D: " + exeDir);
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        string gamesDir = Path.Combine(outDir, "games");
        if (Directory.Exists(gamesDir)) Directory.Delete(gamesDir, true);
        string q = Copy(Q2rtx, @"Q2RTX\q2rtx.exe");
        string la = Copy(Lara, @"Lara\Lara Croft GO.exe");
        Directory.CreateDirectory(Path.Combine(outDir, @"games\Lara\Lara Croft GO_Data\Managed"));   // Unity layout
        File.Copy(Path.Combine(Path.GetDirectoryName(Lara), @"Lara Croft GO_Data\globalgamemanagers"), Path.Combine(outDir, @"games\Lara\Lara Croft GO_Data\globalgamemanagers"));
        string qDir = Path.GetDirectoryName(q), laDir = Path.GetDirectoryName(la);

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
        Eq(form.Text, "Geo Gaming Hub", "window title");
        Eq(tabs.TabPages[2].Text, "3D 방식", "step 1 is called 3D 방식");
        Eq(rb("rbCatGeo").Text + " / " + rb("rbCatWrap").Text, "geo-11 - DirectX 9 / 11 게임 / 자체 래퍼 - Vulkan / OpenGL 게임", "two categories");

        // ---- Quake II RTX: Vulkan kind, fix preset
        Call(form, "StartWizard", q);
        Pump();
        Ok(rb("rbKindVk").Checked && !rb("rbKindGl").Checked && !rb("rbKindGeo").Checked, "Q2RTX: Vulkan kind");
        Ok(rb("rbCatWrap").Checked && Shown(ctl("pnlKindWrap")) && !Shown(ctl("pnlKindGeo")), "Q2RTX: own-wrapper category, only its game types shown");
        rb("rbCatGeo").Checked = true; Pump();
        Ok(rb("rbKindGeo").Checked && !rb("rbKindVk").Checked && Shown(ctl("pnlKindGeo")) && !Shown(ctl("pnlKindWrap")) && Shown(ctl("pnlScope")), "category geo-11: geo-11 game type picked, geo-11 steps");
        rb("rbCatWrap").Checked = true; Pump();
        Ok(rb("rbKindVk").Checked && !rb("rbKindGeo").Checked && !rb("rbKindUe4").Checked && !rb("rbKindDx9").Checked && Shown(ctl("lblVkInstall")), "back to own wrappers: Vulkan again (the last pick)");
        Ok(rb("rb64").Checked && !rb("rb32").Enabled && !rb("rb64").Enabled, "Q2RTX: 64-bit, bits locked");
        Ok(!cb("chkVkDxvk").Enabled, "Q2RTX: DXVK option off (not a DirectX game)");
        Ok(Shown(ctl("lblVkInstall")) && !Shown(ctl("lblGlInstall")) && !Shown(ctl("pnlScope")), "install step: VK3D note");
        Ok(Shown(ctl("pnlGlMode")) && !Shown(ctl("pnlGeoMode")) && !Shown(cb("chkLens")) && !Shown(cb("chkUpscale")) && !Shown(ctl("lblUpHead")), "display step: VK3D output, no lens / upscale");
        Eq(string.Join(",", cbMode.Items.Cast<object>()), "sbs,tab", "display step: VK3D output choices");
        Eq(ctl("lblSrHead").Text, "SR 위빙 (VK3D)", "display heading");
        Eq(ctl("lblStereoHead").Text, "기본 입체 값 (VK3D.ini [Stereo])", "stereo heading");
        Ok(Shown(ctl("lblStereoVk")) && !Shown(ctl("lblStereoGl")), "stereo step: VK3D note");
        Ok(!cb("chkAutoConv").Checked && numConv.Value == 60m && numConv.Enabled && numSep.Value == 25m, "Q2RTX preset: convergence 60 manual, separation 25");
        Ok(ctl("lblDetect").Text.Contains("Vulkan") && ctl("lblDetect").Text.Contains("VK3D_fix") && ctl("lblKindDetect").Text.Contains("VK3D"), "detect notes: " + ctl("lblDetect").Text);
        for (int i = 0; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "ko_vk_tab" + i); }
        Ok(TakeDialogs().Count == 0, "no message box so far");
        cb("chkSr").Checked = false; Pump();
        cbMode.SelectedItem = "tab";
        numSep.Value = 30m;
        Call(form, "Apply");
        Pump();
        var dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("VK3D를 설치했습니다"), "Apply: one info box (" + string.Join(" || ", dl) + ")");
        Eq(VDesc(S("VkOps", "Read", Path.Combine(qDir, "VK3D.ini"), null)), "output=tab swap_eyes=0 separation=30 convergence=60", "Apply: VK3D.ini");
        Ok(File.Exists(Path.Combine(qDir, @"VK3D_fix\fix.ini")) && (bool)S("VkOps", "Installed", qDir), "Apply: VK3D + VK3D_fix");
        var row = Row(form, q);
        if (row != null) { Console.WriteLine("  row: " + string.Join(" | ", row)); Eq(row[1], "설정 완료", "list: state"); Eq(row[3], "Vulkan", "list: engine"); Eq(row[5], "끔", "list: SR"); Eq(row[6], "-", "list: upscale"); Eq(row[7], "30", "list: Sep"); Eq(row[8], "60", "list: Conv"); }
        else Ok(false, "Q2RTX listed");
        var lv = C<ListView>(form, "lvGames");
        foreach (ListViewItem it in lv.Items) it.Selected = string.Equals((string)it.Tag, q, StringComparison.OrdinalIgnoreCase);
        Pump();
        Eq(C<Button>(form, "bLogGame").Text, "VK3D.log", "log button: VK3D.log");
        Call(form, "StartWizard", q);   // re-edit
        Pump();
        Ok(rb("rbKindVk").Checked && !cb("chkSr").Checked && (string)cbMode.SelectedItem == "tab" && numSep.Value == 30m && numConv.Value == 60m, "re-edit: values from the game's VK3D.ini");
        Ok(ctl("lblDetect").Text.Contains("VK3D 설치됨"), "re-edit: installed version noted");
        Call(form, "ApplyHotkeysOnly");
        Ok(!File.Exists(Path.Combine(qDir, "d3dxdm.ini")) && !File.Exists(Path.Combine(qDir, "SRWeave.ini")), "hotkeys-only writes nothing into a VK3D game");
        cb("chkSr").Checked = true; Pump();
        Call(form, "Apply"); Pump(); TakeDialogs();
        Call(form, "RemoveSr"); Pump();
        Eq(Get(Path.Combine(qDir, "VK3D.ini"), "Stereo", "output"), "sbs", "remove SR weaving: output = sbs");
        Call(form, "UninstallAll"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("VK3D(Vulkan)를 제거합니다"), "uninstall asks once");
        Eq(Files(qDir), "q2rtx.exe", "uninstall: only the exe left");
        row = Row(form, q);
        if (row != null) Eq(row[1], "재설정 필요", "list: back to 'needs setup'");

        // ---- Lara Croft GO: Unity DX9 -> geo-11 by default; switching to Vulkan offers DXVK
        Call(form, "StartWizard", la);
        Pump();
        Ok(rb("rbKindGeo").Checked && !rb("rbKindVk").Checked && rb("rbCatGeo").Checked, "Lara: geo-11 (Unity) by default  [" + ctl("lblDetect").Text + "]");
        rb("rbCatWrap").Checked = true; Pump();
        Ok(rb("rbKindVk").Checked && !rb("rbKindGeo").Checked, "Lara: own-wrapper category picks Vulkan (not an OpenGL game)");
        Ok(cb("chkVkDxvk").Enabled && !cb("chkVkDxvk").Checked, "Lara as Vulkan: DXVK option offered");
        Ok(!Shown(ctl("pnlUe4")) && Shown(ctl("lblVkInstall")), "Lara as Vulkan: VK3D install note");
        cb("chkVkDxvk").Checked = true;
        Call(form, "Apply"); Pump();
        dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("VK3D를 설치했습니다"), "Lara: installed (" + string.Join(" || ", dl) + ")");
        Ok((bool)S("VkOps", "IsDxvk", Path.Combine(laDir, "d3d9.dll")) && (bool)S("VkOps", "Installed", laDir), "Lara: DXVK d3d9 + VK3D");
        row = Row(form, la);
        if (row != null) Eq(row[3], "Vulkan (DXVK)", "list: engine Vulkan (DXVK)");
        Call(form, "StartWizard", la);   // an existing VK3D install wins over the Unity detection
        Pump();
        Ok(rb("rbKindVk").Checked && cb("chkVkDxvk").Checked, "Lara re-edit: Vulkan kind with DXVK");
        Call(form, "UninstallAll"); Pump(); TakeDialogs();
        Ok(!File.Exists(Path.Combine(laDir, "d3d9.dll")) && !File.Exists(Path.Combine(laDir, "vulkan-1.dll")), "Lara: uninstall removes DXVK and VK3D");

        // ---- English
        Ty("Lang").GetField("English").SetValue(null, true);
        Call(form, "RebuildUi");
        Pump();
        tabs = C<TabControl>(form, "tabs");
        Call(form, "StartWizard", q);
        Pump();
        Eq(ctl("lblSrHead").Text, "SR weaving (VK3D)", "English: SR heading");
        Eq(rb("rbCatWrap").Text, "Own wrappers - Vulkan / OpenGL games", "English: category");
        Eq(tabs.TabPages[2].Text, "3D Method", "English: step 1 is called 3D Method");
        for (int i = 0; i <= 5; i++) { tabs.SelectedIndex = i; Shot(form, "en_vk_tab" + i); }
        Ok(TakeDialogs().Count == 0, "no message boxes in English pass");
        form.Close();
        Pump();
    }
}
