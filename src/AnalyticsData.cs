using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BoutiquePOS
{
    /// <summary>
    /// Figures for the Analytics page, computed from the store's sales and products (same rules as the web
    /// dashboard, web/app/Services/Pos/Analytics.php). Metrics the POS doesn't record (ad spend, web traffic,
    /// couriers, return reasons, customer identity, online fulfilment, write-off history) are sample data,
    /// repeatable for the same dates, and marked as such on screen.
    /// </summary>
    public class AnalyticsReport
    {
        public const decimal UsdRate = 58m;          // keep in step with POS_USD_RATE on the web server
        public const decimal CardFeePercent = 3.5m, OtherFeePercent = 2.5m, PackagingPerOrder = 25m, ShippingPerOrder = 0m;
        public static readonly string[] Channels = { "All channels", "In-store registers", "Web POS" };
        public static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        public DateTime From, To;                      // To is inclusive (a date)
        public DateTime PrevFrom, PrevTo;
        public int Days;
        public Kpis Cur, Prev;
        public int[,] HeatOrders = new int[7, 24];
        public decimal[,] HeatRevenue = new decimal[7, 24];
        public List<LowStockRow> LowStock = new List<LowStockRow>();
        public List<WaterStep> Waterfall = new List<WaterStep>();
        public decimal NetProfit, ProfitPerOrder, GrossMargin;
        public List<ProductRow> Products = new List<ProductRow>();
        public List<ReturnRow> Returns = new List<ReturnRow>();
        public SortedDictionary<string, int> ReasonTotals = new SortedDictionary<string, int>();
        public List<CategoryVelocity> Velocity = new List<CategoryVelocity>();
        public List<AgingBucket> Aging = new List<AgingBucket>();
        public List<string> Categories = new List<string>();
        public List<QuarterRow> Quarters = new List<QuarterRow>();
        public int[] HourOrders = new int[24], WeekdayOrders = new int[7];
        public decimal[] HourRevenue = new decimal[24], WeekdayRevenue = new decimal[7];
        public List<Daypart> Dayparts = new List<Daypart>();
        public int SystemUnits; public decimal SystemValue;

        // sample data
        public decimal AdSpend, PrevAdSpend; public List<Platform> Platforms = new List<Platform>();
        public List<DeviceRow> Devices = new List<DeviceRow>();
        public List<UnfulfilledRow> Unfulfilled = new List<UnfulfilledRow>();
        public decimal Cac; public int NewCustomers;
        public List<RtoRow> Couriers = new List<RtoRow>(), Regions = new List<RtoRow>();
        public decimal[] Ltv = new decimal[12]; public double RepeatRate, FirstTimeShare;
        public List<CohortRow> Cohorts = new List<CohortRow>();
        public List<ShrinkRow> Shrinkage = new List<ShrinkRow>();

        public decimal BlendedRoas { get { return AdSpend > 0 ? Math.Round(Cur.Net / AdSpend, 2) : 0m; } }
        public decimal PrevRoas { get { return PrevAdSpend > 0 ? Math.Round(Prev.Net / PrevAdSpend, 2) : 0m; } }
        public decimal AovToCac { get { return Cac > 0 ? Math.Round(Cur.Aov / Cac, 2) : 0m; } }

        public class Kpis { public decimal Gross, Cancellations, Discounts, Net, Tax; public int Orders, Units; public decimal Aov; }
        public class LowStockRow { public string Barcode, Name, Size, Color, Status; public int OnHand, Reorder, Sold30; public double Cover; }
        public class WaterStep { public string Label, Kind; public decimal Amount; public bool Total; }
        public class ProductRow { public string Name, Brand, Category; public SortedDictionary<string, int> Sizes = new SortedDictionary<string, int>(new SizeComparer()); public int Units, Returned; public decimal Gross, Net, Cost; public double ReturnRate, Margin; }
        public class ReturnRow { public string Sku, Name, Size; public int Sold, Returned; public double Rate; public Dictionary<string, int> Reasons; }
        public class CategoryVelocity { public string Category; public int Sold, OnHand; public double SellThrough; }
        public class AgingBucket { public string Label; public int Items, Units; public decimal Value; }
        public class QuarterRow { public string Label; public int SortKey; public Dictionary<string, decimal> Revenue = new Dictionary<string, decimal>(); public decimal Total, Cost; public double Margin; }
        public class Daypart { public string Name; public int FromHour, ToHour, Orders; public decimal Revenue; public List<KeyValuePair<string, int>> Top = new List<KeyValuePair<string, int>>(); }
        public class Platform { public string Name; public decimal Spend, Revenue, Roas; }
        public class DeviceRow { public string Device; public int Sessions; public double Share, Cvr, PrevCvr; public bool Drop; }
        public class UnfulfilledRow { public string Order, Customer, Bucket, Courier, Region, Payment; public int Items, AgeHours; public decimal Amount; }
        public class RtoRow { public string Name; public int Orders, Rto; public double Rate; public bool High; }
        public class CohortRow { public string Cohort; public int Customers; public List<double> Cells = new List<double>(); }
        public class ShrinkRow { public string Reason; public int Units; public decimal Cost; }

        /// <summary>Clothing sizes in shop order (XXS..XXL), then numeric sizes, then anything else.</summary>
        public class SizeComparer : IComparer<string>
        {
            static readonly string[] Order = { "XXS", "XS", "S", "M", "L", "XL", "XXL" };
            public int Compare(string a, string b)
            {
                int ia = Array.IndexOf(Order, (a ?? "").ToUpperInvariant()), ib = Array.IndexOf(Order, (b ?? "").ToUpperInvariant());
                if (ia >= 0 && ib >= 0) return ia.CompareTo(ib);
                if (ia >= 0) return -1;
                if (ib >= 0) return 1;
                double da, db;
                bool na = double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out da), nb = double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out db);
                if (na && nb) return da.CompareTo(db);
                if (na) return -1;
                if (nb) return 1;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        static bool InChannel(Sale s, int channel)
        {
            bool web = (s.Terminal ?? "").StartsWith("Web POS", StringComparison.OrdinalIgnoreCase);
            return channel == 0 || (channel == 2 ? web : !web);
        }

        static decimal NetOf(Sale s) { return s.Subtotal - s.DiscountAmount; }

        /// <summary>Loads sales for the range (and the previous period of the same length) and computes everything.</summary>
        public static AnalyticsReport Build(DateTime from, DateTime to, int channel)
        {
            AnalyticsReport r = new AnalyticsReport();
            r.From = from.Date; r.To = to.Date;
            r.Days = Math.Max(1, (int)(r.To - r.From).TotalDays + 1);
            r.PrevFrom = r.From.AddDays(-r.Days); r.PrevTo = r.From.AddDays(-1);

            List<Sale> sales = DataStore.SalesBetween(r.From, r.To.AddDays(1)).Where(s => InChannel(s, channel)).ToList();
            List<Sale> prev = DataStore.SalesBetween(r.PrevFrom, r.From).Where(s => InChannel(s, channel)).ToList();
            DateTime since = DateTime.Today.AddDays(-120);
            List<Sale> recent = since < r.From ? DataStore.SalesBetween(since, DateTime.Today.AddDays(1)) : sales;
            Dictionary<string, Product> products = new Dictionary<string, Product>();
            foreach (Product p in DataStore.Data.Products) products[p.Id] = p;

            r.Cur = KpisOf(sales); r.Prev = KpisOf(prev);
            r.BuildHeatmapAndTiming(sales);
            r.BuildLowStock(recent, products);
            r.BuildProducts(sales, products);
            r.BuildVelocity(sales, recent, products);
            r.BuildQuarters(sales, products);
            r.SystemUnits = products.Values.Sum(p => Math.Max(0, p.Quantity));
            r.SystemValue = products.Values.Sum(p => Math.Max(0, p.Quantity) * p.Cost);
            r.BuildSamples();
            r.BuildWaterfall(sales, products);
            return r;
        }

        static Kpis KpisOf(List<Sale> sales)
        {
            Kpis k = new Kpis();
            foreach (Sale s in sales)
            {
                k.Gross += s.Subtotal;
                if (s.Refunded) { k.Cancellations += s.Subtotal; continue; }
                k.Discounts += s.DiscountAmount; k.Tax += s.Tax; k.Orders++; k.Units += s.ItemCount;
            }
            k.Net = k.Gross - k.Cancellations - k.Discounts;
            k.Aov = k.Orders > 0 ? Math.Round(k.Net / k.Orders, 2) : 0m;
            return k;
        }

        void BuildHeatmapAndTiming(List<Sale> sales)
        {
            string[] names = { "Morning", "Lunch", "Afternoon", "Evening", "Late night" };
            int[,] hours = { { 6, 11 }, { 11, 14 }, { 14, 18 }, { 18, 24 }, { 0, 6 } };
            List<Dictionary<string, int>> items = new List<Dictionary<string, int>>();
            for (int i = 0; i < names.Length; i++)
            {
                Daypart d = new Daypart(); d.Name = names[i]; d.FromHour = hours[i, 0]; d.ToHour = hours[i, 1];
                Dayparts.Add(d); items.Add(new Dictionary<string, int>());
            }
            foreach (Sale s in sales)
            {
                if (s.Refunded) continue;
                int day = ((int)s.Date.DayOfWeek + 6) % 7, h = s.Date.Hour;
                decimal net = NetOf(s);
                HeatOrders[day, h]++; HeatRevenue[day, h] += net;
                HourOrders[h]++; HourRevenue[h] += net;
                WeekdayOrders[day]++; WeekdayRevenue[day] += net;
                int part = 4;
                for (int i = 0; i < 5; i++) if (h >= hours[i, 0] && h < hours[i, 1]) { part = i; break; }
                Dayparts[part].Orders++; Dayparts[part].Revenue += net;
                foreach (SaleLine l in s.Lines)
                {
                    int n; items[part].TryGetValue(l.Name, out n); items[part][l.Name] = n + l.Quantity;
                }
            }
            for (int i = 0; i < 5; i++) Dayparts[i].Top = items[i].OrderByDescending(kv => kv.Value).Take(3).ToList();
        }

        void BuildLowStock(List<Sale> recent, Dictionary<string, Product> products)
        {
            DateTime since = DateTime.Today.AddDays(-30);
            Dictionary<string, int> sold = new Dictionary<string, int>();
            foreach (Sale s in recent)
                if (!s.Refunded && s.Date >= since)
                    foreach (SaleLine l in s.Lines) { int n; sold.TryGetValue(l.ProductId, out n); sold[l.ProductId] = n + l.Quantity; }
            foreach (Product p in products.Values.Where(p => p.IsLowStock))
            {
                int s30; sold.TryGetValue(p.Id, out s30);
                double perDay = s30 / 30.0;
                LowStockRow row = new LowStockRow();
                row.Barcode = p.Barcode; row.Name = p.Name; row.Size = p.Size; row.Color = p.Color;
                row.OnHand = p.Quantity; row.Reorder = p.ReorderLevel; row.Sold30 = s30;
                row.Cover = perDay > 0 ? Math.Round(Math.Max(0, p.Quantity) / perDay, 1) : -1;
                row.Status = p.Quantity <= 0 ? "Out of stock" : ((row.Cover >= 0 && row.Cover < 7) || p.Quantity <= 1 ? "Restock now" : "Low");
                LowStock.Add(row);
            }
            LowStock = LowStock.OrderBy(x => x.OnHand).ThenByDescending(x => x.Sold30).Take(60).ToList();
        }

        void BuildProducts(List<Sale> sales, Dictionary<string, Product> products)
        {
            Dictionary<string, ProductRow> rows = new Dictionary<string, ProductRow>();
            Dictionary<string, ReturnRow> bySku = new Dictionary<string, ReturnRow>();
            foreach (Sale s in sales)
                foreach (SaleLine l in s.Lines)
                {
                    Product p; products.TryGetValue(l.ProductId ?? "", out p);
                    string brand = p != null ? p.Brand : "";
                    string key = (l.Name + "|" + brand).ToLowerInvariant();
                    ProductRow r;
                    if (!rows.TryGetValue(key, out r))
                    {
                        r = new ProductRow(); r.Name = l.Name; r.Brand = brand;
                        r.Category = p != null && p.Category.Length > 0 ? p.Category : "Uncategorized";
                        rows[key] = r;
                    }
                    string sku = l.Barcode.Length > 0 ? l.Barcode : l.ProductId;
                    ReturnRow rr;
                    if (!bySku.TryGetValue(sku, out rr)) { rr = new ReturnRow(); rr.Sku = sku; rr.Name = l.Name; rr.Size = l.Size; bySku[sku] = rr; }
                    if (s.Refunded) { r.Returned += l.Quantity; rr.Returned += l.Quantity; continue; }
                    rr.Sold += l.Quantity;
                    decimal lineGross = l.UnitPrice * l.Quantity;
                    decimal share = s.Subtotal > 0 ? lineGross / s.Subtotal : 0m;
                    string size = l.Size.Length > 0 ? l.Size : "One size";
                    int n; r.Sizes.TryGetValue(size, out n); r.Sizes[size] = n + l.Quantity;
                    r.Units += l.Quantity; r.Gross += lineGross; r.Net += lineGross - share * s.DiscountAmount;
                    r.Cost += l.Quantity * (p != null ? p.Cost : 0m);
                }
            foreach (ProductRow r in rows.Values)
            {
                r.Net = Math.Round(r.Net, 2);
                r.ReturnRate = r.Units + r.Returned > 0 ? Math.Round(100.0 * r.Returned / (r.Units + r.Returned), 1) : 0;
                r.Margin = r.Net > 0 ? Math.Round((double)((r.Net - r.Cost) / r.Net) * 100, 1) : 0;
            }
            Products = rows.Values.OrderByDescending(x => x.Net).ToList();
            foreach (ReturnRow rr in bySku.Values.Where(x => x.Returned > 0).OrderByDescending(x => x.Returned).Take(15))
            {
                rr.Rate = Math.Round(100.0 * rr.Returned / Math.Max(1, rr.Sold + rr.Returned), 1);
                rr.Reasons = Sample.ReturnReasons(rr.Sku, rr.Returned);
                foreach (KeyValuePair<string, int> kv in rr.Reasons) { int n; ReasonTotals.TryGetValue(kv.Key, out n); ReasonTotals[kv.Key] = n + kv.Value; }
                Returns.Add(rr);
            }
        }

        void BuildVelocity(List<Sale> sales, List<Sale> recent, Dictionary<string, Product> products)
        {
            Dictionary<string, int> sold = new Dictionary<string, int>();
            foreach (Sale s in sales)
                if (!s.Refunded) foreach (SaleLine l in s.Lines) { int n; sold.TryGetValue(l.ProductId ?? "", out n); sold[l.ProductId ?? ""] = n + l.Quantity; }
            Dictionary<string, CategoryVelocity> cats = new Dictionary<string, CategoryVelocity>();
            foreach (Product p in products.Values)
            {
                string c = p.Category.Length > 0 ? p.Category : "Uncategorized";
                CategoryVelocity v;
                if (!cats.TryGetValue(c, out v)) { v = new CategoryVelocity(); v.Category = c; cats[c] = v; }
                int n; sold.TryGetValue(p.Id, out n);
                v.Sold += n; v.OnHand += Math.Max(0, p.Quantity);
            }
            foreach (CategoryVelocity v in cats.Values) v.SellThrough = v.Sold + v.OnHand > 0 ? Math.Round(100.0 * v.Sold / (v.Sold + v.OnHand), 1) : 0;
            Velocity = cats.Values.OrderByDescending(v => v.SellThrough).ToList();

            // Dead-stock aging from the last 120 days of sales (older than that counts as 90+).
            Dictionary<string, DateTime> last = new Dictionary<string, DateTime>();
            foreach (Sale s in recent)
                if (!s.Refunded) foreach (SaleLine l in s.Lines)
                    {
                        DateTime d; string id = l.ProductId ?? "";
                        if (!last.TryGetValue(id, out d) || s.Date > d) last[id] = s.Date;
                    }
            string[] labels = { "Active (< 30 days)", "30-59 days", "60-89 days", "90+ days" };
            foreach (string label in labels) { AgingBucket b = new AgingBucket(); b.Label = label; Aging.Add(b); }
            foreach (Product p in products.Values)
            {
                if (p.Quantity <= 0) continue;
                DateTime d;
                DateTime sinceDate = last.TryGetValue(p.Id, out d) ? d : (p.Created > DateTime.Today.AddDays(-120) ? p.Created : DateTime.MinValue);
                double age = sinceDate == DateTime.MinValue ? 999 : (DateTime.Now - sinceDate).TotalDays;
                int i = age < 30 ? 0 : age < 60 ? 1 : age < 90 ? 2 : 3;
                Aging[i].Items++; Aging[i].Units += p.Quantity; Aging[i].Value += p.Quantity * p.Cost;
            }
        }

        void BuildQuarters(List<Sale> sales, Dictionary<string, Product> products)
        {
            Dictionary<string, QuarterRow> map = new Dictionary<string, QuarterRow>();
            HashSet<string> cats = new HashSet<string>();
            foreach (Sale s in sales)
            {
                if (s.Refunded) continue;
                int q = (s.Date.Month - 1) / 3 + 1;
                string label = "Q" + q + " " + s.Date.Year;
                QuarterRow row;
                if (!map.TryGetValue(label, out row)) { row = new QuarterRow(); row.Label = label; row.SortKey = s.Date.Year * 10 + q; map[label] = row; }
                foreach (SaleLine l in s.Lines)
                {
                    Product p; products.TryGetValue(l.ProductId ?? "", out p);
                    string c = p != null && p.Category.Length > 0 ? p.Category : "Uncategorized";
                    cats.Add(c);
                    decimal rev = l.UnitPrice * l.Quantity, v;
                    row.Revenue.TryGetValue(c, out v); row.Revenue[c] = v + rev;
                    row.Total += rev; row.Cost += l.Quantity * (p != null ? p.Cost : 0m);
                }
            }
            Quarters = map.Values.OrderBy(q => q.SortKey).ToList();
            foreach (QuarterRow q in Quarters) q.Margin = q.Total > 0 ? Math.Round((double)((q.Total - q.Cost) / q.Total) * 100, 1) : 0;
            Categories = cats.OrderBy(c => c).ToList();
        }

        void BuildWaterfall(List<Sale> sales, Dictionary<string, Product> products)
        {
            decimal cogs = 0m, fees = 0m;
            foreach (Sale s in sales)
            {
                if (s.Refunded) continue;
                foreach (SaleLine l in s.Lines) { Product p; if (products.TryGetValue(l.ProductId ?? "", out p)) cogs += l.Quantity * p.Cost; }
                string m = (s.PaymentMethod ?? "").ToLowerInvariant();
                if (m != "cash") fees += NetOf(s) * (m == "card" ? CardFeePercent : OtherFeePercent) / 100m;
            }
            decimal packaging = Cur.Orders * PackagingPerOrder, shipping = Cur.Orders * ShippingPerOrder;
            NetProfit = Cur.Net - cogs - fees - packaging - shipping - AdSpend;
            ProfitPerOrder = Cur.Orders > 0 ? Math.Round(NetProfit / Cur.Orders, 2) : 0m;
            GrossMargin = Cur.Net > 0 ? Math.Round((Cur.Net - cogs) / Cur.Net * 100, 1) : 0m;
            Step("Net sales", Cur.Net, "real", true); Step("Cost of goods", -cogs, "real", false);
            Step("Payment fees", -Math.Round(fees, 2), "estimate", false); Step("Packaging", -packaging, "estimate", false);
            Step("Shipping", -shipping, "estimate", false); Step("Marketing", -AdSpend, "sample", false);
            Step("Net profit", NetProfit, "result", true);
        }

        void Step(string label, decimal amount, string kind, bool total)
        {
            WaterStep s = new WaterStep(); s.Label = label; s.Amount = amount; s.Kind = kind; s.Total = total; Waterfall.Add(s);
        }

        void BuildSamples()
        {
            AdSpend = Sample.Spend(From, Days, Cur.Net); PrevAdSpend = Sample.Spend(PrevFrom, Days, Prev.Net);
            Random rnd = Sample.Rng("roas" + From.ToString("yyyy-MM-dd"));
            string[] names = { "Meta", "TikTok", "Google" }; decimal[] split = { 0.52m, 0.31m, 0.17m };
            for (int i = 0; i < 3; i++)
            {
                Platform p = new Platform(); p.Name = names[i]; p.Spend = Math.Round(AdSpend * split[i], 2);
                p.Roas = Math.Round((decimal)(2.1 + rnd.NextDouble() * 2.5), 2); p.Revenue = Math.Round(p.Spend * p.Roas, 2);
                Platforms.Add(p);
            }
            string[] devices = { "Mobile", "Desktop", "Tablet" }; double[] share = { 0.75, 0.19, 0.06 }, cvr = { 1.7, 2.9, 2.2 };
            for (int i = 0; i < 3; i++)
            {
                Random a = Sample.Rng("dev" + devices[i] + From.ToString("yyyy-MM-dd") + Days), b = Sample.Rng("devprev" + devices[i] + From.ToString("yyyy-MM-dd") + Days);
                DeviceRow d = new DeviceRow(); d.Device = devices[i]; d.Share = share[i] * 100;
                d.Sessions = (int)Math.Round(Days * (820 + a.NextDouble() * 280) * share[i]);
                d.Cvr = Math.Round(cvr[i] * (0.72 + a.NextDouble() * 0.4), 2); d.PrevCvr = Math.Round(cvr[i] * (0.9 + b.NextDouble() * 0.2), 2);
                d.Drop = d.Cvr < d.PrevCvr * 0.8;
                Devices.Add(d);
            }
            Random u = Sample.Rng("unfulfilled" + To.ToString("yyyy-MM-dd"));
            string[] first = { "Andrea", "Bea", "Carla", "Dani", "Ella", "Faye", "Gia", "Hana", "Isa", "Jam", "Kim", "Lia", "Mika", "Nina", "Pia", "Rica" };
            string[] last = { "S.", "R.", "C.", "B.", "G.", "M.", "T.", "F.", "A.", "L." };
            string[] couriers = { "J&T Express", "LBC", "Ninja Van", "Flash Express" }, regions = { "Metro Manila", "Luzon", "Visayas", "Mindanao" };
            int count = u.Next(6, 11);
            for (int i = 0; i < count; i++)
            {
                UnfulfilledRow o = new UnfulfilledRow();
                o.Order = "ON-" + u.Next(24000, 27000); o.Customer = first[u.Next(first.Length)] + " " + last[u.Next(last.Length)];
                o.Items = u.Next(1, 5); o.AgeHours = 3 + u.Next(74);
                o.Bucket = o.AgeHours >= 48 ? "48h+" : o.AgeHours >= 24 ? "24-48h" : "< 24h";
                o.Courier = couriers[u.Next(4)]; o.Region = regions[u.Next(4)]; o.Payment = u.Next(10) < 6 ? "COD" : "Prepaid";
                o.Amount = Math.Round((decimal)(690 + u.NextDouble() * 4110), 2);
                Unfulfilled.Add(o);
            }
            Unfulfilled = Unfulfilled.OrderByDescending(o => o.AgeHours).ToList();
            NewCustomers = Math.Max(1, (int)Math.Round(Math.Max(1, Cur.Orders) * 0.62));
            Cac = Math.Round(AdSpend / NewCustomers, 2);

            Random rt = Sample.Rng("rto" + From.ToString("yyyy-MM-dd") + Days);
            double[] cBase = { 11, 8, 13, 16 }, rBase = { 7.5, 11.5, 15.5, 19 };
            for (int i = 0; i < 4; i++) Couriers.Add(Rto(couriers[i], cBase[i], rt));
            for (int i = 0; i < 4; i++) Regions.Add(Rto(regions[i], rBase[i], rt));

            Random lt = Sample.Rng("ltv");
            decimal baseAov = Cur.Aov > 0 ? Cur.Aov : 1650m, value = baseAov; double repeat = 0.34;
            for (int m = 0; m < 12; m++) { Ltv[m] = Math.Round(value, 2); value += baseAov * (decimal)repeat; repeat *= 0.78 + lt.NextDouble() * 0.08; }
            RepeatRate = Math.Round(24 + lt.NextDouble() * 9, 1); FirstTimeShare = Math.Round(58 + lt.NextDouble() * 10, 1);

            DateTime asOf = To.AddDays(1);
            for (int c = 11; c >= 0; c--)
            {
                DateTime month = new DateTime(asOf.AddDays(-1).Year, asOf.AddDays(-1).Month, 1).AddMonths(-c);
                Random cr = Sample.Rng("cohort" + month.ToString("yyyy-MM"));
                CohortRow row = new CohortRow(); row.Cohort = month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                row.Cells.Add(100);
                double rate = 19 + cr.NextDouble() * 12;
                for (int m = 1; m <= c; m++) { row.Cells.Add(Math.Round(rate, 1)); rate *= 0.8 + cr.NextDouble() * 0.13; }
                row.Customers = cr.Next(80, 261);
                Cohorts.Add(row);
            }

            // Typical apparel shrinkage is about 1–2 % of stock a year; scale the sample to this shop's real stock.
            Random sh = Sample.Rng("shrink" + From.ToString("yyyy-MM-dd") + Days);
            decimal avgCost = SystemUnits > 0 ? SystemValue / SystemUnits : 400m;
            double yearly = SystemUnits * (0.01 + sh.NextDouble() * 0.01) * Days / 365.0;
            string[] reasons = { "Damaged", "Count correction", "Lost / missing" }; double[] split2 = { 0.45, 0.35, 0.20 };
            for (int i = 0; i < reasons.Length; i++)
            {
                ShrinkRow s = new ShrinkRow(); s.Reason = reasons[i]; s.Units = (int)Math.Round(yearly * split2[i]);
                s.Cost = Math.Round(s.Units * avgCost, 2);
                Shrinkage.Add(s);
            }
        }

        static RtoRow Rto(string name, double baseRate, Random rnd)
        {
            RtoRow r = new RtoRow(); r.Name = name;
            r.Orders = (int)Math.Round(30 * (4 + rnd.NextDouble() * 5));
            r.Rate = Math.Round(baseRate * (0.8 + rnd.NextDouble() * 0.45), 1);
            r.Rto = (int)Math.Round(r.Orders * r.Rate / 100); r.High = r.Rate >= 15;
            return r;
        }

        /// <summary>Repeatable sample numbers (FNV-1a seeded, so the same dates always give the same values).</summary>
        static class Sample
        {
            public static Random Rng(string key)
            {
                unchecked
                {
                    uint h = 2166136261;
                    foreach (char c in key) { h ^= c; h *= 16777619; }
                    return new Random((int)(h & 0x7FFFFFFF));
                }
            }

            /// <summary>Sample spend sized to the shop: about 10–16 % of real net sales plus a small always-on budget.</summary>
            public static decimal Spend(DateTime from, int days, decimal net)
            {
                Random r = Rng("spend" + from.ToString("yyyy-MM-dd") + days);
                double ratio = 0.10 + r.NextDouble() * 0.06, baseline = days * 150 * (0.8 + r.NextDouble() * 0.4);
                return Math.Round(Math.Max(0m, net) * (decimal)ratio + (decimal)baseline, 2);
            }

            public static Dictionary<string, int> ReturnReasons(string sku, int units)
            {
                Random r = Rng("reasons" + sku);
                string[] names = { "Too small", "Too large", "Fabric / quality", "Changed mind", "Not as pictured" };
                double[] w = { 2 + r.NextDouble() * 3, 1 + r.NextDouble() * 2, 0.5 + r.NextDouble() * 1.5, 0.5 + r.NextDouble() * 2, 0.2 + r.NextDouble() };
                double sum = w.Sum();
                Dictionary<string, int> outp = new Dictionary<string, int>();
                int left = units;
                for (int i = 0; i < names.Length; i++) { int n = (int)Math.Floor(units * w[i] / sum); outp[names[i]] = n; left -= n; }
                foreach (int idx in Enumerable.Range(0, names.Length).OrderByDescending(k => w[k])) { if (left <= 0) break; outp[names[idx]]++; left--; }
                return outp.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            }
        }
    }
}
