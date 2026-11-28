// Self-tests of Geo Gaming Hub's community fixes and the SBS-only install; part of HubTest (partial class).
//   HubTest.exe <GeoGamingHub.exe> <out dir> fixunit|fixgui
// fixunit: FixOps.Load on temp fix folders (bundled and imported) and on the repository's Packages\Fixes (Battlefront II)
// fixgui:  the wizard on copies of the DX12_3D test app: named starwarsbattlefrontii.exe (the bundled fix) it is the
//          geo-11 kind with the fix named in the detection line; installed "SBS only" it gets geo-11 0.6.198, the merged
//          fix and SRWeave's dxgi.dll with weave=0, and a second pass picks "SBS only" again; under another name it stays
//          the DirectX 12 kind. A copy of RFG with another tool's dxgi.dll keeps that dxgi.dll in an SBS-only install.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static partial class HubTest
{
    static void FixDir(string dir, string fixIni)
    {
        Directory.CreateDirectory(Path.Combine(dir, "Fix"));
        File.WriteAllText(Path.Combine(dir, "Fix", "d3dx.ini"), "[TextureOverrideTest]\r\nhash = 12345678\r\n");
        if (fixIni != null) File.WriteAllText(Path.Combine(dir, "fix.ini"), fixIni, Encoding.UTF8);
    }

    static void FixUnit(string app)
    {
        var t = Ty("FixOps");
        string user = Path.Combine(outDir, "fixes_user"), bundled = Path.Combine(outDir, "fixes_bundled");
        foreach (var d in new[] { user, bundled }) if (Directory.Exists(d)) Directory.Delete(d, true);
        Directory.CreateDirectory(user);
        t.GetField("Root").SetValue(null, user);
        t.GetField("BundledRoot").SetValue(null, bundled);
        Func<string, object> load = exe => S("FixOps", "Load", exe);
        Func<object, string, object> fld = (o, n) => o.GetType().GetField(n).GetValue(o);

        FixDir(Path.Combine(bundled, "game.exe"), "[Fix]\r\nname=Test fix\r\n");
        var fx = load(@"C:\Games\X\Game.exe");
        Ok(fx != null && (bool)fld(fx, "Bundled"), "bundled fix found for the exe (name case ignored)");
        Eq(fx == null ? null : fld(fx, "Name"), "Test fix", "bundled fix: name from its fix.ini");
        Ok(load(@"C:\Games\X\nofix.exe") == null, "no fix for an unknown exe");

        // an imported fix wins over the bundled one
        FixDir(Path.Combine(user, "game.exe"), "[Fix]\r\nname=Mine\r\nsource=x\r\ngeo11=\r\n");
        fx = load(@"C:\Games\X\game.exe");
        Ok(fx != null && !(bool)fld(fx, "Bundled"), "imported fix wins over the bundled one");
        Eq(fx == null ? null : fld(fx, "Name"), "Mine", "imported fix: its own name");

        // the repository's Battlefront II fix
        t.GetField("BundledRoot").SetValue(null, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(app)), "Packages", "Fixes"));
        fx = load(@"E:\Games\STAR WARS Battlefront II\starwarsbattlefrontii.exe");
        Ok(fx != null && (bool)fld(fx, "Bundled"), "Packages\\Fixes has the Battlefront II fix");
    }

    static void FixGui()
    {
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        string bfPkg = Path.Combine(exeDir, @"Packages\Fixes\starwarsbattlefrontii.exe");
        Ok(Directory.Exists(bfPkg), "test exe next to Packages\\Fixes: " + exeDir);
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
        string userFix = Path.Combine(exeDir, "fixes", "starwarsbattlefrontii.exe");
        Ok(!Directory.Exists(userFix), "no user fix settings for Battlefront II in the project (fixes\\ untouched)");
        File.WriteAllText(Path.Combine(exeDir, "settings.ini"), "lang=ko\r\n", Encoding.UTF8);
        string gamesDir = Path.Combine(outDir, "games");
        if (Directory.Exists(gamesDir)) Directory.Delete(gamesDir, true);
        string bf = Copy(Dx12App, @"BF\starwarsbattlefrontii.exe"), bfDir = Path.GetDirectoryName(bf);
        string other = Copy(Dx12App, @"Other\dx12game.exe");
        string rf = Copy(Rfg, @"Rfg\rfg.exe"), rfDir = Path.GetDirectoryName(rf);
        byte[] foreign = Encoding.ASCII.GetBytes("another tool's dxgi.dll (not SRWeave)");
        File.WriteAllBytes(Path.Combine(rfDir, "dxgi.dll"), foreign);

        new Thread(Watcher) { IsBackground = true }.Start();
        var form = (Form)Activator.CreateInstance(Ty("MainForm"));
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(40, 40);
        form.Show();
        Pump();
        var tabs = C<TabControl>(form, "tabs");
        Func<string, RadioButton> rb = n => C<RadioButton>(form, n);
        Func<string, Label> lbl = n => C<Label>(form, n);

        // a DX12-only exe with no fix: the DirectX 12 kind
        Call(form, "StartWizard", other);
        Pump();
        Ok(rb("rbKindDx12").Checked && !rb("rbKindGeo").Checked, "no fix: DirectX 12 kind");
        Ok(lbl("lblKindDetect").Text.Contains("DX12_3D") && !lbl("lblKindDetect").Text.Contains("커뮤니티 픽스"), "no fix: detection line names DX12_3D: " + lbl("lblKindDetect").Text);

        // the same exe named starwarsbattlefrontii.exe: the bundled fix makes it geo-11
        Call(form, "StartWizard", bf);
        Pump();
        Ok(rb("rbKindGeo").Checked && !rb("rbKindDx12").Checked && !rb("rbKindTri").Checked, "bundled fix: geo-11 kind instead of DirectX 12");
        Ok(lbl("lblKindDetect").Text.Contains("허브에 이 게임의 커뮤니티 픽스 있음") && lbl("lblKindDetect").Text.Contains("geo-11 + 픽스"), "bundled fix: detection line names the fix: " + lbl("lblKindDetect").Text);

        // SBS only: geo-11 + the fix + SRWeave's dxgi.dll with weave=0
        rb("rbSbsOnly").Checked = true; Pump();
        tabs.SelectedIndex = 5;
        Call(form, "Apply");
        Pump();
        var dl = TakeDialogs();
        Ok(dl.Count == 1 && dl[0].Contains("좌우(SBS) 화면") && !dl[0].Contains("SR 패널"), "SBS only: one info box about SBS (" + string.Join(" || ", dl) + ")");
        string sr = Path.Combine(bfDir, "SRWeave.ini"), dx = Path.Combine(bfDir, "dxgi.dll");
        Ok(File.Exists(dx) && (bool)S("Util", "IsSrWeave", dx, Path.Combine(exeDir, @"Packages\Geo-11_DX11\x64\dxgi.dll")), "SBS only: SRWeave dxgi.dll installed");
        Eq(File.Exists(sr) ? (string)S("Ini", "Get", sr, "weave") : null, "0", "SBS only: SRWeave.ini weave=0");
        Eq((string)S("Ini", "Get", Path.Combine(bfDir, "d3dxdm.ini"), "direct_mode"), "sbs", "SBS only: direct_mode sbs");
        Ok(File.ReadAllText(Path.Combine(bfDir, "d3dx.ini")).Contains("Frostbite3_DHR.ini") && File.Exists(Path.Combine(bfDir, @"ShaderFixes\Frostbite3_DHR.ini")), "SBS only: the bundled fix merged");
        string d0198 = Directory.GetFiles(bfPkg, "d3d11.dll", SearchOption.AllDirectories).First(f => f.Contains("0.6.198"));
        Ok(File.ReadAllBytes(Path.Combine(bfDir, "d3d11.dll")).SequenceEqual(File.ReadAllBytes(d0198)), "SBS only: the fix's geo-11 0.6.198");
        var row = Row(form, bf);
        if (row != null) { Console.WriteLine("  row: " + string.Join(" | ", row)); Eq(row[5], "끔", "list: SR off"); }
        else Ok(false, "game listed");

        // a second pass over the same game: SBS only again (not 'add SR weaving only')
        Call(form, "StartWizard", bf);
        Pump();
        Ok(rb("rbSbsOnly").Checked && !rb("rbSrOnly").Checked && !C<CheckBox>(form, "chkSr").Checked, "existing SBS install: SBS only picked again");
        Ok(lbl("lblDetect").Text.Contains("기존 SBS 설치"), "existing SBS install: noted: " + lbl("lblDetect").Text);

        // another tool's dxgi.dll stays in an SBS-only install
        Call(form, "StartWizard", rf);
        Pump();
        rb("rbSbsOnly").Checked = true; Pump();
        tabs.SelectedIndex = 5;
        Call(form, "Apply");
        Pump();
        TakeDialogs();
        Ok(File.ReadAllBytes(Path.Combine(rfDir, "dxgi.dll")).SequenceEqual(foreign) && !File.Exists(Path.Combine(rfDir, "SRWeave.ini")), "SBS only with another dxgi.dll: kept, no SRWeave");
        Ok(File.Exists(Path.Combine(rfDir, "d3d11.dll")), "SBS only with another dxgi.dll: geo-11 installed");

        // English
        Ty("Lang").GetField("English").SetValue(null, true);
        Call(form, "RebuildUi");
        Pump();
        Call(form, "StartWizard", other);
        Pump();
        Call(form, "StartWizard", bf);
        Pump();
        Ok(lbl("lblKindDetect").Text.Contains("The hub has a community fix for this game") && lbl("lblKindDetect").Text.Contains("geo-11 + the fix"), "English detection line: " + lbl("lblKindDetect").Text);
        Ok(lbl("lblDetect").Text.Contains("existing SBS install"), "English: existing SBS install noted");
        Ty("Lang").GetField("English").SetValue(null, false);
        form.Close();
        Pump();
        foreach (var n in new[] { "games.list", "settings.ini" }) File.Delete(Path.Combine(exeDir, n));
        string fixesDir = Path.Combine(exeDir, "fixes");
        if (Directory.Exists(userFix)) Directory.Delete(userFix, true);
        if (Directory.Exists(fixesDir) && Directory.GetFileSystemEntries(fixesDir).Length == 0) Directory.Delete(fixesDir);
    }
}
