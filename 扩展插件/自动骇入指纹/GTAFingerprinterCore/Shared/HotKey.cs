using GTAFingerprinterCore.Shared;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Threading;

namespace GTAFingerprinterCore.Implementions
{
    public sealed class HotKey : IDisposable
    {
        public event Action<HotKey> HotKeyReleased; // 改为松开触发

        private IntPtr _hookId = IntPtr.Zero;
        private readonly Keys _key;
        private readonly IntPtr _handle;
        private bool _isKeyPressed = false;
        private LowLevelKeyboardProc _hookProc;
        private static readonly object _syncRoot = new object();

        public HotKey(Keys key, IntPtr handle)
        {
            _key = key;
            _handle = handle;
            _hookProc = HookCallback;
            InstallHook();
        }

        ~HotKey()
        {
            Dispose();
        }

        public Keys Key => _key;

        #region Windows API

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetAsyncKeyState(Keys vKey);

        #endregion Windows API

        private void InstallHook()
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc,
                    GetModuleHandle(curModule.ModuleName), 0);

                if (_hookId == IntPtr.Zero)
                {
                    throw new ApplicationException("无法安装键盘钩子");
                }
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                // 检查是否为我们的目标键
                if ((Keys)vkCode == _key)
                {
                    // 键按下
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        _isKeyPressed = true;
                    }
                    // 键释放 - 触发事件
                    else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                    {
                        if (_isKeyPressed)
                        {
                            // 确保在UI线程上触发事件（如果需要）
                            if (System.Windows.Application.Current != null)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    HotKeyReleased?.Invoke(this);
                                });
                            }
                            else
                            {
                                HotKeyReleased?.Invoke(this);
                            }
                            _isKeyPressed = false;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // 可选：使用GetAsyncKeyState的替代方案
        public void StartPolling()
        {
            // 此方法使用轮询方式检测按键释放
            Thread pollThread = new Thread(() =>
            {
                bool wasPressed = false;

                while (_hookId != IntPtr.Zero)
                {
                    bool isPressed = GetAsyncKeyState(_key) != false;

                    if (wasPressed && !isPressed)
                    {
                        // 键被释放
                        HotKeyReleased?.Invoke(this);
                    }

                    wasPressed = isPressed;
                    Thread.Sleep(10); // 10ms轮询间隔
                }
            })
            {
                IsBackground = true
            };

            pollThread.Start();
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _hookProc = null;
            GC.SuppressFinalize(this);
        }
    }
}