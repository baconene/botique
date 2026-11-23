using System;
using System.Drawing;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>
    /// Staff sign-in. Local modes check the staff table/file; web-server mode sends the credentials to
    /// POST /api/v1/auth/login and the server decides access.
    /// </summary>
    public class LoginForm : Form
    {
        readonly TextBox txtUser, txtPass;
        readonly Label lblError;
        readonly Button btnSignIn;
        public SessionUser Result { get; private set; }

        public LoginForm(string storeName, string connection, bool allowCancel, string presetUser)
        {
            Text = "Sign in - Boutique POS";
            Font = new Font("Segoe UI", 10.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;

            Panel band = new Panel();
            band.Dock = DockStyle.Top; band.Height = Ui.S(70); band.BackColor = Ui.Accent;
            Label title = new Label();
            title.Text = string.IsNullOrEmpty(storeName) ? "Boutique POS" : storeName;
            title.ForeColor = Color.White; title.Font = new Font("Segoe UI Semibold", 16f);
            title.AutoSize = false; title.Dock = DockStyle.Fill; title.TextAlign = ContentAlignment.MiddleCenter;
            band.Controls.Add(title);

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true; t.ColumnCount = 1; t.Dock = DockStyle.Fill;
            t.Padding = new Padding(Ui.S(28), Ui.S(18), Ui.S(28), Ui.S(18));

            t.Controls.Add(Caption("Username"));
            txtUser = new TextBox(); txtUser.Width = Ui.S(320); txtUser.Font = new Font("Segoe UI", 12f);
            txtUser.Text = presetUser ?? "";
            t.Controls.Add(txtUser);
            t.Controls.Add(Caption("Password"));
            txtPass = new TextBox(); txtPass.Width = Ui.S(320); txtPass.Font = new Font("Segoe UI", 12f); txtPass.UseSystemPasswordChar = true;
            t.Controls.Add(txtPass);

            lblError = new Label();
            lblError.AutoSize = true; lblError.MaximumSize = new Size(Ui.S(320), 0);
            lblError.ForeColor = Color.FromArgb(170, 40, 30); lblError.Margin = new Padding(0, Ui.S(8), 0, 0);
            t.Controls.Add(lblError);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true; buttons.Dock = DockStyle.Fill; buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Margin = new Padding(0, Ui.S(12), 0, 0);
            btnSignIn = Ui.MakeButton("Sign in", delegate { SignIn(); }, true);
            btnSignIn.Font = new Font("Segoe UI Semibold", 11f);
            Button cancel = Ui.MakeButton(allowCancel ? "Cancel" : "Exit", null);
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(btnSignIn); buttons.Controls.Add(cancel);
            t.Controls.Add(buttons);

            Label conn = new Label();
            conn.Text = connection; conn.AutoSize = true; conn.ForeColor = Ui.Muted; conn.MaximumSize = new Size(Ui.S(320), 0);
            conn.Margin = new Padding(0, Ui.S(12), 0, 0);
            conn.Font = new Font("Segoe UI", 8.5f);
            t.Controls.Add(conn);

            Controls.Add(t);
            Controls.Add(band);
            AcceptButton = btnSignIn; CancelButton = cancel;
            Shown += delegate { if (txtUser.Text.Length > 0) txtPass.Focus(); else txtUser.Focus(); };
        }

        static Label Caption(string s)
        {
            Label l = new Label(); l.Text = s; l.AutoSize = true; l.ForeColor = Ui.AccentDark;
            l.Font = new Font("Segoe UI Semibold", 10f); l.Margin = new Padding(0, Ui.S(8), 0, Ui.S(2));
            return l;
        }

        void SignIn()
        {
            if (txtUser.Text.Trim().Length == 0 || txtPass.Text.Length == 0) { lblError.Text = "Enter your username and password."; return; }
            Cursor = Cursors.WaitCursor;
            btnSignIn.Enabled = false;
            try
            {
                Result = DataStore.Login(txtUser.Text, txtPass.Text);
                DialogResult = DialogResult.OK;
            }
            catch (ApiUnavailableException ex) { lblError.Text = ex.Message; }
            catch (InvalidOperationException ex) { lblError.Text = ex.Message; txtPass.SelectAll(); txtPass.Focus(); }
            catch (PermissionDeniedException ex) { lblError.Text = ex.Message; }
            finally { Cursor = Cursors.Default; btnSignIn.Enabled = true; }
        }
    }

    /// <summary>First run on a local store with no accounts: create the Admin who will manage everyone else.</summary>
    public class FirstAdminForm : Form
    {
        readonly TextBox txtUser, txtName, txtPass, txtPass2;
        readonly Label lblError;

        public FirstAdminForm()
        {
            Text = "Create the Admin account";
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;
            Padding = new Padding(Ui.S(20));

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true; t.ColumnCount = 2; t.Dock = DockStyle.Fill;
            Label intro = new Label();
            intro.Text = "Welcome! Create the Admin account first. The Admin can add staff, assign roles " +
                "(Manager, Cashier, Stock clerk or your own) and decide what each role may do.";
            intro.AutoSize = true; intro.MaximumSize = new Size(Ui.S(440), 0); intro.Margin = new Padding(0, 0, 0, Ui.S(12));
            t.Controls.Add(intro); t.SetColumnSpan(intro, 2);

            txtUser = Row(t, "Username");
            txtName = Row(t, "Your name");
            txtPass = Row(t, "Password"); txtPass.UseSystemPasswordChar = true;
            txtPass2 = Row(t, "Confirm password"); txtPass2.UseSystemPasswordChar = true;
            Ui.SetCue(txtUser, "e.g. owner");

            lblError = new Label(); lblError.AutoSize = true; lblError.ForeColor = Color.FromArgb(170, 40, 30);
            lblError.MaximumSize = new Size(Ui.S(440), 0);
            t.Controls.Add(lblError); t.SetColumnSpan(lblError, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true; buttons.Dock = DockStyle.Fill; buttons.FlowDirection = FlowDirection.RightToLeft;
            Button ok = Ui.MakeButton("Create Admin", delegate { Create(); }, true);
            Button cancel = Ui.MakeButton("Exit", null); cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
            t.Controls.Add(buttons); t.SetColumnSpan(buttons, 2);
            Controls.Add(t);
            AcceptButton = ok; CancelButton = cancel;
        }

        static TextBox Row(TableLayoutPanel t, string label)
        {
            t.Controls.Add(Ui.MakeLabel(label));
            TextBox tb = new TextBox(); tb.Width = Ui.S(260); tb.Margin = new Padding(Ui.S(3));
            t.Controls.Add(tb);
            return tb;
        }

        void Create()
        {
            if (txtPass.Text != txtPass2.Text) { lblError.Text = "The passwords don't match."; return; }
            try
            {
                DataStore.Store.CreateFirstAdmin(txtUser.Text, txtName.Text.Trim(), txtPass.Text);
                DialogResult = DialogResult.OK;
            }
            catch (InvalidOperationException ex) { lblError.Text = ex.Message; }
        }

        public string Username { get { return StaffUser.Normalize(txtUser.Text); } }
    }
}
