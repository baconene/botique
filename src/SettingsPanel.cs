using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace BoutiquePOS
{
    public class SettingsPanel : UserControl
    {
        readonly MainForm main;
        TextBox txtStore, txtAddress, txtPhone, txtFooter, txtPrefix;
        NumericUpDown numTax;
        RadioButton rbKeyboard, rbSerial;
        ComboBox cmbPort, cmbBaud, cmbReceiptPrinter, cmbPaper;
        CheckBox chkAutoPrint;
        Label lblLastScan, lblScanSource, lblSerialStatus;
        TextBox txtScanTest;

        public SettingsPanel(MainForm main)
        {
            this.main = main;
            BackColor = Color.White;
            AutoScroll = true;
            Build();
            LoadValues();
        }

        void Build()
        {
            TableLayoutPanel cols = new TableLayoutPanel();
            cols.Dock = DockStyle.Top; cols.AutoSize = true; cols.ColumnCount = 2;
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            Controls.Add(cols);

            // ---------------- Scanner ----------------
            TableLayoutPanel scan = Group("Barcode scanner (Bluetooth / USB)");
            Label help = Hint(
                "1.  Turn the scanner on and put it in pairing mode (see its manual - usually a pairing barcode).\n" +
                "2.  Click \"Open Windows Bluetooth settings\" > Add device > Bluetooth, and pick the scanner.\n" +
                "3.  Most scanners pair as a keyboard (HID) - choose Keyboard mode below. Nothing else to set up.\n" +
                "4.  If your scanner is set to SPP / serial mode, it shows up as a COM port - choose Serial mode and pick the port (usually the \"Outgoing\" one).\n" +
                "5.  Scan any barcode - it should appear in the test box below.");
            AddFull(scan, help);
            AddFull(scan, Ui.MakeButton("Open Windows Bluetooth settings", delegate { OpenBluetooth(); }, true));

            rbKeyboard = new RadioButton(); rbKeyboard.Text = "Keyboard / HID mode  (recommended - most Bluetooth scanners)"; rbKeyboard.AutoSize = true;
            rbSerial = new RadioButton(); rbSerial.Text = "Serial / SPP mode  (Bluetooth COM port)"; rbSerial.AutoSize = true;
            rbSerial.CheckedChanged += delegate { cmbPort.Enabled = cmbBaud.Enabled = rbSerial.Checked; };
            AddFull(scan, rbKeyboard);
            AddFull(scan, rbSerial);

            FlowLayoutPanel portRow = new FlowLayoutPanel(); portRow.AutoSize = true; portRow.Dock = DockStyle.Fill;
            cmbPort = new ComboBox(); cmbPort.DropDownStyle = ComboBoxStyle.DropDownList; cmbPort.Width = Ui.S(120);
            cmbBaud = new ComboBox(); cmbBaud.DropDownStyle = ComboBoxStyle.DropDownList; cmbBaud.Width = Ui.S(100);
            cmbBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            portRow.Controls.Add(Ui.MakeLabel("COM port"));
            portRow.Controls.Add(cmbPort);
            portRow.Controls.Add(Ui.MakeButton("Refresh", delegate { LoadPorts(); }));
            portRow.Controls.Add(Ui.MakeLabel("Baud"));
            portRow.Controls.Add(cmbBaud);
            AddFull(scan, portRow);
            lblSerialStatus = Hint(""); AddFull(scan, lblSerialStatus);
            AddFull(scan, Ui.MakeButton("Apply scanner settings", delegate { SaveValues(); main.ApplySettings(); }, true));

            Label testCap = Hint("Scanner test - with this tab open, scan any barcode:");
            testCap.Font = new Font("Segoe UI Semibold", 10f);
            testCap.Margin = new Padding(Ui.S(3), Ui.S(14), Ui.S(3), Ui.S(3));
            AddFull(scan, testCap);
            lblLastScan = new Label(); lblLastScan.AutoSize = true; lblLastScan.Font = new Font("Consolas", 20f, FontStyle.Bold);
            lblLastScan.ForeColor = Ui.AccentDark; lblLastScan.Text = "(waiting for a scan)";
            AddFull(scan, lblLastScan);
            lblScanSource = Hint(""); AddFull(scan, lblScanSource);
            txtScanTest = new TextBox(); txtScanTest.Width = Ui.S(380);
            Ui.SetCue(txtScanTest, "Or click here and scan - the text should appear here");
            AddFull(scan, txtScanTest);
            cols.Controls.Add(Wrap(scan), 0, 0);

            // ---------------- Store + printers + data ----------------
            TableLayoutPanel right = new TableLayoutPanel();
            right.AutoSize = true; right.Dock = DockStyle.Fill; right.ColumnCount = 1;

            TableLayoutPanel store = Group("Store details & tax");
            txtStore = Field(store, "Store name");
            txtAddress = Field(store, "Address");
            txtPhone = Field(store, "Phone");
            numTax = new NumericUpDown(); numTax.DecimalPlaces = 3; numTax.Maximum = 30; numTax.Increment = 0.25m; numTax.Width = Ui.S(100);
            AddPair(store, "Sales tax %", numTax);
            txtPrefix = Field(store, "Barcode prefix");
            txtPrefix.MaxLength = 6;
            txtFooter = Field(store, "Receipt footer");
            txtFooter.Multiline = true; txtFooter.Height = Ui.S(60);
            right.Controls.Add(Wrap(store));

            TableLayoutPanel printers = Group("Receipt printer");
            cmbReceiptPrinter = new ComboBox(); cmbReceiptPrinter.DropDownStyle = ComboBoxStyle.DropDownList; cmbReceiptPrinter.Dock = DockStyle.Fill;
            AddPair(printers, "Printer", cmbReceiptPrinter);
            cmbPaper = new ComboBox(); cmbPaper.DropDownStyle = ComboBoxStyle.DropDownList; cmbPaper.Width = Ui.S(160);
            cmbPaper.Items.AddRange(new object[] { "80mm", "58mm" });
            AddPair(printers, "Receipt paper", cmbPaper);
            chkAutoPrint = new CheckBox(); chkAutoPrint.Text = "Print a receipt for every sale by default"; chkAutoPrint.AutoSize = true;
            AddPair(printers, "", chkAutoPrint);
            AddPair(printers, "", Hint("Label printer and label stock are chosen on the Barcode Labels tab."));
            right.Controls.Add(Wrap(printers));

            TableLayoutPanel data = Group("Database & backups");
            Label conn = Hint("Database: " + (DataStore.Config != null ? DataStore.Config.Describe() : DataStore.Store.Describe));
            conn.Font = new Font("Segoe UI Semibold", 10f);
            conn.ForeColor = Ui.AccentDark;
            AddFull(data, conn);
            Label path = Hint("Local data folder: " + DataStore.DataDir);
            path.MaximumSize = new Size(Ui.S(560), 0);
            AddFull(data, path);
            FlowLayoutPanel dataButtons = new FlowLayoutPanel(); dataButtons.AutoSize = true; dataButtons.Dock = DockStyle.Fill;
            dataButtons.Controls.Add(Ui.MakeButton("Change database connection...", delegate { ChangeDatabase(); }, true));
            dataButtons.Controls.Add(Ui.MakeButton("Open data folder", delegate { Process.Start("explorer.exe", "\"" + DataStore.DataDir + "\""); }));
            dataButtons.Controls.Add(Ui.MakeButton("Back up now", delegate
            {
                try { main.SetStatus("Backup saved: " + DataStore.BackupNow()); } catch (Exception ex) { Ui.Error(this, ex.Message); }
            }));
            AddFull(data, dataButtons);
            AddFull(data, Hint("\"Back up now\" saves a full copy of items and sales to the Backups folder. " +
                "Offline mode also keeps a copy each day; the local MySQL database is backed up nightly by a scheduled task."));
            right.Controls.Add(Wrap(data));

            Button save = Ui.MakeButton("Save settings", delegate
            {
                SaveValues();
                main.ApplySettings();
                main.Pos.UpdateTotals();
                main.SetStatus("Settings saved.");
            }, true);
            save.Font = new Font("Segoe UI Semibold", 11f);
            right.Controls.Add(save);
            cols.Controls.Add(right, 1, 0);
        }

        static TableLayoutPanel Group(string title)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true; t.Dock = DockStyle.Fill; t.ColumnCount = 2;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.Padding = new Padding(Ui.S(14));
            t.BackColor = Ui.AccentLight;
            Label h = new Label(); h.Text = title; h.AutoSize = true; h.UseMnemonic = false;
            h.Font = new Font("Segoe UI Semibold", 12f); h.ForeColor = Ui.AccentDark;
            h.Margin = new Padding(Ui.S(3), 0, Ui.S(3), Ui.S(8));
            AddFull(t, h);
            return t;
        }

        static Control Wrap(Control c)
        {
            Panel p = new Panel(); p.AutoSize = true; p.Dock = DockStyle.Fill; p.Padding = new Padding(Ui.S(6));
            p.Controls.Add(c);
            return p;
        }

        static Label Hint(string s)
        {
            Label l = new Label(); l.Text = s; l.AutoSize = true; l.ForeColor = Ui.Muted;
            l.MaximumSize = new Size(Ui.S(580), 0);
            l.Margin = new Padding(Ui.S(3), Ui.S(4), Ui.S(3), Ui.S(4));
            return l;
        }

        static void AddFull(TableLayoutPanel t, Control c)
        {
            int row = t.RowCount;
            t.RowCount = row + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(c, 0, row);
            t.SetColumnSpan(c, 2);
        }

        static void AddPair(TableLayoutPanel t, string label, Control c)
        {
            int row = t.RowCount;
            t.RowCount = row + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(Ui.MakeLabel(label), 0, row);
            c.Margin = new Padding(Ui.S(3));
            t.Controls.Add(c, 1, row);
        }

        static TextBox Field(TableLayoutPanel t, string label)
        {
            TextBox tb = new TextBox(); tb.Dock = DockStyle.Fill;
            AddPair(t, label, tb);
            return tb;
        }

        void LoadPorts()
        {
            string keep = cmbPort.SelectedItem as string ?? DataStore.Data.Settings.SerialPortName;
            cmbPort.Items.Clear();
            cmbPort.Items.AddRange(ScannerService.PortNames());
            if (!string.IsNullOrEmpty(keep) && !cmbPort.Items.Contains(keep)) cmbPort.Items.Add(keep);
            if (!string.IsNullOrEmpty(keep)) cmbPort.SelectedItem = keep;
            else if (cmbPort.Items.Count > 0) cmbPort.SelectedIndex = 0;
        }

        void LoadValues()
        {
            StoreSettings s = DataStore.Data.Settings;
            txtStore.Text = s.StoreName; txtAddress.Text = s.Address; txtPhone.Text = s.Phone;
            txtFooter.Text = (s.ReceiptFooter ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            txtPrefix.Text = s.BarcodePrefix;
            numTax.Value = Math.Max(0, Math.Min(30, s.TaxRatePercent));
            rbSerial.Checked = s.ScannerMode == "Serial";
            rbKeyboard.Checked = !rbSerial.Checked;
            cmbPort.Enabled = cmbBaud.Enabled = rbSerial.Checked;
            LoadPorts();
            cmbBaud.SelectedItem = (s.BaudRate <= 0 ? 9600 : s.BaudRate).ToString();
            if (cmbBaud.SelectedIndex < 0) cmbBaud.SelectedIndex = 0;

            cmbReceiptPrinter.Items.Clear();
            cmbReceiptPrinter.Items.Add("(Windows default printer)");
            try { foreach (string p in PrinterSettings.InstalledPrinters) cmbReceiptPrinter.Items.Add(p); } catch (Exception) { }
            int i = string.IsNullOrEmpty(s.ReceiptPrinter) ? 0 : cmbReceiptPrinter.Items.IndexOf(s.ReceiptPrinter);
            cmbReceiptPrinter.SelectedIndex = i < 0 ? 0 : i;
            cmbPaper.SelectedItem = s.ReceiptPaper == "58mm" ? "58mm" : "80mm";
            chkAutoPrint.Checked = s.AutoPrintReceipt;
        }

        void SaveValues()
        {
            StoreSettings s = DataStore.Data.Settings;
            s.StoreName = txtStore.Text.Trim().Length > 0 ? txtStore.Text.Trim() : "My Boutique";
            s.Address = txtAddress.Text.Trim();
            s.Phone = txtPhone.Text.Trim();
            s.ReceiptFooter = txtFooter.Text.Replace("\r\n", "\n").Trim();
            string prefix = txtPrefix.Text.Trim().ToUpperInvariant();
            s.BarcodePrefix = Code128.IsValid(prefix) ? prefix : "";
            s.TaxRatePercent = numTax.Value;
            s.ScannerMode = rbSerial.Checked ? "Serial" : "Keyboard";
            s.SerialPortName = cmbPort.SelectedItem as string ?? "";
            int baud;
            s.BaudRate = int.TryParse(cmbBaud.SelectedItem as string, out baud) ? baud : 9600;
            s.ReceiptPrinter = cmbReceiptPrinter.SelectedIndex <= 0 ? "" : (string)cmbReceiptPrinter.SelectedItem;
            s.ReceiptPaper = (string)cmbPaper.SelectedItem ?? "80mm";
            s.AutoPrintReceipt = chkAutoPrint.Checked;
            try { DataStore.SaveSettings(); }
            catch (Exception ex) { Ui.Error(this, "Settings could not be saved to the database:\n" + ex.Message); }
        }

        void ChangeDatabase()
        {
            using (DatabaseSetupForm f = new DatabaseSetupForm(DataStore.Config, false))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                MessageBox.Show(this, "Database connection saved. Boutique POS will now restart to use it.", "Boutique POS",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Program.Restart();
            }
        }

        void OpenBluetooth()
        {
            try { Process.Start("ms-settings:bluetooth"); }
            catch (Exception) { Process.Start("control.exe", "bthprops.cpl"); }
        }

        public void ShowScan(string code, string source)
        {
            lblLastScan.Text = code;
            lblScanSource.Text = "Received via " + source + " at " + DateTime.Now.ToString("T") + "  (" + code.Length + " characters)";
        }

        public void UpdateScannerStatus()
        {
            if (lblSerialStatus == null) return;
            StoreSettings s = DataStore.Data.Settings;
            lblSerialStatus.Text = s.ScannerMode == "Serial"
                ? "Serial status: " + main.Scanner.SerialStatus
                : "Keyboard mode active - scans are detected automatically on every tab.";
        }
    }
}
