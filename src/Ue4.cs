// Unreal Engine 4 Universal Fix 2: the parts of its console config tool (00_UE4-UniversalFix-2_Config.cmd /
// z_ConfigToolMain.cmd) that change files in this package version, done natively so no console window is needed.
//   - AA/AO improvements: append Scalability(_AA|_NOAA).ini and Engine_additions(_AA|_NOAA).txt to the game's
//     %LOCALAPPDATA%\<Project>\Saved\Config\WindowsNoEditor\{Scalability,Engine}.ini, back them up, make them read-only
//     (z_AAAOMS_Fix_ForConfigTool.cmd); "In-game AA on/off" is the _AA / _NOAA variant (z_TurnOfAA.cmd).
//   - Force fullscreen: d3dx.ini full_screen 0/1 (FullScreenHandle).
//   - VSync: the NVIDIA profile token on the "Vertical Sync = 0x..." line of d3dx.ini (ChangeVSYNCOverride).
//   - On-screen display: d3dx.ini "y20 = 1.0" / "y20 = 0.0" (OSDSection).
//   - HUD profile: ShaderFixes\AUTOHUDUICONFIGS\<exe> -> ShaderFixes\AutoDepthUIHUD.ini (z_HUDUIProfileInstaller.cmd).
// Not reproduced (their files are missing in this package, so the tool itself cannot do them either): aim-key
// convergence scale, HUD-fix disable / shader hunting, "reset to default". The original tool stays reachable.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GeoGamingHub
{
    static class Ue4Ops
    {
        static readonly Encoding Enc = Encoding.Default;

        // VSync tokens the UF2 tool writes into the NVIDIA profile line
        public static readonly string[] VsyncTokens = { "0x60925292", "0x47814940", "0x08416747", "0x18888888", "0x32610244", "0x71271021", "0x13245256" };
        public static readonly string[] VsyncNamesKo = { "게임 설정 따름 (기본)", "항상 켬", "항상 끔", "Fast Sync", "1/2 (절반 주사율)", "1/3", "1/4" };

        // %LOCALAPPDATA%\<Project>\Saved\Config\{WindowsNoEditor|WindowsClient|Windows}
        public static string ConfigFolder(string exe)
        {
            string project = Util.Ue4ProjectName(exe);
            if (project == null) return null;
            string cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), project, "Saved", "Config");
            foreach (var v in new[] { "WindowsNoEditor", "WindowsClient", "Windows" })
            {
                string d = Path.Combine(cfg, v);
                if (Directory.Exists(d)) return d;
            }
            return null;
        }

        public static bool AaAoInstalled(string exe)
        {
            string f = ConfigFolder(exe);
            return f != null && (File.Exists(Path.Combine(f, "Scalability_backup.ini")) || File.Exists(Path.Combine(f, "Engine_backup.ini")));
        }

        // Which variant is in place. The package's "_AA" set leaves the game's own AA alone (its AA lines are commented
        // out; the tool calls this "ENABLE AA in game"); the "_NOAA" set has them active (r.DefaultFeature.AntiAliasing=2,
        // r.TemporalAASamples=2, r.PostProcessAAQuality=..; the tool's "DISABLE AA in game completely" for transparency
        // issues). true = forced (_NOAA), false = game default (_AA), null = not installed / unknown.
        public static bool? AaForced(string exe)
        {
            string f = ConfigFolder(exe);
            if (f == null) return null;
            string eng = Path.Combine(f, "Engine.ini");
            if (!File.Exists(eng)) return null;
            bool ours = false;
            foreach (var raw in File.ReadAllLines(eng, Enc))
            {
                string l = raw.Trim();
                if (l.StartsWith("r.DefaultFeature.AntiAliasing=", StringComparison.OrdinalIgnoreCase)) return true;
                if (l.IndexOf("r.DefaultFeature.MotionBlur", StringComparison.OrdinalIgnoreCase) >= 0) ours = true;
            }
            return ours ? (bool?)false : null;
        }

        static void ClearReadOnly(string file)
        {
            if (File.Exists(file)) File.SetAttributes(file, File.GetAttributes(file) & ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System));
        }

        static IEnumerable<string> ActiveLines(string file)
        {
            return File.ReadAllLines(file, Enc).Where(l => l.Trim().Length > 0);
        }

        // z_AAAOMS_Fix_ForConfigTool.cmd: back up, append, set read-only. Returns a log line.
        public static string InstallAaAo(string gameDir, string exe, bool forceAa)
        {
            string f = ConfigFolder(exe);
            if (f == null) return Lang.T("  AA/AO: 게임 설정 폴더가 아직 없습니다 (게임을 한 번 실행한 뒤 다시 하세요).");
            string scalSrc = Path.Combine(gameDir, forceAa ? "Scalability_NOAA.ini" : "Scalability_AA.ini");
            string engSrc = Path.Combine(gameDir, forceAa ? "Engine_additions_NOAA.txt" : "Engine_additions_AA.txt");
            if (!File.Exists(scalSrc)) scalSrc = Path.Combine(gameDir, "Scalability.ini");
            if (!File.Exists(engSrc)) engSrc = Path.Combine(gameDir, "Engine_additions.txt");
            if (!File.Exists(scalSrc) || !File.Exists(engSrc)) return Lang.T("  AA/AO: 패키지에 Scalability.ini / Engine_additions.txt가 없습니다.");
            if (AaAoInstalled(exe)) RemoveAaAo(gameDir, exe);   // switch variant: restore originals first

            string scal = Path.Combine(f, "Scalability.ini"), eng = Path.Combine(f, "Engine.ini");
            if (File.Exists(scal)) File.Copy(scal, Path.Combine(f, "Scalability_backup.ini"), true);
            if (File.Exists(eng)) File.Copy(eng, Path.Combine(f, "Engine_backup.ini"), true);
            ClearReadOnly(scal); ClearReadOnly(eng);
            File.AppendAllText(scal, (File.Exists(scal) && !File.ReadAllText(scal, Enc).EndsWith("\n") ? Environment.NewLine : "") + string.Join(Environment.NewLine, ActiveLines(scalSrc)) + Environment.NewLine, Enc);
            File.AppendAllText(eng, (File.Exists(eng) && !File.ReadAllText(eng, Enc).EndsWith("\n") ? Environment.NewLine : "") + string.Join(Environment.NewLine, ActiveLines(engSrc)) + Environment.NewLine, Enc);
            File.SetAttributes(scal, File.GetAttributes(scal) | FileAttributes.ReadOnly);
            File.SetAttributes(eng, File.GetAttributes(eng) | FileAttributes.ReadOnly);
            File.WriteAllText(Path.Combine(gameDir, "z_AAAAOInstallState"), Util.Ue4ProjectName(exe) + Environment.NewLine, Enc);
            return Lang.T("  AA/AO 개선 설치 (") + (forceAa ? Lang.T("AA 강제 지정") : Lang.T("게임 AA 설정 그대로")) + "): " + f;
        }

        public static string RemoveAaAo(string gameDir, string exe)
        {
            string f = ConfigFolder(exe);
            if (f == null) return Lang.T("  AA/AO: 게임 설정 폴더가 없습니다.");
            foreach (var n in new[] { "Scalability", "Engine" })
            {
                string file = Path.Combine(f, n + ".ini"), bak = Path.Combine(f, n + "_backup.ini");
                ClearReadOnly(file);
                if (File.Exists(bak)) { File.Copy(bak, file, true); File.Delete(bak); }
            }
            string st = Path.Combine(gameDir, "z_AAAAOInstallState");
            if (File.Exists(st)) File.Delete(st);
            return Lang.T("  AA/AO 개선 제거 (원래 Engine.ini / Scalability.ini 복원): ") + f;
        }

        public static bool? ForceFullscreen(string gameDir)
        {
            string v = Ini.Get(Path.Combine(gameDir, "d3dx.ini"), "full_screen");
            if (v == null) return null;
            return v.Trim() != "0";
        }
        public static void SetForceFullscreen(string gameDir, bool on)
        {
            Ini.Set(Path.Combine(gameDir, "d3dx.ini"), "full_screen", on ? "1" : "0", "Device");
        }

        public static int VsyncIndex(string gameDir)
        {
            string v = Ini.Get(Path.Combine(gameDir, "d3dx.ini"), "Vertical Sync");
            if (v == null) return -1;
            int i = Array.FindIndex(VsyncTokens, t => string.Equals(t, v.Trim(), StringComparison.OrdinalIgnoreCase));
            return i;
        }
        public static void SetVsync(string gameDir, int index)
        {
            string dx = Path.Combine(gameDir, "d3dx.ini");
            if (index < 0 || index >= VsyncTokens.Length || Ini.Get(dx, "Vertical Sync") == null) return;
            Ini.Set(dx, "Vertical Sync", VsyncTokens[index], "Profile");
        }

        // d3dx.ini "y20 = 1.0" (OSD on) / "y20 = 0.0" (off); null when the line is not there
        public static bool? Osd(string gameDir)
        {
            string v = Ini.Get(Path.Combine(gameDir, "d3dx.ini"), "y20");
            if (v == null) return null;
            return v.Trim() != "0.0" && v.Trim() != "0";
        }
        public static void SetOsd(string gameDir, bool on)
        {
            string dx = Path.Combine(gameDir, "d3dx.ini");
            if (Ini.Get(dx, "y20") != null) Ini.Set(dx, "y20", on ? "1.0" : "0.0", "Constants");
        }

        public static string[] HudProfiles(string gameDir)
        {
            string d = Path.Combine(gameDir, "ShaderFixes", "AUTOHUDUICONFIGS");
            if (!Directory.Exists(d)) return new string[0];
            return Directory.GetFiles(d).Select(Path.GetFileName).OrderBy(n => n == "DEFAULT" ? "" : n, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public static string DefaultHudProfile(string gameDir, string exe)
        {
            string exeKey = Path.GetFileName(exe).Split('.', '-')[0];
            string list = Path.Combine(gameDir, "ShaderFixes", "z_GameProfileList");
            if (File.Exists(list) && File.ReadAllText(list, Enc).IndexOf(exeKey, StringComparison.OrdinalIgnoreCase) >= 0 && File.Exists(Path.Combine(gameDir, "ShaderFixes", "AUTOHUDUICONFIGS", exeKey))) return exeKey;
            return "DEFAULT";
        }
        public static string CurrentHudProfile(string gameDir)
        {
            string cur = Path.Combine(gameDir, "ShaderFixes", "AutoDepthUIHUD.ini");
            if (!File.Exists(cur)) return null;
            foreach (var p in HudProfiles(gameDir))
                if (Util.SameFile(cur, Path.Combine(gameDir, "ShaderFixes", "AUTOHUDUICONFIGS", p))) return p;
            return null;
        }
        public static void SetHudProfile(string gameDir, string profile)
        {
            string src = Path.Combine(gameDir, "ShaderFixes", "AUTOHUDUICONFIGS", profile);
            if (File.Exists(src)) File.Copy(src, Path.Combine(gameDir, "ShaderFixes", "AutoDepthUIHUD.ini"), true);
        }
    }

    // The "UE4 settings" window: shows the current state of the game folder and applies changes on [적용].
    sealed class Ue4Dialog : Form
    {
        readonly string gameDir, exe;
        readonly Action<string> log;
        readonly float dpi;
        CheckBox chkAaAo; RadioButton rbAaOn, rbAaOff; Label lblAaState;
        CheckBox chkFullscreen, chkOsd; ComboBox cbVsync, cbHud;

        int S(int v) { return (int)Math.Round(v * dpi); }
        Padding P(int l, int t, int r, int b) { return new Padding(S(l), S(t), S(r), S(b)); }

        public Ue4Dialog(string gameDir, string exe, Action<string> log, float dpi)
        {
            this.gameDir = gameDir; this.exe = exe; this.log = log; this.dpi = dpi;
            Text = Lang.T("UE4 설정 (Universal Fix 2)") + " - " + Path.GetFileName(exe);
            Font = new Font("Malgun Gothic", 10f);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(S(640), S(680));
            AutoScaleMode = AutoScaleMode.None;

            // buttons in a fixed bar at the bottom; the settings scroll above it
            var bar = new Panel { Dock = DockStyle.Bottom, Height = S(58), BackColor = Color.FromArgb(248, 248, 248) };
            bar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(Color.FromArgb(225, 225, 225)), 0, 0, bar.Width, 0);
            var ok = new Button { Text = Lang.T("적용"), AutoSize = true, MinimumSize = new Size(S(110), S(36)), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 212), ForeColor = Color.White, Font = new Font(Font, FontStyle.Bold) };
            ok.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 212);
            var cancel = new Button { Text = Lang.T("닫기"), AutoSize = true, MinimumSize = new Size(S(90), S(36)), FlatStyle = FlatStyle.Flat, BackColor = Color.White, DialogResult = DialogResult.Cancel };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            ok.Click += (s, e) => { if (Apply()) DialogResult = DialogResult.OK; };
            bar.Controls.Add(ok); bar.Controls.Add(cancel);
            bar.Resize += (s, e) => { cancel.Location = new Point(bar.Width - cancel.Width - S(18), (bar.Height - cancel.Height) / 2); ok.Location = new Point(cancel.Left - ok.Width - S(8), (bar.Height - ok.Height) / 2); };
            Controls.Add(bar);
            CancelButton = cancel;

            var col = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = P(18, 12, 18, 8) };
            Controls.Add(col);
            col.BringToFront();
            Color accent = Color.FromArgb(0, 90, 170), muted = Color.FromArgb(110, 110, 110);
            Func<string, Label> head = t => new Label { Text = t, AutoSize = true, ForeColor = accent, Font = new Font("Malgun Gothic", 10.5f, FontStyle.Bold), Margin = P(0, 10, 0, 4) };
            Func<string, Label> note = t => new Label { Text = t, AutoSize = true, ForeColor = muted, Margin = P(0, 2, 0, 6), MaximumSize = new Size(S(590), 0) };

            // AA/AO
            col.Controls.Add(head(Lang.T("AA/AO 개선 (Engine.ini · Scalability.ini)")));
            bool installed = Ue4Ops.AaAoInstalled(exe);
            bool? forced = Ue4Ops.AaForced(exe);
            string cfg = Ue4Ops.ConfigFolder(exe);
            chkAaAo = new CheckBox { Text = Lang.T("AA/AO 개선 설치"), AutoSize = true, Checked = installed, Margin = P(0, 2, 0, 0) };
            col.Controls.Add(chkAaAo);
            col.Controls.Add(note(Lang.T("모션 블러·비네트·렌즈 플레어 끄기, 그림자·AO 품질 고정. 원래 Engine.ini / Scalability.ini는 *_backup.ini로 보관되고 읽기 전용이 됩니다. 체크를 끄고 적용하면 원래 파일로 되돌립니다.")));
            var aaCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = P(24, 0, 0, 0) };
            rbAaOn = new RadioButton { Text = Lang.T("게임 AA 설정 그대로 (기본)"), AutoSize = true, Checked = forced != true, Margin = P(0, 2, 0, 2) };
            rbAaOff = new RadioButton { Text = Lang.T("AA 강제 지정 (반투명 물체가 깨질 때)"), AutoSize = true, Checked = forced == true, Margin = P(0, 2, 0, 2) };
            aaCol.Controls.Add(rbAaOn); aaCol.Controls.Add(rbAaOff);
            col.Controls.Add(aaCol);
            col.Controls.Add(note(Lang.T("원래 도구의 \"ENABLE AA in Game\" / \"DISABLE AA in Game\"에 해당합니다. 강제 지정은 TAA 2샘플·AA 품질 고정 값을 넣습니다.")));
            lblAaState = note(cfg == null
                ? Lang.T("게임 설정 폴더가 아직 없습니다. 게임을 한 번 실행한 뒤 다시 여세요.")
                : (installed ? Lang.T("현재: 설치됨 - ") : Lang.T("현재: 설치 안 됨 - ")) + cfg);
            col.Controls.Add(lblAaState);

            // display
            col.Controls.Add(head(Lang.T("화면")));
            bool? fs = Ue4Ops.ForceFullscreen(gameDir);
            chkFullscreen = new CheckBox { Text = Lang.T("강제 전체화면"), AutoSize = true, Checked = fs == true, Enabled = fs != null, Margin = P(0, 2, 0, 0) };
            col.Controls.Add(chkFullscreen);
            col.Controls.Add(note(Lang.T("geo-11이 전체화면 스왑체인을 만듭니다. 창 모드에서 문제가 있을 때만 켜세요.")));
            bool? osd = Ue4Ops.Osd(gameDir);
            chkOsd = new CheckBox { Text = Lang.T("화면 표시(OSD) - 픽스 상태를 게임 화면에 표시"), AutoSize = true, Checked = osd == true, Enabled = osd != null, Margin = P(0, 2, 0, 2) };
            col.Controls.Add(chkOsd);
            var vsRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = P(0, 4, 0, 0) };
            vsRow.Controls.Add(new Label { Text = Lang.T("VSync (NVIDIA 프로필):"), AutoSize = true, Margin = P(0, 7, 10, 0) });
            cbVsync = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(260), FlatStyle = FlatStyle.System, Margin = P(0, 3, 0, 3) };
            cbVsync.Items.AddRange(Ue4Ops.VsyncNamesKo.Select(n => (object)Lang.T(n)).ToArray());
            int vi = Ue4Ops.VsyncIndex(gameDir);
            cbVsync.Enabled = vi >= 0; cbVsync.SelectedIndex = vi >= 0 ? vi : 0;
            vsRow.Controls.Add(cbVsync);
            col.Controls.Add(vsRow);
            col.Controls.Add(note(Lang.T("SR 패널에서는 게임 설정 따름(기본)을 권합니다. 끊김이 심할 때만 1/2 등을 시험하세요.")));

            // HUD profile
            col.Controls.Add(head(Lang.T("HUD 프로필 (AutoDepthUIHUD.ini)")));
            var hudRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            hudRow.Controls.Add(new Label { Text = Lang.T("프로필:"), AutoSize = true, Margin = P(0, 7, 10, 0) });
            cbHud = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(300), FlatStyle = FlatStyle.System, Margin = P(0, 3, 0, 3) };
            var profiles = Ue4Ops.HudProfiles(gameDir);
            cbHud.Items.AddRange(profiles.Cast<object>().ToArray());
            string cur = Ue4Ops.CurrentHudProfile(gameDir) ?? Ue4Ops.DefaultHudProfile(gameDir, exe);
            cbHud.SelectedIndex = Math.Max(0, Array.IndexOf(profiles, cur));
            cbHud.Enabled = profiles.Length > 0;
            hudRow.Controls.Add(cbHud);
            col.Controls.Add(hudRow);
            col.Controls.Add(note(Lang.T("게임별 HUD 깊이 설정입니다. 실행 파일 이름과 같은 프로필이 있으면 그것이 기본이고, 없으면 DEFAULT입니다. (") + Lang.T("자동 선택: ") + Ue4Ops.DefaultHudProfile(gameDir, exe) + ")"));

            // other
            col.Controls.Add(head(Lang.T("그 밖의 항목")));
            col.Controls.Add(note(Lang.T("조준 키 수렴 배율, HUD 픽스 끄기/셰이더 헌팅, 자동 수렴/자동 깊이 모드는 이 패키지(Win11판)에 해당 파일이 없어 원래 도구에서도 동작하지 않습니다. 필요하면 아래에서 원래 도구(명령창)를 열 수 있습니다.")));
            var link = new LinkLabel { Text = Lang.T("원래 설정 도구(명령창) 열기"), AutoSize = true, LinkColor = Color.FromArgb(0, 120, 212), Margin = P(0, 0, 0, 8) };
            link.LinkClicked += (s, e) =>
            {
                string tool = Path.Combine(gameDir, "00_UE4-UniversalFix-2_Config.cmd");
                if (File.Exists(tool)) { try { Process.Start(new ProcessStartInfo(tool) { UseShellExecute = true, WorkingDirectory = gameDir }); } catch { } }
            };
            col.Controls.Add(link);
        }

        bool Apply()
        {
            try
            {
                log(Lang.T("UE4 설정 적용: ") + gameDir);
                bool installed = Ue4Ops.AaAoInstalled(exe);
                bool? forced = Ue4Ops.AaForced(exe);
                if (chkAaAo.Checked)
                {
                    if (!installed || forced != rbAaOff.Checked) log(Ue4Ops.InstallAaAo(gameDir, exe, rbAaOff.Checked));
                }
                else if (installed) log(Ue4Ops.RemoveAaAo(gameDir, exe));

                if (chkFullscreen.Enabled && Ue4Ops.ForceFullscreen(gameDir) != chkFullscreen.Checked)
                { Ue4Ops.SetForceFullscreen(gameDir, chkFullscreen.Checked); log("  d3dx.ini full_screen=" + (chkFullscreen.Checked ? 1 : 0)); }
                if (chkOsd.Enabled && Ue4Ops.Osd(gameDir) != chkOsd.Checked)
                { Ue4Ops.SetOsd(gameDir, chkOsd.Checked); log("  d3dx.ini y20 (OSD) = " + (chkOsd.Checked ? "1.0" : "0.0")); }
                if (cbVsync.Enabled && Ue4Ops.VsyncIndex(gameDir) != cbVsync.SelectedIndex)
                { Ue4Ops.SetVsync(gameDir, cbVsync.SelectedIndex); log("  d3dx.ini Vertical Sync = " + Ue4Ops.VsyncTokens[cbVsync.SelectedIndex] + " (" + cbVsync.SelectedItem + ")"); }
                if (cbHud.Enabled && cbHud.SelectedItem != null && Ue4Ops.CurrentHudProfile(gameDir) != (string)cbHud.SelectedItem)
                { Ue4Ops.SetHudProfile(gameDir, (string)cbHud.SelectedItem); log(Lang.T("  HUD 프로필: ") + cbHud.SelectedItem); }
                log(Lang.T("UE4 설정 적용 완료."));
                return true;
            }
            catch (Exception ex)
            {
                log(Lang.T("오류: ") + ex.Message);
                MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
