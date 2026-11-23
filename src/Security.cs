using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace BoutiquePOS
{
    /// <summary>
    /// RBAC permissions. MUST match web/app/Services/Pos/Permissions.php: same keys, same default roles,
    /// "*" = every permission (the built-in Admin role, id 1).
    /// </summary>
    public static class Permissions
    {
        public const string Sell = "sell";
        public const string Discount = "discount";
        public const string Refund = "refund";
        public const string SalesView = "sales.view";
        public const string InventoryView = "inventory.view";
        public const string InventoryEdit = "inventory.edit";
        public const string StockAdjust = "stock.adjust";
        public const string LabelsPrint = "labels.print";
        public const string SettingsManage = "settings.manage";
        public const string UsersManage = "users.manage";

        public const int AdminRoleId = 1;

        public static readonly string[] All = { Sell, Discount, Refund, SalesView, InventoryView, InventoryEdit, StockAdjust, LabelsPrint, SettingsManage, UsersManage };

        public static string Label(string key)
        {
            switch (key)
            {
                case Sell: return "Sell (checkout)";
                case Discount: return "Discounts and price changes";
                case Refund: return "Refunds";
                case SalesView: return "Sales history and reports";
                case InventoryView: return "View inventory";
                case InventoryEdit: return "Add, edit and delete items";
                case StockAdjust: return "Receive and adjust stock";
                case LabelsPrint: return "Print barcode labels";
                case SettingsManage: return "Store settings";
                case UsersManage: return "Users and roles";
                default: return key;
            }
        }

        public static List<string> Parse(string csv)
        {
            csv = (csv ?? "").Trim();
            if (csv == "*") return All.ToList();
            HashSet<string> set = new HashSet<string>(csv.Split(',').Select(s => s.Trim()));
            return All.Where(set.Contains).ToList();
        }

        public static string ToCsv(IEnumerable<string> permissions)
        {
            HashSet<string> set = new HashSet<string>(permissions);
            return string.Join(",", All.Where(set.Contains).ToArray());
        }

        public static List<StaffRole> DefaultRoles()
        {
            return new List<StaffRole>
            {
                StaffRole.Make(1, "Admin", "*", true),
                StaffRole.Make(2, "Manager", "sell,discount,refund,sales.view,inventory.view,inventory.edit,stock.adjust,labels.print,settings.manage", false),
                StaffRole.Make(3, "Cashier", "sell,labels.print", false),
                StaffRole.Make(4, "Stock clerk", "inventory.view,inventory.edit,stock.adjust,labels.print", false)
            };
        }
    }

    public class StaffRole
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Permissions { get; set; }   // CSV or "*"
        public bool IsSystem { get; set; }

        public StaffRole() { Name = ""; Permissions = ""; }

        public static StaffRole Make(int id, string name, string perms, bool system)
        {
            StaffRole r = new StaffRole();
            r.Id = id; r.Name = name; r.Permissions = perms; r.IsSystem = system;
            return r;
        }

        [XmlIgnore] public List<string> PermissionList { get { return BoutiquePOS.Permissions.Parse(Permissions); } }
        [XmlIgnore] public int PermissionCount { get { return PermissionList.Count; } }
        [XmlIgnore] public string Summary { get { return IsSystem ? "Everything (built in)" : string.Join(", ", PermissionList.Select(BoutiquePOS.Permissions.Label).ToArray()); } }
        public StaffRole Clone() { return (StaffRole)MemberwiseClone(); }
    }

    public class StaffUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string PasswordHash { get; set; }
        public int RoleId { get; set; }
        public bool Active { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }

        /// <summary>Filled in by stores when listing (not saved).</summary>
        [XmlIgnore] public string RoleName { get; set; }

        public StaffUser()
        {
            Username = ""; DisplayName = ""; PasswordHash = ""; RoleName = "";
            Active = true; Created = DateTime.Now; Updated = Created;
        }

        [XmlIgnore] public string Status { get { return Active ? "Active" : "Disabled"; } }
        [XmlIgnore] public string LastLoginText { get { return LastLogin.HasValue ? LastLogin.Value.ToString("g") : "never"; } }
        public StaffUser Clone() { return (StaffUser)MemberwiseClone(); }

        public static string Normalize(string username) { return (username ?? "").Trim().ToLowerInvariant(); }

        public static bool ValidUsername(string u)
        {
            if (u.Length < 3 || u.Length > 64) return false;
            foreach (char c in u)
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '.' && c != '_' && c != '-') return false;
            return true;
        }
    }

    /// <summary>Who is signed in on this register right now, and what their role allows.</summary>
    public class SessionUser
    {
        public int Id;
        public string Username = "";
        public string DisplayName = "";
        public string RoleName = "";
        public HashSet<string> Permissions = new HashSet<string>();
        public bool IsAdmin;

        public string CashierName { get { return DisplayName.Length > 0 ? DisplayName : Username; } }
    }

    public class PermissionDeniedException : Exception
    {
        public PermissionDeniedException(string message) : base(message) { }
    }

    public static class Session
    {
        public static SessionUser Current;

        public static bool Can(string permission)
        {
            return Current != null && Current.Permissions.Contains(permission);
        }

        public static void Require(string permission)
        {
            if (!Can(permission))
                throw new PermissionDeniedException("Your role does not allow this (" + Permissions.Label(permission) + ").");
        }

        /// <summary>"PC-NAME / username", recorded on stock movements and sales.</summary>
        public static string TerminalLabel
        {
            get
            {
                string s = Environment.MachineName + (Current != null ? " / " + Current.Username : "");
                return s.Length > 64 ? s.Substring(0, 64) : s;
            }
        }

        public static SessionUser FromLocal(StaffUser u, StaffRole role)
        {
            SessionUser s = new SessionUser();
            s.Id = u.Id; s.Username = u.Username; s.DisplayName = u.DisplayName;
            s.RoleName = role != null ? role.Name : "";
            s.Permissions = new HashSet<string>(role != null ? role.PermissionList : new List<string>());
            s.IsAdmin = u.RoleId == Permissions.AdminRoleId;
            return s;
        }
    }

    /// <summary>
    /// Staff password hashes, shared with the server (web/app/Services/Pos/PasswordHasher.php):
    ///   pbkdf2-sha256$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 32-byte hash&gt;
    /// </summary>
    public static class PasswordHasher
    {
        public const int Iterations = 100000;

        public static string Hash(string password)
        {
            byte[] salt = new byte[16];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            return Format(salt, Derive(password, salt, Iterations, 32), Iterations);
        }

        public static string Format(byte[] salt, byte[] hash, int iterations)
        {
            return "pbkdf2-sha256$" + iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static byte[] Derive(string password, byte[] salt, int iterations, int length)
        {
            using (Rfc2898DeriveBytes kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password ?? ""), salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(length);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? "").Split('$');
            int iterations;
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out iterations))
            {
                Derive(password, Encoding.UTF8.GetBytes("timing-equalizer"), Iterations, 32);   // same cost as a real check
                return false;
            }
            byte[] salt, expected;
            try { salt = Convert.FromBase64String(parts[2]); expected = Convert.FromBase64String(parts[3]); }
            catch (FormatException) { return false; }
            byte[] actual = Derive(password, salt, iterations, expected.Length);
            int diff = 0;
            for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];   // constant time
            return diff == 0;
        }
    }

    /// <summary>
    /// User/role rules for the local stores (offline file, MySQL). The web server enforces the same rules itself.
    /// </summary>
    public static class StaffRules
    {
        public static void CheckUser(StaffUser changed, List<StaffUser> all, List<StaffRole> roles, string newPassword)
        {
            changed.Username = StaffUser.Normalize(changed.Username);
            if (!StaffUser.ValidUsername(changed.Username))
                throw new InvalidOperationException("Username must be 3-64 characters: letters, digits, dot, dash or underscore.");
            if (all.Any(u => u.Id != changed.Id && u.Username == changed.Username))
                throw new InvalidOperationException("The username \"" + changed.Username + "\" is already taken.");
            if (!roles.Any(r => r.Id == changed.RoleId)) throw new InvalidOperationException("Choose a role.");
            if (changed.Id == 0 && string.IsNullOrEmpty(newPassword)) throw new InvalidOperationException("Enter a password.");
            if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 6) throw new InvalidOperationException("Password must be at least 6 characters.");
            if (Session.Current != null && changed.Id == Session.Current.Id && !changed.Active)
                throw new InvalidOperationException("You cannot disable your own account.");
            StaffUser before = all.FirstOrDefault(u => u.Id == changed.Id);
            bool wasAdmin = before != null && before.Active && before.RoleId == Permissions.AdminRoleId;
            bool staysAdmin = changed.Active && changed.RoleId == Permissions.AdminRoleId;
            if (wasAdmin && !staysAdmin && !all.Any(u => u.Id != changed.Id && u.Active && u.RoleId == Permissions.AdminRoleId))
                throw new InvalidOperationException("There must always be at least one active Admin.");
        }

        public static void CheckDeleteUser(StaffUser user, List<StaffUser> all)
        {
            if (Session.Current != null && user.Id == Session.Current.Id) throw new InvalidOperationException("You cannot delete your own account.");
            if (user.Active && user.RoleId == Permissions.AdminRoleId && !all.Any(u => u.Id != user.Id && u.Active && u.RoleId == Permissions.AdminRoleId))
                throw new InvalidOperationException("There must always be at least one active Admin.");
        }

        public static void CheckRole(StaffRole role, List<StaffRole> all)
        {
            role.Name = (role.Name ?? "").Trim();
            if (role.Name.Length == 0 || role.Name.Length > 64) throw new InvalidOperationException("Enter a role name (up to 64 characters).");
            StaffRole before = all.FirstOrDefault(r => r.Id == role.Id);
            if (before != null && before.IsSystem) throw new InvalidOperationException("The Admin role is built in and always has every permission.");
            if (all.Any(r => r.Id != role.Id && string.Equals(r.Name, role.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A role named \"" + role.Name + "\" already exists.");
            role.Permissions = Permissions.ToCsv(role.PermissionList);
        }

        public static void CheckDeleteRole(StaffRole role, List<StaffUser> users)
        {
            if (role.IsSystem) throw new InvalidOperationException("The Admin role cannot be deleted.");
            if (users.Any(u => u.RoleId == role.Id)) throw new InvalidOperationException("Move the users in this role to another role first.");
        }

        public static void Fill(List<StaffUser> users, List<StaffRole> roles)
        {
            foreach (StaffUser u in users)
            {
                StaffRole r = roles.FirstOrDefault(x => x.Id == u.RoleId);
                u.RoleName = r != null ? r.Name : "?";
            }
        }
    }
}
