using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Threading;
using GTAFingerprinterCore.Shared;

namespace GTAFingerprinterCore.Implementions
{
    public sealed class HotKey : IDisposable
    {
        public event Action<HotKey> HotKeyReleased;

        private readonly Keys _key;
        private bool _isKeyPressed = false;
        private bool _isDisposed = false;
        private bool _isRegistered = false;
        private readonly object _syncRoot = new object();
        private IntPtr _hwnd = IntPtr.Zero;
        private Thread _messageThread;
        private volatile bool _threadRunning = false;
        private string _className;

        #region Windows API

        private const int RIDEV_INPUTSINK = 0x00000100;
        private const int WM_INPUT = 0x00FF;
        private const int WM_DESTROY = 0x0002;
        private const int WM_QUIT = 0x0012;
        private const uint RID_INPUT = 0x10000003;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWKEYBOARD keyboard;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate _wndProcDelegate;

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetMessage(ref MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        public HotKey(Keys key, IntPtr handle = default)
        {
            _key = key;
            _wndProcDelegate = WndProc;
            StartMessageThread();
        }

        ~HotKey()
        {
            Dispose();
        }

        public Keys Key => _key;

        private void StartMessageThread()
        {
            _threadRunning = true;
            _messageThread = new Thread(MessageThreadProc)
            {
                IsBackground = true,
                Name = $"HotKeyMessageThread_{_key}"
            };
            _messageThread.Start();

            // 等待窗口创建完成
            int timeout = 5000;
            while (_hwnd == IntPtr.Zero && timeout > 0)
            {
                Thread.Sleep(10);
                timeout -= 10;
            }

            if (_hwnd == IntPtr.Zero)
            {
                throw new ApplicationException("消息窗口创建超时");
            }
        }

        private void MessageThreadProc()
        {
            try
            {
                _className = $"RawInputWindow_{Guid.NewGuid():N}";
                IntPtr hInstance = GetModuleHandle(null);

                WNDCLASSEX wndClass = new WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                    hInstance = hInstance,
                    lpszClassName = _className
                };

                if (RegisterClassEx(ref wndClass) == 0)
                {
                    return;
                }

                _hwnd = CreateWindowEx(
                    0,
                    _className,
                    "",
                    0, // 不可见窗口
                    0, 0, 0, 0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    hInstance,
                    IntPtr.Zero);

                if (_hwnd == IntPtr.Zero)
                {
                    return;
                }

                RegisterRawInput();

                MSG msg = new MSG();
                while (_threadRunning && GetMessage(ref msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            finally
            {
                if (_hwnd != IntPtr.Zero)
                {
                    DestroyWindow(_hwnd);
                    _hwnd = IntPtr.Zero;
                }
                _threadRunning = false;
            }
        }

        private void RegisterRawInput()
        {
            if (_hwnd == IntPtr.Zero) return;

            var devices = new RAWINPUTDEVICE[]
            {
                new RAWINPUTDEVICE
                {
                    usUsagePage = 0x01,
                    usUsage = 0x06,
                    dwFlags = RIDEV_INPUTSINK,
                    hwndTarget = _hwnd
                }
            };

            _isRegistered = RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT)
            {
                ProcessRawInput(lParam);
                return IntPtr.Zero;
            }
            else if (msg == WM_DESTROY)
            {
                PostQuitMessage();
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void PostQuitMessage()
        {
            if (_messageThread != null)
            {
                PostThreadMessage((uint)_messageThread.ManagedThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
        }

        private void ProcessRawInput(IntPtr hRawInput)
        {
            if (_isDisposed) return;

            try
            {
                uint size = 0;
                GetRawInputData(hRawInput, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                if (size == 0) return;

                IntPtr pData = Marshal.AllocHGlobal((int)size);
                try
                {
                    uint result = GetRawInputData(hRawInput, RID_INPUT, pData, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                    if (result == 0 || result == uint.MaxValue) return;

                    RAWINPUT rawInput = Marshal.PtrToStructure<RAWINPUT>(pData);
                    RAWKEYBOARD keyboard = rawInput.keyboard;

                    if ((Keys)keyboard.VKey == _key)
                    {
                        bool isKeyUp = (keyboard.Flags & 0x01) != 0;

                        if (!isKeyUp)
                        {
                            _isKeyPressed = true;
                        }
                        else if (isKeyUp && _isKeyPressed)
                        {
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                HotKeyReleased?.Invoke(this);
                            });
                            _isKeyPressed = false;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pData);
                }
            }
            catch
            {
                // 静默处理异常
            }
        }

        private void UnregisterRawInput()
        {
            try
            {
                if (_isRegistered)
                {
                    var devices = new RAWINPUTDEVICE[]
                    {
                        new RAWINPUTDEVICE
                        {
                            usUsagePage = 0x01,
                            usUsage = 0x06,
                            dwFlags = 0x00000000,
                            hwndTarget = IntPtr.Zero
                        }
                    };

                    RegisterRawInputDevices(
                        devices,
                        (uint)devices.Length,
                        (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));

                    _isRegistered = false;
                }

                _threadRunning = false;
                PostQuitMessage();

                if (_messageThread != null && _messageThread.IsAlive)
                {
                    _messageThread.Join(1000);
                    _messageThread = null;
                }
            }
            catch
            {
                // 静默处理异常
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            UnregisterRawInput();
            GC.SuppressFinalize(this);
        }
    }
}