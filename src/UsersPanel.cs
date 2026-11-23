using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>"Users & Roles" tab (permission users.manage): staff accounts and what each role may do.</summary>
    public class UsersPanel : UserControl
    {
        readonly MainForm main;
        DataGridView gridUsers, gridRoles;
        List<StaffUser> users = new List<StaffUser>();
        List<StaffRole> roles = new List<StaffRole>();

        public UsersPanel(MainForm main)
        {
            this.main = main;
            BackColor = Color.White;
            Build();
        }

        void Build()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill; root.ColumnCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            Controls.Add(root);

            gridUsers = new DataGridView();
            Ui.StyleGrid(gridUsers);
            gridUsers.ReadOnly = true; gridUsers.MultiSelect = false;
            gridUsers.Columns.Add(Ui.Col("Username", "Username", 130, true));
            gridUsers.Columns.Add(Ui.Col("Name", "DisplayName", 0, true));
            gridUsers.Columns.Add(Ui.Col("Role", "RoleName", 120, true));
            gridUsers.Columns.Add(Ui.Col("Status", "Status", 80, true));
            gridUsers.Columns.Add(Ui.Col("Last sign-in", "LastLoginText", 170, true));
            gridUsers.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) EditUser(); };
            gridUsers.CellFormatting += delegate(object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex < users.Count && !users[e.RowIndex].Active) e.CellStyle.ForeColor = Ui.Muted;
            };
            root.Controls.Add(Section("Staff accounts", gridUsers, new Control[] {
                Ui.MakeButton("+ Add user", delegate { AddUser(); }, true),
                Ui.MakeButton("Edit / reset password", delegate { EditUser(); }),
                Ui.MakeButton("Enable / disable", delegate { ToggleUser(); }),
                Ui.MakeButton("Delete", delegate { DeleteUser(); }) }), 0, 0);

            gridRoles = new DataGridView();
            Ui.StyleGrid(gridRoles);
            gridRoles.ReadOnly = true; gridRoles.MultiSelect = false;
            gridRoles.Columns.Add(Ui.Col("Role", "Name", 130, true));
            DataGridViewTextBoxColumn perms = Ui.Col("Allowed to", "Summary", 0, true);
            perms.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            gridRoles.Columns.Add(perms);
            gridRoles.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            gridRoles.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) EditRole(); };
            Panel rolesHost = Section("Roles and permissions", gridRoles, new Control[] {
                Ui.MakeButton("+ Add role", delegate { AddRole(); }, true),
                Ui.MakeButton("Edit", delegate { EditRole(); }),
                Ui.MakeButton("Delete", delegate { DeleteRole(); }) });
            rolesHost.Padding = new Padding(Ui.S(12), 0, 0, 0);
            root.Controls.Add(rolesHost, 1, 0);
        }

        static Panel Section(string title, DataGridView grid, Control[] buttons)
        {
            Panel host = new Panel(); host.Dock = DockStyle.Fill;
            Label h = new Label(); h.Text = title; h.Dock = DockStyle.Top; h.Height = Ui.S(30);
            h.Font = new Font("Segoe UI Semibold", 12f); h.ForeColor = Ui.AccentDark; h.UseMnemonic = false;
            FlowLayoutPanel bar = new FlowLayoutPanel(); bar.Dock = DockStyle.Top; bar.AutoSize = true;
            bar.Controls.AddRange(buttons);
            host.Controls.Add(grid); host.Controls.Add(bar); host.Controls.Add(h);
            return host;
        }

        public void RefreshLists()
        {
            if (!Session.Can(Permissions.UsersManage)) return;
            try
            {
                roles = DataStore.ListRoles();
                users = DataStore.ListUsers();
                StaffRules.Fill(users, roles);
            }
            catch (Exception ex) { main.SetStatus("Could not load users: " + ex.Message, true); return; }
            gridUsers.DataSource = null; gridUsers.DataSource = users;
            gridRoles.DataSource = null; gridRoles.DataSource = roles;
        }

        StaffUser CurrentUser()
        {
            return gridUsers.CurrentRow != null && gridUsers.CurrentRow.Index < users.Count ? users[gridUsers.CurrentRow.Index] : null;
        }

        StaffRole CurrentRole()
        {
            return gridRoles.CurrentRow != null && gridRoles.CurrentRow.Index < roles.Count ? roles[gridRoles.CurrentRow.Index] : null;
        }

        bool Try(Action a, string done)
        {
            try { a(); main.SetStatus(done); RefreshLists(); return true; }
            catch (Exception ex) { Ui.Error(this, ex.Message); }   // rule violations, permission, network or database errors
            RefreshLists();
            return false;
        }

        void AddUser()
        {
            using (UserDialog d = new UserDialog(null, roles))
                while (d.ShowDialog(this) == DialogResult.OK)
                    if (Try(delegate { DataStore.SaveUser(d.User, d.NewPassword); }, "User " + d.User.Username + " added.")) break;
        }

        void EditUser()
        {
            StaffUser u = CurrentUser();
            if (u == null) return;
            using (UserDialog d = new UserDialog(u.Clone(), roles))
                while (d.ShowDialog(this) == DialogResult.OK)
                    if (Try(delegate { DataStore.SaveUser(d.User, d.NewPassword); }, "User " + d.User.Username + " saved.")) break;
        }

        void ToggleUser()
        {
            StaffUser u = CurrentUser();
            if (u == null) return;
            StaffUser c = u.Clone(); c.Active = !u.Active;
            Try(delegate { DataStore.SaveUser(c, null); }, "User " + u.Username + (c.Active ? " enabled." : " disabled."));
        }

        void DeleteUser()
        {
            StaffUser u = CurrentUser();
            if (u == null || !Ui.Confirm(this, "Delete the account \"" + u.Username + "\"?\n\nTip: disabling keeps their name on past records but blocks sign-in.")) return;
            Try(delegate { DataStore.DeleteUser(u); }, "User " + u.Username + " deleted.");
        }

        void AddRole()
        {
            using (RoleDialog d = new RoleDialog(null))
                while (d.ShowDialog(this) == DialogResult.OK)
                    if (Try(delegate { DataStore.SaveRole(d.Role); }, "Role " + d.Role.Name + " added.")) break;
        }

        void EditRole()
        {
            StaffRole r = CurrentRole();
            if (r == null) return;
            if (r.IsSystem) { Ui.Error(this, "The Admin role is built in and always has every permission."); return; }
            using (RoleDialog d = new RoleDialog(r.Clone()))
                while (d.ShowDialog(this) == DialogResult.OK)
                    if (Try(delegate { DataStore.SaveRole(d.Role); }, "Role " + d.Role.Name + " saved. Changes apply the next time each person signs in.")) break;
        }

        void DeleteRole()
        {
            StaffRole r = CurrentRole();
            if (r == null || !Ui.Confirm(this, "Delete the role \"" + r.Name + "\"?")) return;
            Try(delegate { DataStore.DeleteRole(r); }, "Role " + r.Name + " deleted.");
        }
    }

    public class UserDialog : Form
    {
        readonly TextBox txtUser, txtName, txtPass, txtPass2;
        readonly ComboBox cmbRole;
        readonly CheckBox chkActive;
        readonly List<StaffRole> roles;
        public StaffUser User { get; private set; }
        public string NewPassword { get; private set; }

        public UserDialog(StaffUser existing, List<StaffRole> roles)
        {
            this.roles = roles;
            User = existing ?? new StaffUser();
            bool isNew = existing == null;
            Text = isNew ? "Add user" : "Edit user " + existing.Username;
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(Ui.S(16)); BackColor = Color.White;

            TableLayoutPanel t = new TableLayoutPanel(); t.AutoSize = true; t.ColumnCount = 2; t.Dock = DockStyle.Fill;
            txtUser = Row(t, "Username"); txtUser.Text = User.Username;
            txtName = Row(t, "Name (on receipts)"); txtName.Text = User.DisplayName;
            t.Controls.Add(Ui.MakeLabel("Role"));
            cmbRole = new ComboBox(); cmbRole.DropDownStyle = ComboBoxStyle.DropDownList; cmbRole.Width = Ui.S(260); cmbRole.Margin = new Padding(Ui.S(3));
            foreach (StaffRole r in roles) cmbRole.Items.Add(r.Name);
            int idx = roles.FindIndex(r => r.Id == User.RoleId);
            cmbRole.SelectedIndex = idx >= 0 ? idx : Math.Min(2, roles.Count - 1);   // default: Cashier
            t.Controls.Add(cmbRole);
            txtPass = Row(t, isNew ? "Password" : "New password"); txtPass.UseSystemPasswordChar = true;
            if (!isNew) Ui.SetCue(txtPass, "leave blank to keep the current one");
            txtPass2 = Row(t, "Confirm password"); txtPass2.UseSystemPasswordChar = true;
            chkActive = new CheckBox(); chkActive.Text = "Account is active (can sign in)"; chkActive.AutoSize = true; chkActive.Checked = User.Active;
            t.Controls.Add(new Label()); t.Controls.Add(chkActive);

            FlowLayoutPanel b = new FlowLayoutPanel(); b.AutoSize = true; b.Dock = DockStyle.Fill; b.FlowDirection = FlowDirection.RightToLeft;
            Button ok = Ui.MakeButton("Save", delegate { Save(); }, true);
            Button cancel = Ui.MakeButton("Cancel", null); cancel.DialogResult = DialogResult.Cancel;
            b.Controls.Add(cancel); b.Controls.Add(ok);
            t.Controls.Add(b); t.SetColumnSpan(b, 2);
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

        void Save()
        {
            if (txtPass.Text != txtPass2.Text) { Ui.Error(this, "The passwords don't match."); return; }
            User.Username = StaffUser.Normalize(txtUser.Text);
            User.DisplayName = txtName.Text.Trim();
            User.RoleId = roles[cmbRole.SelectedIndex].Id;
            User.Active = chkActive.Checked;
            NewPassword = txtPass.Text.Length > 0 ? txtPass.Text : null;
            DialogResult = DialogResult.OK;
        }
    }

    public class RoleDialog : Form
    {
        readonly TextBox txtName;
        readonly CheckedListBox list;
        public StaffRole Role { get; private set; }

        public RoleDialog(StaffRole existing)
        {
            Role = existing ?? new StaffRole();
            Text = existing == null ? "Add role" : "Edit role " + existing.Name;
            Font = new Font("Segoe UI", 10f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Padding = new Padding(Ui.S(16)); BackColor = Color.White;

            TableLayoutPanel t = new TableLayoutPanel(); t.AutoSize = true; t.ColumnCount = 1; t.Dock = DockStyle.Fill;
            t.Controls.Add(Ui.MakeLabel("Role name"));
            txtName = new TextBox(); txtName.Width = Ui.S(340); txtName.Text = Role.Name;
            t.Controls.Add(txtName);
            t.Controls.Add(Ui.MakeLabel("People in this role may:"));
            list = new CheckedListBox(); list.CheckOnClick = true; list.Width = Ui.S(340); list.Height = Ui.S(250); list.BorderStyle = BorderStyle.FixedSingle;
            List<string> current = Role.PermissionList;
            foreach (string p in Permissions.All) list.Items.Add(Permissions.Label(p), current.Contains(p));
            t.Controls.Add(list);

            FlowLayoutPanel b = new FlowLayoutPanel(); b.AutoSize = true; b.Dock = DockStyle.Fill; b.FlowDirection = FlowDirection.RightToLeft;
            Button ok = Ui.MakeButton("Save", delegate { Save(); }, true);
            Button cancel = Ui.MakeButton("Cancel", null); cancel.DialogResult = DialogResult.Cancel;
            b.Controls.Add(cancel); b.Controls.Add(ok);
            t.Controls.Add(b);
            Controls.Add(t);
            AcceptButton = ok; CancelButton = cancel;
        }

        void Save()
        {
            List<string> perms = new List<string>();
            for (int i = 0; i < list.Items.Count; i++) if (list.GetItemChecked(i)) perms.Add(Permissions.All[i]);
            Role.Name = txtName.Text.Trim();
            Role.Permissions = Permissions.ToCsv(perms);
            DialogResult = DialogResult.OK;
        }
    }
}
