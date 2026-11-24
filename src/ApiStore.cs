using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace BoutiquePOS
{
    /// <summary>Thrown when the web server cannot be reached at all (no network, DNS, timeout).</summary>
    public class ApiUnavailableException : Exception
    {
        public ApiUnavailableException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Minimal JSON-over-HTTPS client for the Laravel POS API (web/docs/API.md).</summary>
    public class ApiClient
    {
        readonly string baseUrl;
        /// <summary>Session token from POST /auth/login (empty until someone signs in).</summary>
        public string Token = "";
        public int TimeoutMs = 20000;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        static ApiClient()
        {
            // csc-built apps run with .NET 4.0 network defaults; turn on modern TLS explicitly.
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; } // 12288 = TLS 1.3
            catch (NotSupportedException) { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }
        }

        public ApiClient(string baseUrl, string token)
        {
            this.baseUrl = (baseUrl ?? "").Trim().TrimEnd('/') + "/api/v1/";
            Token = token ?? "";
        }

        public class Response
        {
            public int Status;
            public Dictionary<string, object> Body;
            public string Message
            {
                get
                {
                    if (Body == null) return "HTTP " + Status;
                    string m = Body.ContainsKey("message") ? Convert.ToString(Body["message"]) : "";
                    Dictionary<string, object> errors = Body.ContainsKey("errors") ? Body["errors"] as Dictionary<string, object> : null;
                    if (errors != null && errors.Count > 0)
                    {
                        IList first = errors.Values.First() as IList;
                        if (first != null && first.Count > 0) m = Convert.ToString(first[0]);
                    }
                    return string.IsNullOrEmpty(m) ? "HTTP " + Status : m;
                }
            }
        }

        public Response Send(string method, string path, object body)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(baseUrl + path);
            req.Method = method;
            req.Accept = "application/json";
            req.UserAgent = "BoutiquePOS/1.1";
            req.Timeout = TimeoutMs;
            req.ReadWriteTimeout = TimeoutMs;
            if (!string.IsNullOrEmpty(Token)) req.Headers["Authorization"] = "Bearer " + Token;
            if (body != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(Json.Serialize(body));
                req.ContentType = "application/json; charset=utf-8";
                req.ContentLength = bytes.Length;
                using (Stream s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            }
            else if (method != "GET" && method != "DELETE") req.ContentLength = 0;

            HttpWebResponse resp = null;
            try { resp = (HttpWebResponse)req.GetResponse(); }
            catch (WebException ex)
            {
                resp = ex.Response as HttpWebResponse;
                if (resp == null) throw new ApiUnavailableException("Cannot reach the web server (" + ex.Message + ")", ex);
            }
            using (resp)
            {
                string text;
                using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) text = r.ReadToEnd();
                Response result = new Response();
                result.Status = (int)resp.StatusCode;
                if (text.Length > 0)
                {
                    try { result.Body = Json.DeserializeObject(text) as Dictionary<string, object>; }
                    catch (Exception) { result.Body = null; }
                }
                return result;
            }
        }

        public static string Esc(string s) { return Uri.EscapeDataString(s ?? ""); }
    }

    /// <summary>
    /// Registers that connect through the web server (Laravel API, e.g. on Forge) instead of directly to MySQL.
    /// The server assigns sale numbers and calculates totals; the store copies them back onto the Sale.
    /// </summary>
    public class ApiStore : IStore
    {
        readonly DbConfig cfg;
        readonly ApiClient api;
        readonly Dictionary<string, Product> products = new Dictionary<string, Product>();
        string lastSync;   // server_time from the previous product sync

        public ApiStore(DbConfig cfg)
        {
            this.cfg = cfg;
            api = new ApiClient(cfg.ApiUrl, "");   // staff sign in with username + password (see Login)
        }

        public string Describe { get { return cfg.Describe(); } }
        public bool IsShared { get { return true; } }

        /// <summary>Checks that the address is a Boutique POS web server. Returns its time.</summary>
        public static string TestConnection(DbConfig cfg)
        {
            ApiClient c = new ApiClient(cfg.ApiUrl, "");
            ApiClient.Response ping = c.Send("GET", "ping", null);
            if (ping.Status != 200 || ping.Body == null || Convert.ToString(ping.Body["status"]) != "ok")
                throw new InvalidOperationException("That address is not a Boutique POS web server (HTTP " + ping.Status + ").");
            return Convert.ToString(ping.Body["server_time"]);
        }
        // ---------------- helpers ----------------

        static ApiClient.Response Check(ApiClient.Response r)
        {
            if (r.Status >= 200 && r.Status < 300) return r;
            if (r.Status == 401)
                throw new InvalidOperationException("Your sign-in session has ended. Click \"Switch user\" and sign in again.");
            if (r.Status == 403) throw new PermissionDeniedException(r.Message);
            if (r.Status == 404) throw new KeyNotFoundException(r.Message);
            if (r.Status == 409 || r.Status == 422) throw new InvalidOperationException(r.Message);
            throw new InvalidOperationException("Web server error " + r.Status + ": " + r.Message);
        }

        static Dictionary<string, object> Data(ApiClient.Response r) { return (Dictionary<string, object>)r.Body["data"]; }

        ApiClient.Response Call(string method, string path, object body) { return Check(api.Send(method, path, body)); }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v; return d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        }

        static decimal Dec(Dictionary<string, object> d, string k)
        {
            string s = Str(d, k);
            return s.Length == 0 ? 0m : decimal.Parse(s, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        static int Int(Dictionary<string, object> d, string k) { string s = Str(d, k); return s.Length == 0 ? 0 : int.Parse(s, CultureInfo.InvariantCulture); }

        static DateTime Time(Dictionary<string, object> d, string k)
        {
            string s = Str(d, k);
            return s.Length == 0 ? DateTime.MinValue : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture).LocalDateTime;
        }

        static string Money(decimal v) { return SaleMath.R(v).ToString("0.00", CultureInfo.InvariantCulture); }

        static Product ToProduct(Dictionary<string, object> d)
        {
            Product p = new Product();
            p.Id = Str(d, "id"); p.Barcode = Str(d, "barcode"); p.Name = Str(d, "name"); p.Category = Str(d, "category");
            p.Brand = Str(d, "brand"); p.Size = Str(d, "size"); p.Color = Str(d, "color"); p.Price = Dec(d, "price");
            p.Cost = Dec(d, "cost"); p.Quantity = Int(d, "quantity"); p.ReorderLevel = Int(d, "reorder_level");
            p.Notes = Str(d, "notes"); p.Created = Time(d, "created"); p.Updated = Time(d, "updated");
            return p;
        }

        static Sale ToSale(Dictionary<string, object> d)
        {
            Sale s = new Sale();
            CopySale(d, s);
            return s;
        }

        /// <summary>Copies the server's version of a sale (number, totals, lines) onto <paramref name="s"/>.</summary>
        static void CopySale(Dictionary<string, object> d, Sale s)
        {
            s.Number = Int(d, "number"); s.Date = Time(d, "sale_date"); s.Subtotal = Dec(d, "subtotal");
            s.DiscountPercent = Dec(d, "discount_percent"); s.DiscountAmount = Dec(d, "discount_amount");
            s.TaxRate = Dec(d, "tax_rate"); s.Tax = Dec(d, "tax"); s.Total = Dec(d, "total");
            s.PaymentMethod = Str(d, "payment_method"); s.AmountTendered = Dec(d, "amount_tendered"); s.Change = Dec(d, "change_due");
            s.Refunded = Str(d, "refunded") == "True" || Str(d, "refunded") == "1";
            s.RefundDate = Str(d, "refund_date").Length > 0 ? (DateTime?)Time(d, "refund_date") : null;
            s.Note = Str(d, "note");
            s.Cashier = Str(d, "cashier");
            s.Lines = new List<SaleLine>();
            IList lines = d.ContainsKey("lines") ? d["lines"] as IList : null;
            if (lines != null)
                foreach (Dictionary<string, object> l in lines)
                {
                    SaleLine line = new SaleLine();
                    line.ProductId = Str(l, "product_id"); line.Barcode = Str(l, "barcode"); line.Name = Str(l, "name");
                    line.Size = Str(l, "size"); line.Color = Str(l, "color"); line.UnitPrice = Dec(l, "unit_price"); line.Quantity = Int(l, "quantity");
                    s.Lines.Add(line);
                }
        }

        static Dictionary<string, int> Quantities(ApiClient.Response r)
        {
            Dictionary<string, int> q = new Dictionary<string, int>();
            Dictionary<string, object> d = r.Body.ContainsKey("quantities") ? r.Body["quantities"] as Dictionary<string, object> : null;
            if (d != null) foreach (KeyValuePair<string, object> kv in d) q[kv.Key] = Convert.ToInt32(kv.Value, CultureInfo.InvariantCulture);
            return q;
        }

        Dictionary<string, object> ProductBody(Product p)
        {
            Dictionary<string, object> b = new Dictionary<string, object>();
            b["barcode"] = p.Barcode; b["name"] = p.Name; b["category"] = p.Category ?? ""; b["brand"] = p.Brand ?? "";
            b["size"] = p.Size ?? ""; b["color"] = p.Color ?? ""; b["price"] = Money(p.Price); b["cost"] = Money(p.Cost);
            b["reorder_level"] = p.ReorderLevel; b["notes"] = p.Notes ?? "";
            return b;
        }

        // ---------------- IStore ----------------

        public AppData LoadAll()
        {
            AppData d = new AppData();
            products.Clear();
            lastSync = null;
            d.Products = LoadProducts();
            d.Settings = MySqlStore.LoadDeviceSettings();
            Dictionary<string, object> shared = Data(Call("GET", "settings", null));
            MySqlStore.ApplyShared(d.Settings, shared.ToDictionary(kv => kv.Key, kv => Convert.ToString(kv.Value, CultureInfo.InvariantCulture)));
            d.Sales = new List<Sale>();
            return d;
        }

        /// <summary>First call downloads everything; later calls only fetch changes since the last sync.</summary>
        public List<Product> LoadProducts()
        {
            int page = 1, lastPage;
            string serverTime = null;
            List<string> deleted = new List<string>();
            do
            {
                string q = "products?per_page=1000&page=" + page + (lastSync != null ? "&updated_since=" + ApiClient.Esc(lastSync) : "");
                ApiClient.Response r = Call("GET", q, null);
                foreach (Dictionary<string, object> p in (IList)r.Body["data"])
                {
                    Product prod = ToProduct(p);
                    products[prod.Id] = prod;
                }
                if (serverTime == null) serverTime = Convert.ToString(r.Body["server_time"]);
                if (r.Body.ContainsKey("deleted_ids"))
                    foreach (object id in (IList)r.Body["deleted_ids"]) deleted.Add(Convert.ToString(id));
                lastPage = Convert.ToInt32(((Dictionary<string, object>)r.Body["meta"])["last_page"]);
                page++;
            } while (page <= lastPage);
            foreach (string id in deleted) products.Remove(id);
            lastSync = serverTime;
            // Fresh objects each time, like the MySQL store, so the cache can be swapped safely.
            return products.Values.Select(p => p.Clone()).ToList();
        }

        public Product FindProduct(string barcode)
        {
            ApiClient.Response r = api.Send("GET", "products/barcode/" + ApiClient.Esc(barcode), null);
            if (r.Status == 404) return null;
            return ToProduct(Data(Check(r)));
        }

        public void SaveProduct(Product p, bool isNew, int quantityDelta)
        {
            Dictionary<string, object> b = ProductBody(p);
            ApiClient.Response r;
            if (isNew)
            {
                b["id"] = p.Id;
                b["quantity"] = p.Quantity;
                r = Call("POST", "products", b);
            }
            else
            {
                if (quantityDelta != 0) b["quantity_delta"] = quantityDelta;
                r = Call("PUT", "products/" + p.Id, b);
            }
            Product saved = ToProduct(Data(r));
            p.Quantity = saved.Quantity;
            p.Barcode = saved.Barcode;
            products[saved.Id] = saved;
        }

        public void DeleteProduct(Product p)
        {
            ApiClient.Response r = api.Send("DELETE", "products/" + p.Id, null);
            if (r.Status != 404) Check(r);
            products.Remove(p.Id);
        }

        public int ChangeQuantity(Product p, int delta, string reason)
        {
            if (delta == 0) return p.Quantity;
            Dictionary<string, object> b = new Dictionary<string, object>();
            b["delta"] = delta; b["reason"] = reason;
            return ToProduct(Data(Call("POST", "products/" + p.Id + "/adjust", b))).Quantity;
        }

        public string NextBarcode()
        {
            return Str(Data(Call("POST", "barcodes/next", null)), "barcode");
        }

        public Dictionary<string, int> InsertSale(Sale s)
        {
            Dictionary<string, object> b = new Dictionary<string, object>();
            // Same ref on every retry (also across "Try again" in the app) => never recorded twice.
            if (string.IsNullOrEmpty(s.ClientRef)) s.ClientRef = Guid.NewGuid().ToString();
            b["client_ref"] = s.ClientRef;
            List<object> lines = new List<object>();
            foreach (SaleLine l in s.Lines)
            {
                Dictionary<string, object> line = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(l.ProductId)) line["product_id"] = l.ProductId;
                line["barcode"] = l.Barcode ?? ""; line["name"] = l.Name ?? ""; line["size"] = l.Size ?? ""; line["color"] = l.Color ?? "";
                line["unit_price"] = Money(l.UnitPrice); line["quantity"] = l.Quantity;
                lines.Add(line);
            }
            b["lines"] = lines;
            b["discount_percent"] = SaleMath.R(s.DiscountPercent).ToString("0.##", CultureInfo.InvariantCulture);
            b["payment_method"] = s.PaymentMethod;
            if (s.PaymentMethod == "Cash") b["amount_tendered"] = Money(s.AmountTendered);
            if (!string.IsNullOrEmpty(s.Note)) b["note"] = s.Note;

            ApiClient.Response r = null;
            for (int attempt = 1; ; attempt++)
            {
                try { r = Call("POST", "sales", b); break; }
                catch (ApiUnavailableException)
                {
                    if (attempt >= 3) throw;
                    System.Threading.Thread.Sleep(1000 * attempt);
                }
            }
            CopySale(Data(r), s);   // server's number, totals and tax are authoritative
            return Quantities(r);
        }

        public Dictionary<string, int> RefundSale(Sale s, DateTime when)
        {
            ApiClient.Response r = Call("POST", "sales/" + s.Number + "/refund", null);
            return Quantities(r);
        }

        public void SaveSettings(StoreSettings s)
        {
            Dictionary<string, object> b = new Dictionary<string, object>();
            foreach (string name in MySqlStore.SharedSettings) b[name] = MySqlStore.SharedValue(s, name);
            Call("PUT", "settings", b);
            MySqlStore.SaveDeviceSettings(s);
        }

        /// <summary>Sale numbers are assigned by the server when the sale is posted (see InsertSale).</summary>
        public int NextNumber(string counter)
        {
            if (counter == "sale") return 0;
            throw new NotSupportedException("Use NextBarcode() with the web API.");
        }

        public List<Sale> SalesBetween(DateTime from, DateTime toExclusive)
        {
            string f = from.Date.ToString("yyyy-MM-dd"), t = toExclusive.AddSeconds(-1).Date.ToString("yyyy-MM-dd");
            List<Sale> result = new List<Sale>();
            int page = 1, lastPage;
            do
            {
                ApiClient.Response r = Call("GET", "sales?per_page=500&from=" + f + "&to=" + t + "&page=" + page, null);
                foreach (Dictionary<string, object> d in (IList)r.Body["data"]) result.Add(ToSale(d));
                lastPage = Convert.ToInt32(((Dictionary<string, object>)r.Body["meta"])["last_page"]);
                page++;
            } while (page <= lastPage);
            return result;
        }

        public Sale FindSale(int number)
        {
            ApiClient.Response r = api.Send("GET", "sales/" + number, null);
            if (r.Status == 404) return null;
            return ToSale(Data(Check(r)));
        }

        public DateTime? FirstSaleDate()
        {
            string s = Str(Data(Call("GET", "stats", null)), "first_sale_date");
            return s.Length == 0 ? (DateTime?)null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture).LocalDateTime;
        }

        public AppData ExportAll()
        {
            AppData d = LoadAll();
            DateTime? first = FirstSaleDate();
            d.Sales = first.HasValue ? SalesBetween(first.Value.Date, DateTime.Today.AddDays(1)) : new List<Sale>();
            d.Sales.Reverse();
            return d;
        }

        public void Commit() { }

        // ---------------- staff sign-in & administration ----------------

        public SessionUser Login(string username, string password)
        {
            Dictionary<string, object> b = new Dictionary<string, object>();
            b["username"] = username ?? ""; b["password"] = password ?? ""; b["device"] = Environment.MachineName;
            ApiClient.Response r = api.Send("POST", "auth/login", b);
            if (r.Status == 401 || r.Status == 403 || r.Status == 422) throw new InvalidOperationException(r.Message);
            if (r.Status == 429) throw new InvalidOperationException("Too many sign-in attempts. Wait a minute and try again.");
            Check(r);
            api.Token = Str(r.Body, "token");
            Dictionary<string, object> u = (Dictionary<string, object>)r.Body["user"];
            SessionUser s = new SessionUser();
            s.Id = Int(u, "id"); s.Username = Str(u, "username"); s.DisplayName = Str(u, "display_name"); s.RoleName = Str(u, "role_name");
            foreach (object p in (IList)u["permissions"]) s.Permissions.Add(Convert.ToString(p));
            s.IsAdmin = Int(u, "role_id") == Permissions.AdminRoleId;
            return s;
        }

        public void Logout()
        {
            if (string.IsNullOrEmpty(api.Token)) return;
            try { api.Send("POST", "auth/logout", null); } catch (Exception) { /* signing out locally is enough */ }
            api.Token = "";
        }

        /// <summary>Online, the first Admin is created on the server: php artisan pos:admin &lt;username&gt;.</summary>
        public bool HasUsers() { return true; }

        public void CreateFirstAdmin(string username, string displayName, string password)
        {
            throw new NotSupportedException("Create the first admin on the server: php artisan pos:admin <username>");
        }

        static StaffUser ToUser(Dictionary<string, object> d)
        {
            StaffUser u = new StaffUser();
            u.Id = Int(d, "id"); u.Username = Str(d, "username"); u.DisplayName = Str(d, "display_name");
            u.RoleId = Int(d, "role_id"); u.RoleName = Str(d, "role_name");
            u.Active = Str(d, "active") == "True" || Str(d, "active") == "1";
            u.LastLogin = Str(d, "last_login").Length > 0 ? (DateTime?)Time(d, "last_login") : null;
            return u;
        }

        public List<StaffUser> ListUsers()
        {
            List<StaffUser> list = new List<StaffUser>();
            foreach (Dictionary<string, object> d in (IList)Call("GET", "staff", null).Body["data"]) list.Add(ToUser(d));
            return list;
        }

        public void SaveUser(StaffUser user, string newPassword)
        {
            Dictionary<string, object> b = new Dictionary<string, object>();
            b["username"] = user.Username; b["display_name"] = user.DisplayName ?? ""; b["role_id"] = user.RoleId; b["active"] = user.Active;
            if (!string.IsNullOrEmpty(newPassword)) b["password"] = newPassword;
            ApiClient.Response r = user.Id == 0 ? Call("POST", "staff", b) : Call("PUT", "staff/" + user.Id, b);
            user.Id = ToUser(Data(r)).Id;
        }

        public void DeleteUser(StaffUser user) { Call("DELETE", "staff/" + user.Id, null); }

        public List<StaffRole> ListRoles()
        {
            List<StaffRole> list = new List<StaffRole>();
            foreach (Dictionary<string, object> d in (IList)Call("GET", "roles", null).Body["data"])
            {
                List<string> perms = new List<string>();
                foreach (object p in (IList)d["permissions"]) perms.Add(Convert.ToString(p));
                bool system = Str(d, "is_system") == "True" || Str(d, "is_system") == "1";
                list.Add(StaffRole.Make(Int(d, "id"), Str(d, "name"), system ? "*" : Permissions.ToCsv(perms), system));
            }
            return list;
        }

        public void SaveRole(StaffRole role)
        {
            Dictionary<string, object> b = new Dictionary<string, object>();
            b["name"] = role.Name; b["permissions"] = role.PermissionList.ToArray();
            ApiClient.Response r = role.Id == 0 ? Call("POST", "roles", b) : Call("PUT", "roles/" + role.Id, b);
            role.Id = Int(Data(r), "id");
        }

        public void DeleteRole(StaffRole role) { Call("DELETE", "roles/" + role.Id, null); }
    }
}
