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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private readonly object _continuouslyClickingLockObj = new object();


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
                        // 初始化热键
                        InitializeHotKey("RecognizeKey", AppConfig.RecognizeKey, Recognize);
                        break;
                    case "quellperico":
                        TabIndex = 1;
                        // 初始化热键
                        InitializeHotKey("RecognizeKey", AppConfig.RecognizeKey, Recognize);
                        break;
                    default:
                        _windowManager.ShowMessageBox($"您没有指定应该运行的指纹模式，请使用QuellGTA或者指纹插件安装器启动。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        Application.Current.Shutdown();
                        break;
                }
            }
            else
            {
                _windowManager.ShowMessageBox($"您没有指定应该运行的指纹模式，请使用QuellGTA或者指纹插件安装器启动。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // 新增：初始化热键方法
        private void InitializeHotKey(string name, Keys key, Action<HotKey> action)
        {
            try
            {
                if (!Keyboard.HotKeys.ContainsKey(name))
                {
                    var hotkey = new HotKey(key, _handle);
                    hotkey.HotKeyReleased += action; // 改为HotKeyReleased
                    Keyboard.HotKeys[name] = hotkey;
                }
            }
            catch (Exception e)
            {
                _windowManager.ShowErrorMessageBox($"初始化热键失败: {e.Message}");
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
                _windowManager.ShowMessageBox($"保存配置失败\n{e.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RecognizeKeyChanged(object sender, SelectionChangedEventArgs e)
        {
            HotKeyChanged(nameof(AppConfig.RecognizeKey), AppConfig.RecognizeKey, Recognize);
        }

        private void HotKeyChanged(string name, Keys key, Action<HotKey> action)
        {
            try
            {
                if (Keyboard.HotKeys.ContainsKey(name))
                {
                    // 先注销旧热键
                    Keyboard.HotKeys[name].Dispose();
                    Keyboard.HotKeys.Remove(name);
                }

                // 创建新热键
                var hotkey = new HotKey(key, _handle);
                hotkey.HotKeyReleased += action; // 改为HotKeyReleased
                Keyboard.HotKeys[name] = hotkey;
            }
            catch (Exception e)
            {
                _windowManager.ShowErrorMessageBox($"热键更改失败: {e.Message}");
            }
        }

        private void AppendHistory(string message)
        {
            // 确保在UI线程上操作
            Execute.OnUIThread(() =>
            {
                if (OperationHistories.Count > 10)
                {
                    OperationHistories.Remove(OperationHistories.Last());
                }
                OperationHistories.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
            });
        }

        private async void Recognize(HotKey k)
        {
            if (_processing)
            {
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
                }
                else if (TabIndex == 1)
                {
                    fingerprinter = _pericoFingerprinter;
                    mainConfig = AppConfig.Perico.MainImg;
                    subConfig = AppConfig.Perico.SubImg;
                }
                else
                {
                    return;
                }
                var img = await fingerprinter.CaptureGameScreenAsync(AppConfig.IsFullScreen);
                AppendHistory("截图成功");
                var main = await fingerprinter.CutBigAsync(img, mainConfig);
                AppendHistory("裁剪大指纹成功");
                var subs = await fingerprinter.CutSubsAsync(img, subConfig);
                AppendHistory("裁剪小指纹成功");
                Main = main.ToBitmapImage();
                Subs = subs.Select(x => x.ToBitmapImage()).ToList();
                var corrects = await fingerprinter.RecognizeAsync(main, subs, AppConfig.Similarity);
                if (corrects == null)
                {
                    AppendHistory($"未识别到结果");
                    return;
                }
                AppendHistory($"识别结束，结果为{string.Join(",", corrects)}");
                await fingerprinter.AutoPressKeysAsync(corrects, AppConfig.KeyPressDelay);
                AppendHistory("按键成功");
                main.Dispose();
                foreach (var sub in subs)
                {
                    sub.Dispose();
                }
            }
            catch (Exception e)
            {
                _windowManager.ShowErrorMessageBox(e.Message);
            }
            finally
            {
                lock (_processingLockObj)
                {
                    _processing = false;
                }
            }
        }

        // 新增：在ViewModel销毁时清理热键
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
        }
    }
}