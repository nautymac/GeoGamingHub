// Look of the window: a charcoal theme laid over the controls MainForm builds (its light colours are mapped when they are
// set), a flat tab strip instead of the system tab headers, rounded buttons, dark lists / combo boxes / scroll bars, and the
// 3D-method categories as square cards that drive MainForm's own (hidden) category radio buttons. Behaviour is MainForm's.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GeoGamingHub
{
    static class Theme
    {
        // the icon's colours: its neutral charcoal for the surfaces (not near-black: the first draft was too dark to read), the
        // teal -> blue of its split screen for the accents, the cube's pale light for the text
        public static readonly Color Window = C("#2B2A30"), Bar = C("#302F36"), Page = C("#35343B"), Input = C("#403F47"), InputAlt = C("#44434B"),
            InputFocus = C("#3B4C5E"), Card = C("#3D3C44"), CardOn = C("#2F4A46"), Border = C("#55545D"), BorderHover = C("#7A7983"), Line = C("#4A4951"),
            Text = C("#EEF4F7"), Text2 = C("#D2E2E8"), Muted = C("#B6C3CA"), Dim = C("#9DA8AF"),
            Accent = C("#63C0AC"), Accent2 = C("#5A8CD4"), AccentHover = C("#7ACDBB"), AccentOff = C("#43605B"), OnAccent = C("#0E2622"), Sel = C("#34524D"),
            Btn = C("#4A4952"), BtnBorder = C("#615F6A"), BtnHover = C("#54535D"), BtnDown = C("#5F5E69"),
            Blue = C("#86AEEB"), Purple = C("#CDAEFF"), Red = C("#FF8E8E"), Green = C("#7DDCB5"), Orange = C("#F5C46E");
        static Color C(string h) { return ColorTranslator.FromHtml(h); }

        // MainForm's light-theme colours -> charcoal ones (by value; any other dark text colour is lightened)
        static readonly Dictionary<int, Color> ForeMap = new Dictionary<int, Color> {
            { Color.FromArgb(32, 32, 32).ToArgb(), Text }, { Color.Black.ToArgb(), Text }, { Color.FromArgb(110, 110, 110).ToArgb(), Muted },
            { Color.FromArgb(109, 109, 109).ToArgb(), Dim }, { Color.FromArgb(0, 90, 170).ToArgb(), Text }, { Color.FromArgb(0, 120, 212).ToArgb(), Accent },
            { Color.FromArgb(196, 43, 28).ToArgb(), Red }, { Color.FromArgb(16, 124, 16).ToArgb(), Green }, { Color.FromArgb(200, 90, 0).ToArgb(), Orange },
            { Color.FromArgb(160, 90, 0).ToArgb(), Orange }, { Color.FromArgb(120, 60, 160).ToArgb(), Purple } };

        static readonly HashSet<Control> hooked = new HashSet<Control>();
        static readonly HashSet<Button> rounded = new HashSet<Button>();
        static readonly HashSet<object> restructured = new HashSet<object>();
        static readonly HashSet<Form> themedForms = new HashSet<Form>();
        static bool idleHooked;
        static float k = 1f;
        static int S(int v) { return (int)Math.Round(v * k); }

        // ---- native dark parts (scroll bars, combo box frames, title bar) ----
        [DllImport("uxtheme.dll", EntryPoint = "#135")] static extern int SetPreferredAppMode(int mode);
        [DllImport("uxtheme.dll", EntryPoint = "#133")] static extern bool AllowDarkModeForWindow(IntPtr h, bool allow);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string ids);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, int flags);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr rect, IntPtr rgn, int flags);

        public static void Init() { try { SetPreferredAppMode(2); } catch { } }   // 2 = force dark (context menus, scroll bars); Windows 10 1903+

        static void Native(Control c, string theme)
        {
            Action a = () => { try { AllowDarkModeForWindow(c.Handle, true); SetWindowTheme(c.Handle, theme, null); } catch { } };
            if (c.IsHandleCreated) a(); else c.HandleCreated += (s, e) => a();
        }

        static void TitleBar(Form f)
        {
            Action a = () =>
            {
                try
                {
                    int on = 1, cap = ColorTranslator.ToWin32(Bar), txt = ColorTranslator.ToWin32(Text);
                    DwmSetWindowAttribute(f.Handle, 20, ref on, 4);    // DWMWA_USE_IMMERSIVE_DARK_MODE
                    DwmSetWindowAttribute(f.Handle, 35, ref cap, 4);   // DWMWA_CAPTION_COLOR (Windows 11)
                    DwmSetWindowAttribute(f.Handle, 36, ref txt, 4);   // DWMWA_TEXT_COLOR
                }
                catch { }
            };
            if (f.IsHandleCreated) a(); else f.HandleCreated += (s, e) => a();
        }

        // once per main window: title bar, and the app's other windows (UE4 settings, report) themed when they open
        public static void Attach(Form f, float dpi)
        {
            k = dpi;
            themedForms.Add(f);
            TitleBar(f);
            if (idleHooked) return;
            idleHooked = true;
            Application.Idle += (s, e) =>
            {
                foreach (Form of in Application.OpenForms)
                    if (!themedForms.Contains(of)) { themedForms.Add(of); TitleBar(of); ApplyTree(of); }
            };
        }

        // after every BuildLayout (also the language switch): tab strip, name, category cards, colours
        public static void Apply(Form f, TabControl tabs, RadioButton[] categories, int firstStep, int lastStep)
        {
            Restructure(tabs, categories, firstStep, lastStep);
            ApplyTree(f);
        }

        // ---- colours ----
        static double Lum(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0; }
        static bool Same(Color c, int r, int g, int b) { return c.R == r && c.G == g && c.B == b; }

        public static Color Fore(Color c)
        {
            if (c.A < 255) return c;
            Color m;
            if (ForeMap.TryGetValue(c.ToArgb(), out m)) return m;
            if (Lum(c) >= 0.5) return c;
            return Color.FromArgb(255 - (int)((255 - c.R) * 0.35), 255 - (int)((255 - c.G) * 0.35), 255 - (int)((255 - c.B) * 0.35));
        }

        static void MapFore(Control c)
        {
            if (c is Button) return;
            var m = Fore(c.ForeColor);
            if (m != c.ForeColor) c.ForeColor = m;
        }

        static void MapBack(Control c)
        {
            if (c is Button || c is CatCard || c is TabStrip || c is Brand) return;
            var col = c.BackColor;
            if (col.A < 255 || Lum(col) < 0.55) return;   // already dark (or transparent)
            Color t;
            if (c is Form) t = Window;
            else if (c is TabPage) t = Page;
            else if (c is TextBoxBase) t = ((TextBoxBase)c).BorderStyle == BorderStyle.None ? Page : Same(col, 240, 247, 255) ? InputFocus : Input;
            else if (c is ListView || c is ComboBox || c is NumericUpDown || c is ListBox) t = Input;
            else if (Same(col, 225, 225, 225) || Same(col, 200, 200, 200)) t = Line;   // separator lines
            else if (Same(col, 248, 248, 248)) t = Bar;
            else
            {
                c.ResetBackColor();   // inherit the (dark) parent
                if (Lum(c.BackColor) < 0.55) return;
                t = Page;
            }
            if (c.BackColor != t) c.BackColor = t;
        }

        static void StyleButton(Button b)
        {
            bool primary = Same(b.BackColor, 0, 120, 212) || b.BackColor == Accent;
            b.FlatStyle = FlatStyle.Flat; b.UseVisualStyleBackColor = false; b.FlatAppearance.BorderSize = 1;
            if (primary)
            {
                b.BackColor = Accent; b.ForeColor = OnAccent; b.FlatAppearance.BorderColor = Accent;
                b.FlatAppearance.MouseOverBackColor = AccentHover; b.FlatAppearance.MouseDownBackColor = AccentHover;
            }
            else
            {
                b.BackColor = Btn; b.ForeColor = Text; b.FlatAppearance.BorderColor = BtnBorder;
                b.FlatAppearance.MouseOverBackColor = BtnHover; b.FlatAppearance.MouseDownBackColor = BtnDown;
            }
            if (rounded.Add(b)) RoundButton(b);
        }

        // rounded corners: the button is painted over (corners in the parent's colour, rounded fill + border, centred text)
        static void RoundButton(Button b)
        {
            bool hover = false, down = false;
            b.MouseEnter += (s, e) => { hover = true; b.Invalidate(); };
            b.MouseLeave += (s, e) => { hover = false; down = false; b.Invalidate(); };
            b.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { down = true; b.Invalidate(); } };
            b.MouseUp += (s, e) => { down = false; b.Invalidate(); };
            b.EnabledChanged += (s, e) => b.Invalidate();
            b.Disposed += (s, e) => rounded.Remove(b);
            b.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(b.Parent != null ? b.Parent.BackColor : Page);
                bool primary = b.BackColor == Accent;
                Color fill = !b.Enabled ? (primary ? AccentOff : Btn) : down || hover ? (primary ? AccentHover : down ? BtnDown : BtnHover) : b.BackColor;
                Color edge = primary ? fill : b.Focused ? BorderHover : BtnBorder;
                using (var path = CatCard.Round(new Rectangle(0, 0, b.Width - 1, b.Height - 1), S(7)))
                {
                    using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                    using (var p = new Pen(edge)) g.DrawPath(p, path);
                }
                TextRenderer.DrawText(g, b.Text, b.Font, new Rectangle(b.Padding.Left, 0, b.Width - b.Padding.Horizontal, b.Height), b.Enabled ? b.ForeColor : Dim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            };
        }

        public static void ApplyTree(Control c)
        {
            if (c.IsDisposed) return;
            if (c is Button) StyleButton((Button)c);
            else { MapBack(c); MapFore(c); }
            var ll = c as LinkLabel;
            if (ll != null) { ll.LinkColor = Accent; ll.ActiveLinkColor = AccentHover; ll.VisitedLinkColor = Accent; ll.DisabledLinkColor = Dim; }
            if (c is ComboBox) DarkCombo((ComboBox)c);
            else if (c is ListView) DarkList((ListView)c);
            else if (c is TextBoxBase || c is ScrollableControl) Native(c, "DarkMode_Explorer");
            if (hooked.Add(c))
            {
                c.ControlAdded += (s, e) => ApplyTree(e.Control);
                if (!(c is Button))
                {
                    c.ForeColorChanged += (s, e) => MapFore((Control)s);   // colours the app sets later (state labels, key boxes)
                    c.BackColorChanged += (s, e) => MapBack((Control)s);
                }
                c.Disposed += (s, e) => hooked.Remove((Control)s);
                // disabled text is drawn in a colour made from the background, which vanishes on dark: draw it grey
                if (c is Label && !(c is LinkLabel))
                    c.Paint += (s, e) =>
                    {
                        var l = (Label)s;
                        if (l.Enabled) return;
                        using (var b = new SolidBrush(l.BackColor)) e.Graphics.FillRectangle(b, l.ClientRectangle);
                        var r = new Rectangle(l.Padding.Left, l.Padding.Top, l.ClientSize.Width - l.Padding.Horizontal, l.ClientSize.Height - l.Padding.Vertical);
                        TextRenderer.DrawText(e.Graphics, l.Text, l.Font, r, Dim, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
                    };
                if (c is CheckBox || c is RadioButton)   // keep the glyph, redraw the text
                    c.Paint += (s, e) =>
                    {
                        var b = (ButtonBase)s;
                        if (b.Enabled) return;
                        int x = S(17);
                        using (var br = new SolidBrush(b.BackColor)) e.Graphics.FillRectangle(br, x - S(1), 0, b.ClientSize.Width, b.ClientSize.Height);
                        TextRenderer.DrawText(e.Graphics, b.Text, b.Font, new Rectangle(x, 0, b.ClientSize.Width - x, b.ClientSize.Height), Dim,
                            TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    };
            }
            foreach (Control ch in c.Controls) ApplyTree(ch);
        }

        // ---- list views: dark header and rows; the app's per-row colours are mapped when drawn ----
        static void DarkList(ListView lv)
        {
            if (lv.OwnerDraw) return;
            lv.OwnerDraw = true;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Input; lv.ForeColor = Text;
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lv, true, null);
            if (lv.SmallImageList == null) lv.SmallImageList = new ImageList { ImageSize = new Size(1, S(26)) };   // taller rows
            Action hdr = () => { try { var h = SendMessage(lv.Handle, 0x101F, IntPtr.Zero, IntPtr.Zero); AllowDarkModeForWindow(h, true); SetWindowTheme(h, "DarkMode_ItemsView", null); } catch { } };
            if (lv.IsHandleCreated) hdr(); else lv.HandleCreated += (s, e) => hdr();   // LVM_GETHEADER: the area right of the last column
            Native(lv, "DarkMode_Explorer");
            lv.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(Bar)) e.Graphics.FillRectangle(b, e.Bounds);
                using (var p = new Pen(Border))
                {
                    e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + S(6), e.Bounds.Right - 1, e.Bounds.Bottom - S(6));
                    e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
                TextRenderer.DrawText(e.Graphics, e.Header.Text, lv.Font, new Rectangle(e.Bounds.X + S(8), e.Bounds.Y, e.Bounds.Width - S(12), e.Bounds.Height), Muted,
                    Align(e.Header.TextAlign) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            };
            lv.DrawItem += (s, e) => { e.DrawDefault = false; };
            lv.DrawSubItem += (s, e) =>
            {
                var it = e.Item;
                var r = e.Bounds;
                if (e.ColumnIndex == 0) r.Width = lv.Columns[0].Width;   // sub-item 0 reports the whole row
                using (var b = new SolidBrush(it.Selected ? Sel : it.Index % 2 == 0 ? Input : InputAlt)) e.Graphics.FillRectangle(b, r);
                int x = r.X + S(8);
                var il = lv.SmallImageList;
                if (e.ColumnIndex == 0 && il != null && il.ImageSize.Width > 1)
                {
                    int idx = it.ImageIndex >= 0 ? it.ImageIndex : !string.IsNullOrEmpty(it.ImageKey) ? il.Images.IndexOfKey(it.ImageKey) : -1;
                    if (idx >= 0) il.Draw(e.Graphics, x, r.Y + (r.Height - il.ImageSize.Height) / 2, idx);
                    x += il.ImageSize.Width + S(8);
                }
                var fore = Fore(it.UseItemStyleForSubItems ? it.ForeColor : e.SubItem.ForeColor);
                var font = it.UseItemStyleForSubItems ? it.Font : e.SubItem.Font;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, font, new Rectangle(x, r.Y, Math.Max(0, r.Right - x - S(4)), r.Height), fore,
                    Align(lv.Columns[e.ColumnIndex].TextAlign) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            };
            ListViewItem last = null;   // owner-drawn rows with full-row select repaint only the first cell on hover: repaint the row
            lv.MouseMove += (s, e) =>
            {
                try
                {
                    var it = lv.GetItemAt(e.X, e.Y);
                    if (it == last) return;
                    if (last != null && last.ListView == lv) lv.Invalidate(last.Bounds);
                    if (it != null) lv.Invalidate(it.Bounds);
                    last = it;
                }
                catch { last = null; }
            };
        }

        // combo boxes: dark frame from the system theme, the items (and the closed box) drawn here so disabled text stays readable
        static void DarkCombo(ComboBox cb)
        {
            Native(cb, "DarkMode_CFD");
            if (cb.DrawMode != DrawMode.Normal) return;
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.DrawItem += (s, e) =>
            {
                var c = (ComboBox)s;
                bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
                bool sel = !edit && (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? Sel : Input)) e.Graphics.FillRectangle(b, e.Bounds);
                if (e.Index < 0 || e.Index >= c.Items.Count) return;
                var r = new Rectangle(e.Bounds.X + S(4), e.Bounds.Y, e.Bounds.Width - S(6), e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, c.GetItemText(c.Items[e.Index]), c.Font, r, c.Enabled ? Text : Dim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            cb.EnabledChanged += (s, e) => ((ComboBox)s).Invalidate();
        }

        static TextFormatFlags Align(HorizontalAlignment a)
        {
            return a == HorizontalAlignment.Right ? TextFormatFlags.Right : a == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
        }

        // ---- layout: tab strip, name, category cards ----
        static void Restructure(TabControl tabs, RadioButton[] cats, int firstStep, int lastStep)
        {
            if (tabs == null || tabs.IsDisposed || !restructured.Add(tabs)) return;
            var outer = tabs.Parent as TableLayoutPanel;
            if (outer == null) return;

            // tab headers -> flat strip above the pages (the TabControl keeps the pages and the selected index)
            new TabHider(tabs);
            var pos = outer.GetPositionFromControl(tabs);
            var holder = new Panel { Dock = DockStyle.Fill, Margin = tabs.Margin, Padding = new Padding(0) };
            outer.Controls.Remove(tabs);
            tabs.Margin = new Padding(0); tabs.Dock = DockStyle.Fill;
            holder.Controls.Add(tabs);
            holder.Controls.Add(new TabStrip(tabs, firstStep, lastStep));
            tabs.BringToFront();
            outer.Controls.Add(holder, pos.Column, pos.Row);

            // header: name on the left, the language switch stays on the right
            var header = outer.GetControlFromPosition(0, 0);
            if (header != null)
            {
                var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, S(6)), Padding = new Padding(0) };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                outer.Controls.Remove(header);
                header.Dock = DockStyle.Fill; header.Margin = new Padding(0, S(8), 0, 0);
                row.Controls.Add(new Brand(), 0, 0);
                row.Controls.Add(header, 1, 0);
                outer.Controls.Add(row, 0, 0);
            }

            // 3D method step: the category radio buttons (geo-11, own wrappers, TriDef) become three square cards
            if (cats != null && cats.Length == 3 && cats[0].Parent != null)
            {
                var cards = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, S(2), 0, S(2)), Padding = new Padding(0) };
                cards.Controls.Add(new CatCard(cats[0], "", Accent, "geo-11 3D", Lang.T("DirectX 9 / 11 게임"), Lang.T("커뮤니티 픽스 · Unity · DX11 · UE4 · DX9"), Muted));
                cards.Controls.Add(new CatCard(cats[1], "", Blue, Lang.T("자체 래퍼"), Lang.T("OpenGL · Vulkan · DirectX 12 게임"), "OGL3D · VK3D · DX12_3D (+ DXVK)", Muted));
                cards.Controls.Add(new CatCard(cats[2], "", Purple, "TriDef 3D", Lang.T("TriDef 프로필이 있는 Steam 게임"), Lang.T("TriDef 3D 설치 필수"), Red));
                foreach (var r in cats) r.Visible = false;
                cats[0].Parent.Controls.Add(cards);
            }
        }

        // ---- screenshot (GeoGamingHub.exe --shot <png> <tab> [game exe]): shown off-screen without taking focus, captured, exit ----
        public static void Shot(Form f, string png, Action prepare)
        {
            f.StartPosition = FormStartPosition.Manual;
            f.ShowInTaskbar = false;
            f.Location = new Point(-20000, 100);
            IntPtr h = f.Handle;
            ShowWindow(h, 4);   // SW_SHOWNOACTIVATE
            int step = 0;
            var t = new Timer { Interval = 1200 };
            t.Tick += (s, e) =>
            {
                if (++step == 1) { prepare(); return; }
                t.Stop();
                RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, 0x1 | 0x4 | 0x80 | 0x100);   // invalidate + erase + all children + update now
                Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); }   // PW_RENDERFULLCONTENT
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
                Application.Exit();
            };
            t.Start();
            Application.Run();
        }
    }

    // Hides the system tab headers and frame: the pages fill the whole control (TCM_ADJUSTRECT left as the client area).
    sealed class TabHider : NativeWindow
    {
        public TabHider(TabControl t)
        {
            if (t.IsHandleCreated) AssignHandle(t.Handle);
            t.HandleCreated += (s, e) => AssignHandle(t.Handle);
            t.HandleDestroyed += (s, e) => ReleaseHandle();
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x1328) { m.Result = (IntPtr)1; return; }   // TCM_ADJUSTRECT
            base.WndProc(ref m);
        }
    }

    // Flat tab strip: page titles, the selected one white with a green underline; the wizard steps are numbered and grouped.
    sealed class TabStrip : Control
    {
        readonly TabControl tabs;
        readonly int firstStep, lastStep;
        readonly List<Rectangle> rects = new List<Rectangle>();
        int hover = -1;
        float k;
        int S(int v) { return (int)Math.Round(v * k); }

        public TabStrip(TabControl t, int firstStep, int lastStep)
        {
            tabs = t; this.firstStep = firstStep; this.lastStep = lastStep;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Dock = DockStyle.Top; Height = S(46); Cursor = Cursors.Hand; BackColor = Theme.Window; Margin = new Padding(0);
            tabs.SelectedIndexChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            rects.Clear();
            int x = 0, pad = S(12);
            using (var font = new Font(Font.FontFamily, 10.5f))
            using (var small = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
            using (var line = new Pen(Theme.Border))
            {
                for (int i = 0; i < tabs.TabPages.Count; i++)
                {
                    if (i == firstStep || i == lastStep + 1) { x += S(6); g.DrawLine(line, x, S(15), x, Height - S(15)); x += S(6); }
                    string text = tabs.TabPages[i].Text;
                    bool step = i >= firstStep && i <= lastStep, sel = i == tabs.SelectedIndex;
                    var ts = TextRenderer.MeasureText(g, text, font, Size.Empty, TextFormatFlags.NoPadding);
                    int badge = S(20);
                    int w = pad * 2 + ts.Width + (step ? badge + S(7) : 0);
                    rects.Add(new Rectangle(x, 0, w, Height));
                    int tx = x + pad;
                    if (step)
                    {
                        var br = new Rectangle(tx, (Height - badge) / 2, badge, badge);
                        using (var b = new SolidBrush(sel ? Theme.Accent : Theme.Btn)) g.FillEllipse(b, br);
                        TextRenderer.DrawText(g, (i - firstStep + 1).ToString(), small, br, sel ? Theme.OnAccent : Theme.Text2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                        tx += badge + S(7);
                    }
                    TextRenderer.DrawText(g, text, font, new Rectangle(tx, 0, ts.Width + S(4), Height), sel ? Theme.Text : i == hover ? Theme.Text2 : Theme.Muted,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    if (sel)   // teal -> blue, like the two halves of the icon's screen
                    {
                        var u = new Rectangle(x + S(6), Height - S(3), w - S(12), S(3));
                        using (var b = new LinearGradientBrush(new Rectangle(u.X - 1, u.Y, u.Width + 2, u.Height), Theme.Accent, Theme.Accent2, LinearGradientMode.Horizontal)) g.FillRectangle(b, u);
                    }
                    x += w;
                }
                g.DrawLine(line, 0, Height - 1, Width, Height - 1);
            }
        }

        int HitTab(Point p) { for (int i = 0; i < rects.Count; i++) if (rects[i].Contains(p)) return i; return -1; }
        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTab(e.Location); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int h = HitTab(e.Location); if (h >= 0 && h < tabs.TabPages.Count) tabs.SelectedIndex = h; base.OnMouseClick(e); }
    }

    // App icon (src\app.ico, embedded as a resource) and name at the top left.
    sealed class Brand : Control
    {
        float k;
        Image logo;
        int S(int v) { return (int)Math.Round(v * k); }
        public Brand()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Size = new Size(S(250), S(40)); Margin = new Padding(0); BackColor = Color.Transparent;
            try
            {
                using (var st = typeof(Brand).Assembly.GetManifestResourceStream("GeoGamingHub.app.ico"))
                    if (st != null) using (var ic = new Icon(st, new Size(S(32), S(32)))) logo = ic.ToBitmap();
            }
            catch { }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (logo != null) g.DrawImage(logo, new Rectangle(0, S(4), S(32), S(32)));
            using (var f = new Font("Segoe UI", 12.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, "GEO GAMING HUB", f, new Rectangle(S(42), 0, Width - S(42), Height), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
        protected override void Dispose(bool disposing) { if (disposing && logo != null) logo.Dispose(); base.Dispose(disposing); }
    }

    // One 3D-method category as a square card; clicking it checks MainForm's own (hidden) category radio button.
    sealed class CatCard : Control
    {
        readonly RadioButton target;
        readonly string glyph, title, sub, line2;
        readonly Color glyphColor, line2Color;
        bool hover;
        float k;
        int S(int v) { return (int)Math.Round(v * k); }

        public CatCard(RadioButton target, string glyph, Color glyphColor, string title, string sub, string line2, Color line2Color)
        {
            this.target = target; this.glyph = glyph; this.glyphColor = glyphColor; this.title = title; this.sub = sub; this.line2 = line2; this.line2Color = line2Color;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Size = new Size(S(318), S(116)); Margin = new Padding(0, 0, S(14), S(8)); Cursor = Cursors.Hand; TabStop = true;
            AccessibleName = title; AccessibleRole = AccessibleRole.RadioButton;
            target.CheckedChanged += (s, e) => Invalidate();
            target.EnabledChanged += (s, e) => Invalidate();
        }

        void Pick() { Focus(); if (target.Enabled) target.Checked = true; }
        protected override void OnClick(EventArgs e) { Pick(); base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Pick(); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            var p = new GraphicsPath();
            int d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Page);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool on = target.Checked;
            var r = new Rectangle(S(1), S(1), Width - S(3), Height - S(3));
            using (var path = Round(r, S(10)))
            {
                using (var b = new SolidBrush(on ? Theme.CardOn : Theme.Card)) g.FillPath(b, path);
                using (var p = new Pen(on ? Theme.Accent : hover || Focused ? Theme.BorderHover : Theme.Border, on ? S(2) : 1)) g.DrawPath(p, path);
            }
            using (var icon = new Font("Segoe MDL2 Assets", 17f))
                TextRenderer.DrawText(g, glyph, icon, new Rectangle(S(16), S(14), S(30), S(30)), glyphColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (var f = new Font(Font.FontFamily, 12.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, title, f, new Point(S(56), S(12)), Theme.Text, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            using (var f = new Font(Font.FontFamily, 9.5f))
            {
                TextRenderer.DrawText(g, sub, f, new Point(S(56), S(40)), Theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, line2, f, new Rectangle(S(18), S(74), Width - S(36), S(26)), line2Color,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
            // selection mark at the top right: a ring, or a green circle with a check
            var c = new Rectangle(Width - S(38), S(14), S(22), S(22));
            if (on)
            {
                using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, c);
                using (var f = new Font("Segoe MDL2 Assets", 9f)) TextRenderer.DrawText(g, "", f, c, Theme.OnAccent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else using (var p = new Pen(hover ? Theme.BorderHover : Theme.BtnBorder, S(2))) g.DrawEllipse(p, c);
        }
    }
}
