using ControlzEx.Standard;
﻿using GTAFingerprinterCore.Shared;
using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;

namespace GTAFingerprinterCore.Implementions
{
    public sealed class HotKey : IDisposable
    {
        public event Action<HotKey> HotKeyReleased;

        private IntPtr _hookId = IntPtr.Zero;
        private readonly Keys _key;
        private readonly IntPtr _handle;
        private bool _isKeyPressed = false;
        private LowLevelKeyboardProc _hookProc;
        private static readonly object _syncRoot = new object();
        private DateTime _lastKeyEventTime = DateTime.Now;
        private Timer _healthCheckTimer;
        private bool _isDisposed = false;
        private int _rehookAttempts = 0;
        private const int MAX_REHOOK_ATTEMPTS = 3;
        private const int HEALTH_CHECK_INTERVAL = 2000; // 2秒检查一次
        private const int EVENT_TIMEOUT = 5000; // 5秒无事件认为钩子失效

        public HotKey(Keys key, IntPtr handle)
        {
            _key = key;
            _handle = handle;
            _hookProc = HookCallback;
            InstallHook();
            StartHealthCheck();
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
            lock (_syncRoot)
            {
                if (_hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }

                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc,
                        GetModuleHandle(curModule.ModuleName), 0);

                    if (_hookId == IntPtr.Zero)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        Debug.WriteLine($"安装键盘钩子失败，错误代码: {errorCode}");
                        throw new ApplicationException($"无法安装键盘钩子，错误代码: {errorCode}");
                    }
                }

                _lastKeyEventTime = DateTime.Now;
                _rehookAttempts = 0;
                Debug.WriteLine($"键盘钩子安装成功 (Key: {_key}, HookId: {_hookId})");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // 更新最后事件时间
                _lastKeyEventTime = DateTime.Now;

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
                            // 确保在UI线程上触发事件
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                HotKeyReleased?.Invoke(this);
                            });
                            _isKeyPressed = false;
                            Debug.WriteLine("回传键盘信息");
                        }
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void StartHealthCheck()
        {
            _healthCheckTimer = new Timer(HealthCheckCallback, null,
                HEALTH_CHECK_INTERVAL, HEALTH_CHECK_INTERVAL);
        }

        private void HealthCheckCallback(object state)
        {
            if (_isDisposed)
                return;

            try
            {
                // 检查钩子是否可能失效
                CheckHookHealth();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"健康检查异常: {ex.Message}");
            }
        }

        private void CheckHookHealth()
        {
            // 如果钩子无效或正在轮询，跳过检查
            if (_polling || _isDisposed || _hookId == IntPtr.Zero)
                return;

            // 如果太久没有收到键盘事件，可能钩子已失效
            TimeSpan timeSinceLastEvent = DateTime.Now - _lastKeyEventTime;

            if (timeSinceLastEvent.TotalMilliseconds > EVENT_TIMEOUT)
            {
                Debug.WriteLine($"钩子可能已失效，最后事件时间: {_lastKeyEventTime}, 已过去: {timeSinceLastEvent.TotalMilliseconds}ms");

                // 尝试重新安装钩子
                if (_rehookAttempts < MAX_REHOOK_ATTEMPTS)
                {
                    _rehookAttempts++;
                    Debug.WriteLine($"尝试重新安装钩子，第 {_rehookAttempts} 次尝试");

                    try
                    {
                        // 在主线程上重新安装钩子
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            ReinstallHook();
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"重新安装钩子失败: {ex.Message}");

                        // 如果多次重试失败，可以尝试使用备用方案
                        if (_rehookAttempts >= MAX_REHOOK_ATTEMPTS)
                        {
                            Debug.WriteLine($"已达到最大重试次数，将使用备用轮询方案");
                            StartBackupPolling();
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"已达到最大重试次数，将使用备用轮询方案");
                    StartBackupPolling();
                }
            }
            else
            {
                // 正常，重置重试计数
                _rehookAttempts = 0;
            }
        }


        private void ReinstallHook()
        {
            Debug.WriteLine("重新安装键盘钩子...");
            InstallHook();
        }

        // 备用方案：使用轮询检测按键
        private Thread _pollingThread;
        private volatile bool _polling = false;

        private void StartBackupPolling()
        {
            if (_polling)
                return;

            _polling = true;
            _pollingThread = new Thread(() =>
            {
                bool wasPressed = false;

                while (_polling && !_isDisposed)
                {
                    bool isPressed = GetAsyncKeyState(_key) != false;

                    if (wasPressed && !isPressed)
                    {
                        // 键被释放
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            HotKeyReleased?.Invoke(this);
                        });
                    }

                    wasPressed = isPressed;
                    Thread.Sleep(10); // 10ms轮询间隔
                }
            })
            {
                IsBackground = true,
                Name = "HotKeyBackupPolling"
            };

            _pollingThread.Start();
            Debug.WriteLine("已启动备用轮询方案");
        }

        private void StopBackupPolling()
        {
            _polling = false;
            _pollingThread?.Join(1000);
            _pollingThread = null;
        }

        // 检查钩子是否有效
        public bool IsHookValid()
        {
            TimeSpan timeSinceLastEvent = DateTime.Now - _lastKeyEventTime;
            return timeSinceLastEvent.TotalMilliseconds < EVENT_TIMEOUT;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;

            StopBackupPolling();

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                Debug.WriteLine("键盘钩子已卸载");
            }

            _hookProc = null;
            GC.SuppressFinalize(this);
        }
    }
}