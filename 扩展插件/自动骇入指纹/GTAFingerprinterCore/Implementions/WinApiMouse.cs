using GTAFingerprinterCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Implementions
{
    public class WinApiMouse : IMouse
    {
        [Flags]
        private enum MouseEventFlags
        {
            Absolute = 0x00008000,
            Move = 0x00000001,
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            RightDown = 0x00000008,
            RightUp = 0x00000010,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040
        }
        public void LeftButtonClick(int delay = 20)
        {
            MouseEvent(MouseEventFlags.LeftDown);
            Task.Delay(delay).Wait();
            MouseEvent(MouseEventFlags.LeftUp);
            Task.Delay(delay).Wait();
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out MousePoint lpMousePoint);
        private MousePoint GetCursorPosition()
        {
            MousePoint currentMousePoint;
            var gotPoint = GetCursorPos(out currentMousePoint);
            if (!gotPoint) { currentMousePoint = new MousePoint(0, 0); }
            return currentMousePoint;
        }

        private void MouseEvent(MouseEventFlags value)
        {
            var position = GetCursorPosition();
            mouse_event((int)value, position.X, position.Y, 0, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MousePoint
        {
            public int X;
            public int Y;

            public MousePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
