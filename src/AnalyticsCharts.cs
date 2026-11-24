using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>Base for the Analytics charts: white, double-buffered, with a hover read-out box.</summary>
    public abstract class ChartBase : Control
    {
        protected Point? Mouse;
        protected static readonly Color GridLine = Color.FromArgb(238, 232, 243);
        protected static readonly Color Soft = Color.FromArgb(205, 185, 211);
        public static readonly Color[] Palette = {
            Color.FromArgb(105, 64, 110), Color.FromArgb(88, 174, 178), Color.FromArgb(217, 154, 43), Color.FromArgb(138, 123, 208),
            Color.FromArgb(194, 69, 61), Color.FromArgb(63, 143, 95), Color.FromArgb(176, 122, 161), Color.FromArgb(91, 107, 191) };

        protected ChartBase()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 8.5f);
        }

        protected override void OnMouseMove(MouseEventArgs e) { Mouse = e.Location; Invalidate(); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { Mouse = null; Invalidate(); base.OnMouseLeave(e); }

        protected void DrawTip(Graphics g, string title, IList<string> lines)
        {
            if (Mouse == null) return;
            using (Font bold = new Font("Segoe UI Semibold", 8.5f))
            {
                int w = TextRenderer.MeasureText(title, bold).Width;
                foreach (string l in lines) w = Math.Max(w, TextRenderer.MeasureText(l, Font).Width);
                w += Ui.S(20);
                int h = Ui.S(14) + Ui.S(17) * (lines.Count + 1);
                int x = Mouse.Value.X + Ui.S(14), y = Mouse.Value.Y + Ui.S(10);
                if (x + w > Width) x = Mouse.Value.X - w - Ui.S(10);
                if (y + h > Height) y = Math.Max(0, Height - h);
                Rectangle box = new Rectangle(x, y, w, h);
                using (GraphicsPath p = Ui.RoundRect(box, Ui.S(8)))
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 255, 255, 255))) g.FillPath(b, p);
                    using (Pen pen = new Pen(Ui.Line)) g.DrawPath(pen, p);
                }
                TextRenderer.DrawText(g, title, bold, new Point(x + Ui.S(10), y + Ui.S(7)), Ui.Ink);
                for (int i = 0; i < lines.Count; i++)
                    TextRenderer.DrawText(g, lines[i], Font, new Point(x + Ui.S(10), y + Ui.S(7) + Ui.S(17) * (i + 1)), Ui.Ink);
            }
        }

        protected static double NiceMax(double v)
        {
            if (v <= 0) return 1;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(v)));
            foreach (double m in new[] { 1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10 }) if (v <= m * mag) return m * mag;
            return 10 * mag;
        }

        protected void DrawLegend(Graphics g, IList<KeyValuePair<string, Color>> items, int y)
        {
            int x = Ui.S(6);
            foreach (KeyValuePair<string, Color> it in items)
            {
                using (SolidBrush b = new SolidBrush(it.Value)) g.FillRectangle(b, x, y + Ui.S(4), Ui.S(10), Ui.S(10));
                TextRenderer.DrawText(g, it.Key, Font, new Point(x + Ui.S(14), y), Ui.Muted);
                x += TextRenderer.MeasureText(it.Key, Font).Width + Ui.S(28);
            }
        }
    }

    /// <summary>Bars (grouped or stacked) on the left axis plus optional lines/areas on the right axis.</summary>
    public class ComboChart : ChartBase
    {
        public class Series
        {
            public string Name; public Color Color; public double[] Values; public bool Dashed, Area;
            public Series(string name, Color color, double[] values) { Name = name; Color = color; Values = values; }
        }

        public string[] Categories = new string[0];
        public List<Series> Bars = new List<Series>(), Lines = new List<Series>();
        public bool Stacked;
        public Func<double, string> LeftFormat = v => v.ToString("N0"), RightFormat = v => v.ToString("N1");
        public Func<int, int, Color> BarColor;          // (series, category) override, e.g. highlight a drop
        public int LabelEvery = 1;
        public bool LinesShareLeftAxis;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int n = Categories.Length;
            bool any = Bars.Concat(Lines).Any(s => s.Values.Any(v => v != 0));
            if (n == 0 || !any) { TextRenderer.DrawText(g, "No completed sales in this period.", Font, ClientRectangle, Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
            double leftMax = 0, rightMax = 0;
            for (int c = 0; c < n; c++)
            {
                double stack = 0;
                foreach (Series s in Bars) { stack += s.Values[c]; if (!Stacked) leftMax = Math.Max(leftMax, s.Values[c]); }
                if (Stacked) leftMax = Math.Max(leftMax, stack);
                foreach (Series s in Lines) { if (LinesShareLeftAxis) leftMax = Math.Max(leftMax, s.Values[c]); else rightMax = Math.Max(rightMax, s.Values[c]); }
            }
            leftMax = NiceMax(leftMax); rightMax = NiceMax(rightMax);
            bool hasRight = Lines.Count > 0 && !LinesShareLeftAxis;
            int legendH = Ui.S(22), labelH = Ui.S(20);
            int left = Ui.S(8) + TextRenderer.MeasureText(LeftFormat(leftMax), Font).Width + Ui.S(6);
            int right = hasRight ? TextRenderer.MeasureText(RightFormat(rightMax), Font).Width + Ui.S(14) : Ui.S(10);
            Rectangle plot = new Rectangle(left, Ui.S(8), Math.Max(10, Width - left - right), Math.Max(10, Height - Ui.S(8) - labelH - legendH));
            using (Pen grid = new Pen(GridLine))
                for (int i = 0; i <= 4; i++)
                {
                    int y = plot.Bottom - plot.Height * i / 4;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    TextRenderer.DrawText(g, LeftFormat(leftMax * i / 4), Font, new Rectangle(0, y - Ui.S(8), left - Ui.S(6), Ui.S(16)), Ui.Muted, TextFormatFlags.Right);
                    if (hasRight) TextRenderer.DrawText(g, RightFormat(rightMax * i / 4), Font, new Point(plot.Right + Ui.S(6), y - Ui.S(8)), Ui.Muted);
                }
            float slot = (float)plot.Width / n;
            int widest = Categories.Max(c => TextRenderer.MeasureText(c ?? "", Font).Width) + Ui.S(8);
            int labelEvery = Math.Max(Math.Max(1, LabelEvery), (int)Math.Ceiling(widest / Math.Max(1f, slot)));
            int groups = Stacked ? 1 : Math.Max(1, Bars.Count);
            float barW = Math.Max(2f, Math.Min(slot * 0.7f / groups, Ui.S(46)));
            int hover = Mouse.HasValue && plot.Contains(Mouse.Value) ? Math.Min(n - 1, (int)((Mouse.Value.X - plot.Left) / slot)) : -1;
            if (hover >= 0) using (SolidBrush hb = new SolidBrush(Color.FromArgb(245, 241, 248))) g.FillRectangle(hb, plot.Left + hover * slot, plot.Top, slot, plot.Height);
            for (int c = 0; c < n; c++)
            {
                float groupX = plot.Left + c * slot + (slot - barW * groups) / 2f;
                double stack = 0;
                for (int si = 0; si < Bars.Count; si++)
                {
                    double v = Bars[si].Values[c];
                    if (v <= 0) continue;
                    float h = (float)(v / leftMax * plot.Height);
                    float x = Stacked ? groupX : groupX + si * barW;
                    float y = plot.Bottom - (float)(stack / leftMax * plot.Height) - h;
                    Color col = BarColor != null ? BarColor(si, c) : Bars[si].Color;
                    RectangleF r = new RectangleF(x + 1, y, barW - 2, h);
                    using (SolidBrush b = new SolidBrush(col))
                    {
                        if (Stacked || h < Ui.S(8)) g.FillRectangle(b, r);
                        else using (GraphicsPath p = TopRounded(r, Math.Min(Ui.S(5), barW / 2))) g.FillPath(b, p);
                    }
                    if (Stacked) stack += v;
                }
                if (c % labelEvery == 0)
                    TextRenderer.DrawText(g, Categories[c], Font, new Rectangle((int)(plot.Left + c * slot - slot), plot.Bottom + Ui.S(3), (int)(slot * 3), labelH), Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
            foreach (Series s in Lines)
            {
                double max = LinesShareLeftAxis ? leftMax : rightMax;
                PointF[] pts = new PointF[n];
                for (int c = 0; c < n; c++) pts[c] = new PointF(plot.Left + c * slot + slot / 2, plot.Bottom - (float)(s.Values[c] / max * plot.Height));
                if (s.Area && n > 1)
                {
                    List<PointF> poly = new List<PointF>(pts); poly.Add(new PointF(pts[n - 1].X, plot.Bottom)); poly.Add(new PointF(pts[0].X, plot.Bottom));
                    using (LinearGradientBrush lg = new LinearGradientBrush(plot, Color.FromArgb(90, s.Color), Color.FromArgb(0, s.Color), 90f)) g.FillPolygon(lg, poly.ToArray());
                }
                using (Pen pen = new Pen(s.Color, Ui.S(2) + 0.5f))
                {
                    if (s.Dashed) pen.DashStyle = DashStyle.Dash;
                    if (n > 1) g.DrawLines(pen, pts);
                }
                if (!s.Dashed && n <= 31)
                    using (SolidBrush dot = new SolidBrush(s.Color))
                        foreach (PointF p in pts) g.FillEllipse(dot, p.X - Ui.S(3), p.Y - Ui.S(3), Ui.S(6), Ui.S(6));
            }
            List<KeyValuePair<string, Color>> legend = new List<KeyValuePair<string, Color>>();
            foreach (Series s in Bars) legend.Add(new KeyValuePair<string, Color>(s.Name, s.Color));
            foreach (Series s in Lines) legend.Add(new KeyValuePair<string, Color>(s.Name, s.Color));
            DrawLegend(g, legend, Height - legendH + Ui.S(2));
            if (hover >= 0)
            {
                List<string> lines = new List<string>();
                foreach (Series s in Bars) lines.Add(s.Name + ": " + LeftFormat(s.Values[hover]));
                foreach (Series s in Lines) lines.Add(s.Name + ": " + (LinesShareLeftAxis ? LeftFormat(s.Values[hover]) : RightFormat(s.Values[hover])));
                DrawTip(g, Categories[hover], lines);
            }
        }

        static GraphicsPath TopRounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Y + radius, r.Right, r.Bottom);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>7 × 24 sales heatmap (day of week × hour).</summary>
    public class HeatmapChart : ChartBase
    {
        public double[,] Values = new double[7, 24];
        public Func<double, string> Format = v => v.ToString("N0");

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            double max = 1; int pd = -1, ph = -1;
            for (int d = 0; d < 7; d++) for (int h = 0; h < 24; h++) if (Values[d, h] > max) { max = Values[d, h]; pd = d; ph = h; }
            int left = Ui.S(38), top = Ui.S(20), gap = Ui.S(3);
            float cw = (float)(Width - left - Ui.S(4)) / 24, ch = (float)(Height - top - Ui.S(4)) / 7;
            for (int h = 0; h < 24; h += 3)
                TextRenderer.DrawText(g, Hour(h), Font, new Point(left + (int)(h * cw), 0), Ui.Muted);
            int hd = -1, hh = -1;
            for (int d = 0; d < 7; d++)
            {
                TextRenderer.DrawText(g, AnalyticsReport.DayNames[d], Font, new Rectangle(0, top + (int)(d * ch), left - Ui.S(4), (int)ch), Ui.Muted, TextFormatFlags.VerticalCenter);
                for (int h = 0; h < 24; h++)
                {
                    RectangleF r = new RectangleF(left + h * cw, top + d * ch, cw - gap, ch - gap);
                    double v = Values[d, h];
                    int alpha = Math.Min(255, (int)(255 * (0.12 + 0.88 * v / max)));
                    Color c = v > 0 ? Color.FromArgb(alpha, 105, 64, 110) : Color.FromArgb(243, 240, 247);
                    using (GraphicsPath p = Ui.RoundRect(Rectangle.Round(r), Ui.S(4))) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
                    if (Mouse.HasValue && r.Contains(Mouse.Value)) { hd = d; hh = h; using (Pen pen = new Pen(Ui.Ink, 2)) g.DrawRectangle(pen, Rectangle.Round(r)); }
                }
            }
            if (hd >= 0) DrawTip(g, AnalyticsReport.DayNames[hd] + "  " + Hour(hh) + "–" + Hour((hh + 1) % 24), new[] { Format(Values[hd, hh]) });
            PeakDay = pd; PeakHour = ph;
        }

        public int PeakDay = -1, PeakHour = -1;
        public static string Hour(int h) { return (h % 12 == 0 ? 12 : h % 12) + (h < 12 ? "a" : "p"); }
    }

    /// <summary>Waterfall from net sales to net profit; colour by source (real, estimate, sample).</summary>
    public class WaterfallChart : ChartBase
    {
        public List<AnalyticsReport.WaterStep> Steps = new List<AnalyticsReport.WaterStep>();
        public Func<double, string> Format = v => v.ToString("N0");

        public static Color KindColor(string kind, decimal amount)
        {
            switch (kind)
            {
                case "real": return Ui.Accent;
                case "estimate": return Color.FromArgb(217, 154, 43);
                case "sample": return Color.FromArgb(181, 166, 196);
                default: return amount >= 0 ? Color.FromArgb(63, 143, 95) : Color.FromArgb(194, 69, 61);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Steps.Count == 0) return;
            double top = 0, low = 0, running = 0;
            List<double[]> bars = new List<double[]>();
            foreach (AnalyticsReport.WaterStep s in Steps)
            {
                double a = (double)s.Amount, from, to;
                if (s.Total) { from = 0; to = a; running = a; }
                else { from = running; running += a; to = running; }
                bars.Add(new[] { Math.Min(from, to), Math.Max(from, to) });
                top = Math.Max(top, Math.Max(from, to)); low = Math.Min(low, Math.Min(from, to));
            }
            double max = NiceMax(top), min = low < 0 ? -NiceMax(-low) : 0;
            int legendH = Ui.S(22), labelH = Ui.S(22);
            int left = Math.Max(TextRenderer.MeasureText(Format(max), Font).Width, TextRenderer.MeasureText(Format(min), Font).Width) + Ui.S(14);
            Rectangle plot = new Rectangle(left, Ui.S(8), Width - left - Ui.S(8), Height - Ui.S(8) - labelH - legendH);
            Func<double, float> Y = v => plot.Bottom - (float)((v - min) / (max - min) * plot.Height);
            using (Pen grid = new Pen(GridLine))
                for (int i = 0; i <= 4; i++)
                {
                    double v = min + (max - min) * i / 4; float y = Y(v);
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    TextRenderer.DrawText(g, Format(v), Font, new Rectangle(0, (int)y - Ui.S(8), left - Ui.S(6), Ui.S(16)), Ui.Muted, TextFormatFlags.Right);
                }
            float slot = (float)plot.Width / Steps.Count, w = Math.Min(slot * 0.6f, Ui.S(60));
            int hover = Mouse.HasValue && plot.Contains(Mouse.Value) ? Math.Min(Steps.Count - 1, (int)((Mouse.Value.X - plot.Left) / slot)) : -1;
            for (int i = 0; i < Steps.Count; i++)
            {
                float x = plot.Left + i * slot + (slot - w) / 2;
                float y1 = Y(bars[i][1]), y0 = Y(bars[i][0]);
                RectangleF r = new RectangleF(x, y1, w, Math.Max(2, y0 - y1));
                using (GraphicsPath p = Ui.RoundRect(Rectangle.Round(r), Ui.S(5))) using (SolidBrush b = new SolidBrush(KindColor(Steps[i].Kind, Steps[i].Amount))) g.FillPath(b, p);
                TextRenderer.DrawText(g, Steps[i].Label, Font, new Rectangle((int)(plot.Left + i * slot), plot.Bottom + Ui.S(4), (int)slot, labelH), Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
            DrawLegend(g, new[] {
                new KeyValuePair<string, Color>("Real", Ui.Accent), new KeyValuePair<string, Color>("Estimate", KindColor("estimate", 0)),
                new KeyValuePair<string, Color>("Sample", KindColor("sample", 0)), new KeyValuePair<string, Color>("Net profit", KindColor("result", Steps[Steps.Count - 1].Amount)) }, Height - legendH + Ui.S(2));
            if (hover >= 0) DrawTip(g, Steps[hover].Label, new[] { Format((double)Steps[hover].Amount), "Source: " + Steps[hover].Kind });
        }
    }

    /// <summary>Half-circle gauge (AOV ÷ CAC).</summary>
    public class GaugeChart : ChartBase
    {
        public double Value, Max = 5;
        public string Label = "";

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int size = Math.Min(Width - Ui.S(20), (Height - Ui.S(20)) * 2);
            Rectangle arc = new Rectangle((Width - size) / 2, Ui.S(10), size, size);
            float thick = Ui.S(16);
            Rectangle inner = Rectangle.Inflate(arc, -(int)(thick / 2), -(int)(thick / 2));
            double ratio = Math.Max(0, Math.Min(1, Value / Max));
            Color col = Value >= 3 ? Color.FromArgb(63, 143, 95) : Value >= 1.5 ? Color.FromArgb(217, 154, 43) : Color.FromArgb(194, 69, 61);
            using (Pen track = new Pen(Color.FromArgb(239, 234, 243), thick)) { track.StartCap = track.EndCap = LineCap.Round; g.DrawArc(track, inner, 180, 180); }
            if (ratio > 0) using (Pen p = new Pen(col, thick)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawArc(p, inner, 180, (float)(180 * ratio)); }
            using (Font big = new Font("Segoe UI Semibold", 20f))
                TextRenderer.DrawText(g, Value.ToString("0.00") + "×", big, new Rectangle(0, arc.Top + size / 2 - Ui.S(46), Width, Ui.S(36)), Ui.Ink, TextFormatFlags.HorizontalCenter);
            using (Font small = new Font("Segoe UI Semibold", 7.5f))
                TextRenderer.DrawText(g, Label.ToUpperInvariant(), small, new Rectangle(0, arc.Top + size / 2 - Ui.S(10), Width, Ui.S(16)), Ui.Eyebrow, TextFormatFlags.HorizontalCenter);
        }
    }

    /// <summary>Donut with a legend on the right.</summary>
    public class DonutChart : ChartBase
    {
        public List<KeyValuePair<string, double>> Slices = new List<KeyValuePair<string, double>>();
        public Func<double, string> Format = v => v.ToString("N0");

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            double total = Slices.Sum(s => s.Value);
            if (total <= 0) { TextRenderer.DrawText(g, "Nothing to show for this period.", Font, ClientRectangle, Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
            int d = Math.Min(Height - Ui.S(12), Width / 2);
            Rectangle r = new Rectangle(Ui.S(6), (Height - d) / 2, d, d);
            float start = -90;
            for (int i = 0; i < Slices.Count; i++)
            {
                float sweep = (float)(Slices[i].Value / total * 360);
                using (SolidBrush b = new SolidBrush(Palette[i % Palette.Length])) g.FillPie(b, r, start, Math.Max(0.5f, sweep - 1.5f));
                start += sweep;
            }
            Rectangle hole = Rectangle.Inflate(r, -d / 4, -d / 4);
            using (SolidBrush w = new SolidBrush(BackColor)) g.FillEllipse(w, hole);
            int y = (Height - Slices.Count * Ui.S(20)) / 2, x = r.Right + Ui.S(16);
            for (int i = 0; i < Slices.Count; i++)
            {
                using (SolidBrush b = new SolidBrush(Palette[i % Palette.Length])) g.FillRectangle(b, x, y + i * Ui.S(20) + Ui.S(4), Ui.S(10), Ui.S(10));
                TextRenderer.DrawText(g, Slices[i].Key + "  " + Format(Slices[i].Value) + " (" + (Slices[i].Value / total * 100).ToString("0") + "%)",
                    Font, new Point(x + Ui.S(16), y + i * Ui.S(20)), Ui.Ink);
            }
        }
    }
}
