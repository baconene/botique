using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>Shared look &amp; feel helpers.</summary>
    public static class Ui
    {
        public static float Scale = 1f;
        public static int S(int px) { return (int)Math.Round(px * Scale); }

        // Same palette as the web back office (web/public/css/admin.css): lavender, plum and soft mint.
        public static readonly Color Accent = Color.FromArgb(105, 64, 110);      // boutique plum #69406e
        public static readonly Color AccentDark = Color.FromArgb(78, 46, 84);
        public static readonly Color AccentLight = Color.FromArgb(238, 229, 240);
        public static readonly Color Good = Color.FromArgb(30, 132, 80);
        public static readonly Color Warn = Color.FromArgb(255, 243, 214);
        public static readonly Color Bad = Color.FromArgb(253, 226, 226);
        public static readonly Color Muted = Color.FromArgb(119, 114, 133);
        public static readonly Color Canvas = Color.FromArgb(250, 249, 253);     // main work area
        public static readonly Color Backdrop = Color.FromArgb(238, 237, 245);   // behind the rounded workspace frame
        public static readonly Color Ink = Color.FromArgb(39, 35, 57);
        public static readonly Color Line = Color.FromArgb(233, 229, 238);
        public static readonly Color Eyebrow = Color.FromArgb(130, 98, 138);
        public static readonly Color Mint = Color.FromArgb(231, 245, 240);

        /// <summary>Display serif for page titles (the web uses Cormorant Garamond; Georgia ships with Windows).</summary>
        public static Font Serif(float size) { return new Font("Georgia", size); }

        public static Control PageHeading(string title, string description)
        {
            return PageHeading("", title, description);
        }

        /// <summary>Small uppercase eyebrow, serif title and a muted line, like the web back office page heads.</summary>
        public static Control PageHeading(string eyebrow, string title, string description)
        {
            bool hasEyebrow = !string.IsNullOrEmpty(eyebrow);
            Panel p = new Panel(); p.Dock = DockStyle.Top; p.Height = S(hasEyebrow ? 98 : 80);
            Label kicker = new Label(); kicker.Text = (eyebrow ?? "").ToUpperInvariant(); kicker.UseMnemonic = false;
            kicker.Font = new Font("Segoe UI Semibold", 8f); kicker.ForeColor = Eyebrow;
            kicker.Dock = DockStyle.Top; kicker.Height = S(20); kicker.Visible = hasEyebrow;
            Label heading = new Label(); heading.Text = title; heading.UseMnemonic = false;
            heading.Font = Serif(23f); heading.ForeColor = Ink;
            heading.Dock = DockStyle.Top; heading.Height = S(46);
            Label detail = new Label(); detail.Text = description; detail.ForeColor = Muted; detail.UseMnemonic = false;
            detail.Dock = DockStyle.Top; detail.Height = S(26);
            p.Controls.Add(detail); p.Controls.Add(heading); p.Controls.Add(kicker);
            return p;
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>First non-transparent background behind a control (for painting rounded corners).</summary>
        public static Color BackdropOf(Control c)
        {
            Control p = c.Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            return p != null ? p.BackColor : Canvas;
        }

        public static bool IsLight(Color c) { return c.GetBrightness() > 0.86f; }

        public static Color Shade(Color c, float amount)
        {
            return Color.FromArgb(c.A, (int)(c.R * (1 - amount)), (int)(c.G * (1 - amount)), (int)(c.B * (1 - amount)));
        }

        public static string Money(decimal d) { return d.ToString("C2", CultureInfo.CurrentCulture); }

        public static bool TryParseMoney(string s, out decimal value)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) { value = 0; return false; }
            if (decimal.TryParse(s, NumberStyles.Currency, CultureInfo.CurrentCulture, out value)) return true;
            // Accept any currency symbol ($, P, PHP, EUR...) by stripping everything but the number itself.
            StringBuilder sb = new StringBuilder();
            foreach (char c in s) if (char.IsDigit(c) || c == '.' || c == ',' || c == '-') sb.Append(c);
            string n = sb.ToString();
            return decimal.TryParse(n, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(n, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        public static Button MakeButton(string text, EventHandler click)
        {
            return MakeButton(text, click, false);
        }

        public static Button MakeButton(string text, EventHandler click, bool primary)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(S(14), S(6), S(14), S(6));
            b.Margin = new Padding(S(3), S(4), S(6), S(4));
            b.MinimumSize = new Size(0, S(40));
            b.Font = new Font("Segoe UI Semibold", 9.5f);
            if (primary)
            {
                b.BackColor = Accent; b.ForeColor = Color.White;
                b.BorderColor = Color.Empty;
            }
            else
            {
                b.BackColor = Color.White; b.ForeColor = AccentDark;
                b.BorderColor = Color.FromArgb(222, 214, 229);
            }
            if (click != null) b.Click += click;
            return b;
        }

        public static Label MakeLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.UseMnemonic = false;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(S(3), S(6), S(3), S(3));
            return l;
        }

        public static void StyleGrid(DataGridView g)
        {
            g.Dock = DockStyle.Fill;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoGenerateColumns = false;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Canvas;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.DefaultCellStyle.ForeColor = Ink;
            g.DefaultCellStyle.Padding = new Padding(S(8), 0, S(8), 0);
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = S(42);
            g.RowTemplate.Height = S(42);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 223, 234);
            g.DefaultCellStyle.SelectionForeColor = AccentDark;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 250, 251);
            g.GridColor = Color.FromArgb(235, 228, 232);
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        }

        public static DataGridViewTextBoxColumn Col(string header, string property, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.DataPropertyName = property;
            c.Name = property;
            c.ReadOnly = readOnly;
            if (width <= 0) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c.MinimumWidth = S(180); }
            else c.Width = S(width);
            return c;
        }

        public static DataGridViewTextBoxColumn MoneyCol(string header, string property, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn c = Col(header, property, width, readOnly);
            c.DefaultCellStyle.Format = "C2";
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return c;
        }

        public static DataGridViewTextBoxColumn NumCol(string header, string property, int width, bool readOnly)
        {
            DataGridViewTextBoxColumn c = Col(header, property, width, readOnly);
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            return c;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Grey placeholder text in a TextBox.</summary>
        public static void SetCue(TextBox tb, string cue)
        {
            EventHandler apply = delegate { SendMessage(tb.Handle, 0x1501, (IntPtr)1, cue); };
            if (tb.IsHandleCreated) apply(tb, EventArgs.Empty);
            tb.HandleCreated += apply;
        }

        /// <summary>Simple one-line input box. Returns null if cancelled.</summary>
        public static string Prompt(IWin32Window owner, string title, string message, string defaultValue)
        {
            using (Form f = new Form())
            {
                f.Text = title;
                f.Font = new Font("Segoe UI", 10f);
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MinimizeBox = false; f.MaximizeBox = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.AutoSize = true;
                f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                f.Padding = new Padding(S(12));

                TableLayoutPanel t = new TableLayoutPanel();
                t.AutoSize = true; t.ColumnCount = 1; t.Dock = DockStyle.Fill;
                Label l = new Label(); l.Text = message; l.AutoSize = true; l.MaximumSize = new Size(S(420), 0);
                TextBox tb = new TextBox(); tb.Text = defaultValue ?? ""; tb.Width = S(420);
                FlowLayoutPanel buttons = new FlowLayoutPanel();
                buttons.FlowDirection = FlowDirection.RightToLeft; buttons.AutoSize = true; buttons.Dock = DockStyle.Fill;
                Button ok = MakeButton("OK", null, true); ok.DialogResult = DialogResult.OK;
                Button cancel = MakeButton("Cancel", null); cancel.DialogResult = DialogResult.Cancel;
                buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
                t.Controls.Add(l); t.Controls.Add(tb); t.Controls.Add(buttons);
                f.Controls.Add(t);
                f.AcceptButton = ok; f.CancelButton = cancel;
                f.Shown += delegate { tb.Focus(); tb.SelectAll(); };
                return f.ShowDialog(owner) == DialogResult.OK ? tb.Text : null;
            }
        }

        public static void Error(IWin32Window owner, string message)
        {
            MessageBox.Show(owner, message, "Boutique POS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static bool Confirm(IWin32Window owner, string message)
        {
            return MessageBox.Show(owner, message, "Boutique POS", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public static string CsvEscape(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new char[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
