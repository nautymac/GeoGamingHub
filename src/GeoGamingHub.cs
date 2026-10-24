// Geo Gaming Hub - 게임을 지오메트리 입체 3D로: geo-11(Unity / 일반 DX11 / UE4 / DX9 + dgVoodoo2)과 자체 래퍼(OpenGL = OGL3D,
// Vulkan = VK3D)를 게임에 설치하고, 탭별 선택에 따라 설정 파일을 고친다. geo-11 SR 설치기에서 갈라져 나옴.
// 단일 WinForms exe (.NET Framework 4.x). 패키지 폴더는 exe 폴더 옆에 있어야 한다:
//   ..\Geo-11_Unity\{x64,x32}
//   ..\Geo-11_DX11\{x64,x32}
// 고치는 파일: d3dxdm.ini (direct_mode, dm_separation, dm_convergence, dm_auto_convergence),
//   ShaderFixesDM\hotkeys.ini (Key = ...), d3dx.ini (hunting, toggle_hunting, reload_config, reload_fixes,
//   show_original; Unity include 줄은 UnitySwitch.ps1), SRWeave.ini (weave, swap_eyes, lens, key_*).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("Geo Gaming Hub")]
[assembly: AssemblyProduct("Geo Gaming Hub")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace GeoGamingHub
{
    sealed class KeyDef
    {
        public string Id, Label, Def;
        public KeyDef(string id, string label, string def) { Id = id; Label = label; Def = def; }
    }

    // A key combination as the user sees it; converted to/from the ini spellings by KeyText.
    sealed class KeyCombo
    {
        public bool Ctrl, Alt, Shift;
        public Keys Key = Keys.None;
        public bool IsEmpty { get { return Key == Keys.None; } }
    }

    static class KeyText
    {
        // Keys enum <-> Windows VK_ names (3DMigoto / geo-11 ini spelling)
        static readonly Dictionary<Keys, string> vkNames = new Dictionary<Keys, string> {
            { Keys.Space, "SPACE" }, { Keys.Return, "RETURN" }, { Keys.Tab, "TAB" }, { Keys.Escape, "ESCAPE" }, { Keys.Back, "BACK" },
            { Keys.Insert, "INSERT" }, { Keys.Delete, "DELETE" }, { Keys.Home, "HOME" }, { Keys.End, "END" }, { Keys.PageUp, "PRIOR" }, { Keys.PageDown, "NEXT" },
            { Keys.Left, "LEFT" }, { Keys.Up, "UP" }, { Keys.Right, "RIGHT" }, { Keys.Down, "DOWN" },
            { Keys.Multiply, "MULTIPLY" }, { Keys.Add, "ADD" }, { Keys.Subtract, "SUBTRACT" }, { Keys.Decimal, "DECIMAL" }, { Keys.Divide, "DIVIDE" },
            { Keys.Pause, "PAUSE" }, { Keys.Scroll, "SCROLL" }, { Keys.PrintScreen, "SNAPSHOT" }, { Keys.CapsLock, "CAPITAL" }, { Keys.NumLock, "NUMLOCK" },
            { Keys.OemMinus, "OEM_MINUS" }, { Keys.Oemplus, "OEM_PLUS" }, { Keys.OemPeriod, "OEM_PERIOD" }, { Keys.Oemcomma, "OEM_COMMA" },
            { Keys.Oem1, "OEM_1" }, { Keys.Oem2, "OEM_2" }, { Keys.Oem3, "OEM_3" }, { Keys.Oem4, "OEM_4" }, { Keys.Oem5, "OEM_5" }, { Keys.Oem6, "OEM_6" }, { Keys.Oem7, "OEM_7" },
        };
        static readonly Dictionary<string, Keys> vkKeys = BuildReverse();
        static Dictionary<string, Keys> BuildReverse()
        {
            var d = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in vkNames) d[kv.Value] = kv.Key;
            d["ENTER"] = Keys.Return; d["ESC"] = Keys.Escape; d["PAGEUP"] = Keys.PageUp; d["PAGEDOWN"] = Keys.PageDown; d["BACKSPACE"] = Keys.Back;
            return d;
        }
        // what the user sees for a key
        static readonly Dictionary<Keys, string> friendly = new Dictionary<Keys, string> {
            { Keys.Space, "Space" }, { Keys.Return, "Enter" }, { Keys.Tab, "Tab" }, { Keys.Escape, "Esc" }, { Keys.Back, "Backspace" },
            { Keys.Insert, "Insert" }, { Keys.Delete, "Delete" }, { Keys.Home, "Home" }, { Keys.End, "End" }, { Keys.PageUp, "Page Up" }, { Keys.PageDown, "Page Down" },
            { Keys.Left, "←" }, { Keys.Up, "↑" }, { Keys.Right, "→" }, { Keys.Down, "↓" },
            { Keys.Multiply, "숫자패드 *" }, { Keys.Add, "숫자패드 +" }, { Keys.Subtract, "숫자패드 -" }, { Keys.Decimal, "숫자패드 ." }, { Keys.Divide, "숫자패드 /" },
            { Keys.Pause, "Pause" }, { Keys.Scroll, "Scroll Lock" }, { Keys.PrintScreen, "Print Screen" }, { Keys.CapsLock, "Caps Lock" }, { Keys.NumLock, "Num Lock" },
            { Keys.OemMinus, "-" }, { Keys.Oemplus, "=" }, { Keys.OemPeriod, "." }, { Keys.Oemcomma, "," },
            { Keys.Oem1, ";" }, { Keys.Oem2, "/" }, { Keys.Oem3, "`" }, { Keys.Oem4, "[" }, { Keys.Oem5, "\\" }, { Keys.Oem6, "]" }, { Keys.Oem7, "'" },
        };

        static bool IsLetterOrDigit(Keys k) { return (k >= Keys.A && k <= Keys.Z) || (k >= Keys.D0 && k <= Keys.D9); }
        static bool IsFKey(Keys k) { return k >= Keys.F1 && k <= Keys.F24; }
        static bool IsNumPad(Keys k) { return k >= Keys.NumPad0 && k <= Keys.NumPad9; }

        public static string KeyName(Keys k)
        {
            if (k == Keys.None) return "";
            if (IsLetterOrDigit(k)) return ((char)(k >= Keys.A ? 'A' + (k - Keys.A) : '0' + (k - Keys.D0))).ToString();
            if (IsFKey(k)) return "F" + (1 + (k - Keys.F1));
            if (IsNumPad(k)) return Lang.T("숫자패드 ") + (k - Keys.NumPad0);
            string s; if (friendly.TryGetValue(k, out s)) return Lang.T(s);
            return k.ToString();
        }

        public static string Friendly(KeyCombo c, bool fixedCtrlAlt)
        {
            if (c == null || c.IsEmpty) return "";
            var parts = new List<string>();
            if (fixedCtrlAlt) { parts.Add("Ctrl"); parts.Add("Alt"); }
            else { if (c.Ctrl) parts.Add("Ctrl"); if (c.Alt) parts.Add("Alt"); if (c.Shift) parts.Add("Shift"); }
            parts.Add(KeyName(c.Key));
            return string.Join(" + ", parts);
        }

        // VK_ spelling of one key (letters/digits as themselves, like 3DMigoto's own examples)
        static string VkToken(Keys k)
        {
            if (IsLetterOrDigit(k)) return ((char)(k >= Keys.A ? 'A' + (k - Keys.A) : '0' + (k - Keys.D0))).ToString();
            if (IsFKey(k)) return "VK_F" + (1 + (k - Keys.F1));
            if (IsNumPad(k)) return "VK_NUMPAD" + (k - Keys.NumPad0);
            string s; if (vkNames.TryGetValue(k, out s)) return "VK_" + s;
            return "VK_" + k.ToString().ToUpperInvariant();   // Keys enum name as a last resort
        }

        static Keys FromToken(string t)
        {
            if (string.IsNullOrEmpty(t)) return Keys.None;
            string u = t.ToUpperInvariant();
            if (u.StartsWith("VK_")) u = u.Substring(3);
            if (u.Length == 1 && ((u[0] >= 'A' && u[0] <= 'Z') || (u[0] >= '0' && u[0] <= '9'))) return u[0] >= 'A' ? Keys.A + (u[0] - 'A') : Keys.D0 + (u[0] - '0');
            var m = Regex.Match(u, @"^F(\d{1,2})$"); if (m.Success) { int n = int.Parse(m.Groups[1].Value); if (n >= 1 && n <= 24) return Keys.F1 + (n - 1); }
            m = Regex.Match(u, @"^NUMPAD(\d)$"); if (m.Success) return Keys.NumPad0 + (u[6] - '0');
            Keys k; if (vkKeys.TryGetValue(u, out k)) return k;
            if (u.StartsWith("0X")) { int v; if (int.TryParse(u.Substring(2), NumberStyles.HexNumber, null, out v) && v > 0 && v < 256) return (Keys)v; }
            Keys parsed; if (Enum.TryParse<Keys>(t, true, out parsed) && parsed != Keys.None) return parsed;
            return Keys.None;
        }

        // ---- 3DMigoto / geo-11 ini: "ctrl F6", "no_modifiers NO_VK_DECIMAL VK_NUMPAD0" ----
        public static string ToMigoto(KeyCombo c)
        {
            if (c == null || c.IsEmpty) return "";
            var parts = new List<string>();
            if (c.Ctrl) parts.Add("ctrl");
            if (c.Alt) parts.Add("alt");
            if (c.Shift) parts.Add("shift");
            if (parts.Count == 0) parts.Add("no_modifiers");
            if (c.Key == Keys.NumPad0) parts.Add("NO_VK_DECIMAL");   // keep 3DMigoto's default guard against the numpad '.' key
            parts.Add(VkToken(c.Key));
            return string.Join(" ", parts);
        }

        public static KeyCombo FromMigoto(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var c = new KeyCombo();
            foreach (var tok in s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string u = tok.ToUpperInvariant();
                if (u == "CTRL") c.Ctrl = true;
                else if (u == "ALT") c.Alt = true;
                else if (u == "SHIFT") c.Shift = true;
                else if (u.StartsWith("NO_")) continue;   // no_modifiers, no_ctrl, NO_VK_DECIMAL ...
                else { var k = FromToken(tok); if (k != Keys.None) c.Key = k; }
            }
            return c.IsEmpty ? null : c;
        }

        // ---- SRWeave.ini: key only (Ctrl+Alt is fixed): "W", "F6", "NUMPAD0", "VK_SPACE", "0x..", "none" ----
        public static string ToSrWeave(KeyCombo c)
        {
            if (c == null || c.IsEmpty) return "none";
            Keys k = c.Key;
            if (IsLetterOrDigit(k)) return VkToken(k);
            if (IsFKey(k)) return "F" + (1 + (k - Keys.F1));
            if (IsNumPad(k)) return "NUMPAD" + (k - Keys.NumPad0);
            string s;
            if (vkNames.TryGetValue(k, out s) && !s.StartsWith("OEM_") && s != "ESCAPE" && s != "CAPITAL" && s != "NUMLOCK") return "VK_" + s;
            return "0x" + ((int)k).ToString("X2");
        }

        public static KeyCombo FromSrWeave(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) || s.Trim() == "0") return null;
            var k = FromToken(s.Trim());
            return k == Keys.None ? null : new KeyCombo { Ctrl = true, Alt = true, Key = k };
        }
    }

    // Read-only text box that records the key combination pressed while it has focus.
    sealed class KeyBox : TextBox
    {
        public bool FixedCtrlAlt;     // SRWeave keys: modifiers are always Ctrl+Alt, only the key is recorded
        KeyCombo combo;
        public KeyCombo Combo
        {
            get { return combo; }
            set { combo = value; Text = KeyText.Friendly(combo, FixedCtrlAlt); }
        }
        public KeyBox()
        {
            ReadOnly = true; BackColor = Color.White; Cursor = Cursors.Hand;
            GotFocus += (s, e) => { BackColor = Color.FromArgb(240, 247, 255); };
            LostFocus += (s, e) => { BackColor = Color.White; };
        }
        protected override bool IsInputKey(Keys keyData) { return true; }   // also take Tab, arrows, Enter
        protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e) { e.IsInputKey = true; base.OnPreviewKeyDown(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true; e.Handled = true;
            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.Menu || k == Keys.ShiftKey || k == Keys.LWin || k == Keys.RWin || k == Keys.None) return;
            if (FixedCtrlAlt) Combo = new KeyCombo { Ctrl = true, Alt = true, Key = k };
            else Combo = new KeyCombo { Ctrl = e.Control, Alt = e.Alt, Shift = e.Shift, Key = k };
        }
        protected override bool ProcessDialogKey(Keys keyData) { return false; }   // keep Tab/Esc/Enter for OnKeyDown
    }

    // ---- ini helpers (ANSI files, same behaviour as the 3DMigoto/geo-11 ini parsers expect) ----------
    static class Ini
    {
        public static readonly Encoding Enc = Encoding.Default;

        public static string Get(string file, string name)
        {
            if (!File.Exists(file)) return null;
            var rx = new Regex(@"^\s*" + Regex.Escape(name) + @"\s*=\s*(.*?)\s*$");
            foreach (var l in File.ReadAllLines(file, Enc))
            {
                var m = rx.Match(l);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        // replace the first active "name = value" line (drop duplicates); if none, append under [section]
        public static void Set(string file, string name, string value, string section)
        {
            var lines = File.ReadAllLines(file, Enc).ToList();
            var rx = new Regex(@"^\s*" + Regex.Escape(name) + @"\s*=");
            bool done = false;
            var outp = new List<string>();
            foreach (var l in lines)
            {
                if (rx.IsMatch(l)) { if (!done) { outp.Add(name + " = " + value); done = true; } }
                else outp.Add(l);
            }
            if (!done)
            {
                int at = -1;
                if (section != null)
                {
                    var srx = new Regex(@"^\s*\[" + Regex.Escape(section) + @"\]\s*$");
                    at = outp.FindIndex(l => srx.IsMatch(l));
                }
                if (at >= 0) outp.Insert(at + 1, name + " = " + value);
                else { if (section != null) outp.Add("[" + section + "]"); outp.Add(name + " = " + value); }
            }
            File.WriteAllLines(file, outp, Enc);
        }

        // geo-11 writes values saved in-game (Ctrl+F7) to d3dx_user.ini as "pre persist <name> = <value>";
        // at start-up those override d3dxdm.ini, so they are the values the game really uses.
        // UF2 (geo-11 0.6.40) cannot persist the built-ins, so the installer's KeySaveSettings stores them in
        // $saved_sep / $saved_conv (d3dxdm.ini -> "$\directmode\saved_sep = .." in d3dx_user.ini); -1 = nothing saved
        static string SavedVar(string name)
        {
            return name == "separation" ? "saved_sep" : name == "convergence" ? "saved_conv" : null;
        }
        static Regex PersistedRx(string name, bool capturePrefix)
        {
            string v = SavedVar(name);
            string alt = v != null ? @"|\$\\directmode\\" + v : "";
            string key = @"(?:(?:pre\s+|post\s+)?persist\s+" + Regex.Escape(name) + alt + ")";
            return new Regex(capturePrefix ? @"^(\s*" + key + @"\s*=\s*)(.*?)\s*$" : @"^\s*" + key + @"\s*=\s*(.*?)\s*$", RegexOptions.IgnoreCase);
        }

        public static string GetPersisted(string gameDir, string name)
        {
            string f = Path.Combine(gameDir, "d3dx_user.ini");
            if (!File.Exists(f)) return null;
            var rx = PersistedRx(name, false);
            foreach (var l in File.ReadAllLines(f, Enc))
            {
                var m = rx.Match(l);
                if (!m.Success) continue;
                string val = m.Groups[1].Value;
                double d;
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d) && d < 0) continue;   // -1: not saved
                return val;
            }
            return null;
        }

        // update an existing persisted value (if there is none, d3dxdm.ini's value applies and nothing is written)
        public static bool SetPersisted(string gameDir, string name, string value)
        {
            string f = Path.Combine(gameDir, "d3dx_user.ini");
            if (!File.Exists(f)) return false;
            var rx = PersistedRx(name, true);
            var lines = File.ReadAllLines(f, Enc);
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var m = rx.Match(lines[i]);
                if (m.Success) { lines[i] = m.Groups[1].Value + value; changed = true; }
            }
            if (changed) File.WriteAllLines(f, lines, Enc);
            return changed;
        }

        // comment out every active "name = value" line (3DMigoto treats a missing key as its default)
        public static void Disable(string file, string name)
        {
            var rx = new Regex(@"^\s*" + Regex.Escape(name) + @"\s*=");
            var lines = File.ReadAllLines(file, Enc);
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
                if (rx.IsMatch(lines[i])) { lines[i] = ";" + lines[i].TrimStart(); changed = true; }
            if (changed) File.WriteAllLines(file, lines, Enc);
        }

        // "include = <path>" line in [Include]: true if an active one exists
        public static bool GetInclude(string file, string path)
        {
            if (!File.Exists(file)) return false;
            var rx = new Regex(@"^\s*include\s*=\s*" + Regex.Escape(path) + @"\s*$", RegexOptions.IgnoreCase);
            return File.ReadAllLines(file, Enc).Any(l => rx.IsMatch(l));
        }

        // enable (uncomment / add) or disable (comment out) the "include = <path>" line
        public static void SetInclude(string file, string path, bool enabled)
        {
            var rx = new Regex(@"^\s*;*\s*include\s*=\s*" + Regex.Escape(path) + @"\s*$", RegexOptions.IgnoreCase);
            var lines = File.ReadAllLines(file, Enc).ToList();
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!rx.IsMatch(lines[i])) continue;
                if (enabled && !found) { lines[i] = "include = " + path; found = true; }
                else lines[i] = ";include = " + path;
            }
            if (enabled && !found)
            {
                int at = lines.FindIndex(l => Regex.IsMatch(l, @"^\s*\[Include\]\s*$"));
                if (at >= 0) lines.Insert(at + 1, "include = " + path); else { lines.Add("[Include]"); lines.Add("include = " + path); }
            }
            File.WriteAllLines(file, lines, Enc);
        }

        public static bool HasSection(string file, string section)
        {
            if (!File.Exists(file)) return false;
            var rx = new Regex(@"^\s*\[" + Regex.Escape(section) + @"\]\s*$", RegexOptions.IgnoreCase);
            return File.ReadAllLines(file, Enc).Any(l => rx.IsMatch(l));
        }

        public static string GetSectionKey(string file, string section)
        {
            if (!File.Exists(file)) return null;
            bool inSec = false;
            foreach (var l in File.ReadAllLines(file, Enc))
            {
                var h = Regex.Match(l, @"^\s*\[(.+?)\]\s*$");
                if (h.Success) { inSec = string.Equals(h.Groups[1].Value, section, StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSec) continue;
                var m = Regex.Match(l, @"^\s*Key\s*=\s*(.*?)\s*$", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        public static void SetSectionKey(string file, string section, string value)
        {
            var lines = File.ReadAllLines(file, Enc).ToList();
            bool inSec = false, done = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var h = Regex.Match(lines[i], @"^\s*\[(.+?)\]\s*$");
                if (h.Success) { inSec = string.Equals(h.Groups[1].Value, section, StringComparison.OrdinalIgnoreCase); continue; }
                if (inSec && !done && Regex.IsMatch(lines[i], @"^\s*Key\s*=", RegexOptions.IgnoreCase)) { lines[i] = "Key = " + value; done = true; }
            }
            if (!done) { lines.Add("[" + section + "]"); lines.Add("Key = " + value); }
            File.WriteAllLines(file, lines, Enc);
        }
    }

    static class Util
    {
        public static int? ExeBits(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    fs.Position = 0x3C;
                    int pe = br.ReadInt32();
                    fs.Position = pe + 4;
                    ushort machine = br.ReadUInt16();
                    if (machine == 0x8664) return 64;
                    if (machine == 0x014C) return 32;
                }
            }
            catch { }
            return null;
        }

        // "2019.4.31" / "unknown" (Unity but version unreadable) / null (not Unity)
        public static string UnityVersion(string dir, string exe)
        {
            string dll = Path.Combine(dir, "UnityPlayer.dll");
            if (File.Exists(dll))
            {
                var v = FileVersionInfo.GetVersionInfo(dll).FileVersion ?? "";
                var m = Regex.Match(v, @"^(\d+\.\d+\.\d+)");
                if (m.Success) return m.Groups[1].Value;
            }
            string data = Path.Combine(dir, Path.GetFileNameWithoutExtension(exe) + "_Data");
            if (!Directory.Exists(data)) return null;
            foreach (var n in new[] { "globalgamemanagers", "mainData", "data.unity3d" })
            {
                string f = Path.Combine(data, n);
                if (!File.Exists(f)) continue;
                try
                {
                    var buf = new byte[4096];
                    int len;
                    using (var fs = File.OpenRead(f)) len = fs.Read(buf, 0, buf.Length);
                    var txt = Encoding.ASCII.GetString(buf, 0, len);
                    var m = Regex.Match(txt, @"(\d{1,4}\.\d+\.\d+)[abfp]\d+");
                    if (m.Success) return m.Groups[1].Value;
                }
                catch { }
            }
            return "unknown";
        }

        // IMAGE_FILE_LARGE_ADDRESS_AWARE: a 32-bit game without it gets only 2 GB of address space, which dgVoodoo2 at panel
        // resolution + geo-11's per-eye targets + SRWeave's copy can exhaust (Sine Mora EX: std::bad_alloc ~45 s in).
        public static bool? IsLargeAddressAware(string exe)
        {
            try
            {
                byte[] b = new byte[4096];
                using (var fs = File.OpenRead(exe)) if (fs.Read(b, 0, b.Length) < 64) return null;
                int pe = BitConverter.ToInt32(b, 0x3C);
                if (pe <= 0 || pe + 24 > b.Length || b[pe] != 'P' || b[pe + 1] != 'E') return null;
                return (BitConverter.ToUInt16(b, pe + 22) & 0x20) != 0;
            }
            catch { return null; }
        }

        // sets the flag (the "4GB patch"); the original exe is kept as <exe>.geo11sr_bak. Returns false when already set.
        public static bool SetLargeAddressAware(string exe)
        {
            if (IsLargeAddressAware(exe) != false) return false;
            string bak = exe + ".geo11sr_bak";
            if (!File.Exists(bak)) File.Copy(exe, bak);
            byte[] b = File.ReadAllBytes(exe);
            int pe = BitConverter.ToInt32(b, 0x3C);
            ushort ch = (ushort)(BitConverter.ToUInt16(b, pe + 22) | 0x20);
            b[pe + 22] = (byte)(ch & 0xFF); b[pe + 23] = (byte)(ch >> 8);
            File.WriteAllBytes(exe, b);
            return true;
        }

        // Graphics API the exe uses, from its import table (static link) or, failing that, from DLL names in the file
        // (dynamic loading): "DX9", "DX11", "DX12" or null. DX11/DX12 win over DX9 (engines that support both).
        public static string DirectXOf(string exe)
        {
            try
            {
                var imports = PeImports(exe);
                bool d9 = imports.Contains("d3d9.dll"), d11 = imports.Contains("d3d11.dll") || imports.Contains("dxgi.dll"), d12 = imports.Contains("d3d12.dll");
                if (!d9 && !d11 && !d12)
                {
                    byte[] b = File.ReadAllBytes(exe);
                    Func<string, bool> has = s => IndexOf(b, Encoding.ASCII.GetBytes(s)) >= 0 || IndexOf(b, Encoding.Unicode.GetBytes(s)) >= 0;
                    d9 = has("d3d9.dll"); d11 = has("d3d11.dll") || has("dxgi.dll"); d12 = has("d3d12.dll");
                }
                if (d12) return "DX12";
                if (d11) return "DX11";
                if (d9) return "DX9";
            }
            catch { }
            return null;
        }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && (hay[i + j] | 0x20) == (needle[j] | 0x20)) j++;   // case-insensitive for ASCII letters
                if (j == needle.Length) return i;
            }
            return -1;
        }

        // lower-case names of the DLLs in the PE import table
        public static HashSet<string> PeImports(string exe)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            byte[] b = File.ReadAllBytes(exe);
            int pe = BitConverter.ToInt32(b, 0x3C);
            if (pe <= 0 || pe + 24 > b.Length || b[pe] != 'P' || b[pe + 1] != 'E') return names;
            int opt = pe + 24;
            bool is64 = BitConverter.ToUInt16(b, opt) == 0x20b;
            int nsec = BitConverter.ToUInt16(b, pe + 6), so = opt + BitConverter.ToUInt16(b, pe + 20);
            var secs = new List<int[]>();
            for (int i = 0; i < nsec; i++) { int s = so + 40 * i; secs.Add(new[] { BitConverter.ToInt32(b, s + 12), BitConverter.ToInt32(b, s + 8), BitConverter.ToInt32(b, s + 20), BitConverter.ToInt32(b, s + 16) }); }
            Func<int, int> r2o = rva => { foreach (var s in secs) if (rva >= s[0] && rva < s[0] + Math.Max(s[1], s[3])) return s[2] + rva - s[0]; return -1; };
            int dd = opt + (is64 ? 112 : 96) + 8;   // import directory
            int irva = BitConverter.ToInt32(b, dd);
            if (irva == 0) return names;
            int off = r2o(irva);
            while (off >= 0 && off + 20 <= b.Length)
            {
                int nm = BitConverter.ToInt32(b, off + 12);
                if (nm == 0) break;
                int o = r2o(nm);
                if (o < 0 || o >= b.Length) break;
                int e = Array.IndexOf(b, (byte)0, o);
                names.Add(Encoding.ASCII.GetString(b, o, e - o).ToLowerInvariant());
                off += 20;
            }
            return names;
        }

        // Unreal Engine 4 game: the shipping exe lives in <Project>\Binaries\Win64 (or Win32) next to a Content folder
        public static bool IsUe4(string exe)
        {
            string dir = Path.GetDirectoryName(exe) ?? "";
            string name = Path.GetFileName(exe);
            if (Regex.IsMatch(name, @"-Win(64|32)-Shipping\.exe$", RegexOptions.IgnoreCase)) return true;
            if (!Regex.IsMatch(dir, @"[\\/]Binaries[\\/]Win(64|32)$", RegexOptions.IgnoreCase)) return false;
            string project = Path.GetDirectoryName(Path.GetDirectoryName(dir));
            return project != null && (Directory.Exists(Path.Combine(project, "Content")) || Directory.Exists(Path.Combine(Path.GetDirectoryName(project) ?? project, "Engine")));
        }

        // <Project> folder name of a UE4 game (= the folder under %LOCALAPPDATA% that holds Saved\Config)
        public static string Ue4ProjectName(string exe)
        {
            string dir = Path.GetDirectoryName(exe);
            string project = Path.GetDirectoryName(Path.GetDirectoryName(dir));
            return project != null ? Path.GetFileName(project) : null;
        }

        // SRWeave's dxgi.dll of any build: the package's own file, or the marker every SRWeave build carries (its log
        // header). An older SRWeave in a game folder is ours to replace or remove, not a ReShade dxgi.dll to back up.
        public static bool IsSrWeave(string file, string ourDll)
        {
            if (!File.Exists(file)) return false;
            if (SameFile(file, ourDll)) return true;
            try
            {
                byte[] b = File.ReadAllBytes(file), m = Encoding.ASCII.GetBytes("SRWeave geo-11 (dxgi.dll proxy)");
                for (int i = 0; i + m.Length <= b.Length; i++)
                {
                    if (b[i] != m[0]) continue;
                    int k = 1;
                    while (k < m.Length && b[i + k] == m[k]) k++;
                    if (k == m.Length) return true;
                }
            }
            catch { }
            return false;
        }

        public static bool SameFile(string a, string b)
        {
            if (!File.Exists(a) || !File.Exists(b)) return false;
            using (var sha = SHA256.Create())
            {
                byte[] ha, hb;
                using (var fa = File.OpenRead(a)) ha = sha.ComputeHash(fa);
                using (var fb = File.OpenRead(b)) hb = sha.ComputeHash(fb);
                return ha.SequenceEqual(hb);
            }
        }

        // ShaderFixes\<name>.<stage>_<model>.<flags>.bin is 3DMigoto's compiled cache of <name>.hlsl / <name>.txt; a bin
        // older than its source is "stale" and gets recompiled at start-up. Makes every bin at least as new as its source.
        public static int FixShaderCacheTimes(string shaderFixes)
        {
            if (!Directory.Exists(shaderFixes)) return 0;
            int n = 0;
            var rx = new Regex(@"^(.*)\.(vs|ps|gs|hs|ds|cs)_\d_\d\.\d+\.bin$", RegexOptions.IgnoreCase);
            foreach (var bin in Directory.GetFiles(shaderFixes, "*.bin"))
            {
                var m = rx.Match(Path.GetFileName(bin));
                if (!m.Success) continue;
                foreach (var ext in new[] { ".hlsl", ".txt" })
                {
                    string srcFile = Path.Combine(shaderFixes, m.Groups[1].Value + ext);
                    if (!File.Exists(srcFile)) continue;
                    var st = File.GetLastWriteTimeUtc(srcFile);
                    if (File.GetLastWriteTimeUtc(bin) < st)
                    {
                        try { File.SetAttributes(bin, FileAttributes.Normal); File.SetLastWriteTimeUtc(bin, st); n++; } catch { }
                    }
                }
            }
            return n;
        }

        public static void CopyDir(string src, string dst, Func<string, bool> skipTop)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
            {
                if (skipTop != null && skipTop(Path.GetFileName(f))) continue;
                string to = Path.Combine(dst, Path.GetFileName(f));
                if (File.Exists(to)) File.SetAttributes(to, FileAttributes.Normal);
                File.Copy(f, to, true);
                // keep the source time stamp: 3DMigoto/geo-11 treats a ShaderFixes *.bin older than its .hlsl/.txt as
                // stale and recompiles it (the UF2 overlay geometry shaders take tens of minutes) - the copy order
                // must not make the source newer than its cached binary
                try { File.SetLastWriteTimeUtc(to, File.GetLastWriteTimeUtc(f)); } catch { }
            }
            foreach (var d in Directory.GetDirectories(src))
            {
                if (skipTop != null && skipTop(Path.GetFileName(d))) continue;
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)), null);
            }
        }

        // Steam: walk up from the game folder to "...\steamapps\common\<installdir>\..." and read appmanifest_*.acf
        public static bool FindSteamApp(string gameDir, out string appId, out string name)
        {
            appId = null; name = null;
            try
            {
                string full = Path.GetFullPath(gameDir).TrimEnd('\\');
                int i = full.IndexOf("\\steamapps\\common\\", StringComparison.OrdinalIgnoreCase);
                if (i < 0) return false;
                string steamapps = full.Substring(0, i + "\\steamapps".Length);
                string rest = full.Substring(i + "\\steamapps\\common\\".Length);
                string installDir = rest.Split('\\')[0];
                foreach (var acf in Directory.GetFiles(steamapps, "appmanifest_*.acf"))
                {
                    string txt = File.ReadAllText(acf, Encoding.UTF8);
                    var mi = Regex.Match(txt, "\"installdir\"\\s+\"(.*?)\"");
                    if (!mi.Success || !string.Equals(mi.Groups[1].Value, installDir, StringComparison.OrdinalIgnoreCase)) continue;
                    var ma = Regex.Match(txt, "\"appid\"\\s+\"(\\d+)\"");
                    var mn = Regex.Match(txt, "\"name\"\\s+\"(.*?)\"");
                    if (!ma.Success) continue;
                    appId = ma.Groups[1].Value;
                    name = mn.Success ? mn.Groups[1].Value : installDir;
                    return true;
                }
            }
            catch { }
            return false;
        }

        // a readable game name: Steam manifest name > exe file description > exe name
        public static string GameDisplayName(string exe)
        {
            string id, n;
            if (FindSteamApp(Path.GetDirectoryName(exe), out id, out n) && !string.IsNullOrWhiteSpace(n)) return n;
            try
            {
                var fd = FileVersionInfo.GetVersionInfo(exe).FileDescription;
                if (!string.IsNullOrWhiteSpace(fd) && fd.Trim().Length > 2) return fd.Trim();
            }
            catch { }
            return Path.GetFileNameWithoutExtension(exe);
        }

        // run a console program hidden, return its output. Both pipes are read at the same time: reading stdout to the end
        // first deadlocked when the program filled the stderr pipe (Uninstall.bat prints "Could Not Find <path>" for every
        // missing file, more than the pipe holds with a long game path). stdin is closed so a "pause" cannot wait forever,
        // and a program that still has not finished after two minutes is ended.
        public static string RunHidden(string file, string args, string workDir)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = workDir,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.Default, StandardErrorEncoding = Encoding.Default
            };
            using (var p = Process.Start(psi))
            {
                p.StandardInput.Close();
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(120000)) { try { p.Kill(); } catch { } p.WaitForExit(5000); }
                System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { o, e }, 5000);
                return ((o.IsCompleted ? o.Result : "") + (e.IsCompleted ? e.Result : "")).Trim();
            }
        }
    }

    sealed class MainForm : Form
    {
        public const string AppName = "Geo Gaming Hub";
        // ---- data ----
        readonly string root;   // folder that holds the package folders
        readonly Dictionary<string, string> pkgDirs;
        static readonly string[] UnityCodes = { "55", "56", "2017", "2019" };
        static string[] UnityNames { get { return new[] { Lang.T("5.5 이하"), "5.6", "2017 - 2018", Lang.T("2019 이상") }; } }
        const int UnityDefaultIndex = 3;   // 2019 이상
        static readonly string[] DirectModes = { "sbs", "sbs_reversed", "tab", "tab_reversed", "interlaced", "interlaced_reversed", "checkerboard", "checkerboard_reversed", "nvidia_dx11", "nvidia_dx9", "katanga_vr" };

        static KeyDef[] GeoKeys { get { return new KeyDef[] {
            new KeyDef("KeyToggleStereo",              Lang.T("3D 켜기 / 끄기"),           "ctrl t"),
            new KeyDef("KeyIncreaseSeparation",        Lang.T("Separation(입체감) 증가"),  "ctrl F4"),
            new KeyDef("KeyDecreaseSeparation",        Lang.T("Separation(입체감) 감소"),  "ctrl F3"),
            new KeyDef("KeyIncreaseConvergence",       Lang.T("Convergence(수렴) 증가"),   "ctrl F6"),
            new KeyDef("KeyDecreaseConvergence",       Lang.T("Convergence(수렴) 감소"),   "ctrl F5"),
            new KeyDef("KeySaveSettings",              Lang.T("현재 값 저장"),             "ctrl F7"),
            new KeyDef("KeyToggleOverlayStereoParams", Lang.T("입체 값 오버레이 표시"),    "ctrl F1"),
        }; } }
        static KeyDef[] MigotoKeys { get { return new KeyDef[] {
            new KeyDef("toggle_hunting", Lang.T("오버레이 표시 / 숨김 (게임 중)"), "no_modifiers NO_VK_DECIMAL VK_NUMPAD0"),
            new KeyDef("reload_config",  Lang.T("설정(d3dx.ini) 다시 읽기"),       "no_modifiers VK_F10"),
            new KeyDef("reload_fixes",   Lang.T("픽스(ShaderFixes) 다시 읽기"),    "no_modifiers VK_F10"),
            new KeyDef("show_original",  Lang.T("픽스 잠시 끄기 (누르는 동안)"),   "no_modifiers VK_F9"),
        }; } }
        static KeyDef[] SrKeys { get { return new KeyDef[] {
            new KeyDef("key_weave", Lang.T("위빙 켜기 / 끄기"),           "W"),
            new KeyDef("key_swap",  Lang.T("좌우 눈 바꾸기"),             "S"),
            new KeyDef("key_lens",  Lang.T("렌티큘러 렌즈 켜기 / 끄기"),  "L"),
            new KeyDef("key_test",  Lang.T("테스트 무늬 (왼눈 빨강)"),    "T"),
        }; } }

        string gameDir, gameExe;

        // ---- controls ----
        TabControl tabs;
        Label lblGame, lblDetect;
        RadioButton rbUnity, rbPlain, rbUe4, rb64, rb32, rbFull, rbSbsOnly, rbSrOnly;   // install scope: full (with SR) / SBS only (no SR) / SR only (existing fix)
        RadioButton rbKindGeo, rbKindUe4, rbKindDx9; Label lblKindGame, lblKindDetect;   // step 1: geo-11 (Unity / 일반), UE4 Universal Fix 2, or DX9 (dgVoodoo2 + 일반 geo-11)
        ComboBox cbDx9Ver, cbDx9Vram; CheckBox chkDx9Desktop; FlowLayoutPanel rowDx9, rowDx9Vram;
        RadioButton rbKindGl;   // OpenGL games: OGL3D (opengl32.dll proxy that weaves by itself, no geo-11)
        Button bLogGame;        // SRWeave.log / OGL3D.log of the selected game
        Button btnReport;       // [3D 동작 보고...]: off for games already in the community list
        Button btnLaunch;       // [실행]: accent colour while a game set up completely ('설정 완료') is selected
        readonly HashSet<string> completeGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Label lblGlInstall; FlowLayoutPanel pnlScope;   // install step: OGL3D note / geo-11 install scope (hidden for OpenGL)
        Label lblSrHead; FlowLayoutPanel pnlGeoMode, pnlGlMode; ComboBox cbGlMode; Label lblGlModeNote;   // display step: direct_mode (geo-11) / output (OGL3D)
        readonly List<Control> geoUpscaleControls = new List<Control>(); TableLayoutPanel gridUpAdv; CheckBox chkUpAdv; Label lblUpNoteGl;
        Label lblStereoHead, lblStereoGl; readonly List<Control> geoStereoNotes = new List<Control>();
        RadioButton rbCatGeo, rbCatWrap; FlowLayoutPanel pnlKindGeo, pnlKindWrap;   // 3D method step: category, then the game types in it
        RadioButton lastGeoKind, lastWrapKind; bool syncingCategory;
        RadioButton rbKindVk; CheckBox chkVkDxvk; Label lblVkDxvk;
        RadioButton rbKindDx12; Label lblDx12Install, lblStereoDx12;   // DirectX 12 games: DX12_3D (d3d12.dll proxy)
        // TriDef 3D games (an installed TriDef 3D, registered here instead of in TriDef 3D Ignition, started by Tridef3D_Play)
        RadioButton rbCatTri, rbKindTri; FlowLayoutPanel pnlKindTri; ComboBox cbTriProfile; Label lblTriState, lblTriInstall, lblStereoTri;
        TableLayoutPanel gridStereo;
        // candidate list tab: data\game_candidates.json + the installed games of this PC
        ListView lvCands; TextBox tbCandSearch; ComboBox cbCandMethod, cbCandProduct; CheckBox chkCandInstalled; Label lblCandInfo;
        List<Candidate> candidates = new List<Candidate>(); bool candScanned, candScanning;   // Vulkan games: VK3D (vulkan-1.dll proxy), optionally DX9/11 through DXVK
        Label lblVkInstall, lblGlModeHead, lblUpHead, lblStereoVk;
        bool vkDxvkPossible;   // the selected exe is a 64-bit DX9 / DX11 game (the DXVK route of the Vulkan kind)
        CheckBox chkLaa; Label lblLaa;   // 4GB patch for 32-bit exes without LARGE_ADDRESS_AWARE (DX9 kind, optional)
        Label lblEngine, lblUnityVer; FlowLayoutPanel rowEngine, rowUnityVer;
        Panel pnlUe4; CheckBox chkEngineIni; Button btnUe4Tool;
        ComboBox cbUnityVer; Label lblUnityDetected;
        CheckBox chkSr, chkSwap, chkLens; ComboBox cbMode; Label lblModeNote;
        CheckBox chkUpscale; ComboBox cbUpscaleFs, cbUpscaleMode; NumericUpDown numUpW, numUpH; Label lblUpNote;
        const string UpscaleInclude = "ShaderFixes\\upscale.ini";
        NumericUpDown numSep, numConv; CheckBox chkAutoConv;
        ComboBox cbHunting;
        readonly Dictionary<string, KeyBox> keyBoxes = new Dictionary<string, KeyBox>();
        TextBox txtLog; Label lblStatus;
        ListView lvGames; ImageList imgGames; Label lblGamesNote;
        readonly string gamesFile;   // one game per line: "<exe>\t<1 done | 0 incomplete>", kept next to the installer
        sealed class GameRec { public string Exe; public bool Done; }
        readonly List<GameRec> games = new List<GameRec>();
        bool wizardActive;           // 종류 -> 설치 -> 디스플레이 -> 입체 값 -> 완료 in progress
        static string[] WizardTitles { get { return new[] { Lang.T("3D 방식"), Lang.T("설치"), Lang.T("디스플레이"), Lang.T("입체 값") }; } }
        const int WizardSteps = 4;
        const int TabList = 0, TabCands = 1, TabKind = 2, TabInstall = 3, TabDisplay = 4, TabStereo = 5, TabKeys = 6;

        public MainForm()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            // release layout: GeoGamingHub.exe + Packages\<package>; development: the package folders next to the exe's folder
            root = Directory.Exists(Path.Combine(exeDir, "Packages")) ? Path.Combine(exeDir, "Packages") : Path.GetDirectoryName(exeDir);
            pkgDirs = new Dictionary<string, string> {
                // short folder names; the long names of the first releases are still accepted
                { "unity", FindPkg(root, "Geo-11_Unity", "Unity", "geo-11 v0.6.109_Unity_Complete_SR") },
                { "plain", FindPkg(root, "Geo-11_DX11", "Preferred", "geo-11 v0.6.109_Preferred_SR") },
                { "ue4",   FindPkg(root, "Geo-11_UE4", "UE4", "UNREAL_Engine_4_UNIVERSAL-FIX_2_SR") },   // x64 only, no x64/x32 subfolders
                { "dx9",   FindPkg(root, "Geo-11_DX9") },   // dgVoodoo2 D3D9.dll per version / bitness, used with the "plain" package
                { "ogl3d", FindPkg(root, "OGL3D") },        // OpenGL games: OGL3D x64|x86\opengl32.dll + OGL3D.ini
                { "vk3d",  FindPkg(root, "VK3D") },         // Vulkan games: VK3D x64\vulkan-1.dll + VK3D.ini + profiles\<exe>
                { "dxvk",  FindPkg(root, "DXVK") },         // DirectX 9 / 11 -> Vulkan (experimental route to VK3D)
                { "tridef", FindPkg(root, "TriDef") },      // TriDef 3D games: Tridef3D_Play.exe + Inject32.exe + sr\x86|x64 (no TriDef files)
                { "dx12",  FindPkg(root, "DX12_3D") },      // DirectX 12 games: DX12_3D x64\d3d12.dll + DX12_3D.ini + DX12_3D\(DXC) + profiles\<exe>
            };
            gamesFile = Path.Combine(exeDir, "games.list");
            Lang.Load(exeDir);   // settings.ini "lang=ko|en", else the Windows display language
            TriOps.BundledList = DataFile("tridef_profiles.txt");   // TriDef's profile names, for PCs without TriDef 3D
            Report.LoadCache(exeDir);   // community 3D list (compat.json) from the last run; refreshed once the window is up
            Shown += (s, e) => FetchCommunity();

            Text = AppName;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }   // the exe's own icon (src\app.ico) in the title bar / taskbar
            Font = new Font(Lang.Face, 10f);
            // The exe is DPI-aware (manifest). Fonts scale by themselves (points); every pixel size below goes
            // through S() so the layout grows with the screen's scaling factor. WinForms' own AutoScale is off.
            AutoScaleMode = AutoScaleMode.None;
            using (var gr = CreateGraphics()) dpi = gr.DpiX / 96f;
            BackColor = Color.White;
            // big enough that the game list shows every column but the folder and the steps rarely scroll; at most 92%
            // of the screen's working area
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(Math.Min(S(1180), wa.Width * 92 / 100), Math.Min(S(840), wa.Height * 92 / 100));
            MinimumSize = Sz(860, 600);
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) =>
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;
                string f = files[0];
                if (Directory.Exists(f)) { var c = Directory.GetFiles(f, "*.exe"); if (c.Length > 0) f = c[0]; }
                if (!File.Exists(f)) return;
                AddGameToList(f, FindGame(f) != null && FindGame(f).Done);
                RefreshGameList();
                StartWizard(f);   // dropping an exe is the same as [게임 추가]
            };

            BuildLayout();
            Theme.Attach(this, dpi);
            ApplyTheme();
            foreach (var kv in pkgDirs)
                Log((Directory.Exists(kv.Value) ? Lang.T("패키지 확인: ") : Lang.T("패키지 없음: ")) + kv.Value);
            UpdateEnabled();
            LoadGameList();
            RefreshGameList();
        }

        // package folder next to the installer folder: the first of the given names that exists (current name first, older names after), else the first (for messages)
        static string FindPkg(string root, params string[] names)
        {
            foreach (var n in names) if (Directory.Exists(Path.Combine(root, n))) return Path.Combine(root, n);
            return Path.Combine(root, names[0]);
        }

        // ---- look ------------------------------------------------------------------------------
        // The controls are built with these light colours; Theme.cs maps them to the charcoal theme, replaces the tab headers
        // with a flat strip and shows the 3D-method categories as cards (after BuildLayout, see ApplyTheme).
        static readonly Color Accent = Color.FromArgb(0, 120, 212);
        static readonly Color AccentDark = Color.FromArgb(0, 90, 170);
        static readonly Color TextMain = Color.FromArgb(32, 32, 32);
        static readonly Color TextMuted = Color.FromArgb(110, 110, 110);
        static readonly Color Line = Color.FromArgb(225, 225, 225);
        static readonly Color ButtonBorder = Color.FromArgb(200, 200, 200);

        // ---- DPI scaling helpers (design values are 96-dpi pixels) ------------------------------
        float dpi = 1f;
        int S(int v) { return (int)Math.Round(v * dpi); }
        Size Sz(int w, int h) { return new Size(S(w), S(h)); }
        Padding P(int l, int t, int r, int b) { return new Padding(S(l), S(t), S(r), S(b)); }
        Padding P(int all) { return new Padding(S(all)); }

        // ---- layout ----------------------------------------------------------------------------
        void BuildLayout()
        {
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = P(16, 6, 16, 10), BackColor = Color.White };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(1, S(1))));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(outer);

            // header: language switch at the right
            var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Margin = P(0, 0, 0, 4), Padding = new Padding(0) };
            var langLink = new LinkLabel { Text = Lang.T("English로 바꾸기"), AutoSize = true, LinkColor = Accent, ActiveLinkColor = AccentDark, LinkBehavior = LinkBehavior.HoverUnderline, Margin = P(8, 2, 0, 0) };
            langLink.LinkClicked += (s, e) => { Lang.English = !Lang.English; Lang.Save(); RebuildUi(); };
            var langNow = new Label { Text = Lang.T("언어: 한국어"), AutoSize = true, ForeColor = TextMuted, Margin = P(0, 2, 0, 0) };
            header.Controls.Add(langLink); header.Controls.Add(langNow);
            outer.Controls.Add(header, 0, 0);

            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(S(16), S(7)), Margin = P(0, 0, 0, 10) };
            outer.Controls.Add(tabs, 0, 1);
            tabs.TabPages.Add(BuildGamesTab());
            tabs.TabPages.Add(BuildCandidatesTab());
            tabs.TabPages.Add(BuildKindTab());
            tabs.TabPages.Add(BuildGameTab());
            tabs.TabPages.Add(BuildSrTab());
            tabs.TabPages.Add(BuildStereoTab());
            tabs.TabPages.Add(BuildKeysTab());
            tabs.TabPages.Add(BuildHelpTab());
            tabs.TabPages.Add(BuildLogTab());

            outer.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Line, Margin = new Padding(0) }, 0, 2);

            // no global action bar: actions live in the game list, the wizard pages carry 취소/이전/다음/완료
            // status line on the left, community links (HelixMod blog, 3D Vision Discord) on the right
            var statusRow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = P(0, 6, 0, 0), Padding = new Padding(0) };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblStatus = new Label { Dock = DockStyle.Fill, AutoSize = false, Height = S(26), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextMuted, Margin = new Padding(0), AutoEllipsis = true, Text = Lang.T("[게임 목록]에서 [게임 추가]를 눌러 설정을 시작하세요. 게임 실행 파일을 이 창에 끌어다 놓아도 됩니다.") };
            statusRow.Controls.Add(lblStatus, 0, 0);
            var links = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            var tips = new ToolTip();
            Action<string, string, string> addLink = (text, url, tip) =>
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                var l = new LinkLabel { Text = text, AutoSize = true, LinkColor = Accent, ActiveLinkColor = AccentDark, LinkBehavior = LinkBehavior.HoverUnderline, Margin = P(16, 4, 0, 0) };
                tips.SetToolTip(l, tip + "\n" + url);
                l.LinkClicked += (s, e) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); } };
                links.Controls.Add(l);
            };
            addLink(Lang.T("호환 게임 목록"), Report.ListUrl, Lang.T("사용자 보고로 만든 엔진별·게임별 3D 동작 목록 (settings.ini compat_list=)"));
            addLink(Lang.T("HelixMod 블로그"), Lang.Setting("helixmod", "https://helixmod.blogspot.com/"), Lang.T("3D 픽스 모음·소식 (settings.ini helixmod=)"));
            addLink(Lang.T("3D Vision Discord"), Lang.Setting("discord", "https://discord.gg/zTnnMrA4GE"), Lang.T("3D Vision 커뮤니티 Discord 초대 (settings.ini discord=)"));
            statusRow.Controls.Add(links, 1, 0);
            outer.Controls.Add(statusRow, 0, 4);
            tabs.SelectedIndexChanged += (s, e) => UpdateWizardStatus();
        }

        void ApplyTheme() { Theme.Apply(this, tabs, new[] { rbCatGeo, rbCatWrap, rbCatTri }, TabKind, TabStereo); }

        // GeoGamingHub.exe --shot <png> <tab> [game exe]: what the off-screen screenshot shows
        internal void PrepareShot(int tab, string exe)
        {
            if (exe != null) StartWizard(exe);
            tabs.SelectedIndex = tab;
        }

        // language switch: throw the controls away and build them again in the other language
        void RebuildUi()
        {
            string logText = txtLog != null ? txtLog.Text : "";
            SuspendLayout();
            Controls.Clear();
            keyBoxes.Clear();
            wizardActive = false;
            Text = AppName;
            Font = new Font(Lang.Face, 10f);
            BuildLayout();
            ApplyTheme();
            txtLog.Text = logText;
            UpdateEnabled();
            RefreshGameList();
            if (gameExe != null && File.Exists(gameExe)) DetectGame(gameExe);
            tabs.SelectedIndex = TabList;
            ResumeLayout(true);
        }

        // ---- wizard (설치 -> 디스플레이 -> 입체 값 -> 완료) -------------------------------------
        // Page with a scrolling column on top and a step footer (취소 / 이전 / 다음|완료) at the bottom.
        TabPage WizardTab(int step, out FlowLayoutPanel column)
        {
            var t = NewTab(WizardTitles[step]);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(layout);
            column = NewColumn();
            layout.Controls.Add(column, 0, 0);

            var footer = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), Padding = P(0, 8, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var stepLbl = new Label { AutoSize = true, ForeColor = AccentDark, Font = new Font(Font, FontStyle.Bold), Margin = P(0, 10, 0, 0),
                Text = Lang.T("단계 ") + (step + 1) + " / " + WizardSteps + " - " + WizardTitles[step] + (step == WizardSteps - 1 ? Lang.T("  (완료를 누르면 게임 폴더에 적용합니다)") : "") };
            footer.Controls.Add(stepLbl, 0, 0);
            var btns = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            var cancel = MakeButton(Lang.T("취소"), 90, false); cancel.Click += (s, e) => CancelWizard();
            btns.Controls.Add(cancel);
            if (step > 0) { var prev = MakeButton(Lang.T("이전"), 90, false); prev.Click += (s, e) => tabs.SelectedIndex = TabKind + step - 1; btns.Controls.Add(prev); }
            if (step < WizardSteps - 1) { var next = MakeButton(Lang.T("다음"), 110, true); next.Click += (s, e) => { if (step <= 1 && !RequireGame()) return; tabs.SelectedIndex = TabKind + step + 1; }; btns.Controls.Add(next); }
            else { var done = MakeButton(Lang.T("완료"), 110, true); done.Click += (s, e) => Apply(); btns.Controls.Add(done); }
            footer.Controls.Add(btns, 1, 0);
            layout.Controls.Add(footer, 0, 1);
            return t;
        }

        void StartWizard(string exe)
        {
            if (!File.Exists(exe)) { Status(Lang.T("파일이 없습니다: ") + exe); return; }
            DetectGame(exe);
            wizardActive = true;
            tabs.SelectedIndex = TabKind;
            UpdateWizardStatus();
        }

        void CancelWizard()
        {
            wizardActive = false;
            tabs.SelectedIndex = TabList;
            RefreshGameList();
            Status(gameExe != null ? Lang.T("설정을 중단했습니다. ") + Path.GetFileName(gameExe) + Lang.T("은(는) '재설정 필요'로 남아 있습니다. [설정 바꾸기]로 처음부터 다시 하면 됩니다.") : Lang.T("설정을 중단했습니다."));
        }

        void UpdateWizardStatus()
        {
            if (tabs.SelectedIndex == TabCands && !candScanned) ScanCandidates();
            int i = tabs.SelectedIndex;
            if (i >= TabKind && i <= TabStereo)
            {
                string g = gameExe != null ? Path.GetFileName(gameExe) : Lang.T("(게임을 고르세요)");
                Status(Lang.T("단계 ") + (i - TabKind + 1) + " / " + WizardSteps + " - " + WizardTitles[i - TabKind] + "  |  " + g + (wizardActive ? "" : Lang.T("  |  [게임 목록]의 [게임 추가] 또는 [설정 바꾸기]로 시작하면 단계대로 진행됩니다.")));
            }
        }

        Button MakeButton(string text, int w, bool primary)
        {
            // w is a minimum: the button grows with its text so English labels are never clipped
            var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = Sz(w, 36), Height = S(36), Padding = P(10, 0, 10, 0), Margin = P(0, 0, 8, 0), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            b.FlatAppearance.BorderSize = 1;
            if (primary)
            {
                b.BackColor = Accent; b.ForeColor = Color.White; b.Font = new Font(Font, FontStyle.Bold);
                b.FlatAppearance.BorderColor = Accent;
                b.FlatAppearance.MouseOverBackColor = AccentDark; b.FlatAppearance.MouseDownBackColor = AccentDark;
            }
            else
            {
                b.BackColor = Color.White; b.ForeColor = TextMain;
                b.FlatAppearance.BorderColor = ButtonBorder;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(243, 243, 243); b.FlatAppearance.MouseDownBackColor = Color.FromArgb(230, 230, 230);
            }
            return b;
        }

        TabPage NewTab(string title) { return new TabPage(title) { Padding = P(18, 10, 18, 10), BackColor = Color.White, UseVisualStyleBackColor = false, AutoScroll = true }; }

        FlowLayoutPanel NewColumn()
        {
            return new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0), Margin = new Padding(0) };
        }

        TableLayoutPanel NewGrid(int cols)
        {
            var g = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = cols, Padding = P(0, 0, 0, 6), Margin = new Padding(0) };
            return g;
        }

        // a game type was picked: its category (0 geo-11, 1 own wrappers, 2 TriDef 3D) becomes the selected one and the
        // other categories' types are cleared
        void KindPicked(RadioButton r, int cat)
        {
            if (cat == 1) lastWrapKind = r; else if (cat == 0) lastGeoKind = r;
            if (syncingCategory) return;
            syncingCategory = true;
            if (cat != 0) rbKindGeo.Checked = rbKindUe4.Checked = rbKindDx9.Checked = false;
            if (cat != 1) rbKindGl.Checked = rbKindVk.Checked = rbKindDx12.Checked = false;
            if (cat != 2) rbKindTri.Checked = false;
            (cat == 0 ? rbCatGeo : cat == 1 ? rbCatWrap : rbCatTri).Checked = true;
            pnlKindGeo.Visible = cat == 0; pnlKindWrap.Visible = cat == 1; pnlKindTri.Visible = cat == 2;
            syncingCategory = false;
        }

        // a category was picked: show its game types and select one (the last one picked there, else a guess from the exe)
        void CategoryPicked(int cat)
        {
            if (syncingCategory) return;
            bool wrapper = cat == 1;
            pnlKindGeo.Visible = cat == 0; pnlKindWrap.Visible = cat == 1; pnlKindTri.Visible = cat == 2;
            if (cat == 2) { if (!rbKindTri.Checked) rbKindTri.Checked = true; }
            else if (wrapper && !rbKindGl.Checked && !rbKindVk.Checked && !rbKindDx12.Checked)
                (lastWrapKind ?? (gameExe != null && Dx12Ops.IsDx12Game(gameExe) ? rbKindDx12
                                  : gameExe != null && !vkDxvkPossible && !VkOps.ImportsVulkan(gameExe) && GlOps.IsOpenGL(gameExe) ? rbKindGl : rbKindVk)).Checked = true;   // a DirectX 9/11 game (Unity players import opengl32 too) -> Vulkan through DXVK
            else if (cat == 0 && !rbKindGeo.Checked && !rbKindUe4.Checked && !rbKindDx9.Checked)
                (lastGeoKind ?? (gameExe != null && Util.IsUe4(gameExe) ? rbKindUe4 : rbKindGeo)).Checked = true;
        }
        Label Head(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = AccentDark, Font = new Font(Lang.Face, 10.5f, FontStyle.Bold), Margin = P(0, 12, 0, 6) };
        }
        Label Note(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = TextMuted, Margin = P(0, 2, 0, 8), MaximumSize = Sz(1040, 0) };
        }
        Label Lbl(string text) { return new Label { Text = text, AutoSize = true, ForeColor = TextMain, Margin = P(0, 7, 10, 0) }; }
        TextBox Field(int width)
        {
            return new TextBox { Width = S(width), BorderStyle = BorderStyle.FixedSingle, Margin = P(0, 3, 0, 3) };
        }
        ComboBox Combo(int width, object[] items, int selected)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = S(width), FlatStyle = FlatStyle.System, Margin = P(0, 3, 12, 3) };
            c.Items.AddRange(items); c.SelectedIndex = selected;
            return c;
        }
        RadioButton Radio(string text, bool chk) { return new RadioButton { Text = text, AutoSize = true, Checked = chk, Margin = P(0, 2, 16, 2) }; }
        CheckBox Check(string text, bool chk, int indent) { return new CheckBox { Text = text, AutoSize = true, Checked = chk, Margin = P(indent, 4, 0, 2), MaximumSize = Sz(1040, 0) }; }

        // ---- game list tab ---------------------------------------------------------------------
        TabPage BuildGamesTab()
        {
            var t = NewTab(Lang.T("게임 목록"));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(layout);

            lblGamesNote = Note(Lang.T("[게임 추가]로 게임 실행 파일을 고르면 3D 방식 → 설치 → 디스플레이 → 입체 값 순서로 설정하고 [완료]에서 게임 폴더에 적용합니다. ") +
                Lang.T("중간에 취소하면 게임 폴더는 바뀌지 않고 '재설정 필요'로 남습니다. Sep/Conv는 게임 안에서 저장(Ctrl+F7)한 값이 있으면 그 값입니다. 두 번 클릭하면 실행합니다."));
            lblGamesNote.Margin = P(0, 4, 0, 8);
            layout.Controls.Add(lblGamesNote, 0, 0);

            imgGames = new ImageList { ImageSize = Sz(32, 32), ColorDepth = ColorDepth.Depth32Bit };
            lvGames = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
                BorderStyle = BorderStyle.FixedSingle, SmallImageList = imgGames, HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(0)
            };
            lvGames.Columns.Add(Lang.T("게임"), S(260));
            lvGames.Columns.Add(Lang.T("상태"), S(90));
            lvGames.Columns.Add(Lang.T("3D 보고"), S(95));   // community list: 가능 / 문제 있음 / 안 됨 (number of reports)
            lvGames.Columns.Add(Lang.T("엔진"), S(60));
            lvGames.Columns.Add(Lang.English ? "Bits" : "비트", S(50));
            lvGames.Columns.Add("SR", S(45));
            lvGames.Columns.Add(Lang.T("업스케일"), S(120));
            lvGames.Columns.Add("Sep", S(55));
            lvGames.Columns.Add("Conv", S(60));
            lvGames.Columns.Add(Lang.T("실행"), S(60));
            lvGames.Columns.Add(Lang.T("폴더"), S(400));
            lvGames.DoubleClick += (s, e) => LaunchSelected();
            lvGames.SelectedIndexChanged += (s, e) =>
            {
                UpdateReportButton();
                if (bLogGame == null || lvGames.SelectedItems.Count == 0) return;
                string sd = Path.GetDirectoryName((string)lvGames.SelectedItems[0].Tag);
                bLogGame.Text = GlOps.Installed(sd) ? "OGL3D.log" : VkOps.Installed(sd) ? "VK3D.log" : Dx12Ops.Installed(sd) ? "DX12_3D.log" : "SRWeave.log";
            };
            layout.Controls.Add(lvGames, 0, 1);

            var bars = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = P(0, 10, 0, 0), Padding = new Padding(0) };
            var row1 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0), Padding = new Padding(0) };
            var row2 = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = P(0, 6, 0, 0), Padding = new Padding(0) };
            var bAdd = MakeButton(Lang.T("게임 추가 (설정 시작)"), 170, true); bAdd.Click += (s, e) => AddGameDialog();
            var bEdit = MakeButton(Lang.T("설정 바꾸기"), 110, false); bEdit.Click += (s, e) => EditSelected();
            var bLaunch = btnLaunch = MakeButton(Lang.T("실행"), 90, false); bLaunch.Click += (s, e) => LaunchSelected();
            var bOpen = MakeButton(Lang.T("폴더 열기"), 100, false); bOpen.Click += (s, e) => { var g = SelectedGame(); if (g != null) Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(g) + "\""); };
            var bRefresh = MakeButton(Lang.T("새로 고침"), 100, false); bRefresh.Click += (s, e) => { RefreshGameList(); FetchCommunity(); };
            var bRemove = MakeButton(Lang.T("목록에서 빼기"), 120, false); bRemove.Click += (s, e) => RemoveSelected();
            var bRemoveSr = MakeButton(Lang.T("SR 위빙만 제거"), 130, false); bRemoveSr.Click += (s, e) => { if (SelectGameForAction()) RemoveSr(); };
            var bUninstall = MakeButton(Lang.T("3D 픽스 제거"), 100, false); bUninstall.Click += (s, e) => { if (SelectGameForAction()) UninstallAll(); };
            var bLog = MakeButton("SRWeave.log", 110, false); bLog.Click += (s, e) => { if (SelectGameForAction()) OpenSrLog(); };
            bLogGame = bLog;
            btnReport = MakeButton(Lang.T("3D 동작 보고..."), 120, false); btnReport.Click += (s, e) => ReportSelected();
            var bUe4 = MakeButton(Lang.T("UE4 설정..."), 100, false); bUe4.Click += (s, e) => { if (SelectGameForAction()) { if (Util.IsUe4(gameExe)) OpenUe4Settings(); else Status(Lang.T("Unreal Engine 4 게임이 아닙니다.")); } };
            foreach (var b in new[] { bAdd, bEdit, bLaunch, bOpen, bRefresh }) { b.Height = S(32); row1.Controls.Add(b); }
            foreach (var b in new[] { bRemove, bRemoveSr, bUninstall, bLog, bUe4, btnReport }) { b.Height = S(32); row2.Controls.Add(b); }
            bars.Controls.Add(row1); bars.Controls.Add(row2);
            layout.Controls.Add(bars, 0, 2);
            return t;
        }

        // ---- candidate list tab -------------------------------------------------------------------------
        // Which 3D method fits a game: the author's owned library classified from PCGamingWiki (data\game_candidates.json,
        // tools\library_survey) plus the games installed on this PC (method read from the exe when the data has no entry).
        TabPage BuildCandidatesTab()
        {
            var t = NewTab(Lang.T("후보 목록"));
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(0) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(layout);

            var note = Note(Lang.T("게임마다 맞는 3D 방식입니다. 소장 게임 목록을 PCGamingWiki의 엔진·그래픽 API로 분류했고, 이 PC에 설치된 게임은 실행 파일로도 확인합니다. 검증됨 = 직접 시험한 게임, 그 밖에는 시험이 필요합니다. 설치된 게임을 두 번 클릭하면 설정을 시작합니다.") + "\n" +
                Lang.T("담당: Geo Gaming Hub = 게임에 실제 깊이가 있는 게임(지오메트리 입체). Flato3D = 평면 2D 레이어 게임(Flato3D-SR이 레이어마다 깊이를 줌). AI 2D→3D만 = 완성 화면 한 장만 나오는 게임(에뮬레이터 등)."));
            note.Margin = P(0, 4, 0, 6);
            layout.Controls.Add(note, 0, 0);

            var filters = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = P(0, 0, 0, 6), Padding = new Padding(0) };
            filters.Controls.Add(Lbl(Lang.T("검색:")));
            tbCandSearch = new TextBox { Width = S(240), Margin = P(0, 3, 16, 3), BorderStyle = BorderStyle.FixedSingle };
            tbCandSearch.TextChanged += (s, e) => FillCandidates();
            filters.Controls.Add(tbCandSearch);
            filters.Controls.Add(Lbl(Lang.T("담당:")));
            var prods = new List<object> { Lang.T("전체") };
            foreach (var pr in Library.Products) prods.Add(Library.ProductLabel(pr));
            cbCandProduct = Combo(170, prods.ToArray(), 0);
            cbCandProduct.SelectedIndexChanged += (s, e) => FillCandidates();
            cbCandProduct.Margin = P(0, 3, 16, 3);
            filters.Controls.Add(cbCandProduct);
            filters.Controls.Add(Lbl(Lang.T("3D 방식:")));
            var items = new List<object> { Lang.T("전체") };
            foreach (var m in Library.Methods) items.Add(Library.MethodLabel(m));
            cbCandMethod = Combo(230, items.ToArray(), 0);
            cbCandMethod.SelectedIndexChanged += (s, e) => FillCandidates();
            filters.Controls.Add(cbCandMethod);
            chkCandInstalled = Check(Lang.T("이 PC에 설치된 게임만"), true, 16);
            chkCandInstalled.CheckedChanged += (s, e) => FillCandidates();
            filters.Controls.Add(chkCandInstalled);
            layout.Controls.Add(filters, 0, 1);

            lvCands = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
                BorderStyle = BorderStyle.FixedSingle, HeaderStyle = ColumnHeaderStyle.Nonclickable, Margin = new Padding(0)
            };
            lvCands.Columns.Add(Lang.T("게임"), S(280));
            lvCands.Columns.Add(Lang.T("담당"), S(120));
            lvCands.Columns.Add(Lang.T("추천 3D 방식"), S(200));
            lvCands.Columns.Add(Lang.T("다른 방법"), S(150));
            lvCands.Columns.Add(Lang.T("상태"), S(75));
            lvCands.Columns.Add(Lang.T("설치"), S(60));
            lvCands.Columns.Add(Lang.T("엔진"), S(140));
            lvCands.Columns.Add("API", S(150));
            lvCands.Columns.Add(Lang.English ? "Bits" : "비트", S(65));
            lvCands.Columns.Add(Lang.T("메모"), S(520));
            lvCands.DoubleClick += (s, e) => StartFromCandidate();
            layout.Controls.Add(lvCands, 0, 2);

            var bar = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = P(0, 10, 0, 0), Padding = new Padding(0) };
            var bStart = MakeButton(Lang.T("이 게임 설정 시작"), 150, true); bStart.Click += (s, e) => StartFromCandidate();
            var bScan = MakeButton(Lang.T("새로 고침"), 100, false); bScan.Click += (s, e) => ScanCandidates();
            foreach (var b in new[] { bStart, bScan }) { b.Height = S(32); bar.Controls.Add(b); }
            lblCandInfo = new Label { AutoSize = true, ForeColor = TextMuted, Margin = P(8, 9, 0, 0) };
            bar.Controls.Add(lblCandInfo);
            layout.Controls.Add(bar, 0, 3);
            return t;
        }

        string CandidatesFile() { return DataFile("game_candidates.json"); }

        // data\<name> next to the exe (release) or next to its folder (development)
        static string DataFile(string name)
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            foreach (var d in new[] { exeDir, Path.GetDirectoryName(exeDir) })
            {
                string f = d == null ? null : Path.Combine(d, "data", name);
                if (f != null && File.Exists(f)) return f;
            }
            return null;
        }

        // data + installed games now; the exes of installed games without a data entry are read in the background
        void ScanCandidates()
        {
            if (candScanning) return;
            candScanned = candScanning = true;
            List<Candidate> data;
            try { data = Library.Load(CandidatesFile()); }
            catch (Exception ex) { data = new List<Candidate>(); Log(Lang.T("후보 목록을 읽지 못했습니다: ") + ex.Message); }
            candidates = Library.Merge(data, Library.ScanInstalled());
            FillCandidates();
            var todo = candidates.Where(c => c.Installed != null).ToList();
            if (todo.Count == 0) { candScanning = false; return; }
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var c in todo)
                {
                    try { c.AntiCheat = Library.AntiCheat(c.Installed.Dir); } catch { }
                    if (!c.FromExe || c.Method != null) continue;
                    try { c.Exe = Library.FindExe(c.Installed); c.Method = Library.MethodOfExe(c.Exe); }
                    catch { c.Method = "unknown"; }
                }
                try { BeginInvoke((Action)(() => { candScanning = false; FillCandidates(); })); } catch { candScanning = false; }
            });
        }

        void FillCandidates()
        {
            if (lvCands == null) return;
            string f = Library.Norm(tbCandSearch.Text);
            string m = cbCandMethod.SelectedIndex > 0 ? Library.Methods[cbCandMethod.SelectedIndex - 1] : null;
            string pf = cbCandProduct.SelectedIndex > 0 ? Library.Products[cbCandProduct.SelectedIndex - 1] : null;
            bool onlyInstalled = chkCandInstalled.Checked;
            lvCands.BeginUpdate();
            lvCands.Items.Clear();
            int shown = 0;
            foreach (var c in candidates.OrderBy(x => x.Installed == null).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                if (onlyInstalled && c.Installed == null) continue;
                if (f.Length > 0 && !Library.Norm(c.Title).Contains(f)) continue;
                if (m != null && c.Method != m && !c.Alts.Contains(m)) continue;
                if (pf != null && c.Product != pf) continue;
                bool flato = c.Product == "flato3d";
                // a flat 2D game: Flato3D-SR's route first; the hub's methods would give it next to no depth
                string method = c.Method == null ? Lang.T("확인 중...") : flato ? Library.FlatoRouteLabel(c.FlatoRoute) : Library.MethodLabel(c.Method);
                string alts = flato ? Lang.T("(Hub: ") + Library.MethodLabel(c.Method) + Lang.T(", 깊이 거의 없음)") : string.Join(", ", c.Alts.Select(Library.MethodLabel));
                string status = c.AntiCheat != null ? Lang.T("안티치트") : Library.StatusLabel(c);
                string memo = c.AntiCheat != null ? Lang.T("안티치트(") + c.AntiCheat + Lang.T(") - 계정 정지 위험이 있어 설정하지 않습니다") + (string.IsNullOrEmpty(c.Note) ? "" : "; " + c.Note) : c.Note ?? "";
                var it = new ListViewItem(new[] { c.Title, Library.ProductLabel(c.Product), method, alts, status, c.Installed != null ? c.Installed.Store : "", c.Engine ?? "", c.Api ?? "", c.Bits ?? "", memo }) { Tag = c };
                bool verified = !c.FromExe && (flato ? c.FlatoStatus == "verified" || c.FlatoStatus == "working" : c.Status == "verified");
                if (c.AntiCheat != null) it.ForeColor = Color.FromArgb(196, 43, 28);
                else if (verified) it.ForeColor = Color.FromArgb(16, 124, 16);
                else if (flato) it.ForeColor = Color.FromArgb(120, 60, 160);
                else if (c.Product == "none" || c.Method == "old" || c.Method == "none" || c.Method == "unknown") it.ForeColor = TextMuted;
                lvCands.Items.Add(it);
                shown++;
            }
            lvCands.EndUpdate();
            int inst = candidates.Count(c => c.Installed != null);
            lblCandInfo.Text = string.Format(Lang.T("{0}개 표시 (전체 {1}, 이 PC에 설치 {2})"), shown, candidates.Count, inst) +
                (CandidatesFile() == null ? Lang.T(" - data\\game_candidates.json 없음: 설치된 게임만") : "");
        }

        // set up the selected candidate: its exe goes to the game list and the 3D method step starts; for a game this
        // project tested, the tested method is chosen even where the exe points elsewhere (Lara Croft GO: Unity -> VK3D + DXVK)
        void StartFromCandidate()
        {
            if (lvCands.SelectedItems.Count == 0) { Status(Lang.T("후보 목록에서 게임을 먼저 고르세요.")); return; }
            var c = (Candidate)lvCands.SelectedItems[0].Tag;
            if (c.Installed == null) { Status(Lang.T("이 PC에 설치되지 않은 게임입니다: ") + c.Title); return; }
            if (c.AntiCheat != null || (c.AntiCheat = Library.AntiCheat(c.Installed.Dir)) != null)
            {
                Status(Lang.T("안티치트가 있는 게임은 설정하지 않습니다(계정 정지 위험): ") + c.Title + " (" + c.AntiCheat + ")");
                return;
            }
            if (c.Product == "flato3d")
            {
                Status(Lang.T("평면 2D 게임입니다. Flato3D-SR 설치기로 설정하세요(https://github.com/nautymac/Flato3D-SR): ") + c.Title);
                Log(Lang.T("평면 2D 게임입니다. Flato3D-SR 설치기로 설정하세요(https://github.com/nautymac/Flato3D-SR): ") + c.Title + " - " + Library.FlatoRouteLabel(c.FlatoRoute));
                return;
            }
            string exe = c.Exe ?? (c.Exe = Library.FindExe(c.Installed));
            if (exe == null || !File.Exists(exe))
                using (var dlg = new OpenFileDialog { Filter = Lang.T("실행 파일 (*.exe)|*.exe"), Title = Lang.T("설정할 게임의 실행 파일"), InitialDirectory = c.Installed.Dir })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    exe = c.Exe = dlg.FileName;
                }
            AddGameToList(exe, false);
            RefreshGameList();
            StartWizard(exe);
            if (!c.FromExe && (c.Status == "verified" || c.Status == "checked")) ApplyMethodHint(c.Method);
        }

        void ApplyMethodHint(string method)
        {
            switch (method)
            {
                case "unity": case "dx11": rbKindGeo.Checked = true; break;
                case "ue4": rbKindUe4.Checked = true; break;
                case "dx9": rbKindDx9.Checked = true; break;
                case "opengl": rbKindGl.Checked = true; break;
                case "dx12": rbKindDx12.Checked = true; break;
                case "tridef": rbKindTri.Checked = true; break;
                case "vulkan":
                    rbKindVk.Checked = true;
                    if (chkVkDxvk.Enabled) chkVkDxvk.Checked = true;   // a DirectX exe: the DXVK route
                    break;
                default: return;
            }
            Log(Lang.T("  후보 목록의 시험 결과대로 3D 방식 선택: ") + Library.MethodLabel(method));
            UpdateEnabled();
        }

        // make the selected list entry the current game for the remove/uninstall/log actions
        bool SelectGameForAction()
        {
            var exe = SelectedGame();
            if (exe == null) return false;
            if (!File.Exists(exe)) { Status(Lang.T("실행 파일이 없습니다: ") + exe); return false; }
            if (!string.Equals(exe, gameExe, StringComparison.OrdinalIgnoreCase)) DetectGame(exe);
            return true;
        }

        void LoadGameList()
        {
            games.Clear();
            try
            {
                if (File.Exists(gamesFile))
                    foreach (var raw in File.ReadAllLines(gamesFile, Encoding.UTF8))
                    {
                        var l = raw.Trim();
                        if (l.Length == 0) continue;
                        var parts = l.Split('\t');
                        string exe = parts[0].Trim();
                        bool done = parts.Length < 2 || parts[1].Trim() != "0";   // old one-column lines count as done
                        if (!games.Any(g => string.Equals(g.Exe, exe, StringComparison.OrdinalIgnoreCase))) games.Add(new GameRec { Exe = exe, Done = done });
                    }
            }
            catch (Exception ex) { Log(Lang.T("games.list 읽기 실패: ") + ex.Message); }
        }

        void SaveGameList()
        {
            try { File.WriteAllLines(gamesFile, games.Select(g => g.Exe + "\t" + (g.Done ? "1" : "0")), Encoding.UTF8); }
            catch (Exception ex) { Log(Lang.T("games.list 쓰기 실패: ") + ex.Message); }
        }

        GameRec FindGame(string exe) { return games.FirstOrDefault(g => string.Equals(g.Exe, exe, StringComparison.OrdinalIgnoreCase)); }

        void AddGameToList(string exe, bool done)
        {
            if (string.IsNullOrEmpty(exe)) return;
            var g = FindGame(exe);
            if (g == null) games.Add(new GameRec { Exe = exe, Done = done });
            else if (done) g.Done = true;
            SaveGameList();
        }

        void RefreshGameList()
        {
            lvGames.BeginUpdate();
            lvGames.Items.Clear();
            completeGames.Clear();
            imgGames.Images.Clear();
            foreach (var rec in games)
            {
                string exe = rec.Exe;
                var it = new ListViewItem { UseItemStyleForSubItems = false };
                string dir = Path.GetDirectoryName(exe);
                bool exists = File.Exists(exe);
                it.Text = exists ? Util.GameDisplayName(exe) : Path.GetFileNameWithoutExtension(exe) + Lang.T(" (없음)");
                if (exists)
                {
                    try { using (var ico = Icon.ExtractAssociatedIcon(exe)) { imgGames.Images.Add(exe, ico.ToBitmap()); it.ImageKey = exe; } } catch { }
                }
                string dm = Path.Combine(dir, "d3dxdm.ini");
                bool hasGeo = exists && File.Exists(Path.Combine(dir, "d3d11.dll")) && File.Exists(dm);
                bool hasGl = exists && GlOps.Installed(dir);   // OpenGL kind: OGL3D, settings in OGL3D.ini
                GlOps.Options glo = hasGl ? GlOps.Read(Path.Combine(dir, GlOps.IniName)) : null;
                bool hasVk = exists && !hasGl && VkOps.Installed(dir);   // Vulkan kind: VK3D, settings in VK3D.ini
                VkOps.Options vko = hasVk ? VkOps.Read(Path.Combine(dir, VkOps.IniName)) : null;
                bool hasDx12 = exists && !hasGl && !hasVk && Dx12Ops.Installed(dir);   // DirectX 12 kind: DX12_3D, settings in DX12_3D.ini
                Dx12Ops.Options d12o = hasDx12 ? Dx12Ops.Read(Path.Combine(dir, Dx12Ops.IniName)) : null;
                string triName = exists && !hasGl && !hasVk && !hasDx12 && !hasGeo ? TriOps.RegisteredName(exe) : null;
                bool hasTri = triName != null;   // TriDef 3D: registered in Ignition's list, started by Tridef3D_Play
                string engine = !exists ? "-" : hasTri ? "TriDef" : hasGl ? "OpenGL" : hasVk ? (VkOps.DxvkInstalled(dir) ? "Vulkan (DXVK)" : "Vulkan") : hasDx12 ? "DX12" : (Util.UnityVersion(dir, exe) != null ? "Unity" : Util.IsUe4(exe) ? "UE4" : Dx9Ops.Installed(dir) ? "DX9" : Lang.T("일반"));
                var bits = exists ? Util.ExeBits(exe) : null;
                string sr = "-";
                if (hasTri) sr = TriOps.Sr(triName) ? Lang.T("켬") : Lang.T("끔");
                else if (hasGl) sr = glo.Sr ? Lang.T("켬") : Lang.T("끔");
                else if (hasVk) sr = vko.Sr ? Lang.T("켬") : Lang.T("끔");
                else if (hasDx12) sr = d12o.Sr ? Lang.T("켬") : Lang.T("끔");
                else if (exists)
                {
                    string srIni = Path.Combine(dir, "SRWeave.ini");
                    bool dll = File.Exists(Path.Combine(dir, "dxgi.dll")) && File.Exists(srIni);
                    string w = dll ? Ini.Get(srIni, "weave") : null;
                    sr = dll && (w == null || w.Trim() != "0") ? Lang.T("켬") : Lang.T("끔");
                }
                // upscaling: d3dx.ini [Device] upscaling + width/height (0/absent = desktop resolution)
                string up = hasGeo ? Lang.T("(geo-11 없음)") : "-";
                if (hasGeo)
                {
                    string dx = Path.Combine(dir, "d3dx.ini");
                    string u = Ini.Get(dx, "upscaling"); int un;
                    if (u != null && int.TryParse(u.Trim(), out un) && un > 0)
                    {
                        string w = Ini.Get(dx, "width"), h = Ini.Get(dx, "height"); int wi, hi;
                        bool fixedRes = w != null && h != null && int.TryParse(w.Trim(), out wi) && int.TryParse(h.Trim(), out hi) && wi > 0 && hi > 0;
                        string um = Ini.Get(dx, "upscale_mode");
                up = Lang.T("켬 → ") + (fixedRes ? w.Trim() + "×" + h.Trim() : Lang.T("바탕화면")) + (um != null && um.Trim() == "0" ? Lang.T(" (성능)") : "");
                    }
                    else up = Lang.T("끔");
                }
                // in-game saved values (d3dx_user.ini) win over the d3dxdm.ini defaults
                string sep = hasGeo ? (Ini.GetPersisted(dir, "separation") ?? Ini.Get(dm, "dm_separation") ?? "") : "";
                string conv = hasGeo ? (Ini.GetPersisted(dir, "convergence") ?? Ini.Get(dm, "dm_convergence") ?? "") : "";
                if (hasGl)
                {
                    up = glo.Upscale ? Lang.T("켬") : Lang.T("끔");
                    sep = glo.Separation.ToString(CultureInfo.InvariantCulture);
                    conv = glo.Auto ? "auto" : glo.Convergence.ToString(CultureInfo.InvariantCulture);
                }
                if (hasVk)
                {
                    up = "-";   // VK3D has no upscaling
                    sep = vko.Separation.ToString(CultureInfo.InvariantCulture);
                    conv = vko.Auto ? "auto" : vko.Convergence.ToString(CultureInfo.InvariantCulture);
                }
                if (hasDx12)
                {
                    up = "-";   // DX12_3D: the game's own render scale
                    sep = d12o.Separation.ToString(CultureInfo.InvariantCulture);
                    conv = d12o.Convergence.ToString(CultureInfo.InvariantCulture);
                }
                sep = Trim1(sep); conv = Trim1(conv);
                string launch = !exists ? "-" : hasTri ? Lang.T("TriDef 실행기") : Launchers.LaunchLabel(exe, Util.IsUe4(exe));
                if (hasTri) { up = "-"; sep = conv = "TriDef"; }
                bool installed = hasGeo || hasGl || hasVk || hasDx12 || hasTri;
                bool complete = rec.Done && installed;
                if (complete && exists) completeGames.Add(exe);
                string state = !exists ? Lang.T("파일 없음") : complete ? Lang.T("설정 완료") : Lang.T("재설정 필요");
                it.SubItems.Add(state).ForeColor = complete ? Color.FromArgb(16, 124, 16) : Color.FromArgb(200, 90, 0);
                var ce = exists ? Report.Find(exe) : null;
                it.SubItems.Add(Report.StatusText(ce)).ForeColor = CommunityColor(ce);
                foreach (var v in new[] { engine, bits.HasValue ? bits.Value.ToString() : "-", sr, up, sep, conv, launch, dir })
                    it.SubItems.Add(v).ForeColor = (!exists || !installed) ? TextMuted : TextMain;
                it.Tag = exe;
                if (!exists || !installed) it.ForeColor = TextMuted;
                lvGames.Items.Add(it);
            }
            lvGames.EndUpdate();
            UpdateReportButton();
        }

        // ---- community 3D list (Report.cs) ----

        static Color CommunityColor(Report.Entry e)
        {
            if (e == null) return TextMuted;
            return e.Status == "works" ? Color.FromArgb(16, 124, 16) : e.Status == "issues" ? Color.FromArgb(200, 90, 0) : Color.FromArgb(196, 43, 28);
        }

        // download compat.json in the background, then update the [3D 보고] column in place (keeps the selection)
        void FetchCommunity()
        {
            Report.FetchAsync(ok => { if (ok && IsHandleCreated && !IsDisposed) try { BeginInvoke((Action)UpdateCommunity); } catch { } });
        }

        void UpdateCommunity()
        {
            if (lvGames == null) return;
            foreach (ListViewItem it in lvGames.Items)
            {
                string exe = (string)it.Tag;
                var ce = File.Exists(exe) ? Report.Find(exe) : null;
                it.SubItems[2].Text = Report.StatusText(ce);
                it.SubItems[2].ForeColor = CommunityColor(ce);
            }
            UpdateReportButton();
        }

        // a game that is already in the list cannot be reported again; its status shows in the [3D 보고] column
        void UpdateReportButton()
        {
            if (btnReport == null) return;
            string exe = lvGames.SelectedItems.Count > 0 ? (string)lvGames.SelectedItems[0].Tag : null;
            btnReport.Enabled = exe == null || !File.Exists(exe) || Report.Find(exe) == null;
            if (btnLaunch != null) Theme.SetPrimary(btnLaunch, exe != null && completeGames.Contains(exe));   // ready to play: [실행] stands out
        }

        void ReportSelected()
        {
            var exe = SelectedGame();
            if (exe == null) return;
            if (!File.Exists(exe)) { Status(Lang.T("실행 파일이 없습니다: ") + exe); return; }
            var ce = Report.Find(exe);
            if (ce != null) { Status(Lang.T("이미 보고된 게임입니다: ") + ce.Game + " - " + Report.StatusText(ce)); return; }
            if (!Report.Installed(exe)) { Status(Lang.T("픽스를 설치한 게임만 보고할 수 있습니다. [설정 바꾸기]로 먼저 설치하세요.")); return; }
            var ok = MessageBox.Show(this, Lang.T("GitHub의 3D 동작 보고 양식을 브라우저로 엽니다.\n\n게임, 엔진, 설정 정보는 미리 채워집니다. [결과]에서 잘 됨 / 문제 있음 / 안 됨을 고르고 제출하세요. 1~2분 뒤 호환 게임 목록과 이 설치기의 [3D 보고] 열에 반영됩니다.\n\n제출하려면 GitHub 계정이 필요합니다. 폴더 경로는 보내지 않습니다."),
                Lang.T("3D 동작 보고"), MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (ok != DialogResult.OK) return;
            try
            {
                Process.Start(new ProcessStartInfo(Report.Url(exe, pkgDirs["dx9"], Report.PcgwLink(exe, candidates.Count > 0 ? candidates : Library.Load(CandidatesFile())))) { UseShellExecute = true });
                Log(Lang.T("보고 양식 열기: ") + Report.GameName(exe) + " (" + Report.Engine(exe) + ")");
                Status(Lang.T("브라우저에서 결과를 고르고 제출하세요: ") + Report.GameName(exe));
            }
            catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); Status(Lang.T("오류: ") + ex.Message); }
        }

        // "20.02425" -> "20.02" for the list; non-numbers pass through
        static string Trim1(string v)
        {
            decimal d;
            return decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d.ToString("0.##", CultureInfo.InvariantCulture) : v;
        }

        string SelectedGame()
        {
            if (lvGames.SelectedItems.Count == 0) { Status(Lang.T("게임 목록에서 게임을 먼저 고르세요.")); tabs.SelectedIndex = 0; return null; }
            return (string)lvGames.SelectedItems[0].Tag;
        }

        void LaunchSelected()
        {
            var exe = SelectedGame();
            if (exe == null) return;
            if (!File.Exists(exe)) { Status(Lang.T("실행 파일이 없습니다: ") + exe); return; }
            string dir = Path.GetDirectoryName(exe), appId, nm;
            bool ue4 = Util.IsUe4(exe);   // UF2 needs the game started with -dx11
            string tn = TriOf(exe);
            if (tn != null)
            {
                // TriDef 3D: Tridef3D_Play starts the game through Steam and injects TriDef (administrator rights)
                try
                {
                    TriOps.Launch(pkgDirs["tridef"], tn, TriOps.Sr(tn));
                    Log(Lang.T("TriDef 실행기로 실행: ") + tn + (TriOps.Sr(tn) ? " (--sr)" : ""));
                    Status(Lang.T("TriDef 실행기로 실행: ") + tn);
                }
                catch (System.ComponentModel.Win32Exception ex) { Log(Lang.T("실행 취소 또는 실패: ") + ex.Message); Status(Lang.T("실행 취소 또는 실패: ") + ex.Message); }
                catch (Exception ex) { Log(Lang.T("실행 실패: ") + ex.Message); Status(Lang.T("실행 실패: ") + ex.Message); }
                return;
            }
            try
            {
                if (Util.FindSteamApp(dir, out appId, out nm))
                {
                    // Steam applies the app's LaunchOptions (set by [완료] / [UE4 설정] for UE4 games)
                    if (ue4 && Launchers.LaunchLabel(exe, true).EndsWith(Launchers.Dx11) == false)
                        Log(Lang.T("  주의: Steam 시작 옵션에 -dx11이 없습니다. [UE4 설정]의 [실행기에 -dx11 넣기]를 쓰세요."));
                    Process.Start(new ProcessStartInfo("steam://rungameid/" + appId) { UseShellExecute = true });
                    Log(Lang.T("Steam으로 실행: ") + nm + " (appid " + appId + ")");
                    Status(Lang.T("Steam으로 실행: ") + nm);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = dir, Arguments = ue4 ? Launchers.Dx11 : "" });
                    Log(Lang.T("실행: ") + exe + (ue4 ? " " + Launchers.Dx11 : ""));
                    Status(Lang.T("실행: ") + Path.GetFileName(exe) + (ue4 ? " " + Launchers.Dx11 : ""));
                }
            }
            catch (Exception ex) { Log(Lang.T("실행 실패: ") + ex.Message); Status(Lang.T("실행 실패: ") + ex.Message); }
        }

        void EditSelected()
        {
            var exe = SelectedGame();
            if (exe == null) return;
            StartWizard(exe);
        }

        // 게임 추가: pick the exe, list it as 재설정 필요, start the wizard at 설치
        void AddGameDialog()
        {
            using (var dlg = new OpenFileDialog { Filter = Lang.T("실행 파일 (*.exe)|*.exe"), Title = Lang.T("설정할 게임의 실행 파일") })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                AddGameToList(dlg.FileName, false);
                RefreshGameList();
                StartWizard(dlg.FileName);
            }
        }

        void RemoveSelected()
        {
            var exe = SelectedGame();
            if (exe == null) return;
            games.RemoveAll(g => string.Equals(g.Exe, exe, StringComparison.OrdinalIgnoreCase));
            SaveGameList();
            RefreshGameList();
            Status(Lang.T("목록에서 뺐습니다 (게임 폴더는 그대로입니다)."));
        }

        // ---- kind tab (wizard step 1): geo-11 (Unity / 일반) or Unreal Engine 4 Universal Fix 2 ------------
        // The two groups differ a lot (package layout, hotkey file, UE4-only extras), so the choice is its own step.
        TabPage BuildKindTab()
        {
            FlowLayoutPanel p;
            var t = WizardTab(0, out p);

            p.Controls.Add(Head(Lang.T("선택한 게임")));
            lblKindGame = new Label { AutoSize = true, ForeColor = TextMain, Font = new Font(Font, FontStyle.Bold), Margin = P(0, 0, 0, 2), MaximumSize = Sz(1040, 0), Text = Lang.T("(아직 고르지 않았습니다 - [게임 목록] 탭의 [게임 추가]를 누르세요)") };
            p.Controls.Add(lblKindGame);
            lblKindDetect = Note(Lang.T("실행 파일을 보고 3D 방식을 자동으로 고릅니다. 다르면 아래에서 바꾸세요."));
            p.Controls.Add(lblKindDetect);

            // category: geo-11 (3DMigoto lineage, Direct3D 9 / 11) or our own wrappers (OGL3D / VK3D); below it only the game
            // types of that category (radio buttons of the two panels are kept exclusive by KindPicked)
            p.Controls.Add(Head(Lang.T("카테고리")));
            var catCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            rbCatGeo = Radio(Lang.T("geo-11 - DirectX 9 / 11 게임"), true);
            rbCatWrap = Radio(Lang.T("자체 래퍼 - Vulkan / OpenGL / DirectX 12 게임"), false);
            rbCatTri = Radio(Lang.T("TriDef 3D - TriDef 프로필이 있는 게임 (TriDef 3D 설치 필수)"), false);
            foreach (var r in new[] { rbCatGeo, rbCatWrap, rbCatTri }) { r.Font = new Font(Font, FontStyle.Bold); catCol.Controls.Add(r); }
            p.Controls.Add(catCol);
            p.Controls.Add(Note(Lang.T("geo-11은 커뮤니티 픽스(3DMigoto 계열)로 DirectX 9 / 11 게임을 입체로 만들고, 자체 래퍼(OGL3D / VK3D / DX12_3D)는 OpenGL·Vulkan·DirectX 12 게임의 양쪽 눈을 직접 그려 위빙합니다. 지원하지 않는 API의 게임은 DXVK로 Vulkan으로 바꿔 자체 래퍼를 쓸 수도 있습니다(실험적). TriDef 3D는 PC에 설치된 TriDef 3D가 게임별 프로필로 입체를 만듭니다(TriDef 3D 설치 필수).")));

            p.Controls.Add(Head(Lang.T("게임 유형")));
            var kindCol = pnlKindGeo = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            rbKindGeo = Radio(Lang.T("geo-11 - Unity 게임(Universal Fix) 또는 일반 게임(Preferred)"), true);
            rbKindGeo.Font = new Font(Font, FontStyle.Bold);
            kindCol.Controls.Add(rbKindGeo);
            kindCol.Controls.Add(Note(Lang.T("geo-11 드라이버 + 픽스 + SR 위빙. 64/32비트 패키지가 있고, Unity 게임은 Unity 버전에 맞는 include를 고릅니다. 단축키는 ShaderFixesDM\\hotkeys.ini에 씁니다.")));
            rbKindUe4 = Radio(Lang.T("Unreal Engine 4 - Universal Fix 2 (UF2)"), false);
            rbKindUe4.Font = new Font(Font, FontStyle.Bold);
            rbKindUe4.Margin = P(0, 10, 0, 2);
            kindCol.Controls.Add(rbKindUe4);
            kindCol.Controls.Add(Note(Lang.T("UE4 게임 전용(64비트). Universal Fix 2 패키지를 Binaries\\Win64에 설치하고 geo-11 드라이버 배치·HUD 프로필·d3dx.ini 정리를 설치기가 대신합니다. AA/AO 개선, 전체화면, VSync, HUD 프로필은 설치 뒤 [UE4 설정...] 창에서 바꿉니다. 단축키는 d3dxdm.ini에 씁니다.")));
            rbKindDx9 = Radio(Lang.T("DirectX 9 게임 - dgVoodoo2 + 일반 geo-11"), false);
            rbKindDx9.Font = new Font(Font, FontStyle.Bold);
            rbKindDx9.Margin = P(0, 10, 0, 2);
            kindCol.Controls.Add(rbKindDx9);
            kindCol.Controls.Add(Note(Lang.T("DX9 게임은 geo-11이 직접 못 다루므로 dgVoodoo2의 D3D9.dll로 D3D11로 변환하고 그 아래에 일반 geo-11(Preferred)과 SR 위빙을 둡니다. 다음 단계의 엔진은 일반 geo-11로 고정되고, 비트는 실행 파일에서 읽습니다(32비트가 대부분, 64비트 DX9도 됨).")));
            rowDx9 = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = P(24, 2, 0, 0) };
            rowDx9.Controls.Add(new Label { Text = Lang.T("dgVoodoo2 버전:"), AutoSize = true, Margin = P(0, 7, 8, 0) });
            var vers = Dx9Ops.Versions(pkgDirs["dx9"]);
            cbDx9Ver = Combo(120, vers.Length > 0 ? vers : new[] { Dx9Ops.DefaultVersion }, 0);
            rowDx9.Controls.Add(cbDx9Ver);
            rowDx9.Controls.Add(new Label { Text = Lang.T("2.79.1 = 가장 안정적(기본), 2.86.5 / 2.87.3 = 최신"), AutoSize = true, ForeColor = TextMuted, Margin = P(10, 7, 0, 0) });
            kindCol.Controls.Add(rowDx9);
            rowDx9Vram = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = P(24, 2, 0, 0) };
            rowDx9Vram.Controls.Add(new Label { Text = Lang.T("dgVoodoo VRAM:"), AutoSize = true, Margin = P(0, 7, 8, 0) });
            cbDx9Vram = Combo(90, Dx9Ops.VramChoices, 0);
            rowDx9Vram.Controls.Add(cbDx9Vram);
            rowDx9Vram.Controls.Add(new Label { Text = Lang.T("게임에 보고하는 비디오 메모리. 기본값 256MB면 패널 해상도에서 게임이 죽으므로 4GB(또는 2GB)로 둡니다."), AutoSize = true, ForeColor = TextMuted, Margin = P(10, 7, 0, 0), MaximumSize = Sz(400, 0) });
            kindCol.Controls.Add(rowDx9Vram);
            chkDx9Desktop = Check(Lang.T("바탕화면(패널) 해상도로 출력 (Resolution = desktop)"), true, 24);
            kindCol.Controls.Add(chkDx9Desktop);
            var dx9Res = Note(Lang.T("SR 패널에서 렌티큘러 무늬가 맞으려면 패널 원래 해상도로 출력해야 합니다. 끄면 게임이 고른 해상도 그대로 출력됩니다."));
            dx9Res.Margin = P(24, 0, 0, 4);
            kindCol.Controls.Add(dx9Res);
            // optional 4GB patch (LARGE_ADDRESS_AWARE) for a 32-bit exe that lacks it
            chkLaa = Check(Lang.T("4GB 패치 (실행 파일에 LARGE_ADDRESS_AWARE 플래그) - 원하면 켜세요"), false, 24);
            kindCol.Controls.Add(chkLaa);
            lblLaa = Note(Lang.T("32비트 게임은 주소 공간이 2GB뿐이라 dgVoodoo(패널 해상도) + geo-11(양안) + SR 위빙(복사본)을 더하면 메모리 부족으로 죽을 수 있습니다(\"Runtime Error\" 창, std::bad_alloc). 그럴 때 켜세요. 원본 exe는 <exe>.geo11sr_bak으로 보관하고 [3D 픽스 제거]가 되돌립니다. Steam '게임 파일 무결성 확인' 뒤에는 다시 적용해야 합니다."));
            lblLaa.Margin = P(24, 0, 0, 6);
            kindCol.Controls.Add(lblLaa);
            var dx9Overlay = new Label { AutoSize = true, ForeColor = Color.FromArgb(160, 90, 0), Margin = P(24, 0, 0, 6), MaximumSize = Sz(1000, 0),
                Text = Lang.T("알아 두기: DX9 게임에서는 화면 오버레이(숫자패드 0)가 표시되지 않습니다 - dgVoodoo 아래에서 geo-11이 오버레이를 만들지 못합니다. 단축키는 모두 동작합니다: Separation/Convergence 조절(Ctrl+F3~F6), 저장(Ctrl+F7), 3D 켜기/끄기(Ctrl+T), SRWeave(Ctrl+Alt+W/S/L/T).") };
            kindCol.Controls.Add(dx9Overlay);
            p.Controls.Add(pnlKindGeo);
            // our own wrappers, which render both eyes and weave by themselves (no geo-11)
            kindCol = pnlKindWrap = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Visible = false };
            rbKindGl = Radio(Lang.T("OpenGL 게임 - OGL3D (opengl32.dll)"), false);
            rbKindGl.Font = new Font(Font, FontStyle.Bold);
            kindCol.Controls.Add(rbKindGl);
            kindCol.Controls.Add(Note(Lang.T("OpenGL 게임은 geo-11이 아니라 OGL3D가 맡습니다. 게임 폴더에 opengl32.dll(비트는 실행 파일에서 읽음)과 OGL3D.ini를 넣으면 OGL3D가 양쪽 눈을 그려 SR SDK로 직접 위빙합니다. geo-11, SRWeave, dgVoodoo는 쓰지 않습니다. 자동 수렴이 기본이고, 값은 게임 안 단축키(Ctrl+F3~F7)로도 바꿉니다.")));
            rbKindVk = Radio(Lang.T("Vulkan 게임 - VK3D (vulkan-1.dll, 64비트)"), false);
            rbKindVk.Font = new Font(Font, FontStyle.Bold);
            rbKindVk.Margin = P(0, 10, 0, 2);
            kindCol.Controls.Add(rbKindVk);
            kindCol.Controls.Add(Note(Lang.T("Vulkan 게임은 VK3D가 맡습니다. 게임 폴더에 vulkan-1.dll과 VK3D.ini를 넣으면 VK3D가 양쪽 눈을 그려 SR SDK로 직접 위빙합니다. 게임 전용 픽스가 있으면(Quake II RTX) 함께 설치하고, 없으면 일반 입체(geo-11 방식)로 동작합니다. 64비트 게임만 됩니다.")));
            chkVkDxvk = Check(Lang.T("DirectX 9 / 11 게임을 DXVK로 Vulkan으로 바꿔 VK3D로 실행 (실험적)"), false, 24);
            chkVkDxvk.CheckedChanged += (s, e) => UpdateEnabled();
            kindCol.Controls.Add(chkVkDxvk);
            lblVkDxvk = Note(Lang.T("geo-11이 안 되는 64비트 DX9/DX11 게임용입니다(예: Lara Croft GO). DXVK의 d3d9 / d3d11 / dxgi / d3d10core.dll을 함께 넣습니다. geo-11이 되는 게임은 geo-11 쪽이 더 낫습니다(예: Unreal Engine 4 게임은 화면 효과가 어긋날 수 있음). 게임 폴더에 geo-11·SR 위빙·dgVoodoo·ReShade가 있으면 먼저 제거해야 합니다."));
            lblVkDxvk.Margin = P(24, 0, 0, 6);
            kindCol.Controls.Add(lblVkDxvk);
            rbKindDx12 = Radio(Lang.T("DirectX 12 게임 - DX12_3D (d3d12.dll, 64비트)"), false);
            rbKindDx12.Font = new Font(Font, FontStyle.Bold);
            rbKindDx12.Margin = P(0, 10, 0, 2);
            kindCol.Controls.Add(rbKindDx12);
            kindCol.Controls.Add(Note(Lang.T("DirectX 12 게임은 DX12_3D가 맡습니다. 게임 폴더에 d3d12.dll, DX12_3D.ini, DX12_3D 폴더(셰이더 패치용 DXC)를 넣으면 DX12_3D가 양쪽 눈을 그려 SR SDK로 직접 위빙합니다. 64비트 게임만 됩니다. 자동 수렴은 아직 없어 Convergence는 고정 값입니다. DLSS는 오른쪽 눈에 적용되지 않으므로 게임의 렌더 스케일이나 FSR / XeSS를 쓰세요. 언리얼 엔진 게임은 UU3D / UEVR 쪽이 더 낫습니다.")));
            p.Controls.Add(kindCol);
            // TriDef 3D: the user's own TriDef install does the 3D; this app registers the game and starts it
            pnlKindTri = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Visible = false };
            rbKindTri = Radio(Lang.T("TriDef 3D 프로필로 실행 (Steam 게임, 32비트 DX9 / 64비트 DX11)"), false);
            rbKindTri.Font = new Font(Font, FontStyle.Bold);
            pnlKindTri.Controls.Add(rbKindTri);
            var triMust = new Label { AutoSize = true, ForeColor = Color.FromArgb(196, 43, 28), Font = new Font(Font, FontStyle.Bold), Margin = P(0, 4, 0, 4), MaximumSize = Sz(1040, 0),
                Text = Lang.T("TriDef 3D 설치 필수: 이 방식은 PC에 설치된 TriDef 3D(DDD)가 3D를 만듭니다. 이 앱에는 TriDef 파일이 들어 있지 않으며(재배포 불가), TriDef 3D가 없으면 동작하지 않습니다.") };
            pnlKindTri.Controls.Add(triMust);
            pnlKindTri.Controls.Add(Note(Lang.T("TriDef 3D Ignition 창 대신 이 앱이 게임을 TriDef에 등록하고(프로필 지정), [실행]은 Tridef3D_Play로 게임을 시작해 TriDef DLL을 주입합니다(관리자 권한 확인 창이 뜹니다). 게임 폴더에는 아무것도 넣지 않습니다. Steam에서 바로 실행하면 3D가 되지 않으니 이 앱의 [실행]으로 시작하세요. 깊이는 TriDef 단축키로 조절합니다.")));
            lblTriState = new Label { AutoSize = true, Margin = P(0, 2, 0, 6), MaximumSize = Sz(1040, 0) };
            pnlKindTri.Controls.Add(lblTriState);
            var triRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = P(0, 2, 0, 0) };
            triRow.Controls.Add(Lbl(Lang.T("TriDef 프로필:")));
            var profs = new List<object> { Lang.T("(프로필 없음 - 목록에서 고르세요)") };
            foreach (var pn in TriOps.ProfileNames()) profs.Add(pn);
            cbTriProfile = Combo(420, profs.ToArray(), 0);
            triRow.Controls.Add(cbTriProfile);
            pnlKindTri.Controls.Add(triRow);
            pnlKindTri.Controls.Add(Note(Lang.T("실행 파일을 고르면 게임 이름(Steam 이름, 폴더, exe 이름)으로 TriDef 프로필을 찾아 자동으로 고릅니다. 프로필이 없는 게임은 화면만 좌우로 나뉘고 깊이가 없습니다.")));
            p.Controls.Add(pnlKindTri);
            p.Controls.Add(Note(Lang.T("3D 방식에 따라 다음 단계의 항목이 달라집니다. 방식마다 다른 패키지이므로 한 게임에 두 가지를 같이 설치하지 마세요.")));
            rbCatGeo.CheckedChanged += (s, e) => { if (rbCatGeo.Checked) CategoryPicked(0); };
            rbCatWrap.CheckedChanged += (s, e) => { if (rbCatWrap.Checked) CategoryPicked(1); };
            rbCatTri.CheckedChanged += (s, e) => { if (rbCatTri.Checked) CategoryPicked(2); };
            rbKindTri.CheckedChanged += (s, e) => { if (rbKindTri.Checked) { KindPicked(rbKindTri, 2); if (gameDir != null && chkSr != null) LoadTri(); } UpdateEnabled(); };
            rbKindGeo.CheckedChanged += (s, e) => { if (rbKindGeo.Checked) { KindPicked(rbKindGeo, 0); if (rbUe4 != null && rbUe4.Checked) rbPlain.Checked = true; } UpdateEnabled(); };
            rbKindUe4.CheckedChanged += (s, e) => { if (rbKindUe4.Checked) { KindPicked(rbKindUe4, 0); if (rbUe4 != null) rbUe4.Checked = true; } UpdateEnabled(); };
            rbKindDx9.CheckedChanged += (s, e) => { if (rbKindDx9.Checked) { KindPicked(rbKindDx9, 0); if (rbPlain != null) rbPlain.Checked = true; } UpdateEnabled(); };
            rbKindGl.CheckedChanged += (s, e) => { if (rbKindGl.Checked) { KindPicked(rbKindGl, 1); if (gameDir != null && chkSr != null) LoadWrapper(); } UpdateEnabled(); };
            rbKindVk.CheckedChanged += (s, e) => { if (rbKindVk.Checked) { KindPicked(rbKindVk, 1); if (gameDir != null && chkSr != null) LoadWrapper(); } UpdateEnabled(); };
            rbKindDx12.CheckedChanged += (s, e) => { if (rbKindDx12.Checked) { KindPicked(rbKindDx12, 1); if (gameDir != null && chkSr != null) LoadWrapper(); } UpdateEnabled(); };
            return t;
        }

        // ---- install tab (wizard step 2) --------------------------------------------------------
        TabPage BuildGameTab()
        {
            FlowLayoutPanel p;
            var t = WizardTab(1, out p);

            p.Controls.Add(Head(Lang.T("선택한 게임")));
            // the game was chosen in [게임 목록] → [게임 추가]; here it is only shown
            lblGame = new Label { AutoSize = true, ForeColor = TextMain, Font = new Font(Font, FontStyle.Bold), Margin = P(0, 0, 0, 2), MaximumSize = Sz(1040, 0), Text = Lang.T("(아직 고르지 않았습니다 - [게임 목록] 탭의 [게임 추가]를 누르세요)") };
            p.Controls.Add(lblGame);
            lblDetect = Note(Lang.T("실행 파일을 고르면 32/64비트, Unity 여부·버전, 기존 픽스(d3d11.dll), 다른 dxgi.dll(ReShade)을 자동으로 확인합니다."));
            p.Controls.Add(lblDetect);

            p.Controls.Add(Head(Lang.T("패키지")));
            // Radio buttons group by container, so each choice (engine / bits / scope) gets its own panel.
            var g = NewGrid(2);
            lblEngine = Lbl(Lang.T("엔진:"));
            g.Controls.Add(lblEngine, 0, 0);
            rowEngine = RadioRow();
            rbUnity = Radio("Unity (Universal Fix)", false);
            rbPlain = Radio(Lang.T("일반 geo-11 (Preferred)"), true);
            rbUe4 = Radio("Unreal Engine 4 (UF2)", false);   // mirrors the kind step; not shown (the kind step decides)
            rbUe4.Visible = false;
            rbUnity.CheckedChanged += (s, e) => UpdateEnabled();
            rbUe4.CheckedChanged += (s, e) => { if (rbKindUe4 != null && rbKindUe4.Checked != rbUe4.Checked) { rbKindUe4.Checked = rbUe4.Checked; rbKindGeo.Checked = !rbUe4.Checked; } UpdateEnabled(); };
            rowEngine.Controls.Add(rbUnity); rowEngine.Controls.Add(rbPlain); rowEngine.Controls.Add(rbUe4);
            g.Controls.Add(rowEngine, 1, 0);

            lblUnityVer = Lbl(Lang.T("Unity 버전:"));
            g.Controls.Add(lblUnityVer, 0, 1);
            rowUnityVer = RadioRow();
            cbUnityVer = Combo(160, UnityNames, UnityDefaultIndex);
            lblUnityDetected = new Label { AutoSize = true, ForeColor = TextMuted, Margin = P(0, 7, 0, 0) };
            rowUnityVer.Controls.Add(cbUnityVer); rowUnityVer.Controls.Add(lblUnityDetected);
            g.Controls.Add(rowUnityVer, 1, 1);

            g.Controls.Add(Lbl(Lang.T("비트:")), 0, 2);
            var bitRow = RadioRow();
            rb64 = Radio(Lang.T("64비트 (x64)"), true);
            rb32 = Radio(Lang.T("32비트 (x32)"), false);
            bitRow.Controls.Add(rb64); bitRow.Controls.Add(rb32);
            g.Controls.Add(bitRow, 1, 2);
            p.Controls.Add(g);

            lblGlInstall = Note(Lang.T("OpenGL (OGL3D): 실행 파일과 같은 비트의 opengl32.dll과 OGL3D.ini를 게임 폴더에 넣습니다. OGL3D.ini가 이미 있으면 게임 안에서 저장한 값과 셰이더 선택을 지키고, 다음 단계의 항목만 바꿉니다. 다른 opengl32.dll(다른 래퍼)이 있으면 opengl32.dll.geo11sr_bak으로 보관합니다."));
            lblGlInstall.Visible = false;
            p.Controls.Add(lblGlInstall);
            lblVkInstall = Note(Lang.T("Vulkan (VK3D): 64비트 vulkan-1.dll과 VK3D.ini를 게임 폴더에 넣습니다. 게임 전용 픽스가 있는 게임(Quake II RTX)은 VK3D_fix 폴더와 권장 값도 넣습니다. VK3D.ini가 이미 있으면 게임 안에서 저장한 값을 지키고, 다음 단계의 항목만 바꿉니다. 다른 vulkan-1.dll이 있으면 vulkan-1.dll.geo11sr_bak으로 보관합니다."));
            lblVkInstall.Visible = false;
            p.Controls.Add(lblVkInstall);
            lblDx12Install = Note(Lang.T("DirectX 12 (DX12_3D): 64비트 d3d12.dll, DX12_3D.ini, DX12_3D 폴더(dxcompiler.dll, dxil.dll)를 게임 폴더에 넣습니다. 권장 값이 있는 게임(Helldivers 2)은 그 값으로 시작합니다. DX12_3D.ini가 이미 있으면 게임 안에서 저장한 값을 지키고, 다음 단계의 항목만 바꿉니다. 다른 d3d12.dll이 있으면 d3d12.dll.geo11sr_bak으로 보관합니다."));
            lblDx12Install.Visible = false;
            p.Controls.Add(lblDx12Install);
            lblTriInstall = Note(Lang.T("TriDef 3D: 게임 폴더에는 아무것도 넣지 않고, TriDef 3D Ignition의 게임 목록(레지스트리 HKCU\\SOFTWARE\\DDD\\TriDefIgnition\\Games)에 이 게임을 고른 프로필과 함께 등록합니다. 비트는 실행 파일에서 읽습니다(32비트 = TriDef DX9, 64비트 = TriDef DX11)."));
            lblTriInstall.Visible = false;
            p.Controls.Add(lblTriInstall);
            pnlScope = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            pnlScope.Controls.Add(Head(Lang.T("설치 범위")));
            var scopeCol = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            rbFull = Radio(Lang.T("전체 설치 (geo-11 + 픽스 + SR 위빙) - 처음 설치하는 게임"), true);
            rbSbsOnly = Radio(Lang.T("SBS만 설치 (3D TV, Non-SR 모니터용) - geo-11 + 픽스, SR 위빙 없이 좌우(SBS) 출력"), false);
            // only offered when the game folder already has a geo-11 / 3DMigoto fix (DetectGame), so that fix is kept
            rbSrOnly = Radio(Lang.T("SR 위빙만 추가 (기존 픽스 유지) - 이미 geo-11/3DMigoto 픽스가 있는 게임"), false);
            rbSrOnly.Visible = false;
            scopeCol.Controls.Add(rbFull); scopeCol.Controls.Add(rbSbsOnly); scopeCol.Controls.Add(rbSrOnly);
            // full = SR weaving on, SBS only = off: kept in step with [SR 위빙 사용] of the display step
            rbFull.CheckedChanged += (s, e) => { if (rbFull.Checked && chkSr != null && !chkSr.Checked) chkSr.Checked = true; };
            rbSbsOnly.CheckedChanged += (s, e) => { if (rbSbsOnly.Checked && chkSr != null && chkSr.Checked) chkSr.Checked = false; };
            pnlScope.Controls.Add(scopeCol);
            pnlScope.Controls.Add(Note(Lang.T("SBS만 설치: geo-11과 픽스만 넣고 SR 위빙(dxgi.dll, SRWeave.ini)은 넣지 않습니다. 3D TV나 일반(Non-SR) 모니터에서 좌우(SBS) 화면으로 봅니다. 출력 모드(상하 등)는 다음 단계에서 바꿀 수 있습니다. 'SR 위빙만 추가'는 게임 폴더에 이미 geo-11/3DMigoto 픽스(d3d11.dll)가 있을 때만 나타나며, dxgi.dll + SRWeave.ini만 넣고 d3dx.ini / ShaderFixes는 건드리지 않습니다.")));
            p.Controls.Add(pnlScope);

            // UE4 only: Universal Fix 2 extras
            pnlUe4 = new Panel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), Padding = new Padding(0), Visible = false };
            var ue4Col = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            ue4Col.Controls.Add(Head(Lang.T("Unreal Engine 4 추가 설정 (Universal Fix 2)")));
            chkEngineIni = Check(Lang.T("AA/AO 개선 설치 (모션 블러·비네트·렌즈 플레어 끄기, 게임 AA 설정은 그대로)"), true, 0);
            ue4Col.Controls.Add(chkEngineIni);
            ue4Col.Controls.Add(Note(Lang.T("게임의 설정 폴더(%LOCALAPPDATA%\\<프로젝트>\\Saved\\Config\\WindowsNoEditor)의 Engine.ini와 Scalability.ini 끝에 패키지의 권장 설정을 붙이고 원본은 *_backup.ini로 보관합니다. 게임을 한 번도 실행하지 않아 그 폴더가 없으면 건너뛰고 로그에 남깁니다.")));
            btnUe4Tool = MakeButton(Lang.T("UE4 설정... (AA/AO, 전체화면, VSync, HUD 프로필)"), 260, false); btnUe4Tool.Height = S(32); btnUe4Tool.Margin = P(0, 4, 0, 0);
            btnUe4Tool.Click += (s, e) => OpenUe4Settings();
            ue4Col.Controls.Add(btnUe4Tool);
            ue4Col.Controls.Add(Note(Lang.T("Universal Fix 2의 설정 도구가 하던 항목을 명령창 없이 이 창에서 바꿉니다. [완료]로 설치한 뒤 필요할 때 여세요.")));
            var btnDx11 = MakeButton(Lang.T("실행기에 -dx11 넣기 (Steam 시작 옵션 / Heroic launcherArgs)"), 260, false); btnDx11.Height = S(32); btnDx11.Margin = P(0, 8, 0, 0);
            btnDx11.Click += (s, e) => { if (!RequireGame()) return; foreach (var l in Launchers.ApplyDx11(gameExe, this)) { Log(l); Status(l.Trim()); } RefreshGameList(); };
            ue4Col.Controls.Add(btnDx11);
            ue4Col.Controls.Add(Note(Lang.T("UF2는 게임을 -dx11 인자로 실행해야 합니다. [완료] 때 자동으로 넣으며, Steam 게임은 Steam의 시작 옵션에(Steam을 잠시 종료했다가 다시 시작), Epic/GOG 게임은 Heroic의 게임 설정(launcherArgs)에 씁니다. 이 설치기의 [실행]과 게임 목록의 실행 열에서 -dx11 여부를 볼 수 있습니다.")));
            pnlUe4.Controls.Add(ue4Col);
            p.Controls.Add(pnlUe4);
            return t;
        }

        // UE4 settings window (replaces launching 00_UE4-UniversalFix-2_Config.cmd)
        void OpenUe4Settings()
        {
            if (!RequireGame()) return;
            if (!File.Exists(Path.Combine(gameDir, "d3dx.ini")) || !Directory.Exists(Path.Combine(gameDir, "ShaderFixes")))
            { Status(Lang.T("UF2가 아직 설치되지 않았습니다. 먼저 [완료]로 설치하세요.")); return; }
            using (var dlg = new Ue4Dialog(gameDir, gameExe, Log, dpi))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) { Status(Lang.T("UE4 설정을 적용했습니다: ") + Path.GetFileName(gameExe)); RefreshGameList(); }
            }
        }

        bool IsUe4Selected { get { return rbUe4 != null && rbUe4.Checked; } }
        bool IsDx9Selected { get { return rbKindDx9 != null && rbKindDx9.Checked; } }
        bool IsGlSelected { get { return rbKindGl != null && rbKindGl.Checked; } }
        bool IsVkSelected { get { return rbKindVk != null && rbKindVk.Checked; } }
        bool IsDx12Selected { get { return rbKindDx12 != null && rbKindDx12.Checked; } }
        bool IsTriSelected { get { return rbKindTri != null && rbKindTri.Checked; } }

        // what 0000_Start_GEO11.cmd does after the package is copied, minus the interactive parts
        void Ue4PostInstall(string dir)
        {
            string geo = Path.Combine(dir, "ShaderFixes", "Geo11");
            string reset = Path.Combine(dir, "ShaderFixes", "ResetFix");
            if (Directory.Exists(geo))
            {
                Directory.CreateDirectory(reset);
                foreach (var pair in new[] { new[] { "d3d11.dll", "d3d11.dll" }, new[] { "d3d11.dll", "d3d11_loader.dll" }, new[] { "nvapi64.dll", "nvapi64.dll" } })
                {
                    string from = Path.Combine(geo, pair[0]);
                    if (!File.Exists(from)) continue;
                    File.Copy(from, Path.Combine(dir, pair[1]), true);
                    File.Copy(from, Path.Combine(reset, pair[1]), true);
                }
                Log(Lang.T("  UF2: geo-11 드라이버(ShaderFixes\\Geo11)를 게임 폴더에 배치"));
            }
            // HUD profile for this exe (z_HUDUIProfileInstaller.cmd)
            string profiles = Path.Combine(dir, "ShaderFixes", "AUTOHUDUICONFIGS");
            string list = Path.Combine(dir, "ShaderFixes", "z_GameProfileList");
            if (Directory.Exists(profiles))
            {
                string exeKey = Path.GetFileName(gameExe).Split('.', '-')[0];
                string chosen = "DEFAULT";
                if (File.Exists(list) && File.ReadAllText(list, Encoding.Default).IndexOf(exeKey, StringComparison.OrdinalIgnoreCase) >= 0 && File.Exists(Path.Combine(profiles, exeKey))) chosen = exeKey;
                string src = Path.Combine(profiles, chosen);
                if (File.Exists(src)) { File.Copy(src, Path.Combine(dir, "ShaderFixes", "AutoDepthUIHUD.ini"), true); Log(Lang.T("  UF2: HUD 프로필 ") + chosen); }
            }
            string dx = Path.Combine(dir, "d3dx.ini");
            if (File.Exists(dx))
            {
                Ini.Set(dx, "force_stereo", "2", "Device");
                Ini.Set(dx, "get_resolution_from", "large_2d_depth_stencil_if_swap_chain_native", "Device");
                Ini.SetInclude(dx, "ShaderFixes\\3dvision2sbs.ini", false);
                Log(Lang.T("  UF2: d3dx.ini force_stereo=2, get_resolution_from, 3dvision2sbs include 끔"));
            }
            foreach (var junk in new[] { "PathTemp", "LinkPathStorage", "GiveInstallState.txt", "z_RestoreTemp", "NewVersion" })
            { string p = Path.Combine(dir, junk); if (File.Exists(p)) File.Delete(p); }
            // the exe the UF2 tool would have chosen: lets its own 0_Start3D.cmd (start "" "<exe>" -dx11) work too
            File.WriteAllText(Path.Combine(dir, "z_ExeChoosenState"), Path.GetFileName(gameExe) + "\r\n", Encoding.Default);
        }

        FlowLayoutPanel RadioRow()
        {
            return new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = P(0, 2, 0, 2), Padding = new Padding(0) };
        }

        TabPage BuildSrTab()
        {
            FlowLayoutPanel p;
            var t = WizardTab(2, out p);
            geoUpscaleControls.Clear();
            lblSrHead = Head(Lang.T("SR 위빙 (SRWeave dxgi.dll)"));
            p.Controls.Add(lblSrHead);
            chkSr = Check(Lang.T("SR 위빙 사용 - 렌티큘러(무안경 3D) 패널용, ReShade 없이 SR SDK 위버로 직접 위빙"), true, 0);
            chkSr.CheckedChanged += (s, e) =>
            {
                // the install scope follows: SR on -> full install, SR off -> SBS only ('SR 위빙만 추가' is left as it is)
                if (chkSr.Checked && rbSbsOnly.Checked) rbFull.Checked = true;
                else if (!chkSr.Checked && rbFull.Checked) rbSbsOnly.Checked = true;
                UpdateEnabled();
            };
            p.Controls.Add(chkSr);
            chkSwap = Check(Lang.T("좌우 눈 바꾸기 (입체가 뒤집혀 보일 때)"), false, 24);
            chkLens = Check(Lang.T("위빙 중 렌티큘러 렌즈 켜기"), true, 24);
            p.Controls.Add(chkSwap); p.Controls.Add(chkLens);
            p.Controls.Add(Note(Lang.T("SR 런타임(SpatialLabs / SR Platform)이 설치돼 있고 SR Service가 실행 중이어야 합니다. 없으면 위빙만 꺼지고 게임은 SBS로 정상 실행됩니다.\n게임은 SR 패널에서 패널 원래 해상도로 전체화면 또는 테두리 없는 창으로 실행하세요.")));

            pnlGeoMode = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0) };
            pnlGeoMode.Controls.Add(Head(Lang.T("3D 출력 모드 (d3dxdm.ini direct_mode)")));
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            row.Controls.Add(Lbl("direct_mode:"));
            cbMode = Combo(200, DirectModes, 0);
            row.Controls.Add(cbMode);
            pnlGeoMode.Controls.Add(row);
            lblModeNote = Note(Lang.T("SR 위빙은 sbs(side-by-side) 입력이 필요하므로 sbs로 고정됩니다. SR 위빙을 끄면 다른 모드를 고를 수 있습니다."));
            pnlGeoMode.Controls.Add(lblModeNote);
            p.Controls.Add(pnlGeoMode);
            // OpenGL kind: OGL3D.ini [Stereo] output (auto = SR weaving, else one of these)
            pnlGlMode = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0), Visible = false };
            lblGlModeHead = Head(Lang.T("3D 출력 모드 (OGL3D.ini output)"));
            pnlGlMode.Controls.Add(lblGlModeHead);
            var glRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            glRow.Controls.Add(Lbl("output:"));
            cbGlMode = Combo(200, GlOps.Modes, 0);
            glRow.Controls.Add(cbGlMode);
            pnlGlMode.Controls.Add(glRow);
            lblGlModeNote = Note(Lang.T("SR 위빙을 켜면 output = auto (SR 런타임이 없으면 sbs)입니다. 끄면 sbs(좌우), tab(상하), left / right(한쪽 눈)를 고릅니다."));
            pnlGlMode.Controls.Add(lblGlModeNote);
            p.Controls.Add(pnlGlMode);

            lblUpHead = Head(Lang.T("업스케일 (성능이 모자랄 때)"));
            p.Controls.Add(lblUpHead);
            chkUpscale = Check(Lang.T("업스케일 사용"), false, 0);
            chkUpscale.CheckedChanged += (s, e) => UpdateEnabled();
            p.Controls.Add(chkUpscale);
            lblUpNote = Note(Lang.T("게임은 낮은 해상도(예: 1920×1080)로 그리고, geo-11이 패널 해상도로 키워서 보여 줍니다. 4K SR 패널에서 프레임이 모자랄 때 켜세요.\n") +
                Lang.T("켠 뒤에는 게임 안 그래픽 옵션에서 낮은 해상도를 고르면 됩니다. 끄면 게임이 고른 해상도 그대로 출력됩니다."));
            p.Controls.Add(lblUpNote);
            lblUpNoteGl = Note(Lang.T("OGL3D 업스케일: 게임은 자기 해상도(예: 1920×1080, 메뉴·글씨가 읽기 좋음)로 그리고, OGL3D가 게임 위의 자체 창에 패널 해상도로 위빙해 보여 줍니다. 켠 뒤 게임 설정에서 1920×1080을 고르세요. 해상도 변경은 막혀 패널은 원래 해상도를 유지합니다."));
            lblUpNoteGl.Visible = false;
            p.Controls.Add(lblUpNoteGl);
            geoUpscaleControls.Add(lblUpNote);

            // the one choice worth showing: compatibility (mode 1, default) vs. performance (mode 0). Index == upscale_mode.
            var modeRow = NewGrid(2);
            modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(250)));
            modeRow.Controls.Add(Lbl(Lang.T("키우는 방식:")), 0, 0);
            cbUpscaleMode = Combo(360, new object[] { Lang.T("성능 우선 - 조금 더 부드럽지만 안 되는 게임이 많음"), Lang.T("호환성 우선 - 대부분 게임에서 됨 (기본)") }, 1);
            modeRow.Controls.Add(cbUpscaleMode, 1, 0);
            p.Controls.Add(modeRow);
            var upModeNote = Note(Lang.T("호환성 우선: geo-11이 패널 해상도의 출력 버퍼를 따로 만들어 거기에 키워 넣습니다. 성능 우선: 게임의 출력 버퍼에 바로 키워 그립니다(복사가 한 번 적음). ") +
                Lang.T("성능 우선에서 화면이 검거나 깨지면 호환성 우선으로 돌리세요. SR 위빙은 둘 다 됩니다."));
            p.Controls.Add(upModeNote);

            chkUpAdv = Check(Lang.T("고급 설정 보기 (전체화면 처리, 출력 해상도)"), false, 0);
            p.Controls.Add(chkUpAdv);
            geoUpscaleControls.Add(modeRow); geoUpscaleControls.Add(upModeNote); geoUpscaleControls.Add(chkUpAdv);
            var g = NewGrid(2);
            gridUpAdv = g;
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(250)));
            g.Visible = false;
            chkUpAdv.CheckedChanged += (s, e) => g.Visible = chkUpAdv.Checked;
            g.Controls.Add(Lbl(Lang.T("전체화면 처리:")), 0, 0);
            cbUpscaleFs = Combo(360, new object[] { Lang.T("게임이 전체화면을 켜고 끌 수 있음 (기본)"), Lang.T("항상 전체화면 고정 - 마우스 커서가 이상할 때") }, 0);
            g.Controls.Add(cbUpscaleFs, 1, 0);
            g.Controls.Add(Lbl(Lang.T("출력 해상도 (0 = 바탕화면):")), 0, 1);
            var resRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            numUpW = new NumericUpDown { Minimum = 0, Maximum = 16384, Increment = 10, Width = S(90), BorderStyle = BorderStyle.FixedSingle, Margin = P(0, 3, 6, 3) };
            numUpH = new NumericUpDown { Minimum = 0, Maximum = 16384, Increment = 10, Width = S(90), BorderStyle = BorderStyle.FixedSingle, Margin = P(6, 3, 0, 3) };
            resRow.Controls.Add(numUpW); resRow.Controls.Add(Lbl("×")); resRow.Controls.Add(numUpH);
            g.Controls.Add(resRow, 1, 1);
            var advNote = Note(Lang.T("출력 해상도는 SR 패널 원래 해상도여야 무늬가 맞으므로 0(바탕화면)이 안전합니다. ") +
                Lang.T("고치는 항목: d3dx.ini [Device] upscaling / upscale_mode / width / height, [Include] ShaderFixes\\upscale.ini."));
            g.Controls.Add(advNote, 1, 2);
            p.Controls.Add(g);
            return t;
        }

        TabPage BuildStereoTab()
        {
            FlowLayoutPanel p;
            var t = WizardTab(3, out p);
            geoStereoNotes.Clear();
            lblStereoHead = Head(Lang.T("기본 입체 값 (d3dxdm.ini [Stereo])"));
            p.Controls.Add(lblStereoHead);
            var g = NewGrid(2);
            g.Controls.Add(Lbl(Lang.T("Separation (입체감, 0 - 100):")), 0, 0);
            numSep = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 1, Increment = 1, Value = 80, Width = S(100), BorderStyle = BorderStyle.FixedSingle, Margin = P(0, 3, 12, 3) };
            g.Controls.Add(numSep, 1, 0);
            g.Controls.Add(Lbl(Lang.T("Convergence (수렴):")), 0, 1);
            numConv = new NumericUpDown { Minimum = 0.01m, Maximum = 10000, DecimalPlaces = 2, Increment = 0.1m, Value = 3.0m, Width = S(100), BorderStyle = BorderStyle.FixedSingle, Margin = P(0, 3, 12, 3) };
            g.Controls.Add(numConv, 1, 1);
            p.Controls.Add(g);
            gridStereo = g;
            var geoConvNote = Note(Lang.T("dm_separation / dm_convergence. 게임 안에서 Separation·Convergence 증가/감소 키로 바꾸고 저장 키(기본 Ctrl+F7)로 저장할 수 있습니다. Convergence가 클수록 화면 밖으로 튀어나오는 느낌이 커집니다. 보통 1 - 8 정도이지만 픽스에 따라 100 이상을 쓰기도 합니다."));
            p.Controls.Add(geoConvNote);
            lblStereoGl = Note(Lang.T("OGL3D.ini separation / convergence. Separation은 깊이 세기(%, 기본 50)입니다. Convergence는 화면 면에 놓일 깊이로, 단위가 게임마다 크게 달라(예: 2 - 4000) 자동 수렴을 권합니다. 게임 안에서 Ctrl+F3~F6으로 바꾸고 Ctrl+F7로 저장하며, 수동 조절(Ctrl+F5/F6)은 자동 수렴을 끕니다. Ctrl+F2가 자동 수렴을 켜고 끕니다."));
            lblStereoGl.Visible = false;
            p.Controls.Add(lblStereoGl);
            lblStereoVk = Note(Lang.T("VK3D.ini separation / convergence. Separation은 깊이 세기(%, 기본 25)입니다. Convergence는 화면 면에 놓일 깊이(게임 단위)로 게임마다 크게 달라 자동 수렴을 권합니다. 게임 안에서 Ctrl+F3~F6으로 바꾸고 Ctrl+F7로 저장하며, 수동 조절(Ctrl+F5/F6)은 자동 수렴을 끕니다. Ctrl+F2가 자동 수렴을 켜고 끕니다."));
            lblStereoVk.Visible = false;
            p.Controls.Add(lblStereoVk);
            lblStereoDx12 = Note(Lang.T("DX12_3D.ini separation / convergence. Separation은 깊이 세기(%, 기본 25)입니다. Convergence는 화면 면에 놓일 깊이(게임 단위)로 게임마다 크게 다릅니다(Helldivers 2는 미터 단위라 2 - 5). DX12_3D는 아직 자동 수렴이 없습니다. 게임 안에서 Ctrl+F3~F6으로 바꾸고 Ctrl+F7로 저장합니다."));
            lblStereoDx12.Visible = false;
            p.Controls.Add(lblStereoDx12);
            lblStereoTri = Note(Lang.T("TriDef 3D는 고른 프로필의 깊이 값으로 시작합니다. 게임 안에서 TriDef 단축키로 깊이(분리값)와 화면 위치(수렴)를 바꾸고, TriDef가 프로필에 저장합니다. 이 단계에서 정할 값은 없습니다."));
            lblStereoTri.Visible = false;
            p.Controls.Add(lblStereoTri);
            chkAutoConv = Check(Lang.T("자동 수렴 (dm_auto_convergence) - 장면에 따라 geo-11이 수렴을 자동으로 맞춤"), false, 0);
            chkAutoConv.CheckedChanged += (s, e) => UpdateEnabled();
            p.Controls.Add(chkAutoConv);
            var geoAutoNote = Note(Lang.T("자동 수렴을 켜면 Convergence 증가/감소 키는 팝아웃 바이어스(dm_popout_bias)를 조절합니다."));
            p.Controls.Add(geoAutoNote);
            geoStereoNotes.Add(geoConvNote); geoStereoNotes.Add(geoAutoNote);
            return t;
        }

        TabPage BuildKeysTab()
        {
            var t = NewTab(Lang.T("단축키"));
            var p = NewColumn();
            t.Controls.Add(p);

            p.Controls.Add(Note(Lang.T("바꾸려면 입력란을 클릭한 뒤 원하는 키(조합)를 누르세요. 저장할 때 설정 파일 표기로 자동 변환됩니다. [지움]은 키를 비웁니다.")));

            p.Controls.Add(Head(Lang.T("geo-11 입체 조절")));
            p.Controls.Add(KeyGrid(GeoKeys, false));

            p.Controls.Add(Head(Lang.T("오버레이 (3DMigoto Hunting)")));
            p.Controls.Add(Note(Lang.T("오버레이는 게임 화면 위에 셰이더 정보와 조작 안내를 띄우는 3DMigoto 기능(Hunting 모드)입니다. 아래 키로 게임 중에 표시/숨김을 바꿉니다.")));
            var g = NewGrid(2);
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(KeyLabelWidth)));
            g.Controls.Add(Lbl(Lang.T("오버레이 모드:")), 0, 0);
            cbHunting = Combo(320, new object[] { Lang.T("0 - 사용 안 함 (가장 빠름, 키로도 못 켬)"), Lang.T("1 - 사용 (표시된 채 시작, 키로 숨김)"), Lang.T("2 - 사용 (숨긴 채 시작, 키로 표시)") }, 1);
            g.Controls.Add(cbHunting, 1, 0);
            p.Controls.Add(g);
            p.Controls.Add(KeyGrid(MigotoKeys, false));

            p.Controls.Add(Head(Lang.T("SR 위빙 (SRWeave) - 항상 Ctrl + Alt + 키")));
            p.Controls.Add(KeyGrid(SrKeys, true));

            var keyBar = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = P(0, 12, 0, 0), Padding = new Padding(0) };
            var btnDefaults = MakeButton(Lang.T("단축키 기본값으로"), 150, false); btnDefaults.Height = S(32);
            btnDefaults.Click += (s, e) => { ResetKeys(); cbHunting.SelectedIndex = 1; };
            var btnApplyKeys = MakeButton(Lang.T("현재 게임에 단축키만 적용"), 200, true); btnApplyKeys.Height = S(32);
            btnApplyKeys.Click += (s, e) => ApplyHotkeysOnly();
            keyBar.Controls.Add(btnDefaults); keyBar.Controls.Add(btnApplyKeys);
            p.Controls.Add(keyBar);
            p.Controls.Add(Note(Lang.T("단축키는 [완료]로 설정을 적용할 때 함께 저장됩니다. 이미 설정한 게임의 키만 바꾸려면 [게임 목록]에서 게임을 고르고 [설정 바꾸기] 뒤 이 버튼을 누르세요.")));
            return t;
        }

        const int KeyLabelWidth = 250;   // same label column in every hotkey section so the boxes line up

        void ResetKeys()
        {
            foreach (var k in GeoKeys.Concat(MigotoKeys)) keyBoxes[k.Id].Combo = KeyText.FromMigoto(k.Def);
            foreach (var k in SrKeys) keyBoxes[k.Id].Combo = KeyText.FromSrWeave(k.Def);
        }

        TableLayoutPanel KeyGrid(KeyDef[] keys, bool fixedCtrlAlt)
        {
            var g = NewGrid(3);
            g.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(KeyLabelWidth)));
            int r = 0;
            foreach (var k in keys)
            {
                g.Controls.Add(Lbl(k.Label), 0, r);
                var kb = new KeyBox { Width = S(260), BorderStyle = BorderStyle.FixedSingle, Margin = P(0, 3, 0, 3), FixedCtrlAlt = fixedCtrlAlt };
                kb.Combo = fixedCtrlAlt ? KeyText.FromSrWeave(k.Def) : KeyText.FromMigoto(k.Def);
                keyBoxes[k.Id] = kb;
                g.Controls.Add(kb, 1, r);
                var clear = MakeButton(Lang.T("지움"), 64, false);
                clear.Height = kb.Height + S(6); clear.Margin = P(6, 0, 0, 0); clear.Tag = kb;
                clear.Click += (s, e) => { ((KeyBox)((Button)s).Tag).Combo = null; };
                g.Controls.Add(clear, 2, r);
                r++;
            }
            return g;
        }

        TabPage BuildHelpTab()
        {
            var t = NewTab(Lang.T("안내"));
            var tb = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, ForeColor = TextMain, BorderStyle = BorderStyle.None, Font = new Font(Lang.Face, 9.75f), WordWrap = true };
            tb.Text = string.Join(Environment.NewLine, new[] {
                Lang.T("■ 사용 순서"),
                Lang.T("  1. [게임 목록] 탭에서 [게임 추가 (설정 시작)]을 누르고 게임 실행 파일을 고릅니다. 목록에 '재설정 필요'로 올라가고 1단계로 넘어갑니다."),
                Lang.T("  2. 1단계 [3D 방식]: 카테고리(geo-11 / 자체 래퍼)를 고르고 그 안의 게임 유형(geo-11: Unity / 일반, Unreal Engine 4, DirectX 9 - 자체 래퍼: OpenGL = OGL3D, Vulkan = VK3D, DirectX 12 = DX12_3D)을 고릅니다. 실행 파일로 자동으로 골라 주며, 다르면 바꿉니다. [다음]."),
                Lang.T("  3. 2단계 [설치]: 고른 게임이 위에 표시되고 비트, 엔진(Unity / 일반), 기존 픽스가 자동으로 잡힙니다. 확인하고 [다음]."),
                Lang.T("     UE4 게임은 Universal Fix 2 패키지를 Binaries\\Win64에 설치합니다. AA/AO 개선·전체화면·VSync·HUD 프로필 같은 게임별 설정은 설치 뒤 [UE4 설정...] 창에서 바꿉니다."),
                Lang.T("     UF2는 게임을 -dx11 인자로 실행해야 합니다. [완료] 때 Steam 시작 옵션(Steam을 잠시 종료했다가 다시 시작) 또는 Heroic(Epic/GOG)의 게임 설정에 -dx11을 넣고, 설치기의 [실행]도 -dx11을 붙입니다."),
                Lang.T("  4. 3단계 [디스플레이]: SR 위빙 사용 여부(SR 패널이 아니면 끄고 3D 출력 모드 선택), 필요하면 업스케일. [다음]."),
                Lang.T("     OpenGL 게임(게임 유형 'OpenGL - OGL3D')은 opengl32.dll + OGL3D.ini만 설치하고, 디스플레이·입체 값 단계의 항목을 OGL3D.ini에 씁니다(output, swap_eyes, upscale, separation, convergence)."),
                Lang.T("     TriDef 3D(카테고리 'TriDef 3D')는 TriDef 3D가 설치돼 있어야 합니다. 게임을 TriDef에 등록만 하고(게임 폴더는 그대로), [실행]은 Tridef3D_Play로 시작합니다(Steam 게임, 관리자 권한)."),
                Lang.T("     Vulkan 게임(게임 유형 'Vulkan - VK3D')은 vulkan-1.dll + VK3D.ini(+ 게임 전용 픽스)를 설치하고, 디스플레이·입체 값 단계의 항목을 VK3D.ini에 씁니다(output, swap_eyes, separation, convergence)."),
                Lang.T("  5. 4단계 [입체 값]: Separation / Convergence. [완료]를 누르면 게임 폴더에 모두 적용되고 목록이 '설정 완료'로 바뀝니다."),
                Lang.T("     중간에 [취소]하면 게임 폴더는 바뀌지 않고 목록에 '재설정 필요'로 남습니다. [완료] 전에는 아무것도 적용되지 않으므로, [설정 바꾸기]로 1단계부터 다시 하면 됩니다."),
                Lang.T("  6. [게임 목록]에서 실행(Steam 게임은 Steam으로), 설정 바꾸기, 폴더 열기, SR 위빙만 제거, 3D 픽스 제거를 할 수 있습니다."),
                Lang.T("  7. [단축키] 탭은 [완료]할 때 함께 저장됩니다. 이미 설정한 게임의 키만 바꾸려면 [설정 바꾸기] 뒤 [현재 게임에 단축키만 적용]."),
                "",
                Lang.T("■ 기본 단축키 ([단축키] 탭에서 입력란을 클릭하고 키를 눌러 바꿀 수 있습니다)"),
                Lang.T("  geo-11      Ctrl+T 3D 켜기/끄기 | Ctrl+F3 / Ctrl+F4 Separation 감소/증가 | Ctrl+F5 / Ctrl+F6 Convergence 감소/증가"),
                Lang.T("              Ctrl+F7 현재 값 저장 | Ctrl+F1 입체 값 오버레이"),
                Lang.T("  오버레이    숫자패드 0 오버레이(3DMigoto Hunting: 셰이더 정보·조작 안내) 표시/숨김 (오버레이 모드가 1 또는 2일 때)"),
                Lang.T("              F10 설정/픽스 다시 읽기 | F9 (누르는 동안) 픽스 끄기"),
                Lang.T("  SRWeave     Ctrl+Alt+W 위빙 켜기/끄기 | Ctrl+Alt+S 좌우 눈 바꾸기 | Ctrl+Alt+L 렌즈 켜기/끄기 | Ctrl+Alt+T 테스트 무늬(왼눈 빨강/오른눈 파랑)"),
                "",
                Lang.T("■ 적용이 고치는 파일 (게임 폴더)"),
                "  d3dxdm.ini               direct_mode, dm_separation, dm_convergence, dm_auto_convergence",
                Lang.T("  ShaderFixesDM\\hotkeys.ini  geo-11 단축키 (각 섹션의 Key =)"),
                Lang.T("  d3dx.ini                 hunting, toggle_hunting, reload_config, reload_fixes, show_original / upscaling, upscale_mode, width, height, include upscale.ini / Unity include 줄 (UnitySwitch.ps1)"),
                "  SRWeave.ini              weave, swap_eyes, lens, key_weave, key_swap, key_lens, key_test",
                Lang.T("  dxgi.dll                 SR 사용 시 SRWeave 복사. 다른 dxgi.dll(ReShade)은 dxgi.dll.reshade_bak으로 백업, SR을 끄면 되돌림"),
                Lang.T("  ShaderCache / ShaderCacheDM 는 전체 설치 때만 비웁니다 (SR만 추가·값 변경 때는 그대로 두어 다음 실행이 느려지지 않게)."),
                "",
                Lang.T("■ 실행 조건"),
                Lang.T("  SR 런타임(SpatialLabs / SR Platform)이 설치돼 있고 SR Service가 실행 중이어야 위빙됩니다. 없으면 SBS 그대로 나오고 게임은 정상 실행됩니다."),
                Lang.T("  게임은 SR 패널에서 패널 원래 해상도로 전체화면 또는 테두리 없는 창으로 실행하세요. 창 크기가 다르면 무늬가 어긋나고 SRWeave.log에 WARNING이 남습니다."),
                Lang.T("  32비트 게임은 x32의 32비트 SRWeave가 SR 런타임의 32비트 DLL(C:\\Program Files (x86)\\...\\Platform\\bin)을 씁니다."),
                "",
                Lang.T("■ 제거"),
                Lang.T("  [SR 위빙만 제거]  dxgi.dll, SRWeave.ini/.log, ShaderFixes\\SRWeave 삭제, ReShade 백업 복원. geo-11은 그대로."),
                Lang.T("  [3D 픽스 제거]       게임 폴더의 Uninstall.bat 실행 (geo-11, ShaderFixes, SRWeave 모두 삭제)."),
                Lang.T("  OpenGL (OGL3D)   [3D 픽스 제거]는 opengl32.dll, OGL3D.ini, OGL3D.log를 지우고, [SR 위빙만 제거]는 OGL3D.ini의 output을 sbs로 바꿉니다."),
                Lang.T("                   OGL3D 단축키: Ctrl+F3/F4 Separation, Ctrl+F5/F6 Convergence, Ctrl+F2 자동 수렴, Ctrl+F7 저장, Ctrl+F1 값 표시, Ctrl+T 3D, Ctrl+Alt+W 위빙"),
                Lang.T("  TriDef 3D        [3D 픽스 제거]는 이 앱이 만든 TriDef 등록을 지우고, [SR 위빙만 제거]는 SR 위빙 없이 실행하게 바꿉니다. 깊이는 TriDef 단축키로 조절합니다."),
                Lang.T("  Vulkan (VK3D)    [3D 픽스 제거]는 vulkan-1.dll, VK3D.ini, VK3D.log, VK3D_fix(와 DXVK 파일)를 지우고, [SR 위빙만 제거]는 VK3D.ini의 output을 sbs로 바꿉니다. 단축키는 OGL3D와 같습니다."),
                Lang.T("  DirectX 12 (DX12_3D)  [3D 픽스 제거]는 d3d12.dll, DX12_3D.ini, DX12_3D.log, DX12_3D 폴더, 덤프를 지우고, [SR 위빙만 제거]는 DX12_3D.ini의 output을 sbs로 바꿉니다. 단축키는 OGL3D와 같습니다(자동 수렴 Ctrl+F2 제외)."),
            });
            t.Controls.Add(tb);
            return t;
        }

        TabPage BuildLogTab()
        {
            var t = NewTab(Lang.T("로그"));
            txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, ForeColor = TextMain, BorderStyle = BorderStyle.None, Font = new Font("Consolas", 9.5f) };
            t.Controls.Add(txtLog);
            return t;
        }

        // ---- state -------------------------------------------------------------------------------
        void Log(string msg) { txtLog.AppendText(msg + Environment.NewLine); }
        void Status(string msg) { lblStatus.Text = msg; }

        void UpdateEnabled()
        {
            cbUnityVer.Enabled = rbUnity.Checked;
            bool gl = IsGlSelected, vk = IsVkSelected, dx12 = IsDx12Selected, w = gl || vk || dx12;   // w: one of our own wrappers (OGL3D / VK3D / DX12_3D)
            bool tri = IsTriSelected;   // TriDef 3D: nothing of this app goes into the game folder
            if (IsUe4Selected && !tri) { rb64.Checked = true; rb32.Enabled = false; rb64.Enabled = true; }   // UF2 is 64-bit only
            else if (w || tri) { rb32.Enabled = rb64.Enabled = false; }   // OGL3D / VK3D: the DLL must match the exe's bitness (read from the exe)
            else { rb32.Enabled = rb64.Enabled = true; }
            if (pnlUe4 != null) pnlUe4.Visible = IsUe4Selected && !w && !tri;
            // the engine / Unity-version rows belong to the geo-11 group only (DX9 = dgVoodoo2 + plain geo-11, engine fixed)
            if (rowEngine != null) lblEngine.Visible = rowEngine.Visible = lblUnityVer.Visible = rowUnityVer.Visible = !IsUe4Selected && !IsDx9Selected && !w && !tri;
            if (pnlScope != null) { pnlScope.Visible = !w && !tri; lblGlInstall.Visible = gl; lblVkInstall.Visible = vk; lblDx12Install.Visible = dx12; lblTriInstall.Visible = tri; }
            if (lblTriState != null) UpdateTriState();
            if (chkVkDxvk != null) { chkVkDxvk.Enabled = lblVkDxvk.Enabled = vk && vkDxvkPossible; if (!chkVkDxvk.Enabled) chkVkDxvk.Checked = false; }
            if (IsDx9Selected && rbPlain != null && !rbPlain.Checked) rbPlain.Checked = true;
            if (rowDx9 != null) { rowDx9.Enabled = IsDx9Selected; rowDx9Vram.Enabled = IsDx9Selected; chkDx9Desktop.Enabled = IsDx9Selected; lblLaa.Enabled = IsDx9Selected; if (!IsDx9Selected) chkLaa.Checked = false; }
            cbMode.Enabled = !chkSr.Checked;
            if (chkSr.Checked) cbMode.SelectedItem = "sbs";
            lblModeNote.Visible = chkSr.Checked;
            chkSwap.Enabled = chkLens.Enabled = chkSr.Checked;
            if (pnlGlMode != null)
            {
                // OpenGL / Vulkan kinds: OGL3D.ini / VK3D.ini output, swap_eyes (and upscale for OGL3D); the wrappers switch the lens themselves
                pnlGeoMode.Visible = !w && !tri; pnlGlMode.Visible = w;
                if (w) SetWrapperModes(vk ? VkOps.Modes : dx12 ? Dx12Ops.Modes : GlOps.Modes);
                lblGlModeHead.Text = vk ? Lang.T("3D 출력 모드 (VK3D.ini output)") : dx12 ? Lang.T("3D 출력 모드 (DX12_3D.ini output)") : Lang.T("3D 출력 모드 (OGL3D.ini output)");
                lblGlModeNote.Text = vk || dx12 ? Lang.T("SR 위빙을 켜면 output = auto (SR 런타임이 없으면 sbs)입니다. 끄면 sbs(좌우) 또는 tab(상하)을 고릅니다.")
                                        : Lang.T("SR 위빙을 켜면 output = auto (SR 런타임이 없으면 sbs)입니다. 끄면 sbs(좌우), tab(상하), left / right(한쪽 눈)를 고릅니다.");
                cbGlMode.Enabled = !chkSr.Checked;
                if (w) chkSwap.Enabled = true;
                chkLens.Visible = !w && !tri;
                chkSwap.Visible = !tri;   // TriDef: SRWeave's own hotkey (Ctrl+Alt+S) swaps the eyes
                lblSrHead.Text = tri ? Lang.T("SR 위빙 (TriDef + SRWeave)") : gl ? Lang.T("SR 위빙 (OGL3D)") : vk ? Lang.T("SR 위빙 (VK3D)") : dx12 ? Lang.T("SR 위빙 (DX12_3D)") : Lang.T("SR 위빙 (SRWeave dxgi.dll)");
                foreach (var c in geoUpscaleControls) c.Visible = !w && !tri;
                gridUpAdv.Visible = !w && !tri && chkUpAdv.Checked;
                lblUpNoteGl.Visible = gl;
                lblUpHead.Visible = chkUpscale.Visible = !vk && !dx12 && !tri;   // VK3D / DX12_3D / TriDef have no upscaling here
                if (vk || dx12 || tri) chkUpscale.Checked = false;
            }
            if (lblStereoGl != null)
            {
                foreach (var c in geoStereoNotes) c.Visible = !w && !tri;
                lblStereoTri.Visible = tri;
                gridStereo.Visible = !tri;
                lblStereoGl.Visible = gl;
                lblStereoVk.Visible = vk;
                lblStereoDx12.Visible = dx12;
                lblStereoHead.Text = tri ? Lang.T("입체 값 (TriDef 프로필)") : gl ? Lang.T("기본 입체 값 (OGL3D.ini [Stereo])") : vk ? Lang.T("기본 입체 값 (VK3D.ini [Stereo])") : dx12 ? Lang.T("기본 입체 값 (DX12_3D.ini [Stereo])") : Lang.T("기본 입체 값 (d3dxdm.ini [Stereo])");
                chkAutoConv.Visible = !dx12 && !tri;   // DX12_3D has no automatic convergence yet; TriDef keeps its own
                if (dx12 || tri) chkAutoConv.Checked = false;
                chkAutoConv.Text = gl ? Lang.T("자동 수렴 (convergence = auto) - 장면에 따라 OGL3D가 수렴을 맞춤 (권장)")
                                 : vk ? Lang.T("자동 수렴 (convergence = auto) - 장면에 따라 VK3D가 수렴을 맞춤 (권장)")
                                 : Lang.T("자동 수렴 (dm_auto_convergence) - 장면에 따라 geo-11이 수렴을 자동으로 맞춤");
                numConv.Enabled = !(w && chkAutoConv.Checked);
            }
            foreach (var k in SrKeys) keyBoxes[k.Id].Enabled = chkSr.Checked;
            if (chkUpscale != null)
                cbUpscaleFs.Enabled = cbUpscaleMode.Enabled = numUpW.Enabled = numUpH.Enabled = chkUpscale.Checked;
        }

        // TriDef 3D: installed or not (required), and whether the launcher can start this game (Steam only)
        void UpdateTriState()
        {
            string drv = TriOps.Installed() ? TriOps.DriverDir() : null;
            string appId, nm;
            bool steam = gameDir != null && Util.FindSteamApp(gameDir, out appId, out nm);
            var lines = new List<string>();
            lines.Add(drv != null ? Lang.T("TriDef 3D 설치됨: ") + drv : Lang.T("이 PC에 TriDef 3D가 설치돼 있지 않습니다. TriDef 3D를 설치해야 이 방식으로 3D가 됩니다."));
            if (gameDir != null && !steam) lines.Add(Lang.T("Steam 게임이 아닙니다: TriDef 실행기는 Steam 게임만 시작할 수 있습니다."));
            lblTriState.Text = string.Join("\n", lines);
            lblTriState.ForeColor = drv != null && (gameDir == null || steam) ? Color.FromArgb(16, 124, 16) : Color.FromArgb(196, 43, 28);
        }

        // output choices of the wrapper kinds (OGL3D: sbs/tab/left/right, VK3D: sbs/tab); keeps the choice when possible
        void SetWrapperModes(string[] modes)
        {
            bool same = cbGlMode.Items.Count == modes.Length;
            for (int i = 0; same && i < modes.Length; i++) same = (string)cbGlMode.Items[i] == modes[i];
            if (same) return;
            var cur = cbGlMode.SelectedItem as string;
            cbGlMode.Items.Clear();
            foreach (var m in modes) cbGlMode.Items.Add(m);
            cbGlMode.SelectedItem = Array.IndexOf(modes, cur) >= 0 ? cur : modes[0];
        }

        string SrcDir()
        {
            if (IsUe4Selected) return pkgDirs["ue4"];
            return Path.Combine(pkgDirs[rbUnity.Checked ? "unity" : "plain"], rb64.Checked ? "x64" : "x32");
        }

        // geo-11 stereo hotkeys: ShaderFixesDM\hotkeys.ini in the Unity/Preferred packages, d3dxdm.ini itself in UF2
        static string GeoKeyFile(string dir)
        {
            string hk = Path.Combine(dir, "ShaderFixesDM", "hotkeys.ini");
            return File.Exists(hk) ? hk : Path.Combine(dir, "d3dxdm.ini");
        }

        void LoadExisting(string dir)
        {
            string dm = Path.Combine(dir, "d3dxdm.ini");
            string v;
            v = Ini.Get(dm, "direct_mode"); if (v != null && DirectModes.Contains(v)) cbMode.SelectedItem = v;
            decimal d;
            // values saved in-game (d3dx_user.ini) override d3dxdm.ini, so show those when present
            v = Ini.GetPersisted(dir, "separation") ?? Ini.Get(dm, "dm_separation");
            if (v != null && decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) numSep.Value = Math.Min(numSep.Maximum, Math.Max(numSep.Minimum, d));
            v = Ini.GetPersisted(dir, "convergence") ?? Ini.Get(dm, "dm_convergence");
            if (v != null && decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out d)) numConv.Value = Math.Min(numConv.Maximum, Math.Max(numConv.Minimum, d));
            if (Ini.GetPersisted(dir, "convergence") != null || Ini.GetPersisted(dir, "separation") != null)
                Log(Lang.T("  d3dx_user.ini에 게임 안에서 저장한 입체 값이 있어 그 값을 보여 줍니다 (적용하면 그 파일도 함께 바꿉니다)."));
            v = Ini.Get(dm, "dm_auto_convergence"); if (v != null) chkAutoConv.Checked = v.Trim() != "0";
            string hk = GeoKeyFile(dir);
            foreach (var k in GeoKeys) { v = Ini.GetSectionKey(hk, k.Id); LoadKey(k, v, false); }
            string dx = Path.Combine(dir, "d3dx.ini");
            v = Ini.Get(dx, "hunting"); int h; if (v != null && int.TryParse(v.Trim(), out h) && h >= 0 && h <= 2) cbHunting.SelectedIndex = h;
            foreach (var k in MigotoKeys) { v = Ini.Get(dx, k.Id); LoadKey(k, v, false); }
            int n;
            v = Ini.Get(dx, "upscaling");
            if (v != null && int.TryParse(v.Trim(), out n)) { chkUpscale.Checked = n > 0; if (n == 2) cbUpscaleFs.SelectedIndex = 1; else if (n == 1) cbUpscaleFs.SelectedIndex = 0; }
            v = Ini.Get(dx, "upscale_mode"); if (v != null && int.TryParse(v.Trim(), out n) && (n == 0 || n == 1)) cbUpscaleMode.SelectedIndex = n;
            v = Ini.Get(dx, "width"); numUpW.Value = (v != null && int.TryParse(v.Trim(), out n) && n >= 0 && n <= numUpW.Maximum) ? n : 0;
            v = Ini.Get(dx, "height"); numUpH.Value = (v != null && int.TryParse(v.Trim(), out n) && n >= 0 && n <= numUpH.Maximum) ? n : 0;
            string sr = Path.Combine(dir, "SRWeave.ini");
            if (File.Exists(sr))
            {
                v = Ini.Get(sr, "swap_eyes"); if (v != null) chkSwap.Checked = v.Trim() != "0";
                v = Ini.Get(sr, "lens"); if (v != null) chkLens.Checked = v.Trim() != "0";
                foreach (var k in SrKeys) { v = Ini.Get(sr, k.Id); LoadKey(k, v, true); }
            }
        }

        void LoadKey(KeyDef k, string iniValue, bool srWeave)
        {
            if (iniValue == null) return;
            var c = srWeave ? KeyText.FromSrWeave(iniValue) : KeyText.FromMigoto(iniValue);
            if (c == null && !(srWeave && iniValue.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)))
            {
                Log("  " + k.Id + " = \"" + iniValue + Lang.T("\" 은(는) 읽을 수 없는 키 표기라 기본값을 보여 줍니다."));
                return;
            }
            keyBoxes[k.Id].Combo = c;
        }

        void DetectGame(string exePath)
        {
            if (!File.Exists(exePath)) { lblDetect.Text = Lang.T("파일이 없습니다: ") + exePath; Status(Lang.T("파일이 없습니다.")); return; }
            gameExe = exePath;
            gameDir = Path.GetDirectoryName(exePath);
            lblGame.Text = Util.GameDisplayName(exePath) + "   " + exePath;
            lblKindGame.Text = lblGame.Text;
            var notes = new List<string>();
            var bits = Util.ExeBits(exePath);
            if (bits == 32) rb32.Checked = true; else if (bits == 64) rb64.Checked = true;
            notes.Add(bits.HasValue ? bits.Value + Lang.T("비트") : Lang.T("비트 알 수 없음"));
            string uv = Util.UnityVersion(gameDir, exePath);
            if (uv != null)
            {
                rbUnity.Checked = true;
                cbUnityVer.SelectedIndex = UnityIndexFor(uv);
                lblUnityDetected.Text = uv == "unknown" ? Lang.T("버전을 읽지 못함 - 직접 고르세요") : Lang.T("감지: Unity ") + uv;
                notes.Add("Unity " + uv);
            }
            else if (Util.IsUe4(exePath))
            {
                rbUe4.Checked = true; lblUnityDetected.Text = "";
                notes.Add(Lang.T("Unreal Engine 4 (Universal Fix 2 패키지)"));
            }
            else
            {
                rbPlain.Checked = true; lblUnityDetected.Text = "";
                notes.Add(Lang.T("Unity 아님 (일반 geo-11)"));
            }
            // OpenGL (imports opengl32.dll) or an existing OGL3D install -> the OpenGL kind. Checked before DX9: an
            // OpenGL exe can still name d3d9.dll (Broken Age), which the DLL-name fallback below would take for DX9.
            lastGeoKind = lastWrapKind = null;
            string anti = Library.AntiCheat(gameDir);
            if (anti != null) notes.Add(Lang.T("안티치트 있음(") + anti + Lang.T(") - 설치하지 않습니다"));
            bool gl = GlOps.Installed(gameDir) || (uv == null && !rbUe4.Checked && GlOps.IsOpenGL(exePath));   // an existing install wins
            // Vulkan (imports vulkan-1.dll and no Direct3D) or an existing VK3D install (also DX games run through DXVK) -> the Vulkan kind
            bool vk = !gl && (VkOps.Installed(gameDir) || (uv == null && !rbUe4.Checked && VkOps.ImportsVulkan(exePath) && !VkOps.ImportsDirect3D(exePath)));
            // DirectX 9 (static import or DLL name in the exe) or an existing dgVoodoo2 install -> the DX9 kind
            string api = Util.DirectXOf(exePath);
            // DirectX 12 only (no DX11 renderer) or an existing DX12_3D install -> the DirectX 12 kind
            bool dx12 = !gl && !vk && (Dx12Ops.Installed(gameDir) || (uv == null && !rbUe4.Checked && Dx12Ops.IsDx12Game(exePath)));
            bool dx9 = !gl && !vk && !dx12 && uv == null && !rbUe4.Checked && (api == "DX9" || Dx9Ops.Installed(gameDir));
            if (gl) { notes.Remove(Lang.T("Unity 아님 (일반 geo-11)")); notes.Add("OpenGL"); }
            else if (vk) { notes.Remove(Lang.T("Unity 아님 (일반 geo-11)")); notes.Add(VkOps.DxvkInstalled(gameDir) ? "Vulkan (DXVK, " + (api ?? "?") + ")" : "Vulkan"); }
            else if (dx12) { notes.Remove(Lang.T("Unity 아님 (일반 geo-11)")); notes.Add("DirectX 12"); }
            else if (api != null) notes.Add(api);
            vkDxvkPossible = bits == 64 && api != null && !Dx12Ops.IsDx12Game(exePath) && !VkOps.ImportsVulkan(exePath);   // DXVK has no D3D12
            string triReg = TriOps.RegisteredName(exePath);
            string triProfile = (triReg != null ? TriOps.Profile(triReg) : null) ?? TriOps.Match(exePath);
            string sid, snm;
            bool steamGame = Util.FindSteamApp(gameDir, out sid, out snm);
            bool otherInstall = GlOps.Installed(gameDir) || VkOps.Installed(gameDir) || Dx12Ops.Installed(gameDir) || Dx9Ops.Installed(gameDir) || File.Exists(Path.Combine(gameDir, "d3d11.dll"));
            // TriDef 3D only renders Direct3D 9 / 11: not for OpenGL, Vulkan or DirectX 12-only games
            bool tri = !otherInstall && (triReg != null || (triProfile != null && steamGame && !gl && !vk && !dx12));
            if (tri) { gl = vk = dx12 = dx9 = false; notes.Remove(Lang.T("Unity 아님 (일반 geo-11)")); }
            if (triProfile != null) notes.Add(Lang.T("TriDef 프로필: ") + triProfile + (TriOps.Installed() ? "" : Lang.T(" (TriDef 3D 설치 필수)")));
            if (triReg != null) notes.Add(Lang.T("TriDef 등록됨: ") + triReg);
            if (cbTriProfile != null)
                cbTriProfile.SelectedIndex = triProfile != null && cbTriProfile.Items.Contains(triProfile) ? cbTriProfile.Items.IndexOf(triProfile) : 0;
            rbKindUe4.Checked = rbUe4.Checked; rbKindDx9.Checked = dx9; rbKindGl.Checked = gl; rbKindVk.Checked = vk; rbKindDx12.Checked = dx12; rbKindGeo.Checked = !rbUe4.Checked && !dx9 && !gl && !vk && !dx12;
            rbKindTri.Checked = tri;   // last: KindPicked clears the other categories' types
            // 4GB patch: only meaningful for a 32-bit exe without the flag; off unless the user wants it
            bool? laa = bits == 32 ? Util.IsLargeAddressAware(exePath) : null;
            chkLaa.Enabled = laa == false;
            chkLaa.Checked = false;
            chkLaa.Text = laa == true ? Lang.T("4GB 패치 - 이미 적용되어 있음") : bits == 64 ? Lang.T("4GB 패치 - 64비트 게임에는 필요 없음") : Lang.T("4GB 패치 (실행 파일에 LARGE_ADDRESS_AWARE 플래그) - 원하면 켜세요");
            if (laa == false && !gl && !vk && !dx12 && !tri) notes.Add(Lang.T("2GB 주소 공간 (LAA 없음)"));   // the 4GB patch is offered with the DX9 kind
            if (dx9)
            {
                string iv = Dx9Ops.InstalledVersion(gameDir, pkgDirs["dx9"], bits ?? 32);
                if (iv != null && cbDx9Ver.Items.Contains(iv)) cbDx9Ver.SelectedItem = iv;
                var dres = Dx9Ops.DesktopResolution(gameDir);
                if (dres.HasValue) chkDx9Desktop.Checked = dres.Value;
                string vr = Dx9Ops.Vram(gameDir);
                if (vr != null && cbDx9Vram.Items.Contains(vr)) cbDx9Vram.SelectedItem = vr;
            }
            lblKindDetect.Text = Lang.T("자동 감지: ") + (tri ? Lang.T("TriDef 프로필 있음 → TriDef 3D (TriDef 3D 설치 필수)") : gl ? Lang.T("OpenGL (opengl32.dll) → OGL3D") : vk ? Lang.T("Vulkan (vulkan-1.dll) → VK3D") : dx12 ? Lang.T("DirectX 12 (d3d12.dll) → DX12_3D") : rbUe4.Checked ? Lang.T("Unreal Engine 4 (Binaries\\Win64\\*-Win64-Shipping.exe 구조)") : uv != null ? "Unity " + uv : dx9 ? Lang.T("DirectX 9 (d3d9.dll) → dgVoodoo2 + 일반 geo-11") : Lang.T("Unity 아님 (일반 geo-11)") + (api != null ? " / " + api : "")) + Lang.T(" - 다르면 아래에서 바꾸세요.");
            bool hasFix = File.Exists(Path.Combine(gameDir, "d3d11.dll"));
            rbSrOnly.Visible = hasFix;
            if (hasFix) { notes.Add(Lang.T("기존 d3d11.dll(geo-11/3DMigoto) 있음 → 'SR 위빙만 추가' 권장")); rbSrOnly.Checked = true; LoadExisting(gameDir); }
            else
            {
                rbFull.Checked = true;
                // show the package's own defaults (UF2 ships with upscaling on, its own keys, etc.)
                string pkg = SrcDir();
                if (Directory.Exists(pkg)) LoadExisting(pkg);
            }
            string dx = Path.Combine(gameDir, "dxgi.dll");
            if (File.Exists(dx))
                notes.Add(Util.IsSrWeave(dx, Path.Combine(SrcDir(), "dxgi.dll")) ? Lang.T("SRWeave dxgi.dll 이미 설치됨") : Lang.T("다른 dxgi.dll(ReShade?) 있음 → 설치 시 dxgi.dll.reshade_bak으로 백업"));
            if (tri) LoadTri();
            if (dx12)
            {
                LoadWrapper();
                string v = Dx12Ops.Version(Path.Combine(gameDir, Dx12Ops.Dll));
                if (v != null) notes.Add(Lang.T("DX12_3D 설치됨 ") + v);
                else if (File.Exists(Path.Combine(gameDir, Dx12Ops.Dll))) notes.Add(Lang.T("다른 d3d12.dll 있음 → 설치 시 d3d12.dll.geo11sr_bak으로 보관"));
                if (Dx12Ops.Profile(pkgDirs["dx12"], exePath) != null) notes.Add(Lang.T("게임 전용 권장 값 있음"));
                if (bits == 32) notes.Add(Lang.T("32비트 - DX12_3D는 64비트 게임만 지원"));
            }
            if (vk)
            {
                LoadWrapper();
                string v = VkOps.Version(Path.Combine(gameDir, VkOps.Dll));
                if (v != null) notes.Add(Lang.T("VK3D 설치됨 ") + v);
                if (VkOps.Profile(pkgDirs["vk3d"], exePath) != null) notes.Add(Lang.T("게임 전용 픽스 있음 (VK3D_fix)"));
                if (bits == 32) notes.Add(Lang.T("32비트 - VK3D는 64비트 게임만 지원"));
            }
            if (gl)
            {
                LoadWrapper();
                string v = GlOps.Version(Path.Combine(gameDir, GlOps.Dll));
                if (v != null) notes.Add(Lang.T("OGL3D 설치됨 ") + v);
                else if (File.Exists(Path.Combine(gameDir, GlOps.Dll))) notes.Add(Lang.T("다른 opengl32.dll 있음 → 설치 시 opengl32.dll.geo11sr_bak으로 보관"));
            }
            lblDetect.Text = string.Join("  |  ", notes);
            UpdateEnabled();
            Log(Lang.T("게임: ") + exePath);
            Log("  " + string.Join(" | ", notes));
            Status(Path.GetFileName(exePath) + " - " + string.Join(", ", notes));
        }

        // "2019.4.31" -> index of the matching Unity_*.ini (same rules as UnitySwitch.ps1 auto)
        static int UnityIndexFor(string ver)
        {
            var m = Regex.Match(ver ?? "", @"^(\d+)\.(\d+)");
            if (!m.Success) return UnityDefaultIndex;
            int major = int.Parse(m.Groups[1].Value), minor = int.Parse(m.Groups[2].Value);
            if (major >= 2019) return 3;
            if (major >= 2017) return 2;
            if (major == 5 && minor >= 6) return 1;
            if (major == 5) return 0;
            return UnityDefaultIndex;
        }

        // TriDef 3D: SR weaving as registered (on for a new game); bitness from the exe
        void LoadTri()
        {
            string reg = TriOps.RegisteredName(gameExe);
            chkSr.Checked = reg == null || TriOps.Sr(reg);
            chkUpscale.Checked = false;
            chkAutoConv.Checked = false;
            var bits = Util.ExeBits(gameExe);
            if (bits == 64) rb64.Checked = true; else if (bits == 32) rb32.Checked = true;
        }

        // OpenGL / Vulkan kinds: show the game's OGL3D.ini / VK3D.ini (or the package's defaults) in the display / stereo steps
        void LoadWrapper()
        {
            if (IsDx12Selected)
            {
                string dini = Path.Combine(gameDir, Dx12Ops.IniName), profile = null;
                if (!File.Exists(dini)) { dini = Path.Combine(pkgDirs["dx12"], Dx12Ops.IniName); profile = Dx12Ops.Profile(pkgDirs["dx12"], gameExe); }
                var d = Dx12Ops.Read(dini, profile);
                chkSr.Checked = d.Sr;
                SetWrapperModes(Dx12Ops.Modes);
                cbGlMode.SelectedItem = d.Mode;
                chkSwap.Checked = d.Swap;
                chkUpscale.Checked = false;
                numSep.Value = Math.Min(numSep.Maximum, Math.Max(numSep.Minimum, d.Separation));
                numConv.Value = Math.Min(numConv.Maximum, Math.Max(numConv.Minimum, d.Convergence));
                chkAutoConv.Checked = false;
                rb64.Checked = true;
                return;
            }
            if (IsVkSelected)
            {
                string vini = Path.Combine(gameDir, VkOps.IniName), profile = null;
                if (!File.Exists(vini)) { vini = Path.Combine(pkgDirs["vk3d"], VkOps.IniName); profile = VkOps.Profile(pkgDirs["vk3d"], gameExe); }
                var v = VkOps.Read(vini, profile);
                chkSr.Checked = v.Sr;
                SetWrapperModes(VkOps.Modes);
                cbGlMode.SelectedItem = v.Mode;
                chkSwap.Checked = v.Swap;
                chkUpscale.Checked = false;
                numSep.Value = Math.Min(numSep.Maximum, Math.Max(numSep.Minimum, v.Separation));
                numConv.Value = Math.Min(numConv.Maximum, Math.Max(numConv.Minimum, v.Convergence));
                chkAutoConv.Checked = v.Auto;
                chkVkDxvk.Checked = VkOps.DxvkInstalled(gameDir);
                rb64.Checked = true;
                return;
            }
            SetWrapperModes(GlOps.Modes);
            string ini = Path.Combine(gameDir, GlOps.IniName);
            if (!File.Exists(ini)) ini = Path.Combine(pkgDirs["ogl3d"], GlOps.IniName);
            var o = GlOps.Read(ini);
            chkSr.Checked = o.Sr;
            cbGlMode.SelectedItem = o.Mode;
            chkSwap.Checked = o.Swap;
            chkUpscale.Checked = o.Upscale;
            numSep.Value = Math.Min(numSep.Maximum, Math.Max(numSep.Minimum, o.Separation));
            numConv.Value = Math.Min(numConv.Maximum, Math.Max(numConv.Minimum, o.Convergence));
            chkAutoConv.Checked = o.Auto;
            var bits = Util.ExeBits(gameExe);
            if (bits == 64) rb64.Checked = true; else if (bits == 32) rb32.Checked = true;
        }

        // OpenGL / Vulkan kinds: the wrapper's DLL + ini (VK3D: + the game's fix, + DXVK when chosen), nothing of geo-11 / SRWeave / dgVoodoo
        void ApplyWrapper()
        {
            if (IsVkSelected) { ApplyVk(); return; }
            if (IsDx12Selected) { ApplyDx12(); return; }
            try
            {
                string dir = gameDir;
                int bits = Util.ExeBits(gameExe) ?? (rb64.Checked ? 64 : 32);
                var o = new GlOps.Options
                {
                    Sr = chkSr.Checked, Mode = (string)cbGlMode.SelectedItem ?? "sbs", Swap = chkSwap.Checked, Upscale = chkUpscale.Checked,
                    Separation = numSep.Value, Auto = chkAutoConv.Checked, Convergence = numConv.Value
                };
                Log("----------------------------------------");
                Log(Lang.T("적용: ") + "OpenGL (OGL3D) / " + (bits == 64 ? "x64" : "x86") + " / SR " + (o.Sr ? Lang.T("사용") : Lang.T("사용 안 함")));
                Log(Lang.T("  원본: ") + pkgDirs["ogl3d"]);
                foreach (var l in GlOps.Install(pkgDirs["ogl3d"], bits, dir, o)) Log(l);
                if (File.Exists(Path.Combine(dir, "d3d11.dll")) || File.Exists(Path.Combine(dir, "dxgi.dll")) || Dx9Ops.Installed(dir))
                    Log(Lang.T("  참고: 게임 폴더에 geo-11 / SRWeave / dgVoodoo 파일이 있습니다. OpenGL 게임은 쓰지 않으니 필요 없으면 지우세요."));
                AddGameToList(gameExe, true);
                wizardActive = false;
                RefreshGameList();
                tabs.SelectedIndex = TabList;
                Log(Lang.T("완료."));
                Status(Lang.T("설정 완료: ") + Path.GetFileName(gameExe) + "  (" + dir + ")");
                MessageBox.Show(this, Lang.T("OGL3D를 설치했습니다.\n\n게임을 SR 패널에서 패널 해상도로 전체화면 또는 테두리 없는 창으로 실행하세요(업스케일을 켰다면 게임 해상도는 1920×1080). 게임 폴더의 OGL3D.log에 동작 기록이 남습니다.\n자세한 내용은 [로그] 탭에 있습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(Lang.T("오류: ") + ex.Message);
                Status(Lang.T("오류: ") + ex.Message);
                MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void ApplyVk()
        {
            try
            {
                string dir = gameDir;
                var o = new VkOps.Options
                {
                    Sr = chkSr.Checked, Mode = (string)cbGlMode.SelectedItem ?? "sbs", Swap = chkSwap.Checked,
                    Separation = numSep.Value, Auto = chkAutoConv.Checked, Convergence = numConv.Value, Dxvk = chkVkDxvk.Enabled && chkVkDxvk.Checked
                };
                Log("----------------------------------------");
                Log(Lang.T("적용: ") + "Vulkan (VK3D) / x64 / SR " + (o.Sr ? Lang.T("사용") : Lang.T("사용 안 함")) + (o.Dxvk ? " / DXVK" : ""));
                Log(Lang.T("  원본: ") + pkgDirs["vk3d"]);
                foreach (var l in VkOps.Install(pkgDirs["vk3d"], pkgDirs["dxvk"], gameExe, dir, o)) Log(l);
                if (!o.Dxvk && (File.Exists(Path.Combine(dir, "d3d11.dll")) || File.Exists(Path.Combine(dir, "dxgi.dll")) || Dx9Ops.Installed(dir)))
                    Log(Lang.T("  참고: 게임 폴더에 geo-11 / SRWeave / dgVoodoo / DXVK 파일이 있습니다. Vulkan 게임은 쓰지 않으니 필요 없으면 지우세요."));
                AddGameToList(gameExe, true);
                wizardActive = false;
                RefreshGameList();
                tabs.SelectedIndex = TabList;
                Log(Lang.T("완료."));
                Status(Lang.T("설정 완료: ") + Path.GetFileName(gameExe) + "  (" + dir + ")");
                MessageBox.Show(this, Lang.T("VK3D를 설치했습니다.\n\n게임을 SR 패널에서 패널 해상도로 전체화면 또는 테두리 없는 창으로 실행하세요. 게임 폴더의 VK3D.log에 동작 기록이 남습니다.\n자세한 내용은 [로그] 탭에 있습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(Lang.T("오류: ") + ex.Message);
                Status(Lang.T("오류: ") + ex.Message);
                MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void ApplyDx12()
        {
            try
            {
                string dir = gameDir;
                var o = new Dx12Ops.Options
                {
                    Sr = chkSr.Checked, Mode = (string)cbGlMode.SelectedItem ?? "sbs", Swap = chkSwap.Checked,
                    Separation = numSep.Value, Convergence = numConv.Value
                };
                Log("----------------------------------------");
                Log(Lang.T("적용: ") + "DirectX 12 (DX12_3D) / x64 / SR " + (o.Sr ? Lang.T("사용") : Lang.T("사용 안 함")));
                Log(Lang.T("  원본: ") + pkgDirs["dx12"]);
                foreach (var l in Dx12Ops.Install(pkgDirs["dx12"], gameExe, dir, o)) Log(l);
                if (File.Exists(Path.Combine(dir, "d3d11.dll")) || File.Exists(Path.Combine(dir, "dxgi.dll")) || VkOps.Installed(dir) || GlOps.Installed(dir))
                    Log(Lang.T("  참고: 게임 폴더에 다른 3D 방식(geo-11 / SRWeave / OGL3D / VK3D)의 파일이 있습니다. 같이 쓰지 말고 필요 없으면 지우세요."));
                AddGameToList(gameExe, true);
                wizardActive = false;
                RefreshGameList();
                tabs.SelectedIndex = TabList;
                Log(Lang.T("완료."));
                Status(Lang.T("설정 완료: ") + Path.GetFileName(gameExe) + "  (" + dir + ")");
                MessageBox.Show(this, Lang.T("DX12_3D를 설치했습니다.\n\n게임을 SR 패널에서 패널 해상도로 전체화면 또는 테두리 없는 창으로 실행하세요. DLSS 대신 게임의 렌더 스케일이나 FSR / XeSS를 쓰세요. 게임 폴더의 DX12_3D.log에 동작 기록이 남습니다.\n자세한 내용은 [로그] 탭에 있습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(Lang.T("오류: ") + ex.Message);
                Status(Lang.T("오류: ") + ex.Message);
                MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        bool RequireGame()
        {
            if (gameDir != null) return true;
            MessageBox.Show(this, Lang.T("먼저 [게임 목록] 탭에서 [게임 추가 (설정 시작)]을 눌러 게임 실행 파일을 고르세요.\n게임 실행 파일을 이 창에 끌어다 놓아도 됩니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            tabs.SelectedIndex = TabList;
            return false;
        }

        // ---- actions ---------------------------------------------------------------------------
        void BackupReshade(string dir, string ourDll)
        {
            string dx = Path.Combine(dir, "dxgi.dll");
            if (!File.Exists(dx) || Util.IsSrWeave(dx, ourDll)) return;
            string bak = Path.Combine(dir, "dxgi.dll.reshade_bak");
            if (File.Exists(bak)) { File.Delete(dx); Log(Lang.T("  기존 dxgi.dll 삭제 (dxgi.dll.reshade_bak이 이미 있음)")); }
            else { File.Move(dx, bak); Log(Lang.T("  기존 dxgi.dll(ReShade?) → dxgi.dll.reshade_bak")); }
        }

        // TriDef 3D: register the game with the chosen profile (TriDef 3D itself must be installed); nothing in the game folder
        void ApplyTri()
        {
            if (!TriOps.Installed())
            {
                Log(Lang.T("적용 안 함: TriDef 3D가 설치돼 있지 않습니다."));
                MessageBox.Show(this, Lang.T("이 PC에 TriDef 3D가 설치돼 있지 않습니다.\n\nTriDef 3D 방식은 설치된 TriDef 3D(DDD)가 3D를 만듭니다. TriDef 3D를 먼저 설치하세요. 이 앱에는 TriDef 파일이 들어 있지 않습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string appId, nm;
            if (!Util.FindSteamApp(gameDir, out appId, out nm))
            {
                Log(Lang.T("적용 안 함: Steam 게임이 아닙니다 (TriDef 실행기는 Steam 게임만 시작합니다)."));
                MessageBox.Show(this, Lang.T("Steam 게임이 아닙니다.\n\nTriDef 실행기(Tridef3D_Play)는 Steam으로 게임을 시작한 뒤 주입하므로 Steam 게임만 됩니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string profile = cbTriProfile.SelectedIndex > 0 ? (string)cbTriProfile.SelectedItem : null;
            if (profile == null)
            {
                MessageBox.Show(this, Lang.T("TriDef 프로필을 고르세요. 프로필이 없으면 화면만 좌우로 나뉘고 깊이가 없습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                string name = TriOps.RegisteredName(gameExe) ?? TriOps.KeyName(nm ?? Util.GameDisplayName(gameExe));
                Log("----------------------------------------");
                Log(Lang.T("적용: ") + "TriDef 3D / " + (Util.ExeBits(gameExe) == 64 ? "x64 (DX11)" : "x86 (DX9)") + " / SR " + (chkSr.Checked ? Lang.T("사용") : Lang.T("사용 안 함")));
                foreach (var l in TriOps.Register(name, gameExe, profile, chkSr.Checked)) Log(l);
                AddGameToList(gameExe, true);
                wizardActive = false;
                RefreshGameList();
                tabs.SelectedIndex = TabList;
                Log(Lang.T("완료."));
                Status(Lang.T("설정 완료: ") + Path.GetFileName(gameExe) + "  (TriDef 3D)");
                MessageBox.Show(this, Lang.T("TriDef 3D에 등록했습니다.\n\n[게임 목록]의 [실행]으로 시작하세요(관리자 권한 확인 창이 뜹니다). Steam에서 바로 실행하면 3D가 되지 않습니다. 깊이는 게임 안에서 TriDef 단축키로 조절합니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(Lang.T("오류: ") + ex.Message);
                Status(Lang.T("오류: ") + ex.Message);
                MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // a TriDef registration counts when no other 3D of this app is in the game folder
        static string TriOf(string exe)
        {
            string dir = Path.GetDirectoryName(exe);
            if (GlOps.Installed(dir) || VkOps.Installed(dir) || Dx12Ops.Installed(dir) || File.Exists(Path.Combine(dir, "d3d11.dll"))) return null;
            return TriOps.RegisteredName(exe);
        }

        void RemoveSrFiles(string dir, string ourDll, bool restore)
        {
            string dx = Path.Combine(dir, "dxgi.dll");
            if (Util.IsSrWeave(dx, ourDll)) { File.Delete(dx); Log(Lang.T("  SRWeave dxgi.dll 삭제")); }
            foreach (var n in new[] { "SRWeave.ini", "SRWeave.log" }) { string p = Path.Combine(dir, n); if (File.Exists(p)) { File.Delete(p); Log("  " + n + Lang.T(" 삭제")); } }
            string sd = Path.Combine(dir, "ShaderFixes", "SRWeave");
            if (Directory.Exists(sd)) { Directory.Delete(sd, true); Log(Lang.T("  ShaderFixes\\SRWeave 삭제")); }
            string bak = Path.Combine(dir, "dxgi.dll.reshade_bak");
            if (restore && File.Exists(bak) && !File.Exists(dx)) { File.Move(bak, dx); Log(Lang.T("  dxgi.dll.reshade_bak → dxgi.dll 복원 (ReShade)")); }
        }

        void Apply()
        {
            if (!RequireGame()) return;
            string ac = Library.AntiCheat(gameDir);
            if (ac != null)
            {
                Log(Lang.T("적용 안 함: 안티치트가 있는 게임입니다 - ") + ac);
                MessageBox.Show(this, Lang.T("이 게임 폴더에 안티치트가 있습니다: ") + ac + Lang.T("\n\n게임 폴더에 DLL을 넣으면 계정이 정지될 수 있어 설치하지 않습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsTriSelected) { ApplyTri(); return; }
            if (IsGlSelected || IsVkSelected || IsDx12Selected) { ApplyWrapper(); return; }
            try
            {
                string dir = gameDir, src = SrcDir(), ourDll = Path.Combine(src, "dxgi.dll");
                if (!File.Exists(ourDll)) throw new Exception(Lang.T("패키지가 없습니다: ") + src + Lang.T("\n이 설치기 폴더 옆에 패키지 폴더가 있어야 합니다."));
                // SBS only = the full install without the SR weaving files
                bool useSr = chkSr.Checked && !rbSbsOnly.Checked, full = rbFull.Checked || rbSbsOnly.Checked;
                Log("----------------------------------------");
                Log(Lang.T("적용: ") + (rbUnity.Checked ? "Unity" : IsUe4Selected ? "UE4 UF2" : IsDx9Selected ? "DX9 dgVoodoo2 " + cbDx9Ver.SelectedItem + " + " + Lang.T("일반 geo-11") : Lang.T("일반 geo-11")) + " / " + (rb64.Checked ? "x64" : "x32") + " / " + (rbSbsOnly.Checked ? Lang.T("SBS만 설치") : full ? Lang.T("전체 설치") : Lang.T("SR만 추가")) + " / SR " + (useSr ? Lang.T("사용") : Lang.T("사용 안 함")));
                Log(Lang.T("  원본: ") + src);

                if (useSr) BackupReshade(dir, ourDll);
                if (full)
                {
                    Util.CopyDir(src, dir, n => n.Equals("SRWeave.log", StringComparison.OrdinalIgnoreCase)
                        || (!useSr && (n.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) || n.Equals("SRWeave.ini", StringComparison.OrdinalIgnoreCase))));
                    if (!useSr) { string sd = Path.Combine(dir, "ShaderFixes", "SRWeave"); if (Directory.Exists(sd)) Directory.Delete(sd, true); }
                    int fixedBins = Util.FixShaderCacheTimes(Path.Combine(dir, "ShaderFixes"));
                    Log(Lang.T("  패키지 파일 복사 완료") + (fixedBins > 0 ? Lang.T(" (셰이더 캐시 시각 보정 ") + fixedBins + ")" : ""));
                    if (IsUe4Selected) Ue4PostInstall(dir);
                }
                else if (useSr)
                {
                    File.Copy(ourDll, Path.Combine(dir, "dxgi.dll"), true);
                    if (!File.Exists(Path.Combine(dir, "SRWeave.ini"))) File.Copy(Path.Combine(src, "SRWeave.ini"), Path.Combine(dir, "SRWeave.ini"));
                    Log(Lang.T("  dxgi.dll + SRWeave.ini 복사 (기존 픽스 유지)"));
                }
                if (IsDx9Selected)
                {
                    foreach (var l in Dx9Ops.Install(pkgDirs["dx9"], (string)cbDx9Ver.SelectedItem, rb64.Checked ? 64 : 32, dir, chkDx9Desktop.Checked, (string)cbDx9Vram.SelectedItem)) Log(l);
                    Log(Lang.T("  DX9: 화면 오버레이(숫자패드 0)는 dgVoodoo 아래에서 표시되지 않습니다. 단축키(입체 값 조절·저장·3D 켜기/끄기·SRWeave 키)는 모두 동작합니다."));
                }
                if (IsDx9Selected && chkLaa.Enabled && chkLaa.Checked)
                {
                    // the game's own exe gets modified: ask once more before doing it
                    var ok = MessageBox.Show(this,
                        Lang.T("4GB 패치는 게임 실행 파일을 직접 수정합니다 (헤더의 LARGE_ADDRESS_AWARE 플래그).\n\n") + gameExe + Lang.T("\n\n원본은 같은 폴더에 <exe>.geo11sr_bak으로 보관되고 [3D 픽스 제거]가 되돌립니다. Steam '게임 파일 무결성 확인' 뒤에는 다시 적용해야 합니다.\n\n지금 패치할까요?"),
                        Lang.T("4GB 패치"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (ok == DialogResult.Yes && Util.SetLargeAddressAware(gameExe))
                    { Log(Lang.T("  4GB 패치: ") + Path.GetFileName(gameExe) + Lang.T("에 LARGE_ADDRESS_AWARE 설정 (원본 .geo11sr_bak)")); chkLaa.Enabled = false; chkLaa.Checked = false; chkLaa.Text = Lang.T("4GB 패치 - 이미 적용되어 있음"); }
                    else if (ok != DialogResult.Yes) Log(Lang.T("  4GB 패치: 건너뜀 (사용자가 취소)"));
                }
                if (IsUe4Selected && chkEngineIni.Checked && !Ue4Ops.AaAoInstalled(gameExe)) Log(Ue4Ops.InstallAaAo(dir, gameExe, false));
                if (IsUe4Selected) foreach (var l in Launchers.ApplyDx11(gameExe, this)) Log(l);   // UF2: the game must start with -dx11
                if (!useSr) RemoveSrFiles(dir, ourDll, true);

                string dm = Path.Combine(dir, "d3dxdm.ini");
                if (File.Exists(dm))
                {
                    string mode = useSr ? "sbs" : (string)cbMode.SelectedItem;
                    Ini.Set(dm, "direct_mode", mode, "Device");
                    string sepS = numSep.Value.ToString("0.#", CultureInfo.InvariantCulture), convS = numConv.Value.ToString("0.0#", CultureInfo.InvariantCulture);
                    Ini.Set(dm, "dm_separation", sepS, "Stereo");
                    Ini.Set(dm, "dm_convergence", convS, "Stereo");
                    Ini.Set(dm, "dm_auto_convergence", chkAutoConv.Checked ? "1" : "0", "Stereo");
                    Log("  d3dxdm.ini: direct_mode=" + mode + " dm_separation=" + sepS + " dm_convergence=" + convS + " dm_auto_convergence=" + (chkAutoConv.Checked ? 1 : 0));
                    // geo-11 loads d3dx_user.ini (values saved in-game with Ctrl+F7) after d3dxdm.ini, so update it too
                    bool u1 = Ini.SetPersisted(dir, "separation", sepS), u2 = Ini.SetPersisted(dir, "convergence", convS);
                    if (u1 || u2) Log(Lang.T("  d3dx_user.ini: 게임 안에서 저장했던 separation/convergence도 같은 값으로 바꿈"));
                }
                else Log(Lang.T("  경고: d3dxdm.ini 없음 - geo-11이 아닌 3DMigoto 픽스입니다. SRWeave는 geo-11 sbs 출력이 필요합니다."));

                ApplyHotkeys(dir, useSr);

                string dx = Path.Combine(dir, "d3dx.ini");
                if (File.Exists(dx))
                {
                    // upscaling: [Device] upscaling / upscale_mode / width / height + [Include] upscale.ini (the Present-time upscale shader)
                    if (chkUpscale.Checked)
                    {
                        string up = cbUpscaleFs.SelectedIndex == 1 ? "2" : "1";
                        Ini.Set(dx, "upscaling", up, "Device");
                        Ini.Set(dx, "upscale_mode", cbUpscaleMode.SelectedIndex.ToString(), "Device");
                        if (numUpW.Value > 0 && numUpH.Value > 0)
                        {
                            Ini.Set(dx, "width", ((int)numUpW.Value).ToString(), "Device");
                            Ini.Set(dx, "height", ((int)numUpH.Value).ToString(), "Device");
                        }
                        else { Ini.Disable(dx, "width"); Ini.Disable(dx, "height"); }
                        Ini.SetInclude(dx, UpscaleInclude, true);
                        Log("  d3dx.ini: upscaling=" + up + " upscale_mode=" + cbUpscaleMode.SelectedIndex + Lang.T(" 출력 ") +
                            (numUpW.Value > 0 && numUpH.Value > 0 ? (int)numUpW.Value + "x" + (int)numUpH.Value : Lang.T("바탕화면 해상도")) + Lang.T(", include upscale.ini 켬"));
                    }
                    else
                    {
                        Ini.Set(dx, "upscaling", "0", "Device");
                        Ini.Disable(dx, "width"); Ini.Disable(dx, "height");
                        Ini.SetInclude(dx, UpscaleInclude, false);
                        Log(Lang.T("  d3dx.ini: upscaling=0, include upscale.ini 끔"));
                    }
                }

                string us = Path.Combine(dir, "UnitySwitch.ps1");
                if (rbUnity.Checked && File.Exists(us))
                {
                    string code = UnityCodes[cbUnityVer.SelectedIndex];
                    string o = Util.RunHidden("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + us + "\" " + code, dir);
                    foreach (var l in o.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) Log("  UnitySwitch: " + l);
                }
                else if (rbUnity.Checked) Log(Lang.T("  UnitySwitch.ps1 없음 - Unity 버전 include 줄은 바꾸지 않았습니다."));

                string sr = Path.Combine(dir, "SRWeave.ini");
                if (useSr && File.Exists(sr))
                {
                    Ini.Set(sr, "weave", "1", "SRWeave");
                    Ini.Set(sr, "swap_eyes", chkSwap.Checked ? "1" : "0", "SRWeave");
                    Ini.Set(sr, "lens", chkLens.Checked ? "1" : "0", "SRWeave");
                    Log("  SRWeave.ini: swap_eyes=" + (chkSwap.Checked ? 1 : 0) + " lens=" + (chkLens.Checked ? 1 : 0));
                }

                // compiled game-shader caches: only worth clearing when the fix files were replaced (full install);
                // clearing them on every re-apply just makes the next game start slow
                if (full) foreach (var c in new[] { "ShaderCache", "ShaderCacheDM" }) { string p = Path.Combine(dir, c); if (Directory.Exists(p)) { Directory.Delete(p, true); Log("  " + c + Lang.T(" 비움")); } }
                AddGameToList(gameExe, true);
                wizardActive = false;
                RefreshGameList();
                tabs.SelectedIndex = TabList;
                Log(Lang.T("완료."));
                Status(Lang.T("설정 완료: ") + Path.GetFileName(gameExe) + "  (" + dir + ")");
                MessageBox.Show(this, Lang.T("설정을 적용했습니다.\n\n게임을 SR 패널에서 패널 해상도로 전체화면 또는 테두리 없는 창으로 실행하세요.\n자세한 내용은 [로그] 탭에 있습니다."), AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(Lang.T("오류: ") + ex.Message);
                Status(Lang.T("오류: ") + ex.Message);
                MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // UF2's d3dxdm.ini has the separation/convergence keys but no stereo toggle and no save key. Saving uses the
        // same "pre persist separation = ..." form as the Unity package's hotkeys.ini, which needs geo-11 0.6.109:
        // UF2 ships 0.6.40, which rejects it (and reads `separation` as garbage in command lists), so the installer
        // puts the 0.6.109 driver into UF2 installs (package ShaderFixes\Geo11, 0.6.40 kept in Geo11_0.6.40).
        // Also removes the $saved_* variable scheme an earlier build wrote. Returns true when the file was changed.
        public static bool EnsureUe4KeySections(string dm)
        {
            if (!File.Exists(dm)) return false;
            var lines = File.ReadAllLines(dm, Encoding.Default).ToList();
            bool changed = false;
            Func<string, int> section = n => lines.FindIndex(l => l.Trim().Equals("[" + n + "]", StringComparison.OrdinalIgnoreCase));
            Func<int, int> sectionEnd = start => { int i = start + 1; while (i < lines.Count && !lines[i].TrimStart().StartsWith("[")) i++; return i; };

            // migration: the $saved_* scheme (declarations, [Present] apply block, save-list lines) from an earlier build
            string[] oldLines = { "; stereo values saved with KeySaveSettings (geo-11 SR installer); -1 = nothing saved yet", "global persist $saved_sep = -1", "global persist $saved_conv = -1", "global $saved_applied = 0",
                "; apply the stereo values saved with KeySaveSettings (geo-11 SR installer)", "if $saved_applied == 0", "if $saved_sep >= 0", "separation = $saved_sep", "convergence = $saved_conv", "$saved_applied = 1",
                "$saved_sep = separation", "$saved_conv = convergence" };
            int removed = 0;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                string t = lines[i].Trim();
                if (oldLines.Contains(t)) { lines.RemoveAt(i); removed++; }
                else if (t == "endif" && i >= 1 && (lines[i - 1].Trim() == "convergence = $saved_conv" || lines[i - 1].Trim() == "$saved_applied = 1" || lines[i - 1].Trim() == "endif" && i >= 2 && lines[i - 2].Trim() == "convergence = $saved_conv")) { lines.RemoveAt(i); removed++; }
            }
            if (removed > 0) changed = true;

            var add = new List<string>();
            if (section("KeyToggleStereo") < 0)
                add.AddRange(new[] { "", "[KeyToggleStereo]", "Key = ctrl t", "type = toggle", "dm_stereo_enabled = !dm_stereo_enabled" });
            if (section("KeySaveSettings") < 0)
                add.AddRange(new[] { "", "[KeySaveSettings]", "Key = ctrl F7", "run = CommandListSaveSettings" });
            var save = new[] { "pre overlay_notice = Saved modified stereo values", "pre persist separation = separation", "pre persist convergence = convergence" };
            int cl = section("CommandListSaveSettings");
            if (cl < 0) add.AddRange(new[] { "", "[CommandListSaveSettings]" }.Concat(save));
            else
            {
                int end = sectionEnd(cl);
                var body = lines.Skip(cl + 1).Take(end - cl - 1).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith(";")).ToList();
                if (!save.All(body.Contains))
                {
                    lines.RemoveRange(cl + 1, end - cl - 1);
                    lines.InsertRange(cl + 1, save.Concat(new[] { "" }));
                    changed = true;
                }
            }
            if (add.Count > 0)
            {
                int at = section("KeyDecreaseSeparation");
                if (at >= 0) { int i = sectionEnd(at); while (i > at + 1 && lines[i - 1].Trim().Length == 0) i--; lines.InsertRange(i, add); }
                else lines.AddRange(add);
                changed = true;
            }
            if (changed) File.WriteAllLines(dm, lines, Encoding.Default);
            return changed;
        }

        // The save key needs geo-11 0.6.109 in the game folder. An existing UF2 install still on 0.6.40 (SR-only re-apply
        // does not copy the package) gets the driver from the package's ShaderFixes\Geo11.
        void Ue4EnsureDriver(string dir)
        {
            string root = Path.Combine(dir, "d3d11.dll");
            if (!File.Exists(root) || DriverSupportsSave(root)) return;
            string src = Path.Combine(pkgDirs["ue4"], "ShaderFixes", "Geo11");
            if (!DriverSupportsSave(Path.Combine(src, "d3d11.dll"))) { Log(Lang.T("  경고: 게임의 geo-11이 0.6.40이라 현재 값 저장 키가 동작하지 않습니다 (패키지에도 0.6.109가 없음).")); return; }
            try
            {
                foreach (var d in new[] { dir, Path.Combine(dir, "ShaderFixes", "Geo11"), Path.Combine(dir, "ShaderFixes", "ResetFix") })
                {
                    if (!Directory.Exists(d)) continue;
                    File.Copy(Path.Combine(src, "d3d11.dll"), Path.Combine(d, "d3d11.dll"), true);
                    File.Copy(Path.Combine(src, "nvapi64.dll"), Path.Combine(d, "nvapi64.dll"), true);
                    if (File.Exists(Path.Combine(d, "d3d11_loader.dll"))) File.Copy(Path.Combine(src, "d3d11.dll"), Path.Combine(d, "d3d11_loader.dll"), true);
                }
                Log(Lang.T("  geo-11 드라이버를 0.6.40 → 0.6.109로 교체 (현재 값 저장 키에 필요)"));
            }
            catch (Exception ex) { Log(Lang.T("  경고: geo-11 드라이버 교체 실패 (게임이 실행 중?): ") + ex.Message); }
        }

        // UF2 driver: the geo-11 in ShaderFixes\Geo11 must be 0.6.109-class for the save key (probe: it knows overlay_notice)
        public static bool DriverSupportsSave(string d3d11Dll)
        {
            try
            {
                if (!File.Exists(d3d11Dll)) return false;
                byte[] b = File.ReadAllBytes(d3d11Dll);
                byte[] pat = Encoding.Unicode.GetBytes("overlay_notice");
                for (int i = 0; i <= b.Length - pat.Length; i++)
                {
                    int j = 0;
                    while (j < pat.Length && b[i + j] == pat[j]) j++;
                    if (j == pat.Length) return true;
                }
            }
            catch { }
            return false;
        }

        // hotkeys.ini (geo-11 keys), d3dx.ini [Hunting] (overlay mode + keys), SRWeave.ini key_* (when SR is used)
        void ApplyHotkeys(string dir, bool useSr)
        {
            string hk = GeoKeyFile(dir);
            if (File.Exists(hk))
            {
                // UF2 keeps the keys in d3dxdm.ini and ships without KeyToggleStereo / KeySaveSettings: add them first
                if (string.Equals(Path.GetFileName(hk), "d3dxdm.ini", StringComparison.OrdinalIgnoreCase))
                {
                    if (EnsureUe4KeySections(hk)) Log(Lang.T("  d3dxdm.ini: 3D 켜기/끄기·현재 값 저장 키 섹션 추가 (UF2에는 없던 것)"));
                    Ue4EnsureDriver(dir);
                }
                var written = new List<string>();
                foreach (var k in GeoKeys)
                {
                    var v = KeyText.ToMigoto(keyBoxes[k.Id].Combo);
                    if (v.Length > 0 && Ini.HasSection(hk, k.Id)) { Ini.SetSectionKey(hk, k.Id, v); written.Add(k.Label + " = " + v); }
                }
                Log("  " + Path.GetFileName(hk) + ": " + string.Join(", ", written));
            }
            string dx = Path.Combine(dir, "d3dx.ini");
            if (File.Exists(dx))
            {
                Ini.Set(dx, "hunting", cbHunting.SelectedIndex.ToString(), "Hunting");
                foreach (var k in MigotoKeys) { var v = KeyText.ToMigoto(keyBoxes[k.Id].Combo); if (v.Length > 0) Ini.Set(dx, k.Id, v, "Hunting"); }
                Log(Lang.T("  d3dx.ini: hunting(오버레이 모드)=") + cbHunting.SelectedIndex + ", " + string.Join(", ", MigotoKeys.Select(k => k.Id + " = " + KeyText.ToMigoto(keyBoxes[k.Id].Combo))));
            }
            string sr = Path.Combine(dir, "SRWeave.ini");
            if (useSr && File.Exists(sr))
            {
                foreach (var k in SrKeys) Ini.Set(sr, k.Id, KeyText.ToSrWeave(keyBoxes[k.Id].Combo), "SRWeave");
                Log("  SRWeave.ini keys=" + string.Join("/", SrKeys.Select(k => KeyText.ToSrWeave(keyBoxes[k.Id].Combo))));
            }
        }

        void ApplyHotkeysOnly()
        {
            if (!RequireGame()) return;
            try
            {
                Log("----------------------------------------");
                Log(Lang.T("단축키만 적용: ") + gameDir);
                if (TriOf(gameExe) != null || IsTriSelected)
                {
                    Log(Lang.T("  TriDef 3D 게임의 단축키는 TriDef 3D 설정에서 바꿉니다. 이 탭의 키(geo-11 / SRWeave)는 쓰지 않습니다."));
                    Status(Lang.T("TriDef 3D 게임의 단축키는 TriDef 3D 설정에서 바꿉니다."));
                    return;
                }
                if (Dx12Ops.Installed(gameDir) || IsDx12Selected)
                {
                    Log(Lang.T("  DirectX 12 (DX12_3D) 게임의 단축키는 DX12_3D.ini의 [Keys]에서 바꿉니다. 이 탭의 키(geo-11 / SRWeave)는 쓰지 않습니다."));
                    Status(Lang.T("DirectX 12 (DX12_3D) 게임의 단축키는 DX12_3D.ini [Keys]에서 바꿉니다."));
                    return;
                }
                if (VkOps.Installed(gameDir) || IsVkSelected)
                {
                    Log(Lang.T("  Vulkan (VK3D) 게임의 단축키는 VK3D.ini의 [Keys]에서 바꿉니다. 이 탭의 키(geo-11 / SRWeave)는 쓰지 않습니다."));
                    Status(Lang.T("Vulkan (VK3D) 게임의 단축키는 VK3D.ini [Keys]에서 바꿉니다."));
                    return;
                }
                if (GlOps.Installed(gameDir) || IsGlSelected)
                {
                    Log(Lang.T("  OpenGL (OGL3D) 게임의 단축키는 OGL3D.ini의 [Keys]에서 바꿉니다. 이 탭의 키(geo-11 / SRWeave)는 쓰지 않습니다."));
                    Status(Lang.T("OpenGL (OGL3D) 게임의 단축키는 OGL3D.ini [Keys]에서 바꿉니다."));
                    return;
                }
                ApplyHotkeys(gameDir, File.Exists(Path.Combine(gameDir, "SRWeave.ini")));
                Status(Lang.T("단축키를 적용했습니다: ") + Path.GetFileName(gameExe));
            }
            catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void RemoveSr()
        {
            if (!RequireGame()) return;
            try
            {
                Log("----------------------------------------");
                Log(Lang.T("SR 위빙 제거"));
                string trn = TriOf(gameExe);
                if (trn != null)
                {
                    TriOps.SetSr(trn, false);
                    Log(Lang.T("  TriDef 3D: SR 위빙 없이 실행 (3D는 TriDef의 좌우 SBS로 유지)"));
                    Status(Lang.T("SR 위빙 제거 완료"));
                    RefreshGameList();
                    return;
                }
                if (Dx12Ops.Installed(gameDir))
                {
                    // DX12_3D weaves by itself: switch its output to side-by-side
                    GlOps.Set(Path.Combine(gameDir, Dx12Ops.IniName), "Stereo", "output", "sbs");
                    Log(Lang.T("  DX12_3D.ini: output = sbs (SR 위빙 끔, 3D는 좌우 SBS로 유지)"));
                    Status(Lang.T("SR 위빙 제거 완료"));
                    RefreshGameList();
                    return;
                }
                if (VkOps.Installed(gameDir))
                {
                    // VK3D weaves by itself: switch its output to side-by-side
                    GlOps.Set(Path.Combine(gameDir, VkOps.IniName), "Stereo", "output", "sbs");
                    Log(Lang.T("  VK3D.ini: output = sbs (SR 위빙 끔, 3D는 좌우 SBS로 유지)"));
                    Status(Lang.T("SR 위빙 제거 완료"));
                    RefreshGameList();
                    return;
                }
                if (GlOps.Installed(gameDir))
                {
                    // OGL3D weaves by itself: switch its output to side-by-side
                    GlOps.Set(Path.Combine(gameDir, GlOps.IniName), "Stereo", "output", "sbs");
                    Log(Lang.T("  OGL3D.ini: output = sbs (SR 위빙 끔, 3D는 좌우 SBS로 유지)"));
                    Status(Lang.T("SR 위빙 제거 완료"));
                    RefreshGameList();
                    return;
                }
                RemoveSrFiles(gameDir, Path.Combine(SrcDir(), "dxgi.dll"), true);
                Log(Lang.T("완료 (geo-11은 그대로입니다)."));
                Status(Lang.T("SR 위빙 제거 완료"));
            }
            catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void UninstallAll()
        {
            if (!RequireGame()) return;
            string trn = TriOf(gameExe);
            if (trn != null)
            {
                if (MessageBox.Show(this, Lang.T("TriDef 3D 등록을 지웁니다: ") + trn + Lang.T("\n\n게임 폴더는 바뀌지 않습니다. TriDef 3D Ignition에서 직접 추가한 게임이면 등록은 그대로 두고 이 앱의 설정만 지웁니다.\n\n계속할까요?"), Lang.T("3D 픽스 제거"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    Log("----------------------------------------");
                    Log(Lang.T("TriDef 3D 등록 제거: ") + trn);
                    foreach (var l in TriOps.Unregister(trn)) Log(l);
                    Status(Lang.T("3D 픽스 제거 완료"));
                    RefreshGameList();
                }
                catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                return;
            }
            if (Dx12Ops.Installed(gameDir))
            {
                if (MessageBox.Show(this, Lang.T("DX12_3D(DirectX 12)를 제거합니다: d3d12.dll, DX12_3D.ini(저장한 값 포함), DX12_3D.log, DX12_3D 폴더(DXC), 덤프를 지우고 보관해 둔 다른 d3d12.dll이 있으면 되돌립니다.\n\n") + gameDir + Lang.T("\n\n계속할까요?"), Lang.T("3D 픽스 제거"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    Log("----------------------------------------");
                    Log(Lang.T("DX12_3D 제거: ") + gameDir);
                    foreach (var l in Dx12Ops.Remove(gameDir)) Log(l);
                    Status(Lang.T("3D 픽스 제거 완료"));
                    RefreshGameList();
                }
                catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                if (!File.Exists(Path.Combine(gameDir, "Uninstall.bat"))) return;   // a geo-11 install as well: remove that too
            }
            if (VkOps.Installed(gameDir))
            {
                if (MessageBox.Show(this, Lang.T("VK3D(Vulkan)를 제거합니다: vulkan-1.dll, VK3D.ini(저장한 값 포함), VK3D.log, VK3D_fix, 프레임 덤프, 그리고 DXVK 파일(d3d9/d3d11/dxgi/d3d10core.dll, 있으면)을 지우고 보관해 둔 원래 DLL이 있으면 되돌립니다.\n\n") + gameDir + Lang.T("\n\n계속할까요?"), Lang.T("3D 픽스 제거"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    Log("----------------------------------------");
                    Log(Lang.T("VK3D 제거: ") + gameDir);
                    foreach (var l in VkOps.Remove(gameDir)) Log(l);
                    Status(Lang.T("3D 픽스 제거 완료"));
                    RefreshGameList();
                }
                catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                if (!File.Exists(Path.Combine(gameDir, "Uninstall.bat"))) return;   // a geo-11 install as well: remove that too
            }
            if (GlOps.Installed(gameDir))
            {
                if (MessageBox.Show(this, Lang.T("OGL3D(OpenGL)를 제거합니다: opengl32.dll, OGL3D.ini(저장한 값 포함), OGL3D.log, 프레임 덤프를 지우고 보관해 둔 다른 opengl32.dll이 있으면 되돌립니다.\n\n") + gameDir + Lang.T("\n\n계속할까요?"), Lang.T("3D 픽스 제거"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    Log("----------------------------------------");
                    Log(Lang.T("OGL3D 제거: ") + gameDir);
                    foreach (var l in GlOps.Remove(gameDir)) Log(l);
                    Status(Lang.T("3D 픽스 제거 완료"));
                    RefreshGameList();
                }
                catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                if (!File.Exists(Path.Combine(gameDir, "Uninstall.bat"))) return;   // a geo-11 install as well: remove that too
            }
            string un = Path.Combine(gameDir, "Uninstall.bat");
            if (!File.Exists(un)) { Log(Lang.T("Uninstall.bat이 게임 폴더에 없습니다.")); Status(Lang.T("Uninstall.bat 없음")); return; }
            if (MessageBox.Show(this, Lang.T("게임 폴더의 Uninstall.bat을 실행해 geo-11, ShaderFixes, SRWeave를 모두 지웁니다.\n\n") + gameDir + Lang.T("\n\n계속할까요?"), Lang.T("3D 픽스 제거"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                Log("----------------------------------------");
                RemoveSrFiles(gameDir, Path.Combine(SrcDir(), "dxgi.dll"), false);
                foreach (var l in Dx9Ops.Remove(gameDir)) Log(l);   // dgVoodoo2 (DX9 kind), if present
                string exeBak = gameExe + ".geo11sr_bak";
                if (File.Exists(exeBak)) { File.Copy(exeBak, gameExe, true); File.Delete(exeBak); Log(Lang.T("  4GB 패치 되돌림: 원본 exe 복원")); }
                Util.RunHidden("cmd.exe", "/c \"" + un + "\"", gameDir);
                string bak = Path.Combine(gameDir, "dxgi.dll.reshade_bak");
                if (File.Exists(bak)) { File.Copy(bak, Path.Combine(gameDir, "dxgi.dll"), true); File.Delete(bak); Log(Lang.T("  dxgi.dll.reshade_bak → dxgi.dll 복원")); }
                Log(Lang.T("Uninstall.bat 실행 완료."));
                Status(Lang.T("3D 픽스 제거 완료"));
            }
            catch (Exception ex) { Log(Lang.T("오류: ") + ex.Message); MessageBox.Show(this, ex.Message, Lang.T("오류"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OpenSrLog()
        {
            if (!RequireGame()) return;
            string l = TriOf(gameExe) != null ? TriOps.SrLog(pkgDirs["tridef"], gameExe) : Path.Combine(gameDir, GlOps.Installed(gameDir) ? "OGL3D.log" : VkOps.Installed(gameDir) ? "VK3D.log" : Dx12Ops.Installed(gameDir) ? "DX12_3D.log" : "SRWeave.log");
            if (File.Exists(l)) Process.Start("notepad.exe", "\"" + l + "\"");
            else { Log(Path.GetFileName(l) + Lang.T("가 아직 없습니다 (게임을 한 번 실행하면 생깁니다).")); Status(Path.GetFileName(l) + Lang.T("가 아직 없습니다.")); }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Theme.Init();
            var f = new MainForm();
            int shot = Array.IndexOf(args, "--shot");   // --shot <png> <tab> [game exe]: screenshot without showing the window
            int tab;
            if (shot >= 0 && args.Length > shot + 2 && int.TryParse(args[shot + 2], out tab))
            {
                string png = args[shot + 1], exe = args.Length > shot + 3 ? args[shot + 3] : null;
                Theme.Shot(f, png, () => f.PrepareShot(tab, exe));
                return;
            }
            Application.Run(f);
        }
    }
}
