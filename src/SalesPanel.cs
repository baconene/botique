using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BoutiquePOS
{
    public class SalesPanel : UserControl
    {
        readonly MainForm main;
        DateTimePicker dtFrom, dtTo;
        DataGridView gridSales, gridLines;
        Label lblSummary;
        List<Sale> view = new List<Sale>();

        public SalesPanel(MainForm main)
        {
            this.main = main;
            BackColor = Color.White;
            Build();
            SetRange(DateTime.Today, DateTime.Today);
        }

        void Build()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill; root.ColumnCount = 1; root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            FlowLayoutPanel range = new FlowLayoutPanel();
            range.AutoSize = true; range.Dock = DockStyle.Fill;
            dtFrom = new DateTimePicker(); dtFrom.Format = DateTimePickerFormat.Short; dtFrom.Width = Ui.S(130);
            dtTo = new DateTimePicker(); dtTo.Format = DateTimePickerFormat.Short; dtTo.Width = Ui.S(130);
            dtFrom.ValueChanged += delegate { RefreshList(); };
            dtTo.ValueChanged += delegate { RefreshList(); };
            range.Controls.Add(Ui.MakeLabel("From"));
            range.Controls.Add(dtFrom);
            range.Controls.Add(Ui.MakeLabel("to"));
            range.Controls.Add(dtTo);
            range.Controls.Add(Ui.MakeButton("Today", delegate { SetRange(DateTime.Today, DateTime.Today); }));
            range.Controls.Add(Ui.MakeButton("Last 7 days", delegate { SetRange(DateTime.Today.AddDays(-6), DateTime.Today); }));
            range.Controls.Add(Ui.MakeButton("This month", delegate { SetRange(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today); }));
            range.Controls.Add(Ui.MakeButton("All time", delegate { SetAll(); }));
            root.Controls.Add(range, 0, 0);

            lblSummary = new Label();
            lblSummary.AutoSize = true;
            lblSummary.Font = new Font("Segoe UI Semibold", 10.5f);
            lblSummary.ForeColor = Ui.AccentDark;
            lblSummary.Margin = new Padding(Ui.S(3), Ui.S(6), Ui.S(3), Ui.S(8));
            root.Controls.Add(lblSummary, 0, 1);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            root.Controls.Add(split, 0, 2);
            split.HandleCreated += delegate { split.SplitterDistance = Math.Max(Ui.S(120), split.Height * 58 / 100); };

            gridSales = new DataGridView();
            Ui.StyleGrid(gridSales);
            gridSales.ReadOnly = true;
            gridSales.MultiSelect = false;
            gridSales.Columns.Add(Ui.Col("Receipt", "Code", 100, true));
            DataGridViewTextBoxColumn date = Ui.Col("Date / time", "Date", 160, true);
            date.DefaultCellStyle.Format = "g";
            gridSales.Columns.Add(date);
            gridSales.Columns.Add(Ui.NumCol("Items", "ItemCount", 60, true));
            gridSales.Columns.Add(Ui.MoneyCol("Subtotal", "Subtotal", 100, true));
            gridSales.Columns.Add(Ui.MoneyCol("Discount", "DiscountAmount", 90, true));
            gridSales.Columns.Add(Ui.MoneyCol("Tax", "Tax", 80, true));
            gridSales.Columns.Add(Ui.MoneyCol("Total", "Total", 100, true));
            gridSales.Columns.Add(Ui.Col("Payment", "PaymentMethod", 90, true));
            gridSales.Columns.Add(Ui.Col("Cashier", "Cashier", 110, true));
            gridSales.Columns.Add(Ui.Col("Status", "Status", 0, true));
            gridSales.SelectionChanged += delegate { ShowLines(); };
            gridSales.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex < view.Count && view[e.RowIndex].Refunded) e.CellStyle.ForeColor = Color.FromArgb(170, 40, 30);
            };
            split.Panel1.Controls.Add(gridSales);

            gridLines = new DataGridView();
            Ui.StyleGrid(gridLines);
            gridLines.ReadOnly = true;
            gridLines.Columns.Add(Ui.Col("Barcode", "Barcode", 130, true));
            gridLines.Columns.Add(Ui.Col("Item", "Name", 0, true));
            gridLines.Columns.Add(Ui.Col("Size / Color", "Variant", 150, true));
            gridLines.Columns.Add(Ui.MoneyCol("Price", "UnitPrice", 90, true));
            gridLines.Columns.Add(Ui.NumCol("Qty", "Quantity", 60, true));
            gridLines.Columns.Add(Ui.MoneyCol("Total", "LineTotal", 100, true));
            split.Panel2.Controls.Add(gridLines);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true; actions.Dock = DockStyle.Fill;
            actions.Controls.Add(Ui.MakeButton("Reprint receipt", delegate { Reprint(); }));
            actions.Controls.Add(Ui.MakeButton("Preview receipt", delegate { PreviewReceipt(); }));
            actions.Controls.Add(btnRefund = Ui.MakeButton("Refund / return entire sale...", delegate { Refund(); }));
            actions.Controls.Add(Ui.MakeButton("Export sales CSV...", delegate { ExportCsv(); }));
            Label hint = new Label(); hint.AutoSize = true; hint.ForeColor = Ui.Muted; hint.Margin = new Padding(Ui.S(12), Ui.S(10), 0, 0);
            hint.Text = "Tip: scan the barcode at the bottom of a receipt to find that sale.";
            actions.Controls.Add(hint);
            root.Controls.Add(actions, 0, 3);
        }

        bool suppress;

        void SetRange(DateTime from, DateTime to)
        {
            suppress = true;
            dtFrom.Value = from; dtTo.Value = to;
            suppress = false;
            RefreshList();
        }

        void SetAll()
        {
            DateTime? first = null;
            try { first = DataStore.FirstSaleDate(); } catch (Exception ex) { main.SetStatus("Database error: " + ex.Message, true); }
            SetRange(first.HasValue ? first.Value.Date : DateTime.Today, DateTime.Today);
        }

        Button btnRefund;

        public void ApplyPermissions() { btnRefund.Enabled = Session.Can(Permissions.Refund); }

        public void RefreshList()
        {
            if (suppress || gridSales == null || DataStore.Store == null) return;
            Sale keep = Current();
            DateTime from = dtFrom.Value.Date, to = dtTo.Value.Date.AddDays(1);
            try { view = DataStore.SalesBetween(from, to); }
            catch (Exception ex) { view = new List<Sale>(); main.SetStatus("Could not load sales: " + ex.Message, true); }
            gridSales.DataSource = null;
            gridSales.DataSource = view;
            if (keep != null) Select(keep);
            ShowLines();

            List<Sale> done = view.Where(s => !s.Refunded).ToList();
            decimal refunds = view.Where(s => s.Refunded).Sum(s => s.Total);
            lblSummary.Text = string.Format(
                "{0} sales   |   {1} items   |   Net sales {2}   |   Discounts {3}   |   Tax collected {4}   |   Cash {5}  Card {6}  Other {7}   |   Refunded {8}",
                done.Count, done.Sum(s => s.ItemCount), Ui.Money(done.Sum(s => s.Total)), Ui.Money(done.Sum(s => s.DiscountAmount)),
                Ui.Money(done.Sum(s => s.Tax)),
                Ui.Money(done.Where(s => s.PaymentMethod == "Cash").Sum(s => s.Total)),
                Ui.Money(done.Where(s => s.PaymentMethod == "Card").Sum(s => s.Total)),
                Ui.Money(done.Where(s => s.PaymentMethod == "Other").Sum(s => s.Total)),
                Ui.Money(refunds));
        }

        Sale Current()
        {
            if (gridSales == null || gridSales.CurrentRow == null) return null;
            int i = gridSales.CurrentRow.Index;
            return i >= 0 && i < view.Count ? view[i] : null;
        }

        void Select(Sale s)
        {
            int i = view.FindIndex(x => x.Number == s.Number); // database reloads create new objects
            if (i < 0) return;
            gridSales.ClearSelection();
            gridSales.CurrentCell = gridSales.Rows[i].Cells[0];
            gridSales.Rows[i].Selected = true;
        }

        void ShowLines()
        {
            Sale s = Current();
            gridLines.DataSource = s == null ? null : s.Lines.ToList();
        }

        public void SelectSale(Sale s)
        {
            if (s.Date.Date < dtFrom.Value.Date || s.Date.Date > dtTo.Value.Date) SetAll();
            else RefreshList();
            Select(s);
        }

        public void HandleScan(string code)
        {
            Sale s = DataStore.FindSaleByCode(code);
            if (s != null) { SelectSale(s); main.SetStatus("Found receipt " + s.Code + "."); }
            else main.SetStatus("No sale found for \"" + code + "\".", true);
        }

        void Reprint()
        {
            Sale s = Current();
            if (s == null) return;
            try { ReceiptPrinter.Print(s, DataStore.Data.Settings); main.SetStatus("Receipt " + s.Code + " sent to printer."); }
            catch (Exception ex) { Ui.Error(this, "Could not print: " + ex.Message); }
        }

        void PreviewReceipt()
        {
            Sale s = Current();
            if (s == null) return;
            try
            {
                using (System.Drawing.Printing.PrintDocument doc = ReceiptPrinter.CreateDocument(s, DataStore.Data.Settings))
                using (PrintPreviewDialog dlg = new PrintPreviewDialog())
                {
                    dlg.Document = doc;
                    dlg.Width = Ui.S(520); dlg.Height = Ui.S(800);
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex) { Ui.Error(this, "Could not preview: " + ex.Message); }
        }

        void Refund()
        {
            Sale s = Current();
            if (s == null) return;
            if (s.Refunded) { Ui.Error(this, "Sale " + s.Code + " was already refunded."); return; }
            if (!Ui.Confirm(this, string.Format("Refund sale {0} for {1} ({2})?\n\nAll {3} item(s) will be returned to stock.",
                s.Code, Ui.Money(s.Total), s.PaymentMethod, s.ItemCount))) return;
            try { DataStore.RefundSale(s); }
            catch (Exception ex) { Ui.Error(this, "Refund not recorded:\n" + ex.Message); RefreshList(); return; }
            main.DataChanged();
            main.SetStatus("Sale " + s.Code + " refunded: give back " + Ui.Money(s.Total) + " by " + s.PaymentMethod + ". Items returned to stock.");
        }

        void ExportCsv()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "CSV file (*.csv)|*.csv";
                d.FileName = "sales-" + dtFrom.Value.ToString("yyyy-MM-dd") + "-to-" + dtTo.Value.ToString("yyyy-MM-dd") + ".csv";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Receipt,Date,Status,Payment,Barcode,Item,Size,Color,UnitPrice,Qty,LineTotal,SaleSubtotal,SaleDiscount,SaleTax,SaleTotal");
                foreach (Sale s in view.OrderBy(x => x.Date))
                    foreach (SaleLine l in s.Lines)
                        sb.AppendLine(string.Join(",", new string[] {
                            s.Code, s.Date.ToString("yyyy-MM-dd HH:mm"), s.Status, s.PaymentMethod,
                            Ui.CsvEscape(l.Barcode), Ui.CsvEscape(l.Name), Ui.CsvEscape(l.Size), Ui.CsvEscape(l.Color),
                            l.UnitPrice.ToString(CultureInfo.InvariantCulture), l.Quantity.ToString(), l.LineTotal.ToString(CultureInfo.InvariantCulture),
                            s.Subtotal.ToString(CultureInfo.InvariantCulture), s.DiscountAmount.ToString(CultureInfo.InvariantCulture),
                            s.Tax.ToString(CultureInfo.InvariantCulture), s.Total.ToString(CultureInfo.InvariantCulture) }));
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                main.SetStatus("Exported " + view.Count + " sales to " + d.FileName);
            }
        }
    }
}
