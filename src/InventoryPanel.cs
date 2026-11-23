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
    public class InventoryPanel : UserControl
    {
        readonly MainForm main;
        TextBox txtSearch;
        ComboBox cmbCategory;
        CheckBox chkLow, chkReceive;
        DataGridView grid;
        Label lblSummary;
        Label metricItems, metricUnits, metricValue, metricLow;
        List<Product> view = new List<Product>();

        public InventoryPanel(MainForm main)
        {
            this.main = main;
            BackColor = Color.White;
            Build();
            RefreshGrid();
        }

        void Build()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill; root.ColumnCount = 1; root.RowCount = 5;
            root.BackColor = Ui.Canvas;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            TableLayoutPanel metrics = new TableLayoutPanel();
            metrics.Dock = DockStyle.Fill; metrics.Height = Ui.S(86); metrics.ColumnCount = 4;
            metrics.Margin = new Padding(0, 0, 0, Ui.S(16));
            metricItems = Metric(metrics, "PRODUCTS", 0);
            metricUnits = Metric(metrics, "UNITS ON HAND", 1);
            metricValue = Metric(metrics, "RETAIL VALUE", 2);
            metricLow = Metric(metrics, "LOW / OUT OF STOCK", 3);
            root.Controls.Add(metrics, 0, 0);
            Resize += delegate { metrics.Visible = Height >= Ui.S(450); };

            FlowLayoutPanel filters = new FlowLayoutPanel();
            filters.AutoSize = true; filters.Dock = DockStyle.Fill; filters.WrapContents = true;
            filters.BackColor = Color.White; filters.Padding = new Padding(Ui.S(10));
            txtSearch = new TextBox(); txtSearch.Width = Ui.S(320); txtSearch.Margin = new Padding(Ui.S(3), Ui.S(5), Ui.S(3), Ui.S(3));
            Ui.SetCue(txtSearch, "Search name, barcode, brand, size, color...");
            txtSearch.TextChanged += delegate { RefreshGrid(); };
            cmbCategory = new ComboBox(); cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList; cmbCategory.Width = Ui.S(180);
            cmbCategory.Margin = new Padding(Ui.S(3), Ui.S(5), Ui.S(3), Ui.S(3));
            cmbCategory.SelectedIndexChanged += delegate { RefreshGrid(); };
            chkLow = new CheckBox(); chkLow.Text = "Low stock only"; chkLow.AutoSize = true; chkLow.Margin = new Padding(Ui.S(10), Ui.S(8), Ui.S(3), Ui.S(3));
            chkLow.CheckedChanged += delegate { RefreshGrid(); };
            chkReceive = new CheckBox(); chkReceive.Text = "Receive stock mode (each scan adds +1)"; chkReceive.AutoSize = true;
            chkReceive.Margin = new Padding(Ui.S(16), Ui.S(8), Ui.S(3), Ui.S(3));
            chkReceive.Font = new Font("Segoe UI Semibold", 10f);
            chkReceive.ForeColor = Ui.AccentDark;
            filters.Controls.AddRange(new Control[] { txtSearch, cmbCategory, chkLow, chkReceive });
            root.Controls.Add(filters, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true; actions.Dock = DockStyle.Fill;
            actions.Padding = new Padding(Ui.S(8), Ui.S(2), Ui.S(8), Ui.S(8));
            actions.BackColor = Color.White;
            actions.Controls.Add(Ui.MakeButton("+ Add item", delegate { AddItem(null); }, true));
            actions.Controls.Add(Ui.MakeButton("Edit", delegate { EditSelected(); }));
            actions.Controls.Add(Ui.MakeButton("Add variant", delegate { AddVariant(); }));
            actions.Controls.Add(Ui.MakeButton("Adjust stock...", delegate { AdjustStock(); }));
            actions.Controls.Add(Ui.MakeButton("Print labels", delegate { PrintLabels(); }));
            ContextMenuStrip moreMenu = new ContextMenuStrip();
            moreMenu.Font = Font;
            moreMenu.Items.Add("Import CSV...", null, delegate { ImportCsv(); });
            moreMenu.Items.Add("Export CSV...", null, delegate { ExportCsv(); });
            moreMenu.Items.Add(new ToolStripSeparator());
            moreMenu.Items.Add("Delete selected items...", null, delegate { DeleteSelected(); });
            Button more = Ui.MakeButton("More actions ▾", null);
            more.Click += delegate { moreMenu.Show(more, new Point(0, more.Height)); };
            Disposed += delegate { moreMenu.Dispose(); };
            actions.Controls.Add(more);
            root.Controls.Add(actions, 0, 2);

            grid = new DataGridView();
            Ui.StyleGrid(grid);
            grid.ReadOnly = true;
            grid.MultiSelect = true;
            grid.Columns.Add(Ui.Col("Barcode", "Barcode", 120, true));
            grid.Columns.Add(Ui.Col("Item", "Name", 0, true));
            grid.Columns.Add(Ui.Col("Category", "Category", 110, true));
            grid.Columns.Add(Ui.Col("Brand", "Brand", 120, true));
            grid.Columns.Add(Ui.Col("Size", "Size", 70, true));
            grid.Columns.Add(Ui.Col("Color", "Color", 90, true));
            grid.Columns.Add(Ui.MoneyCol("Price", "Price", 85, true));
            grid.Columns.Add(Ui.MoneyCol("Cost", "Cost", 80, true));
            grid.Columns.Add(Ui.NumCol("On hand", "Quantity", 80, true));
            grid.Columns.Add(Ui.NumCol("Alert at", "ReorderLevel", 75, true));
            grid.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= view.Count) return;
                Product p = view[e.RowIndex];
                if (p.Quantity <= 0) e.CellStyle.BackColor = Ui.Bad;
                else if (p.IsLowStock) e.CellStyle.BackColor = Ui.Warn;
            };
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) EditSelected(); };
            grid.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { EditSelected(); e.Handled = true; }
            };
            root.Controls.Add(grid, 0, 3);

            lblSummary = new Label();
            lblSummary.AutoSize = true;
            lblSummary.Margin = new Padding(Ui.S(3), Ui.S(8), Ui.S(3), Ui.S(3));
            lblSummary.ForeColor = Ui.Muted;
            root.Controls.Add(lblSummary, 0, 4);
        }

        static Label Metric(TableLayoutPanel host, string caption, int column)
        {
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            Panel card = new Panel(); card.Dock = DockStyle.Fill; card.BackColor = Color.White;
            card.Padding = new Padding(Ui.S(16), Ui.S(10), Ui.S(12), Ui.S(8));
            card.Margin = new Padding(column == 0 ? 0 : Ui.S(6), 0, column == 3 ? 0 : Ui.S(6), 0);
            Label title = new Label(); title.Text = caption; title.Dock = DockStyle.Top;
            title.Height = Ui.S(22); title.ForeColor = Ui.Muted; title.Font = new Font("Segoe UI Semibold", 9f);
            Label value = new Label(); value.Dock = DockStyle.Fill; value.ForeColor = Ui.AccentDark;
            value.Font = new Font("Segoe UI Semibold", 21f); value.AutoEllipsis = true;
            card.Controls.Add(value); card.Controls.Add(title); host.Controls.Add(card, column, 0);
            return value;
        }

        public void RefreshGrid()
        {
            if (grid == null) return;
            string keepCat = cmbCategory.SelectedItem as string;
            List<string> cats = DataStore.Categories();
            cats.Insert(0, "All categories");
            if (!cats.SequenceEqual(cmbCategory.Items.Cast<string>()))
            {
                cmbCategory.Items.Clear();
                cmbCategory.Items.AddRange(cats.ToArray());
                int i = keepCat == null ? 0 : cats.IndexOf(keepCat);
                cmbCategory.SelectedIndex = i < 0 ? 0 : i;
                return; // SelectedIndexChanged re-enters RefreshGrid
            }
            if (cmbCategory.SelectedIndex < 0 && cmbCategory.Items.Count > 0) { cmbCategory.SelectedIndex = 0; return; }

            HashSet<string> selected = new HashSet<string>(SelectedProducts().Select(p => p.Id));
            string q = txtSearch.Text;
            string cat = cmbCategory.SelectedIndex > 0 ? (string)cmbCategory.SelectedItem : null;
            view = DataStore.Data.Products
                .Where(p => ItemPicker.Matches(p, q))
                .Where(p => cat == null || string.Equals(p.Category, cat, StringComparison.OrdinalIgnoreCase))
                .Where(p => !chkLow.Checked || p.IsLowStock)
                .OrderBy(p => p.Name).ThenBy(p => p.Color).ThenBy(p => p.Size)
                .ToList();
            grid.DataSource = null;
            grid.DataSource = view;
            grid.ClearSelection();
            for (int i = 0; i < view.Count; i++) if (selected.Contains(view[i].Id)) grid.Rows[i].Selected = true;

            List<Product> all = DataStore.Data.Products;
            int units = all.Sum(p => Math.Max(0, p.Quantity));
            decimal retail = all.Sum(p => Math.Max(0, p.Quantity) * p.Price);
            decimal cost = all.Sum(p => Math.Max(0, p.Quantity) * p.Cost);
            int low = all.Count(p => p.IsLowStock);
            metricItems.Text = all.Count.ToString("N0"); metricUnits.Text = units.ToString("N0");
            metricValue.Text = Ui.Money(retail); metricLow.Text = low.ToString("N0");
            lblSummary.Text = string.Format("Showing {0} of {1} products   •   {3} low / out of stock   •   Cost value {2}   •   Double-click to edit",
                view.Count, all.Count, Ui.Money(cost), low);
        }

        List<Product> SelectedProducts()
        {
            if (grid == null) return new List<Product>();
            return grid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Index < view.Count).OrderBy(r => r.Index).Select(r => view[r.Index]).ToList();
        }

        Product Current()
        {
            List<Product> s = SelectedProducts();
            if (s.Count > 0) return s[0];
            if (grid.CurrentRow != null && grid.CurrentRow.Index < view.Count) return view[grid.CurrentRow.Index];
            return null;
        }

        void SelectProduct(Product p)
        {
            int i = view.IndexOf(p);
            if (i < 0)
            {
                txtSearch.Text = ""; chkLow.Checked = false; cmbCategory.SelectedIndex = 0;
                RefreshGrid();
                i = view.IndexOf(p);
            }
            if (i < 0) return;
            grid.ClearSelection();
            grid.Rows[i].Selected = true;
            grid.CurrentCell = grid.Rows[i].Cells[1];
            grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 3);
        }

        public void HandleScan(string code)
        {
            Product p = DataStore.FindProduct(code);
            if (p != null)
            {
                if (chkReceive.Checked)
                {
                    try { DataStore.ChangeQuantity(p, 1, "received"); }
                    catch (Exception ex) { Ui.Error(this, "Stock not updated:\n" + ex.Message); return; }
                    RefreshGrid();
                    main.SetStatus("Received +1  " + p.Name + " " + p.Variant + "  - now " + p.Quantity + " on hand.");
                }
                else main.SetStatus("Found " + p.Name + " " + p.Variant + " - " + p.Quantity + " on hand.");
                SelectProduct(p);
                return;
            }
            main.SetStatus("Barcode " + code + " is not in inventory.", true);
            if (Ui.Confirm(this, "Barcode \"" + code + "\" is not in inventory.\n\nCreate a new item with this barcode?"))
                AddItem(code);
        }

        void AddItem(string barcode)
        {
            using (ProductDialog d = new ProductDialog(null, null, barcode))
            {
                if (chkReceive.Checked && barcode != null) d.Shown += delegate { };
                if (d.ShowDialog(this) == DialogResult.OK) { main.DataChanged(); SelectProduct(d.Result); }
            }
        }

        void EditSelected()
        {
            Product p = Current();
            if (p == null) return;
            using (ProductDialog d = new ProductDialog(p, null, null))
                if (d.ShowDialog(this) == DialogResult.OK) { main.DataChanged(); SelectProduct(p); }
        }

        void AddVariant()
        {
            Product p = Current();
            if (p == null) { Ui.Error(this, "Select the item you want to copy, then click Add variant."); return; }
            using (ProductDialog d = new ProductDialog(null, p, null))
                if (d.ShowDialog(this) == DialogResult.OK) { main.DataChanged(); SelectProduct(d.Result); }
        }

        void AdjustStock()
        {
            Product p = Current();
            if (p == null) return;
            string s = Ui.Prompt(this, "Adjust stock",
                p.Name + " " + p.Variant + "\nCurrently " + p.Quantity + " on hand.\n\nEnter a change like +5 or -2, or a new total like =12:", "+1");
            if (s == null) return;
            s = s.Trim();
            int n;
            int delta;
            if (s.StartsWith("=") && int.TryParse(s.Substring(1), out n)) delta = n - p.Quantity;
            else if (int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out n)) delta = n;
            else { Ui.Error(this, "Please enter a number like +5, -2 or =12."); return; }
            try { DataStore.ChangeQuantity(p, delta, "adjustment"); }
            catch (Exception ex) { Ui.Error(this, "Stock not updated:\n" + ex.Message); return; }
            RefreshGrid();
            SelectProduct(p);
            main.SetStatus(p.Name + " " + p.Variant + " now has " + p.Quantity + " on hand.");
        }

        void DeleteSelected()
        {
            List<Product> sel = SelectedProducts();
            if (sel.Count == 0) return;
            string what = sel.Count == 1 ? "\"" + sel[0].Name + " " + sel[0].Variant + "\"" : sel.Count + " items";
            if (!Ui.Confirm(this, "Delete " + what + " from inventory?\n\nPast sales keep their records.")) return;
            try { DataStore.DeleteProducts(sel); }
            catch (Exception ex) { Ui.Error(this, "Delete failed:\n" + ex.Message); }
            main.DataChanged();
        }

        void PrintLabels()
        {
            List<Product> sel = SelectedProducts();
            if (sel.Count == 0) { Ui.Error(this, "Select one or more items first (Ctrl+click or Shift+click for several)."); return; }
            bool byQty = MessageBox.Show(this, "Print one label per unit on hand?\n\nYes = one label for each piece in stock\nNo = one label per item",
                "Print labels", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            main.QueueLabels(sel, byQty);
        }

        // ---------------- CSV ----------------

        static readonly string[] CsvColumns = { "Barcode", "Name", "Category", "Brand", "Size", "Color", "Price", "Cost", "Quantity", "ReorderLevel", "Notes" };

        void ExportCsv()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "CSV file (*.csv)|*.csv";
                d.FileName = "inventory-" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(string.Join(",", CsvColumns));
                foreach (Product p in DataStore.Data.Products.OrderBy(x => x.Name))
                {
                    sb.AppendLine(string.Join(",", new string[] {
                        Ui.CsvEscape(p.Barcode), Ui.CsvEscape(p.Name), Ui.CsvEscape(p.Category), Ui.CsvEscape(p.Brand),
                        Ui.CsvEscape(p.Size), Ui.CsvEscape(p.Color), p.Price.ToString(CultureInfo.InvariantCulture),
                        p.Cost.ToString(CultureInfo.InvariantCulture), p.Quantity.ToString(), p.ReorderLevel.ToString(), Ui.CsvEscape(p.Notes) }));
                }
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                main.SetStatus("Exported " + DataStore.Data.Products.Count + " items to " + d.FileName);
            }
        }

        void ImportCsv()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "CSV file (*.csv)|*.csv|All files|*.*";
                d.Title = "Import inventory CSV (columns: " + string.Join(", ", CsvColumns) + ")";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    int added, updated, skipped;
                    try { ImportCsvFile(d.FileName, out added, out updated, out skipped); }
                    finally { main.DataChanged(); }
                    MessageBox.Show(this, string.Format("Import finished.\n\nAdded: {0}\nUpdated: {1}\nSkipped (no name): {2}", added, updated, skipped),
                        "Import CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { Ui.Error(this, "Import failed: " + ex.Message); }
            }
        }

        /// <summary>Rows whose barcode already exists update that item; others are added. Name is required for new rows.</summary>
        public static void ImportCsvFile(string path, out int added, out int updated, out int skipped)
        {
            added = updated = skipped = 0;
            List<List<string>> rows = ParseCsv(File.ReadAllText(path));
            if (rows.Count < 2) return;
            Dictionary<string, int> col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++) col[rows[0][i].Trim()] = i;
            Func<List<string>, string, string> get = delegate(List<string> r, string name)
            {
                int i;
                return col.TryGetValue(name, out i) && i < r.Count ? r[i].Trim() : null;
            };
            for (int ri = 1; ri < rows.Count; ri++)
            {
                List<string> r = rows[ri];
                if (r.Count == 0 || r.All(x => x.Trim().Length == 0)) continue;
                string code = get(r, "Barcode");
                Product p = string.IsNullOrEmpty(code) ? null : DataStore.FindProduct(code);
                bool isNew = p == null;
                string name = get(r, "Name");
                if (isNew && string.IsNullOrEmpty(name)) { skipped++; continue; }
                Product original = p;
                p = isNew ? new Product() : original.Clone();
                if (!string.IsNullOrEmpty(name)) p.Name = name;
                string v;
                if ((v = get(r, "Category")) != null) p.Category = v;
                if ((v = get(r, "Brand")) != null) p.Brand = v;
                if ((v = get(r, "Size")) != null) p.Size = v;
                if ((v = get(r, "Color")) != null) p.Color = v;
                if ((v = get(r, "Notes")) != null) p.Notes = v;
                decimal dv; int iv;
                if ((v = get(r, "Price")) != null && Ui.TryParseMoney(v, out dv)) p.Price = SaleMath.R(dv);
                if ((v = get(r, "Cost")) != null && Ui.TryParseMoney(v, out dv)) p.Cost = SaleMath.R(dv);
                if ((v = get(r, "Quantity")) != null && int.TryParse(v, out iv)) p.Quantity = iv;
                if ((v = get(r, "ReorderLevel")) != null && int.TryParse(v, out iv)) p.ReorderLevel = iv;
                p.Updated = DateTime.Now;
                if (isNew)
                {
                    p.Barcode = !string.IsNullOrEmpty(code) && Code128.IsValid(code) ? code : DataStore.NextBarcode();
                    DataStore.UpsertProduct(p, true, p.Quantity);
                    added++;
                }
                else
                {
                    DataStore.UpsertProduct(p, false, p.Quantity - original.Quantity);
                    original.CopyFrom(p);
                    updated++;
                }
            }
            DataStore.Store.Commit();
        }

        public static List<List<string>> ParseCsv(string text)
        {
            List<List<string>> rows = new List<List<string>>();
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { row.Add(field.ToString()); field.Length = 0; }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString()); field.Length = 0;
                    rows.Add(row); row = new List<string>();
                }
                else if (c != (char)0xFEFF) field.Append(c); // skip byte-order mark
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
