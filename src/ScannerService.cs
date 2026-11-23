using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BoutiquePOS
{
    public delegate void ScanHandler(string code, string source);

    /// <summary>
    /// Receives barcodes from a scanner in either mode:
    ///  * Keyboard / HID (how most Bluetooth scanners pair): detects a fast burst of keystrokes ending
    ///    in Enter while no text box has focus. If a text box has focus the scanner simply types into it.
    ///  * Serial / SPP: reads lines from the Bluetooth virtual COM port.
    /// </summary>
    public sealed class ScannerService : IMessageFilter, IDisposable
    {
        const int WM_CHAR = 0x0102;
        [DllImport("user32.dll")] static extern IntPtr GetFocus();

        readonly Control invoker;
        readonly StringBuilder kbBuf = new StringBuilder();
        int firstTick, lastTick;

        SerialPort port;
        readonly StringBuilder serialBuf = new StringBuilder();
        System.Threading.Timer idleFlush;

        public event ScanHandler Scanned;

        /// <summary>Max gap between keystrokes (ms) still treated as part of one scan.</summary>
        public int MaxGapMs = 120;
        public int MinLength = 3;
        public bool KeyboardEnabled = true;
        public string SerialStatus { get; private set; }
        public bool SerialConnected { get { return port != null && port.IsOpen; } }

        public ScannerService(Control invoker)
        {
            this.invoker = invoker;
            SerialStatus = "Not connected";
        }

        // ---------------- Keyboard wedge (HID) ----------------

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_CHAR || !KeyboardEnabled) return false;
            if (IsTextInputFocused()) { kbBuf.Length = 0; return false; }

            char c = (char)(m.WParam.ToInt64() & 0xFFFF);
            int now = Environment.TickCount;

            if (kbBuf.Length > 0 && unchecked(now - lastTick) > MaxGapMs) kbBuf.Length = 0; // too slow: a person typing

            if (c == '\r' || c == '\n')
            {
                if (kbBuf.Length >= MinLength)
                {
                    int avg = unchecked(now - firstTick) / kbBuf.Length;
                    string code = kbBuf.ToString();
                    kbBuf.Length = 0;
                    if (avg <= MaxGapMs) { Raise(code, "Keyboard/HID"); return true; }
                }
                kbBuf.Length = 0;
                return false;
            }
            if (c < 32) { kbBuf.Length = 0; return false; }

            if (kbBuf.Length == 0) firstTick = now;
            kbBuf.Append(c);
            lastTick = now;
            return true; // swallow: nothing editable has focus anyway
        }

        static bool IsTextInputFocused()
        {
            Control c = Control.FromChildHandle(GetFocus());
            if (c == null) return false;
            if (c is TextBoxBase) return !((TextBoxBase)c).ReadOnly;
            ComboBox cb = c as ComboBox;
            if (cb != null) return cb.DropDownStyle != ComboBoxStyle.DropDownList;
            return c is NumericUpDown || c is DateTimePicker;
        }

        // ---------------- Serial (SPP) ----------------

        public static string[] PortNames()
        {
            try
            {
                string[] p = SerialPort.GetPortNames();
                Array.Sort(p, StringComparer.OrdinalIgnoreCase);
                return p;
            }
            catch (Exception) { return new string[0]; }
        }

        /// <summary>Opens the port on a worker thread (Bluetooth ports can take seconds) and reports back on the UI thread.</summary>
        public void OpenSerialAsync(string name, int baud, Action<bool, string> done)
        {
            CloseSerial();
            SerialStatus = "Connecting to " + name + "...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok; string msg;
                try
                {
                    SerialPort p = new SerialPort(name, baud, Parity.None, 8, StopBits.One);
                    p.Handshake = Handshake.None;
                    p.DtrEnable = true;
                    p.RtsEnable = true;
                    p.Encoding = Encoding.ASCII;
                    p.DataReceived += OnSerialData;
                    p.Open();
                    port = p;
                    ok = true; msg = "Connected to " + name + " @ " + baud;
                }
                catch (Exception ex)
                {
                    ok = false; msg = "Could not open " + name + ": " + ex.Message;
                }
                SerialStatus = msg;
                if (done != null && invoker.IsHandleCreated)
                    invoker.BeginInvoke(new Action(delegate { done(ok, msg); }));
            });
        }

        public void CloseSerial()
        {
            SerialPort p = port;
            port = null;
            if (p != null)
            {
                try { p.DataReceived -= OnSerialData; if (p.IsOpen) p.Close(); p.Dispose(); } catch (Exception) { }
            }
            SerialStatus = "Not connected";
        }

        void OnSerialData(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort p = sender as SerialPort;
            string chunk;
            try { chunk = p.ReadExisting(); } catch (Exception) { return; }
            List<string> codes = new List<string>();
            lock (serialBuf)
            {
                foreach (char c in chunk)
                {
                    if (c == '\r' || c == '\n' || c == '\t')
                    {
                        if (serialBuf.Length > 0) { codes.Add(serialBuf.ToString()); serialBuf.Length = 0; }
                    }
                    else if (c >= 32 && c <= 126) serialBuf.Append(c);
                }
                // Some scanners send no terminator: flush after 150 ms of silence.
                if (idleFlush == null) idleFlush = new System.Threading.Timer(FlushSerial, null, Timeout.Infinite, Timeout.Infinite);
                idleFlush.Change(serialBuf.Length > 0 ? 150 : Timeout.Infinite, Timeout.Infinite);
            }
            foreach (string code in codes) Raise(code, "Serial");
        }

        void FlushSerial(object state)
        {
            string code = null;
            lock (serialBuf)
            {
                if (serialBuf.Length > 0) { code = serialBuf.ToString(); serialBuf.Length = 0; }
            }
            if (code != null) Raise(code, "Serial");
        }

        void Raise(string code, string source)
        {
            ScanHandler h = Scanned;
            code = (code ?? "").Trim();
            if (h == null || code.Length == 0) return;
            if (invoker.InvokeRequired)
            {
                if (invoker.IsHandleCreated) invoker.BeginInvoke(new Action(delegate { h(code, source); }));
            }
            else h(code, source);
        }

        public void Dispose()
        {
            CloseSerial();
            if (idleFlush != null) idleFlush.Dispose();
        }
    }
}
