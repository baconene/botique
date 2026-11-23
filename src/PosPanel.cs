using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>The "Sell" tab: scan items into a cart, take payment, print receipt.</summary>
    public class PosPanel : UserControl
    {
        readonly MainForm main;
        readonly BindingList<SaleLine> cart = new BindingList<SaleLine>();
        TextBox txtScan, txtTendered;
        DataGridView grid;
        NumericUpDown numDiscount;
        Label lblSubtotal, lblDiscount, lblTax, lblTaxCaption, lblTotal, lblChange, lblMessage, lblItems;
        RadioButton rbCash, rbCard, rbOther;
        CheckBox chkPrint;
        Button btnComplete;
        Sale lastSale;
        Button btnChangePrice;
        DataGridViewTextBoxColumn colPrice;

        public PosPanel(MainForm main)
        {
            this.main = main;
            BackColor = Color.White;
            Build();
            UpdateTotals();
        }

        void Build()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.S(390)));
            Controls.Add(root);

            // ---------- Left: scan + cart ----------
            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 4;
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(left, 0, 0);

            TableLayoutPanel scanBar = new TableLayoutPanel();
            scanBar.Dock = DockStyle.Fill; scanBar.AutoSize = true; scanBar.ColumnCount = 3;
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            scanBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            txtScan = new TextBox();
            txtScan.Dock = DockStyle.Fill;
            txtScan.Font = new Font("Segoe UI", 16f);
            txtScan.Margin = new Padding(Ui.S(3), Ui.S(4), Ui.S(3), Ui.S(4));
            Ui.SetCue(txtScan, "Scan a barcode or type an item name, then press Enter");
            txtScan.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ProcessEntry(txtScan.Text); }
            };
            scanBar.Controls.Add(txtScan, 0, 0);
            scanBar.Controls.Add(Ui.MakeButton("Add", delegate { ProcessEntry(txtScan.Text); }, true), 1, 0);
            scanBar.Controls.Add(Ui.MakeButton("Find item...", delegate { FindItem(txtScan.Text); }), 2, 0);
            left.Controls.Add(scanBar, 0, 0);

            lblMessage = new Label();
            lblMessage.AutoSize = true;
            lblMessage.Margin = new Padding(Ui.S(4), Ui.S(2), Ui.S(4), Ui.S(6));
            lblMessage.Font = new Font("Segoe UI", 10.5f);
            lblMessage.ForeColor = Ui.Muted;
            lblMessage.Text = "Ready to scan. Bluetooth scanners in keyboard mode work anywhere on this screen.";
            left.Controls.Add(lblMessage, 0, 1);

            grid = new DataGridView();
            Ui.StyleGrid(grid);
            grid.Font = new Font("Segoe UI", 11f);
            grid.RowTemplate.Height = Ui.S(36);
            grid.MultiSelect = false;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.Columns.Add(Ui.Col("Barcode", "Barcode", 130, true));
            grid.Columns.Add(Ui.Col("Item", "Name", 0, true));
            grid.Columns.Add(Ui.Col("Size / Color", "Variant", 150, true));
            DataGridViewTextBoxColumn price = colPrice = Ui.MoneyCol("Price", "UnitPrice", 100, false);
            price.ToolTipText = "Double-click to change the price (markdown)";
            grid.Columns.Add(price);
            DataGridViewTextBoxColumn qty = Ui.NumCol("Qty", "Quantity", 70, false);
            qty.ToolTipText = "Double-click to change quantity";
            grid.Columns.Add(qty);
            grid.Columns.Add(Ui.MoneyCol("Total", "LineTotal", 110, true));
            grid.DataSource = cart;
            grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && !grid.Columns[e.ColumnIndex].ReadOnly) grid.BeginEdit(true);
            };
            grid.CellParsing += delegate(object s, DataGridViewCellParsingEventArgs e)
            {
                if (grid.Columns[e.ColumnIndex].DataPropertyName == "UnitPrice")
                {
                    decimal v;
                    if (Ui.TryParseMoney(Convert.ToString(e.Value), out v)) { e.Value = SaleMath.R(v); e.ParsingApplied = true; }
                }
            };
            grid.CellValidating += delegate(object s, DataGridViewCellValidatingEventArgs e)
            {
                string prop = grid.Columns[e.ColumnIndex].DataPropertyName;
                string text = Convert.ToString(e.FormattedValue);
                if (prop == "Quantity")
                {
                    int q;
                    if (!int.TryParse(text, out q) || q < 1 || q > 999) { e.Cancel = true; Message("Quantity must be a whole number from 1 to 999.", true); }
                }
                else if (prop == "UnitPrice")
                {
                    decimal v;
                    if (!Ui.TryParseMoney(text, out v) || v < 0) { e.Cancel = true; Message("Enter a valid price.", true); }
                }
            };
            grid.DataError += delegate(object s, DataGridViewDataErrorEventArgs e) { e.Cancel = true; Message("Invalid value.", true); };
            grid.CellEndEdit += delegate { grid.Refresh(); UpdateTotals(); FocusScanLater(); };
            left.Controls.Add(grid, 0, 2);

            FlowLayoutPanel cartButtons = new FlowLayoutPanel();
            cartButtons.AutoSize = true; cartButtons.Dock = DockStyle.Fill;
            cartButtons.Controls.Add(Ui.MakeButton("+1 Qty", delegate { ChangeQty(1); }));
            cartButtons.Controls.Add(Ui.MakeButton("-1 Qty", delegate { ChangeQty(-1); }));
            cartButtons.Controls.Add(btnChangePrice = Ui.MakeButton("Change price...", delegate { ChangePrice(); }));
            cartButtons.Controls.Add(Ui.MakeButton("Remove item", delegate { RemoveSelected(); }));
            cartButtons.Controls.Add(Ui.MakeButton("Clear sale", delegate
            {
                if (cart.Count > 0 && Ui.Confirm(this, "Clear all items from this sale?")) { ClearSale(); }
            }));
            lblItems = new Label(); lblItems.AutoSize = true; lblItems.Margin = new Padding(Ui.S(12), Ui.S(10), 0, 0); lblItems.ForeColor = Ui.Muted;
            cartButtons.Controls.Add(lblItems);
            left.Controls.Add(cartButtons, 0, 3);

            // ---------- Right: totals + payment ----------
            Panel rightHost = new Panel();
            rightHost.Dock = DockStyle.Fill;
            rightHost.Padding = new Padding(Ui.S(12), 0, 0, 0);
            root.Controls.Add(rightHost, 1, 0);

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Top;
            right.AutoSize = true;
            right.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            right.BackColor = Ui.AccentLight;
            right.Padding = new Padding(Ui.S(16));
            right.ColumnCount = 2;
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Panel paymentScroll = new Panel();
            paymentScroll.Dock = DockStyle.Fill; paymentScroll.AutoScroll = true;
            paymentScroll.Controls.Add(right);
            rightHost.Controls.Add(paymentScroll);

            Font big = new Font("Segoe UI", 12f);
            lblSubtotal = AddTotalRow(right, "Subtotal", big);
            Label discCaption = Ui.MakeLabel("Discount %"); discCaption.Font = big;
            numDiscount = new NumericUpDown();
            numDiscount.DecimalPlaces = 1; numDiscount.Maximum = 100; numDiscount.Increment = 5;
            numDiscount.Font = big; numDiscount.Anchor = AnchorStyles.Right; numDiscount.Width = Ui.S(90);
            numDiscount.TextAlign = HorizontalAlignment.Right;
            numDiscount.ValueChanged += delegate { UpdateTotals(); };
            AddRow(right, discCaption, numDiscount);
            lblDiscount = AddTotalRow(right, "Discount", big);
            lblTax = AddTotalRow(right, "Tax", big);
            lblTaxCaption = (Label)right.GetControlFromPosition(0, right.RowCount - 1);

            Label totalCaption = Ui.MakeLabel("TOTAL");
            totalCaption.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            totalCaption.ForeColor = Ui.AccentDark;
            lblTotal = new Label();
            lblTotal.AutoSize = true; lblTotal.Anchor = AnchorStyles.Right;
            lblTotal.Font = new Font("Segoe UI", 24f, FontStyle.Bold);
            lblTotal.ForeColor = Ui.AccentDark;
            lblTotal.Margin = new Padding(0, Ui.S(8), 0, Ui.S(8));
            AddRow(right, totalCaption, lblTotal);

            Label payCaption = Ui.MakeLabel("Payment");
            payCaption.Font = new Font("Segoe UI Semibold", 11f);
            payCaption.Margin = new Padding(Ui.S(3), Ui.S(14), Ui.S(3), Ui.S(3));
            AddRow(right, payCaption, null);
            FlowLayoutPanel payTypes = new FlowLayoutPanel();
            payTypes.AutoSize = true; payTypes.Dock = DockStyle.Fill;
            rbCash = new RadioButton(); rbCash.Text = "Cash"; rbCash.Checked = true; rbCash.AutoSize = true;
            rbCard = new RadioButton(); rbCard.Text = "Card"; rbCard.AutoSize = true;
            rbOther = new RadioButton(); rbOther.Text = "Other"; rbOther.AutoSize = true;
            foreach (RadioButton rb in new RadioButton[] { rbCash, rbCard, rbOther })
            {
                rb.Font = big;
                rb.CheckedChanged += delegate { txtTendered.Enabled = rbCash.Checked; UpdateTotals(); };
                payTypes.Controls.Add(rb);
            }
            AddSpan(right, payTypes);

            Label tenderedCaption = Ui.MakeLabel("Cash received"); tenderedCaption.Font = big;
            txtTendered = new TextBox();
            txtTendered.Font = big; txtTendered.Width = Ui.S(130); txtTendered.Anchor = AnchorStyles.Right;
            txtTendered.TextAlign = HorizontalAlignment.Right;
            txtTendered.TextChanged += delegate { UpdateTotals(); };
            txtTendered.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; CompleteSale(); } };
            AddRow(right, tenderedCaption, txtTendered);

            FlowLayoutPanel quickCash = new FlowLayoutPanel();
            quickCash.AutoSize = true; quickCash.Dock = DockStyle.Fill;
            quickCash.Controls.Add(Ui.MakeButton("Exact", delegate { SetTendered(0); }));
            // Common bill sizes for the PC's currency (pesos use larger denominations).
            bool peso = System.Globalization.RegionInfo.CurrentRegion.ISOCurrencySymbol == "PHP";
            foreach (int amt in peso ? new int[] { 500, 1000 } : new int[] { 20, 50, 100 })
            {
                int a = amt;
                quickCash.Controls.Add(Ui.MakeButton(((decimal)a).ToString("C0"), delegate { SetTendered(a); }));
            }
            AddSpan(right, quickCash);

            lblChange = AddTotalRow(right, "Change due", new Font("Segoe UI", 14f, FontStyle.Bold));

            chkPrint = new CheckBox();
            chkPrint.Text = "Print receipt";
            chkPrint.AutoSize = true;
            chkPrint.Checked = DataStore.Data.Settings.AutoPrintReceipt;
            chkPrint.Margin = new Padding(Ui.S(3), Ui.S(12), Ui.S(3), Ui.S(3));
            AddSpan(right, chkPrint);

            btnComplete = new Button();
            btnComplete.Text = "Complete Sale  (F12)";
            btnComplete.Dock = DockStyle.Bottom;
            btnComplete.Height = Ui.S(64);
            btnComplete.FlatStyle = FlatStyle.Flat;
            btnComplete.FlatAppearance.BorderSize = 0;
            btnComplete.BackColor = Ui.Good;
            btnComplete.ForeColor = Color.White;
            btnComplete.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            btnComplete.Cursor = Cursors.Hand;
            btnComplete.Margin = new Padding(Ui.S(3), Ui.S(10), Ui.S(3), Ui.S(6));
            btnComplete.Click += delegate { CompleteSale(); };
            rightHost.Controls.Add(btnComplete);

            FlowLayoutPanel after = new FlowLayoutPanel();
            after.AutoSize = true; after.Dock = DockStyle.Fill;
            after.Controls.Add(Ui.MakeButton("Reprint last receipt", delegate { Reprint(); }));
            after.Controls.Add(Ui.MakeButton("Open last sale", delegate { if (lastSale != null) main.GoToSale(lastSale); }));
            AddSpan(right, after);

            right.RowCount++;
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        }

        static void AddRow(TableLayoutPanel t, Control a, Control b)
        {
            int row = t.RowCount;
            t.RowCount = row + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (a != null) t.Controls.Add(a, 0, row);
            if (b != null) t.Controls.Add(b, 1, row);
        }

        static void AddSpan(TableLayoutPanel t, Control c)
        {
            int row = t.RowCount;
            t.RowCount = row + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(c, 0, row);
            t.SetColumnSpan(c, 2);
        }

        static Label AddTotalRow(TableLayoutPanel t, string caption, Font font)
        {
            Label c = Ui.MakeLabel(caption); c.Font = font;
            Label v = new Label(); v.AutoSize = true; v.Anchor = AnchorStyles.Right; v.Font = font;
            v.Margin = new Padding(Ui.S(3), Ui.S(6), Ui.S(3), Ui.S(3));
            AddRow(t, c, v);
            return v;
        }

        // ---------------- behaviour ----------------

        /// <summary>Discounts and price changes need the "discount" permission.</summary>
        public void ApplyPermissions()
        {
            bool discount = Session.Can(Permissions.Discount);
            numDiscount.Enabled = discount;
            if (!discount) numDiscount.Value = 0;
            btnChangePrice.Enabled = discount;
            colPrice.ReadOnly = !discount;
        }

        public void FocusScan()
        {
            if (txtScan == null) return;
            txtScan.Focus();
            txtScan.SelectAll();
        }

        void FocusScanLater() { BeginInvoke(new Action(FocusScan)); }

        void Message(string text, bool warn)
        {
            lblMessage.Text = text;
            lblMessage.ForeColor = warn ? Color.FromArgb(180, 40, 30) : Ui.Good;
            if (warn) SystemSounds.Exclamation.Play();
        }

        public void HandleScan(string code)
        {
            AddByCode(code, true);
        }

        void ProcessEntry(string text)
        {
            text = (text ?? "").Trim();
            txtScan.Clear();
            if (text.Length == 0) return;
            if (DataStore.FindProduct(text) != null || DataStore.FindSaleByCode(text) != null) { AddByCode(text, true); return; }

            var matches = DataStore.Data.Products.Where(p => ItemPicker.Matches(p, text)).ToList();
            if (matches.Count == 1) AddProduct(matches[0]);
            else if (matches.Count > 1) FindItem(text);
            else AddByCode(text, true);
        }

        void FindItem(string search)
        {
            using (ItemPicker picker = new ItemPicker(search, false))
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                    foreach (Product p in picker.Selected) AddProduct(p);
            }
            txtScan.Clear();
            FocusScan();
        }

        void AddByCode(string code, bool offerCreate)
        {
            Product p = DataStore.FindProduct(code);
            if (p != null) { AddProduct(p); return; }

            Sale sale = DataStore.FindSaleByCode(code);
            if (sale != null)
            {
                main.SetStatus("Scanned receipt " + sale.Code + " - opened in Sales History.");
                main.GoToSale(sale);
                return;
            }

            Message("Barcode \"" + code + "\" is not in inventory.", true);
            if (offerCreate && Session.Can(Permissions.InventoryEdit) && Ui.Confirm(this, "Barcode \"" + code + "\" is not in inventory.\n\nAdd it as a new item now?"))
            {
                using (ProductDialog d = new ProductDialog(null, null, code))
                {
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        main.DataChanged();
                        AddProduct(d.Result);
                    }
                }
            }
            FocusScan();
        }

        void AddProduct(Product p)
        {
            SaleLine line = cart.FirstOrDefault(l => l.ProductId == p.Id && l.UnitPrice == p.Price);
            if (line == null)
            {
                line = SaleLine.FromProduct(p);
                cart.Add(line);
            }
            else
            {
                line.Quantity++;
                cart.ResetItem(cart.IndexOf(line));
            }
            int inCart = cart.Where(l => l.ProductId == p.Id).Sum(l => l.Quantity);
            int idx = cart.IndexOf(line);
            if (idx >= 0 && idx < grid.Rows.Count) { grid.ClearSelection(); grid.Rows[idx].Selected = true; grid.CurrentCell = grid.Rows[idx].Cells[1]; }
            UpdateTotals();

            string variant = p.Variant.Length > 0 ? " (" + p.Variant + ")" : "";
            if (inCart > p.Quantity)
                Message("Added " + p.Name + variant + " - " + Ui.Money(p.Price) + ".  Warning: only " + p.Quantity + " in stock.", true);
            else
                Message("Added " + p.Name + variant + " - " + Ui.Money(p.Price), false);
            FocusScan();
        }

        SaleLine SelectedLine()
        {
            if (grid.CurrentRow == null || grid.CurrentRow.Index < 0 || grid.CurrentRow.Index >= cart.Count) return null;
            return cart[grid.CurrentRow.Index];
        }

        void ChangeQty(int delta)
        {
            SaleLine l = SelectedLine();
            if (l == null) return;
            l.Quantity += delta;
            if (l.Quantity <= 0) cart.Remove(l);
            else cart.ResetItem(cart.IndexOf(l));
            UpdateTotals();
            FocusScan();
        }

        void ChangePrice()
        {
            SaleLine l = SelectedLine();
            if (l == null) return;
            string s = Ui.Prompt(this, "Change price", "New price for " + l.Name + " (was " + Ui.Money(l.UnitPrice) + "):", l.UnitPrice.ToString("0.00"));
            decimal v;
            if (s != null && Ui.TryParseMoney(s, out v) && v >= 0)
            {
                l.UnitPrice = SaleMath.R(v);
                cart.ResetItem(cart.IndexOf(l));
                UpdateTotals();
            }
            FocusScan();
        }

        void RemoveSelected()
        {
            SaleLine l = SelectedLine();
            if (l != null) cart.Remove(l);
            UpdateTotals();
            FocusScan();
        }

        void ClearSale()
        {
            cart.Clear();
            numDiscount.Value = 0;
            txtTendered.Clear();
            rbCash.Checked = true;
            UpdateTotals();
            FocusScan();
        }

        void SetTendered(int amount)
        {
            decimal sub, disc, tax, total;
            Compute(out sub, out disc, out tax, out total);
            rbCash.Checked = true;
            txtTendered.Text = (amount == 0 ? total : amount).ToString("0.00");
        }

        void Compute(out decimal sub, out decimal disc, out decimal tax, out decimal total)
        {
            SaleMath.Compute(cart, numDiscount.Value, DataStore.Data.Settings.TaxRatePercent, out sub, out disc, out tax, out total);
        }

        public void UpdateTotals()
        {
            if (lblTotal == null) return;
            decimal sub, disc, tax, total;
            Compute(out sub, out disc, out tax, out total);
            lblSubtotal.Text = Ui.Money(sub);
            lblDiscount.Text = disc == 0 ? Ui.Money(0) : "-" + Ui.Money(disc);
            lblTaxCaption.Text = "Tax (" + DataStore.Data.Settings.TaxRatePercent.ToString("0.###") + "%)";
            lblTax.Text = Ui.Money(tax);
            lblTotal.Text = Ui.Money(total);
            int items = cart.Sum(l => l.Quantity);
            lblItems.Text = items + (items == 1 ? " item" : " items");

            decimal tendered;
            if (rbCash.Checked && Ui.TryParseMoney(txtTendered.Text, out tendered))
            {
                decimal change = tendered - total;
                lblChange.Text = change >= 0 ? Ui.Money(change) : "Short " + Ui.Money(-change);
                lblChange.ForeColor = change >= 0 ? Ui.Good : Color.FromArgb(180, 40, 30);
            }
            else { lblChange.Text = Ui.Money(0); lblChange.ForeColor = Color.Black; }
        }

        public void CompleteSale()
        {
            if (grid.IsCurrentCellInEditMode) grid.EndEdit();
            if (cart.Count == 0) { Message("Scan at least one item first.", true); FocusScan(); return; }

            decimal sub, disc, tax, total;
            Compute(out sub, out disc, out tax, out total);
            string method = rbCash.Checked ? "Cash" : rbCard.Checked ? "Card" : "Other";
            decimal tendered = total;
            if (method == "Cash" && txtTendered.Text.Trim().Length > 0)
            {
                if (!Ui.TryParseMoney(txtTendered.Text, out tendered)) { Message("Enter the cash amount received.", true); txtTendered.Focus(); return; }
                if (tendered < total) { Message("Cash received is less than the total.", true); txtTendered.Focus(); return; }
            }

            Sale sale = new Sale();
            try { sale.Number = DataStore.TakeSaleNumber(); }
            catch (Exception ex) { Ui.Error(this, "Cannot reach the database:\n" + ex.Message); return; }
            sale.Date = DateTime.Now;
            sale.Lines = cart.Select(l => l.Clone()).ToList();
            sale.Subtotal = sub;
            sale.DiscountPercent = numDiscount.Value;
            sale.DiscountAmount = disc;
            sale.TaxRate = DataStore.Data.Settings.TaxRatePercent;
            sale.Tax = tax;
            sale.Total = total;
            sale.PaymentMethod = method;
            sale.AmountTendered = tendered;
            sale.Change = method == "Cash" ? tendered - total : 0;

            try
            {
                DataStore.RecordSale(sale);
            }
            catch (Exception ex)
            {
                Ui.Error(this, "The sale could not be saved, so nothing was charged or removed from stock.\n\n" + ex.Message +
                    "\n\nCheck the database connection and press Complete Sale again.");
                return;
            }
            lastSale = sale;

            if (chkPrint.Checked) PrintReceipt(sale);

            ClearSale();
            main.DataChanged();
            string msg = "Sale " + sale.Code + " complete - " + Ui.Money(sale.Total) + " (" + method + ")";
            if (sale.Change > 0) msg += ".  CHANGE DUE: " + Ui.Money(sale.Change);
            Message(msg, false);
            main.SetStatus(msg);
            if (sale.Change > 0)
                MessageBox.Show(this, "Change due: " + Ui.Money(sale.Change), "Sale " + sale.Code, MessageBoxButtons.OK, MessageBoxIcon.Information);
            FocusScan();
        }

        void PrintReceipt(Sale sale)
        {
            try { ReceiptPrinter.Print(sale, DataStore.Data.Settings); }
            catch (Exception ex) { Ui.Error(this, "The sale was saved, but the receipt could not be printed:\n" + ex.Message); }
        }

        void Reprint()
        {
            if (lastSale == null) { Message("No sale completed yet in this session.", true); return; }
            PrintReceipt(lastSale);
        }
    }
}
