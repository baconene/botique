using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Linq;
using System.Windows.Forms;

namespace BoutiquePOS
{
    public class LabelJob
    {
        public Product Product { get; set; }
        public int Copies { get; set; }
        public string Barcode { get { return Product.Barcode; } }
        public string Name { get { return Product.Name; } }
        public string Variant { get { return Product.Variant; } }
        public decimal Price { get { return Product.Price; } }
    }

    public class LabelsPanel : UserControl
    {
        readonly MainForm main;
        readonly BindingList<LabelJob> queue = new BindingList<LabelJob>();
        DataGridView grid;
        ComboBox cmbLayout, cmbPrinter;
        NumericUpDown numStart;
        PictureBox preview;
        Label lblCount, lblPreviewCaption;

        public LabelsPanel(MainForm main)
        {
            this.main = main;
            BackColor = Color.White;
            Build();
        }

        void Build()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill; root.ColumnCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.S(430)));
            Controls.Add(root);

            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill; left.ColumnCount = 1; left.RowCount = 4;
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(left, 0, 0);

            Label intro = new Label();
            intro.AutoSize = true;
            intro.ForeColor = Ui.Muted;
            intro.Margin = new Padding(Ui.S(3), Ui.S(4), Ui.S(3), Ui.S(6));
            intro.Text = "Scan items or add them from inventory, set copies, then Preview / Print. Each label shows name, size/color, price and a Code 128 barcode.";
            intro.MaximumSize = new Size(Ui.S(760), 0);
            left.SizeChanged += delegate { intro.MaximumSize = new Size(Math.Max(Ui.S(100), left.ClientSize.Width - Ui.S(12)), 0); };
            left.Controls.Add(intro, 0, 0);

            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.AutoSize = true; tools.Dock = DockStyle.Fill;
            tools.Controls.Add(Ui.MakeButton("+ Add from inventory...", delegate { AddFromInventory(); }, true));
            tools.Controls.Add(Ui.MakeButton("Remove", delegate { RemoveSelected(); }));
            tools.Controls.Add(Ui.MakeButton("Clear list", delegate { queue.Clear(); UpdateCount(); RefreshPreview(); }));
            tools.Controls.Add(Ui.MakeButton("Save barcode image (PNG)...", delegate { SavePng(); }));
            left.Controls.Add(tools, 0, 1);

            grid = new DataGridView();
            Ui.StyleGrid(grid);
            grid.MultiSelect = true;
            grid.Columns.Add(Ui.Col("Barcode", "Barcode", 130, true));
            grid.Columns.Add(Ui.Col("Item", "Name", 0, true));
            grid.Columns.Add(Ui.Col("Size / Color", "Variant", 150, true));
            grid.Columns.Add(Ui.MoneyCol("Price", "Price", 90, true));
            DataGridViewTextBoxColumn copies = Ui.NumCol("Copies", "Copies", 80, false);
            copies.DefaultCellStyle.BackColor = Color.FromArgb(255, 252, 235);
            grid.Columns.Add(copies);
            grid.DataSource = queue;
            grid.CellValidating += delegate(object s, DataGridViewCellValidatingEventArgs e)
            {
                if (grid.Columns[e.ColumnIndex].DataPropertyName != "Copies") return;
                int n;
                if (!int.TryParse(Convert.ToString(e.FormattedValue), out n) || n < 0 || n > 500) { e.Cancel = true; main.SetStatus("Copies must be 0-500.", true); }
            };
            grid.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.Cancel = true; };
            grid.CellEndEdit += delegate { UpdateCount(); };
            grid.SelectionChanged += delegate { RefreshPreview(); };
            left.Controls.Add(grid, 0, 2);

            lblCount = new Label(); lblCount.AutoSize = true; lblCount.ForeColor = Ui.Muted; lblCount.Margin = new Padding(Ui.S(3), Ui.S(8), Ui.S(3), Ui.S(3));
            left.Controls.Add(lblCount, 0, 3);

            // ---- right: options + preview ----
            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Top;
            right.AutoSize = true;
            right.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            right.BackColor = Ui.AccentLight;
            right.Padding = new Padding(Ui.S(14));
            right.Margin = new Padding(Ui.S(12), 0, 0, 0);
            right.ColumnCount = 1;
            Panel optionsHost = new Panel(); optionsHost.Dock = DockStyle.Fill; optionsHost.AutoScroll = true;
            optionsHost.BackColor = Ui.AccentLight;
            optionsHost.Controls.Add(right);
            root.Controls.Add(optionsHost, 1, 0);

            right.Controls.Add(Caption("Label stock"));
            cmbLayout = new ComboBox(); cmbLayout.DropDownStyle = ComboBoxStyle.DropDownList; cmbLayout.Dock = DockStyle.Fill;
            foreach (LabelLayout l in LabelPrinter.Layouts) cmbLayout.Items.Add(l);
            cmbLayout.SelectedItem = LabelPrinter.Find(DataStore.Data.Settings.LabelLayout);
            cmbLayout.SelectedIndexChanged += delegate
            {
                LabelLayout l = (LabelLayout)cmbLayout.SelectedItem;
                numStart.Enabled = l.IsSheet;
                numStart.Maximum = l.PerPage;
                DataStore.Data.Settings.LabelLayout = l.Key;
                try { DataStore.SaveSettings(); } catch (Exception) { /* non-critical preference */ }
                RefreshPreview();
            };
            right.Controls.Add(cmbLayout);

            right.Controls.Add(Caption("Start at label # (to reuse a partly used sheet)"));
            numStart = new NumericUpDown(); numStart.Minimum = 1; numStart.Maximum = 30; numStart.Width = Ui.S(90);
            right.Controls.Add(numStart);

            right.Controls.Add(Caption("Printer"));
            cmbPrinter = new ComboBox(); cmbPrinter.DropDownStyle = ComboBoxStyle.DropDownList; cmbPrinter.Dock = DockStyle.Fill;
            right.Controls.Add(cmbPrinter);
            LoadPrinters();

            FlowLayoutPanel printButtons = new FlowLayoutPanel();
            printButtons.AutoSize = true; printButtons.Dock = DockStyle.Fill;
            printButtons.Margin = new Padding(0, Ui.S(10), 0, Ui.S(10));
            printButtons.Controls.Add(Ui.MakeButton("Print preview", delegate { Print(true); }));
            Button print = Ui.MakeButton("Print labels", delegate { Print(false); }, true);
            print.Font = new Font("Segoe UI Semibold", 11f);
            printButtons.Controls.Add(print);
            right.Controls.Add(printButtons);

            lblPreviewCaption = Caption("Preview");
            right.Controls.Add(lblPreviewCaption);
            preview = new PictureBox();
            preview.Dock = DockStyle.Fill;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BackColor = Color.White;
            preview.MinimumSize = new Size(0, Ui.S(160));
            right.Controls.Add(preview);
            right.RowCount = right.Controls.Count;
            for (int i = 0; i < right.RowCount; i++) right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles[right.RowCount - 1] = new RowStyle(SizeType.Percent, 100);

            numStart.Enabled = ((LabelLayout)cmbLayout.SelectedItem).IsSheet;
            UpdateCount();
        }

        static Label Caption(string text)
        {
            Label l = Ui.MakeLabel(text);
            l.Font = new Font("Segoe UI Semibold", 10f);
            l.ForeColor = Ui.AccentDark;
            l.Margin = new Padding(Ui.S(2), Ui.S(10), Ui.S(2), Ui.S(2));
            return l;
        }

        public void LoadPrinters()
        {
            cmbPrinter.Items.Clear();
            cmbPrinter.Items.Add("(Windows default printer)");
            try { foreach (string p in PrinterSettings.InstalledPrinters) cmbPrinter.Items.Add(p); } catch (Exception) { }
            string want = DataStore.Data.Settings.LabelPrinter;
            int i = string.IsNullOrEmpty(want) ? 0 : cmbPrinter.Items.IndexOf(want);
            cmbPrinter.SelectedIndex = i < 0 ? 0 : i;
        }

        public void AddProducts(IEnumerable<Product> products, bool copiesFromQuantity)
        {
            foreach (Product p in products)
            {
                int copies = copiesFromQuantity ? Math.Max(1, p.Quantity) : 1;
                LabelJob existing = queue.FirstOrDefault(j => j.Product == p);
                if (existing != null) { existing.Copies += copies; queue.ResetItem(queue.IndexOf(existing)); }
                else { LabelJob j = new LabelJob(); j.Product = p; j.Copies = copies; queue.Add(j); }
            }
            UpdateCount();
            RefreshPreview();
        }

        public void HandleScan(string code)
        {
            Product p = DataStore.FindProduct(code);
            if (p == null) { main.SetStatus("Barcode " + code + " is not in inventory - add it on the Inventory tab first.", true); return; }
            AddProducts(new Product[] { p }, false);
            main.SetStatus("Added label for " + p.Name + " " + p.Variant + ".");
        }

        void AddFromInventory()
        {
            using (ItemPicker picker = new ItemPicker("", true))
                if (picker.ShowDialog(this) == DialogResult.OK) AddProducts(picker.Selected, false);
        }

        void RemoveSelected()
        {
            foreach (LabelJob j in grid.SelectedRows.Cast<DataGridViewRow>().Select(r => (LabelJob)r.DataBoundItem).ToList()) queue.Remove(j);
            UpdateCount();
            RefreshPreview();
        }

        void UpdateCount()
        {
            if (lblCount == null) return;
            int n = queue.Sum(j => j.Copies);
            LabelLayout l = cmbLayout == null ? null : (LabelLayout)cmbLayout.SelectedItem;
            string pages = "";
            if (l != null && l.IsSheet && n > 0)
            {
                int start = (int)numStart.Value - 1;
                pages = string.Format("  ({0} sheet(s))", (start + n + l.PerPage - 1) / l.PerPage);
            }
            lblCount.Text = n + " label(s) to print" + pages;
        }

        List<Product> Expanded()
        {
            List<Product> list = new List<Product>();
            foreach (LabelJob j in queue) for (int i = 0; i < j.Copies; i++) list.Add(j.Product);
            return list;
        }

        string SelectedPrinter()
        {
            return cmbPrinter.SelectedIndex <= 0 ? "" : (string)cmbPrinter.SelectedItem;
        }

        public void RefreshPreview()
        {
            if (preview == null) return;
            Product p = null;
            if (grid.CurrentRow != null && grid.CurrentRow.Index >= 0 && grid.CurrentRow.Index < queue.Count) p = queue[grid.CurrentRow.Index].Product;
            else if (queue.Count > 0) p = queue[0].Product;
            Image old = preview.Image;
            preview.Image = p == null ? null : LabelPrinter.RenderLabel(p, (LabelLayout)cmbLayout.SelectedItem, 300, DataStore.Data.Settings.StoreName);
            lblPreviewCaption.Text = p == null ? "Preview (add an item to see its label)" : "Preview - " + p.Name;
            if (old != null) old.Dispose();
            UpdateCount();
        }

        void Print(bool previewOnly)
        {
            if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            List<Product> labels = Expanded();
            if (labels.Count == 0) { Ui.Error(this, "Add at least one item (with copies > 0) to print."); return; }
            LabelLayout layout = (LabelLayout)cmbLayout.SelectedItem;
            string printer = SelectedPrinter();
            if (DataStore.Data.Settings.LabelPrinter != printer) { DataStore.Data.Settings.LabelPrinter = printer; try { DataStore.SaveSettings(); } catch (Exception) { } }
            try
            {
                using (PrintDocument doc = LabelPrinter.CreateDocument(labels, layout, (int)numStart.Value, printer, DataStore.Data.Settings.StoreName))
                {
                    if (previewOnly)
                    {
                        using (PrintPreviewDialog dlg = new PrintPreviewDialog())
                        {
                            dlg.Document = doc;
                            dlg.WindowState = FormWindowState.Maximized;
                            dlg.ShowDialog(this);
                        }
                    }
                    else
                    {
                        if (!Ui.Confirm(this, "Print " + labels.Count + " label(s) on \"" + layout.Name + "\" to " +
                            (printer.Length == 0 ? "the default printer" : printer) + "?")) return;
                        doc.Print();
                        main.SetStatus("Sent " + labels.Count + " label(s) to the printer.");
                    }
                }
            }
            catch (Exception ex) { Ui.Error(this, "Could not print labels: " + ex.Message); }
        }

        void SavePng()
        {
            Product p = null;
            if (grid.CurrentRow != null && grid.CurrentRow.Index < queue.Count) p = queue[grid.CurrentRow.Index].Product;
            if (p == null) { Ui.Error(this, "Select an item in the list first."); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "PNG image (*.png)|*.png";
                d.FileName = p.Barcode + ".png";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                using (Bitmap bmp = Code128.Render(p.Barcode, 4, 160, true)) bmp.Save(d.FileName, ImageFormat.Png);
                main.SetStatus("Saved " + d.FileName);
            }
        }
    }
}
