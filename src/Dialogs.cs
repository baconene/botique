using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>Dialogs that want barcode scans while they are open.</summary>
    public interface IScanTarget { void HandleScan(string code); }

    public class ProductDialog : Form, IScanTarget
    {
        readonly Product target;
        readonly bool isNew;
        TextBox txtBarcode, txtName, txtBrand, txtColor, txtPrice, txtCost, txtNotes;
        ComboBox cmbCategory, cmbSize;
        NumericUpDown numQty, numReorder;
        PictureBox preview;

        public Product Result { get; private set; }

        static readonly string[] Sizes = { "XXS", "XS", "S", "M", "L", "XL", "XXL", "1X", "2X", "3X", "0", "2", "4", "6", "8", "10", "12", "14", "16", "24", "25", "26", "27", "28", "29", "30", "31", "32", "One Size" };

        /// <param name="existing">Product to edit, or null to create.</param>
        /// <param name="template">When creating a variant, the product to copy fields from.</param>
        /// <param name="presetBarcode">Barcode to pre-fill (e.g. an unknown scanned code).</param>
        public ProductDialog(Product existing, Product template, string presetBarcode)
        {
            isNew = existing == null;
            target = existing ?? new Product();
            Product src = existing ?? template;

            Text = !isNew ? "Edit item" : (template != null ? "Add variant (new size / color)" : "Add item");
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(Ui.S(14));
            BackColor = Color.White;

            AutoSize = false;
            ClientSize = new Size(Ui.S(820), Ui.S(650));
            MinimumSize = new Size(Ui.S(700), Ui.S(540));
            FormBorderStyle = FormBorderStyle.Sizable;
            Padding = new Padding(Ui.S(24));
            BackColor = Ui.Canvas;
            ForeColor = Ui.Ink;

            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill; body.ColumnCount = 2; body.AutoScroll = true;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            TableLayoutPanel details = EditorSection("Product details");
            TableLayoutPanel stock = EditorSection("Pricing & stock");
            body.Controls.Add(details, 0, 0); body.Controls.Add(stock, 1, 0);

            txtName = new TextBox(); AddField(details, "Item name *", txtName);
            cmbCategory = new ComboBox();
            foreach (string c in DataStore.Categories()) cmbCategory.Items.Add(c);
            foreach (string c in new string[] { "Dresses", "Tops", "Bottoms", "Outerwear", "Knitwear", "Accessories", "Shoes", "Jewelry" })
                if (!cmbCategory.Items.Contains(c)) cmbCategory.Items.Add(c);
            AddField(details, "Category", cmbCategory);
            txtBrand = new TextBox(); AddField(details, "Brand", txtBrand);
            cmbSize = new ComboBox(); cmbSize.Items.AddRange(Sizes); AddField(details, "Size", cmbSize);
            txtColor = new TextBox(); AddField(details, "Color", txtColor);
            txtNotes = new TextBox(); txtNotes.Multiline = true; txtNotes.Height = Ui.S(65);
            AddField(details, "Notes", txtNotes);

            txtPrice = new TextBox(); AddField(stock, "Retail price *", txtPrice);
            txtCost = new TextBox(); AddField(stock, "Cost", txtCost);
            numQty = new NumericUpDown(); numQty.Minimum = -9999; numQty.Maximum = 99999;
            AddField(stock, "Quantity on hand", numQty);
            numReorder = new NumericUpDown(); numReorder.Maximum = 9999;
            AddField(stock, "Low-stock alert at", numReorder);
            txtBarcode = new TextBox();
            Ui.SetCue(txtBarcode, "Scan, type or leave blank");
            AddField(stock, "Barcode", txtBarcode);
            Button gen = Ui.MakeButton("Generate barcode", delegate { txtBarcode.Text = DataStore.NextBarcode(); });
            stock.Controls.Add(gen, 0, stock.RowCount++);
            preview = new PictureBox(); preview.Height = Ui.S(65);
            preview.Dock = DockStyle.Top; preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BackColor = Color.White;
            stock.Controls.Add(preview, 0, stock.RowCount++);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Height = Ui.S(58); buttons.Dock = DockStyle.Bottom;
            buttons.Padding = new Padding(0, Ui.S(8), 0, 0);
            Button ok = Ui.MakeButton("Save item", delegate { OnSave(); }, true);
            Button cancel = Ui.MakeButton("Cancel", null); cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
            Controls.Add(body);
            Controls.Add(Ui.PageHeading(Text, "Keep product details and stock up to date.  * Required"));
            Controls.Add(buttons);
            AcceptButton = ok; CancelButton = cancel;

            if (src != null)
            {
                txtName.Text = src.Name; cmbCategory.Text = src.Category; txtBrand.Text = src.Brand;
                cmbSize.Text = src.Size; txtColor.Text = src.Color;
                txtPrice.Text = src.Price.ToString("0.00"); txtCost.Text = src.Cost.ToString("0.00");
                numReorder.Value = Math.Max(0, Math.Min(9999, src.ReorderLevel));
                txtNotes.Text = existing != null ? src.Notes : "";
            }
            else numReorder.Value = 2;
            if (existing != null) { txtBarcode.Text = existing.Barcode; numQty.Value = Math.Max(numQty.Minimum, Math.Min(numQty.Maximum, existing.Quantity)); }
            if (!string.IsNullOrEmpty(presetBarcode)) txtBarcode.Text = presetBarcode;

            txtBarcode.TextChanged += delegate { UpdatePreview(); };
            UpdatePreview();
            Shown += delegate
            {
                if (template != null) { cmbSize.Focus(); cmbSize.SelectAll(); }
                else { txtName.Focus(); }
            };
        }

        static TableLayoutPanel EditorSection(string title)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.ColumnCount = 1; section.AutoSize = true; section.Dock = DockStyle.Top;
            section.BackColor = Color.White; section.Padding = new Padding(Ui.S(18));
            section.Margin = new Padding(Ui.S(4));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Label heading = Ui.MakeLabel(title); heading.Font = new Font("Segoe UI Semibold", 12f);
            heading.ForeColor = Ui.Accent; heading.Margin = new Padding(0, 0, 0, Ui.S(10));
            section.Controls.Add(heading, 0, section.RowCount++);
            return section;
        }

        static void AddField(TableLayoutPanel section, string caption, Control field)
        {
            Label label = Ui.MakeLabel(caption);
            label.ForeColor = Ui.Muted; label.Margin = new Padding(0, Ui.S(7), 0, Ui.S(4));
            section.Controls.Add(label, 0, section.RowCount++);
            field.Dock = DockStyle.Top; field.Margin = new Padding(0, 0, 0, Ui.S(2));
            section.Controls.Add(field, 0, section.RowCount++);
        }

        void UpdatePreview()
        {
            Image old = preview.Image;
            string code = txtBarcode.Text.Trim();
            preview.Image = Code128.IsValid(code) ? Code128.Render(code, 3, 120, true) : null;
            if (old != null) old.Dispose();
        }

        public void HandleScan(string code) { txtBarcode.Text = code; }

        void OnSave()
        {
            string name = txtName.Text.Trim();
            if (name.Length == 0) { Ui.Error(this, "Please enter an item name."); txtName.Focus(); return; }
            decimal price, cost = 0;
            if (!Ui.TryParseMoney(txtPrice.Text, out price) || price < 0) { Ui.Error(this, "Please enter a valid retail price."); txtPrice.Focus(); return; }
            if (txtCost.Text.Trim().Length > 0 && (!Ui.TryParseMoney(txtCost.Text, out cost) || cost < 0)) { Ui.Error(this, "Please enter a valid cost (or leave it blank)."); txtCost.Focus(); return; }

            string code = txtBarcode.Text.Trim();
            if (code.Length > 0)
            {
                if (!Code128.IsValid(code)) { Ui.Error(this, "Barcode can contain only letters, digits and standard symbols."); return; }
                Product other = DataStore.FindProduct(code);
                if (other != null && other.Id != target.Id) { Ui.Error(this, "Barcode " + code + " is already used by \"" + other.Name + " " + other.Variant + "\"."); return; }
            }
            else code = DataStore.NextBarcode();

            // Edit a copy; the cached item only changes once the database accepted the save.
            Product work = isNew ? target : target.Clone();
            work.Barcode = code;
            work.Name = name;
            work.Category = cmbCategory.Text.Trim();
            work.Brand = txtBrand.Text.Trim();
            work.Size = cmbSize.Text.Trim();
            work.Color = txtColor.Text.Trim();
            work.Price = SaleMath.R(price);
            work.Cost = SaleMath.R(cost);
            work.Quantity = (int)numQty.Value;
            work.ReorderLevel = (int)numReorder.Value;
            work.Notes = txtNotes.Text.Trim();
            work.Updated = DateTime.Now;
            try
            {
                DataStore.SaveProduct(work, isNew ? null : target);
            }
            catch (Exception ex)
            {
                string msg = ex.Message.IndexOf("Duplicate entry", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "Barcode " + code + " is already used by another item (possibly added on another register)."
                    : "Could not save the item:\n" + ex.Message;
                Ui.Error(this, msg);
                return;
            }
            Result = target;
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>Search-and-pick list of inventory items.</summary>
    public class ItemPicker : Form, IScanTarget
    {
        readonly TextBox txtSearch;
        readonly DataGridView grid;
        public List<Product> Selected { get; private set; }

        public ItemPicker(string initialSearch, bool multiSelect)
        {
            Text = multiSelect ? "Select items" : "Find item";
            Font = new Font("Segoe UI", 10f);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            Size = new Size(Ui.S(820), Ui.S(520));
            Padding = new Padding(Ui.S(10));
            Selected = new List<Product>();

            txtSearch = new TextBox();
            txtSearch.Dock = DockStyle.Top;
            txtSearch.Font = new Font("Segoe UI", 12f);
            Ui.SetCue(txtSearch, "Search name, barcode, brand, size, color...");
            txtSearch.Text = initialSearch ?? "";

            grid = new DataGridView();
            Ui.StyleGrid(grid);
            grid.ReadOnly = true;
            grid.MultiSelect = multiSelect;
            grid.Columns.Add(Ui.Col("Barcode", "Barcode", 120, true));
            grid.Columns.Add(Ui.Col("Item", "Name", 0, true));
            grid.Columns.Add(Ui.Col("Size", "Size", 70, true));
            grid.Columns.Add(Ui.Col("Color", "Color", 90, true));
            grid.Columns.Add(Ui.MoneyCol("Price", "Price", 80, true));
            grid.Columns.Add(Ui.NumCol("In stock", "Quantity", 75, true));

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Bottom; buttons.AutoSize = true; buttons.FlowDirection = FlowDirection.RightToLeft;
            Button ok = Ui.MakeButton(multiSelect ? "Add selected" : "Select", delegate { Accept(); }, true);
            Button cancel = Ui.MakeButton("Cancel", null); cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
            CancelButton = cancel;

            Panel spacer = new Panel(); spacer.Dock = DockStyle.Top; spacer.Height = Ui.S(8);
            Controls.Add(grid); Controls.Add(spacer); Controls.Add(txtSearch); Controls.Add(buttons);

            txtSearch.TextChanged += delegate { Filter(); };
            txtSearch.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down) { grid.Focus(); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { Accept(); e.SuppressKeyPress = true; }
            };
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) Accept(); };
            grid.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; } };
            Shown += delegate { txtSearch.Focus(); txtSearch.SelectionStart = txtSearch.TextLength; };
            Filter();
        }

        public static bool Matches(Product p, string query)
        {
            string hay = (p.Barcode + " " + p.Name + " " + p.Category + " " + p.Brand + " " + p.Size + " " + p.Color).ToLowerInvariant();
            foreach (string tok in (query ?? "").ToLowerInvariant().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (!hay.Contains(tok)) return false;
            return true;
        }

        void Filter()
        {
            string q = txtSearch.Text;
            grid.DataSource = DataStore.Data.Products.Where(p => Matches(p, q)).OrderBy(p => p.Name).ThenBy(p => p.Size).ToList();
        }

        void Accept()
        {
            Selected = grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).Select(r => (Product)r.DataBoundItem).ToList();
            if (Selected.Count == 0 && grid.Rows.Count == 1) Selected.Add((Product)grid.Rows[0].DataBoundItem);
            if (Selected.Count == 0) return;
            DialogResult = DialogResult.OK;
        }

        public void HandleScan(string code) { txtSearch.Text = code; }
    }
}
