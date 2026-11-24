using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>
    /// "Your boutique, at a glance": today's figures, a 14-day sales chart, today's payment mix and a stock watch.
    /// Mirrors the web back office overview. Read-only; sections follow the signed-in role.
    /// </summary>
    public class OverviewPanel : UserControl
    {
        readonly MainForm main;
        readonly StatCard statSales, statCount, statItems, statLow;
        readonly Card chartCard, payCard, stockCard;
        readonly SalesChart chart;
        readonly Label lblChartTotal, lblNoSales;
        readonly RowList payments, stock;

        public OverviewPanel(MainForm owner)
        {
            main = owner;
            BackColor = Ui.Canvas;
            DoubleBuffered = true;

            TableLayoutPanel stats = new TableLayoutPanel();
            stats.Dock = DockStyle.Top; stats.Height = Ui.S(146); stats.ColumnCount = 4; stats.RowCount = 1;
            stats.BackColor = Ui.Canvas;
            for (int i = 0; i < 4; i++) stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            statSales = new StatCard("Sales today · incl. tax", "Completed receipts only", true);
            statCount = new StatCard("Transactions", "Completed today", false);
            statItems = new StatCard("Pieces sold", "Units sold today", false);
            statLow = new StatCard("Needs attention", "Low or out-of-stock variants", false);
            statLow.BackColor = Ui.Mint;
            statLow.Cursor = Cursors.Hand;
            statLow.Click += delegate { main.OpenInventory(); };
            foreach (StatCard c in new StatCard[] { statSales, statCount, statItems, statLow })
            {
                c.Dock = DockStyle.Fill; c.Margin = new Padding(0, 0, c == statLow ? 0 : Ui.S(4), Ui.S(6));
                stats.Controls.Add(c);
            }

            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill; body.ColumnCount = 2; body.RowCount = 2; body.BackColor = Ui.Canvas;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));

            // Sales story
            chartCard = new Card(); chartCard.Dock = DockStyle.Fill; chartCard.Margin = new Padding(0, 0, Ui.S(4), 0);
            Label chartKicker = Kicker("Your sales story");
            Label chartTitle = Title("The last two weeks");
            lblChartTotal = new Label(); lblChartTotal.Dock = DockStyle.Top; lblChartTotal.Height = Ui.S(44);
            lblChartTotal.Font = new Font("Segoe UI Semibold", 20f); lblChartTotal.ForeColor = Ui.Ink; lblChartTotal.UseMnemonic = false;
            lblNoSales = new Label(); lblNoSales.Dock = DockStyle.Bottom; lblNoSales.Height = Ui.S(24); lblNoSales.ForeColor = Ui.Muted;
            lblNoSales.Text = "No completed sales in the last two weeks.";
            chart = new SalesChart(); chart.Dock = DockStyle.Fill;
            chartCard.Controls.Add(chart); chartCard.Controls.Add(lblNoSales);
            chartCard.Controls.Add(lblChartTotal); chartCard.Controls.Add(chartTitle); chartCard.Controls.Add(chartKicker);
            body.Controls.Add(chartCard, 0, 0);
            body.SetRowSpan(chartCard, 2);

            // Payment mix
            payCard = new Card(); payCard.Dock = DockStyle.Fill; payCard.Margin = new Padding(0, 0, 0, Ui.S(2));
            payments = new RowList(); payments.Dock = DockStyle.Fill; payments.Empty = "Cash, card and other payments will appear here after checkout.";
            payCard.Controls.Add(payments); payCard.Controls.Add(Title("Payment mix")); payCard.Controls.Add(Kicker("Today's balance"));
            body.Controls.Add(payCard, 1, 0);

            // Stock watch
            stockCard = new Card(); stockCard.Dock = DockStyle.Fill; stockCard.Margin = Padding.Empty;
            stock = new RowList(); stock.Dock = DockStyle.Fill; stock.Empty = "Every item is above its reorder level.";
            Button review = Ui.MakeButton("Review inventory", delegate { main.OpenInventory(); });
            review.Dock = DockStyle.Bottom; review.AutoSize = false; review.Height = Ui.S(40);
            stockCard.Controls.Add(stock); stockCard.Controls.Add(review);
            stockCard.Controls.Add(Title("Stock watch")); stockCard.Controls.Add(Kicker("Keep the rails ready"));
            body.Controls.Add(stockCard, 1, 1);

            // Short windows: the stock watch gets the whole right column; the payment mix returns when there's room.
            body.Resize += delegate { FitRightColumn(body); };

            Controls.Add(body);
            Controls.Add(stats);
        }

        bool showSales;

        void FitRightColumn(TableLayoutPanel body)
        {
            bool roomy = body.Height >= Ui.S(440);
            payCard.Visible = showSales && roomy;
            float payShare = payCard.Visible ? 42f : 0f;
            if (body.RowStyles[0].Height != payShare)
            {
                body.RowStyles[0].Height = payShare;
                body.RowStyles[1].Height = 100f - payShare;
            }
        }

        static Label Kicker(string text)
        {
            Label l = new Label(); l.Text = text.ToUpperInvariant(); l.Dock = DockStyle.Top; l.Height = Ui.S(18);
            l.Font = new Font("Segoe UI Semibold", 7.5f); l.ForeColor = Ui.Eyebrow; l.UseMnemonic = false;
            return l;
        }

        static Label Title(string text)
        {
            Label l = new Label(); l.Text = text; l.Dock = DockStyle.Top; l.Height = Ui.S(32);
            l.Font = new Font("Segoe UI Semibold", 13f); l.ForeColor = Ui.Ink; l.UseMnemonic = false;
            return l;
        }

        /// <summary>Recomputes everything from the data cache and the store. Safe to call often.</summary>
        public void RefreshData()
        {
            bool canSales = Session.Can(Permissions.SalesView);
            bool canStock = Session.Can(Permissions.InventoryView) || Session.Can(Permissions.InventoryEdit) || Session.Can(Permissions.StockAdjust);
            statSales.Visible = statCount.Visible = statItems.Visible = chartCard.Visible = canSales;
            showSales = canSales;
            if (chartCard.Parent is TableLayoutPanel) FitRightColumn((TableLayoutPanel)chartCard.Parent);
            statLow.Visible = stockCard.Visible = canStock;

            if (canSales)
            {
                DateTime today = DateTime.Today;
                List<Sale> recent;
                try { recent = DataStore.SalesBetween(today.AddDays(-13), today.AddDays(1)); }
                catch (Exception ex) { recent = new List<Sale>(); main.SetStatus("Couldn't load sales for the overview. " + ex.Message, true); }
                List<Sale> done = recent.Where(s => !s.Refunded).ToList();
                List<Sale> todays = done.Where(s => s.Date >= today).ToList();
                statSales.Value = Ui.Money(todays.Sum(s => s.Total));
                statCount.Value = todays.Count.ToString(CultureInfo.CurrentCulture);
                statItems.Value = todays.Sum(s => s.ItemCount).ToString(CultureInfo.CurrentCulture);

                decimal[] days = new decimal[14];
                foreach (Sale s in done)
                {
                    int i = 13 - (int)(today - s.Date.Date).TotalDays;
                    if (i >= 0 && i < 14) days[i] += s.Total;
                }
                chart.SetData(today.AddDays(-13), days);
                lblChartTotal.Text = Ui.Money(days.Sum());
                lblNoSales.Visible = days.Sum() == 0m;

                decimal net = todays.Sum(s => s.Total);
                payments.SetRows(todays.GroupBy(s => string.IsNullOrEmpty(s.PaymentMethod) ? "Other" : s.PaymentMethod)
                    .Select(gr => new Row(gr.Key, gr.Count() + (gr.Count() == 1 ? " receipt" : " receipts"), Ui.Money(gr.Sum(s => s.Total)),
                        net > 0 ? (float)(gr.Sum(s => s.Total) / net) : 0f, Color.Empty))
                    .OrderByDescending(r => r.Fraction).ToList());
            }
            if (canStock)
            {
                List<Product> low = DataStore.Data.Products.Where(p => p.IsLowStock).OrderBy(p => p.Quantity).ThenBy(p => p.Name).ToList();
                statLow.Value = low.Count.ToString(CultureInfo.CurrentCulture);
                stock.Empty = DataStore.Data.Products.Count == 0 ? "Add your first pieces to start tracking stock." : "Every item is above its reorder level.";
                stock.SetRows(low.Take(8).Select(p => new Row(p.Name, p.Variant.Length > 0 ? p.Variant : "No variant",
                    p.Quantity + " left", -1f, p.Quantity <= 0 ? Ui.Bad : Ui.Warn)).ToList());
            }
        }

        // ---------------- small painted pieces ----------------

        /// <summary>Metric tile (.stat). The featured one is filled plum.</summary>
        class StatCard : Card
        {
            readonly Label caption, figure, note;
            public StatCard(string title, string sub, bool featured)
            {
                Padding = new Padding(Ui.S(20), Ui.S(18), Ui.S(16), Ui.S(14));
                caption = new Label(); caption.Text = title.ToUpperInvariant(); caption.Dock = DockStyle.Top; caption.Height = Ui.S(20);
                caption.Font = new Font("Segoe UI Semibold", 7.5f); caption.UseMnemonic = false; caption.AutoEllipsis = true;
                figure = new Label(); figure.Dock = DockStyle.Top; figure.Height = Ui.S(46); figure.Text = "0";
                figure.Font = new Font("Segoe UI Semibold", 21f); figure.UseMnemonic = false; figure.AutoEllipsis = true;
                note = new Label(); note.Text = sub; note.Dock = DockStyle.Top; note.Height = Ui.S(20); note.Font = new Font("Segoe UI", 8.5f);
                note.UseMnemonic = false; note.AutoEllipsis = true;
                Controls.Add(note); Controls.Add(figure); Controls.Add(caption);
                if (featured)
                {
                    BackColor = Ui.Accent; BorderColor = Ui.Accent;
                    caption.ForeColor = note.ForeColor = Color.FromArgb(230, 214, 234); figure.ForeColor = Color.White;
                }
                else { caption.ForeColor = Ui.Muted; note.ForeColor = Ui.Muted; figure.ForeColor = Ui.Ink; }
                foreach (Control c in Controls) c.Click += delegate { OnClick(EventArgs.Empty); };
            }
            public string Value { set { figure.Text = value ?? ""; } }
        }

        /// <summary>Fourteen daily bars; today's bar in plum (.sales-chart).</summary>
        class SalesChart : Control
        {
            DateTime first = DateTime.Today;
            decimal[] values = new decimal[14];
            public SalesChart()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }
            public void SetData(DateTime firstDay, decimal[] days) { first = firstDay; values = days; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int n = values.Length, labelH = Ui.S(22), top = Ui.S(10);
                float slot = (float)Width / n, barW = Math.Max(Ui.S(6), slot * 0.56f);
                decimal max = Math.Max(1m, values.Max());
                int track = Math.Max(Ui.S(20), Height - labelH - top);
                using (Font small = new Font("Segoe UI", 8f))
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(246, 243, 249)))
                using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(205, 186, 210)))
                using (SolidBrush todayBrush = new SolidBrush(Ui.Accent))
                {
                    for (int i = 0; i < n; i++)
                    {
                        float x = i * slot + (slot - barW) / 2f;
                        RectangleF trackR = new RectangleF(x, top, barW, track);
                        using (GraphicsPath tp = Ui.RoundRect(Rectangle.Round(trackR), Ui.S(6))) g.FillPath(trackBrush, tp);
                        if (values[i] > 0)
                        {
                            float h = Math.Max(Ui.S(4), (float)(values[i] / max) * track);
                            Rectangle bar = Rectangle.Round(new RectangleF(x, top + track - h, barW, h));
                            using (GraphicsPath bp = Ui.RoundRect(bar, Ui.S(6))) g.FillPath(i == n - 1 ? todayBrush : barBrush, bp);
                        }
                        TextRenderer.DrawText(g, first.AddDays(i).Day.ToString(CultureInfo.InvariantCulture), small,
                            new Rectangle((int)(i * slot), top + track + Ui.S(4), (int)slot, labelH), i == n - 1 ? Ui.Accent : Ui.Muted,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
                    }
                }
            }
        }

        public struct Row
        {
            public string Title, Sub, Right; public float Fraction; public Color Badge;
            public Row(string title, string sub, string right, float fraction, Color badge)
            { Title = title; Sub = sub; Right = right; Fraction = fraction; Badge = badge; }
        }

        /// <summary>Painted list rows: title + subtitle, right-hand figure or badge, optional proportion bar.</summary>
        class RowList : Control
        {
            List<Row> rows = new List<Row>();
            public string Empty = "";
            public RowList()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                BackColor = Color.White;
            }
            public void SetRows(List<Row> list) { rows = list; Invalidate(); }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Font bold = new Font("Segoe UI Semibold", 9.5f))
                using (Font small = new Font("Segoe UI", 8.5f))
                using (Pen line = new Pen(Ui.Line))
                {
                    if (rows.Count == 0)
                    {
                        TextRenderer.DrawText(g, Empty, small, new Rectangle(0, Ui.S(6), Width, Height), Ui.Muted, TextFormatFlags.WordBreak);
                        return;
                    }
                    int y = 0, rowH = Ui.S(rows[0].Fraction >= 0 ? 54 : 48);
                    foreach (Row r in rows)
                    {
                        if (y + rowH > Height) break;
                        int rightW = Ui.S(120);
                        TextRenderer.DrawText(g, r.Title, bold, new Rectangle(0, y + Ui.S(6), Width - rightW, Ui.S(20)), Ui.Ink, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                        TextRenderer.DrawText(g, r.Sub, small, new Rectangle(0, y + Ui.S(26), Width - rightW, Ui.S(18)), Ui.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                        if (r.Badge != Color.Empty)
                        {
                            Size sz = TextRenderer.MeasureText(r.Right, small);
                            Rectangle badge = new Rectangle(Width - sz.Width - Ui.S(18), y + Ui.S(12), sz.Width + Ui.S(16), Ui.S(24));
                            using (GraphicsPath p = Ui.RoundRect(badge, Ui.S(12))) using (SolidBrush b = new SolidBrush(r.Badge)) g.FillPath(b, p);
                            TextRenderer.DrawText(g, r.Right, small, badge, Ui.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        }
                        else
                            TextRenderer.DrawText(g, r.Right, bold, new Rectangle(Width - rightW, y + Ui.S(6), rightW, Ui.S(20)), Ui.Ink, TextFormatFlags.Right | TextFormatFlags.NoPrefix);
                        if (r.Fraction >= 0)
                        {
                            Rectangle trackR = new Rectangle(Width - rightW, y + Ui.S(32), rightW, Ui.S(6));
                            using (GraphicsPath tp = Ui.RoundRect(trackR, Ui.S(3))) using (SolidBrush tb = new SolidBrush(Color.FromArgb(240, 236, 245))) g.FillPath(tb, tp);
                            int w = (int)(rightW * Math.Min(1f, r.Fraction));
                            if (w > Ui.S(4))
                                using (GraphicsPath fp = Ui.RoundRect(new Rectangle(trackR.X, trackR.Y, w, trackR.Height), Ui.S(3))) using (SolidBrush fb = new SolidBrush(Ui.Accent)) g.FillPath(fb, fp);
                        }
                        y += rowH;
                        g.DrawLine(line, 0, y - 1, Width, y - 1);
                    }
                }
            }
        }
    }
}
