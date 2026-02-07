using ControlzEx.Standard;
using GTAFingerprinterCore.Interfaces;
using GTAFingerprinterCore.Pages;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Background;

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
        private static extern IntPtr GetWindowDC(IntPtr ptr);

        [DllImport("user32.dll", EntryPoint = "ReleaseDC")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("User32.dll", EntryPoint = "GetWindowRect")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("User32.dll", EntryPoint = "GetClientRect")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("User32.dll", EntryPoint = "ClientToScreen")]
        private static extern bool ClientToScreen(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        private static Bitmap CaptureDesktopWindow()
        {
            var hWnd = GetDesktopWindow();
            var hDC = GetDC(hWnd);
            var hMemDC = CreateCompatibleDC(hDC);
            GetClientRect(hWnd, out RECT rect);

            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;
            var m_HBitmap = CreateCompatibleBitmap(hDC, width, height);
            if (m_HBitmap != IntPtr.Zero)
            {
                var hOld = SelectObject(hMemDC, m_HBitmap);
                BitBlt(hMemDC, 0, 0, width, height, hDC, 0, 0, SRCCOPY | CAPTUREBLT);
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
            return CaptureDesktopWindow();
        }

        public Bitmap CaptureWindow(string title)
        {
            var hWnd = FindWindow(null, title);
            if (hWnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("指定截图的窗口不存在");
            }
            var desktopBitmap = CaptureDesktopWindow();

            GetClientRect(hWnd, out RECT rect);
            
            var width = rect.right - rect.left;
            var height = rect.bottom - rect.top;
            var border = width - height * 16 / 9; // 黑边
            var adjWidth = width - border;

            var bitmap = new Bitmap(adjWidth, height);
            ClientToScreen(hWnd, out RECT screenPos);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(desktopBitmap, new Rectangle(0, 0, bitmap.Width, height),
                      new Rectangle(screenPos.left + border / 2, screenPos.top, adjWidth, height),
                      GraphicsUnit.Pixel);
            }
            return bitmap;
        }
    }
}