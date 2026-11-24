using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BoutiquePOS
{
    static class Program
    {
        const string MutexName = "BoutiquePOS_SingleInstance";

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Ui.Scale = g.DpiX / 96f;

            if (args.Length >= 1 && args[0] == "--selftest")
                return SelfTest.RunOffline(args.Length >= 2 ? args[1] : Path.Combine(Path.GetTempPath(), "BoutiquePOS-selftest"));
            if (args.Length >= 1 && args[0] == "--dbtest")
                return SelfTest.RunMySql(args);
            if (args.Length >= 1 && args[0] == "--apitest")
                return SelfTest.RunApi(args);

            AppPaths.Init();
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { ShowError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { ShowError(e.ExceptionObject as Exception); };

            using (Mutex m = new Mutex(false, MutexName))
            {
                bool owned;
                try { owned = m.WaitOne(args.Contains("--restarted") ? 15000 : 0); }
                catch (AbandonedMutexException) { owned = true; }
                if (!owned)
                {
                    MessageBox.Show("Boutique POS is already open.", "Boutique POS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                try
                {
                    if (!OpenData()) return 0;
                    Application.Run(new MainForm());
                }
                finally { m.ReleaseMutex(); }
            }
            return 0;
        }

        /// <summary>Loads db.config (running the setup wizard on first run) and connects. False = user chose to exit.</summary>
        static bool OpenData()
        {
            DbConfig cfg;
            try { cfg = DbConfig.LoadOrNull(); }
            catch (Exception) { cfg = null; }
            if (cfg == null)
            {
                using (DatabaseSetupForm f = new DatabaseSetupForm(null, true))
                {
                    if (f.ShowDialog() != DialogResult.OK) return false;
                    cfg = f.Result;
                }
            }
            while (true)
            {
                try
                {
                    // 1. connect   2. first Admin (local stores only)   3. sign in   4. load data
                    DataStore.Attach(cfg, DataStore.CreateStore(cfg));
                    string preset = "";
                    if (!DataStore.Store.HasUsers())
                    {
                        using (FirstAdminForm f = new FirstAdminForm())
                        {
                            if (f.ShowDialog() != DialogResult.OK) return false;
                            preset = f.Username;
                        }
                    }
                    using (LoginForm login = new LoginForm("Boutique POS", cfg.Describe(), false, preset))
                        if (login.ShowDialog() != DialogResult.OK) return false;
                    DataStore.Load();
                    return true;
                }
                catch (Exception ex)
                {
                    DialogResult r = MessageBox.Show(
                        "Could not open your data:\n\n" + ex.Message + "\n\n" + cfg.Describe() +
                        "\n\nYes = try again     No = change the database connection     Cancel = exit",
                        "Boutique POS - database", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (r == DialogResult.Cancel) return false;
                    if (r == DialogResult.No)
                    {
                        using (DatabaseSetupForm f = new DatabaseSetupForm(cfg, true))
                        {
                            if (f.ShowDialog() != DialogResult.OK) return false;
                            cfg = f.Result;
                        }
                    }
                }
            }
        }

        /// <summary>Starts a fresh instance (which waits for this one to exit) and closes this one.</summary>
        public static void Restart()
        {
            Process.Start(Application.ExecutablePath, "--restarted");
            Application.Exit();
        }

        static void ShowError(Exception ex)
        {
            if (ex == null) return;
            try
            {
                File.AppendAllText(Path.Combine(AppPaths.DataDir ?? Path.GetTempPath(), "error.log"),
                    DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine + Environment.NewLine);
            }
            catch (Exception) { }
            MessageBox.Show("Something went wrong:\n" + ex.Message + "\n\nDetails were written to error.log in the data folder.", "Boutique POS",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Automated checks for the qa-tester / database-engineer agents.
    ///   --selftest &lt;outDir&gt;                                  offline (XML) store + UI screenshots
    ///   --dbtest &lt;host&gt; &lt;port&gt; &lt;db&gt; &lt;user&gt; &lt;password&gt; [outDir]  MySQL store (use a THROWAWAY database)
    /// </summary>
    static class SelfTest
    {
        static StringBuilder log;
        static int failures;

        static void Check(bool ok, string what)
        {
            log.AppendLine((ok ? "PASS  " : "FAIL  ") + what);
            if (!ok) failures++;
        }

        static int Finish(string outDir)
        {
            log.AppendLine(failures == 0 ? "RESULT: ALL PASSED" : "RESULT: " + failures + " FAILURE(S)");
            File.WriteAllText(Path.Combine(outDir, "report.txt"), log.ToString());
            return failures == 0 ? 0 : 2;
        }

        /// <summary>Business rules that must hold for every store type.</summary>
        static Sale CoreFlow(bool shared)
        {
            // New product + barcode numbering
            Product p = new Product();
            p.Name = "Test Blazer " + DateTime.Now.Ticks; p.Category = "Outerwear"; p.Size = "M"; p.Color = "Black";
            p.Price = 120.00m; p.Cost = 50.00m; p.Quantity = 5;
            p.Barcode = DataStore.NextBarcode();
            DataStore.SaveProduct(p, null);
            Check(DataStore.FindProduct(p.Barcode) == p, "New product saved and found by barcode " + p.Barcode);

            // Edit through a copy; quantity edits are applied as a delta
            Product work = p.Clone(); work.Price = 110.00m; work.Quantity = 7;
            DataStore.SaveProduct(work, p);
            Check(p.Price == 110.00m && p.Quantity == 7, "Edit applied (price 110, qty 7)");

            DataStore.ChangeQuantity(p, 3, "received");
            Check(p.Quantity == 10, "Receive +3 -> 10 on hand");

            // Totals
            List<SaleLine> lines = new List<SaleLine>();
            SaleLine a = SaleLine.FromProduct(p); a.Quantity = 2; lines.Add(a);          // 2 x 110.00
            decimal sub, disc, tax, total;
            SaleMath.Compute(lines, 10m, 7m, out sub, out disc, out tax, out total);
            Check(sub == 220.00m && disc == 22.00m && tax == 13.86m && total == 211.86m,
                "Totals: 220.00 - 10% (22.00) + 7% tax 13.86 = 211.86 (got " + sub + ", " + disc + ", " + tax + ", " + total + ")");

            // Sale
            Sale sale = new Sale();
            sale.Number = DataStore.TakeSaleNumber();
            sale.Lines = lines.Select(l => l.Clone()).ToList();
            sale.Subtotal = sub; sale.DiscountPercent = 10; sale.DiscountAmount = disc; sale.TaxRate = 7; sale.Tax = tax; sale.Total = total;
            sale.PaymentMethod = "Cash"; sale.AmountTendered = 250m; sale.Change = 250m - total;
            DataStore.RecordSale(sale);
            Check(p.Quantity == 8, "Sale decrements stock (10 -> 8)");
            Sale found = DataStore.FindSaleByCode(sale.Code);
            Check(found != null && found.Total == 211.86m && found.Lines.Count == 1 && found.Lines[0].Quantity == 2,
                "Sale " + sale.Code + " stored with exact money values");
            Check(DataStore.SalesBetween(DateTime.Today, DateTime.Today.AddDays(1)).Any(s => s.Number == sale.Number), "Sale listed in today's sales");

            // Refund
            DataStore.RefundSale(found);
            Check(p.Quantity == 10 && found.Refunded, "Refund restocks (8 -> 10)");
            bool blocked = false;
            try { DataStore.RefundSale(DataStore.FindSaleByCode(sale.Code)); } catch (InvalidOperationException) { blocked = true; }
            Check(blocked || !shared, "Second refund of the same sale is rejected");
            return sale;
        }

        public static int RunOffline(string outDir)
        {
            log = new StringBuilder();
            failures = 0;
            Directory.CreateDirectory(outDir);
            try
            {
                Check(Code128.TableIsConsistent(), "Code128 table: 107 symbols, 11 modules each (stop 13)");
                foreach (string s in new string[] { "BTQ000001", "Hello World", "R001001", "12345" })
                {
                    log.AppendLine("BITS  " + s + "  " + Code128.ToBitString(s));
                    Check(Code128.CountModules(s) == 11 * (s.Length + 3) + 2, "Code128 length for " + s);
                }
                Check(!Code128.IsValid("café") && Code128.IsValid("A-1 b/2"), "Code128 character validation");

                string dataDir = Path.Combine(outDir, "data");
                if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
                AppPaths.UseDir(dataDir);
                DbConfig cfg = new DbConfig();

                // --- Sign-in & first Admin ---
                string vector = PasswordHasher.Format(Encoding.UTF8.GetBytes("fixed-salt-16byt"),
                    Convert.FromBase64String("Tp1pzUOJkR3nd6S73zjMJlozvBG0iGolb7PdoeDgWhE="), 100000);
                Check(PasswordHasher.Verify("S3cret!pass", vector) && !PasswordHasher.Verify("wrong", vector),
                    "Password hash matches the server's PHP reference value (pbkdf2-sha256)");
                DataStore.Attach(cfg, DataStore.CreateStore(cfg));
                Check(!DataStore.Store.HasUsers(), "New store has no accounts yet (first-Admin setup shown)");
                DataStore.Store.CreateFirstAdmin("Owner", "Olivia Owner", "owner123");
                Check(Throws(delegate { DataStore.Login("owner", "nope"); }) && Session.Current == null, "Wrong password is rejected");
                DataStore.Login("OWNER", "owner123");
                Check(Session.Current != null && Session.Current.IsAdmin && Session.Current.Permissions.Count == Permissions.All.Length,
                    "Admin signs in with every permission");
                DataStore.Load();
                Check(DataStore.Data.Products.Count == 6, "Sample inventory seeded (6 items)");
                Check(DataStore.FindProduct("btq000001") != null, "Barcode lookup is case-insensitive");

                Sale sale = CoreFlow(false);
                Check(sale.Cashier == "Olivia Owner", "Sale records the cashier (" + sale.Cashier + ")");

                // --- RBAC ---
                StaffUser cashier = new StaffUser(); cashier.Username = "maria"; cashier.DisplayName = "Maria"; cashier.RoleId = 3;
                DataStore.SaveUser(cashier, "maria123");
                Check(Throws(delegate { StaffUser dup = new StaffUser(); dup.Username = "MARIA"; dup.RoleId = 3; DataStore.SaveUser(dup, "abcdef"); }),
                    "Duplicate username is rejected");
                StaffUser me = DataStore.ListUsers().First(u => u.Username == "owner");
                me.Active = false;
                Check(Throws(delegate { DataStore.SaveUser(me, null); }), "Admin cannot disable their own account");
                me.Active = true; me.RoleId = 2;
                Check(Throws(delegate { DataStore.SaveUser(me, null); }), "The last active Admin cannot lose the Admin role");
                StaffRole senior = new StaffRole(); senior.Name = "Senior cashier"; senior.Permissions = "sell,discount,refund";
                DataStore.SaveRole(senior);
                Check(Throws(delegate { DataStore.DeleteRole(DataStore.ListRoles().First(r => r.Id == 1)); }), "Admin role cannot be deleted");

                DataStore.Logout();
                DataStore.Login("maria", "maria123");
                Check(Session.Can(Permissions.Sell) && !Session.Can(Permissions.Refund), "Cashier role: can sell, cannot refund");
                Product anyItem = DataStore.Data.Products.First();
                Check(Throws(delegate { DataStore.RefundSale(sale); }), "Cashier is blocked from refunds");
                Check(Throws(delegate { DataStore.ChangeQuantity(anyItem, 1, "x"); }), "Cashier is blocked from stock adjustments");
                Check(Throws(delegate { DataStore.SaveProduct(anyItem.Clone(), anyItem); }), "Cashier is blocked from editing items");
                Check(Throws(delegate { DataStore.ListUsers(); }), "Cashier is blocked from Users & roles");
                Sale discounted = new Sale(); discounted.DiscountPercent = 10; discounted.Lines.Add(SaleLine.FromProduct(anyItem));
                Check(Throws(delegate { DataStore.RecordSale(discounted); }), "Cashier is blocked from discounts");
                DataStore.Logout();
                DataStore.Login("owner", "owner123");

                // Reload from disk
                DataStore.Open(cfg, DataStore.CreateStore(cfg));
                Sale reloaded = DataStore.FindSaleByCode(sale.Code);
                Check(reloaded != null && reloaded.Refunded && DataStore.Data.Products.Count == 7, "Data persists after reload");

                // CSV import
                string csv = Path.Combine(outDir, "import.csv");
                File.WriteAllText(csv, "Name,Category,Size,Color,Price,Quantity,Barcode\r\n\"Boho Maxi Skirt, tiered\",Bottoms,M,Rust,$72.50,3,\r\nSilk Cami Top,Tops,M,Ivory,54,20,BTQ000003\r\n");
                int added, updated, skipped;
                InventoryPanel.ImportCsvFile(csv, out added, out updated, out skipped);
                Check(added == 1 && updated == 1 && skipped == 0, "CSV import adds 1 / updates 1");
                Product skirt = DataStore.Data.Products.FirstOrDefault(x => x.Name == "Boho Maxi Skirt, tiered");
                Check(skirt != null && skirt.Price == 72.50m && skirt.Quantity == 3 && skirt.Barcode.StartsWith("BTQ"), "CSV quoted field, currency parse, auto barcode");
                Check(DataStore.FindProduct("BTQ000003").Quantity == 20, "CSV update sets quantity by barcode");

                // Config encryption round-trip
                DbConfig enc = new DbConfig(); enc.Password = "p@ss;}word";
                Check(enc.Password == "p@ss;}word" && !enc.PasswordProtected.Contains("word"), "DB password is encrypted (DPAPI) and round-trips");

                // Images
                foreach (LabelLayout layout in LabelPrinter.Layouts)
                    using (Bitmap bmp = LabelPrinter.RenderLabel(DataStore.Data.Products[1], layout, 300, DataStore.Data.Settings.StoreName))
                        bmp.Save(Path.Combine(outDir, "label-" + layout.Key + ".png"), ImageFormat.Png);
                using (Bitmap bmp = ReceiptPrinter.Render(reloaded, DataStore.Data.Settings, 203))
                    bmp.Save(Path.Combine(outDir, "receipt.png"), ImageFormat.Png);
                Check(true, "Rendered label and receipt images");

                // Screenshots
                MainForm form = new MainForm();
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-5000, -5000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                string[] names = { "sell", "inventory", "sales", "labels", "settings", "users" };
                Button[] navigation = form.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().ToArray();
                Control[] pages = { form.Pos, form.Inventory, form.Sales, form.Labels, form.SettingsView, form.UsersView };
                form.Pos.HandleScan(DataStore.Data.Products.First().Barcode);
                form.Labels.AddProducts(DataStore.Data.Products.Take(3), false);
                for (int i = 0; i < names.Length; i++)
                {
                    navigation[i].PerformClick();
                    Check(pages[i].Visible, "Navigation opens " + names[i]);
                    if (i == 2) form.Sales.SelectSale(reloaded);
                    Application.DoEvents();
                    Shot(form, Path.Combine(outDir, "tab-" + names[i] + ".png"));
                }
                using (ProductDialog editor = new ProductDialog(DataStore.Data.Products.First(), null, null))
                {
                    editor.StartPosition = FormStartPosition.Manual;
                    editor.Location = new Point(-5000, -5000);
                    editor.ShowInTaskbar = false;
                    editor.Show(); Application.DoEvents();
                    Shot(editor, Path.Combine(outDir, "product-editor.png"));
                    editor.Close();
                }
                form.Size = form.MinimumSize;
                for (int i = 0; i < names.Length; i++)
                {
                    form.SelectTabIndex(i); Application.DoEvents();
                    Shot(form, Path.Combine(outDir, "compact-" + names[i] + ".png"));
                }
                form.Close();
                DatabaseSetupForm setup = new DatabaseSetupForm(null, true);
                setup.StartPosition = FormStartPosition.Manual;
                setup.Location = new Point(-5000, -5000);
                setup.Show();
                Application.DoEvents();
                Shot(setup, Path.Combine(outDir, "database-setup.png"));
                setup.Close();
                File.WriteAllText(DbConfig.SetupChoicePath, "cloud");   // as if the installer chose the web server
                setup = new DatabaseSetupForm(null, true);
                setup.StartPosition = FormStartPosition.Manual;
                setup.Location = new Point(-5000, -5000);
                setup.Show();
                Application.DoEvents();
                Shot(setup, Path.Combine(outDir, "database-setup-cloud.png"));
                setup.Close();
                using (LoginForm login = new LoginForm("My Boutique", cfg.Describe(), false, ""))
                {
                    login.StartPosition = FormStartPosition.Manual; login.Location = new Point(-5000, -5000);
                    login.Show(); Application.DoEvents();
                    Shot(login, Path.Combine(outDir, "login.png"));
                    login.Close();
                }
                // Checkout feedback windows
                using (ProcessingForm pf = new ProcessingForm("Completing sale...", Ui.Money(211.86m) + "  -  Cash", DataStore.Store.Describe,
                    delegate { System.Threading.Thread.Sleep(5000); }))
                {
                    pf.StartPosition = FormStartPosition.Manual; pf.Location = new Point(-5000, -5000);
                    pf.Show();
                    DateTime until = DateTime.Now.AddSeconds(4.5);
                    while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }   // let "slow connection" text appear
                    Shot(pf, Path.Combine(outDir, "checkout-processing.png"));
                    while (pf.Visible) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
                    Check(pf.DialogResult == DialogResult.OK && pf.Error == null, "Processing window closes itself when the save finishes");
                }
                Sale shown = new Sale(); shown.Number = 1042; shown.Cashier = "Maria"; shown.PaymentMethod = "Cash";
                shown.Lines.Add(SaleLine.FromProduct(DataStore.Data.Products[0])); shown.Lines[0].Quantity = 2;
                shown.Total = 137.71m; shown.AmountTendered = 500m; shown.Change = 362.29m;
                string[] kinds = { "completed", "failed", "uncertain" };
                SaleOutcome[] outcomes = { SaleOutcome.Completed, SaleOutcome.Failed, SaleOutcome.Uncertain };
                string[] errors = { null, "Your role does not allow discounts or price changes.", "Cannot reach the web server (The operation has timed out)" };
                for (int i = 0; i < 3; i++)
                    using (SaleResultForm rf = new SaleResultForm(outcomes[i], shown, 137.71m, "Receipt printed (sent to the default printer).", errors[i], delegate { return "Receipt printed."; }))
                    {
                        rf.StartPosition = FormStartPosition.Manual; rf.Location = new Point(-5000, -5000);
                        rf.Show(); Application.DoEvents();
                        Shot(rf, Path.Combine(outDir, "checkout-" + kinds[i] + ".png"));
                        rf.Close();
                    }
                Check(true, "Captured screenshots");
            }
            catch (Exception ex)
            {
                failures++;
                log.AppendLine("FAIL  Exception: " + ex);
            }
            return Finish(outDir);
        }

        static bool Throws(Action a)
        {
            try { a(); return false; }
            catch (Exception) { return true; }
        }

        static void Shot(Form f, string path)
        {
            using (Bitmap shot = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(shot, new Rectangle(0, 0, f.Width, f.Height));
                shot.Save(path, ImageFormat.Png);
            }
        }

        public static int RunMySql(string[] args)
        {
            log = new StringBuilder();
            failures = 0;
            string outDir = args.Length >= 7 ? args[6] : Path.Combine(Path.GetTempPath(), "BoutiquePOS-dbtest");
            Directory.CreateDirectory(outDir);
            try
            {
                if (args.Length < 6) throw new ArgumentException("Usage: --dbtest <host> <port> <database> <user> <password> [outDir]");
                AppPaths.UseDir(Path.Combine(outDir, "data"));
                DbConfig cfg = new DbConfig();
                cfg.Mode = DbConfig.ModeMySql; cfg.Location = DbConfig.LocServer;
                cfg.Host = args[1]; cfg.Port = int.Parse(args[2]); cfg.Database = args[3]; cfg.User = args[4]; cfg.Password = args[5];
                log.AppendLine("INFO  ODBC driver: " + (OdbcDrivers.BestMySql() ?? "(none)"));
                log.AppendLine("INFO  Server version: " + MySqlStore.TestConnection(cfg));
                DataStore.Attach(cfg, DataStore.CreateStore(cfg));
                if (!DataStore.Store.HasUsers()) DataStore.Store.CreateFirstAdmin("dbtest", "DB Test", "dbtest123");
                DataStore.Login("dbtest", "dbtest123");   // a throwaway test database is expected
                DataStore.Load();
                Check(Session.Current.IsAdmin, "Schema ensured (version " + MySqlStore.SchemaVersion + "), Admin signed in, " + DataStore.Data.Products.Count + " products loaded");

                CoreFlow(true);

                // A second "register": a fresh store instance must see the same stock
                MySqlStore other = new MySqlStore(cfg);
                Check(other.ListRoles().Count >= 4, "Default roles exist in the staff_roles table");
                Product last = DataStore.Data.Products.OrderByDescending(x => x.Created).First();
                Product seen = other.FindProduct(last.Barcode);
                Check(seen != null && seen.Quantity == last.Quantity, "Another register sees the same quantity");

                // Shared settings
                string oldName = DataStore.Data.Settings.StoreName;
                DataStore.Data.Settings.StoreName = "DB Test Store";
                DataStore.SaveSettings();
                Check(other.LoadAll().Settings.StoreName == "DB Test Store", "Shared settings stored in database");
                DataStore.Data.Settings.StoreName = oldName;
                DataStore.SaveSettings();

                DataStore.DeleteProducts(new Product[] { last });
                Check(other.FindProduct(last.Barcode) == null, "Delete removes the product");
            }
            catch (Exception ex)
            {
                failures++;
                log.AppendLine("FAIL  Exception: " + ex);
            }
            return Finish(outDir);
        }

        /// <summary>--apitest &lt;baseUrl&gt; &lt;adminUser&gt; &lt;adminPassword&gt; [outDir] : the same rules through the Laravel web API (use a test server!).</summary>
        public static int RunApi(string[] args)
        {
            log = new StringBuilder();
            failures = 0;
            string outDir = args.Length >= 5 ? args[4] : Path.Combine(Path.GetTempPath(), "BoutiquePOS-apitest");
            Directory.CreateDirectory(outDir);
            try
            {
                if (args.Length < 4) throw new ArgumentException("Usage: --apitest <baseUrl> <adminUser> <adminPassword> [outDir]");
                AppPaths.UseDir(Path.Combine(outDir, "data"));
                DbConfig cfg = new DbConfig();
                cfg.Mode = DbConfig.ModeApi; cfg.Location = DbConfig.LocCloud; cfg.ApiUrl = args[1];
                log.AppendLine("INFO  Server time: " + ApiStore.TestConnection(cfg));
                DataStore.Attach(cfg, DataStore.CreateStore(cfg));
                Check(Throws(delegate { DataStore.Login(args[2], "definitely-wrong"); }), "Server rejects a wrong password");
                DataStore.Login(args[2], args[3]);
                Check(Session.Current.IsAdmin && Session.Current.Permissions.Count == Permissions.All.Length, "Admin signs in through POST /auth/login with every permission");
                DataStore.Load();
                int before = DataStore.Data.Products.Count;
                Check(true, "Connected and loaded " + before + " products and shared settings (store \"" + DataStore.Data.Settings.StoreName + "\")");

                Sale sale = CoreFlow(true);
                Check(sale.Number > 0 && sale.Total == 211.86m && sale.Change == 38.14m, "Server assigned receipt " + sale.Code + " and totals (211.86, change 38.14)");

                // A second register sees the same data
                ApiStore other = new ApiStore(cfg);
                other.Login(args[2], args[3]);
                Product last = DataStore.Data.Products.OrderByDescending(x => x.Created).First();
                Product seen = other.FindProduct(last.Barcode);
                Check(seen != null && seen.Quantity == last.Quantity, "Another register sees the same quantity");

                // Incremental sync picks up a change made by the other register
                other.LoadAll();
                other.ChangeQuantity(seen, -1, "count");
                DataStore.RefreshProducts(true);
                Product synced = DataStore.FindById(last.Id);
                Check(synced != null && synced.Quantity == last.Quantity - 1, "Incremental sync picks up the other register's change");

                // Shared settings round-trip
                string oldName = DataStore.Data.Settings.StoreName;
                DataStore.Data.Settings.StoreName = "API Test Store";
                DataStore.SaveSettings();
                Check(other.LoadAll().Settings.StoreName == "API Test Store", "Shared settings saved on the server");
                DataStore.Data.Settings.StoreName = oldName;
                DataStore.SaveSettings();

                Check(DataStore.FirstSaleDate().HasValue, "First sale date available for 'All time'");

                DataStore.DeleteProducts(new Product[] { synced });
                Check(other.FindProduct(last.Barcode) == null, "Delete removes the product");
                DataStore.RefreshProducts(true);
                Check(DataStore.FindById(last.Id) == null && DataStore.Data.Products.Count == before, "Deleted item drops out on sync");

                // RBAC enforced by the SERVER (calls go straight to the store, skipping the app's own checks)
                StaffUser cashier = new StaffUser(); cashier.Username = "apitest.cashier" + DateTime.Now.Ticks % 100000; cashier.DisplayName = "Test Cashier"; cashier.RoleId = 3;
                DataStore.SaveUser(cashier, "cashier123");
                Check(DataStore.ListUsers().Any(u => u.Username == cashier.Username && u.RoleName == "Cashier"), "Admin created a Cashier through the API");
                ApiStore till = new ApiStore(cfg);
                SessionUser cs = till.Login(cashier.Username, "cashier123");
                Check(cs.Permissions.Contains(Permissions.Sell) && !cs.Permissions.Contains(Permissions.Refund), "Cashier signs in with the Cashier role's permissions");
                Sale toRefund = DataStore.FindSaleByCode(sale.Code);
                bool denied = false;
                try { till.RefundSale(toRefund, DateTime.Now); } catch (PermissionDeniedException) { denied = true; }
                Check(denied, "Server refuses a refund from a Cashier (403)");
                denied = false;
                try { till.ListUsers(); } catch (PermissionDeniedException) { denied = true; }
                Check(denied, "Server refuses Users & roles to a Cashier (403)");
                till.Logout();
                Check(Throws(delegate { till.ListRoles(); }), "Signed-out session no longer works");
                cashier.Active = false;
                DataStore.SaveUser(cashier, null);
                Check(Throws(delegate { new ApiStore(cfg).Login(cashier.Username, "cashier123"); }), "Disabled account cannot sign in");
                DataStore.DeleteUser(cashier);

                // "Try again" after a lost connection: the same ClientRef must replay, not record a second sale
                Product rp = new Product(); rp.Name = "Retry Test " + DateTime.Now.Ticks; rp.Price = 20m; rp.Quantity = 5;
                rp.Barcode = DataStore.NextBarcode();
                DataStore.SaveProduct(rp, null);
                string sameRef = Guid.NewGuid().ToString();
                Sale first = new Sale(); first.ClientRef = sameRef; first.PaymentMethod = "Card"; first.Lines.Add(SaleLine.FromProduct(rp));
                DataStore.RecordSale(first);
                Sale again = new Sale(); again.ClientRef = sameRef; again.PaymentMethod = "Card"; again.Lines.Add(SaleLine.FromProduct(rp));
                DataStore.RecordSale(again);
                Product after = other.FindProduct(rp.Barcode);
                Check(again.Number == first.Number && after != null && after.Quantity == 4,
                    "Retrying a sale with the same ID returns " + first.Code + " again and deducts stock only once (on hand " + (after == null ? -1 : after.Quantity) + ")");
                DataStore.DeleteProducts(new Product[] { DataStore.FindById(rp.Id) });
            }
            catch (Exception ex)
            {
                failures++;
                log.AppendLine("FAIL  Exception: " + ex);
            }
            return Finish(outDir);
        }
    }
}
