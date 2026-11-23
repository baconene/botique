using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;

namespace BoutiquePOS
{
    public class LabelLayout
    {
        public string Key;
        public string Name;
        public bool IsSheet;
        public float W, H;               // label size, inches
        public int Cols = 1, Rows = 1;   // sheets only
        public float Left, Top, PitchX, PitchY;
        public int PerPage { get { return IsSheet ? Cols * Rows : 1; } }
        public override string ToString() { return Name; }
    }

    public static class PrintUtil
    {
        public static void SetPrinter(PrintDocument doc, string printerName)
        {
            if (string.IsNullOrEmpty(printerName)) return;
            doc.PrinterSettings.PrinterName = printerName;
            if (!doc.PrinterSettings.IsValid) doc.PrinterSettings = new PrinterSettings(); // fall back to default
        }

        public static bool IsPreview(PrintDocument doc)
        {
            return doc.PrintController != null && doc.PrintController.IsPreview;
        }
    }

    public static class LabelPrinter
    {
        public static readonly List<LabelLayout> Layouts = new List<LabelLayout>
        {
            Sheet("Avery5160", "Avery 5160 / 8160 sheet - 30 per page (2.625\" x 1\")", 2.625f, 1f, 3, 10, 0.1875f, 0.5f, 2.75f, 1f),
            Sheet("Avery5163", "Avery 5163 / 8163 sheet - 10 per page (4\" x 2\")", 4f, 2f, 2, 5, 0.15625f, 0.5f, 4.1875f, 2f),
            Roll("Roll2x1", "Thermal roll label 2\" x 1\" (Zebra, Rollo, Munbyn...)", 2f, 1f),
            Roll("Roll225x125", "Thermal roll label 2.25\" x 1.25\"", 2.25f, 1.25f),
            Roll("Roll15x1", "Thermal roll label 1.5\" x 1\"", 1.5f, 1f),
            Roll("Tag2x3", "Hang tag 2\" x 3\"", 2f, 3f)
        };

        static LabelLayout Sheet(string key, string name, float w, float h, int cols, int rows, float left, float top, float px, float py)
        {
            LabelLayout l = new LabelLayout();
            l.Key = key; l.Name = name; l.IsSheet = true; l.W = w; l.H = h; l.Cols = cols; l.Rows = rows;
            l.Left = left; l.Top = top; l.PitchX = px; l.PitchY = py;
            return l;
        }

        static LabelLayout Roll(string key, string name, float w, float h)
        {
            LabelLayout l = new LabelLayout();
            l.Key = key; l.Name = name; l.IsSheet = false; l.W = w; l.H = h;
            return l;
        }

        public static LabelLayout Find(string key)
        {
            foreach (LabelLayout l in Layouts) if (l.Key == key) return l;
            return Layouts[0];
        }

        /// <summary>Draws one label. Graphics must use GraphicsUnit.Pixel; r is in device pixels.</summary>
        public static void DrawLabel(Graphics g, Product p, RectangleF r, float dpiX, float dpiY, string storeName, bool outline)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float wIn = r.Width / dpiX, hIn = r.Height / dpiY;
            float fs = Math.Max(0.7f, Math.Min(1.6f, Math.Min(hIn, wIn / 2f)));
            bool tall = hIn > wIn * 1.2f;
            float padX = 0.07f * dpiX, padY = 0.05f * dpiY;
            RectangleF inner = new RectangleF(r.X + padX, r.Y + padY, r.Width - 2 * padX, r.Height - 2 * padY);

            if (outline)
            {
                using (Pen pen = new Pen(Color.FromArgb(190, 190, 190), 1f))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
            }

