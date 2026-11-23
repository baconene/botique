using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace BoutiquePOS
{
    /// <summary>
    /// The only API the UI uses for data. Each write goes to the store first (so a failed database
    /// write changes nothing on screen), then updates the in-memory cache, then commits.
    /// </summary>
    public static class DataStore
    {
        public static AppData Data;
        public static IStore Store;
        public static DbConfig Config;
        static DateTime lastRefresh = DateTime.MinValue;

        public static string DataDir { get { return AppPaths.DataDir; } }
        public static string XmlFile { get { return Path.Combine(AppPaths.DataDir, "boutique-data.xml"); } }

        public static IStore CreateStore(DbConfig cfg)
        {
            if (cfg != null && cfg.IsApi)
            {
                ApiStore.TestConnection(cfg);   // fail fast with a clear message (bad URL / revoked token)
                return new ApiStore(cfg);
            }
            if (cfg != null && cfg.IsMySql)
            {
                MySqlStore.EnsureSchema(cfg);
                return new MySqlStore(cfg);
            }
            return new XmlStore(AppPaths.DataDir, true);
        }

        public static void Open(DbConfig cfg, IStore store)
        {
            Config = cfg;
            Store = store;
            Data = store.LoadAll();
            lastRefresh = DateTime.Now;
        }

        /// <summary>Re-reads products from a shared database (other registers may have changed them).</summary>
        public static bool RefreshProducts(bool force)
        {
            if (Store == null || !Store.IsShared) return false;
            if (!force && (DateTime.Now - lastRefresh).TotalSeconds < 15) return false;
            Data.Products = Store.LoadProducts();
            lastRefresh = DateTime.Now;
            return true;
        }

        // ---------------- products ----------------

        public static Product FindProduct(string code)
        {
            code = (code ?? "").Trim();
            if (code.Length == 0) return null;
            Product p = Data.Products.FirstOrDefault(x => string.Equals((x.Barcode ?? "").Trim(), code, StringComparison.OrdinalIgnoreCase));
            if (p == null && Store.IsShared)
            {
                // Possibly added on another register since we last loaded.
                p = Store.FindProduct(code);
                if (p != null)
                {
                    Data.Products.RemoveAll(x => x.Id == p.Id);
                    Data.Products.Add(p);
                }
            }
            return p;
        }

        public static Product FindById(string id)
        {
            return Data.Products.FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// Saves a product. For an edit, pass the cached original as <paramref name="original"/> and an edited
        /// copy as <paramref name="work"/>; the original is only updated after the store accepted the change.
        /// </summary>
        public static void SaveProduct(Product work, Product original)
        {
            bool isNew = original == null;
            int delta = isNew ? work.Quantity : work.Quantity - original.Quantity;
            Store.SaveProduct(work, isNew, delta);
            if (isNew) Data.Products.Add(work);
            else original.CopyFrom(work);
            Store.Commit();
        }

        /// <summary>Bulk add/update used by CSV import (each item is written individually).</summary>
        public static void UpsertProduct(Product p, bool isNew, int quantityDelta)
        {
            Store.SaveProduct(p, isNew, quantityDelta);
            if (isNew) Data.Products.Add(p);
        }

        public static void DeleteProducts(IEnumerable<Product> products)
        {
            foreach (Product p in products.ToList())
            {
                Store.DeleteProduct(p);
                Data.Products.Remove(p);
            }
            Store.Commit();
        }

        public static void ChangeQuantity(Product p, int delta, string reason)
        {
            int q = Store.ChangeQuantity(p, delta, reason);
            p.Quantity = q;
            p.Updated = DateTime.Now;
            Store.Commit();
        }

        public static void SetQuantity(Product p, int quantity, string reason)
        {
            ChangeQuantity(p, quantity - p.Quantity, reason);
        }

        public static string NextBarcode()
        {
            ApiStore api = Store as ApiStore;
            if (api != null) return api.NextBarcode();   // the server owns numbering and the prefix
            string prefix = (Data.Settings.BarcodePrefix ?? "").Trim().ToUpperInvariant();
            string code;
            do { code = prefix + Store.NextNumber("item").ToString("D6"); } while (FindProduct(code) != null);
            Store.Commit();
            return code;
        }

        public static List<string> Categories()
        {
            return Data.Products.Select(p => (p.Category ?? "").Trim()).Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
        }

        // ---------------- sales ----------------

        public static int TakeSaleNumber() { return Store.NextNumber("sale"); }

        public static string SaleCode(int number) { return "R" + number.ToString("D6"); }

        public static void RecordSale(Sale s)
        {
            Dictionary<string, int> q = Store.InsertSale(s);
            ApplyQuantities(q);
            if (!Store.IsShared) Data.Sales.Add(s);
            Store.Commit();
        }

        public static void RefundSale(Sale s)
        {
            DateTime when = DateTime.Now;
            Dictionary<string, int> q = Store.RefundSale(s, when);
            s.Refunded = true;
            s.RefundDate = when;
            ApplyQuantities(q);
            Store.Commit();
        }

        static void ApplyQuantities(Dictionary<string, int> q)
        {
            foreach (KeyValuePair<string, int> kv in q)
            {
                Product p = FindById(kv.Key);
                if (p != null) { p.Quantity = kv.Value; p.Updated = DateTime.Now; }
            }
        }

        public static List<Sale> SalesBetween(DateTime from, DateTime toExclusive) { return Store.SalesBetween(from, toExclusive); }

        public static DateTime? FirstSaleDate() { return Store.FirstSaleDate(); }

        public static Sale FindSaleByCode(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            int n;
            if (code.Length < 2 || code[0] != 'R' || !int.TryParse(code.Substring(1), out n)) return null;
            return Store.FindSale(n);
        }

        // ---------------- settings / backup ----------------

        public static void SaveSettings()
        {
            Store.SaveSettings(Data.Settings);
            Store.Commit();
        }

        /// <summary>Writes a full XML snapshot (all products and sales) to the Backups folder.</summary>
        public static string BackupNow()
        {
            Directory.CreateDirectory(AppPaths.BackupDir);
            string f = Path.Combine(AppPaths.BackupDir, "manual-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".xml");
            AppData all = Store.ExportAll();
            XmlSerializer ser = new XmlSerializer(typeof(AppData));
            using (FileStream fs = new FileStream(f, FileMode.Create)) ser.Serialize(fs, all);
            return f;
        }
    }
}
