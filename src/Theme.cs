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

    /// <summary>
    /// White rounded card with a hairline border and a soft drop shadow (.panel / .stat). Children inherit its
    /// fill colour. The shadow is painted inside the card's own bounds, so the card body is inset by the shadow
    /// size and children are laid out inside the body (see DisplayRectangle).
    /// </summary>
    public class Card : Panel
    {
        public int Radius = 16;
        public Color BorderColor = Ui.Line;
        public int Shadow = 7;                                       // unscaled px; 0 = flat
        public Color ShadowColor = Color.FromArgb(66, 48, 92);       // plum-tinted, like the web's box-shadows

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            Padding = new Padding(Ui.S(20));
        }

        int Sh { get { return Shadow <= 0 ? 0 : Ui.S(Shadow); } }

        /// <summary>The visible card body: room for the shadow on the sides, a little above and more below.</summary>
        Rectangle Body
        {
            get
            {
                int s = Sh;
                return new Rectangle(s, s / 2, Math.Max(1, Width - 2 * s - 1), Math.Max(1, Height - s / 2 - s * 3 / 2 - 1));
            }
        }

        public override Rectangle DisplayRectangle
        {
            get
            {
                Rectangle r = base.DisplayRectangle;
                int s = Sh;
                return new Rectangle(r.X + s, r.Y + s / 2, Math.Max(0, r.Width - 2 * s), Math.Max(0, r.Height - s / 2 - s * 3 / 2));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.BackdropOf(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle body = Body;
            int s = Sh, radius = Ui.S(Radius);
            // Soft shadow: stacked, faint rounded layers, shifted down a little.
            for (int i = s; i >= 1; i--)
            {
                int alpha = (int)Math.Round(9.0 * (1.0 - (double)(i - 1) / s) + 1);
                Rectangle layer = Rectangle.Inflate(body, i, i);
                layer.Offset(0, s / 3);
                using (GraphicsPath sp = Ui.RoundRect(layer, radius + i))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(alpha, ShadowColor)))
                    g.FillPath(sb, sp);
            }
            using (GraphicsPath path = Ui.RoundRect(body, radius))
            {
                using (SolidBrush b = new SolidBrush(BackColor)) g.FillPath(b, path);
                if (BorderColor != Color.Empty) using (Pen pen = new Pen(BorderColor)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Minimise / maximise / close button for the borderless main window.</summary>
    public class CaptionButton : Button
    {
        bool hover, pressed;
        public string Glyph = "";
        public bool IsClose;

        public CaptionButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Color back = Ui.BackdropOf(this), ink = Ui.Muted;
            if (hover && IsClose) { back = pressed ? Color.FromArgb(160, 30, 22) : Color.FromArgb(196, 43, 28); ink = Color.White; }
            else if (hover) { back = pressed ? Color.FromArgb(222, 214, 229) : Color.FromArgb(236, 232, 241); ink = Ui.Ink; }
            g.Clear(back);
            using (Font icon = new Font(NavButton.IconFontName, 7.5f))
                TextRenderer.DrawText(g, Glyph, icon, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
        public static string IconFontName { get { return IconFont; } }

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
