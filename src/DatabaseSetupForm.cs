using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BoutiquePOS
{
    /// <summary>
    /// Asks where data should live: the local MySQL installed with the app, a cloud server (e.g. our Forge server),
    /// another MySQL server, or an offline file. Shown on first run and from Settings.
    /// </summary>
    public class DatabaseSetupForm : Form
    {
        readonly DbConfig existing;
        RadioButton rbLocal, rbCloud, rbServer, rbOffline;
        TextBox txtHost, txtDb, txtUser, txtPass, txtApiUrl;
        NumericUpDown numPort;
        ComboBox cmbSsl;
        Label lblHelp, lblResult, lblDriver;
        TableLayoutPanel fields, apiFields, target;
        bool loading;

        public DbConfig Result { get; private set; }

        public const string LocalServiceName = "BoutiquePOS-MySQL";

        public DatabaseSetupForm(DbConfig current, bool firstRun)
        {
            existing = current;
            Text = firstRun ? "Welcome to Boutique POS - database setup" : "Database connection";
            if (Ui.AppIcon != null) Icon = Ui.AppIcon;
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = firstRun;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;
            Padding = new Padding(Ui.S(18));

            TableLayoutPanel root = new TableLayoutPanel();
            root.AutoSize = true; root.ColumnCount = 1; root.Dock = DockStyle.Fill;
            Controls.Add(root);

            Label title = new Label();
            title.Text = "Where should Boutique POS keep your inventory and sales?";
            title.Font = new Font("Segoe UI Semibold", 13f); title.ForeColor = Ui.AccentDark; title.AutoSize = true;
            title.Margin = new Padding(0, 0, 0, Ui.S(10));
            root.Controls.Add(title);

            rbLocal = Option(root, "On this computer", "Private MySQL database installed with Boutique POS (recommended for one shop).");
            rbCloud = Option(root, "Cloud - our web server (e.g. Forge)", "Registers connect securely over the internet (HTTPS) and share stock and sales. Recommended for several registers or branches.");
            rbServer = Option(root, "MySQL server (direct connection)", "Your own MySQL 8 server on this PC, the shop network, or online with SSL.");
            rbOffline = Option(root, "No database server (offline file)", "Saves to a file on this computer only. You can switch to a database later.");

            fields = FieldPanel();
            target = fields;
            txtHost = Field("Server / host");
            Ui.SetCue(txtHost, "e.g. 203.0.113.10 or db.yourdomain.com");
            numPort = new NumericUpDown(); numPort.Minimum = 1; numPort.Maximum = 65535; numPort.Width = Ui.S(100);
            Pair("Port", numPort);
            txtDb = Field("Database name");
            txtUser = Field("Username");
            txtPass = Field("Password");
            txtPass.UseSystemPasswordChar = true;
            if (existing != null && !string.IsNullOrEmpty(existing.PasswordProtected)) Ui.SetCue(txtPass, "(saved - leave blank to keep)");
            cmbSsl = new ComboBox(); cmbSsl.DropDownStyle = ComboBoxStyle.DropDownList; cmbSsl.Width = Ui.S(200);
            cmbSsl.Items.AddRange(new object[] { "REQUIRED", "PREFERRED", "VERIFY_CA", "VERIFY_IDENTITY", "DISABLED" });
            Pair("Encryption (SSL)", cmbSsl);
            root.Controls.Add(fields);

            apiFields = FieldPanel();
            target = apiFields;
            txtApiUrl = Field("Web server address");
            Ui.SetCue(txtApiUrl, "https://pos.yourdomain.com");
            root.Controls.Add(apiFields);

            lblHelp = new Label();
            lblHelp.AutoSize = true; lblHelp.MaximumSize = new Size(Ui.S(560), 0); lblHelp.ForeColor = Ui.Muted;
            lblHelp.Margin = new Padding(0, Ui.S(4), 0, Ui.S(6));
            root.Controls.Add(lblHelp);

            lblDriver = new Label(); lblDriver.AutoSize = true; lblDriver.MaximumSize = new Size(Ui.S(560), 0);
            string driver = OdbcDrivers.BestMySql();
            lblDriver.Text = driver != null ? "MySQL driver: " + driver + "  ✓" : "MySQL ODBC driver not found - re-run the Boutique POS installer to use a database server.";
            lblDriver.ForeColor = driver != null ? Ui.Good : Color.FromArgb(170, 40, 30);
            root.Controls.Add(lblDriver);

            lblResult = new Label(); lblResult.AutoSize = true; lblResult.MaximumSize = new Size(Ui.S(560), 0);
            lblResult.Margin = new Padding(0, Ui.S(8), 0, Ui.S(4));
            lblResult.Font = new Font("Segoe UI Semibold", 10f);
            root.Controls.Add(lblResult);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true; buttons.Dock = DockStyle.Fill; buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Margin = new Padding(0, Ui.S(10), 0, 0);
            Button save = Ui.MakeButton("Save and continue", delegate { OnSave(); }, true);
            Button cancel = Ui.MakeButton(firstRun ? "Exit" : "Cancel", null); cancel.DialogResult = DialogResult.Cancel;
            Button test = Ui.MakeButton("Test connection", delegate { OnTest(); });
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); buttons.Controls.Add(test);
            root.Controls.Add(buttons);
            AcceptButton = save; CancelButton = cancel;

            foreach (RadioButton rb in new RadioButton[] { rbLocal, rbCloud, rbServer, rbOffline })
                rb.CheckedChanged += delegate(object s, EventArgs e) { if (((RadioButton)s).Checked) ModeChanged(); };

            LoadInitial();
        }

        RadioButton Option(TableLayoutPanel root, string text, string detail)
        {
            RadioButton rb = new RadioButton();
            rb.Text = text; rb.AutoSize = true;
            rb.Font = new Font("Segoe UI Semibold", 10.5f);
            rb.Margin = new Padding(0, Ui.S(4), 0, 0);
            root.Controls.Add(rb);
            Label d = new Label(); d.Text = detail; d.AutoSize = true; d.ForeColor = Ui.Muted;
            d.Margin = new Padding(Ui.S(22), 0, 0, Ui.S(2));
            d.Click += delegate { rb.Checked = true; };
            root.Controls.Add(d);
            return rb;
        }

        static TableLayoutPanel FieldPanel()
        {
            TableLayoutPanel p = new TableLayoutPanel();
            p.AutoSize = true; p.ColumnCount = 2; p.Dock = DockStyle.Fill;
            p.BackColor = Ui.AccentLight;
            p.Padding = new Padding(Ui.S(12));
            p.Margin = new Padding(0, Ui.S(10), 0, Ui.S(6));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.S(330)));
            return p;
        }

        TextBox Field(string label)
        {
            TextBox tb = new TextBox(); tb.Dock = DockStyle.Fill;
            Pair(label, tb);
            return tb;
        }

        void Pair(string label, Control c)
        {
            int row = target.RowCount;
            target.RowCount = row + 1;
            target.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            target.Controls.Add(Ui.MakeLabel(label), 0, row);
            c.Margin = new Padding(Ui.S(3));
            target.Controls.Add(c, 1, row);
        }

        static bool LocalServiceInstalled()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + LocalServiceName))
                    return k != null;
            }
            catch (Exception) { return false; }
        }

        void LoadInitial()
        {
            loading = true;
            DbConfig c = existing;
            if (c != null)
            {
                if (c.IsApi) { rbCloud.Checked = true; txtApiUrl.Text = c.ApiUrl; }
                else if (!c.IsMySql) rbOffline.Checked = true;
                else if (c.Location == DbConfig.LocLocal) rbLocal.Checked = true;
                else rbServer.Checked = true;   // includes older direct-to-cloud MySQL setups
                if (c.IsMySql)
                {
                    txtHost.Text = c.Host; numPort.Value = Math.Max(1, Math.Min(65535, c.Port)); txtDb.Text = c.Database;
                    txtUser.Text = c.User; cmbSsl.SelectedItem = c.SslMode;
                }
                else Presets();
            }
            else
            {
                string choice = DbConfig.InstallerChoice();
                if (choice == "cloud") rbCloud.Checked = true;
                else if (choice == "server") rbServer.Checked = true;
                else if (choice == "offline" || OdbcDrivers.BestMySql() == null) rbOffline.Checked = true;
                else if (LocalServiceInstalled()) rbLocal.Checked = true;
                else rbOffline.Checked = true;
                Presets();
            }
            if (cmbSsl.SelectedIndex < 0) cmbSsl.SelectedItem = "PREFERRED";
            loading = false;
            UpdateHelp();
        }

        void ModeChanged()
        {
            if (loading) return;
            Presets();
            UpdateHelp();
            lblResult.Text = "";
        }

        void Presets()
        {
            if (rbLocal.Checked) { txtHost.Text = "localhost"; numPort.Value = 3307; txtDb.Text = "boutique_pos"; txtUser.Text = "boutique"; cmbSsl.SelectedItem = "PREFERRED"; }

            else if (rbServer.Checked) { txtHost.Text = "localhost"; numPort.Value = 3306; txtDb.Text = "boutique_pos"; txtUser.Text = ""; cmbSsl.SelectedItem = "PREFERRED"; }
        }

        void UpdateHelp()
        {
            fields.Visible = rbLocal.Checked || rbServer.Checked;
            apiFields.Visible = rbCloud.Checked;
            lblDriver.Visible = fields.Visible;
            if (rbLocal.Checked)
                lblHelp.Text = "Uses the private MySQL database the installer set up on this computer (service \"" + LocalServiceName +
                    "\", port 3307). The installer normally fills this in for you.";
            else if (rbCloud.Checked)
                lblHelp.Text = "Enter the web server address. Everyone then signs in with their own username and password, " +
                    "which the server checks (the first Admin is created on the server with \"php artisan pos:admin\"). " +
                    "No database port has to be opened on the internet.";
            else if (rbServer.Checked)
                lblHelp.Text = "Any MySQL 8 server you already run. The database is created automatically if the user has permission; " +
                    "otherwise create it first. For a server on the internet, keep encryption set to REQUIRED and allow only the shop's IP address.";
            else
                lblHelp.Text = "Data is saved in " + DataStore.XmlFile + ". Choose this for a single register without a database server.";
        }

        DbConfig Build()
        {
            DbConfig c = existing != null ? existing.Clone() : new DbConfig();
            if (rbOffline.Checked) { c.Mode = DbConfig.ModeXml; return c; }
            if (rbCloud.Checked)
            {
                c.Mode = DbConfig.ModeApi;
                c.Location = DbConfig.LocCloud;
                c.ApiUrl = txtApiUrl.Text.Trim().TrimEnd('/');
                return c;
            }
            c.Mode = DbConfig.ModeMySql;
            c.Location = rbLocal.Checked ? DbConfig.LocLocal : DbConfig.LocServer;
            c.Host = txtHost.Text.Trim();
            c.Port = (int)numPort.Value;
            c.Database = txtDb.Text.Trim();
            c.User = txtUser.Text.Trim();
            c.SslMode = (string)cmbSsl.SelectedItem ?? "PREFERRED";
            if (txtPass.Text.Length > 0) c.Password = txtPass.Text;
            return c;
        }

        string Validate(DbConfig c)
        {
            if (c.IsApi)
            {
                Uri u;
                if (!Uri.TryCreate(c.ApiUrl, UriKind.Absolute, out u) || (u.Scheme != "https" && u.Scheme != "http"))
                    return "Enter the web server address, e.g. https://pos.yourdomain.com";
                bool local = u.IsLoopback || u.Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase);
                if (u.Scheme != "https" && !local) return "Use an https:// address so sales and the token are encrypted on the internet.";
                return null;
            }
            if (!c.IsMySql) return null;
            if (OdbcDrivers.BestMySql() == null) return "The MySQL ODBC driver is not installed. Re-run the Boutique POS installer, or choose the offline file.";
            if (c.Host.Length == 0) return "Enter the server address.";
            if (c.Database.Length == 0) return "Enter the database name.";
            if (c.User.Length == 0) return "Enter the username.";
            return null;
        }

        void ShowResult(string text, bool ok)
        {
            lblResult.Text = text;
            lblResult.ForeColor = ok ? Ui.Good : Color.FromArgb(170, 40, 30);
        }

        void OnTest()
        {
            DbConfig c = Build();
            if (!c.IsMySql && !c.IsApi) { ShowResult("Offline mode needs no connection.", true); return; }
            string err = Validate(c);
            if (err != null) { ShowResult(err, false); return; }
            Cursor = Cursors.WaitCursor;
            try
            {
                if (c.IsApi) { ApiStore.TestConnection(c); ShowResult("Connected to the Boutique POS web server. Staff will sign in next.", true); }
                else ShowResult("Connected - MySQL " + MySqlStore.TestConnection(c), true);
            }
            catch (Exception ex) { ShowResult("Could not connect: " + ex.Message, false); }
            finally { Cursor = Cursors.Default; }
        }

        void OnSave()
        {
            DbConfig c = Build();
            string err = Validate(c);
            if (err != null) { ShowResult(err, false); return; }
            Cursor = Cursors.WaitCursor;
            try
            {
                if (c.IsApi)
                {
                    ApiStore.TestConnection(c);
                    ShowResult("Connected to the web server.", true);
                }
                else if (c.IsMySql)
                {
                    string version = MySqlStore.EnsureSchema(c);
                    ShowResult("Connected - MySQL " + version + ". Tables are ready.", true);
                    OfferMigration(c);
                }
                c.Save();
                Result = c;
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { ShowResult("Not saved: " + ex.Message, false); }
            finally { Cursor = Cursors.Default; }
        }

        /// <summary>Copies an existing offline data file into a new, empty database.</summary>
        void OfferMigration(DbConfig c)
        {
            if (!File.Exists(DataStore.XmlFile)) return;
            AppData old;
            try { old = XmlStore.ReadFile(DataStore.XmlFile); } catch (Exception) { return; }
            if (old.Products.Count == 0 && old.Sales.Count == 0) return;
            MySqlStore store = new MySqlStore(c);
            if (!store.IsEmpty()) return;
            if (MessageBox.Show(this, string.Format("This computer has offline data ({0} items, {1} sales).\n\nCopy it into the new database now?",
                old.Products.Count, old.Sales.Count), "Copy existing data", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            store.ImportAll(old);
            MessageBox.Show(this, "Your items and sales were copied into the database.", "Copy existing data", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
