using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace BoutiquePOS
{
    public class Product
    {
        public string Id { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Brand { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public decimal Price { get; set; }
        public decimal Cost { get; set; }
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }
        public string Notes { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }

        public Product()
        {
            Id = Guid.NewGuid().ToString("N");
            Barcode = ""; Name = ""; Category = ""; Brand = ""; Size = ""; Color = ""; Notes = "";
            ReorderLevel = 2;
            Created = DateTime.Now;
            Updated = Created;
        }

        [XmlIgnore]
        public string Variant { get { return Join(Size, Color); } }

        [XmlIgnore]
        public bool IsLowStock { get { return Quantity <= ReorderLevel; } }

        public Product Clone() { return (Product)MemberwiseClone(); }

        public void CopyFrom(Product o)
        {
            Barcode = o.Barcode; Name = o.Name; Category = o.Category; Brand = o.Brand; Size = o.Size; Color = o.Color;
            Price = o.Price; Cost = o.Cost; Quantity = o.Quantity; ReorderLevel = o.ReorderLevel; Notes = o.Notes; Updated = o.Updated;
        }

        internal static string Join(string a, string b)
        {
            a = (a ?? "").Trim(); b = (b ?? "").Trim();
            if (a.Length > 0 && b.Length > 0) return a + " / " + b;
            return a + b;
        }
    }

    public class SaleLine
    {
        public string ProductId { get; set; }
        public string Barcode { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        public SaleLine() { ProductId = ""; Barcode = ""; Name = ""; Size = ""; Color = ""; Quantity = 1; }

        public static SaleLine FromProduct(Product p)
        {
            SaleLine l = new SaleLine();
            l.ProductId = p.Id; l.Barcode = p.Barcode; l.Name = p.Name; l.Size = p.Size; l.Color = p.Color;
            l.UnitPrice = p.Price; l.Quantity = 1;
            return l;
        }

        [XmlIgnore] public decimal LineTotal { get { return UnitPrice * Quantity; } }
        [XmlIgnore] public string Variant { get { return Product.Join(Size, Color); } }
        public SaleLine Clone() { return (SaleLine)MemberwiseClone(); }
    }

    public class Sale
    {
        public int Number { get; set; }
        public DateTime Date { get; set; }
        public List<SaleLine> Lines { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountTendered { get; set; }
        public decimal Change { get; set; }
        public bool Refunded { get; set; }
        public DateTime? RefundDate { get; set; }
        public string Note { get; set; }

        public Sale() { Lines = new List<SaleLine>(); PaymentMethod = "Cash"; Note = ""; Date = DateTime.Now; }

        [XmlIgnore] public string Code { get { return DataStore.SaleCode(Number); } }
        [XmlIgnore] public int ItemCount { get { int n = 0; foreach (SaleLine l in Lines) n += l.Quantity; return n; } }
        [XmlIgnore] public string Status { get { return Refunded ? "Refunded" : "Completed"; } }
    }

    public class StoreSettings
    {
        public string StoreName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string ReceiptFooter { get; set; }
        public decimal TaxRatePercent { get; set; }
        public string BarcodePrefix { get; set; }

        /// <summary>"Keyboard" (HID, default) or "Serial" (SPP COM port).</summary>
        public string ScannerMode { get; set; }
        public string SerialPortName { get; set; }
        public int BaudRate { get; set; }

        public string ReceiptPrinter { get; set; }
        public string ReceiptPaper { get; set; }   // "80mm" or "58mm"
        public bool AutoPrintReceipt { get; set; }
        public string LabelPrinter { get; set; }
        public string LabelLayout { get; set; }

        public StoreSettings()
        {
            StoreName = "My Boutique";
            Address = "123 Main Street";
            Phone = "(555) 555-0100";
            ReceiptFooter = "Thank you for shopping with us!\nExchanges within 14 days with receipt.";
            TaxRatePercent = 7m;
            BarcodePrefix = "BTQ";
            ScannerMode = "Keyboard";
            SerialPortName = "";
            BaudRate = 9600;
            ReceiptPrinter = "";
            ReceiptPaper = "80mm";
            AutoPrintReceipt = false;
            LabelPrinter = "";
            LabelLayout = "Avery5160";
        }
    }

    public class AppData
    {
        public List<Product> Products { get; set; }
        public List<Sale> Sales { get; set; }
        public StoreSettings Settings { get; set; }
        public int NextItemNumber { get; set; }
        public int NextSaleNumber { get; set; }

        public AppData()
        {
            Products = new List<Product>();
            Sales = new List<Sale>();
            Settings = new StoreSettings();
            NextItemNumber = 1;
            NextSaleNumber = 1001;
        }
    }

    public static class SaleMath
    {
        public static decimal R(decimal d) { return Math.Round(d, 2, MidpointRounding.AwayFromZero); }

        public static void Compute(IEnumerable<SaleLine> lines, decimal discountPercent, decimal taxRatePercent,
            out decimal subtotal, out decimal discount, out decimal tax, out decimal total)
        {
            subtotal = 0m;
            foreach (SaleLine l in lines) subtotal += l.LineTotal;
            subtotal = R(subtotal);
            if (discountPercent < 0) discountPercent = 0;
            if (discountPercent > 100) discountPercent = 100;
            discount = R(subtotal * discountPercent / 100m);
            decimal taxable = subtotal - discount;
            tax = R(taxable * taxRatePercent / 100m);
            total = taxable + tax;
        }
    }
}
