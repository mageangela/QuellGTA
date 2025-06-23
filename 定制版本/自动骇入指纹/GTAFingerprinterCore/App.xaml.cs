using System;
using System.Runtime.InteropServices;
using System.Windows;
using GTAFingerprinterCore.Pages;
using Stylet;

namespace GTAFingerprinterCore
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        protected override void OnStartup(StartupEventArgs e)
        {
            // 强制重置为系统DPI感知
            SetProcessDPIAware();
            base.OnStartup(e);
        }
    }
}
