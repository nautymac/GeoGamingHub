// Self-tests of the candidate list (Library.cs, the 후보 목록 tab); part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> cand
// MethodOfExe on copies of game exes, Load / Merge on a small data file, then the tab in-process with the real data
// file and the games installed on this PC (read-only: a tested candidate's wizard is started, nothing is applied).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static partial class HubTest
{
    static string Method(string exe) { return (string)S("Library", "MethodOfExe", exe); }

    static void Cands()
    {
        // ---- MethodOfExe on copies (no installs next to them)
        string q = Copy(Q2rtx, @"cand\Q2\q2rtx.exe");
        Eq(Method(q), "vulkan", "MethodOfExe: Quake II RTX");
        string sr = Copy(SteelRats, @"cand\SteelRats\SteelRats\Binaries\Win64\SteelRats-Win64-Shipping.exe");
        Directory.CreateDirectory(Path.Combine(outDir, @"games\cand\SteelRats\SteelRats\Content"));
        Eq(Method(sr), "ue4", "MethodOfExe: Steel Rats (UE4 layout)");
        Eq(Method(Copy(BrokenAge, @"cand\BA\BrokenAge.exe")), "opengl", "MethodOfExe: Broken Age (OpenGL although it names d3d9)");
        Eq(Method(Copy(SineMora, @"cand\SM\SineMoraEX.exe")), "dx9", "MethodOfExe: Sine Mora EX");
        Eq(Method(Copy(Rfg, @"cand\Rfg\rfg.exe")), "dx11", "MethodOfExe: Red Faction Guerrilla");
        string la = Copy(Lara, @"cand\Lara\Lara Croft GO.exe");
        Directory.CreateDirectory(Path.Combine(outDir, @"games\cand\Lara\Lara Croft GO_Data"));
        File.Copy(Path.Combine(Path.GetDirectoryName(Lara), @"Lara Croft GO_Data\globalgamemanagers"), Path.Combine(outDir, @"games\cand\Lara\Lara Croft GO_Data\globalgamemanagers"), true);
        Eq(Method(la), "unity", "MethodOfExe: Lara Croft GO (Unity layout)");
        Eq(Method(Path.Combine(outDir, "missing.exe")), "unknown", "MethodOfExe: missing file");
        // anti-cheat markers (copies of folder layouts, no game files)
        string ac1 = Fresh(@"ac\Arc"); Directory.CreateDirectory(Path.Combine(ac1, "Installers")); File.WriteAllText(Path.Combine(ac1, @"Installers\AntiCheatInstaller.exe"), "x");
        string ac2 = Fresh(@"ac\Hd2"); Directory.CreateDirectory(Path.Combine(ac2, @"bin\GameGuard"));
        string ac3 = Fresh(@"ac\Eac"); Directory.CreateDirectory(Path.Combine(ac3, @"Game\Binaries\Win64\EasyAntiCheat"));
        Eq(S("Library", "AntiCheat", ac1), "AntiCheatInstaller.exe", "AntiCheat: an anti-cheat installer (ARC Raiders layout)");
        Eq(S("Library", "AntiCheat", ac2), "GameGuard", "AntiCheat: nProtect GameGuard (HELLDIVERS 2 layout)");
        Eq(S("Library", "AntiCheat", ac3), null, "AntiCheat: deeper than three levels is not searched");
        Directory.CreateDirectory(Path.Combine(ac3, "EasyAntiCheat"));
        Eq(S("Library", "AntiCheat", ac3), "EasyAntiCheat", "AntiCheat: EasyAntiCheat");
        Eq(S("Library", "AntiCheat", Path.GetDirectoryName(q)), null, "AntiCheat: none in a plain game folder");
        string ac4 = Fresh(@"ac\Dk"); Directory.CreateDirectory(Path.Combine(ac4, @"media\sounds")); File.WriteAllText(Path.Combine(ac4, @"media\sounds\ricochet1.ogg"), "x");
        File.WriteAllText(Path.Combine(ac4, "anticheat_notes.txt"), "x");
        Eq(S("Library", "AntiCheat", ac4), null, "AntiCheat: game content (ricochet1.ogg, a .txt) does not count (Door Kickers: Action Squad)");

        // ---- Load / Merge on a small data file
        string d = Fresh("canddata");
        string f = Path.Combine(d, "c.json");
        File.WriteAllText(f, "{\"schema\":1,\"games\":[" +
            "{\"title\":\"Quake II RTX\",\"steam_appid\":1089130,\"method\":\"vulkan\",\"alts\":[],\"engine\":[\"id Tech 2\"],\"api\":\"Vulkan\",\"bits\":\"x64\",\"status\":\"verified\",\"note\":\"fix\",\"flat2d\":false}," +
            "{\"title\":\"Some Game: Deluxe\",\"steam_appid\":null,\"method\":\"dx11\",\"alts\":[\"vulkan\"],\"engine\":[],\"api\":\"D3D 11, Vulkan\",\"bits\":\"\",\"status\":\"untested\",\"note\":\"\",\"flat2d\":true}]}", Encoding.UTF8);
        var data = (IList)S("Library", "Load", f);
        Eq(data.Count, 2, "Load: two entries");
        var c0 = data[0];
        Eq(F(c0, "Method"), "vulkan", "Load: method"); Eq(F(c0, "SteamAppId"), "1089130", "Load: Steam appid"); Eq(F(c0, "Engine"), "id Tech 2", "Load: engine");
        Eq(string.Join(",", (string[])F(data[1], "Alts")), "vulkan", "Load: alternatives"); Eq(F(data[1], "Flat2D"), true, "Load: flat 2D flag");
        Eq(((IList)S("Library", "Load", Path.Combine(d, "none.json"))).Count, 0, "Load: missing file = empty");
        var igT = Ty("InstalledGame");
        Func<string, string, string, object> inst = (title, appid, dir) =>
        {
            var g = Activator.CreateInstance(igT);
            igT.GetField("Title").SetValue(g, title); igT.GetField("SteamAppId").SetValue(g, appid); igT.GetField("Dir").SetValue(g, dir); igT.GetField("Store").SetValue(g, "Steam");
            return g;
        };
        var installed = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(igT));
        installed.Add(inst("Quake II RTX", "1089130", Path.GetDirectoryName(q)));
        installed.Add(inst("Some Game Deluxe", null, d));          // title match without punctuation
        installed.Add(inst("Not In Data", "999", Path.GetDirectoryName(sr)));
        var merged = (IList)S("Library", "Merge", data, installed);
        Eq(merged.Count, 3, "Merge: two data entries + one installed-only game");
        Ok(F(merged[0], "Installed") != null && F(merged[1], "Installed") != null, "Merge: data entries matched by appid and by title");
        var extra = merged[2];
        Ok((bool)F(extra, "FromExe") && F(extra, "Method") == null && (string)F(extra, "Title") == "Not In Data", "Merge: installed-only game waits for its exe");

        // ---- the tab, with the real data file and this PC's installed games
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        Ok(File.Exists(Path.Combine(exeDir, @"data\game_candidates.json")), "data\\game_candidates.json next to the test exe");
        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(40, 40);
        form.Show();
        Pump();
        var tabs = C<TabControl>(form, "tabs");
        Eq(tabs.TabPages[1].Text, "후보 목록", "tab 1 is the candidate list");
        tabs.SelectedIndex = 1;
        Pump();
        var cands = (IList)F(form, "candidates");
        Ok(cands.Count > 500, "candidates loaded: " + cands.Count);
        for (int i = 0; i < 300 && (bool)F(form, "candScanning"); i++) { Pump(); Thread.Sleep(100); }
        Ok(!(bool)F(form, "candScanning"), "installed games' exes read");
        var lv = C<ListView>(form, "lvCands");
        Func<string, ListViewItem> row = t => lv.Items.Cast<ListViewItem>().FirstOrDefault(it => it.Text == t);
        Console.WriteLine("  shown: " + lv.Items.Count + " / " + C<Label>(form, "lblCandInfo").Text);
        Ok(lv.Items.Count > 0 && lv.Items.Cast<ListViewItem>().All(it => it.SubItems[5].Text.Length > 0), "only installed games shown by default");
        var rq = row("Quake II RTX");
        Ok(rq != null && rq.SubItems[1].Text == "Geo Gaming Hub" && rq.SubItems[2].Text == "Vulkan → VK3D" && rq.SubItems[4].Text == "검증됨", "Quake II RTX: Vulkan → VK3D, verified" + (rq == null ? "" : "  [" + string.Join(" | ", rq.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(x => x.Text)) + "]"));
        Ok(lv.Items.Cast<ListViewItem>().All(it => it.SubItems[2].Text != "확인 중..."), "no row left at 'checking'");
        var arc = row("ARC Raiders");
        if (arc != null)
        {
            Eq(arc.SubItems[4].Text, "안티치트", "ARC Raiders: anti-cheat");
            arc.Selected = true; Pump();
            Call(form, "StartFromCandidate"); Pump();
            Eq(tabs.SelectedIndex, 1, "ARC Raiders: setup refused, still on the candidate list");
            Ok(!File.Exists(Path.Combine(exeDir, "games.list")) || !File.ReadAllText(Path.Combine(exeDir, "games.list")).Contains("Arc Raiders"), "ARC Raiders: not added to the game list");
        }
        else Console.WriteLine("skip: ARC Raiders not installed");
        C<CheckBox>(form, "chkCandInstalled").Checked = false; Pump();
        int all = lv.Items.Count;
        Ok(all > 500, "all candidates: " + all);
        var cm = C<ComboBox>(form, "cbCandMethod");
        cm.SelectedIndex = Array.IndexOf((string[])Ty("Library").GetField("Methods").GetValue(null), "opengl") + 1; Pump();
        Ok(lv.Items.Count > 0 && lv.Items.Count < all && lv.Items.Cast<ListViewItem>().All(it => it.SubItems[1].Text == "Flato3D" || it.SubItems[2].Text == "OpenGL → OGL3D" || it.SubItems[3].Text.Contains("OpenGL → OGL3D")), "filter by method: OpenGL (" + lv.Items.Count + ")");
        cm.SelectedIndex = 0;
        var cp = C<ComboBox>(form, "cbCandProduct");
        cp.SelectedIndex = 2; Pump();   // Flato3D
        int nf = lv.Items.Count;
        Ok(nf > 100 && lv.Items.Cast<ListViewItem>().All(it => it.SubItems[1].Text == "Flato3D" && it.SubItems[2].Text.StartsWith("Flato3D:")), "filter by program: Flato3D (" + nf + ")");
        Shot(form, "ko_cands_flato");
        var rh = lv.Items.Cast<ListViewItem>().FirstOrDefault(it => it.Text == "Huntdown");
        Ok(rh != null && rh.SubItems[4].Text == "동작", "Huntdown: Flato3D, works (its profile)");
        cp.SelectedIndex = 1; Pump();   // Geo Gaming Hub
        Ok(lv.Items.Count > 300 && lv.Items.Cast<ListViewItem>().All(it => it.SubItems[1].Text == "Geo Gaming Hub"), "filter by program: Geo Gaming Hub (" + lv.Items.Count + ")");
        cp.SelectedIndex = 0;
        C<TextBox>(form, "tbCandSearch").Text = "broken age"; Pump();
        Ok(lv.Items.Count >= 1 && lv.Items[0].Text.StartsWith("Broken Age"), "search: Broken Age");
        Shot(form, "ko_cands_search");
        C<TextBox>(form, "tbCandSearch").Text = ""; C<CheckBox>(form, "chkCandInstalled").Checked = true; Pump();
        Shot(form, "ko_cands_installed");

        // a tested candidate starts the wizard with the tested method (Lara Croft GO: Unity exe, VK3D + DXVK tested)
        var rl = row("Lara Croft GO");
        if (rl != null)
        {
            rl.Selected = true; Pump();
            Call(form, "StartFromCandidate"); Pump();
            Eq(tabs.SelectedIndex, 2, "Lara: 3D method step opened");
            Ok(C<RadioButton>(form, "rbKindVk").Checked && C<CheckBox>(form, "chkVkDxvk").Checked, "Lara: tested method chosen (Vulkan + DXVK)");
            Shot(form, "ko_cands_lara_step1");
        }
        else Console.WriteLine("skip: Lara Croft GO not installed");
        Ok(TakeDialogs().Count == 0, "no message boxes");
        form.Close();
        Pump();
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
    }
}