            using (Font fStore = new Font("Segoe UI Semibold", 7.5f * fs, GraphicsUnit.Point))
            using (Font fName = new Font("Segoe UI Semibold", 7.5f * fs, GraphicsUnit.Point))
            using (Font fVar = new Font("Segoe UI", 6.5f * fs, GraphicsUnit.Point))
            using (Font fPrice = new Font("Segoe UI", 11f * fs, FontStyle.Bold, GraphicsUnit.Point))
            using (Font fCode = new Font("Consolas", 6.5f * fs, GraphicsUnit.Point))
            using (StringFormat left = new StringFormat(StringFormatFlags.NoWrap))
            using (StringFormat center = new StringFormat(StringFormatFlags.NoWrap))
            using (StringFormat right = new StringFormat(StringFormatFlags.NoWrap))
            {
                left.Trimming = StringTrimming.EllipsisCharacter;
                center.Alignment = StringAlignment.Center; center.Trimming = StringTrimming.EllipsisCharacter;
                right.Alignment = StringAlignment.Far;

                float y = inner.Y;
                string price = Ui.Money(p.Price);
                if (tall)
                {
                    if (!string.IsNullOrEmpty(storeName))
                    {
                        g.DrawString(storeName.ToUpperInvariant(), fStore, Brushes.Black, new RectangleF(inner.X, y, inner.Width, fStore.GetHeight(g)), center);
                        y += fStore.GetHeight(g) * 1.6f;
                    }
                    g.DrawString(p.Name, fName, Brushes.Black, new RectangleF(inner.X, y, inner.Width, fName.GetHeight(g)), center);
                    y += fName.GetHeight(g);
                    g.DrawString(p.Variant, fVar, Brushes.Black, new RectangleF(inner.X, y, inner.Width, fVar.GetHeight(g)), center);
                    y += fVar.GetHeight(g) * 1.3f;
                    g.DrawString(price, fPrice, Brushes.Black, new RectangleF(inner.X, y, inner.Width, fPrice.GetHeight(g)), center);
                    y += fPrice.GetHeight(g) * 1.3f;
                }
                else
                {
                    float priceW = g.MeasureString(price, fPrice).Width;
                    float rowH = Math.Max(fName.GetHeight(g) + fVar.GetHeight(g), fPrice.GetHeight(g));
                    g.DrawString(price, fPrice, Brushes.Black, new RectangleF(inner.Right - priceW, y, priceW, fPrice.GetHeight(g)), right);
                    float textW = Math.Max(10, inner.Width - priceW - 0.04f * dpiX);
                    g.DrawString(p.Name, fName, Brushes.Black, new RectangleF(inner.X, y, textW, fName.GetHeight(g)), left);
                    g.DrawString(p.Variant, fVar, Brushes.Black, new RectangleF(inner.X, y + fName.GetHeight(g), textW, fVar.GetHeight(g)), left);
                    y += rowH + 0.02f * dpiY;
                }

                float codeH = fCode.GetHeight(g);
                float barsBottom = inner.Bottom - codeH;
                int barsH = (int)(barsBottom - y);
                if (barsH > 4 && Code128.IsValid(p.Barcode))
                {
                    Rectangle bars = Code128.DrawBars(g, p.Barcode,
                        new Rectangle((int)r.X, (int)y, (int)r.Width, barsH));
                    g.DrawString(p.Barcode, fCode, Brushes.Black, new RectangleF(inner.X, barsBottom, inner.Width, codeH), center);
                }
                else
                {
                    g.DrawString(p.Barcode, fCode, Brushes.Black, new RectangleF(inner.X, barsBottom, inner.Width, codeH), center);
                }
            }
        }

