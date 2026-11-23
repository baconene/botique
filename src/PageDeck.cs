using System;
using System.Windows.Forms;

namespace BoutiquePOS
{
    // Preserve TabControl selection, keyboard navigation and page lifetime while
    // presenting the navigation as ordinary accessible buttons in MainForm.
    public class PageDeck : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            const int TcmAdjustRect = 0x1328;
            if (m.Msg == TcmAdjustRect && !DesignMode)
            {
                m.Result = (IntPtr)1;
                return;
            }
            base.WndProc(ref m);
        }
    }
}
