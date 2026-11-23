using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BoutiquePOS
{
    public class MainForm : Form
    {
        readonly TabControl tabs;
        readonly TabPage tabSell, tabInventory, tabSales, tabLabels, tabSettings;
        public readonly PosPanel Pos;
        public readonly InventoryPanel Inventory;
        public readonly SalesPanel Sales;
        public readonly LabelsPanel Labels;
        public readonly SettingsPanel SettingsView;
        readonly Label lblStore, lblScanner;
        readonly ToolStripStatusLabel status;
        readonly Timer statusTimer;
        public readonly ScannerService Scanner;

        public MainForm()
        {
            Text = "Boutique POS & Inventory";
            Font = new Font("Segoe UI", 10f);
            BackColor = Ui.Canvas;
            ForeColor = Ui.Ink;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Ui.S(1320), Ui.S(840));
            MinimumSize = new Size(Ui.S(1024), Ui.S(640));
            KeyPreview = true;

            // Header bar
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = Ui.S(72);
            header.BackColor = Color.White;
            header.Padding = new Padding(Ui.S(24), 0, Ui.S(24), 0);
            lblStore = new Label();
            lblStore.Dock = DockStyle.Fill; lblStore.AutoSize = false; lblStore.AutoEllipsis = true;
            lblStore.TextAlign = ContentAlignment.MiddleLeft;
            lblStore.ForeColor = Ui.Accent;
            lblStore.Font = new Font("Segoe UI Semibold", 20f);
            lblScanner = new Label();
            lblScanner.Dock = DockStyle.Right; lblScanner.AutoSize = false; lblScanner.Width = Ui.S(460);
            lblScanner.TextAlign = ContentAlignment.MiddleRight;
            lblScanner.ForeColor = Ui.Muted;
            header.Controls.Add(lblStore);
            header.Controls.Add(lblScanner);

            StatusStrip strip = new StatusStrip();
            strip.BackColor = Color.White;
            strip.Padding = new Padding(Ui.S(20), Ui.S(5), Ui.S(20), Ui.S(5));
            status = new ToolStripStatusLabel("Ready");
            status.Spring = true; status.TextAlign = ContentAlignment.MiddleLeft;
            strip.Items.Add(status);
            ToolStripStatusLabel dbLabel = new ToolStripStatusLabel(
                "● " + (DataStore.Config != null ? DataStore.Config.Describe() : "Offline file"));
            dbLabel.ForeColor = Ui.AccentDark;
            strip.Items.Add(dbLabel);
            statusTimer = new Timer();
            statusTimer.Interval = 8000;
            statusTimer.Tick += delegate { statusTimer.Stop(); status.Text = "Ready"; status.ForeColor = Color.Black; };

            tabs = new PageDeck();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(Ui.S(18), Ui.S(6));
            tabs.Font = new Font("Segoe UI Semibold", 10.5f);
            Pos = new PosPanel(this);
            Inventory = new InventoryPanel(this);
            Sales = new SalesPanel(this);
            Labels = new LabelsPanel(this);
            SettingsView = new SettingsPanel(this);
            tabSell = Page("Sell  (F1)", Pos, "New sale", "Scan a barcode or find an item to start checkout.");
            tabInventory = Page("Inventory  (F2)", Inventory, "Inventory", "Your collection, stock levels and product details.");
            tabSales = Page("Sales history  (F3)", Sales, "Sales history", "Review transactions, reprint receipts and manage refunds.");
            tabLabels = Page("Barcode labels  (F4)", Labels, "Barcode labels", "Prepare product labels and preview them before printing.");
            tabSettings = Page("Settings & scanner", SettingsView, "Settings", "Manage your store, printers, scanner and database connection.");
            tabs.TabPages.AddRange(new TabPage[] { tabSell, tabInventory, tabSales, tabLabels, tabSettings });
            FlowLayoutPanel navigation = new FlowLayoutPanel();
            navigation.Dock = DockStyle.Top; navigation.Height = Ui.S(50);
            navigation.BackColor = Color.White; navigation.Padding = new Padding(Ui.S(24), 0, 0, 0);
            navigation.WrapContents = false;
            List<Button> navigationButtons = new List<Button>();
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                int index = i;
                Button button = new Button(); button.Text = tabs.TabPages[i].Text;
                button.Size = new Size(Ui.S(180), Ui.S(48)); button.Margin = Padding.Empty;
                button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
                button.Font = new Font("Segoe UI Semibold", 10f); button.Cursor = Cursors.Hand;
                button.UseMnemonic = false;
                button.Click += delegate { tabs.SelectedIndex = index; };
                button.Paint += delegate(object sender, PaintEventArgs e)
                {
                    if (tabs.SelectedIndex == index)
                        using (SolidBrush accent = new SolidBrush(Ui.Accent))
                            e.Graphics.FillRectangle(accent, Ui.S(16), button.Height - Ui.S(3), button.Width - Ui.S(32), Ui.S(3));
                };
                navigation.Controls.Add(button); navigationButtons.Add(button);
            }
            Action updateNavigation = delegate
            {
                for (int i = 0; i < navigationButtons.Count; i++)
                {
                    navigationButtons[i].BackColor = i == tabs.SelectedIndex ? Ui.AccentLight : Color.White;
                    navigationButtons[i].ForeColor = i == tabs.SelectedIndex ? Ui.Accent : Ui.Muted;
                    navigationButtons[i].Invalidate();
                }
            };
            tabs.SelectedIndexChanged += delegate { updateNavigation(); OnTabChanged(); };
            updateNavigation();

            Controls.Add(tabs);
            Controls.Add(navigation);
            Controls.Add(header);
            Controls.Add(strip);

            Scanner = new ScannerService(this);
            Scanner.Scanned += OnScanned;
            Application.AddMessageFilter(Scanner);

            Load += delegate { ApplySettings(); Pos.FocusScan(); };
            FormClosing += delegate { Application.RemoveMessageFilter(Scanner); Scanner.Dispose(); };
        }

        TabPage Page(string title, Control content, string heading, string description)
        {
            TabPage p = new TabPage(title);
            p.BackColor = Ui.Canvas;
            p.Padding = new Padding(Ui.S(24), Ui.S(18), Ui.S(24), Ui.S(12));
            p.Font = new Font("Segoe UI", 10f);
            content.Dock = DockStyle.Fill;
            p.Controls.Add(content);
            p.Controls.Add(Ui.PageHeading(heading, description));
            return p;
        }

        /// <summary>Re-applies store name and scanner configuration after settings change.</summary>
        public void ApplySettings()
        {
            StoreSettings s = DataStore.Data.Settings;
            lblStore.Text = s.StoreName;
            Text = s.StoreName + " - Boutique POS & Inventory";
            if (s.ScannerMode == "Serial" && !string.IsNullOrEmpty(s.SerialPortName))
            {
                Scanner.KeyboardEnabled = true; // keep HID working too
                lblScanner.Text = "Scanner: connecting to " + s.SerialPortName + "...";
                Scanner.OpenSerialAsync(s.SerialPortName, s.BaudRate <= 0 ? 9600 : s.BaudRate, delegate(bool ok, string msg)
                {
                    lblScanner.Text = (ok ? "● Scanner: " : "○ Scanner: ") + msg;
                    SettingsView.UpdateScannerStatus();
                    if (!ok) SetStatus(msg, true);
                });
            }
            else
            {
                Scanner.CloseSerial();
                Scanner.KeyboardEnabled = true;
                lblScanner.Text = "● Scanner: Keyboard / HID mode - ready";
            }
            SettingsView.UpdateScannerStatus();
        }

        void OnTabChanged()
        {
            // With a shared database, pick up items/stock changed on other registers.
            if (tabs.SelectedTab == tabSell || tabs.SelectedTab == tabInventory || tabs.SelectedTab == tabLabels)
            {
                try { DataStore.RefreshProducts(tabs.SelectedTab == tabInventory); }
                catch (Exception ex) { SetStatus("Database unreachable - showing last loaded data. " + ex.Message, true); }
            }
            if (tabs.SelectedTab == tabSell) Pos.FocusScan();
            else if (tabs.SelectedTab == tabInventory) Inventory.RefreshGrid();
            else if (tabs.SelectedTab == tabSales) Sales.RefreshList();
            else if (tabs.SelectedTab == tabLabels) Labels.RefreshPreview();
        }

        void OnScanned(string code, string source)
        {
            Form active = Form.ActiveForm;
            if (active != null && active != this)
            {
                IScanTarget t = active as IScanTarget;
                if (t != null) t.HandleScan(code);
                return;
            }
            SettingsView.ShowScan(code, source);
            TabPage page = tabs.SelectedTab;
            if (page == tabInventory) Inventory.HandleScan(code);
            else if (page == tabSales) Sales.HandleScan(code);
            else if (page == tabLabels) Labels.HandleScan(code);
            else if (page == tabSettings) { /* test display only */ }
            else Pos.HandleScan(code);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F1: tabs.SelectedTab = tabSell; return true;
                case Keys.F2: tabs.SelectedTab = tabInventory; return true;
                case Keys.F3: tabs.SelectedTab = tabSales; return true;
                case Keys.F4: tabs.SelectedTab = tabLabels; return true;
                case Keys.F12:
                    if (tabs.SelectedTab == tabSell) { Pos.CompleteSale(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void SetStatus(string text) { SetStatus(text, false); }

        public void SetStatus(string text, bool warning)
        {
            status.Text = text;
            status.ForeColor = warning ? Color.FromArgb(170, 40, 30) : Color.Black;
            statusTimer.Stop(); statusTimer.Start();
        }

        public void GoToSale(Sale s)
        {
            tabs.SelectedTab = tabSales;
            Sales.SelectSale(s);
        }

        public void QueueLabels(IEnumerable<Product> products, bool copiesFromQuantity)
        {
            Labels.AddProducts(products, copiesFromQuantity);
            tabs.SelectedTab = tabLabels;
        }

        /// <summary>Call after any change to products or sales so every tab shows fresh data.</summary>
        public void DataChanged()
        {
            Inventory.RefreshGrid();
            Sales.RefreshList();
        }

        public void SelectTabIndex(int i) { tabs.SelectedIndex = i; }
    }
}
