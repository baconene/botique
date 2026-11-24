using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>
    /// Analytics page: Daily operations, Monthly profitability, Annual strategy and Product &amp; sales timing,
    /// with date range, sales channel and ₱/$ filters. Same figures as the web dashboard (/admin/analytics).
    /// Blocks tagged "Sample data" are metrics the POS doesn't record yet.
    /// </summary>
    public class AnalyticsPanel : UserControl
    {
        static readonly string[] ViewNames = { "Daily operations", "Monthly profitability", "Annual strategy", "Product & sales timing" };
        static readonly int[] ViewPreset = { 0, 5, 7, 4 };
        static readonly string[] Presets = { "Today", "Yesterday", "Last 7 days", "Last 30 days", "Last 90 days", "This month", "Last month", "This year", "Last 12 months", "Custom" };

        readonly MainForm main;
        readonly RoundButton[] viewButtons = new RoundButton[4];
        readonly ComboBox cmbPreset, cmbChannel;
        readonly DateTimePicker dtFrom, dtTo;
        readonly RoundButton btnPhp, btnUsd;
        readonly Label lblContext;
        readonly Panel scroll;
        TableLayoutPanel body;
        AnalyticsReport report;
        int view;
        bool usd, heatRevenue, quiet;

        public AnalyticsPanel(MainForm owner)
        {
            main = owner;
            BackColor = Ui.Canvas;
            DoubleBuffered = true;

            // ---------- filters ----------
            Card filters = new Card(); filters.Dock = DockStyle.Top; filters.Height = Ui.S(142);
            filters.Padding = new Padding(Ui.S(16), Ui.S(12), Ui.S(16), Ui.S(8));
            Panel tabsRow = new Panel(); tabsRow.Dock = DockStyle.Top; tabsRow.Height = Ui.S(48);
            FlowLayoutPanel tabs = new FlowLayoutPanel(); tabs.Dock = DockStyle.Fill; tabs.WrapContents = false;
            FlowLayoutPanel tools = new FlowLayoutPanel(); tools.Dock = DockStyle.Right; tools.AutoSize = true; tools.WrapContents = false;
            tabsRow.Controls.Add(tabs); tabsRow.Controls.Add(tools);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                RoundButton b = (RoundButton)Ui.MakeButton(ViewNames[i], delegate { SelectView(index); });
                b.Margin = new Padding(0, 0, Ui.S(8), 0);
                viewButtons[i] = b; tabs.Controls.Add(b);
            }
            FlowLayoutPanel row = new FlowLayoutPanel(); row.Dock = DockStyle.Top; row.Height = Ui.S(44); row.WrapContents = false;
            cmbPreset = new ComboBox(); cmbPreset.DropDownStyle = ComboBoxStyle.DropDownList; cmbPreset.Width = Ui.S(150); cmbPreset.Items.AddRange(Presets);
            dtFrom = new DateTimePicker(); dtFrom.Format = DateTimePickerFormat.Custom; dtFrom.CustomFormat = "MMM d, yyyy"; dtFrom.Width = Ui.S(130);
            dtTo = new DateTimePicker(); dtTo.Format = DateTimePickerFormat.Custom; dtTo.CustomFormat = "MMM d, yyyy"; dtTo.Width = Ui.S(130);
            dtFrom.MaxDate = dtTo.MaxDate = DateTime.Today;
            cmbChannel = new ComboBox(); cmbChannel.DropDownStyle = ComboBoxStyle.DropDownList; cmbChannel.Width = Ui.S(160); cmbChannel.Items.AddRange(AnalyticsReport.Channels); cmbChannel.SelectedIndex = 0;
            btnPhp = (RoundButton)Ui.MakeButton("₱ PHP", delegate { SetCurrency(false); });
            btnUsd = (RoundButton)Ui.MakeButton("$ USD", delegate { SetCurrency(true); });
            Button refresh = Ui.MakeButton("Refresh", delegate { RefreshData(); });
            foreach (Control c in new Control[] { Caption("Date range"), cmbPreset, Caption("From"), dtFrom, Caption("To"), dtTo, Caption("Channel"), cmbChannel })
            {
                if (!(c is Label)) c.Margin = new Padding(0, Ui.S(8), Ui.S(8), 0);
                row.Controls.Add(c);
            }
            foreach (Control c in new Control[] { Caption("Currency"), btnPhp, btnUsd, refresh })
            {
                if (!(c is Label)) c.Margin = new Padding(0, 0, Ui.S(6), 0);
                tools.Controls.Add(c);
            }
            lblContext = new Label(); lblContext.Dock = DockStyle.Top; lblContext.Height = Ui.S(22); lblContext.ForeColor = Ui.Muted;
            lblContext.Font = new Font("Segoe UI", 8.5f); lblContext.UseMnemonic = false;
            filters.Controls.Add(lblContext); filters.Controls.Add(row); filters.Controls.Add(tabsRow);

            cmbPreset.SelectedIndexChanged += delegate { if (!quiet && cmbPreset.SelectedIndex < 9) { ApplyPreset(cmbPreset.SelectedIndex); RefreshData(); } };
            EventHandler dateChanged = delegate
            {
                if (quiet) return;
                quiet = true;
                if (dtTo.Value.Date < dtFrom.Value.Date) dtTo.Value = dtFrom.Value.Date;
                cmbPreset.SelectedIndex = 9;
                quiet = false;
                RefreshData();
            };
            dtFrom.CloseUp += dateChanged; dtTo.CloseUp += dateChanged;
            cmbChannel.SelectedIndexChanged += delegate { if (!quiet) RefreshData(); };

            scroll = new Panel(); scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.BackColor = Ui.Canvas;
            scroll.Padding = new Padding(0, Ui.S(8), Ui.S(4), Ui.S(8));
            Controls.Add(scroll);
            Controls.Add(filters);

            quiet = true; ApplyPreset(0); quiet = false;
            UpdateButtons();
        }

        static Label Caption(string text)
        {
            Label l = new Label(); l.Text = text.ToUpperInvariant(); l.AutoSize = true; l.ForeColor = Ui.Eyebrow; l.UseMnemonic = false;
            l.Font = new Font("Segoe UI Semibold", 7.5f); l.Margin = new Padding(Ui.S(4), Ui.S(13), Ui.S(4), 0);
            return l;
        }

        void ApplyPreset(int p)
        {
            DateTime t = DateTime.Today, from = t, to = t;
            switch (p)
            {
                case 1: from = to = t.AddDays(-1); break;
                case 2: from = t.AddDays(-6); break;
                case 3: from = t.AddDays(-29); break;
                case 4: from = t.AddDays(-89); break;
                case 5: from = new DateTime(t.Year, t.Month, 1); break;
                case 6: from = new DateTime(t.Year, t.Month, 1).AddMonths(-1); to = new DateTime(t.Year, t.Month, 1).AddDays(-1); break;
                case 7: from = new DateTime(t.Year, 1, 1); break;
                case 8: from = t.AddYears(-1).AddDays(1); break;
            }
            bool was = quiet; quiet = true;
            dtFrom.Value = from; dtTo.Value = to; cmbPreset.SelectedIndex = p;
            quiet = was;
        }

        void SelectView(int i)
        {
            view = i;
            ApplyPreset(ViewPreset[i]);
            UpdateButtons();
            RefreshData();
        }

        void SetCurrency(bool toUsd) { usd = toUsd; UpdateButtons(); Render(); }

        /// <summary>Opens a report view (0 Daily, 1 Monthly, 2 Annual, 3 Timing) with its default date range.</summary>
        public void ShowView(int index) { SelectView(Math.Max(0, Math.Min(3, index))); }

        /// <summary>Scrolls the report body (used by the self-test to capture long views).</summary>
        public void ScrollTo(int y) { scroll.AutoScrollPosition = new Point(0, y); }

        public int ContentHeight { get { return body == null ? 0 : body.Height; } }

        void UpdateButtons()
        {
            for (int i = 0; i < 4; i++) Highlight(viewButtons[i], i == view);
            Highlight(btnPhp, !usd); Highlight(btnUsd, usd);
        }

        static void Highlight(RoundButton b, bool on)
        {
            b.BackColor = on ? Ui.Accent : Color.White; b.ForeColor = on ? Color.White : Ui.AccentDark;
            b.BorderColor = on ? Color.Empty : Color.FromArgb(222, 214, 229); b.Invalidate();
        }

        // ---------------- formatting ----------------

        string Money(decimal php)
        {
            decimal v = usd ? php / AnalyticsReport.UsdRate : php;
            return (v < 0 ? "-" : "") + (usd ? "$" : "₱") + Math.Abs(v).ToString("N2", CultureInfo.InvariantCulture);
        }

        string Short(double php)
        {
            double v = usd ? php / (double)AnalyticsReport.UsdRate : php, a = Math.Abs(v);
            string s = a >= 1e6 ? (a / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M" : a >= 1e3 ? (a / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "K" : a.ToString("0", CultureInfo.InvariantCulture);
            return (v < 0 ? "-" : "") + (usd ? "$" : "₱") + s;
        }

        static string Pct(double v) { return v.ToString("0.0", CultureInfo.InvariantCulture) + "%"; }
        static string N(int v) { return v.ToString("N0", CultureInfo.InvariantCulture); }
        static double? Change(decimal cur, decimal prev) { return prev != 0 ? (double?)((double)((cur - prev) / Math.Abs(prev)) * 100) : null; }

        // ---------------- data ----------------

        /// <summary>Reloads the report for the current filters (called when the page opens and on every filter change).</summary>
        public void RefreshData()
        {
            Cursor = Cursors.WaitCursor;
            try { report = AnalyticsReport.Build(dtFrom.Value.Date, dtTo.Value.Date, Math.Max(0, cmbChannel.SelectedIndex)); }
            catch (Exception ex) { report = null; main.SetStatus("Couldn't load analytics. " + ex.Message, true); }
            finally { Cursor = Cursors.Default; }
            Render();
        }

        void Render()
        {
            scroll.SuspendLayout();
            if (body != null) { scroll.Controls.Remove(body); body.Dispose(); }
            body = new TableLayoutPanel();
            body.Dock = DockStyle.Top; body.AutoSize = true; body.AutoSizeMode = AutoSizeMode.GrowAndShrink; body.ColumnCount = 2; body.BackColor = Ui.Canvas;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            if (report == null)
            {
                lblContext.Text = "No data loaded.";
                Add(Message("Analytics couldn't be loaded. Check the database connection and press Refresh."), null);
            }
            else
            {
                AnalyticsReport r = report;
                lblContext.Text = "Comparing " + Range(r.From, r.To) + " with " + Range(r.PrevFrom, r.PrevTo) + (usd ? "   ·   $1 = ₱" + AnalyticsReport.UsdRate.ToString("0.00") : "")
                    + "   ·   Blocks tagged SAMPLE DATA show metrics the POS doesn't record yet.";
                switch (view)
                {
                    case 0: Daily(r); break;
                    case 1: Monthly(r); break;
                    case 2: Annual(r); break;
                    default: Timing(r); break;
                }
            }
            scroll.Controls.Add(body);
            scroll.ResumeLayout();
            scroll.AutoScrollPosition = Point.Empty;
        }

        static string Range(DateTime a, DateTime b)
        {
            return a == b ? a.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) : a.ToString("MMM d", CultureInfo.InvariantCulture) + " – " + b.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        // ---------------- layout helpers ----------------

        void Add(Control left, Control right)
        {
            int rowIndex = body.RowCount++;
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.Margin = new Padding(0, 0, right == null ? 0 : Ui.S(2), Ui.S(4)); left.Dock = DockStyle.Fill;
            body.Controls.Add(left, 0, rowIndex);
            if (right == null) body.SetColumnSpan(left, 2);
            else { right.Margin = new Padding(Ui.S(2), 0, 0, Ui.S(4)); right.Dock = DockStyle.Fill; body.Controls.Add(right, 1, rowIndex); }
        }

        static Control Message(string text)
        {
            Card c = new Card(); c.Height = Ui.S(90);
            Label l = new Label(); l.Text = text; l.Dock = DockStyle.Fill; l.ForeColor = Ui.Muted; l.TextAlign = ContentAlignment.MiddleLeft;
            c.Controls.Add(l);
            return c;
        }

        /// <summary>Panel card: title, muted line, optional tag (Sample data / Estimate) and tools, then the content.</summary>
        static Card Block(string title, string sub, string tag, Control content, int height, params Control[] tools)
        {
            Card card = new Card(); card.Height = Ui.S(height); card.Padding = new Padding(Ui.S(18), Ui.S(14), Ui.S(18), Ui.S(12));
            Panel head = new Panel(); head.Dock = DockStyle.Top; head.Height = Ui.S(sub.Length > 0 ? 50 : 32);
            Label t = new Label(); t.Text = title; t.Font = new Font("Segoe UI Semibold", 12f); t.ForeColor = Ui.Ink; t.AutoSize = true; t.Location = new Point(0, 0); t.UseMnemonic = false;
            Label s = new Label(); s.Text = sub; s.Font = new Font("Segoe UI", 8.5f); s.ForeColor = Ui.Muted; s.AutoEllipsis = true; s.UseMnemonic = false;
            s.Location = new Point(0, Ui.S(26)); s.Size = new Size(Ui.S(700), Ui.S(20)); s.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            FlowLayoutPanel right = new FlowLayoutPanel(); right.Dock = DockStyle.Right; right.AutoSize = true; right.WrapContents = false; right.FlowDirection = FlowDirection.LeftToRight;
            if (!string.IsNullOrEmpty(tag)) right.Controls.Add(TagLabel(tag));
            foreach (Control c in tools) { c.Margin = new Padding(Ui.S(6), 0, 0, 0); right.Controls.Add(c); }
            head.Controls.Add(s); head.Controls.Add(t); head.Controls.Add(right);
            head.Resize += delegate { s.Width = Math.Max(Ui.S(100), head.Width - right.Width - Ui.S(10)); };
            content.Dock = DockStyle.Fill;
            card.Controls.Add(content); card.Controls.Add(head);
            return card;
        }

        static Label TagLabel(string text)
        {
            bool estimate = text.StartsWith("Estimate", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Mixed", StringComparison.OrdinalIgnoreCase);
            Label l = new Label(); l.Text = text.ToUpperInvariant(); l.AutoSize = true; l.UseMnemonic = false;
            l.Font = new Font("Segoe UI Semibold", 7f); l.Padding = new Padding(Ui.S(6), Ui.S(3), Ui.S(6), Ui.S(3));
            l.BackColor = estimate ? Color.FromArgb(255, 246, 229) : Color.FromArgb(244, 239, 250);
            l.ForeColor = estimate ? Color.FromArgb(148, 102, 26) : Color.FromArgb(123, 92, 148);
            return l;
        }

        /// <summary>Four KPI tiles; the first is the featured (plum) one.</summary>
        void Kpis(params object[][] tiles)
        {
            TableLayoutPanel row = new TableLayoutPanel(); row.ColumnCount = 4; row.RowCount = 1; row.Height = Ui.S(140); row.BackColor = Ui.Canvas;
            for (int i = 0; i < 4; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < tiles.Length; i++)
            {
                object[] t = tiles[i];   // label, value, sub, change (double?), sample (bool), invert (bool)
                bool featured = i == 0, sample = t.Length > 4 && (bool)t[4], invert = t.Length > 5 && (bool)t[5];
                double? change = t.Length > 3 ? (double?)t[3] : null;
                Card c = new Card(); c.Dock = DockStyle.Fill; c.Margin = new Padding(0, 0, i == 3 ? 0 : Ui.S(4), 0);
                c.Padding = new Padding(Ui.S(18), Ui.S(12), Ui.S(12), Ui.S(8));
                if (featured) { c.BackColor = Ui.Accent; c.BorderColor = Ui.Accent; }
                Color ink = featured ? Color.White : Ui.Ink, muted = featured ? Color.FromArgb(230, 214, 234) : Ui.Muted;
                Label cap = new Label(); cap.Text = ((string)t[0]).ToUpperInvariant() + (sample ? "   ·   SAMPLE" : ""); cap.Dock = DockStyle.Top; cap.Height = Ui.S(20);
                cap.Font = new Font("Segoe UI Semibold", 7.5f); cap.ForeColor = muted; cap.UseMnemonic = false; cap.AutoEllipsis = true;
                Label val = new Label(); val.Text = (string)t[1]; val.Dock = DockStyle.Top; val.Height = Ui.S(42); val.ForeColor = ink;
                val.Font = new Font("Segoe UI Semibold", 19f); val.AutoEllipsis = true; val.UseMnemonic = false;
                Label sub = new Label(); sub.Dock = DockStyle.Fill; sub.ForeColor = muted; sub.Font = new Font("Segoe UI", 8.5f); sub.UseMnemonic = false; sub.AutoEllipsis = true;
                string changeText = "";
                if (change.HasValue)
                {
                    double ch = Math.Round(change.Value, 1);
                    bool good = invert ? ch <= 0 : ch >= 0;
                    changeText = (ch >= 0 ? "▲ " : "▼ ") + Math.Abs(ch).ToString("0.0", CultureInfo.InvariantCulture) + "%   ";
                    if (!featured) sub.ForeColor = Ui.Muted;
                    Label chip = new Label(); chip.Text = changeText.Trim(); chip.AutoSize = true; chip.Dock = DockStyle.Top; chip.UseMnemonic = false;
                    chip.Font = new Font("Segoe UI Semibold", 8f);
                    chip.ForeColor = featured ? Color.White : (good ? Color.FromArgb(35, 118, 75) : Color.FromArgb(168, 54, 47));
                    c.Controls.Add(sub); c.Controls.Add(chip);
                }
                else c.Controls.Add(sub);
                sub.Text = (string)t[2];
                c.Controls.Add(val); c.Controls.Add(cap);
                row.Controls.Add(c, i, 0);
            }
            Add(row, null);
        }

        // ---------------- tables ----------------

        /// <summary>Sortable grid (click a header) bound to a DataTable, with CSV export. Money columns follow the currency toggle.</summary>
        DataGridView Grid(DataTable table, ICollection<string> moneyCols, ICollection<string> pctCols)
        {
            DataGridView g = new DataGridView();
            Ui.StyleGrid(g);
            g.AutoGenerateColumns = true; g.ReadOnly = true; g.AllowUserToResizeColumns = true;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.DataSource = table.DefaultView;
            g.DataBindingComplete += delegate
            {
                foreach (DataGridViewColumn c in g.Columns)
                {
                    c.SortMode = DataGridViewColumnSortMode.Automatic;
                    Type t = table.Columns[c.DataPropertyName].DataType;
                    if (t != typeof(string)) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    if (c.DataPropertyName == "Item" || c.DataPropertyName == "Product") { c.FillWeight = 220; }
                }
            };
            g.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.Value == null || e.Value is DBNull) return;
                string col = g.Columns[e.ColumnIndex].DataPropertyName;
                if (moneyCols != null && moneyCols.Contains(col)) { e.Value = Money(Convert.ToDecimal(e.Value)); e.FormattingApplied = true; }
                else if (pctCols != null && pctCols.Contains(col)) { e.Value = Pct(Convert.ToDouble(e.Value)); e.FormattingApplied = true; }
            };
            return g;
        }

        Button CsvButton(string name, DataTable table, ICollection<string> moneyCols)
        {
            return Ui.MakeButton("Export CSV", delegate
            {
                using (SaveFileDialog d = new SaveFileDialog())
                {
                    d.Filter = "CSV files (*.csv)|*.csv"; d.FileName = name + "-" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv";
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Ui.CsvEscape(c.ColumnName))));
                    foreach (DataRowView rv in table.DefaultView)
                        sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c =>
                        {
                            object v = rv[c.ColumnName];
                            if (moneyCols != null && moneyCols.Contains(c.ColumnName) && !(v is DBNull))
                                return (usd ? Convert.ToDecimal(v) / AnalyticsReport.UsdRate : Convert.ToDecimal(v)).ToString("0.00", CultureInfo.InvariantCulture);
                            return Ui.CsvEscape(Convert.ToString(v, CultureInfo.InvariantCulture));
                        })));
                    File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
                    main.SetStatus("Exported " + table.DefaultView.Count + " rows to " + Path.GetFileName(d.FileName) + ".");
                }
            });
        }

        static int TableHeight(int rows, int max) { return 120 + 42 * Math.Max(2, Math.Min(rows, max)); }

        // ---------------- views ----------------

        void Daily(AnalyticsReport r)
        {
            AnalyticsReport.Kpis k = r.Cur, p = r.Prev;
            Kpis(new object[] { "Gross sales", Money(k.Gross), "Net " + Money(k.Net) + " after " + Money(k.Cancellations) + " refunds & " + Money(k.Discounts) + " discounts", Change(k.Gross, p.Gross) },
                 new object[] { "Net sales", Money(k.Net), "vs " + Money(p.Net) + " previous period", Change(k.Net, p.Net) },
                 new object[] { "Ad spend", Money(r.AdSpend), r.BlendedRoas + "× blended ROAS (" + r.PrevRoas + "× before)", Change(r.AdSpend, r.PrevAdSpend), true, true },
                 new object[] { "Total orders", N(k.Orders), "AOV " + Money(k.Aov) + " · " + N(k.Units) + " pieces", Change(k.Orders, p.Orders) });

            ComboChart ads = new ComboChart();
            ads.Categories = r.Platforms.Select(x => x.Name).ToArray();
            ads.Bars.Add(new ComboChart.Series("Spend", Color.FromArgb(205, 185, 211), r.Platforms.Select(x => (double)x.Spend).ToArray()));
            ads.Bars.Add(new ComboChart.Series("Attributed revenue", Ui.Accent, r.Platforms.Select(x => (double)x.Revenue).ToArray()));
            ads.Lines.Add(new ComboChart.Series("ROAS", Color.FromArgb(88, 174, 178), r.Platforms.Select(x => (double)x.Roas).ToArray()));
            ads.LeftFormat = Short; ads.RightFormat = v => v.ToString("0.#") + "×";
            ComboChart dev = new ComboChart();
            dev.Categories = r.Devices.Select(x => x.Device).ToArray();
            dev.Bars.Add(new ComboChart.Series("Sessions", Color.FromArgb(205, 185, 211), r.Devices.Select(x => (double)x.Sessions).ToArray()));
            dev.Lines.Add(new ComboChart.Series("Conversion rate", Ui.Accent, r.Devices.Select(x => x.Cvr).ToArray()));
            ComboChart.Series prevCvr = new ComboChart.Series("Previous CVR", Ui.Muted, r.Devices.Select(x => x.PrevCvr).ToArray()); prevCvr.Dashed = true;
            dev.Lines.Add(prevCvr);
            dev.RightFormat = v => v.ToString("0.0") + "%";
            List<AnalyticsReport.DeviceRow> devices = r.Devices;
            dev.BarColor = (s, c) => devices[c].Drop ? Color.FromArgb(194, 69, 61) : Color.FromArgb(205, 185, 211);
            string devSub = string.Join("  ·  ", r.Devices.Select(x => x.Device + " " + x.Share.ToString("0") + "% of traffic, CVR " + x.Cvr.ToString("0.00") + "%" + (x.Drop ? " (sudden drop)" : "")));
            Add(Block("Ad spend & ROAS by platform", "Spend vs attributed revenue; blended ROAS uses your real net sales.", "Sample data", ads, 330),
                Block("Traffic & conversion by device", devSub, "Sample data", dev, 330));

            AddHeatmap(r);

            DataTable low = new DataTable();
            low.Columns.Add("Item"); low.Columns.Add("Barcode"); low.Columns.Add("Size"); low.Columns.Add("Color");
            low.Columns.Add("On hand", typeof(int)); low.Columns.Add("Sold (30d)", typeof(int)); low.Columns.Add("Days left", typeof(double)); low.Columns.Add("Status");
            foreach (AnalyticsReport.LowStockRow x in r.LowStock)
                low.Rows.Add(x.Name, x.Barcode, x.Size, x.Color, x.OnHand, x.Sold30, x.Cover < 0 ? (object)DBNull.Value : x.Cover, x.Status == "Low" ? "Low" : "Restock" + (x.OnHand <= 0 ? " · out of stock" : ""));
            DataGridView lowGrid = Grid(low, null, null);
            lowGrid.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (lowGrid.Columns[e.ColumnIndex].DataPropertyName != "Status" || e.Value == null) return;
                string v = e.Value.ToString();
                e.CellStyle.ForeColor = v.Contains("out") ? Color.FromArgb(168, 54, 47) : v.StartsWith("Restock") ? Color.FromArgb(138, 90, 13) : Ui.Muted;
                e.CellStyle.Font = new Font("Segoe UI Semibold", 9f);
            };
            Add(Block("Low-stock & out-of-stock alerts", "At or below the reorder level, fastest movers first. Days left uses the last 30 days of sales.", null, lowGrid,
                TableHeight(r.LowStock.Count, 8), CsvButton("low-stock", low, null)), null);

            DataTable un = new DataTable();
            un.Columns.Add("Order"); un.Columns.Add("Customer"); un.Columns.Add("Waiting (h)", typeof(int)); un.Columns.Add("Age"); un.Columns.Add("Courier");
            un.Columns.Add("Region"); un.Columns.Add("Payment"); un.Columns.Add("Amount", typeof(decimal));
            foreach (AnalyticsReport.UnfulfilledRow x in r.Unfulfilled) un.Rows.Add(x.Order, x.Customer, x.AgeHours, x.Bucket, x.Courier, x.Region, x.Payment, x.Amount);
            HashSet<string> unMoney = new HashSet<string> { "Amount" };
            DataGridView unGrid = Grid(un, unMoney, null);
            unGrid.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (unGrid.Columns[e.ColumnIndex].DataPropertyName != "Age" || e.Value == null) return;
                string v = e.Value.ToString();
                e.CellStyle.BackColor = v == "48h+" ? Ui.Bad : v == "24-48h" ? Ui.Warn : Color.FromArgb(227, 244, 236);
            };
            int over24 = r.Unfulfilled.Count(x => x.AgeHours >= 24), over48 = r.Unfulfilled.Count(x => x.AgeHours >= 48);
            Add(Block("Unfulfilled orders aging", over24 + " orders past 24h, " + over48 + " past 48h. Ship these first to prevent cancellations.", "Sample data", unGrid,
                TableHeight(r.Unfulfilled.Count, 10), CsvButton("unfulfilled-orders", un, unMoney)), null);
        }

        void AddHeatmap(AnalyticsReport r)
        {
            HeatmapChart heat = new HeatmapChart();
            double[,] values = new double[7, 24];
            for (int d = 0; d < 7; d++) for (int h = 0; h < 24; h++) values[d, h] = heatRevenue ? (double)r.HeatRevenue[d, h] : r.HeatOrders[d, h];
            heat.Values = values;
            heat.Format = heatRevenue ? (Func<double, string>)(v => Money((decimal)v)) : (v => N((int)v) + (v == 1 ? " order" : " orders"));
            int pd = -1, ph = -1; double peak = 0;
            for (int d = 0; d < 7; d++) for (int h = 0; h < 24; h++) if (values[d, h] > peak) { peak = values[d, h]; pd = d; ph = h; }
            RoundButton orders = (RoundButton)Ui.MakeButton("Orders", delegate { heatRevenue = false; Render(); });
            RoundButton revenue = (RoundButton)Ui.MakeButton("Net sales", delegate { heatRevenue = true; Render(); });
            Highlight(orders, !heatRevenue); Highlight(revenue, heatRevenue);
            string sub = pd >= 0 ? "Peak: " + AnalyticsReport.DayNames[pd] + " " + HeatmapChart.Hour(ph) + " (" + heat.Format(peak) + "). Use the peaks to time ads and flash drops."
                : "No completed sales in this period.";
            Add(Block("Sales heatmap", sub, null, heat, 300, orders, revenue), null);
        }

        void Monthly(AnalyticsReport r)
        {
            Kpis(new object[] { "Net sales", Money(r.Cur.Net), N(r.Cur.Orders) + " orders", Change(r.Cur.Net, r.Prev.Net) },
                 new object[] { "Gross margin", Pct((double)r.GrossMargin), "After cost of goods (real costs)" },
                 new object[] { "Net profit", Money(r.NetProfit), "After fees, packaging, shipping & ads" },
                 new object[] { "Profit per order", Money(r.ProfitPerOrder), "AOV " + Money(r.Cur.Aov) });

            WaterfallChart wf = new WaterfallChart(); wf.Steps = r.Waterfall; wf.Format = Short;
            Panel unit = new Panel(); unit.BackColor = Color.White;
            GaugeChart gauge = new GaugeChart(); gauge.Value = (double)r.AovToCac; gauge.Label = "AOV ÷ CAC"; gauge.Dock = DockStyle.Fill;
            Label stats = new Label(); stats.Dock = DockStyle.Bottom; stats.Height = Ui.S(64); stats.ForeColor = Ui.Ink; stats.UseMnemonic = false;
            stats.Text = "AOV " + Money(r.Cur.Aov) + " (real)   ·   CAC " + Money(r.Cac) + " (sample)\nNew customers " + N(r.NewCustomers) + ".  Aim for 3× or more.";
            stats.TextAlign = ContentAlignment.MiddleCenter;
            unit.Controls.Add(gauge); unit.Controls.Add(stats);
            Add(Block("Contribution margin", "Net sales to net profit. Cost of goods is real; fees, packaging and shipping are estimates; marketing is sample ad spend.", "Mixed sources", wf, 360),
                Block("Unit economics", "Average order value against the cost to acquire a customer.", "CAC is sample", unit, 360));

            // Product table with size velocity for the selected row.
            DataTable prod = new DataTable();
            prod.Columns.Add("Product"); prod.Columns.Add("Brand"); prod.Columns.Add("Category"); prod.Columns.Add("Sizes");
            prod.Columns.Add("Units", typeof(int)); prod.Columns.Add("Gross revenue", typeof(decimal)); prod.Columns.Add("Net revenue", typeof(decimal));
            prod.Columns.Add("Return rate", typeof(double)); prod.Columns.Add("Profit margin", typeof(double));
            Dictionary<string, AnalyticsReport.ProductRow> byName = new Dictionary<string, AnalyticsReport.ProductRow>();
            foreach (AnalyticsReport.ProductRow x in r.Products)
            {
                prod.Rows.Add(x.Name, x.Brand, x.Category, string.Join("  ", x.Sizes.Select(kv => kv.Key + ":" + kv.Value)), x.Units, x.Gross, x.Net, x.ReturnRate, x.Margin);
                byName[x.Name + "|" + x.Brand] = x;
            }
            HashSet<string> prodMoney = new HashSet<string> { "Gross revenue", "Net revenue" };
            HashSet<string> prodPct = new HashSet<string> { "Return rate", "Profit margin" };
            DataGridView pg = Grid(prod, prodMoney, prodPct);
            TextBox search = new TextBox(); search.Width = Ui.S(220); Ui.SetCue(search, "Search products...");
            search.TextChanged += delegate
            {
                string q = search.Text.Replace("'", "''").Replace("[", "[[]").Replace("*", "[*]").Replace("%", "[%]");
                prod.DefaultView.RowFilter = q.Length == 0 ? "" : "Product LIKE '%" + q + "%' OR Brand LIKE '%" + q + "%' OR Category LIKE '%" + q + "%' OR Sizes LIKE '%" + q + "%'";
            };
            SizeBars bars = new SizeBars(); bars.Dock = DockStyle.Bottom; bars.Height = Ui.S(120);
            pg.SelectionChanged += delegate
            {
                if (pg.CurrentRow == null || pg.CurrentRow.DataBoundItem == null) return;
                DataRowView rv = (DataRowView)pg.CurrentRow.DataBoundItem;
                AnalyticsReport.ProductRow pr;
                if (byName.TryGetValue(rv["Product"] + "|" + rv["Brand"], out pr)) { bars.Product = pr; bars.Money = Money; bars.Invalidate(); }
            };
            Panel prodHost = new Panel(); prodHost.BackColor = Color.White;
            pg.Dock = DockStyle.Fill; prodHost.Controls.Add(pg); prodHost.Controls.Add(bars);
            Add(Block("Product sales", "Per style, with size breakdown. Click a header to sort; select a row to see its size velocity below.", null, prodHost,
                TableHeight(r.Products.Count, 10) + 130, search, CsvButton("product-sales", prod, prodMoney)), null);

            // Returns (reasons are sample) and inventory velocity.
            DonutChart reasons = new DonutChart(); reasons.Dock = DockStyle.Top; reasons.Height = Ui.S(150);
            reasons.Slices = r.ReasonTotals.Select(kv => new KeyValuePair<string, double>(kv.Key, kv.Value)).ToList();
            reasons.Format = v => N((int)v);
            DataTable ret = new DataTable();
            ret.Columns.Add("Item"); ret.Columns.Add("Size"); ret.Columns.Add("Returned", typeof(int)); ret.Columns.Add("Return rate", typeof(double)); ret.Columns.Add("Top reason");
            foreach (AnalyticsReport.ReturnRow x in r.Returns)
                ret.Rows.Add(x.Name, x.Size, x.Returned, x.Rate, x.Reasons.Count > 0 ? x.Reasons.OrderByDescending(kv => kv.Value).First().Key : "");
            DataGridView rg = Grid(ret, null, new HashSet<string> { "Return rate" });
            Panel retHost = new Panel(); retHost.BackColor = Color.White; rg.Dock = DockStyle.Fill; retHost.Controls.Add(rg); retHost.Controls.Add(reasons);
            ComboChart vel = new ComboChart();
            vel.Categories = r.Velocity.Select(x => x.Category).ToArray();
            vel.Bars.Add(new ComboChart.Series("Sell-through", Ui.Accent, r.Velocity.Select(x => x.SellThrough).ToArray()));
            vel.LeftFormat = v => v.ToString(v < 10 ? "0.##" : "0", CultureInfo.InvariantCulture) + "%"; vel.Dock = DockStyle.Fill;
            Label aging = new Label(); aging.Dock = DockStyle.Bottom; aging.Height = Ui.S(82); aging.UseMnemonic = false; aging.ForeColor = Ui.Ink;
            aging.Text = "Unsold stock by age:\n" + string.Join("\n", r.Aging.Select(a => a.Label + ":  " + N(a.Units) + " units in " + N(a.Items) + " items  (" + Money(a.Value) + " at cost)"));
            Panel velHost = new Panel(); velHost.BackColor = Color.White; velHost.Controls.Add(vel); velHost.Controls.Add(aging);
            int returned = r.Returns.Sum(x => x.Returned);
            Add(Block("Returns & exchanges by SKU", N(returned) + " returned units. Counts are real; the POS doesn't record reasons yet.", "Reasons are sample", retHost, 470),
                Block("Inventory velocity", "Sell-through in the period by category, and how long unsold stock has been sitting.", null, velHost, 470));

            // COD RTO by courier and region (sample).
            TableLayoutPanel rto = new TableLayoutPanel(); rto.ColumnCount = 2; rto.BackColor = Color.White;
            rto.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); rto.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            rto.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            DataTable couriers = RtoTable("Courier", r.Couriers), regions = RtoTable("Region", r.Regions);
            DataGridView cg = RtoGrid(couriers), regionGrid = RtoGrid(regions);
            cg.Margin = new Padding(0, 0, Ui.S(8), 0); regionGrid.Margin = new Padding(Ui.S(8), 0, 0, 0);
            rto.Controls.Add(cg, 0, 0); rto.Controls.Add(regionGrid, 1, 0);
            Add(Block("COD return-to-origin (RTO)", "Cash-on-delivery parcels that came back, by courier and region. 15%+ is flagged as a high-RTO zone.", "Sample data", rto, 300,
                CsvButton("rto-couriers", couriers, null), CsvButton("rto-regions", regions, null)), null);
        }

        static DataTable RtoTable(string first, List<AnalyticsReport.RtoRow> rows)
        {
            DataTable t = new DataTable();
            t.Columns.Add(first); t.Columns.Add("COD orders", typeof(int)); t.Columns.Add("Returned", typeof(int)); t.Columns.Add("RTO rate", typeof(double)); t.Columns.Add("Flag");
            foreach (AnalyticsReport.RtoRow x in rows.OrderByDescending(x => x.Rate)) t.Rows.Add(x.Name, x.Orders, x.Rto, x.Rate, x.High ? "High RTO" : x.Rate >= 10 ? "Watch" : "OK");
            return t;
        }

        DataGridView RtoGrid(DataTable t)
        {
            DataGridView g = Grid(t, null, new HashSet<string> { "RTO rate" });
            g.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (g.Columns[e.ColumnIndex].DataPropertyName != "Flag" || e.Value == null) return;
                string v = e.Value.ToString();
                e.CellStyle.BackColor = v == "High RTO" ? Ui.Bad : v == "Watch" ? Ui.Warn : Color.FromArgb(227, 244, 236);
            };
            return g;
        }

        void Annual(AnalyticsReport r)
        {
            Kpis(new object[] { "Net sales", Money(r.Cur.Net), N(r.Cur.Orders) + " orders in the period", Change(r.Cur.Net, r.Prev.Net) },
                 new object[] { "Gross margin", Pct((double)r.GrossMargin), "Across all categories" },
                 new object[] { "Repeat purchase rate", Pct(r.RepeatRate), "Customers who bought again", null, true },
                 new object[] { "12-month LTV", Money(r.Ltv[11]), "Average revenue per customer", null, true });

            ComboChart q = new ComboChart();
            q.Categories = r.Quarters.Select(x => x.Label).ToArray();
            for (int i = 0; i < r.Categories.Count; i++)
            {
                string c = r.Categories[i];
                q.Bars.Add(new ComboChart.Series(c, ChartBase.Palette[i % ChartBase.Palette.Length], r.Quarters.Select(x => { decimal v; return x.Revenue.TryGetValue(c, out v) ? (double)v : 0; }).ToArray()));
            }
            q.Lines.Add(new ComboChart.Series("Gross margin", Ui.Ink, r.Quarters.Select(x => x.Margin).ToArray()));
            q.LeftFormat = Short; q.RightFormat = v => v.ToString("0") + "%";
            Add(Block("Category & seasonal margin", "Revenue by category (bars) and overall gross margin (line), by quarter.", null, q, 380), null);

            Panel ltvHost = new Panel(); ltvHost.BackColor = Color.White;
            ComboChart ltv = new ComboChart(); ltv.Dock = DockStyle.Fill;
            ltv.Categories = Enumerable.Range(1, 12).Select(m => "M" + m).ToArray();
            ComboChart.Series curve = new ComboChart.Series("Lifetime value", Ui.Accent, r.Ltv.Select(v => (double)v).ToArray()); curve.Area = true;
            ltv.Lines.Add(curve); ltv.LinesShareLeftAxis = true; ltv.LeftFormat = Short;
            DonutChart split = new DonutChart(); split.Dock = DockStyle.Right; split.Width = Ui.S(260);
            split.Slices = new List<KeyValuePair<string, double>> { new KeyValuePair<string, double>("First-time", r.FirstTimeShare), new KeyValuePair<string, double>("Returning", 100 - r.FirstTimeShare) };
            split.Format = v => v.ToString("0.0") + "%";
            ltvHost.Controls.Add(ltv); ltvHost.Controls.Add(split);

            DataTable coh = new DataTable();
            coh.Columns.Add("Cohort"); coh.Columns.Add("Customers", typeof(int));
            for (int m = 0; m <= 12; m++) coh.Columns.Add("M" + m, typeof(double));
            foreach (AnalyticsReport.CohortRow c in r.Cohorts)
            {
                object[] vals = new object[15]; vals[0] = c.Cohort; vals[1] = c.Customers;
                for (int m = 0; m <= 12; m++) vals[m + 2] = m < c.Cells.Count ? (object)c.Cells[m] : DBNull.Value;
                coh.Rows.Add(vals);
            }
            DataGridView cg = Grid(coh, null, null);
            cg.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                string col = cg.Columns[e.ColumnIndex].DataPropertyName;
                if (!col.StartsWith("M") || col.Length > 3 || e.Value == null || e.Value is DBNull) return;
                double v = Convert.ToDouble(e.Value);
                int alpha = v >= 100 ? 255 : (int)(30 + Math.Min(1, v / 40) * 200);
                e.CellStyle.BackColor = Color.FromArgb(255 - alpha * (255 - 105) / 255, 255 - alpha * (255 - 64) / 255, 255 - alpha * (255 - 110) / 255);
                e.CellStyle.ForeColor = v >= 25 ? Color.White : Ui.Ink;
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                e.Value = v.ToString("0") + "%"; e.FormattingApplied = true;
            };
            cg.DefaultCellStyle.Padding = new Padding(Ui.S(2), 0, Ui.S(2), 0);
            cg.ColumnHeadersDefaultCellStyle.Padding = new Padding(Ui.S(2), 0, Ui.S(2), 0);
            cg.DataBindingComplete += delegate
            {
                cg.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                foreach (DataGridViewColumn c in cg.Columns) c.Width = c.Index == 0 ? Ui.S(84) : Ui.S(c.Index == 1 ? 84 : 56);
                cg.Columns[1].HeaderText = "Buyers";
            };
            Add(Block("Lifetime value & repeat customers", "Average cumulative revenue per customer over 12 months; first-time vs returning revenue.", "Sample data", ltvHost, 360),
                Block("Cohort retention", "Share of each month's new customers who bought again, months 0–12.", "Sample data", cg, 360));

            DataTable sh = new DataTable();
            sh.Columns.Add("Reason"); sh.Columns.Add("Units", typeof(int)); sh.Columns.Add("Cost impact", typeof(decimal));
            foreach (AnalyticsReport.ShrinkRow x in r.Shrinkage) sh.Rows.Add(x.Reason, x.Units, x.Cost);
            HashSet<string> shMoney = new HashSet<string> { "Cost impact" };
            Panel shHost = new Panel(); shHost.BackColor = Color.White;
            Label sys = new Label(); sys.Dock = DockStyle.Left; sys.Width = Ui.S(300); sys.ForeColor = Ui.Ink; sys.UseMnemonic = false;
            int lost = r.Shrinkage.Sum(x => x.Units); decimal lostCost = r.Shrinkage.Sum(x => x.Cost);
            sys.Text = "System stock (real)\n" + N(r.SystemUnits) + " units  ·  " + Money(r.SystemValue) + " at cost\n\nWritten off (sample)\n" + N(lost) + " units  ·  " + Money(lostCost)
                + "\n\nShrinkage rate " + Pct(r.SystemUnits + lost > 0 ? 100.0 * lost / (r.SystemUnits + lost) : 0);
            DataGridView shg = Grid(sh, shMoney, null); shg.Dock = DockStyle.Fill;
            shHost.Controls.Add(shg); shHost.Controls.Add(sys);
            Add(Block("Inventory shrinkage", "System stock vs stock written off as damaged, lost or corrected down at a count. Write-off history is in the web dashboard; figures here are sample.",
                "Write-offs are sample", shHost, 260, CsvButton("shrinkage", sh, shMoney)), null);
        }

        void Timing(AnalyticsReport r)
        {
            int bd = -1, bh = -1;
            for (int i = 0; i < 7; i++) if (r.WeekdayOrders[i] > 0 && (bd < 0 || r.WeekdayOrders[i] > r.WeekdayOrders[bd])) bd = i;
            for (int i = 0; i < 24; i++) if (r.HourOrders[i] > 0 && (bh < 0 || r.HourOrders[i] > r.HourOrders[bh])) bh = i;
            AnalyticsReport.Daypart busiest = r.Dayparts.OrderByDescending(x => x.Orders).First();
            Kpis(new object[] { "Best day", bd >= 0 ? AnalyticsReport.DayNames[bd] : "—", "Most completed orders" },
                 new object[] { "Peak hour", bh >= 0 ? HeatmapChart.Hour(bh) + "m" : "—", "Busiest hour of the day" },
                 new object[] { "Busiest part of day", busiest.Orders > 0 ? busiest.Name : "—", busiest.Orders > 0 ? N(busiest.Orders) + " orders · " + Money(busiest.Revenue) : "No sales yet" },
                 new object[] { "Orders", N(r.Cur.Orders), Money(r.Cur.Net) + " net sales" });

            ComboChart hours = new ComboChart();
            hours.Categories = Enumerable.Range(0, 24).Select(h => HeatmapChart.Hour(h)).ToArray(); hours.LabelEvery = 3;
            hours.Bars.Add(new ComboChart.Series("Orders", Color.FromArgb(205, 185, 211), r.HourOrders.Select(v => (double)v).ToArray()));
            hours.Lines.Add(new ComboChart.Series("Net sales", Ui.Accent, r.HourRevenue.Select(v => (double)v).ToArray()));
            hours.RightFormat = Short;
            ComboChart days = new ComboChart();
            days.Categories = AnalyticsReport.DayNames;
            days.Bars.Add(new ComboChart.Series("Net sales", Ui.Accent, r.WeekdayRevenue.Select(v => (double)v).ToArray()));
            days.LeftFormat = Short;
            int best = bd;
            days.BarColor = (s, c) => c == best ? Ui.Accent : Color.FromArgb(205, 185, 211);
            Add(Block("Sales by hour", "Completed orders (bars) and net sales (line) for each hour of the day.", null, hours, 340),
                Block("Sales by weekday", "Net sales per day of the week; the best day is highlighted.", null, days, 340));

            AddHeatmap(r);

            TableLayoutPanel parts = new TableLayoutPanel(); parts.ColumnCount = 5; parts.RowCount = 1; parts.Height = Ui.S(220); parts.BackColor = Ui.Canvas;
            for (int i = 0; i < 5; i++) parts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            parts.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            for (int i = 0; i < r.Dayparts.Count; i++)
            {
                AnalyticsReport.Daypart d = r.Dayparts[i];
                Label text = new Label(); text.ForeColor = Ui.Ink; text.UseMnemonic = false; text.Font = new Font("Segoe UI", 9f);
                text.Text = N(d.Orders) + " orders  ·  " + Money(d.Revenue) + "\n\n" +
                    (d.Top.Count > 0 ? string.Join("\n", d.Top.Select((kv, n) => (n + 1) + ". " + kv.Key + "  (" + kv.Value + ")")) : "No sales in this part of the day.");
                Card c = Block(d.Name, d.FromHour.ToString("00") + ":00–" + (d.ToHour % 24).ToString("00") + ":00", null, text, 220);
                c.Dock = DockStyle.Fill; c.Margin = new Padding(0, 0, i == 4 ? 0 : Ui.S(4), 0);
                parts.Controls.Add(c, i, 0);
            }
            Add(parts, null);
        }

        /// <summary>Size-level velocity for the selected product (units per size as bars).</summary>
        class SizeBars : ChartBase
        {
            public AnalyticsReport.ProductRow Product;
            public Func<decimal, string> Money = v => v.ToString("N2");

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                if (Product == null) { TextRenderer.DrawText(g, "Select a product to see which sizes sell fastest.", Font, new Point(0, Ui.S(8)), Ui.Muted); return; }
                using (Font bold = new Font("Segoe UI Semibold", 9f))
                    TextRenderer.DrawText(g, "Size velocity · " + Product.Name + "   (" + Product.Returned + " returned, " + Money(Product.Net - Product.Cost) + " gross profit)", bold, new Point(0, Ui.S(4)), Ui.Ink);
                if (Product.Sizes.Count == 0) { TextRenderer.DrawText(g, "No units sold in this period (returns only).", Font, new Point(0, Ui.S(30)), Ui.Muted); return; }
                int max = Math.Max(1, Product.Sizes.Values.DefaultIfEmpty(0).Max()), y = Ui.S(28), n = Product.Sizes.Count;
                int rowH = Math.Max(Ui.S(12), Math.Min(Ui.S(20), (Height - y) / Math.Max(1, n)));
                foreach (KeyValuePair<string, int> kv in Product.Sizes)
                {
                    TextRenderer.DrawText(g, kv.Key, Font, new Point(0, y), Ui.Muted);
                    int w = (int)((Width - Ui.S(220)) * (double)kv.Value / max);
                    using (SolidBrush track = new SolidBrush(Color.FromArgb(239, 234, 243))) g.FillRectangle(track, Ui.S(70), y + Ui.S(4), Width - Ui.S(220), rowH - Ui.S(8));
                    using (SolidBrush b = new SolidBrush(Ui.Accent)) g.FillRectangle(b, Ui.S(70), y + Ui.S(4), Math.Max(2, w), rowH - Ui.S(8));
                    TextRenderer.DrawText(g, kv.Value + " units (" + (Product.Units > 0 ? 100 * kv.Value / Product.Units : 0) + "%)", Font, new Point(Width - Ui.S(140), y), Ui.Ink);
                    y += rowH;
                    if (y > Height - rowH) break;
                }
            }
        }
    }
}
