using GTAFingerprinterCore.Configurations;
using GTAFingerprinterCore.Extensions;
using GTAFingerprinterCore.Implementions;
using GTAFingerprinterCore.Interfaces;
using GTAFingerprinterCore.Shared;
using Microsoft.WindowsAPICodePack.Dialogs;
using PropertyChanged;
using Stylet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace GTAFingerprinterCore.Pages
{
    [AddINotifyPropertyChangedInterface]
    public class RootViewModel : Screen
    {
        public AppConfig AppConfig { get; private set; }
        public BitmapImage Main { get; private set; }
        public IList<BitmapImage> Subs { get; private set; }
        public ObservableCollection<string> OperationHistories { get; } = new ObservableCollection<string>();
        public IList<Keys> Keys { get; } = Enum.GetValues(typeof(Keys)).Cast<Keys>().ToList();
        public IKeyboard Keyboard { get; }
        public IMouse Mouse { get; }
        public int TabIndex { get; set; }

        private readonly IDiamondFingerprinter _diamondFingerprinter;
        private readonly IPericoFingerprinter _pericoFingerprinter;
        private readonly IWindowManager _windowManager;
        private IntPtr _handle;
        private bool _processing = false;
        private readonly object _processingLockObj = new object();

        public RootViewModel(AppConfig appConfig, IDiamondFingerprinter diamondFingerprinter, IWindowManager windowManager, IKeyboard keyboard, IMouse mouse, IPericoFingerprinter pericoFingerprinter)
        {
            AppConfig = appConfig;
            _diamondFingerprinter = diamondFingerprinter;
            _windowManager = windowManager;
            Keyboard = keyboard;
            Mouse = mouse;
            _pericoFingerprinter = pericoFingerprinter;
        }

        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();
            _handle = new WindowInteropHelper(View as Window).Handle;

            // 获取命令行参数
            string[] args = Environment.GetCommandLineArgs();

            // 跳过第一个参数（程序路径）
            if (args.Length > 1)
            {
                string command = args[1].ToLower();

                switch (command)
                {
                    case "quelldiamond":
                        TabIndex = 0;
                        InitializeHotKey("RecognizeKey", AppConfig.RecognizeKey, Recognize);
                        break;
                    case "quellperico":
                        TabIndex = 1;
                        InitializeHotKey("RecognizeKey", AppConfig.RecognizeKey, Recognize);
                        break;
                    default:
                        // 因为界面隐藏，我们直接写入日志
                        File.AppendAllText("error.log", $"{DateTime.Now}: 无效的命令行参数: {command}\n");
                        Application.Current.Shutdown();
                        break;
                }
            }
            else
            {
                File.AppendAllText("error.log", $"{DateTime.Now}: 缺少命令行参数\n");
                Application.Current.Shutdown();
            }
        }

        private void InitializeHotKey(string name, Keys key, Action<HotKey> action)
        {
            try
            {
                if (!Keyboard.HotKeys.ContainsKey(name))
                {
                    var hotkey = new HotKey(key, _handle);
                    hotkey.HotKeyReleased += action;
                    Keyboard.HotKeys[name] = hotkey;
                    AppendHistory($"热键 {key} 注册成功");
                }
            }
            catch (Exception e)
            {
                AppendHistory($"热键注册失败: {e.Message}");
                File.AppendAllText("hotkey_error.log", $"{DateTime.Now}: 热键注册失败 - {e}\n");
            }
        }

        public void SaveConfig()
        {
            try
            {
                AppConfig.SaveConfig(Bootstrapper.configFilePath);
            }
            catch (Exception e)
            {
                File.AppendAllText("error.log", $"{DateTime.Now}: 保存配置失败 - {e}\n");
            }
        }

        public void RecognizeKeyChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Keyboard.HotKeys.ContainsKey("RecognizeKey"))
                {
                    Keyboard.HotKeys["RecognizeKey"].Dispose();
                    Keyboard.HotKeys.Remove("RecognizeKey");
                }

                InitializeHotKey("RecognizeKey", AppConfig.RecognizeKey, Recognize);
            }
            catch (Exception ex)
            {
                AppendHistory($"热键更改失败: {ex.Message}");
            }
        }

        private void AppendHistory(string message)
        {
            Debug.WriteLine(message);
        }

        private async void Recognize(HotKey k)
        {
            AppendHistory("热键触发，开始识别...");

            if (_processing)
            {
                AppendHistory("正在处理中，跳过本次操作");
                return;
            }

            try
            {
                lock (_processingLockObj)
                {
                    _processing = true;
                }

                IFingerprinter fingerprinter;
                ImgConfig mainConfig;
                ImgConfig subConfig;

                if (TabIndex == 0)
                {
                    fingerprinter = _diamondFingerprinter;
                    mainConfig = AppConfig.Diamond.MainImg;
                    subConfig = AppConfig.Diamond.SubImg;
                    AppendHistory("使用钻石赌场指纹识别器");
                }
                else if (TabIndex == 1)
                {
                    fingerprinter = _pericoFingerprinter;
                    mainConfig = AppConfig.Perico.MainImg;
                    subConfig = AppConfig.Perico.SubImg;
                    AppendHistory("使用佩里科岛指纹识别器");
                }
                else
                {
                    return;
                }

                // 捕获屏幕
                AppendHistory("正在截图...");
                var img = await fingerprinter.CaptureGameScreenAsync(AppConfig.IsFullScreen);

                // 裁剪大指纹
                AppendHistory("裁剪大指纹...");
                var main = await fingerprinter.CutBigAsync(img, mainConfig);

                // 裁剪小指纹
                AppendHistory("裁剪小指纹...");
                var subs = await fingerprinter.CutSubsAsync(img, subConfig);

                // 识别
                AppendHistory("开始识别...");
                var corrects = await fingerprinter.RecognizeAsync(main, subs, AppConfig.Similarity);

                if (corrects == null)
                {
                    AppendHistory("未识别到有效结果");
                    return;
                }

                AppendHistory($"识别结果: {string.Join(",", corrects)}");

                // 自动按键
                AppendHistory($"开始自动按键，延迟: {AppConfig.KeyPressDelay}ms");
                await fingerprinter.AutoPressKeysAsync(corrects, AppConfig.KeyPressDelay);
                AppendHistory("按键完成");

                // 清理资源
                main.Dispose();
                foreach (var sub in subs)
                {
                    sub.Dispose();
                }
                img.Dispose();

            }
            catch (Exception e)
            {
                AppendHistory($"识别过程中出错: {e.Message}");
                File.AppendAllText("error.log", $"{DateTime.Now}: 识别错误 - {e}\n");
            }
            finally
            {
                lock (_processingLockObj)
                {
                    _processing = false;
                }
            }
        }

        protected override void OnClose()
        {
            base.OnClose();

            // 清理所有热键
            if (Keyboard.HotKeys != null)
            {
                foreach (var hotkey in Keyboard.HotKeys.Values)
                {
                    hotkey.Dispose();
                }
                Keyboard.HotKeys.Clear();
            }

            AppendHistory("程序退出");
        }
    }
}