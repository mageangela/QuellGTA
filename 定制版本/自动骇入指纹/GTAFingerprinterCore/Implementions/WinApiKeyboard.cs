using GTAFingerprinterCore.Configurations;
using GTAFingerprinterCore.Interfaces;
using GTAFingerprinterCore.Shared;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GTAFingerprinterCore.Implementions
{
    internal class WinApiKeyboard : IKeyboard
    {
        public const uint MAPVK_VK_TO_VSC = 0;

        public IDictionary<string, HotKey> HotKeys { get; } = new Dictionary<string, HotKey>();

        [DllImport("user32.dll", EntryPoint = "MapVirtualKey")]
        private extern static uint MapVirtualKey(
            Keys uCode,
            uint uMapType);

        [DllImport("user32.dll", EntryPoint = "keybd_event", SetLastError = true)]
#pragma warning disable IDE1006 // 命名样式
        private extern static void keybd_event(Keys bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

#pragma warning restore IDE1006 // 命名样式

        public async Task Press(Keys key, int count = 1, int delay = 20)
        {
            if (count == 0) return;
            uint extended = 0;
            if (key == Keys.Up || key == Keys.Down || key == Keys.Left || key == Keys.Right)
                extended = 1;
            var scan = (byte)MapVirtualKey(key, 0);
            for (int i = 0; i < count; i++)
            {
                keybd_event(key, scan, extended, 0);
                await Task.Delay(delay);
                keybd_event(key, scan, extended | 2, 0);
                await Task.Delay(delay);
            }
        }
    }
}