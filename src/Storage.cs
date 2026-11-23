using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace BoutiquePOS
{
    /// <summary>
    /// Persistence back-end. The DataStore facade calls a write method FIRST (MySQL writes to the
    /// database here), then updates its in-memory cache, then calls Commit() (the XML store saves its file there).
    /// </summary>
    public interface IStore
    {
        string Describe { get; }
        /// <summary>True when several registers may share the data (MySQL).</summary>
        bool IsShared { get; }
        AppData LoadAll();
        List<Product> LoadProducts();
        Product FindProduct(string barcode);
        void SaveProduct(Product p, bool isNew, int quantityDelta);
        void DeleteProduct(Product p);
        /// <summary>Returns the new quantity on hand.</summary>
        int ChangeQuantity(Product p, int delta, string reason);
        /// <summary>Saves the sale and decrements stock. Returns productId -> new quantity.</summary>
        Dictionary<string, int> InsertSale(Sale s);
        /// <summary>Marks refunded and restocks. Returns productId -> new quantity.</summary>
        Dictionary<string, int> RefundSale(Sale s, DateTime when);
        void SaveSettings(StoreSettings s);
        int NextNumber(string counter);
        List<Sale> SalesBetween(DateTime from, DateTime toExclusive);
        Sale FindSale(int number);
        DateTime? FirstSaleDate();
        /// <summary>Everything, including all sales (for backups and migration).</summary>
        AppData ExportAll();
        void Commit();
    }

    // =====================================================================================
    //  XML file (single computer, offline)
    // =====================================================================================
    public class XmlStore : IStore
    {
        readonly string dir;
        readonly bool seedIfNew;
        AppData data;

        public XmlStore(string dir, bool seedIfNew) { this.dir = dir; this.seedIfNew = seedIfNew; }

        public string FilePath { get { return Path.Combine(dir, "boutique-data.xml"); } }
        public string Describe { get { return "Offline file: " + FilePath; } }
        public bool IsShared { get { return false; } }

        public static AppData ReadFile(string path)
        {
            XmlSerializer ser = new XmlSerializer(typeof(AppData));
            using (FileStream fs = File.OpenRead(path)) return (AppData)ser.Deserialize(fs);
        }

        public AppData LoadAll()
        {
            Directory.CreateDirectory(dir);
            if (File.Exists(FilePath)) data = ReadFile(FilePath);
            else
            {
                data = new AppData();
                if (seedIfNew) SampleData.Seed(data);
            }
            if (data.Settings == null) data.Settings = new StoreSettings();
            if (data.Products == null) data.Products = new List<Product>();
            if (data.Sales == null) data.Sales = new List<Sale>();
            if (data.NextSaleNumber < 1) data.NextSaleNumber = 1001;
            if (data.NextItemNumber < 1) data.NextItemNumber = 1;
            Commit();
            DailyBackup();
            return data;
        }

        public List<Product> LoadProducts() { return data.Products; }
        public Product FindProduct(string barcode) { return null; } // the cache is authoritative
        public void SaveProduct(Product p, bool isNew, int quantityDelta) { }
        public void DeleteProduct(Product p) { }
        public int ChangeQuantity(Product p, int delta, string reason) { return p.Quantity + delta; }

        public Dictionary<string, int> InsertSale(Sale s)
        {
            return NewQuantities(s, -1);
        }

        public Dictionary<string, int> RefundSale(Sale s, DateTime when)
        {
            if (s.Refunded) throw new InvalidOperationException("Sale " + s.Code + " was already refunded.");
            return NewQuantities(s, +1);
        }

        Dictionary<string, int> NewQuantities(Sale s, int sign)
        {
            Dictionary<string, int> q = new Dictionary<string, int>();
            foreach (SaleLine l in s.Lines)
            {
                Product p = data.Products.FirstOrDefault(x => x.Id == l.ProductId);
                if (p == null) continue;
                int cur = q.ContainsKey(p.Id) ? q[p.Id] : p.Quantity;
                q[p.Id] = cur + sign * l.Quantity;
            }
            return q;
        }

        public void SaveSettings(StoreSettings s) { }

        public int NextNumber(string counter)
        {
            if (counter == "sale") return data.NextSaleNumber++;
            return data.NextItemNumber++;
        }

        public List<Sale> SalesBetween(DateTime from, DateTime toExclusive)
        {
            return data.Sales.Where(s => s.Date >= from && s.Date < toExclusive).OrderByDescending(s => s.Date).ToList();
        }

        public Sale FindSale(int number) { return data.Sales.FirstOrDefault(s => s.Number == number); }

        public DateTime? FirstSaleDate()
        {
            if (data.Sales.Count == 0) return null;
            return data.Sales.Min(s => s.Date);
        }

        public AppData ExportAll() { return data; }

        public void Commit()
        {
            string tmp = FilePath + ".tmp";
            XmlSerializer ser = new XmlSerializer(typeof(AppData));
            using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write)) ser.Serialize(fs, data);
            if (!File.Exists(FilePath)) { File.Move(tmp, FilePath); return; }
            try { File.Replace(tmp, FilePath, FilePath + ".bak", true); }
            catch (Exception)
            {
                // OneDrive or antivirus can block File.Replace; fall back to copy.
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
        }

        void DailyBackup()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.BackupDir);
                string f = Path.Combine(AppPaths.BackupDir, "boutique-data-" + DateTime.Now.ToString("yyyyMMdd") + ".xml");
                if (!File.Exists(f) && File.Exists(FilePath)) File.Copy(FilePath, f);
                string[] all = Directory.GetFiles(AppPaths.BackupDir, "boutique-data-*.xml");
                Array.Sort(all);
                for (int i = 0; i < all.Length - 90; i++) File.Delete(all[i]);
            }
            catch (Exception) { /* best effort */ }
        }
    }

    // =====================================================================================
    //  MySQL through ODBC (local instance, cloud / Forge server, or any MySQL 8 server)
    // =====================================================================================
    public class MySqlStore : IStore
    {
        readonly DbConfig cfg;
        readonly string cs;
        static readonly string Terminal = Environment.MachineName;

        /// <summary>Settings shared by every register; everything else stays on this PC.</summary>
        public static readonly string[] SharedSettings = { "StoreName", "Address", "Phone", "ReceiptFooter", "TaxRatePercent", "BarcodePrefix" };

        public MySqlStore(DbConfig cfg)
        {
            this.cfg = cfg;
            cs = cfg.ConnectionString(true);
        }

        public string Describe { get { return cfg.Describe(); } }
        public bool IsShared { get { return true; } }

        // ---------------- schema ----------------

        /// <summary>Append new versions; never edit a version that has shipped.</summary>
        static readonly string[][] Migrations =
        {
            new string[] // v1 - initial schema
            {
                @"CREATE TABLE IF NOT EXISTS products (
                    id CHAR(32) NOT NULL PRIMARY KEY,
                    barcode VARCHAR(64) NOT NULL,
                    name VARCHAR(200) NOT NULL,
                    category VARCHAR(100) NOT NULL DEFAULT '',
                    brand VARCHAR(100) NOT NULL DEFAULT '',
                    size VARCHAR(40) NOT NULL DEFAULT '',
                    color VARCHAR(60) NOT NULL DEFAULT '',
                    price DECIMAL(12,2) NOT NULL DEFAULT 0,
                    cost DECIMAL(12,2) NOT NULL DEFAULT 0,
                    quantity INT NOT NULL DEFAULT 0,
                    reorder_level INT NOT NULL DEFAULT 2,
                    notes TEXT NULL,
                    created DATETIME NOT NULL,
                    updated DATETIME NOT NULL,
                    UNIQUE KEY ux_products_barcode (barcode),
                    KEY ix_products_name (name)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS sales (
                    number INT NOT NULL PRIMARY KEY,
                    sale_date DATETIME NOT NULL,
                    subtotal DECIMAL(12,2) NOT NULL,
                    discount_percent DECIMAL(6,2) NOT NULL DEFAULT 0,
                    discount_amount DECIMAL(12,2) NOT NULL DEFAULT 0,
                    tax_rate DECIMAL(7,3) NOT NULL DEFAULT 0,
                    tax DECIMAL(12,2) NOT NULL DEFAULT 0,
                    total DECIMAL(12,2) NOT NULL,
                    payment_method VARCHAR(20) NOT NULL,
                    amount_tendered DECIMAL(12,2) NOT NULL DEFAULT 0,
                    change_due DECIMAL(12,2) NOT NULL DEFAULT 0,
                    refunded TINYINT(1) NOT NULL DEFAULT 0,
                    refund_date DATETIME NULL,
                    note VARCHAR(500) NOT NULL DEFAULT '',
                    terminal VARCHAR(64) NOT NULL DEFAULT '',
                    KEY ix_sales_date (sale_date)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS sale_lines (
                    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    sale_number INT NOT NULL,
                    line_no INT NOT NULL,
                    product_id CHAR(32) NOT NULL DEFAULT '',
                    barcode VARCHAR(64) NOT NULL DEFAULT '',
                    name VARCHAR(200) NOT NULL DEFAULT '',
                    size VARCHAR(40) NOT NULL DEFAULT '',
                    color VARCHAR(60) NOT NULL DEFAULT '',
                    unit_price DECIMAL(12,2) NOT NULL,
                    quantity INT NOT NULL,
                    KEY ix_lines_sale (sale_number),
                    KEY ix_lines_product (product_id),
                    CONSTRAINT fk_lines_sale FOREIGN KEY (sale_number) REFERENCES sales(number) ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS stock_movements (
                    id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    product_id CHAR(32) NOT NULL,
                    change_qty INT NOT NULL,
                    reason VARCHAR(40) NOT NULL,
                    reference VARCHAR(40) NOT NULL DEFAULT '',
                    created DATETIME NOT NULL,
                    terminal VARCHAR(64) NOT NULL DEFAULT '',
                    KEY ix_moves_product (product_id),
                    KEY ix_moves_created (created)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                @"CREATE TABLE IF NOT EXISTS counters (
                    name VARCHAR(32) NOT NULL PRIMARY KEY,
                    value INT NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                "INSERT IGNORE INTO counters (name, value) VALUES ('item', 0), ('sale', 1000)",
                @"CREATE TABLE IF NOT EXISTS settings (
                    name VARCHAR(64) NOT NULL PRIMARY KEY,
                    value TEXT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
            }
        };

        public static int SchemaVersion { get { return Migrations.Length; } }

        /// <summary>Creates the database (if allowed) and applies pending migrations. Returns the server version.</summary>
        public static string EnsureSchema(DbConfig cfg)
        {
            string version;
            OdbcConnection c = null;
            try
            {
                try { c = new OdbcConnection(cfg.ConnectionString(true)); c.Open(); }
                catch (OdbcException ex)
                {
                    if (c != null) c.Dispose();
                    c = null;
                    if (ex.Message.IndexOf("Unknown database", StringComparison.OrdinalIgnoreCase) < 0) throw;
                    // Database missing: create it (works on local installs; cloud users usually create it in their panel).
                    using (OdbcConnection root = new OdbcConnection(cfg.ConnectionString(false)))
                    {
                        root.Open();
                        string safe = cfg.Database.Replace("`", "");
                        Exec(root, null, "CREATE DATABASE IF NOT EXISTS `" + safe + "` CHARACTER SET utf8mb4");
                    }
                    c = new OdbcConnection(cfg.ConnectionString(true));
                    c.Open();
                }
                version = Convert.ToString(Scalar(c, null, "SELECT VERSION()"));
                Exec(c, null, "CREATE TABLE IF NOT EXISTS schema_info (version INT NOT NULL) ENGINE=InnoDB");
                object cur = Scalar(c, null, "SELECT MAX(version) FROM schema_info");
                int current = cur == null || cur is DBNull ? 0 : Convert.ToInt32(cur);
                for (int v = current; v < Migrations.Length; v++)
                {
                    foreach (string sql in Migrations[v]) Exec(c, null, sql);
                    Exec(c, null, "INSERT INTO schema_info (version) VALUES (?)", v + 1);
                }
            }
            finally { if (c != null) c.Dispose(); }
            return version;
        }

        /// <summary>Quick connectivity check for the setup wizard. Returns e.g. "8.0.36".</summary>
        public static string TestConnection(DbConfig cfg)
        {
            using (OdbcConnection c = new OdbcConnection(cfg.ConnectionString(false)))
            {
                c.Open();
                return Convert.ToString(Scalar(c, null, "SELECT VERSION()"));
            }
        }

        // ---------------- helpers ----------------

        OdbcConnection Open()
        {
            OdbcConnection c = new OdbcConnection(cs);
            c.Open();
            return c;
        }

        static OdbcParameter Param(object v)
        {
            OdbcParameter p = new OdbcParameter();
            if (v == null) { p.OdbcType = OdbcType.NVarChar; p.Value = DBNull.Value; }
            else if (v is string) { p.OdbcType = OdbcType.NVarChar; p.Value = v; p.Size = Math.Max(1, ((string)v).Length); }
            else if (v is int) { p.OdbcType = OdbcType.Int; p.Value = v; }
            else if (v is bool) { p.OdbcType = OdbcType.Int; p.Value = (bool)v ? 1 : 0; }
            else if (v is decimal) { p.OdbcType = OdbcType.Decimal; p.Precision = 18; p.Scale = 4; p.Value = v; }
            else if (v is DateTime)
            {
                DateTime d = (DateTime)v;
                p.OdbcType = OdbcType.DateTime;
                p.Value = new DateTime(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond); // DATETIME has no fractions
            }
            else { p.OdbcType = OdbcType.NVarChar; p.Value = Convert.ToString(v, CultureInfo.InvariantCulture); }
            return p;
        }

        static OdbcCommand Cmd(OdbcConnection c, OdbcTransaction tx, string sql, object[] args)
        {
            OdbcCommand cmd = new OdbcCommand(sql, c, tx);
            cmd.CommandTimeout = 30;
            foreach (object a in args) cmd.Parameters.Add(Param(a));
            return cmd;
        }

        static int Exec(OdbcConnection c, OdbcTransaction tx, string sql, params object[] args)
        {
            using (OdbcCommand cmd = Cmd(c, tx, sql, args)) return cmd.ExecuteNonQuery();
        }

        static object Scalar(OdbcConnection c, OdbcTransaction tx, string sql, params object[] args)
        {
            using (OdbcCommand cmd = Cmd(c, tx, sql, args)) return cmd.ExecuteScalar();
        }

        static string S(IDataRecord r, int i) { return r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i)); }
        static int I(IDataRecord r, int i) { return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i)); }
        static decimal D(IDataRecord r, int i) { return r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i)); }
        static DateTime T(IDataRecord r, int i) { return r.IsDBNull(i) ? DateTime.MinValue : Convert.ToDateTime(r.GetValue(i)); }

        const string ProductCols = "id, barcode, name, category, brand, size, color, price, cost, quantity, reorder_level, notes, created, updated";

        static Product ReadProduct(IDataRecord r)
        {
            Product p = new Product();
            p.Id = S(r, 0); p.Barcode = S(r, 1); p.Name = S(r, 2); p.Category = S(r, 3); p.Brand = S(r, 4);
            p.Size = S(r, 5); p.Color = S(r, 6); p.Price = D(r, 7); p.Cost = D(r, 8); p.Quantity = I(r, 9);
            p.ReorderLevel = I(r, 10); p.Notes = S(r, 11); p.Created = T(r, 12); p.Updated = T(r, 13);
            return p;
        }

        static void Move(OdbcConnection c, OdbcTransaction tx, string productId, int delta, string reason, string reference)
        {
            if (delta == 0) return;
            Exec(c, tx, "INSERT INTO stock_movements (product_id, change_qty, reason, reference, created, terminal) VALUES (?,?,?,?,?,?)",
                productId, delta, reason, reference ?? "", DateTime.Now, Terminal);
        }

        // ---------------- IStore ----------------

        public AppData LoadAll()
        {
            AppData d = new AppData();
            d.Products = LoadProducts();
            d.Settings = LoadSettings();
            d.Sales = new List<Sale>(); // sales are queried on demand (SalesBetween / FindSale)
            return d;
        }

        public List<Product> LoadProducts()
        {
            List<Product> list = new List<Product>();
            using (OdbcConnection c = Open())
            using (OdbcCommand cmd = Cmd(c, null, "SELECT " + ProductCols + " FROM products", new object[0]))
            using (OdbcDataReader r = cmd.ExecuteReader())
                while (r.Read()) list.Add(ReadProduct(r));
            return list;
        }

        public Product FindProduct(string barcode)
        {
            using (OdbcConnection c = Open())
            using (OdbcCommand cmd = Cmd(c, null, "SELECT " + ProductCols + " FROM products WHERE barcode = ?", new object[] { barcode }))
            using (OdbcDataReader r = cmd.ExecuteReader())
                return r.Read() ? ReadProduct(r) : null;
        }

        public void SaveProduct(Product p, bool isNew, int quantityDelta)
        {
            using (OdbcConnection c = Open())
            using (OdbcTransaction tx = c.BeginTransaction())
            {
                if (isNew)
                {
                    Exec(c, tx, "INSERT INTO products (" + ProductCols + ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        p.Id, p.Barcode, p.Name, p.Category, p.Brand, p.Size, p.Color, p.Price, p.Cost, p.Quantity, p.ReorderLevel,
                        p.Notes, p.Created, p.Updated);
                    Move(c, tx, p.Id, p.Quantity, "new item", "");
                }
                else
                {
                    // Quantity is applied as a change so sales made meanwhile on other registers are not lost.
                    int n = Exec(c, tx, "UPDATE products SET barcode=?, name=?, category=?, brand=?, size=?, color=?, price=?, cost=?, " +
                        "quantity = quantity + ?, reorder_level=?, notes=?, updated=? WHERE id=?",
                        p.Barcode, p.Name, p.Category, p.Brand, p.Size, p.Color, p.Price, p.Cost, quantityDelta, p.ReorderLevel, p.Notes, p.Updated, p.Id);
                    if (n == 0) throw new InvalidOperationException("This item was deleted on another register.");
                    Move(c, tx, p.Id, quantityDelta, "edit", "");
                    p.Quantity = Convert.ToInt32(Scalar(c, tx, "SELECT quantity FROM products WHERE id=?", p.Id));
                }
                tx.Commit();
            }
        }

        public void DeleteProduct(Product p)
        {
            using (OdbcConnection c = Open()) Exec(c, null, "DELETE FROM products WHERE id=?", p.Id);
        }

        public int ChangeQuantity(Product p, int delta, string reason)
        {
            using (OdbcConnection c = Open())
            using (OdbcTransaction tx = c.BeginTransaction())
            {
                Exec(c, tx, "UPDATE products SET quantity = quantity + ?, updated=? WHERE id=?", delta, DateTime.Now, p.Id);
                Move(c, tx, p.Id, delta, reason, "");
                object q = Scalar(c, tx, "SELECT quantity FROM products WHERE id=?", p.Id);
                tx.Commit();
                if (q == null || q is DBNull) throw new InvalidOperationException("This item was deleted on another register.");
                return Convert.ToInt32(q);
            }
        }

        public Dictionary<string, int> InsertSale(Sale s)
        {
            using (OdbcConnection c = Open())
            using (OdbcTransaction tx = c.BeginTransaction())
            {
                Exec(c, tx, "INSERT INTO sales (number, sale_date, subtotal, discount_percent, discount_amount, tax_rate, tax, total, " +
                    "payment_method, amount_tendered, change_due, refunded, refund_date, note, terminal) VALUES (?,?,?,?,?,?,?,?,?,?,?,0,NULL,?,?)",
                    s.Number, s.Date, s.Subtotal, s.DiscountPercent, s.DiscountAmount, s.TaxRate, s.Tax, s.Total,
                    s.PaymentMethod, s.AmountTendered, s.Change, s.Note ?? "", Terminal);
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    SaleLine l = s.Lines[i];
                    Exec(c, tx, "INSERT INTO sale_lines (sale_number, line_no, product_id, barcode, name, size, color, unit_price, quantity) VALUES (?,?,?,?,?,?,?,?,?)",
                        s.Number, i + 1, l.ProductId, l.Barcode, l.Name, l.Size, l.Color, l.UnitPrice, l.Quantity);
                }
                Dictionary<string, int> q = ApplyStock(c, tx, s, -1, "sale");
                tx.Commit();
                return q;
            }
        }

        public Dictionary<string, int> RefundSale(Sale s, DateTime when)
        {
            using (OdbcConnection c = Open())
            using (OdbcTransaction tx = c.BeginTransaction())
            {
                int n = Exec(c, tx, "UPDATE sales SET refunded=1, refund_date=? WHERE number=? AND refunded=0", when, s.Number);
                if (n == 0) throw new InvalidOperationException("Sale " + s.Code + " was already refunded (possibly on another register).");
                Dictionary<string, int> q = ApplyStock(c, tx, s, +1, "refund");
                tx.Commit();
                return q;
            }
        }

        static Dictionary<string, int> ApplyStock(OdbcConnection c, OdbcTransaction tx, Sale s, int sign, string reason)
        {
            Dictionary<string, int> q = new Dictionary<string, int>();
            foreach (var g in s.Lines.Where(l => !string.IsNullOrEmpty(l.ProductId)).GroupBy(l => l.ProductId))
            {
                int delta = sign * g.Sum(l => l.Quantity);
                Exec(c, tx, "UPDATE products SET quantity = quantity + ?, updated=? WHERE id=?", delta, DateTime.Now, g.Key);
                Move(c, tx, g.Key, delta, reason, s.Code);
                object v = Scalar(c, tx, "SELECT quantity FROM products WHERE id=?", g.Key);
                if (v != null && !(v is DBNull)) q[g.Key] = Convert.ToInt32(v);
            }
            return q;
        }

        public int NextNumber(string counter)
        {
            using (OdbcConnection c = Open())
            {
                int n = Exec(c, null, "UPDATE counters SET value = LAST_INSERT_ID(value + 1) WHERE name = ?", counter);
                if (n == 0) throw new InvalidOperationException("Counter '" + counter + "' is missing from the database.");
                return Convert.ToInt32(Scalar(c, null, "SELECT LAST_INSERT_ID()"));
            }
        }

        const string SaleCols = "number, sale_date, subtotal, discount_percent, discount_amount, tax_rate, tax, total, payment_method, amount_tendered, change_due, refunded, refund_date, note";

        List<Sale> QuerySales(string where, object[] args)
        {
            Dictionary<int, Sale> map = new Dictionary<int, Sale>();
            List<Sale> list = new List<Sale>();
            using (OdbcConnection c = Open())
            {
                using (OdbcCommand cmd = Cmd(c, null, "SELECT " + SaleCols + " FROM sales WHERE " + where + " ORDER BY sale_date DESC", args))
                using (OdbcDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        Sale s = new Sale();
                        s.Number = I(r, 0); s.Date = T(r, 1); s.Subtotal = D(r, 2); s.DiscountPercent = D(r, 3); s.DiscountAmount = D(r, 4);
                        s.TaxRate = D(r, 5); s.Tax = D(r, 6); s.Total = D(r, 7); s.PaymentMethod = S(r, 8); s.AmountTendered = D(r, 9);
                        s.Change = D(r, 10); s.Refunded = I(r, 11) != 0; s.RefundDate = r.IsDBNull(12) ? (DateTime?)null : T(r, 12); s.Note = S(r, 13);
                        map[s.Number] = s;
                        list.Add(s);
                    }
                }
                if (list.Count == 0) return list;
                using (OdbcCommand cmd = Cmd(c, null,
                    "SELECT sale_number, product_id, barcode, name, size, color, unit_price, quantity FROM sale_lines " +
                    "WHERE sale_number IN (SELECT number FROM sales WHERE " + where + ") ORDER BY sale_number, line_no", args))
                using (OdbcDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        Sale s;
                        if (!map.TryGetValue(I(r, 0), out s)) continue;
                        SaleLine l = new SaleLine();
                        l.ProductId = S(r, 1); l.Barcode = S(r, 2); l.Name = S(r, 3); l.Size = S(r, 4); l.Color = S(r, 5);
                        l.UnitPrice = D(r, 6); l.Quantity = I(r, 7);
                        s.Lines.Add(l);
                    }
                }
            }
            return list;
        }

        public List<Sale> SalesBetween(DateTime from, DateTime toExclusive)
        {
            return QuerySales("sale_date >= ? AND sale_date < ?", new object[] { from, toExclusive });
        }

        public Sale FindSale(int number)
        {
            return QuerySales("number = ?", new object[] { number }).FirstOrDefault();
        }

        public DateTime? FirstSaleDate()
        {
            using (OdbcConnection c = Open())
            {
                object v = Scalar(c, null, "SELECT MIN(sale_date) FROM sales");
                return v == null || v is DBNull ? (DateTime?)null : Convert.ToDateTime(v);
            }
        }

        // ---------------- settings ----------------

        static string DeviceSettingsPath { get { return Path.Combine(AppPaths.DataDir, "device-settings.xml"); } }

        /// <summary>This PC's settings (scanner, printers, label stock) - shared by MySqlStore and ApiStore.</summary>
        internal static StoreSettings LoadDeviceSettings()
        {
            try
            {
                if (File.Exists(DeviceSettingsPath))
                {
                    XmlSerializer ser = new XmlSerializer(typeof(StoreSettings));
                    using (FileStream fs = File.OpenRead(DeviceSettingsPath)) return (StoreSettings)ser.Deserialize(fs);
                }
            }
            catch (Exception) { }
            return new StoreSettings();
        }

        internal static void SaveDeviceSettings(StoreSettings s)
        {
            XmlSerializer ser = new XmlSerializer(typeof(StoreSettings));
            using (FileStream fs = new FileStream(DeviceSettingsPath, FileMode.Create)) ser.Serialize(fs, s);
        }

        /// <summary>Copies shared values (store name, tax...) onto the settings object.</summary>
        internal static void ApplyShared(StoreSettings s, IDictionary<string, string> shared)
        {
            foreach (string name in SharedSettings)
            {
                string v;
                if (!shared.TryGetValue(name, out v)) continue;
                PropertyInfo pi = typeof(StoreSettings).GetProperty(name);
                try { pi.SetValue(s, Convert.ChangeType(v, pi.PropertyType, CultureInfo.InvariantCulture), null); }
                catch (Exception) { }
            }
        }

        internal static string SharedValue(StoreSettings s, string name)
        {
            return Convert.ToString(typeof(StoreSettings).GetProperty(name).GetValue(s, null), CultureInfo.InvariantCulture);
        }

        StoreSettings LoadSettings()
        {
            StoreSettings s = LoadDeviceSettings();
            Dictionary<string, string> shared = new Dictionary<string, string>();
            using (OdbcConnection c = Open())
            using (OdbcCommand cmd = Cmd(c, null, "SELECT name, value FROM settings", new object[0]))
            using (OdbcDataReader r = cmd.ExecuteReader())
                while (r.Read()) shared[S(r, 0)] = S(r, 1);

            if (shared.Count == 0) { SaveSharedSettings(s); return s; } // first register seeds the shared values
            ApplyShared(s, shared);
            return s;
        }

        void SaveSharedSettings(StoreSettings s)
        {
            using (OdbcConnection c = Open())
            using (OdbcTransaction tx = c.BeginTransaction())
            {
                foreach (string name in SharedSettings)
                {
                    object v = typeof(StoreSettings).GetProperty(name).GetValue(s, null);
                    Exec(c, tx, "INSERT INTO settings (name, value) VALUES (?, ?) ON DUPLICATE KEY UPDATE value = VALUES(value)",
                        name, Convert.ToString(v, CultureInfo.InvariantCulture));
                }
                tx.Commit();
            }
        }

        public void SaveSettings(StoreSettings s)
        {
            SaveSharedSettings(s);
            SaveDeviceSettings(s);
        }

        // ---------------- backup / migration ----------------

        public AppData ExportAll()
        {
            AppData d = LoadAll();
            d.Sales = QuerySales("1=1", new object[0]);
            d.Sales.Reverse();
            return d;
        }

        public bool IsEmpty()
        {
            using (OdbcConnection c = Open())
                return Convert.ToInt32(Scalar(c, null, "SELECT (SELECT COUNT(*) FROM products) + (SELECT COUNT(*) FROM sales)")) == 0;
        }

        /// <summary>Copies an offline data file into an EMPTY database (one transaction).</summary>
        public void ImportAll(AppData src)
        {
            if (!IsEmpty()) throw new InvalidOperationException("The database already contains data; import is only allowed into an empty database.");
            using (OdbcConnection c = Open())
            using (OdbcTransaction tx = c.BeginTransaction())
            {
                foreach (Product p in src.Products)
                    Exec(c, tx, "INSERT INTO products (" + ProductCols + ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        p.Id, p.Barcode, p.Name, p.Category ?? "", p.Brand ?? "", p.Size ?? "", p.Color ?? "", p.Price, p.Cost, p.Quantity,
                        p.ReorderLevel, p.Notes ?? "", p.Created, p.Updated);
                foreach (Sale s in src.Sales)
                {
                    Exec(c, tx, "INSERT INTO sales (number, sale_date, subtotal, discount_percent, discount_amount, tax_rate, tax, total, " +
                        "payment_method, amount_tendered, change_due, refunded, refund_date, note, terminal) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        s.Number, s.Date, s.Subtotal, s.DiscountPercent, s.DiscountAmount, s.TaxRate, s.Tax, s.Total, s.PaymentMethod,
                        s.AmountTendered, s.Change, s.Refunded, s.RefundDate.HasValue ? (object)s.RefundDate.Value : null, s.Note ?? "", "import");
                    for (int i = 0; i < s.Lines.Count; i++)
                    {
                        SaleLine l = s.Lines[i];
                        Exec(c, tx, "INSERT INTO sale_lines (sale_number, line_no, product_id, barcode, name, size, color, unit_price, quantity) VALUES (?,?,?,?,?,?,?,?,?)",
                            s.Number, i + 1, l.ProductId ?? "", l.Barcode ?? "", l.Name ?? "", l.Size ?? "", l.Color ?? "", l.UnitPrice, l.Quantity);
                    }
                }
                Exec(c, tx, "UPDATE counters SET value = GREATEST(value, ?) WHERE name = 'item'", src.NextItemNumber - 1);
                Exec(c, tx, "UPDATE counters SET value = GREATEST(value, ?) WHERE name = 'sale'", src.NextSaleNumber - 1);
                tx.Commit();
            }
            if (src.Settings != null) SaveSettings(src.Settings);
        }

        public void Commit() { }
    }

    public static class SampleData
    {
        public static void Seed(AppData d)
        {
            Add(d, "Linen Wrap Dress", "Dresses", "Maison Lune", "S", "Sage", 89.00m, 34.00m, 4);
            Add(d, "Linen Wrap Dress", "Dresses", "Maison Lune", "M", "Sage", 89.00m, 34.00m, 6);
            Add(d, "Silk Cami Top", "Tops", "Ellery & Co", "M", "Ivory", 54.00m, 19.50m, 8);
            Add(d, "High-Rise Wide Leg Jeans", "Bottoms", "Denim Row", "28", "Indigo", 98.00m, 41.00m, 5);
            Add(d, "Cashmere Crew Sweater", "Knitwear", "North Loom", "L", "Camel", 148.00m, 62.00m, 2);
            Add(d, "Woven Straw Tote", "Accessories", "Sol Studio", "One Size", "Natural", 45.00m, 15.00m, 10);
        }

        static void Add(AppData d, string name, string cat, string brand, string size, string color, decimal price, decimal cost, int qty)
        {
            Product p = new Product();
            p.Name = name; p.Category = cat; p.Brand = brand; p.Size = size; p.Color = color;
            p.Price = price; p.Cost = cost; p.Quantity = qty; p.ReorderLevel = 2;
            p.Notes = "Sample item - edit or delete";
            p.Barcode = (d.Settings.BarcodePrefix ?? "") + d.NextItemNumber.ToString("D6");
            d.NextItemNumber++;
            d.Products.Add(p);
        }
    }
}
