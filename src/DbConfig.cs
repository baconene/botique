using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace BoutiquePOS
{
    /// <summary>Where the app keeps its writable files.</summary>
    public static class AppPaths
    {
        public static string DataDir { get; private set; }

        /// <summary>
        /// Installed copies use %ProgramData%\BoutiquePOS (Program Files is read-only).
        /// A developer build has bin\portable.txt and keeps data in bin\Data instead.
        /// </summary>
        public static void Init()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(exeDir, "portable.txt"))) UseDir(Path.Combine(exeDir, "Data"));
            else UseDir(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BoutiquePOS"));
        }

        public static void UseDir(string dir)
        {
            DataDir = dir;
            Directory.CreateDirectory(DataDir);
        }

        public static string BackupDir { get { return Path.Combine(DataDir, "Backups"); } }
    }

    /// <summary>Database connection settings, stored in &lt;DataDir&gt;\db.config.</summary>
    public class DbConfig
    {
        public const string ModeXml = "Xml", ModeMySql = "MySql", ModeApi = "Api";
        public const string LocLocal = "Local", LocCloud = "Cloud", LocServer = "Server";

        public string Mode { get; set; }        // Xml | MySql
        public string Location { get; set; }    // Local | Cloud | Server (for display / defaults)
        public string Host { get; set; }
        public int Port { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string PasswordProtected { get; set; }
        public string SslMode { get; set; }     // DISABLED | PREFERRED | REQUIRED | VERIFY_CA | VERIFY_IDENTITY
        public string Driver { get; set; }      // blank = auto-detect
        public string ApiUrl { get; set; }      // Api mode: e.g. https://pos.example.com

        public DbConfig()
        {
            Mode = ModeXml; Location = LocLocal;
            Host = "localhost"; Port = 3307; Database = "boutique_pos"; User = "boutique";
            PasswordProtected = ""; SslMode = "PREFERRED"; Driver = "";
            ApiUrl = "";
        }

        public bool IsMySql { get { return Mode == ModeMySql; } }
        public bool IsApi { get { return Mode == ModeApi; } }

        static string Protect(string value)
        {
            return string.IsNullOrEmpty(value) ? "" :
                Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.LocalMachine));
        }

        static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.LocalMachine)); }
            catch (Exception) { return ""; }
        }

        // Same algorithm is used by installer\setup-local-mysql.ps1 - keep in sync.
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BoutiquePOS.db.v1");

        [XmlIgnore]
        public string Password
        {
            get { return Unprotect(PasswordProtected); }
            set { PasswordProtected = Protect(value); }
        }

        public static string ConfigPath { get { return Path.Combine(AppPaths.DataDir, "db.config"); } }
        public static string SetupChoicePath { get { return Path.Combine(AppPaths.DataDir, "setup-choice.txt"); } }

        public static DbConfig LoadOrNull()
        {
            if (!File.Exists(ConfigPath)) return null;
            XmlSerializer ser = new XmlSerializer(typeof(DbConfig));
            using (FileStream fs = File.OpenRead(ConfigPath)) return (DbConfig)ser.Deserialize(fs);
        }

        public void Save()
        {
            XmlSerializer ser = new XmlSerializer(typeof(DbConfig));
            string tmp = ConfigPath + ".tmp";
            using (FileStream fs = new FileStream(tmp, FileMode.Create)) ser.Serialize(fs, this);
            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            File.Move(tmp, ConfigPath);
        }

        /// <summary>What the installer asked the customer ("local", "cloud", "server", "offline"), if anything.</summary>
        public static string InstallerChoice()
        {
            try { return File.Exists(SetupChoicePath) ? File.ReadAllText(SetupChoicePath).Trim().ToLowerInvariant() : ""; }
            catch (Exception) { return ""; }
        }

        public DbConfig Clone() { return (DbConfig)MemberwiseClone(); }

        static string OdbcValue(string v)
        {
            v = v ?? "";
            return "{" + v.Replace("}", "}}") + "}";
        }

        public string ConnectionString(bool includeDatabase)
        {
            string driver = string.IsNullOrEmpty(Driver) ? OdbcDrivers.BestMySql() : Driver;
            if (driver == null) throw new InvalidOperationException(
                "The MySQL ODBC driver is not installed on this computer.\nRe-run the Boutique POS installer, or install \"MySQL Connector/ODBC\" (64-bit).");
            StringBuilder sb = new StringBuilder();
            sb.Append("Driver={").Append(driver).Append("};");
            sb.Append("Server=").Append(Host).Append(";Port=").Append(Port <= 0 ? 3306 : Port).Append(";");
            if (includeDatabase) sb.Append("Database=").Append(Database).Append(";");
            sb.Append("Uid=").Append(OdbcValue(User)).Append(";Pwd=").Append(OdbcValue(Password)).Append(";");
            sb.Append("SSLMODE=").Append(string.IsNullOrEmpty(SslMode) ? "PREFERRED" : SslMode).Append(";");
            sb.Append("CHARSET=utf8mb4;NO_PROMPT=1;");
            return sb.ToString();
        }

        public string Describe()
        {
            if (IsApi) return "Web server " + ApiUrl;
            if (!IsMySql) return "Offline file on this computer";
            string where = Location == LocCloud ? "Cloud" : Location == LocLocal ? "This PC" : "Server";
            return "MySQL (" + where + ") " + Host + ":" + Port + " / " + Database;
        }
    }

    public static class OdbcDrivers
    {
        public static List<string> MySql()
        {
            List<string> found = new List<string>();
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\ODBC\ODBCINST.INI\ODBC Drivers"))
                {
                    if (k != null)
                        foreach (string name in k.GetValueNames())
                            if (name.StartsWith("MySQL ODBC", StringComparison.OrdinalIgnoreCase)) found.Add(name);
                }
            }
            catch (Exception) { }
            // Prefer Unicode drivers, then the newest version.
            return found.OrderByDescending(n => n.IndexOf("Unicode", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ThenByDescending(n => VersionOf(n)).ToList();
        }

        static Version VersionOf(string name)
        {
            Match m = Regex.Match(name, @"(\d+)\.(\d+)");
            return m.Success ? new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value)) : new Version(0, 0);
        }

        public static string BestMySql()
        {
            List<string> d = MySql();
            return d.Count > 0 ? d[0] : null;
        }
    }
}
