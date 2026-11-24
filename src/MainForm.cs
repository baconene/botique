using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>
    /// Main window, laid out like the web back office: a rounded workspace on a lavender backdrop,
    /// a white sidebar (store name, WORKSPACE / YOUR BOUTIQUE navigation, signed-in person) and a
    /// top bar with the breadcrumb and device status above the current page.
    /// </summary>
    public class MainForm : Form
    {
        readonly TabControl tabs;
        readonly TabPage tabOverview, tabSell, tabInventory, tabSales, tabLabels, tabSettings, tabUsers;
        public readonly OverviewPanel Overview;
        public readonly PosPanel Pos;
        public readonly InventoryPanel Inventory;
        public readonly SalesPanel Sales;
        public readonly LabelsPanel Labels;
        public readonly SettingsPanel SettingsView;
        public readonly UsersPanel UsersView;
        readonly Label lblStore, lblScanner, lblUser, lblRole, lblAvatar, lblCrumb;
        readonly List<NavButton> navigationButtons = new List<NavButton>();
        readonly Action fitSidebarNote;
        readonly ToolStripStatusLabel status;
        readonly Timer statusTimer;
        public readonly ScannerService Scanner;

        /// <summary>Sidebar buttons in page order (used by the self-test).</summary>
        public NavButton[] NavigationButtons { get { return navigationButtons.ToArray(); } }

        public MainForm()
        {
            Text = "Boutique POS & Inventory";
            Font = new Font("Segoe UI", 10f);
            BackColor = Ui.Backdrop;
            ForeColor = Ui.Ink;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(Ui.S(1360), Ui.S(860));
            MinimumSize = new Size(Ui.S(1080), Ui.S(660));
            Padding = new Padding(Ui.S(14));
            KeyPreview = true;
            DoubleBuffered = true;

            // Rounded workspace frame
            Panel frame = new Panel();
            frame.Dock = DockStyle.Fill;
            frame.BackColor = Ui.Canvas;
            frame.Resize += delegate
            {
                if (frame.Width <= 0 || frame.Height <= 0) return;
                using (GraphicsPath p = Ui.RoundRect(new Rectangle(0, 0, frame.Width, frame.Height), Ui.S(22)))
                    frame.Region = new Region(p);
            };

            // ---------------- sidebar ----------------
            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left; sidebar.Width = Ui.S(236);
            sidebar.BackColor = Color.White;
            sidebar.Padding = new Padding(Ui.S(16), Ui.S(28), Ui.S(17), Ui.S(16));
            sidebar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen line = new Pen(Ui.Line)) e.Graphics.DrawLine(line, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
            };

            lblStore = new Label();
            lblStore.Dock = DockStyle.Top; lblStore.Height = Ui.S(34); lblStore.AutoEllipsis = true; lblStore.UseMnemonic = false;
            lblStore.Font = Ui.Serif(17f); lblStore.ForeColor = Ui.Ink; lblStore.Padding = new Padding(Ui.S(8), 0, 0, 0);
            Label tagline = SidebarCaption("T H E   B U S I N E S S   E D I T");
            tagline.Height = Ui.S(42); tagline.ForeColor = Ui.Muted;

            Pos = new PosPanel(this);
            Overview = new OverviewPanel(this);
            Inventory = new InventoryPanel(this);
            Sales = new SalesPanel(this);
            Labels = new LabelsPanel(this);
            SettingsView = new SettingsPanel(this);
            UsersView = new UsersPanel(this);
            tabOverview = Page("Overview", Overview, DateTime.Today.ToString("dddd, MMMM d", CultureInfo.InvariantCulture),
                "Your boutique, at a glance.", "Here is how things are looking today.");
            tabSell = Page("Point of sale", Pos, "The register", "Point of sale", "Scan a barcode or find an item to start checkout.");
            tabInventory = Page("Inventory", Inventory, "Your collection", "Inventory", "Stock levels, prices and product details.");
            tabSales = Page("Sales history", Sales, "The business edit", "Sales history", "Review transactions, reprint receipts and manage refunds.");
            tabLabels = Page("Barcode labels", Labels, "Ready for the rails", "Barcode labels", "Prepare product labels and preview them before printing.");
            tabSettings = Page("Settings", SettingsView, "Make it yours", "Settings & devices", "Manage your store, printers, scanner and database connection.");
            tabUsers = Page("Users & roles", UsersView, "Your team", "Users & roles", "Staff accounts, roles and what each role is allowed to do.");

            tabs = new PageDeck();
            tabs.Dock = DockStyle.Fill;
            tabs.TabPages.AddRange(new TabPage[] { tabOverview, tabSell, tabInventory, tabSales, tabLabels, tabSettings, tabUsers });

            // Glyphs from Segoe Fluent Icons / Segoe MDL2 Assets (built into Windows 10 and 11).
            string[] glyphs = { "", "", "", "", "", "", "" };
            string[] hints = { "", "F1", "F2", "F3", "F4", "", "" };
            List<Control> stack = new List<Control>();
            stack.Add(lblStore); stack.Add(tagline); stack.Add(SidebarCaption("WORKSPACE"));
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                if (i == 5)
                {
                    Label second = SidebarCaption("YOUR BOUTIQUE");
                    second.Height = Ui.S(48); second.Padding = new Padding(Ui.S(12), Ui.S(24), 0, 0);
                    stack.Add(second);
                }
                int index = i;
                NavButton button = new NavButton();
                button.Text = tabs.TabPages[i].Text; button.Glyph = glyphs[i]; button.Hint = hints[i];
                button.Dock = DockStyle.Top; button.Height = Ui.S(46);
                button.AccessibleName = tabs.TabPages[i].Text;
                button.Click += delegate { if (Allowed(tabs.TabPages[index])) tabs.SelectedIndex = index; };
                stack.Add(button); navigationButtons.Add(button);
            }
            for (int i = stack.Count - 1; i >= 0; i--) sidebar.Controls.Add(stack[i]);   // Dock=Top stacks in reverse

            // Signed-in person and a quiet note, pinned to the bottom of the sidebar.
            Panel who = new Panel();
            who.Dock = DockStyle.Bottom; who.Height = Ui.S(70);
            who.Paint += delegate(object sender, PaintEventArgs e) { using (Pen line = new Pen(Ui.Line)) e.Graphics.DrawLine(line, 0, 0, who.Width, 0); };
            lblAvatar = new Label();
            lblAvatar.Size = new Size(Ui.S(36), Ui.S(36)); lblAvatar.Location = new Point(Ui.S(6), Ui.S(18));
            lblAvatar.TextAlign = ContentAlignment.MiddleCenter; lblAvatar.BackColor = Color.FromArgb(238, 232, 245);
            lblAvatar.ForeColor = Ui.Accent; lblAvatar.Font = new Font("Segoe UI Semibold", 11f); lblAvatar.UseMnemonic = false;
            lblUser = new Label();
            lblUser.Location = new Point(Ui.S(52), Ui.S(16)); lblUser.Size = new Size(Ui.S(150), Ui.S(20));
            lblUser.Font = new Font("Segoe UI Semibold", 9.5f); lblUser.ForeColor = Ui.Ink; lblUser.AutoEllipsis = true; lblUser.UseMnemonic = false;
            lblRole = new Label();
            lblRole.Location = new Point(Ui.S(52), Ui.S(36)); lblRole.Size = new Size(Ui.S(82), Ui.S(18));
            lblRole.Font = new Font("Segoe UI", 8.5f); lblRole.ForeColor = Ui.Muted; lblRole.AutoEllipsis = true; lblRole.UseMnemonic = false;
            LinkLabel signOut = new LinkLabel();
            signOut.Text = "Sign out"; signOut.AutoSize = true; signOut.Location = new Point(Ui.S(138), Ui.S(36));
            signOut.Font = new Font("Segoe UI", 8.5f); signOut.LinkColor = Ui.Muted; signOut.ActiveLinkColor = Ui.Accent;
            signOut.LinkClicked += delegate { SignOut(); };
            who.Controls.Add(lblAvatar); who.Controls.Add(lblUser); who.Controls.Add(lblRole); who.Controls.Add(signOut);
            Label note = new Label();
            note.Dock = DockStyle.Bottom; note.Height = Ui.S(70); note.UseMnemonic = false;
            note.Text = "Beautiful pieces.\nA thriving boutique."; note.Font = Ui.Serif(13f);
            note.ForeColor = Color.FromArgb(133, 118, 143); note.Padding = new Padding(Ui.S(10), Ui.S(4), 0, 0);
            Label noteKicker = SidebarCaption("A LITTLE PERSPECTIVE");
            noteKicker.Dock = DockStyle.Bottom; noteKicker.ForeColor = Ui.Eyebrow;
            sidebar.Controls.Add(noteKicker);
            sidebar.Controls.Add(note);
            sidebar.Controls.Add(who);
            // The quiet note is decoration: drop it when the window is too short to fit it under the navigation.
            fitSidebarNote = delegate
            {
                int used = sidebar.Padding.Top;
                foreach (Control c in stack)
                {
                    // Count by role, not Control.Visible (which is false for everything while the window is hidden).
                    NavButton nav = c as NavButton;
                    if (nav != null && Session.Current != null && !Allowed(tabs.TabPages[navigationButtons.IndexOf(nav)])) continue;
                    used += c.Height;
                }
                int free = sidebar.Height - sidebar.Padding.Bottom - who.Height - used;
                bool room = free >= noteKicker.Height + note.Height + Ui.S(12);
                note.Visible = room; noteKicker.Visible = room;
            };
            sidebar.Resize += delegate { fitSidebarNote(); };

            // ---------------- main column ----------------
            Panel mainColumn = new Panel();
            mainColumn.Dock = DockStyle.Fill; mainColumn.BackColor = Ui.Canvas;
            mainColumn.Padding = new Padding(Ui.S(30), 0, Ui.S(30), 0);

            Panel topbar = new Panel();
            topbar.Dock = DockStyle.Top; topbar.Height = Ui.S(66); topbar.BackColor = Ui.Canvas;
            topbar.Paint += delegate(object sender, PaintEventArgs e) { using (Pen line = new Pen(Ui.Line)) e.Graphics.DrawLine(line, 0, topbar.Height - 1, topbar.Width, topbar.Height - 1); };
            lblCrumb = new Label();
            lblCrumb.Dock = DockStyle.Fill; lblCrumb.TextAlign = ContentAlignment.MiddleLeft; lblCrumb.UseMnemonic = false;
            lblCrumb.Font = new Font("Segoe UI", 9f); lblCrumb.ForeColor = Ui.Ink;
            lblScanner = new Label();
            lblScanner.Dock = DockStyle.Right; lblScanner.Width = Ui.S(440); lblScanner.AutoEllipsis = true; lblScanner.UseMnemonic = false;
            lblScanner.TextAlign = ContentAlignment.MiddleRight; lblScanner.ForeColor = Ui.Muted; lblScanner.Font = new Font("Segoe UI", 9f);
            topbar.Controls.Add(lblCrumb);
            topbar.Controls.Add(lblScanner);

            StatusStrip strip = new StatusStrip();
            strip.BackColor = Ui.Canvas; strip.SizingGrip = false;
            strip.Padding = new Padding(0, Ui.S(5), 0, Ui.S(5));
            status = new ToolStripStatusLabel("Ready");
            status.Spring = true; status.TextAlign = ContentAlignment.MiddleLeft; status.ForeColor = Ui.Ink;
            strip.Items.Add(status);
            ToolStripStatusLabel dbLabel = new ToolStripStatusLabel(
                "● " + (DataStore.Config != null ? DataStore.Config.Describe() : "Offline file"));
            dbLabel.ForeColor = Ui.Accent;
            strip.Items.Add(dbLabel);
            statusTimer = new Timer();
            statusTimer.Interval = 8000;
            statusTimer.Tick += delegate { statusTimer.Stop(); status.Text = "Ready"; status.ForeColor = Ui.Ink; };

            tabs.SelectedIndexChanged += delegate
            {
                if (!Allowed(tabs.SelectedTab)) { SelectFirstAllowed(); return; }   // role can't open this page
                UpdateNavigation(); OnTabChanged();
            };
            UpdateNavigation();

            mainColumn.Controls.Add(tabs);
            mainColumn.Controls.Add(topbar);
            mainColumn.Controls.Add(strip);
            frame.Controls.Add(mainColumn);
            frame.Controls.Add(sidebar);
            Controls.Add(frame);

            Scanner = new ScannerService(this);
            Scanner.Scanned += OnScanned;
            Application.AddMessageFilter(Scanner);

            Load += delegate { ApplySettings(); ApplySession(); };
            FormClosing += delegate { Application.RemoveMessageFilter(Scanner); Scanner.Dispose(); };
        }

        static Label SidebarCaption(string text)
        {
            Label l = new Label();
            l.Text = text; l.Dock = DockStyle.Top; l.Height = Ui.S(30); l.UseMnemonic = false;
            l.Font = new Font("Segoe UI Semibold", 7f); l.ForeColor = Color.FromArgb(140, 132, 153);
            l.Padding = new Padding(Ui.S(12), Ui.S(6), 0, 0);
            return l;
        }

        void UpdateNavigation()
        {
            for (int i = 0; i < navigationButtons.Count; i++)
            {
                navigationButtons[i].Selected = i == tabs.SelectedIndex;
                navigationButtons[i].Invalidate();
            }
            if (tabs.SelectedTab != null) lblCrumb.Text = "Workspace   /   " + tabs.SelectedTab.Text;
        }

        TabPage Page(string title, Control content, string eyebrow, string heading, string description)
        {
            TabPage p = new TabPage(title);
            p.BackColor = Ui.Canvas;
            p.Padding = new Padding(0, Ui.S(22), 0, Ui.S(8));
            p.Font = new Font("Segoe UI", 10f);
            content.Dock = DockStyle.Fill;
            p.Controls.Add(content);
            p.Controls.Add(Ui.PageHeading(eyebrow, heading, description));
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
                lblScanner.Text = "● Scanner ready  ·  keyboard / HID mode";
            }
            SettingsView.UpdateScannerStatus();
        }

        // ---------------- roles & permissions ----------------

        bool Allowed(TabPage page)
        {
            if (page == tabOverview) return true;
            if (page == tabSell) return Session.Can(Permissions.Sell);
            if (page == tabInventory) return Session.Can(Permissions.InventoryView) || Session.Can(Permissions.InventoryEdit) || Session.Can(Permissions.StockAdjust);
            if (page == tabSales) return Session.Can(Permissions.SalesView) || Session.Can(Permissions.Refund);
            if (page == tabLabels) return Session.Can(Permissions.LabelsPrint);
            if (page == tabSettings) return Session.Can(Permissions.SettingsManage);
            if (page == tabUsers) return Session.Can(Permissions.UsersManage);
            return false;
        }

        void SelectFirstAllowed()
        {
            foreach (TabPage p in tabs.TabPages)
                if (Allowed(p)) { if (tabs.SelectedTab != p) tabs.SelectedTab = p; else OnTabChanged(); return; }
        }

        /// <summary>Shows only what the signed-in person's role allows. Called after every sign-in.</summary>
        public void ApplySession()
        {
            SessionUser u = Session.Current;
            lblUser.Text = u == null ? "" : u.CashierName;
            lblRole.Text = u == null ? "" : u.RoleName;
            lblAvatar.Text = u == null || string.IsNullOrEmpty(u.CashierName) ? "" : u.CashierName.Substring(0, 1).ToUpperInvariant();
            for (int i = 0; i < navigationButtons.Count; i++) navigationButtons[i].Visible = Allowed(tabs.TabPages[i]);
            fitSidebarNote();
            Pos.ApplyPermissions();
            Inventory.ApplyPermissions();
            Sales.ApplyPermissions();
            UsersView.RefreshLists();
            // Registers open straight on the point of sale; everyone else lands on the overview.
            if (Allowed(tabSell)) { if (tabs.SelectedTab != tabSell) tabs.SelectedTab = tabSell; else OnTabChanged(); }
            else SelectFirstAllowed();
            if (tabs.SelectedTab == tabSell) Pos.FocusScan();
        }

        /// <summary>Ends the session and asks for the next sign-in (cashier change, lunch break...).</summary>
        void SignOut()
        {
            string who = Session.Current != null ? Session.Current.Username : "";
            try { DataStore.Logout(); } catch (Exception) { }
            Hide();
            using (LoginForm f = new LoginForm(DataStore.Data.Settings.StoreName, DataStore.Store.Describe, false, ""))
            {
                if (f.ShowDialog() != DialogResult.OK) { Close(); return; }
            }
            Show();
            ApplySession();
            SetStatus("Signed in as " + Session.Current.CashierName + (who.Length > 0 && who != Session.Current.Username ? " (was " + who + ")" : "") + ".");
        }

        void OnTabChanged()
        {
            // With a shared database, pick up items/stock changed on other registers.
            if (tabs.SelectedTab == tabSell || tabs.SelectedTab == tabInventory || tabs.SelectedTab == tabLabels || tabs.SelectedTab == tabOverview)
            {
                try { DataStore.RefreshProducts(tabs.SelectedTab == tabInventory); }
                catch (Exception ex) { SetStatus("Database unreachable - showing last loaded data. " + ex.Message, true); }
            }
            if (tabs.SelectedTab == tabSell) Pos.FocusScan();
            else if (tabs.SelectedTab == tabOverview) Overview.RefreshData();
            else if (tabs.SelectedTab == tabInventory) Inventory.RefreshGrid();
            else if (tabs.SelectedTab == tabSales) Sales.RefreshList();
            else if (tabs.SelectedTab == tabLabels) Labels.RefreshPreview();
            else if (tabs.SelectedTab == tabUsers) UsersView.RefreshLists();
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
            else if (Allowed(tabSell))
            {
                if (page != tabSell) tabs.SelectedTab = tabSell;   // a scan on the overview starts a sale
                Pos.HandleScan(code);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F1: if (Allowed(tabSell)) tabs.SelectedTab = tabSell; return true;
                case Keys.F2: if (Allowed(tabInventory)) tabs.SelectedTab = tabInventory; return true;
                case Keys.F3: if (Allowed(tabSales)) tabs.SelectedTab = tabSales; return true;
                case Keys.F4: if (Allowed(tabLabels)) tabs.SelectedTab = tabLabels; return true;
                case Keys.F12:
                    if (tabs.SelectedTab == tabSell && Allowed(tabSell)) { Pos.CompleteSale(); return true; }
                    break;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void SetStatus(string text) { SetStatus(text, false); }

        public void SetStatus(string text, bool warning)
        {
            status.Text = text;
            status.ForeColor = warning ? Color.FromArgb(170, 40, 30) : Ui.Ink;
            statusTimer.Stop(); statusTimer.Start();
        }

        public void GoToSale(Sale s)
        {
            if (!Allowed(tabSales)) { SetStatus("Your role can't open sales history.", true); return; }
            tabs.SelectedTab = tabSales;
            Sales.SelectSale(s);
        }

        /// <summary>Opens the inventory page (from the overview's stock watch).</summary>
        public void OpenInventory()
        {
            if (Allowed(tabInventory)) tabs.SelectedTab = tabInventory;
            else SetStatus("Your role can't open inventory.", true);
        }

        public void QueueLabels(IEnumerable<Product> products, bool copiesFromQuantity)
        {
            Labels.AddProducts(products, copiesFromQuantity);
            if (Allowed(tabLabels)) tabs.SelectedTab = tabLabels;
        }

        /// <summary>Call after any change to products or sales so every tab shows fresh data.</summary>
        public void DataChanged()
        {
            Inventory.RefreshGrid();
            Sales.RefreshList();
            if (tabs.SelectedTab == tabOverview) Overview.RefreshData();
        }

        public void SelectTabIndex(int i) { tabs.SelectedIndex = i; }
    }
}
