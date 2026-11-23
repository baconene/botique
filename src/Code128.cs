using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

namespace BoutiquePOS
{
    /// <summary>Code 128 subset B encoder (printable ASCII 32-126) with pixel-exact rendering.</summary>
    public static class Code128
    {
        // Bar/space module widths for code values 0..106 (106 = stop, 7 elements).
        static readonly string[] Patterns = {
            "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
            "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
            "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
            "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
            "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
            "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
            "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
            "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
            "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
            "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
            "114131","311141","411131","211412","211214","211232","2331112"
        };

        public const int StartB = 104;
        public const int Stop = 106;
        public const int QuietModules = 10;

        public static bool IsValid(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text) if (c < 32 || c > 126) return false;
            return true;
        }

        public static List<int> Codes(string text)
        {
            if (!IsValid(text)) throw new ArgumentException("Barcode must contain only printable characters (letters, digits, symbols).");
            List<int> codes = new List<int>();
            codes.Add(StartB);
            int sum = StartB;
            for (int i = 0; i < text.Length; i++)
            {
                int v = text[i] - 32;
                codes.Add(v);
                sum += v * (i + 1);
            }
            codes.Add(sum % 103);
            codes.Add(Stop);
            return codes;
        }

        public static List<int> ModuleWidths(string text)
        {
            List<int> w = new List<int>();
            foreach (int c in Codes(text))
                foreach (char ch in Patterns[c]) w.Add(ch - '0');
            return w;
        }

        /// <summary>Bars as '1' and spaces as '0', one char per module (no quiet zone).</summary>
        public static string ToBitString(string text)
        {
            StringBuilder sb = new StringBuilder();
            bool bar = true;
            foreach (int w in ModuleWidths(text)) { sb.Append(bar ? '1' : '0', w); bar = !bar; }
            return sb.ToString();
        }

        public static int CountModules(string text)
        {
            int t = 0;
            foreach (int w in ModuleWidths(text)) t += w;
            return t;
        }

        /// <summary>Self-check of the pattern table: every symbol must be 11 modules (stop = 13).</summary>
        public static bool TableIsConsistent()
        {
            for (int i = 0; i < Patterns.Length; i++)
            {
                int s = 0;
                foreach (char c in Patterns[i]) s += c - '0';
                if (s != (i == Stop ? 13 : 11)) return false;
            }
            return Patterns.Length == 107;
        }

        /// <summary>
        /// Draws the bars centred in <paramref name="area"/>. The Graphics must be in pixel units
        /// so that every module is an exact whole number of device pixels.
        /// </summary>
        public static Rectangle DrawBars(Graphics g, string text, Rectangle area)
        {
            List<int> widths = ModuleWidths(text);
            int modules = 0;
            foreach (int w in widths) modules += w;
            int mw = Math.Max(1, area.Width / (modules + 2 * QuietModules));
            int barsWidth = modules * mw;
            int x = area.X + (area.Width - barsWidth) / 2;
            int start = x;

            SmoothingMode oldSm = g.SmoothingMode;
            PixelOffsetMode oldPo = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.None;
            bool bar = true;
            foreach (int w in widths)
            {
                if (bar) g.FillRectangle(Brushes.Black, x, area.Y, w * mw, area.Height);
                x += w * mw;
                bar = !bar;
            }
            g.SmoothingMode = oldSm;
            g.PixelOffsetMode = oldPo;
            return new Rectangle(start, area.Y, barsWidth, area.Height);
        }

        /// <summary>Renders a stand-alone barcode image (used for "Save as PNG").</summary>
        public static Bitmap Render(string text, int moduleWidthPx, int barHeightPx, bool humanReadable)
        {
            int modules = CountModules(text) + 2 * QuietModules;
            int w = modules * moduleWidthPx;
            int textH = humanReadable ? Math.Max(14, barHeightPx / 4) : 0;
            Bitmap bmp = new Bitmap(w, barHeightPx + textH + 4);
            bmp.SetResolution(300, 300);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                g.Clear(Color.White);
                DrawBars(g, text, new Rectangle(0, 2, w, barHeightPx));
                if (humanReadable)
                {
                    using (Font f = new Font("Consolas", textH * 0.7f, GraphicsUnit.Pixel))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(text, f, Brushes.Black, new RectangleF(0, barHeightPx + 2, w, textH), sf);
                    }
                }
            }
            return bmp;
        }
    }
}
