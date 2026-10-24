// Scanlines plugin for CSpect.
// CSpect 3 no longer has a TV shader (+tv is still in its ReadMe but does nothing). This plugin draws
// the scanlines in a transparent, click-through window kept exactly over the CSpect window, so it does
// not depend on the emulator's renderer.
//
// Enabled when CSpect is started with +tv. Keys (while CSpect has the focus):
//   Ctrl+F1                     scanlines on/off
//   Ctrl+PageUp / Ctrl+PageDown darker / lighter lines
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Plugin;

namespace Scanlines
{
    public class ScanlinesPlugin : iPlugin
    {
        iCSpect CSpect;
        IntPtr cspectWindow;
        Thread uiThread;
        OverlayForm overlay;

        public List<sIO> Init(iCSpect _CSpect)
        {
            CSpect = _CSpect;
            bool enabled = false;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, "+tv", StringComparison.OrdinalIgnoreCase)) enabled = true;
            }
            try { cspectWindow = (IntPtr)CSpect.GetGlobal(eGlobal.window_handle); } catch { cspectWindow = IntPtr.Zero; }
            Console.WriteLine(" Scanlines added (" + (enabled ? "on" : "off, start CSpect with +tv") + ")");

            // The overlay has its own UI thread, so it never waits for the emulation.
            uiThread = new Thread(() =>
            {
                overlay = new OverlayForm(cspectWindow, enabled);
                Application.Run(overlay);
            });
            uiThread.IsBackground = true;
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
            return new List<sIO>();
        }

        public void Quit()
        {
            OverlayForm f = overlay;
            if (f != null && f.IsHandleCreated)
            {
                try { f.BeginInvoke(new Action(f.Close)); } catch { }
            }
        }

        public void Tick() { }
        public void OSTick() { }
        public void Reset() { }
        public bool KeyPressed(int _id) { return false; }
        public bool Write(eAccess _type, int _port, int _id, byte _value) { return false; }
        public byte Read(eAccess _type, int _port, int _id, out bool _isvalid) { _isvalid = false; return 0; }
    }

    class OverlayForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

        const int GWL_HWNDPARENT = -8;
        const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        const int VK_CONTROL = 0x11, VK_F1 = 0x70, VK_PRIOR = 0x21, VK_NEXT = 0x22;

        // ZX Spectrum Next display height (lines), border included; below 2x there is no room for lines
        const int SourceLines = 256;

        IntPtr target;
        bool enabled;
        double darkness = 0.4;
        bool ownerSet;
        System.Windows.Forms.Timer timer;
        bool f1Down, upDown, downDown;

        public OverlayForm(IntPtr cspectWindow, bool startEnabled)
        {
            target = cspectWindow;
            enabled = startEnabled;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;   // the gaps between the lines are fully transparent
            Opacity = darkness;                // the lines are semi-transparent black
            Bounds = new Rectangle(-32000, -32000, 1, 1);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 30;
            timer.Tick += (s, e) => Follow();
            timer.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        void Follow()
        {
            if (target == IntPtr.Zero || !IsWindow(target))
            {
                target = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (target == IntPtr.Zero) { Visible = false; return; }
                ownerSet = false;
            }
            if (!ownerSet && IsHandleCreated)
            {
                // Owned by CSpect: always just above it, minimised and closed with it
                if (IntPtr.Size == 8) SetWindowLongPtr64(Handle, GWL_HWNDPARENT, target);
                else SetWindowLong32(Handle, GWL_HWNDPARENT, target.ToInt32());
                ownerSet = true;
            }

            CheckKeys();

            RECT r;
            if (!enabled || !IsWindowVisible(target) || IsIconic(target) || !GetClientRect(target, out r) || r.Bottom < 2 * SourceLines)
            {
                if (Visible) Visible = false;
                return;
            }
            POINT p = new POINT();
            ClientToScreen(target, ref p);
            Rectangle b = new Rectangle(p.X, p.Y, r.Right, r.Bottom);
            if (Bounds != b) { Bounds = b; Invalidate(); }
            if (!Visible) Visible = true;
        }

        void CheckKeys()
        {
            bool fg = GetForegroundWindow() == target;
            bool ctrl = fg && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool f1 = ctrl && (GetAsyncKeyState(VK_F1) & 0x8000) != 0;
            bool up = ctrl && (GetAsyncKeyState(VK_PRIOR) & 0x8000) != 0;
            bool down = ctrl && (GetAsyncKeyState(VK_NEXT) & 0x8000) != 0;
            if (f1 && !f1Down) enabled = !enabled;
            if (up && !upDown) SetDarkness(darkness + 0.1);
            if (down && !downDown) SetDarkness(darkness - 0.1);
            f1Down = f1; upDown = up; downDown = down;
        }

        void SetDarkness(double d)
        {
            darkness = Math.Max(0.1, Math.Min(0.9, d));
            Opacity = darkness;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Magenta);
            // One dark band per emulated line: the lower half of the pixels of each line
            double scale = (double)ClientSize.Height / SourceLines;
            int dark = Math.Max(1, (int)(scale / 2));
            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                for (int line = 0; line < SourceLines; line++)
                {
                    int y0 = (int)Math.Round(line * scale);
                    int y1 = (int)Math.Round((line + 1) * scale);
                    e.Graphics.FillRectangle(brush, 0, y1 - dark, ClientSize.Width, dark);
                }
            }
        }
    }
}
