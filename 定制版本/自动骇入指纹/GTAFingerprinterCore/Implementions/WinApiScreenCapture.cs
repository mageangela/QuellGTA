using GTAFingerprinterCore.Interfaces;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GTAFingerprinterCore.Implementions
{
    public class WinApiScreenCapture : IScreenCapture
    {
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private const int SRCCOPY = 13369376;
        private const int CAPTUREBLT = 1073741824;

        [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
        private static extern IntPtr DeleteDC(IntPtr hDc);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        private static extern IntPtr DeleteObject(IntPtr hDc);

        [DllImport("gdi32.dll", EntryPoint = "BitBlt")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest, IntPtr hdcSource, int xSrc, int ySrc, int RasterOp);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr bmp);

        [DllImport("user32.dll", EntryPoint = "GetDC")]
        private static extern IntPtr GetDC(IntPtr ptr);

        [DllImport("user32.dll", EntryPoint = "GetWindowDC")]
        private static extern IntPtr GetWindowDC(Int32 ptr);

        [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("User32.dll", EntryPoint = "GetWindowRect")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("User32.dll", EntryPoint = "GetClientRect")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        private static Bitmap CaptureWindow(IntPtr hWnd)
        {
            var hDC = GetDC(hWnd);
            var hMemDC = CreateCompatibleDC(hDC);
            GetClientRect(hWnd, out RECT rECT);
            SIZE size;
            size.cx = rECT.right - rECT.left;
            size.cy = rECT.bottom - rECT.top;

            var m_HBitmap = CreateCompatibleBitmap(hDC, size.cx, size.cy);
            if (m_HBitmap != IntPtr.Zero)
            {
                var hOld = SelectObject(hMemDC, m_HBitmap);

                BitBlt(hMemDC, 0, 0, size.cx, size.cy, hDC, 0, 0, SRCCOPY | CAPTUREBLT);
                SelectObject(hMemDC, hOld);
                DeleteDC(hMemDC);
                ReleaseDC(hWnd, hDC);
                var img = Image.FromHbitmap(m_HBitmap);
                DeleteObject(m_HBitmap);
                return new Bitmap(img);
            }
            return null;
        }

        public Bitmap CaptureScreen()
        {
            return CaptureWindow(GetDesktopWindow());
        }

        public Bitmap CaptureWindow(string title)
        {
            var hWnd = FindWindow(null, title);
            if (hWnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("指定截图的窗口不存在");
            }
            return CaptureWindow(hWnd);
        }
    }
}