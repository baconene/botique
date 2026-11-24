using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BoutiquePOS
{
    /// <summary>
    /// "Completing sale..." window. Runs the save on a background thread so the app never looks frozen,
    /// and can't be closed until the outcome is known.
    /// </summary>
    public class ProcessingForm : Form
    {
        readonly Action work;
        readonly Label lblSlow;
        readonly Timer slowTimer;
        bool finished;
        public Exception Error { get; private set; }

        public ProcessingForm(string title, string detail, string where, Action work)
        {
            this.work = work;
            Text = "Please wait";
            Font = new Font("Segoe UI", 10.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;   // no X: the sale must finish first
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;
            Padding = new Padding(Ui.S(28), Ui.S(22), Ui.S(28), Ui.S(22));

            TableLayoutPanel t = new TableLayoutPanel();
            t.AutoSize = true; t.ColumnCount = 1; t.Dock = DockStyle.Fill;
            Label h = new Label(); h.Text = title; h.AutoSize = true; h.UseMnemonic = false;
            h.Font = new Font("Segoe UI Semibold", 15f); h.ForeColor = Ui.AccentDark;
            Label d = new Label(); d.Text = detail; d.AutoSize = true; d.UseMnemonic = false;
            d.Font = new Font("Segoe UI", 12f); d.Margin = new Padding(0, Ui.S(4), 0, Ui.S(12));
            ProgressBar bar = new ProgressBar(); bar.Style = ProgressBarStyle.Marquee; bar.MarqueeAnimationSpeed = 25;
            bar.Width = Ui.S(380); bar.Height = Ui.S(10);
            Label w = new Label(); w.Text = "Saving to " + where; w.AutoSize = true; w.ForeColor = Ui.Muted; w.UseMnemonic = false;
            w.MaximumSize = new Size(Ui.S(380), 0); w.Margin = new Padding(0, Ui.S(10), 0, 0);
            lblSlow = new Label(); lblSlow.AutoSize = true; lblSlow.MaximumSize = new Size(Ui.S(380), 0);
            lblSlow.ForeColor = Color.FromArgb(154, 98, 0); lblSlow.Margin = new Padding(0, Ui.S(8), 0, 0);
            t.Controls.Add(h); t.Controls.Add(d); t.Controls.Add(bar); t.Controls.Add(w); t.Controls.Add(lblSlow);
            Controls.Add(t);

            slowTimer = new Timer();
            slowTimer.Interval = 4000;
            slowTimer.Tick += delegate
            {
                slowTimer.Stop();
                lblSlow.Text = "Still working - the connection is slow. Please wait and don't close the app; " +
                               "you'll see whether the sale was completed.";
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            slowTimer.Start();
            Task.Factory.StartNew(work).ContinueWith(delegate(Task task)
            {
                slowTimer.Stop();
                Error = task.Exception != null ? task.Exception.InnerException : null;
                finished = true;
                DialogResult = DialogResult.OK;
                Close();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!finished) e.Cancel = true;   // Alt+F4 can't abandon a sale mid-save
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) slowTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    public enum SaleOutcome { Completed, Failed, Uncertain }

    /// <summary>The unmistakable answer to "did that sale go through?"</summary>
    public class SaleResultForm : Form
    {
        public bool RetryRequested { get; private set; }
        readonly Label lblReceipt;

        static readonly Color GoodBg = Color.FromArgb(230, 244, 236);
        static readonly Color BadBg = Color.FromArgb(253, 236, 234);
        static readonly Color WarnBg = Color.FromArgb(253, 243, 220);
        static readonly Color BadFg = Color.FromArgb(180, 35, 24);
        static readonly Color WarnFg = Color.FromArgb(154, 98, 0);

        /// <param name="printReceipt">Prints the receipt and returns a status line (used by the Print button).</param>
        public SaleResultForm(SaleOutcome outcome, Sale sale, decimal attemptedTotal, string receiptStatus, string error, Func<string> printReceipt)
        {
            Font = new Font("Segoe UI", 10.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;
            KeyPreview = true;

            bool ok = outcome == SaleOutcome.Completed;
            Color bg = ok ? GoodBg : outcome == SaleOutcome.Failed ? BadBg : WarnBg;
            Color fg = ok ? Ui.Good : outcome == SaleOutcome.Failed ? BadFg : WarnFg;
            Text = ok ? "Sale " + sale.Code + " completed" : outcome == SaleOutcome.Failed ? "Sale not completed" : "Sale not confirmed";

            // ---- coloured banner ----
            Panel band = new Panel(); band.Dock = DockStyle.Top; band.BackColor = bg;
            band.Padding = new Padding(Ui.S(24), Ui.S(18), Ui.S(24), Ui.S(18)); band.AutoSize = true;
            TableLayoutPanel bt = new TableLayoutPanel(); bt.AutoSize = true; bt.ColumnCount = 2; bt.Dock = DockStyle.Fill;
            bt.BackColor = bg;
            Label icon = new Label(); icon.AutoSize = true; icon.ForeColor = fg; icon.Font = new Font("Segoe UI Symbol", 30f, FontStyle.Bold);
            icon.Text = ok ? "✔" : outcome == SaleOutcome.Failed ? "✖" : "⚠";
            icon.Margin = new Padding(0, 0, Ui.S(14), 0);
            Label head = new Label(); head.AutoSize = true; head.ForeColor = fg; head.UseMnemonic = false;
            head.Font = new Font("Segoe UI Semibold", 18f);
            head.Text = ok ? "Sale completed" : outcome == SaleOutcome.Failed ? "Sale NOT completed" : "Couldn't confirm the sale";
            head.Anchor = AnchorStyles.Left;
            bt.Controls.Add(icon, 0, 0); bt.Controls.Add(head, 1, 0);
            band.Controls.Add(bt);

            // ---- body ----
            TableLayoutPanel body = new TableLayoutPanel();
            body.AutoSize = true; body.ColumnCount = 2; body.Dock = DockStyle.Fill;
            body.Padding = new Padding(Ui.S(24), Ui.S(16), Ui.S(24), Ui.S(8));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            if (ok)
            {
                bool cashChange = sale.PaymentMethod == "Cash" && sale.Change > 0;
                Label big = new Label(); big.AutoSize = true; big.UseMnemonic = false;
                big.Font = new Font("Segoe UI", 30f, FontStyle.Bold); big.ForeColor = Ui.AccentDark;
                big.Text = cashChange ? "Change due  " + Ui.Money(sale.Change) : "Paid  " + Ui.Money(sale.Total);
                big.Margin = new Padding(0, 0, 0, Ui.S(10));
                body.Controls.Add(big); body.SetColumnSpan(big, 2);
                Row(body, "Receipt", sale.Code);
                Row(body, "Total", Ui.Money(sale.Total));
                Row(body, "Payment", sale.PaymentMethod + (sale.PaymentMethod == "Cash" ? "  (received " + Ui.Money(sale.AmountTendered) + ")" : ""));
                Row(body, "Items", sale.ItemCount.ToString());
                Row(body, "Time", sale.Date.ToString("MMM d, yyyy  h:mm tt", System.Globalization.CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(sale.Cashier)) Row(body, "Cashier", sale.Cashier);
                Row(body, "Stock", "Updated");
            }
            else
            {
                Label msg = new Label(); msg.AutoSize = true; msg.MaximumSize = new Size(Ui.S(460), 0); msg.UseMnemonic = false;
                msg.Font = new Font("Segoe UI", 11.5f);
                msg.Text = outcome == SaleOutcome.Failed
                    ? "Nothing was charged and no stock was removed. The items are still in the cart, so you can fix the problem and try again."
                    : "The connection dropped before the server answered, so we can't tell yet whether the sale was saved. " +
                      "Press Try again: it's safe. This sale can never be recorded twice, and you'll see the confirmation.";
                msg.Margin = new Padding(0, 0, 0, Ui.S(10));
                body.Controls.Add(msg); body.SetColumnSpan(msg, 2);
                Row(body, "Amount", Ui.Money(attemptedTotal) + " (not confirmed)");
                if (!string.IsNullOrEmpty(error))
                {
                    Label why = new Label(); why.AutoSize = true; why.MaximumSize = new Size(Ui.S(460), 0); why.UseMnemonic = false;
                    why.ForeColor = Ui.Muted; why.Text = "Details: " + error; why.Margin = new Padding(0, Ui.S(8), 0, 0);
                    body.Controls.Add(why); body.SetColumnSpan(why, 2);
                }
            }

            lblReceipt = new Label(); lblReceipt.AutoSize = true; lblReceipt.MaximumSize = new Size(Ui.S(460), 0); lblReceipt.UseMnemonic = false;
            lblReceipt.Margin = new Padding(0, Ui.S(10), 0, 0);
            if (ok) { SetReceiptStatus(receiptStatus); body.Controls.Add(lblReceipt); body.SetColumnSpan(lblReceipt, 2); }

            // ---- buttons ----
            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.AutoSize = true; buttons.Dock = DockStyle.Bottom; buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Padding = new Padding(Ui.S(20), Ui.S(6), Ui.S(20), Ui.S(18));
            if (ok)
            {
                Button next = Ui.MakeButton("New sale  (Enter)", delegate { DialogResult = DialogResult.OK; }, true);
                next.Font = new Font("Segoe UI Semibold", 11f);
                Button print = Ui.MakeButton(receiptStatus != null && receiptStatus.StartsWith("Receipt printed") ? "Print again" : "Print receipt", null);
                print.Click += delegate
                {
                    Cursor = Cursors.WaitCursor;
                    try { SetReceiptStatus(printReceipt()); } finally { Cursor = Cursors.Default; }
                    print.Text = "Print again";
                };
                buttons.Controls.Add(next); buttons.Controls.Add(print);
                AcceptButton = next; CancelButton = next;
                Shown += delegate { next.Focus(); };
            }
            else
            {
                Button retry = Ui.MakeButton("Try again", delegate { RetryRequested = true; DialogResult = DialogResult.OK; }, true);
                retry.Font = new Font("Segoe UI Semibold", 11f);
                Button back = Ui.MakeButton("Back to the sale", delegate { DialogResult = DialogResult.Cancel; });
                buttons.Controls.Add(retry); buttons.Controls.Add(back);
                AcceptButton = retry; CancelButton = back;
                Shown += delegate { retry.Focus(); };
            }

            Controls.Add(body);
            Controls.Add(buttons);
            Controls.Add(band);
        }

        void SetReceiptStatus(string status)
        {
            status = status ?? "";
            lblReceipt.Text = status;
            lblReceipt.ForeColor = status.StartsWith("Receipt printed") ? Ui.Good
                : status.StartsWith("Receipt could not") ? BadFg : Ui.Muted;
        }

        static void Row(TableLayoutPanel t, string label, string value)
        {
            Label a = new Label(); a.Text = label; a.AutoSize = true; a.ForeColor = Ui.Muted; a.UseMnemonic = false;
            a.Margin = new Padding(0, Ui.S(3), Ui.S(24), Ui.S(3));
            Label b = new Label(); b.Text = value; b.AutoSize = true; b.UseMnemonic = false;
            b.Font = new Font("Segoe UI Semibold", 10.5f); b.Margin = new Padding(0, Ui.S(3), 0, Ui.S(3));
            t.Controls.Add(a); t.Controls.Add(b);
        }
    }
}