        public static Bitmap RenderLabel(Product p, LabelLayout layout, int dpi, string storeName)
        {
            Bitmap bmp = new Bitmap((int)(layout.W * dpi), (int)(layout.H * dpi));
            bmp.SetResolution(dpi, dpi);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.Clear(Color.White);
                DrawLabel(g, p, new RectangleF(0, 0, bmp.Width, bmp.Height), dpi, dpi, storeName, true);
            }
            return bmp;
        }

        public static PrintDocument CreateDocument(List<Product> labels, LabelLayout layout, int startPosition, string printerName, string storeName)
        {
            PrintDocument doc = new PrintDocument();
            doc.DocumentName = "Boutique labels";
            PrintUtil.SetPrinter(doc, printerName);
            PageSettings ps = doc.DefaultPageSettings;
            ps.Landscape = false;
            ps.Margins = new Margins(0, 0, 0, 0);
            if (layout.IsSheet)
            {
                PaperSize letter = null;
                foreach (PaperSize s in doc.PrinterSettings.PaperSizes) if (s.Kind == PaperKind.Letter) { letter = s; break; }
                ps.PaperSize = letter ?? new PaperSize("Letter", 850, 1100);
            }
            else
            {
                ps.PaperSize = new PaperSize("Label " + layout.W + "x" + layout.H, (int)Math.Round(layout.W * 100), (int)Math.Round(layout.H * 100));
            }

            int firstPos = layout.IsSheet ? Math.Max(0, Math.Min(startPosition - 1, layout.PerPage - 1)) : 0;
            int index = 0, pos = firstPos;
            doc.BeginPrint += delegate { index = 0; pos = firstPos; };
            doc.PrintPage += delegate(object sender, PrintPageEventArgs e)
            {
                Graphics g = e.Graphics;
                g.PageUnit = GraphicsUnit.Pixel;
                float dx = g.DpiX, dy = g.DpiY;
                bool preview = PrintUtil.IsPreview(doc);
                float offX = preview ? 0 : e.PageSettings.HardMarginX / 100f;
                float offY = preview ? 0 : e.PageSettings.HardMarginY / 100f;
                if (labels.Count == 0) { e.HasMorePages = false; return; }

                if (layout.IsSheet)
                {
                    while (pos < layout.PerPage && index < labels.Count)
                    {
                        int col = pos % layout.Cols, row = pos / layout.Cols;
                        float xIn = layout.Left + col * layout.PitchX - offX;
                        float yIn = layout.Top + row * layout.PitchY - offY;
                        DrawLabel(g, labels[index], new RectangleF(xIn * dx, yIn * dy, layout.W * dx, layout.H * dy), dx, dy, storeName, preview);
                        index++; pos++;
                    }
                    pos = 0;
                }
                else
                {
                    DrawLabel(g, labels[index], new RectangleF(-offX * dx, -offY * dy, layout.W * dx, layout.H * dy), dx, dy, storeName, false);
                    index++;
                }
                e.HasMorePages = index < labels.Count;
            };
            return doc;
        }
    }

    public static class ReceiptPrinter
    {
        static bool Narrow(StoreSettings s) { return s.ReceiptPaper == "58mm"; }
        public static int CharsPerLine(StoreSettings s) { return Narrow(s) ? 32 : 42; }
        public static float PaperWidthIn(StoreSettings s) { return Narrow(s) ? 2.28f : 3.15f; }
        static float BodyPt(StoreSettings s) { return Narrow(s) ? 7f : 8f; }
        const float Margin = 0.1f, BarcodeH = 0.45f;

        static string LR(string left, string right, int w)
        {
            left = left ?? ""; right = right ?? "";
            int room = w - right.Length - 1;
            if (room < 0) room = 0;
            if (left.Length > room) left = left.Substring(0, room);
            return left + new string(' ', Math.Max(1, w - left.Length - right.Length)) + right;
        }

        static string Center(string s, int w)
        {
            s = (s ?? "").Trim();
            if (s.Length >= w) return s.Substring(0, w);
            return new string(' ', (w - s.Length) / 2) + s;
        }

        public static List<string> BodyLines(Sale sale, StoreSettings st)
        {
            int w = CharsPerLine(st);
            string rule = new string('-', w);
            List<string> L = new List<string>();
            if (!string.IsNullOrEmpty(st.Address)) L.Add(Center(st.Address, w));
            if (!string.IsNullOrEmpty(st.Phone)) L.Add(Center(st.Phone, w));
            L.Add("");
            L.Add(LR("Receipt " + sale.Code, sale.Date.ToString("yyyy-MM-dd HH:mm"), w));
            L.Add(rule);
            foreach (SaleLine l in sale.Lines)
            {
                L.Add(l.Name.Length > w ? l.Name.Substring(0, w) : l.Name);
                string detail = "  " + l.Quantity + " x " + Ui.Money(l.UnitPrice);
                if (l.Variant.Length > 0) detail += "  " + l.Variant;
                L.Add(LR(detail, Ui.Money(l.LineTotal), w));
            }
            L.Add(rule);
            L.Add(LR("Items", sale.ItemCount.ToString(), w));
            L.Add(LR("Subtotal", Ui.Money(sale.Subtotal), w));
            if (sale.DiscountAmount != 0) L.Add(LR("Discount (" + sale.DiscountPercent.ToString("0.##") + "%)", "-" + Ui.Money(sale.DiscountAmount), w));
            L.Add(LR("Tax (" + sale.TaxRate.ToString("0.###") + "%)", Ui.Money(sale.Tax), w));
            L.Add(LR("TOTAL", Ui.Money(sale.Total), w));
            L.Add("");
            L.Add(LR(sale.PaymentMethod, Ui.Money(sale.AmountTendered), w));
            if (sale.PaymentMethod == "Cash") L.Add(LR("Change", Ui.Money(sale.Change), w));
            if (sale.Refunded)
            {
                L.Add("");
                L.Add(Center("*** REFUNDED " + (sale.RefundDate.HasValue ? sale.RefundDate.Value.ToString("d") : "") + " ***", w));
            }
            L.Add(rule);
            foreach (string f in (st.ReceiptFooter ?? "").Replace("\r", "").Split('\n'))
                if (f.Trim().Length > 0) L.Add(Center(f, w));
            return L;
        }

        public static float HeightIn(Sale sale, StoreSettings st)
        {
            int lines = BodyLines(sale, st).Count;
            return Margin * 2 + 0.35f + lines * BodyPt(st) * 1.25f / 72f + BarcodeH + 0.45f;
        }

        /// <summary>Draws the receipt with the Graphics in pixel units.</summary>
        public static void Draw(Graphics g, float dpiX, float dpiY, float originX, float originY, Sale sale, StoreSettings st)
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float widthPx = (PaperWidthIn(st) - 2 * Margin) * dpiX;
            float x = originX + Margin * dpiX;
            float y = originY + Margin * dpiY;
            using (Font fHead = new Font("Segoe UI", Narrow(st) ? 11f : 13f, FontStyle.Bold, GraphicsUnit.Point))
            using (Font fBody = new Font("Consolas", BodyPt(st), GraphicsUnit.Point))
            using (StringFormat center = new StringFormat())
            {
                center.Alignment = StringAlignment.Center;
                g.DrawString(st.StoreName, fHead, Brushes.Black, new RectangleF(x, y, widthPx, fHead.GetHeight(g) * 2), center);
                y += fHead.GetHeight(g) * 1.15f;
                float lh = fBody.GetHeight(g);
                foreach (string line in BodyLines(sale, st))
                {
                    g.DrawString(line, fBody, Brushes.Black, x, y);
                    y += lh;
                }
                y += lh * 0.5f;
                int barH = (int)(BarcodeH * dpiY);
                Code128.DrawBars(g, sale.Code, new Rectangle((int)x, (int)y, (int)widthPx, barH));
                y += barH;
                g.DrawString(sale.Code, fBody, Brushes.Black, new RectangleF(x, y, widthPx, lh), center);
            }
        }

        public static PrintDocument CreateDocument(Sale sale, StoreSettings st)
        {
            PrintDocument doc = new PrintDocument();
            doc.DocumentName = "Receipt " + sale.Code;
            PrintUtil.SetPrinter(doc, st.ReceiptPrinter);
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
            doc.DefaultPageSettings.PaperSize = new PaperSize("Receipt",
                (int)Math.Round(PaperWidthIn(st) * 100), (int)Math.Round(HeightIn(sale, st) * 100));
            doc.PrintPage += delegate(object sender, PrintPageEventArgs e)
            {
                e.Graphics.PageUnit = GraphicsUnit.Pixel;
                bool preview = PrintUtil.IsPreview(doc);
                float ox = preview ? 0 : -e.PageSettings.HardMarginX / 100f * e.Graphics.DpiX;
                float oy = preview ? 0 : -e.PageSettings.HardMarginY / 100f * e.Graphics.DpiY;
                Draw(e.Graphics, e.Graphics.DpiX, e.Graphics.DpiY, Math.Max(0, ox), Math.Max(0, oy), sale, st);
                e.HasMorePages = false;
            };
            return doc;
        }

        public static void Print(Sale sale, StoreSettings st)
        {
            using (PrintDocument doc = CreateDocument(sale, st)) doc.Print();
        }

        public static Bitmap Render(Sale sale, StoreSettings st, int dpi)
        {
            Bitmap bmp = new Bitmap((int)(PaperWidthIn(st) * dpi), (int)(HeightIn(sale, st) * dpi));
            bmp.SetResolution(dpi, dpi);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.Clear(Color.White);
                Draw(g, dpi, dpi, 0, 0, sale, st);
            }
            return bmp;
        }
    }
}
