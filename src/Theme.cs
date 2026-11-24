using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>Rounded, flat button matching the web back office (.btn / .btn.accent).</summary>
    public class RoundButton : Button
    {
        bool hover, pressed;
        public Color BorderColor = Color.Empty;
        public int Radius = 10;

        public RoundButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseMnemonic = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.BackdropOf(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = BackColor;
            if (!Enabled) fill = Color.FromArgb(240, 238, 244);
            else if (pressed) fill = Ui.IsLight(BackColor) ? Ui.Shade(Ui.AccentLight, 0.05f) : Ui.Shade(BackColor, 0.22f);
            else if (hover) fill = Ui.IsLight(BackColor) ? Ui.AccentLight : Ui.Shade(BackColor, 0.14f);
            using (GraphicsPath path = Ui.RoundRect(r, Ui.S(Radius)))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                if (BorderColor != Color.Empty && Enabled)
                    using (Pen pen = new Pen(hover ? Ui.Shade(BorderColor, 0.1f) : BorderColor)) g.DrawPath(pen, path);
            }
            if (Focused && ShowFocusCues)
            {
                Rectangle f = Rectangle.Inflate(r, -Ui.S(3), -Ui.S(3));
                using (GraphicsPath ring = Ui.RoundRect(f, Math.Max(2, Ui.S(Radius) - Ui.S(3))))
                using (Pen pen = new Pen(Ui.IsLight(fill) ? Ui.Accent : Color.White, 1.5f))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawPath(pen, ring);
                }
            }
            Rectangle text = new Rectangle(Padding.Left, 0, Width - Padding.Horizontal, Height);
            TextRenderer.DrawText(g, Text, Font, text, Enabled ? ForeColor : Ui.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>White rounded card with a hairline border (.panel / .stat). Children inherit its fill colour.</summary>
    public class Card : Panel
    {
        public int Radius = 16;
        public Color BorderColor = Ui.Line;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            Padding = new Padding(Ui.S(20));
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.BackdropOf(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Ui.RoundRect(r, Ui.S(Radius)))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, path);
                if (BorderColor != Color.Empty) using (Pen pen = new Pen(BorderColor)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Sidebar navigation item: icon glyph + label, plum pill when selected (.nav-item / .nav-item.active).</summary>
    public class NavButton : Button
    {
        bool hover;
        public bool Selected;
        public string Glyph = "";
        public string Hint = "";   // keyboard shortcut shown on the right, e.g. "F1"
        static readonly string IconFont = PickIconFont();

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            UseMnemonic = false;
            Font = new Font("Segoe UI Semibold", 9.5f);
        }

        static string PickIconFont()
        {
            foreach (FontFamily f in FontFamily.Families)
                if (f.Name == "Segoe Fluent Icons") return f.Name;
            return "Segoe MDL2 Assets";   // Windows 10
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.BackdropOf(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, Ui.S(2), Width - 1, Height - Ui.S(4) - 1);
            Color fill = Selected ? Ui.Accent : (hover ? Color.FromArgb(245, 241, 248) : Ui.BackdropOf(this));
            Color ink = Selected ? Color.White : (hover ? Ui.Accent : Color.FromArgb(109, 101, 124));
            using (GraphicsPath path = Ui.RoundRect(r, Ui.S(11)))
            using (SolidBrush b = new SolidBrush(fill))
                g.FillPath(b, path);
            int x = Ui.S(14);
            if (Glyph.Length > 0)
            {
                using (Font icon = new Font(IconFont, 11f))
                    TextRenderer.DrawText(g, Glyph, icon, new Rectangle(x, r.Y, Ui.S(22), r.Height), ink,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                x += Ui.S(34);
            }
            int hintW = 0;
            if (Hint.Length > 0)
                using (Font small = new Font("Segoe UI", 8f))
                {
                    hintW = Ui.S(30);
                    TextRenderer.DrawText(g, Hint, small, new Rectangle(Width - hintW - Ui.S(8), r.Y, hintW, r.Height),
                        Selected ? Color.FromArgb(225, 206, 230) : Color.FromArgb(160, 152, 172),
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, r.Y, Width - x - hintW - Ui.S(10), r.Height), ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            if (Focused && ShowFocusCues)
                using (Pen pen = new Pen(Selected ? Color.White : Ui.Accent, 1.5f))
                {
                    pen.DashStyle = DashStyle.Dot;
                    using (GraphicsPath ring = Ui.RoundRect(Rectangle.Inflate(r, -Ui.S(3), -Ui.S(3)), Ui.S(8))) g.DrawPath(pen, ring);
                }
        }
    }
}
